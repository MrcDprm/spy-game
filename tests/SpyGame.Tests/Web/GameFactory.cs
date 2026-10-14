using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpyGame.Tests.Services;
using SpyGame.Web.Data;
using SpyGame.Web.Hubs;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services.Email;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Tests.Web;

/// <summary>E-mails are kept here instead of being sent; tests open the links in them.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly Lock _lock = new();
    private readonly List<(string To, string Subject, string Html)> _sent = [];

    public Task SendAsync(string to, string subject, string html)
    {
        lock (_lock) _sent.Add((to, subject, html));
        return Task.CompletedTask;
    }

    public IReadOnlyList<(string To, string Subject, string Html)> To(string address)
    {
        lock (_lock) return _sent.Where(m => m.To == address).ToList();
    }

    /// <summary>The path and query of the button link in the last e-mail to this address.</summary>
    public string LastLink(string address)
    {
        var html = To(address)[^1].Html;
        var href = System.Text.RegularExpressions.Regex.Match(html, "href=\"([^\"]+)\"").Groups[1].Value;
        return new Uri(System.Net.WebUtility.HtmlDecode(href)).PathAndQuery;
    }
}

/// <summary>
/// The real app in memory: in-memory game store (no Redis), SQLite instead of Postgres,
/// e-mails kept in a list, and a clock the test can move.
/// </summary>
public sealed class GameFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _database = new("DataSource=:memory:");

    public ManualClock Clock { get; } = new(DateTimeOffset.UtcNow);
    public FakeEmailSender Emails { get; } = new();

    public const string AdminEmail = "boss@example.com";

    // The app redirects plain HTTP to HTTPS outside Development, like in production.
    public static readonly Uri Https = new("https://localhost/");

    public GameFactory()
    {
        ClientOptions.BaseAddress = Https;
        _database.Open(); // an in-memory SQLite database lives as long as its connection
        // The schema must exist before the app starts: data protection reads its keys at start-up.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_database).Options);
        db.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // not Development: user-secrets (REDIS_URL) are not read
        builder.UseSetting("REDIS_URL", "");
        builder.UseSetting("ADMIN_EMAILS", GameFactory.AdminEmail);
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_database));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _database.Dispose();
    }

    public Browser NewBrowser(string ip) => new(this, ip);
}

/// <summary>
/// One visitor: keeps its own cookies, fills in forms with the anti-forgery token, and opens the
/// game channel like the room page does. Each browser has its own address for the rate limits.
/// </summary>
public sealed partial class Browser(GameFactory factory, string ip)
{
    private readonly HttpClient _http = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = GameFactory.Https });
    private readonly Dictionary<string, string> _cookies = [];

    public string CookieHeader => string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}"));

    public async Task<HttpResponseMessage> GetAsync(string path)
    {
        var response = await _http.SendAsync(Request(HttpMethod.Get, path));
        Remember(response);
        return response;
    }

    /// <summary>Opens <paramref name="formPage"/>, takes its anti-forgery token and posts the form.</summary>
    public async Task<HttpResponseMessage> SubmitAsync(string formPage, string action, Dictionary<string, string> fields, bool withToken = true)
    {
        var page = await (await GetAsync(formPage)).Content.ReadAsStringAsync();
        var data = fields.ToList();
        if (withToken) data.Add(new("__RequestVerificationToken", AntiForgeryToken().Match(page).Groups[1].Value));
        var request = Request(HttpMethod.Post, action);
        request.Content = new FormUrlEncodedContent(data);
        var response = await _http.SendAsync(request);
        Remember(response);
        return response;
    }

    public async Task<string> CreateRoomAsync(string nickname, string categories = "food", int minutes = 1, int spies = 1)
    {
        var fields = new Dictionary<string, string>
        {
            ["Create.Nickname"] = nickname, ["Create.RoundMinutes"] = $"{minutes}", ["Create.SpyCount"] = $"{spies}",
        };
        var response = await SubmitAsync("/", "/rooms", fields.Concat(categories.Split(',').Select((c, i) => new KeyValuePair<string, string>($"Create.CategoryIds[{i}]", c))).ToDictionary());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!.ToString().Split('/').Last();
    }

    public async Task JoinRoomAsync(string nickname, string code)
    {
        var response = await SubmitAsync($"/join/{code}", "/join", new() { ["Nickname"] = nickname, ["Code"] = code });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public async Task<GameChannel> ConnectAsync(string code)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(GameFactory.Https, $"hubs/game?room={code}"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.Headers["Cookie"] = CookieHeader;
            })
            .Build();
        var channel = new GameChannel(connection);
        await connection.StartAsync();
        return channel;
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (_cookies.Count > 0) request.Headers.Add("Cookie", CookieHeader);
        request.Headers.Add("CF-Connecting-IP", ip);
        return request;
    }

    private void Remember(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
        foreach (var value in values)
        {
            var pair = value.Split(';')[0].Split('=', 2);
            _cookies[pair[0]] = pair[1];
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiForgeryToken();
}

/// <summary>A hub connection that keeps every "room" message, both as raw JSON and as a view.</summary>
public sealed class GameChannel : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(GameJson.Options);
    private readonly Lock _lock = new();
    private readonly List<(string Raw, RoomView View)> _messages = [];
    private readonly List<TaskCompletionSource> _waiters = [];

    public HubConnection Connection { get; }

    public GameChannel(HubConnection connection)
    {
        Connection = connection;
        connection.On<JsonElement>("room", element =>
        {
            var raw = element.GetRawText();
            lock (_lock)
            {
                _messages.Add((raw, element.Deserialize<RoomView>(Json)!));
                foreach (var waiter in _waiters) waiter.TrySetResult();
                _waiters.Clear();
            }
        });
    }

    public IReadOnlyList<string> Raw { get { lock (_lock) return _messages.Select(m => m.Raw).ToList(); } }

    public RoomView? Latest { get { lock (_lock) return _messages.Count == 0 ? null : _messages[^1].View; } }

    /// <summary>Waits until a message matching <paramref name="condition"/> arrives (or has arrived).</summary>
    public async Task<RoomView> WaitForAsync(Func<RoomView, bool> condition, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            TaskCompletionSource next;
            lock (_lock)
            {
                if (_messages.Count > 0 && condition(_messages[^1].View)) return _messages[^1].View;
                next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add(next);
            }
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero || await Task.WhenAny(next.Task, Task.Delay(left)) != next.Task)
                throw new TimeoutException($"No matching room message; last phase: {Latest?.Phase}");
        }
    }

    public Task<string?> InvokeAsync(string method, params object[] args) =>
        Connection.InvokeCoreAsync<string?>(method, args);

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
