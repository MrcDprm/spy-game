using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.SignalR;
using SpyGame.Web.Controllers;
using SpyGame.Web.Data;
using SpyGame.Web.Hubs;
using SpyGame.Web.Infrastructure;
using SpyGame.Web.Localization;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services;
using SpyGame.Web.Services.Email;
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

// Lasting data (accounts, scores, data-protection keys): Neon Postgres. Tests plug in SQLite instead.
var databaseUrl = builder.Configuration["DATABASE_URL"];
if (!string.IsNullOrWhiteSpace(databaseUrl))
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(ConnectionStrings.Normalize(databaseUrl)));
else if (!builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("DATABASE_URL is required (dotnet user-secrets set DATABASE_URL ...).");

builder.Services.AddIdentity<Account, IdentityRole>(o =>
    {
        o.SignIn.RequireConfirmedEmail = true;
        o.User.RequireUniqueEmail = true;
        o.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789"; // the nickname rules
        o.Password.RequiredLength = 10;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "sg_auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // https in production
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/";
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
});
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
builder.Services.Configure<CookieTempDataProviderOptions>(o => o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
// The keys that protect cookies and e-mail links live in the database: a restart keeps them valid.
builder.Services.AddDataProtection().SetApplicationName("SpyGame").PersistKeysToDbContext<AppDbContext>();

builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
builder.Services.AddScoped<AccountEmails>();
builder.Services.AddSingleton<IRegisteredNames, DbRegisteredNames>();

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
    o.AddPolicy(AccountController.AuthPolicy, http => RateLimitPartition.GetFixedWindowLimiter(ClientIp.Of(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
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

// Bring the database schema up to date at start-up (Postgres only; the tests build theirs).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetService<AppDbContext>();
    if (db is not null && db.Database.IsNpgsql()) db.Database.Migrate();
}

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
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/healthz");
app.MapControllers();
app.MapHub<GameHub>(GameHub.Path);

app.Run();

// Lets the tests start the app with WebApplicationFactory<Program>.
public partial class Program;
