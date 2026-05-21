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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MutexHolder.Acquire();
        AccountStore.Load();
        ProcessTracker.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ProcessTracker.Stop();
        MutexHolder.Dispose();
        base.OnExit(e);
    }
}
