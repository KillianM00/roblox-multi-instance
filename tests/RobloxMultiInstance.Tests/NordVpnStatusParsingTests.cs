using RobloxMultiInstance.Services.Network;
using Xunit;

namespace RobloxMultiInstance.Tests;

public class NordVpnStatusParsingTests
{
    [Fact]
    public void Status_connected_with_country_city()
    {
        var output = """
Status: Connected
Current server: us1234.nordvpn.com
Country: United States
City: Atlanta
Server IP: 192.0.2.1
""";
        var s = NordVpnProvider.ParseStatus(output);
        Assert.True(s.Connected);
        Assert.NotNull(s.Location);
        Assert.Contains("United States", s.Location);
        Assert.Contains("Atlanta", s.Location);
    }

    [Fact]
    public void Status_disconnected()
    {
        var s = NordVpnProvider.ParseStatus("Status: Disconnected");
        Assert.False(s.Connected);
        Assert.Null(s.Location);
    }

    [Fact]
    public void Connected_without_city_still_returns_country()
    {
        var s = NordVpnProvider.ParseStatus("Status: Connected\nCountry: Germany");
        Assert.True(s.Connected);
        Assert.Equal("Germany", s.Location);
    }

    [Fact]
    public void Empty_is_disconnected()
    {
        var s = NordVpnProvider.ParseStatus("");
        Assert.False(s.Connected);
    }
}
