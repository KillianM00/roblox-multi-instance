using RobloxMultiInstance.Services.Network;
using Xunit;

namespace RobloxMultiInstance.Tests;

public class ProtonVpnStatusParsingTests
{
    [Fact]
    public void Status_connected_with_server()
    {
        var s = ProtonVpnProvider.ParseStatus("Status: Connected\nServer: NL-FREE-1\nProtocol: WireGuard");
        Assert.True(s.Connected);
        Assert.Equal("NL-FREE-1", s.Location);
    }

    [Fact]
    public void Status_not_connected()
    {
        var s = ProtonVpnProvider.ParseStatus("Status: Not connected");
        Assert.False(s.Connected);
    }

    [Fact]
    public void Status_disconnected()
    {
        var s = ProtonVpnProvider.ParseStatus("Status: Disconnected");
        Assert.False(s.Connected);
    }
}
