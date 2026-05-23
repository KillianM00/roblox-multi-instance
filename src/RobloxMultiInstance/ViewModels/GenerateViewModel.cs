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

    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private int _birthMonth = 1;
    [ObservableProperty] private int _birthDay = 1;
    [ObservableProperty] private int _birthYear = 2000;
    [ObservableProperty] private int _count = 1;
    [ObservableProperty] private bool _randomUsernamePerAccount;
    [ObservableProperty] private string _statusText = "Enter a username + password, optionally adjust birthday, then click Create. Each iteration opens an embedded Roblox signup page with the fields pre-filled — you only need to solve the CAPTCHA and click Sign Up.";
    [ObservableProperty] private bool _isRunning;

    public IReadOnlyList<int> Months { get; } = Enumerable.Range(1, 12).ToList();
    public IReadOnlyList<int> Days { get; } = Enumerable.Range(1, 31).ToList();
    public IReadOnlyList<int> Years { get; } =
        Enumerable.Range(1950, Math.Max(1, DateTime.Now.Year - 13 - 1950 + 1)).ToList();

    public GenerateViewModel()
    {
        RegenUsername();
        RegenPassword();
    }

    [RelayCommand]
    private void RegenUsername() => Username = RandomUsername();

    [RelayCommand]
    private void RegenPassword() => Password = RandomPassword(16);

    [RelayCommand]
    private void CopyUsername()
    {
        try { Clipboard.SetText(Username); StatusText = "Username copied to clipboard."; } catch { }
    }

    [RelayCommand]
    private void CopyPassword()
    {
        try { Clipboard.SetText(Password); StatusText = "Password copied to clipboard."; } catch { }
    }

    [RelayCommand]
    private async Task CreateAccountsAsync()
    {
        if (IsRunning) return;
        if (string.IsNullOrWhiteSpace(Password))
        {
            StatusText = "Password is required.";
            return;
        }
        if (Password.Length < 8)
        {
            StatusText = "Roblox requires passwords of at least 8 characters.";
            return;
        }
        if (!RandomUsernamePerAccount && string.IsNullOrWhiteSpace(Username))
        {
            StatusText = "Enter a username, or check 'Random username per account'.";
            return;
        }

        int target = Math.Max(1, Math.Min(20, Count));
        var baseUsername = string.IsNullOrWhiteSpace(Username) ? RandomUsername() : Username.Trim();
        int added = 0;

        IsRunning = true;
        try
        {
            for (int i = 0; i < target; i++)
            {
                string user;
                if (RandomUsernamePerAccount)
                {
                    user = RandomUsername();
                }
                else if (target == 1)
                {
                    user = baseUsername;
                }
                else
                {
                    var suffix = $"_{i + 1}";
                    var maxBaseLen = 20 - suffix.Length;
                    var b = baseUsername.Length > maxBaseLen ? baseUsername[..maxBaseLen] : baseUsername;
                    user = b + suffix;
                }

                StatusText = $"Creating {i + 1} of {target}: {user} — solve the CAPTCHA and click Sign Up.";

                var login = new LoginWindow
                {
                    Owner = Application.Current.MainWindow,
                    StartUrl = "https://www.roblox.com/CreateAccount",
                    Heading = $"Sign up account {i + 1} of {target}",
                    SubHeading = $"Pre-filling username '{user}'. Solve the CAPTCHA, click Sign Up. The window closes automatically once Roblox lands on /home.",
                    PrefillUsername = user,
                    PrefillPassword = Password,
                    PrefillBirthMonth = BirthMonth,
                    PrefillBirthDay = BirthDay,
                    PrefillBirthYear = BirthYear,
                };

                if (login.ShowDialog() != true || string.IsNullOrWhiteSpace(login.CapturedCookie))
                {
                    StatusText = $"Stopped after {added} of {target} created (window closed or signup didn't complete).";
                    return;
                }

                var cookie = login.CapturedCookie!;
                var info = await App.RobloxAuth.GetUserInfoAsync(cookie);
                if (info == null)
                {
                    StatusText = $"Captured a cookie at step {i + 1} but couldn't verify it with Roblox. Skipping and continuing.";
                    continue;
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
                added++;
                StatusText = $"Added {info.Name} ({added}/{target}). Continuing...";

                if (i < target - 1) await Task.Delay(1500);
            }

            StatusText = $"Done — created {added} of {target} account(s).";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static string RandomUsername()
    {
        var rng = new Random();
        var adj = Adjectives[rng.Next(Adjectives.Length)];
        var noun = Nouns[rng.Next(Nouns.Length)];
        return $"{adj}{noun}{rng.Next(100, 9999)}";
    }

    private static string RandomPassword(int length)
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        var sb = new StringBuilder();
        var buf = new byte[1];
        using var rng = RandomNumberGenerator.Create();
        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(buf);
            sb.Append(chars[buf[0] % chars.Length]);
        }
        return sb.ToString();
    }
}
