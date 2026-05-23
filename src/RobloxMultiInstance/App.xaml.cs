using System.Windows;
using RobloxMultiInstance.Services;

namespace RobloxMultiInstance;

public partial class App : Application
{
    public static MutexHolder MutexHolder { get; } = new();
    public static AccountStore AccountStore { get; } = new();
    public static RobloxAuth RobloxAuth { get; } = new();
    public static RobloxProcessTracker ProcessTracker { get; } = new();
    public static RobloxLauncher RobloxLauncher { get; } = new(RobloxAuth, ProcessTracker);
    public static AccountValidityMonitor ValidityMonitor { get; } = new(RobloxAuth, AccountStore);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MutexHolder.Acquire();
        AccountStore.Load();
        ProcessTracker.Start();
        ValidityMonitor.Start();

        // When Roblox rotates a .ROBLOSECURITY value on us mid-session, swap in the new
        // one so we keep using a fresh, server-extended cookie instead of the stale one.
        RobloxAuth.CookieRotated += OnCookieRotated;
    }

    private static void OnCookieRotated(string oldCookie, string newCookie)
    {
        var account = AccountStore.Accounts.FirstOrDefault(a => a.Cookie == oldCookie);
        if (account == null) return;
        account.Cookie = newCookie;
        account.LastValidatedAt = DateTime.UtcNow;
        account.IsValid = true;
        AccountStore.Upsert(account);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ValidityMonitor.Stop();
        ProcessTracker.Stop();
        MutexHolder.Dispose();
        base.OnExit(e);
    }
}
