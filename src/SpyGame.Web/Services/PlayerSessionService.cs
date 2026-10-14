using System.Security.Claims;
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
        if (!IsWellFormed(token) || await sessions.GetAsync(token!) is not { } session) return null;
        return await WithAccountAsync(http, token!, session);
    }

    /// <summary>The session of this request, starting a new one if needed.</summary>
    public async Task<(string Token, PlayerSession Session)> GetOrCreateAsync(HttpContext http)
    {
        var token = http.Request.Cookies[CookieName];
        if (IsWellFormed(token) && await sessions.GetAsync(token!) is { } existing) return (token!, await WithAccountAsync(http, token!, existing));

        token = RandomToken(32);
        var session = await WithAccountAsync(http, token, new PlayerSession(RandomToken(12), null));
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

    /// <summary>Ends the game session (on logout): the browser plays on as a new guest.</summary>
    public void Forget(HttpContext http) => http.Response.Cookies.Delete(CookieName);

    /// <summary>
    /// A logged-in browser plays as its account: same session, the account's id and name.
    /// The sign-in cookie is the proof; nothing the page sends can set these.
    /// </summary>
    private async Task<PlayerSession> WithAccountAsync(HttpContext http, string token, PlayerSession session)
    {
        if (http.User.Identity?.IsAuthenticated != true) return session;
        var accountId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = http.User.FindFirstValue(ClaimTypes.Name);
        if (accountId is null || name is null || (session.AccountId == accountId && session.Nickname == name)) return session;
        var linked = session with { AccountId = accountId, Nickname = name };
        await sessions.SaveAsync(token, linked);
        return linked;
    }

    // 32 random bytes in base64url are 43 characters; anything else is not one of our tokens.
    private static bool IsWellFormed(string? token) =>
        token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string RandomToken(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
