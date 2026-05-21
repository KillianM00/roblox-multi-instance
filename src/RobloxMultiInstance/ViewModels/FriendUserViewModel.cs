using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxMultiInstance.Services;

namespace RobloxMultiInstance.ViewModels;

public partial class FriendStatusViewModel : ObservableObject
{
    public AccountItemViewModel Account { get; }

    [ObservableProperty] private string _status = "Idle";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isSuccess;

    public string Alias => Account.Alias;
    public string Username => Account.Username;

    public FriendStatusViewModel(AccountItemViewModel acc)
    {
        Account = acc;
    }
}

public partial class FriendUserViewModel : ObservableObject
{
    private readonly List<AccountItemViewModel> _allAccounts;

    [ObservableProperty] private string _input = "";
    [ObservableProperty] private string _lookupStatus = "";
    [ObservableProperty] private bool _isLookingUp;
    [ObservableProperty] private RobloxAuth.UserInfo? _target;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _onlySelected = true;

    public ObservableCollection<FriendStatusViewModel> Rows { get; } = new();

    public FriendUserViewModel(IEnumerable<AccountItemViewModel> accounts)
    {
        _allAccounts = accounts.ToList();
        RebuildRows();
    }

    partial void OnOnlySelectedChanged(bool value) => RebuildRows();

    private void RebuildRows()
    {
        Rows.Clear();
        var src = OnlySelected ? _allAccounts.Where(a => a.IsSelected) : _allAccounts;
        foreach (var a in src) Rows.Add(new FriendStatusViewModel(a));
    }

    [RelayCommand]
    private async Task LookupAsync()
    {
        var input = (Input ?? "").Trim();
        if (string.IsNullOrEmpty(input))
        {
            LookupStatus = "Enter a username or User ID.";
            Target = null;
            return;
        }
        IsLookingUp = true;
        LookupStatus = "Looking up...";
        RobloxAuth.UserInfo? info = null;
        if (long.TryParse(input, out var id))
            info = await App.RobloxAuth.LookupUserByIdAsync(id);
        else
            info = await App.RobloxAuth.LookupUserByNameAsync(input);
        IsLookingUp = false;
        if (info != null)
        {
            Target = info;
            LookupStatus = $"Found: {info.Name} (ID {info.Id})";
        }
        else
        {
            Target = null;
            LookupStatus = "No user found.";
        }
    }

    [RelayCommand]
    private async Task SendAllAsync()
    {
        if (Target == null)
        {
            LookupStatus = "Look up a user first.";
            return;
        }
        if (Rows.Count == 0)
        {
            LookupStatus = "No accounts to send from.";
            return;
        }
        IsSending = true;
        foreach (var row in Rows)
        {
            row.IsBusy = true;
            row.Status = "Sending...";
            var res = await App.RobloxAuth.SendFriendRequestAsync(row.Account.Model.Cookie, Target.Id);
            row.Status = res.Message;
            row.IsSuccess = res.Success;
            row.IsBusy = false;
        }
        IsSending = false;
    }
}
