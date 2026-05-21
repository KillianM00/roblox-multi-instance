using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RobloxMultiInstance.Models;

namespace RobloxMultiInstance.Services;

public class AccountStore
{
    private static readonly string StoreDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RobloxMultiInstance");
    private static readonly string StorePath = Path.Combine(StoreDir, "accounts.dat");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RobloxMultiInstance.v1");

    private readonly List<Account> _accounts = new();
    private readonly object _lock = new();

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
                var loaded = JsonSerializer.Deserialize<List<Account>>(json);
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
            Directory.CreateDirectory(StoreDir);
            var json = JsonSerializer.SerializeToUtf8Bytes(_accounts);
            var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StorePath, encrypted);
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
