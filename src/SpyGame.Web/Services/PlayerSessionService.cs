using System.Security.Claims;
using System.Security.Cryptography;
using SpyGame.Web.Infrastructure;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Services;

/// <summary>
/// Who is this browser? A guest gets a random token in an HttpOnly cookie (JavaScript cannot
/// read it); the store keeps only its hash, with the player's id and nickname.
/// </summary>
public sealed class PlayerSessionService(ISessionStore sessions, IWebHostEnvironment env)
{
    public string CookieName { get; } = CookieNames.For(env, "sg_session");

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

    /// <summary>
    /// Ends the game session: on logout (play on as a new guest) and on login (a session that was
    /// planted in the browser before must not become the account's).
    /// </summary>
    public void Forget(HttpContext http) =>
        // A "__Host-" cookie is only removed by a reply with the same Secure and Path attributes.
        http.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/", Secure = !env.IsDevelopment() || http.Request.IsHttps, HttpOnly = true, SameSite = SameSiteMode.Lax });

    /// <summary>
    /// A logged-in browser plays as its account: same session, the account's id and name.
    /// The sign-in cookie is the proof; nothing the page sends can set these.
    /// </summary>
    private async Task<PlayerSession> WithAccountAsync(HttpContext http, string token, PlayerSession session)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            // No (valid) sign-in any more: logged out elsewhere, password reset, cookie expired.
            // The game session must not keep playing, or scoring, as the account.
            if (session.AccountId is null) return session;
            var guest = session with { AccountId = null, Nickname = null };
            await sessions.SaveAsync(token, guest);
            return guest;
        }
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
