using System.Diagnostics;
using System.Windows.Threading;
using RobloxMultiInstance.Models;

namespace RobloxMultiInstance.Services;

public class RobloxProcessTracker
{
    private const string ProcessName = "RobloxPlayerBeta";

    private readonly Dictionary<int, RobloxInstance> _byPid = new();
    private readonly DispatcherTimer _timer;
    private readonly Queue<Guid> _pendingAssociations = new();
    private HashSet<int> _preLaunchPids = new();
    private DateTime _associationWindowExpires = DateTime.MinValue;

    public event Action? InstancesChanged;

    public IReadOnlyList<RobloxInstance> Instances
    {
        get { lock (_byPid) return _byPid.Values.OrderBy(i => i.SeenAt).ToList(); }
    }

    public RobloxProcessTracker()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(750),
        };
        _timer.Tick += (_, _) => Refresh();
    }

    public void Start() { if (!_timer.IsEnabled) _timer.Start(); }
    public void Stop() => _timer.Stop();

    public bool KillByPid(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int KillAll()
    {
        int killed = 0;
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            try { p.Kill(entireProcessTree: true); killed++; }
            catch { }
            finally { p.Dispose(); }
        }
        Refresh();
        return killed;
    }

    public void BeginLaunchAssociation(Guid accountId)
    {
        lock (_byPid)
        {
            _preLaunchPids = Process.GetProcessesByName(ProcessName).Select(p => p.Id).ToHashSet();
            _pendingAssociations.Enqueue(accountId);
            _associationWindowExpires = DateTime.UtcNow.AddSeconds(45);
        }
    }

    public RobloxInstance? FindByAccount(Guid accountId)
    {
        lock (_byPid) return _byPid.Values.LastOrDefault(i => i.AccountId == accountId);
    }

    public void Refresh()
    {
        var procs = Process.GetProcessesByName(ProcessName);
        var currentPids = new HashSet<int>();
        bool changed = false;

        try
        {
            foreach (var p in procs)
            {
                currentPids.Add(p.Id);
                RobloxInstance? inst;
                lock (_byPid)
                {
                    if (!_byPid.TryGetValue(p.Id, out inst))
                    {
                        inst = new RobloxInstance { Pid = p.Id };

                        if (_pendingAssociations.Count > 0
                            && !_preLaunchPids.Contains(p.Id)
                            && DateTime.UtcNow <= _associationWindowExpires)
                        {
                            inst.AccountId = _pendingAssociations.Dequeue();
                        }

                        _byPid[p.Id] = inst;
                        changed = true;
                    }
                }

                try
                {
                    p.Refresh();
                    var hwnd = p.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        if (hwnd != inst.Hwnd)
                        {
                            inst.Hwnd = hwnd;
                            changed = true;
                        }
                        var title = p.MainWindowTitle;
                        if (!string.IsNullOrEmpty(title) && title != inst.Title)
                        {
                            inst.Title = title;
                            changed = true;
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    p.Dispose();
                }
            }

            lock (_byPid)
            {
                foreach (var key in _byPid.Keys.ToList())
                {
                    if (!currentPids.Contains(key))
                    {
                        _byPid.Remove(key);
                        changed = true;
                    }
                }

                if (_pendingAssociations.Count > 0 && DateTime.UtcNow > _associationWindowExpires)
                    _pendingAssociations.Clear();
            }
        }
        finally
        {
            if (changed) InstancesChanged?.Invoke();
        }
    }
}
