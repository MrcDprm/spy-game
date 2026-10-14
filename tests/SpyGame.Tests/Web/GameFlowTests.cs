using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Web;

public class GameFlowTests : IClassFixture<GameFactory>
{
    private readonly GameFactory _factory;
    private static int _nextIp;

    public GameFlowTests(GameFactory factory) => _factory = factory;

    private static string NewIp() => $"203.0.113.{Interlocked.Increment(ref _nextIp) % 250 + 1}";
    private static string NewName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private async Task<(string Code, List<GameChannel> Channels)> TableAsync(int players)
    {
        var host = _factory.NewBrowser(NewIp());
        var code = await host.CreateRoomAsync(NewName("host"));
        var browsers = new List<Browser> { host };
        for (var i = 1; i < players; i++)
        {
            var browser = _factory.NewBrowser(NewIp());
            await browser.JoinRoomAsync(NewName("guest"), code);
            browsers.Add(browser);
        }
        var channels = new List<GameChannel>();
        foreach (var browser in browsers) channels.Add(await browser.ConnectAsync(code));
        foreach (var channel in channels) await channel.WaitForAsync(v => v.Players.Count == players && v.Players.All(p => p.Connected));
        return (code, channels);
    }

    [Fact]
    public async Task A_whole_round_over_real_connections_and_the_spy_never_receives_the_place()
    {
        var (_, channels) = await TableAsync(4);
        Assert.Null(await channels[0].InvokeAsync("StartRound"));

        var views = new List<RoomView>();
        foreach (var channel in channels) views.Add(await channel.WaitForAsync(v => v.Phase == Phase.Asking));
        var spyIndex = views.FindIndex(v => v.Card!.IsSpy);
        Assert.Single(views, v => v.Card!.IsSpy);
        var spy = channels[spyIndex];
        var spyId = views[spyIndex].YouId;
        var place = views.First(v => !v.Card!.IsSpy).Card!.Place!;
        Assert.All(views.Where(v => !v.Card!.IsSpy), v => Assert.Equal(place, v.Card!.Place));

        // Everyone votes for the spy; the spy votes for someone else.
        // 3 of 4 is more than half: voting starts, so a 4th "ready" would be too late.
        foreach (var channel in channels.Take(3)) Assert.Null(await channel.InvokeAsync("ReadyToVote"));
        await channels[0].WaitForAsync(v => v.Phase == Phase.Voting);
        for (var i = 0; i < channels.Count; i++)
        {
            var target = i == spyIndex ? views[(spyIndex + 1) % 4].YouId : spyId;
            Assert.Null(await channels[i].InvokeAsync("Vote", target));
        }
        var guessing = await spy.WaitForAsync(v => v.Phase == Phase.LastGuess);
        Assert.True(guessing.Guess!.YouAreAccused);

        // Before the results, nothing the spy's connection received singled out the place.
        foreach (var raw in spy.Raw)
        {
            Assert.DoesNotContain("wordIndex", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("spyIds", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"place\":{", raw);
        }

        var wrong = guessing.Guess.Places!.ToList().FindIndex(w => w != place);
        Assert.Null(await spy.InvokeAsync("Guess", wrong));
        foreach (var channel in channels)
        {
            var result = (await channel.WaitForAsync(v => v.Phase == Phase.Results)).Result!;
            Assert.Equal(Team.Civilians, result.Winner);
            Assert.Equal([spyId], result.SpyIds);
            Assert.Equal(place, result.Place);
        }
        foreach (var channel in channels) await channel.DisposeAsync();
    }

    [Fact]
    public async Task Nobody_can_act_for_another_player_or_out_of_turn()
    {
        var (_, channels) = await TableAsync(3);
        Assert.Equal("not_host", await channels[1].InvokeAsync("StartRound"));
        Assert.Equal("wrong_phase", await channels[0].InvokeAsync("Vote", "someone"));
        Assert.Equal("unknown_player", await channels[0].InvokeAsync("Vote", new string('x', 100)));
        foreach (var channel in channels) await channel.DisposeAsync();
    }

    [Fact]
    public async Task The_clock_moves_the_round_on_without_anyone_doing_anything()
    {
        var (_, channels) = await TableAsync(3);
        Assert.Null(await channels[0].InvokeAsync("StartRound"));
        await channels[0].WaitForAsync(v => v.Phase == Phase.Asking);

        _factory.Clock.Advance(TimeSpan.FromMinutes(1)); // the 1-minute round is over
        await channels[0].WaitForAsync(v => v.Phase == Phase.Voting);
        _factory.Clock.Advance(TimeSpan.FromMinutes(1)); // nobody voted
        var result = await channels[0].WaitForAsync(v => v.Phase == Phase.Results);
        Assert.Equal(Team.Spies, result.Result!.Winner);
        foreach (var channel in channels) await channel.DisposeAsync();
    }

    [Fact]
    public async Task Closing_a_tab_shows_the_player_as_away_but_another_open_tab_keeps_them_here()
    {
        var host = _factory.NewBrowser(NewIp());
        var code = await host.CreateRoomAsync(NewName("host"));
        var guest = _factory.NewBrowser(NewIp());
        await guest.JoinRoomAsync(NewName("guest"), code);

        var hostChannel = await host.ConnectAsync(code);
        var tab1 = await guest.ConnectAsync(code);
        var tab2 = await guest.ConnectAsync(code);
        await hostChannel.WaitForAsync(v => v.Players.Count == 2 && v.Players.All(p => p.Connected));

        await tab1.DisposeAsync();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.All(hostChannel.Latest!.Players, p => Assert.True(p.Connected));
        await tab2.DisposeAsync();
        await hostChannel.WaitForAsync(v => v.Players.Any(p => !p.Connected));
        await hostChannel.DisposeAsync();
    }

    [Fact]
    public async Task The_game_channel_refuses_strangers_and_other_sites()
    {
        var host = _factory.NewBrowser(NewIp());
        var code = await host.CreateRoomAsync(NewName("host"));

        // A visitor with a session but not in this room: the connection is closed, no state sent.
        var stranger = _factory.NewBrowser(NewIp());
        await stranger.GetAsync("/");
        await stranger.CreateRoomAsync(NewName("other"));
        var channel = await stranger.ConnectAsync(code);
        var closed = new TaskCompletionSource();
        channel.Connection.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        if (channel.Connection.State != HubConnectionState.Disconnected)
            await Task.WhenAny(closed.Task, Task.Delay(5000, TestContext.Current.CancellationToken));
        Assert.Equal(HubConnectionState.Disconnected, channel.Connection.State);
        Assert.Null(channel.Latest);

        // Another site's page trying to open the channel with our cookie.
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/hubs/game/negotiate?room={code}&negotiateVersion=1");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Cookie", host.CookieHeader);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task A_flood_of_calls_is_cut_off()
    {
        var (_, channels) = await TableAsync(3);
        var answers = new List<string?>();
        for (var i = 0; i < 30; i++) answers.Add(await channels[1].InvokeAsync("StartRound"));
        Assert.Equal(20, answers.Count(a => a == "not_host"));
        Assert.Equal(10, answers.Count(a => a == "rate_limited"));
        foreach (var channel in channels) await channel.DisposeAsync();
    }
}

public class RoomPageTests : IClassFixture<GameFactory>
{
    private readonly GameFactory _factory;
    private static int _nextIp;

