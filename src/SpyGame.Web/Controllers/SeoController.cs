using Microsoft.AspNetCore.Mvc;
using SpyGame.Web.Infrastructure;

namespace SpyGame.Web.Controllers;

public class SeoController : Controller
{
    [HttpGet("sitemap.xml"), HttpHead("sitemap.xml")]
    public IActionResult Sitemap() => Content(Seo.Sitemap(), "application/xml; charset=utf-8");

    [HttpGet("robots.txt"), HttpHead("robots.txt")]
    public IActionResult Robots() => Content(Seo.Robots, "text/plain; charset=utf-8");
}
