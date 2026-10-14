using System.Net;
using System.Text.RegularExpressions;

namespace SpyGame.Tests.Web;

public partial class AccountTests(GameFactory factory) : IClassFixture<GameFactory>
{
    private const string Password = "gizli-oda-2026";
    private static int _nextIp;

    private Browser NewBrowser() => factory.NewBrowser($"192.0.2.{Interlocked.Increment(ref _nextIp) % 250 + 1}");
    private static string NewName(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    [GeneratedRegex("data-error=\"([a-z_.]+)\"")]
    private static partial Regex ErrorCode();

    private static async Task<string> ErrorOf(HttpResponseMessage response) =>
        ErrorCode().Match(await response.Content.ReadAsStringAsync()).Groups[1].Value;

    private static Task<HttpResponseMessage> RegisterAsync(Browser browser, string nickname, string email, string password = Password, string? again = null) =>
        browser.SubmitAsync("/account/register", "/account/register", new()
        {
            ["Nickname"] = nickname, ["Email"] = email, ["Password"] = password, ["ConfirmPassword"] = again ?? password,
        });

    private static Task<HttpResponseMessage> LoginAsync(Browser browser, string email, string password = Password) =>
        browser.SubmitAsync("/account/login", "/account/login", new() { ["Email"] = email, ["Password"] = password });

    /// <summary>Signs up, opens the link in the e-mail and returns the logged-in browser.</summary>
    private async Task<Browser> ConfirmedAccountAsync(string nickname, string email)
    {
        var browser = NewBrowser();
        Assert.Equal("/account/check-email", (await RegisterAsync(browser, nickname, email)).Headers.Location!.ToString());
        var confirmed = await browser.GetAsync(factory.Emails.LastLink(email));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        return browser;
    }

    [Fact]
    public async Task Sign_up_confirm_and_play_as_the_account()
    {
        var name = NewName("hesap");
        var email = NewEmail();
        var browser = await ConfirmedAccountAsync(name, email);
        Assert.Contains("/account/confirm?userId=", factory.Emails.LastLink(email));

        var home = await (await browser.GetAsync("/")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains($">{name}</span>", home); // shown in the header

        // The room is created under the account's name, whatever the form says.
        var code = await browser.CreateRoomAsync("something" + "else");
        var channel = await browser.ConnectAsync(code);
        var view = await channel.WaitForAsync(v => v.Players.Count == 1);
        Assert.Equal(name, view.Players[0].Nickname);
        await channel.DisposeAsync();

        // Nobody else can take the name, now or later.
        var guest = await NewBrowser().SubmitAsync($"/join/{code}", "/join", new() { ["Nickname"] = name, ["Code"] = code });
        Assert.Equal("nickname_registered", await ErrorOf(guest));
    }

    [Fact]
    public async Task Logging_in_needs_the_right_password_and_then_a_confirmed_email()
    {
        var name = NewName("onay");
        var email = NewEmail();
        await RegisterAsync(NewBrowser(), name, email);

        Assert.Equal("login_failed", await ErrorOf(await LoginAsync(NewBrowser(), email, "yanlis-sifre-1")));
        Assert.Equal("login_failed", await ErrorOf(await LoginAsync(NewBrowser(), NewEmail())));       // unknown address: same answer
        Assert.Equal("email_not_confirmed", await ErrorOf(await LoginAsync(NewBrowser(), email)));   // only after the right password

        await NewBrowser().GetAsync(factory.Emails.LastLink(email));
        var login = await LoginAsync(NewBrowser(), email);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-sg_auth=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_a_while()
    {
        var email = NewEmail();
        await ConfirmedAccountAsync(NewName("kilit"), email);
        for (var i = 0; i < 5; i++) await LoginAsync(NewBrowser(), email, "yanlis-sifre-1");
        Assert.Equal("login_locked", await ErrorOf(await LoginAsync(NewBrowser(), email))); // even with the right one
    }

    [Fact]
    public async Task Signing_up_again_with_a_registered_email_reveals_nothing()
    {
        var email = NewEmail();
        await ConfirmedAccountAsync(NewName("ilk"), email);
        var again = await RegisterAsync(NewBrowser(), NewName("ikinci"), email);
        Assert.Equal("/account/check-email", again.Headers.Location!.ToString()); // same as a new sign-up
        Assert.StartsWith("Zaten bir hesabın var", factory.Emails.To(email)[^1].Subject); // a note to the owner, not a new account
        Assert.Contains("/account/reset?userId=", factory.Emails.LastLink(email)); // the owner gets a reset link
    }

    [Fact]
    public async Task Sign_up_checks_its_fields()
    {
        Assert.Equal("nickname_invalid_characters", await ErrorOf(await RegisterAsync(NewBrowser(), "Ayşe", NewEmail())));
        Assert.Equal("email_invalid", await ErrorOf(await RegisterAsync(NewBrowser(), NewName("e"), "not-an-email")));
        Assert.Equal("password_mismatch", await ErrorOf(await RegisterAsync(NewBrowser(), NewName("m"), NewEmail(), Password, Password + "x")));
        Assert.Equal("password_too_short", await ErrorOf(await RegisterAsync(NewBrowser(), NewName("s"), NewEmail(), "kisa12")));
        Assert.Equal("password_requires_digit", await ErrorOf(await RegisterAsync(NewBrowser(), NewName("d"), NewEmail(), "uzunamarakamsiz")));
    }

    [Fact]
    public async Task A_name_a_guest_is_using_cannot_be_registered_by_someone_else()
    {
        var name = NewName("misafir");
        await NewBrowser().CreateRoomAsync(name); // a guest holds the name
        Assert.Equal("nickname_in_use", await ErrorOf(await RegisterAsync(NewBrowser(), name, NewEmail())));
    }

    [Fact]
    public async Task Forgot_password_resets_with_the_emailed_link_and_reveals_nothing()
    {
        var email = NewEmail();
        await ConfirmedAccountAsync(NewName("unut"), email);

        var unknown = await NewBrowser().SubmitAsync("/account/forgot", "/account/forgot", new() { ["Email"] = NewEmail() });
        Assert.Equal("/account/check-email", unknown.Headers.Location!.ToString());

        var browser = NewBrowser();
        await browser.SubmitAsync("/account/forgot", "/account/forgot", new() { ["Email"] = email });
        var link = factory.Emails.LastLink(email);
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(GameFactory.Https, link).Query);
        var reset = await browser.SubmitAsync(link, "/account/reset", new()
        {
            ["UserId"] = query["userId"]!, ["Code"] = query["code"]!, ["Password"] = "yeni-sifre-2026", ["ConfirmPassword"] = "yeni-sifre-2026",
        });
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal("login_failed", await ErrorOf(await LoginAsync(NewBrowser(), email))); // the old password is gone
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(NewBrowser(), email, "yeni-sifre-2026")).StatusCode);

        // A used or made-up link does not work.
        var again = await browser.SubmitAsync(link, "/account/reset", new()
        {
            ["UserId"] = query["userId"]!, ["Code"] = query["code"]!, ["Password"] = "baska-sifre-2026", ["ConfirmPassword"] = "baska-sifre-2026",
        });
        Assert.Equal("link_invalid", await ErrorOf(again));
    }

