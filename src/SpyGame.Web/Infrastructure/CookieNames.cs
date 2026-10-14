namespace SpyGame.Web.Infrastructure;

/// <summary>
/// Outside development the cookies carry the "__Host-" prefix: the browser then accepts them only
/// from this exact host over HTTPS, so another subdomain cannot plant or overwrite them.
/// (Local development runs on plain HTTP, where the prefix is not allowed.)
/// </summary>
public static class CookieNames
{
    public static string For(IHostEnvironment env, string name) => env.IsDevelopment() ? name : "__Host-" + name;
}
