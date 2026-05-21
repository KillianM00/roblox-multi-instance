using System.Threading;

namespace RobloxMultiInstance.Services;

public sealed class MutexHolder : IDisposable
{
    private const string MutexName = "ROBLOX_singletonEvent";
    private Mutex? _mutex;

    public bool IsHeld => _mutex != null;

    public bool Acquire()
    {
        if (_mutex != null) return true;
        try
        {
            _mutex = new Mutex(false, MutexName, out _);
            return true;
        }
        catch
        {
            _mutex = null;
            return false;
        }
    }

    public void Dispose()
    {
        try { _mutex?.Dispose(); } catch { }
        _mutex = null;
    }
}
