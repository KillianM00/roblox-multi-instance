using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;

namespace RobloxMultiInstance.Services;

public class RobloxLauncher
{
    private readonly RobloxAuth _auth;
    private readonly RobloxProcessTracker _tracker;

    public RobloxLauncher(RobloxAuth auth, RobloxProcessTracker tracker)
    {
        _auth = auth;
        _tracker = tracker;
    }

    public record LaunchResult(bool Success, string? Error);

    public async Task<LaunchResult> LaunchAsync(string cookie, long placeId, Guid? accountId = null, string? jobId = null, CancellationToken ct = default)
    {
        if (placeId <= 0) return new LaunchResult(false, "Place ID required.");
        if (accountId.HasValue) _tracker.BeginLaunchAssociation(accountId.Value);

        var ticket = await _auth.GetAuthTicketAsync(cookie, ct);
        if (string.IsNullOrEmpty(ticket)) return new LaunchResult(false, "Failed to fetch auth ticket — cookie may be invalid or expired.");

        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var placeLauncher = string.IsNullOrEmpty(jobId)
            ? $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId=0&placeId={placeId}&isPlayTogetherGame=false"
            : $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGameJob&browserTrackerId=0&placeId={placeId}&gameId={jobId}&isPlayTogetherGame=false";

        var encoded = HttpUtility.UrlEncode(placeLauncher);
        var uri = $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{unixMs}+placelauncherurl:{encoded}+browsertrackerid:0+robloxLocale:en_us+gameLocale:en_us";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
            return new LaunchResult(true, null);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, $"Process.Start failed: {ex.Message}");
        }
    }

    private static readonly Regex PlaceIdRegex = new(@"(?:placeId=|/games/)(\d+)", RegexOptions.Compiled);

    public static bool TryParsePlaceId(string input, out long placeId)
    {
        placeId = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();
        if (long.TryParse(input, out placeId)) return placeId > 0;
        var m = PlaceIdRegex.Match(input);
        if (m.Success && long.TryParse(m.Groups[1].Value, out placeId)) return placeId > 0;
        return false;
    }
}
