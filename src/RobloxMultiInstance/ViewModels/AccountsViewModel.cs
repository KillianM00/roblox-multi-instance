using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxMultiInstance.Services;
using RobloxMultiInstance.Views;

namespace RobloxMultiInstance.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    public ObservableCollection<AccountItemViewModel> Accounts { get; } = new();

    [ObservableProperty] private string _gameInput = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private int _launchDelayMs = 1500;

    public AccountsViewModel()
    {
        ReloadAccounts();
    }

    private void ReloadAccounts()
    {
        Accounts.Clear();
        foreach (var a in App.AccountStore.Accounts)
            Accounts.Add(new AccountItemViewModel(a));
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var sel = Accounts.Count(a => a.IsSelected);
        StatusText = $"{Accounts.Count} account{(Accounts.Count == 1 ? "" : "s")} · {sel} selected";
    }

    [RelayCommand]
    private void AddAccount()
    {
        var vm = new AddAccountViewModel(null);
        var win = new AddAccountWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        if (win.ShowDialog() == true && vm.Result != null)
        {
            App.AccountStore.Upsert(vm.Result);
            ReloadAccounts();
        }
    }

    [RelayCommand]
    private async Task SignInWithBrowserAsync()
    {
        var login = new LoginWindow { Owner = Application.Current.MainWindow };
        if (login.ShowDialog() != true || string.IsNullOrWhiteSpace(login.CapturedCookie)) return;

        var cookie = login.CapturedCookie!;
        var info = await App.RobloxAuth.GetUserInfoAsync(cookie);
        if (info == null)
        {
            MessageBox.Show(
                "Captured a cookie but couldn't verify it with the Roblox API. The account was not added.",
                "Verification failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var avatar = await App.RobloxAuth.GetAvatarHeadshotUrlAsync(info.Id);

        var existing = App.AccountStore.Accounts.FirstOrDefault(a => a.UserId == info.Id);
        if (existing != null)
        {
            existing.Cookie = cookie;
            existing.Username = info.Name;
            existing.DisplayName = info.DisplayName;
            if (avatar != null) existing.AvatarUrl = avatar;
            App.AccountStore.Upsert(existing);
        }
        else
        {
            App.AccountStore.Upsert(new Models.Account
            {
                Alias = info.Name,
                Cookie = cookie,
                Username = info.Name,
                DisplayName = info.DisplayName,
                UserId = info.Id,
                AvatarUrl = avatar,
            });
        }

        ReloadAccounts();
    }

    [RelayCommand]
    private void EditAccount(AccountItemViewModel? item)
    {
        if (item == null) return;
        var vm = new AddAccountViewModel(item.Model);
        var win = new AddAccountWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        if (win.ShowDialog() == true && vm.Result != null)
        {
            App.AccountStore.Upsert(vm.Result);
            ReloadAccounts();
        }
    }

    [RelayCommand]
    private void DeleteAccount(AccountItemViewModel? item)
    {
        if (item == null) return;
        var result = MessageBox.Show(
            $"Delete account \"{item.Alias}\"?", "Confirm delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        App.AccountStore.Remove(item.Id);
        ReloadAccounts();
    }

    [RelayCommand]
    private async Task RefreshInfoAsync(AccountItemViewModel? item)
    {
        var targets = item != null
            ? new[] { item }
            : Accounts.Where(a => a.IsSelected).ToArray();
        if (targets.Length == 0) targets = Accounts.ToArray();

        foreach (var t in targets)
        {
            t.IsBusy = true;
            t.Status = "Checking...";
            var info = await App.RobloxAuth.GetUserInfoAsync(t.Model.Cookie);
            if (info != null)
            {
                t.Model.Username = info.Name;
                t.Model.DisplayName = info.DisplayName;
                t.Model.UserId = info.Id;
                var avatar = await App.RobloxAuth.GetAvatarHeadshotUrlAsync(info.Id);
                if (avatar != null) t.Model.AvatarUrl = avatar;
                App.AccountStore.Upsert(t.Model);
                t.Status = "OK";
            }
            else
            {
                t.Status = "Invalid/expired";
            }
            t.NotifyModelChanged();
            t.IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        if (!RobloxLauncher.TryParsePlaceId(GameInput, out var placeId))
        {
            MessageBox.Show("Enter a valid Roblox game URL or Place ID.", "Invalid game", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var targets = Accounts.Where(a => a.IsSelected).ToArray();
        if (targets.Length == 0)
        {
            MessageBox.Show("Select at least one account (checkbox in the first column).",
                "No accounts selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!App.MutexHolder.IsHeld) App.MutexHolder.Acquire();

        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            t.IsBusy = true;
            t.Status = "Launching...";
            var res = await App.RobloxLauncher.LaunchAsync(t.Model.Cookie, placeId, t.Model.Id);
            if (res.Success)
            {
                t.Status = "Launched";
                t.Model.LastUsedAt = DateTime.UtcNow;
                App.AccountStore.Upsert(t.Model);
                t.NotifyModelChanged();
            }
            else
            {
                t.Status = res.Error ?? "Launch failed";
            }
            t.IsBusy = false;

            if (i < targets.Length - 1 && LaunchDelayMs > 0)
                await Task.Delay(LaunchDelayMs);
        }
    }

    [RelayCommand]
    private async Task LaunchOneAsync(AccountItemViewModel? item)
    {
        if (item == null) return;
        if (!RobloxLauncher.TryParsePlaceId(GameInput, out var placeId))
        {
            MessageBox.Show("Enter a valid Roblox game URL or Place ID.", "Invalid game", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!App.MutexHolder.IsHeld) App.MutexHolder.Acquire();

        item.IsBusy = true;
        item.Status = "Launching...";
        var res = await App.RobloxLauncher.LaunchAsync(item.Model.Cookie, placeId, item.Model.Id);
        if (res.Success)
        {
            item.Status = "Launched";
            item.Model.LastUsedAt = DateTime.UtcNow;
            App.AccountStore.Upsert(item.Model);
            item.NotifyModelChanged();
        }
        else
        {
            item.Status = res.Error ?? "Launch failed";
        }
        item.IsBusy = false;
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var a in Accounts) a.IsSelected = true;
        UpdateStatus();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var a in Accounts) a.IsSelected = false;
        UpdateStatus();
    }

    public void OnAccountSelectionChanged() => UpdateStatus();
}
