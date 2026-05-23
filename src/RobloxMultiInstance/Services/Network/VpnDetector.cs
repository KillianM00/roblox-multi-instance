namespace RobloxMultiInstance.Services.Network;

public static class VpnDetector
{
    public static IReadOnlyList<IVpnProvider> DetectAll()
    {
        var list = new List<IVpnProvider>();
        TryAdd(list, MullvadProvider.TryDetect());
        TryAdd(list, NordVpnProvider.TryDetect());
        TryAdd(list, ProtonVpnProvider.TryDetect());
        TryAdd(list, WarpProvider.TryDetect());
        return list;
    }

    public static IReadOnlyList<IVpnProvider> AllKnown()
    {
        // For showing install suggestions when nothing is detected — synthetic placeholders
        // with no ExecutablePath. UI uses DownloadUrl to point users at the installer.
        return new IVpnProvider[]
        {
            new InstallSuggestion("Mullvad VPN", "https://mullvad.net/en/download/vpn/windows"),
            new InstallSuggestion("Cloudflare WARP (free)", "https://1.1.1.1/"),
            new InstallSuggestion("ProtonVPN", "https://protonvpn.com/download"),
            new InstallSuggestion("NordVPN", "https://nordvpn.com/download/"),
        };
    }

    private static void TryAdd(List<IVpnProvider> list, IVpnProvider? p)
    {
        if (p != null && p.IsInstalled) list.Add(p);
    }

    private sealed class InstallSuggestion : IVpnProvider
    {
        public string Name { get; }
        public string DownloadUrl { get; }
        public string? ExecutablePath => null;

        public InstallSuggestion(string name, string url)
        {
            Name = name;
            DownloadUrl = url;
        }

        public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(new VpnStatus(false, null, "Not installed"));
        public Task<bool> ConnectAsync(string? locationId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> DisconnectAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CycleAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<VpnLocation>> ListLocationsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<VpnLocation>>(Array.Empty<VpnLocation>());
    }
}