    [Fact]
    public async Task Logging_out_turns_the_browser_back_into_a_new_guest()
    {
        var name = NewName("cikis");
        var browser = await ConfirmedAccountAsync(name, NewEmail());
        var logout = await browser.SubmitAsync("/", "/account/logout", []);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var home = await (await browser.GetAsync("/")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain($">{name}</span>", home);
        Assert.Contains("/account/login", home);
    }

    [Fact]
    public async Task A_browser_whose_sign_in_is_gone_no_longer_plays_as_the_account()
    {
        var name = NewName("kayip");
        var browser = await ConfirmedAccountAsync(name, NewEmail());
        await browser.CreateRoomAsync("ignored");      // the game session is now linked to the account
        browser.Drop("sg_auth");                       // logged out elsewhere, password reset, cookie expired
        var home = await (await browser.GetAsync("/")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain($"value=\"{name}\"", home); // the name is no longer filled in
        var create = await browser.SubmitAsync("/", "/rooms", new()
        {
            ["Create.Nickname"] = name, ["Create.CategoryIds[0]"] = "art", ["Create.RoundMinutes"] = "5", ["Create.SpyCount"] = "1",
        });
        Assert.Equal("nickname_registered", await ErrorOf(create));
    }

    [Fact]
    public async Task Logging_in_starts_a_fresh_game_session()
    {
        var email = NewEmail();
        await ConfirmedAccountAsync(NewName("taze"), email);
        var browser = NewBrowser();
        await browser.CreateRoomAsync(NewName("once")); // a game session from before logging in (or planted)
        var login = await LoginAsync(browser, email);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-sg_session=;") && c.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Links_in_emails_use_the_sites_own_address_even_with_a_forged_host()
    {
        var email = NewEmail();
        await ConfirmedAccountAsync(NewName("host"), email);
        var attacker = NewBrowser();
        attacker.Headers["X-Forwarded-Host"] = "evil.example";
        await attacker.SubmitAsync("/account/forgot", "/account/forgot", new() { ["Email"] = email });
        var html = factory.Emails.To(email)[^1].Html;
        Assert.Contains($"href=\"{GameFactory.PublicOrigin}/account/reset?", html);
        Assert.DoesNotContain("evil.example", html);
    }

    [Fact]
    public async Task One_address_gets_at_most_three_emails_an_hour()
    {
        var email = NewEmail();
        await RegisterAsync(NewBrowser(), NewName("sel"), email); // 1: the confirmation
        for (var i = 0; i < 5; i++)
        {
            var again = await RegisterAsync(NewBrowser(), NewName("sel"), email);
            Assert.Equal("/account/check-email", again.Headers.Location!.ToString()); // the page answers the same
        }
        Assert.Equal(3, factory.Emails.To(email).Count);
    }

    [Fact]
    public async Task A_bad_confirmation_link_is_refused()
    {
        var response = await NewBrowser().GetAsync("/account/confirm?userId=nobody&code=bad");
        Assert.Equal("account.confirm.failed", await ErrorOf(response));
    }
}
