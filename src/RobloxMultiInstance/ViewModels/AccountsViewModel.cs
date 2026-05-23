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
    [ObservableProperty] private int _launchDelayMs = 25000;
    private const int RateLimitBackoffMs = 300_000;

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

        using (App.AccountStore.BeginBatch())
        {
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
    }

    private static readonly System.Text.RegularExpressions.Regex ShareLinkRegex = new(
        @"roblox\.com/share(?:\?|\?.*&)code=([A-Za-z0-9_-]+)(?:.*&type=([A-Za-z]+))?",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private async Task<(long? PlaceId, string? LinkCode, string? AccessCode)?> ResolveGameInputAsync(string cookieForResolution)
    {
        if (string.IsNullOrWhiteSpace(GameInput)) return (null, null, null);

        var share = ShareLinkRegex.Match(GameInput);
        if (share.Success)
        {
            var code = share.Groups[1].Value;
            var type = share.Groups[2].Success && !string.IsNullOrEmpty(share.Groups[2].Value)
                ? share.Groups[2].Value : "Server";

            // Try each selected account's cookie until one resolves — different accounts
            // may have different access to the link's owner / different rate-limit state.
            var cookies = new List<string> { cookieForResolution };
            foreach (var a in Accounts.Where(a => a.IsSelected && a.Model.Cookie != cookieForResolution))
                cookies.Add(a.Model.Cookie);

            string? lastErr = null;
            bool sawCookieValid = false;
            foreach (var c in cookies)
            {
                var result = await App.RobloxAuth.ResolveShareLinkAsync(c, code, type);
                if (result.Data != null)
                    return (result.Data.PlaceId, result.Data.PrivateServerLinkCode, null);
                lastErr = result.Error;

                // If the share-link endpoint rejects us, check whether the cookie
                // itself is even valid via /users/authenticated. Lets us distinguish
                // "bad cookie" from "endpoint blocks us specifically".
                if (!sawCookieValid && (lastErr?.Contains("401") ?? false))
                {
                    var info = await App.RobloxAuth.GetUserInfoAsync(c);
                    if (info != null) sawCookieValid = true;
                }
            }

            var diagnosis = sawCookieValid
                ? "At least one cookie is valid for general auth, but apis.roblox.com is rejecting the Server-link resolution specifically. This endpoint enforces extra checks (TLS fingerprint / device token) that block non-browser clients. Workaround: open the share URL in your browser, copy the redirected /games/.../?privateServerLinkCode=... URL from the address bar, paste THAT in the Game field instead."
                : "None of the selected accounts' cookies authenticated successfully — they may all be expired or rate-limited.";

            MessageBox.Show(
                $"Couldn't resolve the share link (type={type}).\n\nLast error: {lastErr ?? "(no details)"}\n\n{diagnosis}",
                "Share link resolution failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (RobloxLauncher.TryParseGameLink(GameInput, out var parsed) && parsed is not null)
            return (parsed.PlaceId, parsed.PrivateServerLinkCode, parsed.PrivateServerAccessCode);

        MessageBox.Show("Couldn't parse that as a Roblox game URL, Place ID, private server link, or share link.",
            "Invalid game", MessageBoxButton.OK, MessageBoxImage.Warning);
        return null;
    }

    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        var targets = Accounts.Where(a => a.IsSelected).ToArray();
        if (targets.Length == 0)
        {
            MessageBox.Show("Select at least one account (checkbox in the first column).",
                "No accounts selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var resolved = await ResolveGameInputAsync(targets[0].Model.Cookie);
        if (resolved == null) return;
        var (placeId, linkCode, accessCode) = resolved.Value;

        if (!App.MutexHolder.IsHeld) App.MutexHolder.Acquire();
        SwitchToInstancesTab();

        using (App.AccountStore.BeginBatch())
        {
            for (int i = 0; i < targets.Length; i++)
            {
                var t = targets[i];
                t.IsBusy = true;
                t.Status = "Launching...";
                var res = await App.RobloxLauncher.LaunchAsync(t.Model.Cookie, placeId, t.Model.Id, privateServerLinkCode: linkCode, privateServerAccessCode: accessCode);
                bool hit429 = !res.Success && (res.Error?.Contains("429") ?? false);
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

                if (i < targets.Length - 1)
                {
                    int wait = hit429
                        ? Math.Max(RateLimitBackoffMs, (res.RetryAfterSeconds ?? 90) * 1000)
                        : LaunchDelayMs;
                    if (hit429)
                        t.Status = $"429 — cooling down {wait / 1000}s before next account";
                    if (wait > 0) await Task.Delay(wait);
                }
            }
        }
    }

    [RelayCommand]
    private async Task FollowLeaderAsync(AccountItemViewModel? leader)
    {
        if (leader == null) return;
        if (leader.Model.UserId is not > 0)
        {
            MessageBox.Show("Validate this account first (Refresh Info) — its user ID is unknown.",
                "Cannot follow", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        leader.Status = "Checking presence...";
        leader.IsBusy = true;
        var presence = await App.RobloxAuth.GetPresenceAsync(leader.Model.Cookie, leader.Model.UserId.Value);
        leader.IsBusy = false;

        if (presence == null)
        {
            leader.Status = "Presence query failed";
            MessageBox.Show("Couldn't fetch presence for this account. The cookie may be expired.",
                "Presence failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!presence.InGame || string.IsNullOrEmpty(presence.GameId))
        {
            leader.Status = $"Not in a game (type {presence.Type})";
            MessageBox.Show(
                $"{leader.Alias} isn't currently in a game (presence type {presence.Type}).\nLaunch them into a game first, then try Follow again.",
                "Leader not in-game", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var followers = Accounts.Where(a => a.IsSelected && a.Id != leader.Id).ToArray();
        if (followers.Length == 0)
        {
            MessageBox.Show(
                "No other accounts selected. Tick the checkbox on at least one other account, then click Follow on the leader.",
                "Nothing to follow with", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!App.MutexHolder.IsHeld) App.MutexHolder.Acquire();
        SwitchToInstancesTab();

        leader.Status = $"Leading: {presence.LastLocation}";

        using (App.AccountStore.BeginBatch())
        {
            for (int i = 0; i < followers.Length; i++)
            {
                var f = followers[i];
                f.IsBusy = true;
                f.Status = $"Joining {presence.LastLocation}...";
                var res = await App.RobloxLauncher.LaunchAsync(
                    f.Model.Cookie,
                    presence.PlaceId,
                    f.Model.Id,
                    jobId: presence.GameId);
                bool hit429 = !res.Success && (res.Error?.Contains("429") ?? false);
                if (res.Success)
                {
                    f.Status = "Joined leader's server";
                    f.Model.LastUsedAt = DateTime.UtcNow;
                    App.AccountStore.Upsert(f.Model);
                    f.NotifyModelChanged();
                }
                else
                {
                    f.Status = res.Error ?? "Launch failed";
                }
                f.IsBusy = false;
                if (i < followers.Length - 1)
                {
                    int wait = hit429
                        ? Math.Max(RateLimitBackoffMs, (res.RetryAfterSeconds ?? 90) * 1000)
                        : LaunchDelayMs;
                    if (hit429)
                        f.Status = $"429 — cooling down {wait / 1000}s before next account";
                    if (wait > 0) await Task.Delay(wait);
                }
            }
        }
    }

    [RelayCommand]
    private async Task LaunchOneAsync(AccountItemViewModel? item)
    {
        if (item == null) return;

        var resolved = await ResolveGameInputAsync(item.Model.Cookie);
        if (resolved == null) return;
        var (placeId, linkCode, accessCode) = resolved.Value;

        if (!App.MutexHolder.IsHeld) App.MutexHolder.Acquire();
        SwitchToInstancesTab();

        item.IsBusy = true;
        item.Status = "Launching...";
        var res = await App.RobloxLauncher.LaunchAsync(item.Model.Cookie, placeId, item.Model.Id, privateServerLinkCode: linkCode, privateServerAccessCode: accessCode);
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
    private void KillOne(AccountItemViewModel? item)
    {
        item?.KillTrackedInstance();
    }

    [RelayCommand]
    private void KillAll()
    {
        var count = App.ProcessTracker.Instances.Count;
        if (count == 0)
        {
            MessageBox.Show("No Roblox instances are running.",
                "Nothing to kill", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var r = MessageBox.Show($"Kill all {count} running Roblox instance(s)?",
            "Confirm kill all", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        App.ProcessTracker.KillAll();
        foreach (var a in Accounts)
            if (a.Status == "Launched") a.Status = "Killed";
    }

    [RelayCommand]
    private void FriendUser()
    {
        var vm = new FriendUserViewModel(Accounts);
        var win = new FriendUserWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        win.ShowDialog();
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

    private static void SwitchToInstancesTab()
    {
        if (Application.Current?.MainWindow?.DataContext is MainWindowViewModel mwvm)
            mwvm.CurrentTab = "Instances";
    }
}
