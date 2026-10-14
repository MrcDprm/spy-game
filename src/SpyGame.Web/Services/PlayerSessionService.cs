using System.Security.Cryptography;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Services;

/// <summary>
/// Who is this browser? A guest gets a random token in an HttpOnly cookie (JavaScript cannot
/// read it); the store keeps only its hash, with the player's id and nickname.
/// </summary>
public sealed class PlayerSessionService(ISessionStore sessions, IWebHostEnvironment env)
{
    public const string CookieName = "sg_session";

    /// <summary>The session of this request, or null if the browser has none (or it expired).</summary>
    public async Task<PlayerSession?> FindAsync(HttpContext http)
    {
        var token = http.Request.Cookies[CookieName];
        return IsWellFormed(token) ? await sessions.GetAsync(token!) : null;
    }

    /// <summary>The session of this request, starting a new one if needed.</summary>
    public async Task<(string Token, PlayerSession Session)> GetOrCreateAsync(HttpContext http)
    {
        var token = http.Request.Cookies[CookieName];
        if (IsWellFormed(token) && await sessions.GetAsync(token!) is { } existing) return (token!, existing);

        token = RandomToken(32);
        var session = new PlayerSession(RandomToken(12), null);
        await sessions.SaveAsync(token, session);
        http.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment() || http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(30),
            IsEssential = true,
        });
        return (token, session);
    }

    public Task SaveAsync(string token, PlayerSession session) => sessions.SaveAsync(token, session);

    // 32 random bytes in base64url are 43 characters; anything else is not one of our tokens.
    private static bool IsWellFormed(string? token) =>
        token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string RandomToken(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
