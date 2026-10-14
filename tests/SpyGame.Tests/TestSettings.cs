using Microsoft.Extensions.Configuration;

namespace SpyGame.Tests;

/// <summary>Settings for tests that need outside services, from the web project's user-secrets or the environment.</summary>
internal static class TestSettings
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static string? RedisUrl => Config["REDIS_URL"] is { Length: > 0 } url ? url : null;
}
