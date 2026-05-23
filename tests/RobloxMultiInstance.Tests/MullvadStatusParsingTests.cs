using RobloxMultiInstance.Services.Network;
using Xunit;

namespace RobloxMultiInstance.Tests;

public class MullvadStatusParsingTests
{
    [Fact]
    public void Empty_is_disconnected()
    {
        var s = MullvadProvider.ParseStatus("");
        Assert.False(s.Connected);
        Assert.Null(s.Location);
    }

    [Fact]
    public void Plain_disconnected()
    {
        var s = MullvadProvider.ParseStatus("Disconnected");
        Assert.False(s.Connected);
    }

    [Fact]
    public void Connected_short_form_with_relay()
    {
        var s = MullvadProvider.ParseStatus("Connected to wg in us-nyc-wg-301");
        Assert.True(s.Connected);
        Assert.Equal("us-nyc-wg-301", s.Location);
    }

    [Fact]
    public void Connected_with_country_only()
    {
        var s = MullvadProvider.ParseStatus("Connected to wg in se");
        Assert.True(s.Connected);
        Assert.Equal("se", s.Location);
    }

    [Fact]
    public void Multi_line_status_block()
    {
        var s = MullvadProvider.ParseStatus("Tunnel status: Connected\nRelay: nl-ams-wg-001\nIPv4 address: 10.x.x.x");
        Assert.True(s.Connected);
        Assert.Equal("nl-ams-wg-001", s.Location);
    }

    [Fact]
    public void Connecting_is_not_yet_connected()
    {
        // "Connecting" should not be treated as Connected.
        var s = MullvadProvider.ParseStatus("Connecting to wg in fr-par-wg-001...");
        Assert.False(s.Connected);
    }

    [Fact]
    public void Disconnected_with_noise_does_not_misparse()
    {
        var s = MullvadProvider.ParseStatus("Disconnected\nLast known: wg in us-nyc-wg-301");
        Assert.False(s.Connected);
    }
}
