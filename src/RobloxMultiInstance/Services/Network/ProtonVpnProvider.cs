using System.IO;
using System.Text.RegularExpressions;

namespace RobloxMultiInstance.Services.Network;

/// <summary>
/// ProtonVPN — supports its 'protonvpn-cli' command on Windows when the
/// official client is installed with CLI support enabled. CLI surface is
/// less stable than Mullvad/Nord across versions.
/// </summary>
public class ProtonVpnProvider : IVpnProvider
{
    public string Name => "ProtonVPN";
    public string DownloadUrl => "https://protonvpn.com/download";
    public string? ExecutablePath { get; }

    private static readonly Random Rng = new();
    private static readonly string[] DefaultCountries = {
        "US", "CA", "UK", "DE", "SE", "NL", "CH", "FR", "NO", "ES", "IT", "JP", "SG",
    };

    private ProtonVpnProvider(string exe) { ExecutablePath = exe; }

    public static ProtonVpnProvider? TryDetect()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = new[]
        {
            Path.Combine(pf, "Proton", "VPN", "protonvpn-cli.exe"),
            Path.Combine(pfx86, "Proton", "VPN", "protonvpn-cli.exe"),
            Path.Combine(pf, "Proton VPN", "protonvpn-cli.exe"),
        };
        foreach (var p in candidates)
        {
            try { if (File.Exists(p)) return new ProtonVpnProvider(p); } catch { }
        }
        var fromPath = ProcessRunner.FindOnPath("protonvpn-cli.exe");
        return fromPath != null ? new ProtonVpnProvider(fromPath) : null;
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
        bool connected = text.Contains("Connected", StringComparison.OrdinalIgnoreCase)
                        && !text.Contains("Not connected", StringComparison.OrdinalIgnoreCase)
                        && !text.Contains("Disconnected", StringComparison.OrdinalIgnoreCase);
        string? location = null;
        var server = Regex.Match(text, @"Server:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        if (server.Success) location = server.Groups[1].Value.Trim();
        return new VpnStatus(connected, location, text);
    }

    public async Task<bool> ConnectAsync(string? locationId = null, CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var args = string.IsNullOrEmpty(locationId) ? "connect --fastest" : $"connect --cc {locationId}";
        var r = await ProcessRunner.RunAsync(ExecutablePath, args, ct, TimeSpan.FromSeconds(25));
        return r.ExitCode == 0;
    }

    public async Task<bool> DisconnectAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var r = await ProcessRunner.RunAsync(ExecutablePath, "disconnect", ct, TimeSpan.FromSeconds(10));
        return r.ExitCode == 0;
    }

    public async Task<bool> CycleAsync(CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
        var pick = DefaultCountries[Rng.Next(DefaultCountries.Length)];
        await DisconnectAsync(ct);
        await Task.Delay(500, ct);
        return await ConnectAsync(pick, ct);
    }

    public Task<IReadOnlyList<VpnLocation>> ListLocationsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<VpnLocation> list = DefaultCountries
            .Select(c => new VpnLocation(c, c))
            .ToList();
        return Task.FromResult(list);
    }
}
