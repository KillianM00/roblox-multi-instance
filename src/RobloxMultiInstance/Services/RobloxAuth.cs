using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RobloxMultiInstance.Services;

public class RobloxAuth
{
    private const string AuthTicketUrl = "https://auth.roblox.com/v1/authentication-ticket/";
    private const string AuthenticatedUserUrl = "https://users.roblox.com/v1/users/authenticated";
    private const string ThumbnailUrlTemplate = "https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={0}&size=150x150&format=Png&isCircular=true";
    private const string UsernameLookupUrl = "https://users.roblox.com/v1/usernames/users";
    private const string UserInfoByIdUrlTemplate = "https://users.roblox.com/v1/users/{0}";
    private const string FriendRequestUrlTemplate = "https://friends.roblox.com/v1/users/{0}/request-friendship";
    private const string Referer = "https://www.roblox.com/";

    public record UserInfo(long Id, string Name, string DisplayName);
    public record FriendRequestResult(bool Success, string Message);

    public async Task<UserInfo?> GetUserInfoAsync(string cookie, CancellationToken ct = default)
    {
        using var client = CreateClient(cookie);
        using var resp = await client.GetAsync(AuthenticatedUserUrl, ct);
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

    public async Task<UserInfo?> LookupUserByNameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxMultiInstance/0.1");
            var payload = JsonSerializer.Serialize(new
            {
                usernames = new[] { username.Trim() },
                excludeBannedUsers = false,
            });
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(UsernameLookupUrl, content, ct);
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
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxMultiInstance/0.1");
            using var resp = await client.GetAsync(string.Format(UserInfoByIdUrlTemplate, userId), ct);
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
        using var client = CreateClient(cookie);
        var url = string.Format(FriendRequestUrlTemplate, targetUserId);

        try
        {
            // Probe for CSRF
            using var probe = new HttpRequestMessage(HttpMethod.Post, url);
            probe.Headers.Referrer = new Uri(Referer);
            probe.Content = new StringContent("");
            using var probeResp = await client.SendAsync(probe, ct);
            if (probeResp.IsSuccessStatusCode)
                return new FriendRequestResult(true, "Sent");

            if (!probeResp.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                return new FriendRequestResult(false, $"No CSRF token (HTTP {(int)probeResp.StatusCode})");

            var csrf = csrfValues.FirstOrDefault();
            if (string.IsNullOrEmpty(csrf))
                return new FriendRequestResult(false, "Empty CSRF token");

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Referrer = new Uri(Referer);
            req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            req.Content = new StringContent("");
            using var resp = await client.SendAsync(req, ct);
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
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxMultiInstance/0.1");
            var url = string.Format(ThumbnailUrlTemplate, userId);
            using var resp = await client.GetAsync(url, ct);
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

    public async Task<string?> GetAuthTicketAsync(string cookie, CancellationToken ct = default)
    {
        using var client = CreateClient(cookie);

        // First call: provoke a CSRF token via 403 response
        using (var probe = new HttpRequestMessage(HttpMethod.Post, AuthTicketUrl))
        {
            probe.Headers.Referrer = new Uri(Referer);
            probe.Content = new StringContent("");
            using var probeResp = await client.SendAsync(probe, ct);
            if (!probeResp.Headers.TryGetValues("x-csrf-token", out var csrfValues))
                return null;

            var csrf = csrfValues.FirstOrDefault();
            if (string.IsNullOrEmpty(csrf)) return null;

            using var req = new HttpRequestMessage(HttpMethod.Post, AuthTicketUrl);
            req.Headers.Referrer = new Uri(Referer);
            req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            req.Headers.TryAddWithoutValidation("RBXAuthenticationNegotiation", "1");
            req.Content = new StringContent("");
            using var resp = await client.SendAsync(req, ct);

            if (resp.Headers.TryGetValues("rbx-authentication-ticket", out var ticketValues))
                return ticketValues.FirstOrDefault();
            return null;
        }
    }

    private static HttpClient CreateClient(string cookie)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Roblox/WinInet");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
        return client;
    }
}
