using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Tests.Web;

public class LeaderboardTests(GameFactory factory) : IClassFixture<GameFactory>
{
    private const string Password = "gizli-oda-2026";
    private static int _nextIp;

    private Browser NewBrowser() => factory.NewBrowser($"198.18.0.{Interlocked.Increment(ref _nextIp) % 250 + 1}");
    private static string NewName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private async Task<Browser> AccountAsync(string nickname, string? email = null)
    {
        email ??= $"{Guid.NewGuid():N}@example.com";
        var browser = NewBrowser();
        await browser.SubmitAsync("/account/register", "/account/register", new()
        {
            ["Nickname"] = nickname, ["Email"] = email, ["Password"] = Password, ["ConfirmPassword"] = Password,
        });
        await browser.GetAsync(factory.Emails.LastLink(email));
        return browser;
    }

    /// <summary>The admin account is created once per test class, then logged into.</summary>
    private async Task<Browser> AdminAsync()
    {
        var browser = NewBrowser();
        var login = await browser.SubmitAsync("/account/login", "/account/login", new() { ["Email"] = GameFactory.AdminEmail, ["Password"] = Password });
        if (login.StatusCode == HttpStatusCode.Redirect) return browser;
        return await AccountAsync(NewName("patron"), GameFactory.AdminEmail);
    }

    private async Task<string> PageAsync(Browser browser, string path) =>
        await (await browser.GetAsync(path)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    /// <summary>Three accounts play one round; the spy is caught and guesses wrong. Returns (civilians, spy).</summary>
    private async Task<(List<string> Civilians, string Spy)> PlayRoundAsync()
    {
        var names = new[] { NewName("lb"), NewName("lb"), NewName("lb") };
        var browsers = new List<Browser>();
        foreach (var name in names) browsers.Add(await AccountAsync(name));
        var code = await browsers[0].CreateRoomAsync(names[0]);
        foreach (var b in browsers.Skip(1)) await b.JoinRoomAsync(names[0], code); // the form name is ignored for accounts
        var channels = new List<GameChannel>();
        foreach (var b in browsers) channels.Add(await b.ConnectAsync(code));
        await channels[0].WaitForAsync(v => v.Players.Count == 3 && v.Players.All(p => p.Connected));

        Assert.Null(await channels[0].InvokeAsync("StartRound"));
        var views = new List<RoomView>();
        foreach (var c in channels) views.Add(await c.WaitForAsync(v => v.Phase == Phase.Asking));
        var spy = views.Single(v => v.Card!.IsSpy).YouId;
        foreach (var c in channels.Take(2)) await c.InvokeAsync("ReadyToVote");
        await channels[0].WaitForAsync(v => v.Phase == Phase.Voting);
        for (var i = 0; i < 3; i++)
            await channels[i].InvokeAsync("Vote", views[i].YouId == spy ? views.First(v => v.YouId != spy).YouId : spy);
        var spyChannel = channels[views.FindIndex(v => v.YouId == spy)];
        var guess = await spyChannel.WaitForAsync(v => v.Phase == Phase.LastGuess);
        var place = views.First(v => !v.Card!.IsSpy).Card!.Place;
        await spyChannel.InvokeAsync("Guess", guess.Guess!.Places!.ToList().FindIndex(w => w != place));
        var result = await channels[0].WaitForAsync(v => v.Phase == Phase.Results);
        foreach (var c in channels) await c.DisposeAsync();

        string Name(string id) => result.Players.Single(p => p.Id == id).Nickname;
        return (views.Where(v => v.YouId != spy).Select(v => Name(v.YouId)).ToList(), Name(spy));
    }

    [Fact]
    public async Task Points_of_confirmed_accounts_reach_the_weekly_leaderboard()
    {
        var (civilians, spy) = await PlayRoundAsync();
        var page = await PageAsync(NewBrowser(), "/leaderboard");
        foreach (var name in civilians) Assert.Contains(name, page);
        Assert.DoesNotContain(spy, page); // 0 points this round
        Assert.Contains("Misafirlerin puanları sıralamaya girmez.", page);

        // History is in the database: if Redis loses the board, it is rebuilt.
        var cache = factory.Services.GetRequiredService<ILeaderboardCache>();
        var week = factory.Services.GetRequiredService<LeaderboardService>().CurrentWeek;
        await cache.ReplaceAsync(week, []);
        Assert.False(await cache.ExistsAsync(week));
        page = await PageAsync(NewBrowser(), "/leaderboard");
        foreach (var name in civilians) Assert.Contains(name, page);
    }

    [Fact]
    public async Task Only_the_admin_reaches_the_admin_page()
    {
        Assert.Equal("/account/login?ReturnUrl=%2Fadmin", (await NewBrowser().GetAsync("/admin")).Headers.Location!.PathAndQuery);
        var player = await AccountAsync(NewName("oyuncu"));
        var refused = await player.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.StartsWith("/?ReturnUrl", refused.Headers.Location!.PathAndQuery);

        Assert.Equal(HttpStatusCode.OK, (await (await AdminAsync()).GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task The_admin_can_remove_points_and_keep_an_account_off_the_board()
    {
        var (civilians, _) = await PlayRoundAsync();
        var admin = await AdminAsync();
        var adminPage = await PageAsync(admin, "/admin");
        Assert.Contains(civilians[0], adminPage);

        // Account ids come from the admin page's own forms.
        string IdOf(string html, string name)
        {
            var at = html.IndexOf(name, StringComparison.Ordinal);
            var id = System.Text.RegularExpressions.Regex.Match(html[at..], "name=\"accountId\" value=\"([^\"]+)\"").Groups[1].Value;
            return id;
        }
        var week = factory.Services.GetRequiredService<LeaderboardService>().CurrentWeek;

        await admin.SubmitAsync("/admin", "/admin/remove", new() { ["accountId"] = IdOf(adminPage, civilians[0]), ["week"] = week });
        Assert.DoesNotContain(civilians[0], await PageAsync(NewBrowser(), "/leaderboard"));

        await admin.SubmitAsync("/admin", "/admin/ban", new() { ["accountId"] = IdOf(adminPage, civilians[1]) });
        Assert.DoesNotContain(civilians[1], await PageAsync(NewBrowser(), "/leaderboard"));
        var banned = await PageAsync(admin, "/admin");
        Assert.Contains(civilians[1], banned); // listed under "removed from the leaderboard"

        await admin.SubmitAsync("/admin", "/admin/ban", new() { ["accountId"] = IdOf(adminPage, civilians[1]), ["banned"] = "false" });
        Assert.Contains(civilians[1], await PageAsync(NewBrowser(), "/leaderboard")); // its history comes back
    }

    [Fact]
    public async Task Admin_actions_need_the_anti_forgery_token()
    {
        var admin = await AdminAsync();
        var response = await admin.SubmitAsync("/admin", "/admin/ban", new() { ["accountId"] = "x" }, withToken: false);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