    public RoomPageTests(GameFactory factory) => _factory = factory;

    private static string NewIp() => $"198.51.100.{Interlocked.Increment(ref _nextIp) % 250 + 1}";

    [Fact]
    public async Task Forms_without_the_anti_forgery_token_are_refused()
    {
        var browser = _factory.NewBrowser(NewIp());
        var response = await browser.SubmitAsync("/", "/join", new() { ["Nickname"] = "ali", ["Code"] = "ABC234" }, withToken: false);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bad_nicknames_wrong_codes_and_taken_names_come_back_with_an_error()
    {
        var host = _factory.NewBrowser(NewIp());
        var name = "dup" + Guid.NewGuid().ToString("N")[..8];
        var code = await host.CreateRoomAsync(name);

        async Task<string> JoinError(string nickname, string roomCode)
        {
            var response = await _factory.NewBrowser(NewIp()).SubmitAsync($"/join/{roomCode}", "/join", new() { ["Nickname"] = nickname, ["Code"] = roomCode });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            return System.Text.RegularExpressions.Regex.Match(html, "data-error=\"([a-z_]+)\"").Groups[1].Value;
        }

        Assert.Equal("nickname_invalid_characters", await JoinError("Ayşe", code));
        Assert.Equal("nickname_in_use", await JoinError(name, code));
        Assert.Equal("room_not_found", await JoinError("veli" + Guid.NewGuid().ToString("N")[..6], "ZZZZZZ"));
    }

    [Fact]
    public async Task A_member_gets_the_room_page_with_its_invite_link_qr_and_texts()
    {
        var host = _factory.NewBrowser(NewIp());
        var code = await host.CreateRoomAsync("page" + Guid.NewGuid().ToString("N")[..8]);
        var response = await host.GetAsync($"/r/{code}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains($"data-room=\"{code}\"", html);
        Assert.Contains($"{GameFactory.PublicOrigin}/join/{code}", html); // the configured address, not the request's Host
        Assert.Contains("src=\"data:image/png;base64,", html);
        Assert.Contains("<script type=\"application/json\" id=\"texts\">", html);
        Assert.Contains("\"lobby.start\":", html); // the room script's texts are on the page
    }

    [Fact]
    public async Task The_room_page_sends_outsiders_to_the_join_form()
    {
        var browser = _factory.NewBrowser(NewIp());
        var response = await browser.GetAsync("/r/ABC234");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/join/ABC234", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task The_session_cookie_cannot_be_read_by_scripts()
    {
        var browser = _factory.NewBrowser(NewIp());
        var response = await browser.SubmitAsync("/", "/join", new() { ["Nickname"] = "kuki" + Guid.NewGuid().ToString("N")[..6], ["Code"] = "ZZZZZZ" });
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-sg_session="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase); // required by the __Host- prefix
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Creating_rooms_is_limited_per_address()
    {
        var browser = _factory.NewBrowser("192.0.2.77");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            var response = await browser.SubmitAsync("/", "/rooms", new()
            {
                ["Create.Nickname"] = "limit" + i, ["Create.CategoryIds[0]"] = "art", ["Create.RoundMinutes"] = "5", ["Create.SpyCount"] = "1",
            });
            statuses.Add(response.StatusCode);
        }
        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.Redirect));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}
