using StackExchange.Redis;

namespace SpyGame.Web.Services.Storage;

/// <summary>
/// Upstash and Render give a URL ("rediss://user:password@host:6379"); StackExchange.Redis
/// wants its own options. "rediss" means TLS.
/// </summary>
public static class RedisConnection
{
    public static ConfigurationOptions Options(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("redis" or "rediss"))
            throw new ArgumentException("REDIS_URL must look like redis://user:password@host:port", nameof(url));

        var options = new ConfigurationOptions
        {
            Ssl = uri.Scheme == "rediss",
            AbortOnConnectFail = false, // keep retrying in the background instead of failing at start-up
            ConnectTimeout = 10_000,
        };
        options.EndPoints.Add(uri.Host, uri.IsDefaultPort ? 6379 : uri.Port);
        if (uri.UserInfo.Length > 0)
        {
            var parts = uri.UserInfo.Split(':', 2);
            var (user, password) = parts.Length == 2 ? (parts[0], parts[1]) : ("", parts[0]);
            if (user.Length > 0) options.User = Uri.UnescapeDataString(user);
            options.Password = Uri.UnescapeDataString(password);
        }
        return options;
    }
}
