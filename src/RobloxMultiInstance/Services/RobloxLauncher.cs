using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;

namespace RobloxMultiInstance.Services;

public class RobloxLauncher
{
    private const long DefaultPlaceId = 920587237; // Adopt Me — always-online popular lobby

    private readonly RobloxAuth _auth;
    private readonly RobloxProcessTracker _tracker;

    public RobloxLauncher(RobloxAuth auth, RobloxProcessTracker tracker)
    {
        _auth = auth;
        _tracker = tracker;
    }

    public record LaunchResult(bool Success, string? Error);

    public async Task<LaunchResult> LaunchAsync(string cookie, long? placeId = null, Guid? accountId = null, string? jobId = null, string? privateServerLinkCode = null, string? privateServerAccessCode = null, CancellationToken ct = default)
    {
        if (accountId.HasValue) _tracker.BeginLaunchAssociation(accountId.Value);

        var ticketResult = await _auth.GetAuthTicketAsync(cookie, ct);
        if (string.IsNullOrEmpty(ticketResult.Ticket))
            return new LaunchResult(false, ticketResult.Error ?? "Failed to fetch auth ticket.");
        var ticket = ticketResult.Ticket;

        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long resolvedPlaceId = placeId is > 0 ? placeId.Value : DefaultPlaceId;

        string placeLauncher;
        if (!string.IsNullOrEmpty(privateServerLinkCode))
        {
            placeLauncher = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&browserTrackerId=0&placeId={resolvedPlaceId}&linkCode={privateServerLinkCode}";
        }
        else if (!string.IsNullOrEmpty(privateServerAccessCode))
        {
            placeLauncher = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&browserTrackerId=0&placeId={resolvedPlaceId}&accessCode={privateServerAccessCode}";
        }
        else if (!string.IsNullOrEmpty(jobId))
        {
            placeLauncher = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGameJob&browserTrackerId=0&placeId={resolvedPlaceId}&gameId={jobId}&isPlayTogetherGame=false";
        }
        else
        {
            placeLauncher = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId=0&placeId={resolvedPlaceId}&isPlayTogetherGame=false";
        }

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
    private static readonly Regex PrivateServerLinkCodeRegex = new(@"[?&]privateServerLinkCode=([A-Za-z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PrivateServerAccessCodeRegex = new(@"[?&]accessCode=([A-Za-z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public record ParsedGameLink(long PlaceId, string? PrivateServerLinkCode, string? PrivateServerAccessCode);

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

    public static bool TryParseGameLink(string input, out ParsedGameLink? result)
    {
        result = null;
        if (!TryParsePlaceId(input, out var placeId)) return false;
        var trimmed = (input ?? "").Trim();
        string? linkCode = null;
        string? accessCode = null;
        var lc = PrivateServerLinkCodeRegex.Match(trimmed);
        if (lc.Success) linkCode = lc.Groups[1].Value;
        var ac = PrivateServerAccessCodeRegex.Match(trimmed);
        if (ac.Success) accessCode = ac.Groups[1].Value;
        result = new ParsedGameLink(placeId, linkCode, accessCode);
        return true;
    }
}
