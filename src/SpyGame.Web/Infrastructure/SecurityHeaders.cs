namespace SpyGame.Web.Infrastructure;

public static class SecurityHeaders
{
    // Everything is served from this site. The only connection a page makes is the game channel
    // back to this same server (a WebSocket, so wss:// is named too). No inline script anywhere;
    // Trusted Types make HTML-string sinks such as innerHTML throw in browsers that support them.
    private static string Csp(HttpContext http) =>
        "default-src 'none'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "font-src 'self'; " +
        "img-src 'self' data:; " +
        $"connect-src 'self' {(http.Request.IsHttps ? "wss" : "ws")}://{http.Request.Host}; " +
        "manifest-src 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; base-uri 'none'; object-src 'none'; " +
        "require-trusted-types-for 'script'; trusted-types 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = Csp(context);
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            await next();
        });
}
