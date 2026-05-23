using System.Security.Cryptography;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxMultiInstance.Views;

namespace RobloxMultiInstance.ViewModels;

public partial class GenerateViewModel : ObservableObject
{
    private static readonly string[] Adjectives =
    {
        "Swift", "Quiet", "Bright", "Frosty", "Nimble", "Brave", "Sunny", "Crimson",
        "Silver", "Stormy", "Sleepy", "Witty", "Hidden", "Lucky", "Tame", "Bold",
        "Cosmic", "Iron", "Velvet", "Mellow", "Honest", "Quick", "Lazy", "Mild",
    };
    private static readonly string[] Nouns =
    {
        "Wolf", "Falcon", "Otter", "Panda", "Tiger", "Lynx", "Raven", "Comet",
        "Anchor", "Echo", "Maple", "Onyx", "Drift", "Frost", "Cinder", "Glade",
        "Hawk", "Marlin", "Quill", "Sable", "Whim", "Nova", "Pulse", "Vortex",
    };

    [ObservableProperty] private string _suggestedUsername = "";
    [ObservableProperty] private string _suggestedPassword = "";
    [ObservableProperty] private string _statusText = "Generate suggestions, then click Open Signup. Solve Roblox's CAPTCHA in the embedded browser; once signed up you'll land on /home and the new account is captured.";

    public GenerateViewModel()
    {
        RegenUsername();
        RegenPassword();
    }

    [RelayCommand]
    private void RegenUsername()
    {
        var rng = new Random();
        var adj = Adjectives[rng.Next(Adjectives.Length)];
        var noun = Nouns[rng.Next(Nouns.Length)];
        SuggestedUsername = $"{adj}{noun}{rng.Next(100, 9999)}";
    }

    [RelayCommand]
    private void RegenPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@$#%";
        var sb = new StringBuilder();
        var buf = new byte[1];
        using var rng = RandomNumberGenerator.Create();
        for (int i = 0; i < 16; i++)
        {
            rng.GetBytes(buf);
            sb.Append(chars[buf[0] % chars.Length]);
        }
        SuggestedPassword = sb.ToString();
    }

    [RelayCommand]
    private void CopyUsername()
    {
        try { Clipboard.SetText(SuggestedUsername); StatusText = "Username copied to clipboard."; } catch { }
    }

    [RelayCommand]
    private void CopyPassword()
    {
        try { Clipboard.SetText(SuggestedPassword); StatusText = "Password copied to clipboard."; } catch { }
    }

    [RelayCommand]
    private async Task OpenSignupAsync()
    {
        StatusText = "Opening Roblox signup in embedded browser...";
        var login = new LoginWindow
        {
            Owner = Application.Current.MainWindow,
            StartUrl = "https://www.roblox.com/CreateAccount",
            Heading = "Create a new Roblox account",
            SubHeading = "Fill in the signup form (paste the suggested username/password if you like) and solve the CAPTCHA. Once Roblox lands you on /home the new account's cookie is captured and the window closes.",
        };
        if (login.ShowDialog() != true || string.IsNullOrWhiteSpace(login.CapturedCookie))
        {
            StatusText = "Signup cancelled or didn't complete.";
            return;
        }

        var cookie = login.CapturedCookie!;
        StatusText = "Verifying new account...";
        var info = await App.RobloxAuth.GetUserInfoAsync(cookie);
        if (info == null)
        {
            StatusText = "Captured a cookie but couldn't fetch user info. Account not added.";
            return;
        }
        var avatar = await App.RobloxAuth.GetAvatarHeadshotUrlAsync(info.Id);
        App.AccountStore.Upsert(new Models.Account
        {
            Alias = info.Name,
            Cookie = cookie,
            Username = info.Name,
            DisplayName = info.DisplayName,
            UserId = info.Id,
            AvatarUrl = avatar,
            IsValid = true,
            LastValidatedAt = DateTime.UtcNow,
        });
        StatusText = $"Added {info.Name} (ID {info.Id}). Switch to the Accounts tab to use it.";
        RegenUsername();
        RegenPassword();
    }
}
