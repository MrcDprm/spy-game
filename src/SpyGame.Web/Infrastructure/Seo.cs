using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SpyGame.Web.Infrastructure;

// Search engines: one address per page and language, structured data, sitemap.
public static class Seo
{
    public const string SiteUrl = "https://spygame.miracdeprem.com";
    public const string Image = SiteUrl + "/og-image.png";

    // Every page a search engine should know about; the key picks its title and description.
    public static readonly (string Path, string Key)[] Pages =
    [
        ("/", "home"),
        ("/leaderboard", "board"),
    ];

    /// <summary>Absolute address of a page in one language (English adds ?lang=en).</summary>
    public static string PageUrl(string path, bool english) => SiteUrl + path + (english ? "?lang=en" : "");

    /// <summary>The current address without the lang parameter (for the language switch).</summary>
    public static string WithoutLang(string path, IQueryCollection query)
    {
        var parts = query.Where(q => !q.Key.Equals("lang", StringComparison.OrdinalIgnoreCase))
            .SelectMany(q => q.Value.Select(v => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(v ?? "")}"))
            .ToList();
        return path + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");
    }

    /// <summary>JSON-LD that cannot close its script tag.</summary>
    public static string JsonLd(object data) =>
        JsonSerializer.Serialize(data).Replace("<", "\\u003c", StringComparison.Ordinal);

    public static string Sitemap()
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");
        foreach (var (path, _) in Pages)
        {
            var tr = PageUrl(path, english: false);
            var en = PageUrl(path, english: true);
            foreach (var loc in new[] { tr, en })
            {
                xml.AppendLine("  <url>");
                xml.AppendLine($"    <loc>{loc}</loc>");
                xml.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"tr\" href=\"{tr}\"/>");
                xml.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"{en}\"/>");
                xml.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"{tr}\"/>");
                xml.AppendLine("  </url>");
            }
        }
        xml.AppendLine("</urlset>");
        return xml.ToString();
    }

    public const string Robots =
        "User-agent: *\n" +
        "Allow: /\n" +
        // Rooms, accounts and the admin page are private or change every minute.
        "Disallow: /lang/\n" +
        "Disallow: /join/\n" +
        "Disallow: /r/\n" +
        "Disallow: /account/\n" +
        "Disallow: /admin\n" +
        "\n" +
        "Sitemap: " + SiteUrl + "/sitemap.xml\n";
}
