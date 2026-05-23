using System.Windows.Threading;

namespace RobloxMultiInstance.Services;

public class AccountValidityMonitor
{
    private readonly DispatcherTimer _timer;
    private readonly RobloxAuth _auth;
    private readonly AccountStore _store;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _perAccountSpacing = TimeSpan.FromSeconds(3);
    private bool _running;

    public event Action<Guid, bool>? ValidityChanged;

    public AccountValidityMonitor(RobloxAuth auth, AccountStore store, TimeSpan? interval = null)
    {
        _auth = auth;
        _store = store;
        _interval = interval ?? TimeSpan.FromMinutes(30);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = _interval };
        _timer.Tick += async (_, _) => await CheckAllAsync();
    }

    public void Start()
    {
        if (!_timer.IsEnabled) _timer.Start();
        _ = Task.Run(async () => { await Task.Delay(15_000); await CheckAllAsync(); });
    }

    public void Stop() => _timer.Stop();

    public async Task CheckAllAsync()
    {
        if (_running) return;
        _running = true;
        try
        {
            var accounts = _store.Accounts.ToList();
            using (_store.BeginBatch())
            {
                foreach (var a in accounts)
                {
                    await ValidateInternalAsync(a);
                    if (_perAccountSpacing.TotalMilliseconds > 0)
                        await Task.Delay(_perAccountSpacing);
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    public async Task<bool> CheckOneAsync(Guid accountId)
    {
        var a = _store.Find(accountId);
        if (a == null) return false;
        return await ValidateInternalAsync(a);
    }

    private async Task<bool> ValidateInternalAsync(Models.Account a)
    {
        var info = await _auth.GetUserInfoAsync(a.Cookie);
        bool valid = info != null;
        var prev = a.IsValid;
        a.IsValid = valid;
        a.LastValidatedAt = DateTime.UtcNow;
        if (info != null)
        {
            a.Username = info.Name;
            a.DisplayName = info.DisplayName;
            a.UserId = info.Id;
        }
        _store.Upsert(a);
        if (prev != valid) ValidityChanged?.Invoke(a.Id, valid);
        return valid;
    }
}
