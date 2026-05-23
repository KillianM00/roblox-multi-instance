namespace RobloxMultiInstance.Services.Network;

public record VpnStatus(bool Connected, string? Location, string? Detail);

public record VpnLocation(string Id, string Display);

public interface IVpnProvider
{
    string Name { get; }
    string? ExecutablePath { get; }
    bool IsInstalled => !string.IsNullOrEmpty(ExecutablePath);

    string DownloadUrl { get; }

    Task<VpnStatus> GetStatusAsync(CancellationToken ct = default);
    Task<bool> ConnectAsync(string? locationId = null, CancellationToken ct = default);
    Task<bool> DisconnectAsync(CancellationToken ct = default);
    Task<bool> CycleAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VpnLocation>> ListLocationsAsync(CancellationToken ct = default);
}
