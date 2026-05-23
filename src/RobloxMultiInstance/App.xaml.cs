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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ValidityMonitor.Stop();
        ProcessTracker.Stop();
        MutexHolder.Dispose();
        base.OnExit(e);
    }
}
