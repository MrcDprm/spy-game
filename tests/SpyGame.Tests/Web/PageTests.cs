using System.Net;

namespace SpyGame.Tests.Web;

public class PageTests(GameFactory factory) : IClassFixture<GameFactory>
{
    private HttpClient Client() => factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = GameFactory.Https });

    [Fact]
    public async Task Pages_carry_a_strict_content_security_policy_and_no_inline_script()
    {
        var response = await Client().GetAsync("/", TestContext.Current.CancellationToken);
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' https://nabiz.miracdeprem.com;", csp);
        // Only the game channel back home, plus the visitor counter.
        Assert.Contains("connect-src 'self' wss://localhost https://nabiz.miracdeprem.com;", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("require-trusted-types-for 'script'", csp);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Every <script> must load a file; an inline one would be blocked by the policy anyway.
        var inline = System.Text.RegularExpressions.Regex.Match(html, @"<script(?![^>]*\bsrc=)(?![^>]*application/(ld\+)?json)[^>]*>");
        Assert.False(inline.Success, $"inline script: {inline.Value}");
    }

    [Fact]
    public async Task The_language_switch_only_returns_to_this_site()
    {
        var response = await Client().GetAsync("/lang/en?returnUrl=https://evil.example/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.ToString());

        var english = new HttpRequestMessage(HttpMethod.Get, "/");
        english.Headers.Add("Cookie", response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        var html = await (await Client().SendAsync(english, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Find the spy among you.", html);
        Assert.Contains("<html lang=\"en\">", html);
    }

    [Fact]
    public async Task Turkish_text_is_written_as_is_and_missing_pages_get_a_friendly_404()
    {
        var html = await (await Client().GetAsync("/", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Aranızdaki casusu bulun.", html); // not &#x131;

        var missing = await Client().GetAsync("/no-such-page", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("Sayfa bulunamadı", await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_health_check_answers_over_plain_http_for_render()
    {
        var plain = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("http://localhost/") });
        Assert.Equal(HttpStatusCode.OK, (await plain.GetAsync("/healthz", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.TemporaryRedirect, (await plain.GetAsync("/", TestContext.Current.CancellationToken)).StatusCode);
    }
}
