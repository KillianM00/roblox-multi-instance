using RobloxMultiInstance.Services.Network;
using Xunit;

namespace RobloxMultiInstance.Tests;

public class VpnDetectorTests
{
    [Fact]
    public void DetectAll_returns_only_installed_providers()
    {
        // Live detection — assertion is that the call doesn't throw and the
        // returned providers are all marked installed.
        var providers = VpnDetector.DetectAll();
        Assert.NotNull(providers);
        Assert.All(providers, p =>
        {
            Assert.True(p.IsInstalled, $"{p.Name} reported as detected but IsInstalled is false");
            Assert.False(string.IsNullOrEmpty(p.ExecutablePath));
        });
    }

    [Fact]
    public void AllKnown_returns_at_least_one_suggestion()
    {
        var known = VpnDetector.AllKnown();
        Assert.NotEmpty(known);
        Assert.All(known, p => Assert.False(string.IsNullOrEmpty(p.DownloadUrl)));
    }

    [Fact]
    public void Suggestions_are_marked_not_installed()
    {
        var known = VpnDetector.AllKnown();
        Assert.All(known, p =>
        {
            Assert.False(p.IsInstalled);
            Assert.Null(p.ExecutablePath);
        });
    }
}
