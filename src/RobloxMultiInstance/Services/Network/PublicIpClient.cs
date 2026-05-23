using System.Net.Http;

namespace RobloxMultiInstance.Services.Network;

public static class PublicIpClient
{
    private static readonly string[] Endpoints =
    {
        "https://api.ipify.org",
        "https://icanhazip.com",
        "https://ifconfig.me/ip",
    };

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    static PublicIpClient()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxMultiInstance/0.1 (NetworkProbe)");
    }

    public static async Task<string?> GetAsync(CancellationToken ct = default)
    {
        foreach (var url in Endpoints)
        {
            try
            {
                using var resp = await Client.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) continue;
                var body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
                if (System.Net.IPAddress.TryParse(body, out _))
                    return body;
            }
            catch
            {
            }
        }
        return null;
    }
}
