namespace SpyGame.Web.Data;

public static class ConnectionStrings
{
    // Neon and Render give a URI (postgresql://user:pass@host/db?sslmode=require);
    // Npgsql wants key=value pairs. Anything else is returned unchanged.
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var parts = new List<string>
        {
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))}",
            $"Username={Uri.UnescapeDataString(userInfo[0])}",
        };
        if (userInfo.Length > 1)
            parts.Add($"Password={Uri.UnescapeDataString(userInfo[1])}");

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslMode = query["sslmode"];
        parts.Add($"SSL Mode={(string.IsNullOrEmpty(sslMode) ? "Require" : ToNpgsqlSslMode(sslMode))}");
        return string.Join(';', parts);
    }

    private static string ToNpgsqlSslMode(string mode) => mode.ToLowerInvariant() switch
    {
        "disable" => "Disable",
        "allow" => "Allow",
        "prefer" => "Prefer",
        "verify-ca" => "VerifyCA",
        "verify-full" => "VerifyFull",
        _ => "Require",
    };
}
