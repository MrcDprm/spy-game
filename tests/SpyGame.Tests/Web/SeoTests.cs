using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SpyGame.Web.Infrastructure;

namespace SpyGame.Tests.Web;

public class SeoTests(GameFactory factory) : IClassFixture<GameFactory>
{
    private HttpClient Client() => factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = GameFactory.Https });
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/", "VideoGame")]
    [InlineData("/leaderboard", "WebPage")]
    public async Task Public_pages_have_canonical_alternates_and_json_ld(string path, string type)
    {
        var html = await Client().GetStringAsync(path, Ct);
        var canonical = "https://spygame.miracdeprem.com" + path;
        Assert.Contains($"<link rel=\"canonical\" href=\"{canonical}\" />", html);
        Assert.Contains($"hreflang=\"en\" href=\"{canonical}?lang=en\"", html);
        Assert.DoesNotContain("name=\"robots\"", html);
        var json = Regex.Match(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(type, JsonDocument.Parse(json).RootElement.GetProperty("@type").GetString());
    }

    [Fact]
    public async Task Lang_query_selects_english()
    {
        var html = await Client().GetStringAsync("/?lang=en", Ct);
        Assert.Contains("<html lang=\"en\">", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://spygame.miracdeprem.com/?lang=en\" />", html);
    }

    [Theory]
    [InlineData("/account/login")]
    [InlineData("/join")]
    [InlineData("/no-such-page")]
    public async Task Private_and_error_pages_are_noindex(string path)
    {
        var html = await (await Client().GetAsync(path, Ct)).Content.ReadAsStringAsync(Ct);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\" />", html);
    }

    [Fact]
    public async Task Sitemap_and_robots()
    {
        var sitemap = await Client().GetAsync("/sitemap.xml", Ct);
        Assert.Equal("application/xml", sitemap.Content.Headers.ContentType?.MediaType);
        var doc = XDocument.Parse(await sitemap.Content.ReadAsStringAsync(Ct));
        Assert.Equal(4, doc.Descendants(XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9") + "loc").Count());
        Assert.Equal(HttpStatusCode.OK, (await Client().SendAsync(new HttpRequestMessage(HttpMethod.Head, "/sitemap.xml"), Ct)).StatusCode);

        var robots = await Client().GetStringAsync("/robots.txt", Ct);
        Assert.Contains("Disallow: /r/", robots);
        Assert.Contains("Sitemap: https://spygame.miracdeprem.com/sitemap.xml", robots);
    }

    [Fact]
    public void Language_switch_returns_without_the_lang_parameter()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["lang"] = "en" });
        Assert.Equal("/leaderboard", Seo.WithoutLang("/leaderboard", query));
    }
}
