using System.IO;
using System.Text.RegularExpressions;

namespace RobloxMultiInstance.Services.Network;

public class MullvadProvider : IVpnProvider
{
    public string Name => "Mullvad VPN";
    public string DownloadUrl => "https://mullvad.net/en/download/vpn/windows";
    public string? ExecutablePath { get; }

    private static readonly Random Rng = new();

    // Country codes Mullvad supports — small curated subset for the Cycle picker.
    private static readonly string[] DefaultCountries = {
        "us", "ca", "gb", "de", "se", "nl", "ch", "fr", "no", "fi",
        "es", "it", "au", "jp", "sg", "br", "mx", "pl", "cz", "ie",
    };

    private MullvadProvider(string exe) { ExecutablePath = exe; }

    public static MullvadProvider? TryDetect()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = new[]
        {
            Path.Combine(pf, "Mullvad VPN", "resources", "mullvad.exe"),
            Path.Combine(pfx86, "Mullvad VPN", "resources", "mullvad.exe"),
            Path.Combine(pf, "Mullvad VPN", "mullvad.exe"),
        };
        foreach (var p in candidates)
        {
            try { if (File.Exists(p)) return new MullvadProvider(p); } catch { }
        }
        var fromPath = ProcessRunner.FindOnPath("mullvad.exe");
        return fromPath != null ? new MullvadProvider(fromPath) : null;
    }

    public async Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return new VpnStatus(false, null, "Not installed");
        var r = await ProcessRunner.RunAsync(ExecutablePath, "status", ct, TimeSpan.FromSeconds(8));
        return ParseStatus(r.Stdout);
    }

    public static VpnStatus ParseStatus(string stdout)
    {
        var text = stdout?.Trim() ?? string.Empty;
        if (text.Length == 0) return new VpnStatus(false, null, "");
        // Examples:
        //   "Disconnected"
        //   "Connecting to wg in us-nyc-wg-301..."
        //   "Connected to wg in us-nyc-wg-301"
        //   "Tunnel status: Connected\nRelay: us-nyc-wg-301"
        bool connected = text.Contains("Connected", StringComparison.OrdinalIgnoreCase)
                        && !text.Contains("Disconnected", StringComparison.OrdinalIgnoreCase)
                        && !text.StartsWith("Disconnected", StringComparison.OrdinalIgnoreCase);
        string? location = null;
        var m = Regex.Match(text, @"in\s+([a-z]{2}(?:-[a-z0-9-]+)?)", RegexOptions.IgnoreCase);
        if (m.Success) location = m.Groups[1].Value;
        else
        {
            var m2 = Regex.Match(text, @"Relay:\s*([^\s\r\n]+)", RegexOptions.IgnoreCase);
            if (m2.Success) location = m2.Groups[1].Value;
        }
        return new VpnStatus(connected, location, text);
    }

    public async Task<bool> ConnectAsync(string? locationId = null, CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        if (!string.IsNullOrEmpty(locationId))
        {
            var rs = await ProcessRunner.RunAsync(ExecutablePath, $"relay set location {locationId}", ct, TimeSpan.FromSeconds(8));
            if (rs.ExitCode != 0 && !string.IsNullOrEmpty(rs.Stderr))
                return false;
        }
        var r = await ProcessRunner.RunAsync(ExecutablePath, "connect", ct, TimeSpan.FromSeconds(15));
        return r.ExitCode == 0;
    }

    public async Task<bool> DisconnectAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var r = await ProcessRunner.RunAsync(ExecutablePath, "disconnect", ct, TimeSpan.FromSeconds(8));
        return r.ExitCode == 0;
    }

    public async Task<bool> CycleAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var status = await GetStatusAsync(ct);
        var currentCountry = ExtractCountry(status.Location);
        var pool = DefaultCountries.Where(c => c != currentCountry).ToArray();
        if (pool.Length == 0) pool = DefaultCountries;
        var pick = pool[Rng.Next(pool.Length)];

        if (status.Connected) await DisconnectAsync(ct);
        await Task.Delay(500, ct);
        return await ConnectAsync(pick, ct);
    }

    public Task<IReadOnlyList<VpnLocation>> ListLocationsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<VpnLocation> list = DefaultCountries
            .Select(c => new VpnLocation(c, c.ToUpperInvariant()))
            .ToList();
        return Task.FromResult(list);
    }

    private static string? ExtractCountry(string? location)
    {
        if (string.IsNullOrEmpty(location)) return null;
        var m = Regex.Match(location, @"^([a-z]{2})", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
    }
}
