using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxMultiInstance.Models;

namespace RobloxMultiInstance.ViewModels;

public partial class AddAccountViewModel : ObservableObject
{
    private readonly Account? _existing;

    [ObservableProperty] private string _alias = "";
    [ObservableProperty] private string _cookie = "";
    [ObservableProperty] private string _validateStatus = "";
    [ObservableProperty] private bool _isValidating;
    [ObservableProperty] private bool _isValid;

    public Account? Result { get; private set; }
    public string Title => _existing == null ? "Add account" : "Edit account";

    public AddAccountViewModel(Account? existing)
    {
        _existing = existing;
        if (existing != null)
        {
            Alias = existing.Alias;
            Cookie = existing.Cookie;
        }
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        if (string.IsNullOrWhiteSpace(Cookie))
        {
            ValidateStatus = "Enter a cookie first.";
            IsValid = false;
            return;
        }
        IsValidating = true;
        ValidateStatus = "Checking...";
        var info = await App.RobloxAuth.GetUserInfoAsync(Cookie.Trim());
        IsValidating = false;
        if (info != null)
        {
            ValidateStatus = $"OK — {info.Name} (ID {info.Id})";
            IsValid = true;
            if (string.IsNullOrWhiteSpace(Alias)) Alias = info.Name;
        }
        else
        {
            ValidateStatus = "Invalid or expired cookie.";
            IsValid = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(System.Windows.Window? window)
    {
        if (string.IsNullOrWhiteSpace(Cookie))
        {
            ValidateStatus = "Cookie is required.";
            return;
        }
        var trimmed = Cookie.Trim();
        var info = await App.RobloxAuth.GetUserInfoAsync(trimmed);
        var alias = string.IsNullOrWhiteSpace(Alias) ? (info?.Name ?? "Account") : Alias.Trim();

        if (_existing != null)
        {
            _existing.Alias = alias;
            _existing.Cookie = trimmed;
            if (info != null)
            {
                _existing.Username = info.Name;
                _existing.DisplayName = info.DisplayName;
                _existing.UserId = info.Id;
            }
            Result = _existing;
        }
        else
        {
            Result = new Account
            {
                Alias = alias,
                Cookie = trimmed,
                Username = info?.Name,
                DisplayName = info?.DisplayName,
                UserId = info?.Id,
            };
        }

        if (window != null)
        {
            window.DialogResult = true;
            window.Close();
        }
    }

    [RelayCommand]
    private void Cancel(System.Windows.Window? window)
    {
        Result = null;
        if (window != null)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
