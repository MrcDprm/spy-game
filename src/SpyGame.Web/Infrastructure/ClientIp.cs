namespace SpyGame.Web.Infrastructure;

/// <summary>
/// The visitor's address, for rate limits. In production Caddy runs on the same machine, ignores any
/// X-Forwarded-For the visitor sends and writes the address it sees; UseForwardedHeaders believes that
/// header only from the loopback address, so the connection address is the visitor's and cannot be forged.
/// Headers such as CF-Connecting-IP are not read: nothing in front of the site would overwrite them.
/// </summary>
public static class ClientIp
{
    public static string Of(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
