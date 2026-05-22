using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace RobloxMultiInstance.Services;

public class RobloxAuth
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Token, DateTime Expires)> CsrfCache = new();
    private static readonly TimeSpan CsrfTtl = TimeSpan.FromMinutes(5);

    private const string AuthTicketUrl = "https://auth.roblox.com/v1/authentication-ticket/";
    private const string AuthenticatedUserUrl = "https://users.roblox.com/v1/users/authenticated";
    private const string ThumbnailUrlTemplate = "https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={0}&size=150x150&format=Png&isCircular=true";
    private const string UsernameLookupUrl = "https://users.roblox.com/v1/usernames/users";
    private const string UserInfoByIdUrlTemplate = "https://users.roblox.com/v1/users/{0}";
    private const string FriendRequestUrlTemplate = "https://friends.roblox.com/v1/users/{0}/request-friendship";
    private const string PresenceUrl = "https://presence.roblox.com/v1/presence/users";
    private const string ResolveShareLinkUrl = "https://apis.roblox.com/sharelinks/v1/resolve-link";
    private const string Referer = "https://www.roblox.com/";

    public record UserInfo(long Id, string Name, string DisplayName);
    public record FriendRequestResult(bool Success, string Message);
    public record PresenceInfo(int Type, long? PlaceId, long? RootPlaceId, string? GameId, string LastLocation)
    {
        public bool InGame => Type == 2 && PlaceId.HasValue && PlaceId.Value > 0;
    }
    public record ShareLinkResolution(long PlaceId, string? PrivateServerLinkCode, string LinkType);

    private const string BrowserUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    private static readonly HttpClient AuthedClient = BuildClient(TimeSpan.FromSeconds(20));
    private static readonly HttpClient AnonClient = BuildClient(TimeSpan.FromSeconds(10));

    private static HttpClient BuildClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        };
        var c = new HttpClient(handler)
        {
            Timeout = timeout,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUA);
        c.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not?A_Brand\";v=\"99\"");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
        return c;
    }

    private static void DecorateAuthed(HttpRequestMessage req)
    {
        req.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
        req.Headers.ConnectionClose = true;
        req.Version = HttpVersion.Version11;
        req.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    }

    private static int? ParseRetryAfter(HttpResponseMessage resp)
    {
        if (resp.Headers.RetryAfter is { } ra)
        {
            if (ra.Delta is { } d) return (int)Math.Ceiling(d.TotalSeconds);
            if (ra.Date is { } when) return (int)Math.Ceiling((when - DateTimeOffset.UtcNow).TotalSeconds);
        }
        return null;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string cookie)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
        return req;
    }

    private static string CookieKey(string cookie)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cookie));
        return Convert.ToHexString(bytes);
    }

    private static void CacheCsrf(string cookie, string token)
    {
        CsrfCache[CookieKey(cookie)] = (token, DateTime.UtcNow.Add(CsrfTtl));
    }

    private static string? GetCachedCsrf(string cookie)
    {
        if (CsrfCache.TryGetValue(CookieKey(cookie), out var entry) && DateTime.UtcNow < entry.Expires)
            return entry.Token;
        return null;
    }

    public async Task<UserInfo?> GetUserInfoAsync(string cookie, CancellationToken ct = default)
    {
        using var req = AuthedRequest(HttpMethod.Get, AuthenticatedUserUrl, cookie);
        using var resp = await AuthedClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new UserInfo(
                root.GetProperty("id").GetInt64(),
                root.GetProperty("name").GetString() ?? "",
                root.GetProperty("displayName").GetString() ?? "");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ShareLinkResolution?> ResolveShareLinkAsync(string cookie, string linkId, string linkType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(linkId)) return null;
        var payload = JsonSerializer.Serialize(new { linkId, linkType });

        string? csrf = null;
        using (var probe = AuthedRequest(HttpMethod.Post, ResolveShareLinkUrl, cookie))
        {
            probe.Headers.Referrer = new Uri(Referer);
            probe.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var probeResp = await AuthedClient.SendAsync(probe, ct);
            if (probeResp.IsSuccessStatusCode)
            {
                var jsonOk = await probeResp.Content.ReadAsStringAsync(ct);
                return ParseShareResolution(jsonOk, linkType);
            }
            if (probeResp.Headers.TryGetValues("x-csrf-token", out var v))
                csrf = v.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(csrf)) return null;

        using var req = AuthedRequest(HttpMethod.Post, ResolveShareLinkUrl, cookie);
        req.Headers.Referrer = new Uri(Referer);
        req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
        req.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        using var resp = await AuthedClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseShareResolution(json, linkType);
    }

    private static ShareLinkResolution? ParseShareResolution(string json, string linkType)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("privateServerInviteData", out var psi) && psi.ValueKind == JsonValueKind.Object)
            {
                if (!psi.TryGetProperty("placeId", out var pl) || pl.ValueKind != JsonValueKind.Number) return null;
                long placeId = pl.GetInt64();
                string? linkCode = psi.TryGetProperty("linkCode", out var lc) ? lc.GetString() : null;
                return new ShareLinkResolution(placeId, linkCode, linkType);
            }

            if (root.TryGetProperty("experienceInviteData", out var exp) && exp.ValueKind == JsonValueKind.Object)
            {
                if (!exp.TryGetProperty("placeId", out var pl) || pl.ValueKind != JsonValueKind.Number) return null;
                return new ShareLinkResolution(pl.GetInt64(), null, linkType);
            }

            if (root.TryGetProperty("placeId", out var pl2) && pl2.ValueKind == JsonValueKind.Number)
            {
                return new ShareLinkResolution(pl2.GetInt64(), null, linkType);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<PresenceInfo?> GetPresenceAsync(string cookie, long userId, CancellationToken ct = default)
    {
        if (userId <= 0) return null;
        var payload = JsonSerializer.Serialize(new { userIds = new[] { userId } });

        string? csrf = null;
        using (var probe = AuthedRequest(HttpMethod.Post, PresenceUrl, cookie))
        {
            probe.Headers.Referrer = new Uri(Referer);
            probe.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var probeResp = await AuthedClient.SendAsync(probe, ct);
            if (probeResp.IsSuccessStatusCode)
            {
                var jsonOk = await probeResp.Content.ReadAsStringAsync(ct);
                return ParsePresence(jsonOk);
            }
            if (probeResp.Headers.TryGetValues("x-csrf-token", out var v))
                csrf = v.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(csrf)) return null;

        using var req = AuthedRequest(HttpMethod.Post, PresenceUrl, cookie);
        req.Headers.Referrer = new Uri(Referer);
        req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
        req.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        using var resp = await AuthedClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParsePresence(json);
    }

    private static PresenceInfo? ParsePresence(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("userPresences", out var arr) || arr.GetArrayLength() == 0)
                return null;
            var p = arr[0];
            int type = p.TryGetProperty("userPresenceType", out var t) ? t.GetInt32() : 0;
            long? placeId = p.TryGetProperty("placeId", out var pl) && pl.ValueKind == JsonValueKind.Number
                ? pl.GetInt64() : null;
            long? rootPlace = p.TryGetProperty("rootPlaceId", out var rp) && rp.ValueKind == JsonValueKind.Number
                ? rp.GetInt64() : null;
            string? gameId = p.TryGetProperty("gameId", out var gi) && gi.ValueKind == JsonValueKind.String
                ? gi.GetString() : null;
            string loc = p.TryGetProperty("lastLocation", out var ll) ? ll.GetString() ?? "" : "";
            return new PresenceInfo(type, placeId, rootPlace, gameId, loc);
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserInfo?> LookupUserByNameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                usernames = new[] { username.Trim() },
                excludeBannedUsers = false,
            });
            using var req = new HttpRequestMessage(HttpMethod.Post, UsernameLookupUrl)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            };
            using var resp = await AnonClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;
            var first = data[0];
            return new UserInfo(
                first.GetProperty("id").GetInt64(),
                first.GetProperty("name").GetString() ?? "",
                first.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "");
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserInfo?> LookupUserByIdAsync(long userId, CancellationToken ct = default)
    {
        if (userId <= 0) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, string.Format(UserInfoByIdUrlTemplate, userId));
            using var resp = await AnonClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new UserInfo(
                root.GetProperty("id").GetInt64(),
                root.GetProperty("name").GetString() ?? "",
                root.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "");
        }
        catch
        {
            return null;
        }
    }

    public async Task<FriendRequestResult> SendFriendRequestAsync(string cookie, long targetUserId, CancellationToken ct = default)
    {
        if (targetUserId <= 0) return new FriendRequestResult(false, "Invalid target user ID");
        var url = string.Format(FriendRequestUrlTemplate, targetUserId);

        try
        {
            using var probe = AuthedRequest(HttpMethod.Post, url, cookie);
            probe.Headers.Referrer = new Uri(Referer);
            probe.Content = new StringContent("");
            using var probeResp = await AuthedClient.SendAsync(probe, ct);
            if (probeResp.IsSuccessStatusCode)
                return new FriendRequestResult(true, "Sent");

            if (!probeResp.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                return new FriendRequestResult(false, $"No CSRF token (HTTP {(int)probeResp.StatusCode})");

            var csrf = csrfValues.FirstOrDefault();
            if (string.IsNullOrEmpty(csrf))
                return new FriendRequestResult(false, "Empty CSRF token");

            using var req = AuthedRequest(HttpMethod.Post, url, cookie);
            req.Headers.Referrer = new Uri(Referer);
            req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            req.Content = new StringContent("");
            using var resp = await AuthedClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return new FriendRequestResult(true, "Sent");

            var body = await resp.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("errors", out var errs) && errs.GetArrayLength() > 0)
                {
                    var msg = errs[0].TryGetProperty("message", out var m) ? m.GetString() : null;
                    return new FriendRequestResult(false, msg ?? $"HTTP {(int)resp.StatusCode}");
                }
            }
            catch { }
            return new FriendRequestResult(false, $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return new FriendRequestResult(false, ex.Message);
        }
    }

    public async Task<string?> GetAvatarHeadshotUrlAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, string.Format(ThumbnailUrlTemplate, userId));
            using var resp = await AnonClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;
            var first = data[0];
            if (first.TryGetProperty("imageUrl", out var img) && img.ValueKind == JsonValueKind.String)
                return img.GetString();
            return null;
        }
        catch
        {
            return null;
        }
    }

    public record TicketResult(string? Ticket, string? Error, int? RetryAfterSeconds = null);

    private async Task<HttpResponseMessage> SendTicketRequestAsync(string cookie, string csrf, CancellationToken ct)
    {
        var req = AuthedRequest(HttpMethod.Post, AuthTicketUrl, cookie);
        DecorateAuthed(req);
        req.Headers.Referrer = new Uri(Referer);
        req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
        req.Headers.TryAddWithoutValidation("RBXAuthenticationNegotiation", "1");
        req.Content = new StringContent("");
        return await AuthedClient.SendAsync(req, ct);
    }

    public async Task<TicketResult> GetAuthTicketAsync(string cookie, CancellationToken ct = default)
    {
        // Try cached CSRF first to halve request count under steady use.
        var cached = GetCachedCsrf(cookie);
        if (cached != null)
        {
            using var resp = await SendTicketRequestAsync(cookie, cached, ct);
            if ((int)resp.StatusCode == 429)
                return new TicketResult(null, "Rate limited by Roblox (HTTP 429). Wait 1-2 minutes or raise the launch delay.", ParseRetryAfter(resp));
            if (resp.IsSuccessStatusCode && resp.Headers.TryGetValues("rbx-authentication-ticket", out var tv))
            {
                var t = tv.FirstOrDefault();
                if (!string.IsNullOrEmpty(t)) return new TicketResult(t, null);
            }
            // Cached CSRF rejected; refresh below.
            if (resp.Headers.TryGetValues("x-csrf-token", out var fresh))
            {
                var f = fresh.FirstOrDefault();
                if (!string.IsNullOrEmpty(f))
                {
                    CacheCsrf(cookie, f);
                    using var retry = await SendTicketRequestAsync(cookie, f, ct);
                    if ((int)retry.StatusCode == 429)
                        return new TicketResult(null, "Rate limited by Roblox (HTTP 429). Wait 1-2 minutes or raise the launch delay.");
                    if (retry.Headers.TryGetValues("rbx-authentication-ticket", out var t2))
                    {
                        var v = t2.FirstOrDefault();
                        if (!string.IsNullOrEmpty(v)) return new TicketResult(v, null);
                    }
                }
            }
        }

        // No cache hit — do the standard probe.
        using var probe = AuthedRequest(HttpMethod.Post, AuthTicketUrl, cookie);
        probe.Headers.Referrer = new Uri(Referer);
        probe.Content = new StringContent("");
        using var probeResp = await AuthedClient.SendAsync(probe, ct);

        if ((int)probeResp.StatusCode == 429)
            return new TicketResult(null, "Rate limited by Roblox (HTTP 429). Wait 1-2 minutes or raise the launch delay.");

        if (!probeResp.Headers.TryGetValues("x-csrf-token", out var csrfValues))
            return new TicketResult(null, $"No CSRF token from auth endpoint (HTTP {(int)probeResp.StatusCode}). Cookie may be invalid or expired.");

        var csrf = csrfValues.FirstOrDefault();
        if (string.IsNullOrEmpty(csrf)) return new TicketResult(null, "Empty CSRF token");
        CacheCsrf(cookie, csrf);

        using var resp2 = await SendTicketRequestAsync(cookie, csrf, ct);
        if ((int)resp2.StatusCode == 429)
            return new TicketResult(null, "Rate limited by Roblox (HTTP 429). Wait 1-2 minutes or raise the launch delay.");

        if (resp2.Headers.TryGetValues("rbx-authentication-ticket", out var ticketValues))
        {
            var ticket = ticketValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(ticket))
                return new TicketResult(ticket, null);
        }

        return new TicketResult(null, $"Ticket endpoint returned HTTP {(int)resp2.StatusCode} with no ticket header. Cookie may be invalid.");
    }
}
