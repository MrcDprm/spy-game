using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.SignalR;
using SpyGame.Web.Controllers;
using SpyGame.Web.Hubs;
using SpyGame.Web.Infrastructure;
using SpyGame.Web.Localization;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services;
using SpyGame.Web.Services.Storage;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Text in two flat JSON files (Resources/tr.json, en.json); the JSON localizer is registered first.
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { new CultureInfo("tr"), new CultureInfo("en") };
    options.DefaultRequestCulture = new RequestCulture("tr");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()];
});
// Razor encodes everything outside Basic Latin by default ("ı" becomes &#x131;); <, >, & and quotes stay encoded.
builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(o => o.AddFilter<HubRateLimitFilter>())
    .AddJsonProtocol(o =>
    {
        foreach (var converter in GameJson.Options.Converters) o.PayloadSerializerOptions.Converters.Add(converter);
    });

// Live state: Redis when REDIS_URL is set (production must have it), otherwise this process's memory.
var redisUrl = builder.Configuration["REDIS_URL"];
if (!string.IsNullOrWhiteSpace(redisUrl))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(RedisConnection.Options(redisUrl)));
    builder.Services.AddSingleton<RedisGameStore>();
    builder.Services.AddSingleton<IRoomStore>(sp => sp.GetRequiredService<RedisGameStore>());
    builder.Services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<RedisGameStore>());
    builder.Services.AddSingleton<INicknameRegistry>(sp => sp.GetRequiredService<RedisGameStore>());
}
else if (builder.Environment.IsProduction())
{
    throw new InvalidOperationException("REDIS_URL is required in production.");
}
else
{
    builder.Services.AddSingleton<InMemoryGameStore>();
    builder.Services.AddSingleton<IRoomStore>(sp => sp.GetRequiredService<InMemoryGameStore>());
    builder.Services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<InMemoryGameStore>());
    builder.Services.AddSingleton<INicknameRegistry>(sp => sp.GetRequiredService<InMemoryGameStore>());
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRandomSource, CryptoRandomSource>();
builder.Services.AddSingleton<IRoomNotifier, HubNotifier>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<PlayerSessionService>();
builder.Services.AddSingleton<ConnectionTracker>();
builder.Services.AddSingleton<HubRateLimitFilter>();
builder.Services.AddHostedService<DeadlineWorker>();

// Creating rooms and trying codes are limited per visitor address.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RoomController.CreatePolicy, http => RateLimitPartition.GetFixedWindowLimiter(ClientIp.Of(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10) }));
    o.AddPolicy(RoomController.JoinPolicy, http => RateLimitPartition.GetFixedWindowLimiter(ClientIp.Of(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Render terminates TLS at its proxy and forwards plain HTTP.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443); // behind Render's proxy
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    // Render's health check calls the container over plain HTTP; a redirect would mark it down.
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/healthz"), branch => branch.UseHttpsRedirection());
}
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseSecurityHeaders();
app.UseStaticFiles(new StaticFileOptions
{
    // Files linked with asp-append-version (?v=hash) change URL when they change: cache for a year.
    OnPrepareResponse = context =>
        context.Context.Response.Headers.CacheControl = context.Context.Request.Query.ContainsKey("v")
            ? "public,max-age=31536000,immutable"
            : "no-cache",
});
app.UseRequestLocalization();

// Browsers send cookies with WebSocket connections from other sites too, and CORS does not
// cover WebSockets: only pages of this site may open the game channel.
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments(GameHub.Path)
        && http.Request.Headers.Origin.ToString() is { Length: > 0 } origin
        && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !string.Equals(uri.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase)))
    {
        http.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});

app.UseRouting();
app.UseRateLimiter();

app.MapHealthChecks("/healthz");
app.MapControllers();
app.MapHub<GameHub>(GameHub.Path);

app.Run();

// Lets the tests start the app with WebApplicationFactory<Program>.
public partial class Program;
