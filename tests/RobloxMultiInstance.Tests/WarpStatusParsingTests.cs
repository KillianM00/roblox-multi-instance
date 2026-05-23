using RobloxMultiInstance.Services.Network;
using Xunit;

namespace RobloxMultiInstance.Tests;

public class WarpStatusParsingTests
{
    [Fact]
    public void Connected_status()
    {
        var s = WarpProvider.ParseStatus("Status update: Connected");
        Assert.True(s.Connected);
        Assert.Equal("Cloudflare anycast", s.Location);
    }

    [Fact]
    public void Disconnected_status()
    {
        var s = WarpProvider.ParseStatus("Status update: Disconnected");
        Assert.False(s.Connected);
    }

    [Fact]
    public void Unknown_output_is_disconnected()
    {
        var s = WarpProvider.ParseStatus("garbage");
        Assert.False(s.Connected);
    }
}
