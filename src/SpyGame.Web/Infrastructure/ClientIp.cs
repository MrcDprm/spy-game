using System.Net;

namespace SpyGame.Web.Infrastructure;

/// <summary>
/// The visitor's address, for rate limits. Render serves the site through Cloudflare, which puts
/// the real address in CF-Connecting-IP and overwrites any value the visitor sends; the last
/// X-Forwarded-For entry would be a Cloudflare server. Locally the connection address is used.
/// </summary>
public static class ClientIp
{
    public const string CloudflareHeader = "CF-Connecting-IP";

    public static string Of(HttpContext context)
    {
        var header = context.Request.Headers[CloudflareHeader].ToString();
        if (IPAddress.TryParse(header, out var address)) return address.ToString();
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
