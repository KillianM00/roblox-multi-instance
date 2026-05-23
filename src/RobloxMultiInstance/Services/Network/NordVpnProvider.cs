using System.IO;
using System.Text.RegularExpressions;

namespace RobloxMultiInstance.Services.Network;

public class NordVpnProvider : IVpnProvider
{
    public string Name => "NordVPN";
    public string DownloadUrl => "https://nordvpn.com/download/";
    public string? ExecutablePath { get; }

    private static readonly Random Rng = new();
    private static readonly string[] DefaultCountries = {
        "United_States", "Canada", "United_Kingdom", "Germany", "Sweden",
        "Netherlands", "Switzerland", "France", "Norway", "Spain",
        "Italy", "Australia", "Japan", "Singapore", "Poland",
    };

    private NordVpnProvider(string exe) { ExecutablePath = exe; }

    public static NordVpnProvider? TryDetect()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = new[]
        {
            Path.Combine(pf, "NordVPN", "nordvpn.exe"),
            Path.Combine(pfx86, "NordVPN", "nordvpn.exe"),
        };
        foreach (var p in candidates)
        {
            try { if (File.Exists(p)) return new NordVpnProvider(p); } catch { }
        }
        var fromPath = ProcessRunner.FindOnPath("nordvpn.exe");
        return fromPath != null ? new NordVpnProvider(fromPath) : null;
    }

    public async Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return new VpnStatus(false, null, "Not installed");
        var r = await ProcessRunner.RunAsync(ExecutablePath, "-c -s", ct, TimeSpan.FromSeconds(8));
        var combined = r.Stdout + "\n" + r.Stderr;
        return ParseStatus(combined);
    }

    public static VpnStatus ParseStatus(string text)
    {
        text = text?.Trim() ?? string.Empty;
        // Examples (NordVPN CLI on Windows has different shapes across versions):
        //   "Status: Connected\nCurrent server: us1234.nordvpn.com\nCountry: United States\nCity: Atlanta"
        //   "Status: Disconnected"
        var status = Regex.Match(text, @"Status:\s*(\w+)", RegexOptions.IgnoreCase);
        bool connected = status.Success && status.Groups[1].Value.Equals("Connected", StringComparison.OrdinalIgnoreCase);
        string? location = null;
        var country = Regex.Match(text, @"Country:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        var city = Regex.Match(text, @"City:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        if (country.Success)
        {
            location = country.Groups[1].Value.Trim();
            if (city.Success) location += " / " + city.Groups[1].Value.Trim();
        }
        return new VpnStatus(connected, location, text);
    }

    public async Task<bool> ConnectAsync(string? locationId = null, CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var args = string.IsNullOrEmpty(locationId) ? "-c" : $"-c -g \"{locationId}\"";
        var r = await ProcessRunner.RunAsync(ExecutablePath, args, ct, TimeSpan.FromSeconds(20));
        return r.ExitCode == 0;
    }

    public async Task<bool> DisconnectAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var r = await ProcessRunner.RunAsync(ExecutablePath, "-d", ct, TimeSpan.FromSeconds(10));
        return r.ExitCode == 0;
    }

    public async Task<bool> CycleAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var status = await GetStatusAsync(ct);
        var pool = DefaultCountries
            .Where(c => status.Location == null || !status.Location.Contains(c.Replace("_", " "), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (pool.Length == 0) pool = DefaultCountries;
        var pick = pool[Rng.Next(pool.Length)];

        if (status.Connected) await DisconnectAsync(ct);
        await Task.Delay(500, ct);
        return await ConnectAsync(pick, ct);
    }

    public Task<IReadOnlyList<VpnLocation>> ListLocationsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<VpnLocation> list = DefaultCountries
            .Select(c => new VpnLocation(c, c.Replace("_", " ")))
            .ToList();
        return Task.FromResult(list);
    }
}
