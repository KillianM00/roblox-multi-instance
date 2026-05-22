using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using RobloxMultiInstance.Models;

namespace RobloxMultiInstance.Services;

[JsonSerializable(typeof(List<Account>))]
[JsonSerializable(typeof(Account))]
internal partial class AccountStoreJsonContext : JsonSerializerContext { }

public class AccountStore
{
    private static readonly string StoreDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxMultiInstance");
    private static readonly string StorePath = Path.Combine(StoreDir, "accounts.dat");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RobloxMultiInstance.v1");

    private readonly List<Account> _accounts = new();
    private readonly object _lock = new();
    private int _batchDepth;
    private bool _pendingSave;

    public IReadOnlyList<Account> Accounts
    {
        get { lock (_lock) return _accounts.ToList(); }
    }

    public void Load()
    {
        lock (_lock)
        {
            _accounts.Clear();
            if (!File.Exists(StorePath)) return;
            try
            {
                var encrypted = File.ReadAllBytes(StorePath);
                var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                var loaded = JsonSerializer.Deserialize(json, AccountStoreJsonContext.Default.ListAccount);
                if (loaded != null) _accounts.AddRange(loaded);
            }
            catch
            {
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            if (_batchDepth > 0)
            {
                _pendingSave = true;
                return;
            }
            WriteToDiskLocked();
        }
    }

    private void WriteToDiskLocked()
    {
        Directory.CreateDirectory(StoreDir);
        var json = JsonSerializer.SerializeToUtf8Bytes(_accounts, AccountStoreJsonContext.Default.ListAccount);
        var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(StorePath, encrypted);
        _pendingSave = false;
    }

    public IDisposable BeginBatch()
    {
        Interlocked.Increment(ref _batchDepth);
        return new BatchScope(this);
    }

    private void EndBatch()
    {
        bool shouldWrite = false;
        lock (_lock)
        {
            if (Interlocked.Decrement(ref _batchDepth) == 0 && _pendingSave)
                shouldWrite = true;
            if (shouldWrite) WriteToDiskLocked();
        }
    }

    private sealed class BatchScope : IDisposable
    {
        private AccountStore? _store;
        public BatchScope(AccountStore s) { _store = s; }
        public void Dispose()
        {
            var s = Interlocked.Exchange(ref _store, null);
            s?.EndBatch();
        }
    }

    public void Upsert(Account account)
    {
        lock (_lock)
        {
            var idx = _accounts.FindIndex(a => a.Id == account.Id);
            if (idx >= 0) _accounts[idx] = account;
            else _accounts.Add(account);
        }
        Save();
    }

    public void Remove(Guid id)
    {
        lock (_lock) _accounts.RemoveAll(a => a.Id == id);
        Save();
    }

    public Account? Find(Guid id)
    {
        lock (_lock) return _accounts.FirstOrDefault(a => a.Id == id);
    }
}
