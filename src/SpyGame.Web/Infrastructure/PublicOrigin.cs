namespace SpyGame.Web.Infrastructure;

/// <summary>
/// The site's own address for links in e-mails and invites. In production it comes from
/// PUBLIC_ORIGIN, never from the request: a forged Host header would otherwise put someone
/// else's site in a password-reset e-mail, with a valid token in it.
/// </summary>
public static class PublicOrigin
{
    public static string Of(HttpContext http)
    {
        var configured = http.RequestServices.GetRequiredService<IConfiguration>()["PUBLIC_ORIGIN"];
        return string.IsNullOrWhiteSpace(configured) ? $"{http.Request.Scheme}://{http.Request.Host}" : configured.TrimEnd('/');
    }
}
