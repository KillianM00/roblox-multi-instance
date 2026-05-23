using System.IO;
using System.Text.RegularExpressions;

namespace RobloxMultiInstance.Services.Network;

/// <summary>
/// Cloudflare WARP — free, no signup. Single global anycast exit but the
/// public-facing IP shifts when the tunnel re-handshakes, which usually
/// resets per-IP rate limits on common origins.
/// </summary>
public class WarpProvider : IVpnProvider
{
    public string Name => "Cloudflare WARP";
    public string DownloadUrl => "https://1.1.1.1/";
    public string? ExecutablePath { get; }

    private WarpProvider(string exe) { ExecutablePath = exe; }

    public static WarpProvider? TryDetect()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[]
        {
            Path.Combine(pf, "Cloudflare", "Cloudflare WARP", "warp-cli.exe"),
            Path.Combine(pf, "Cloudflare WARP", "warp-cli.exe"),
        };
        foreach (var p in candidates)
        {
            try { if (File.Exists(p)) return new WarpProvider(p); } catch { }
        }
        var fromPath = ProcessRunner.FindOnPath("warp-cli.exe");
        return fromPath != null ? new WarpProvider(fromPath) : null;
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
        // Examples:
        //   "Status update: Connected"
        //   "Status update: Disconnected"
        var m = Regex.Match(text, @"Status update:\s*(\w+)", RegexOptions.IgnoreCase);
        bool connected = m.Success && m.Groups[1].Value.Equals("Connected", StringComparison.OrdinalIgnoreCase);
        return new VpnStatus(connected, connected ? "Cloudflare anycast" : null, text);
    }

    public async Task<bool> ConnectAsync(string? locationId = null, CancellationToken ct = default)
    {
        if (ExecutablePath == null) return false;
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
        // WARP doesn't expose location selection; disconnect/reconnect forces a fresh handshake
        // which generally lands on a different egress.
        await DisconnectAsync(ct);
        await Task.Delay(1000, ct);
        return await ConnectAsync(null, ct);
    }

    public Task<IReadOnlyList<VpnLocation>> ListLocationsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<VpnLocation>>(Array.Empty<VpnLocation>());
}
