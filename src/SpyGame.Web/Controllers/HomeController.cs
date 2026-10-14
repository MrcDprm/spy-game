using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using SpyGame.Web.Models.ViewModels;
using SpyGame.Web.Services;

namespace SpyGame.Web.Controllers;

public class HomeController(PlayerSessionService sessions) : Controller
{
    /// <summary>The start page: create a room or join one. A returning player's nickname is filled in.</summary>
    [HttpGet("/"), HttpHead("/")]
    public async Task<IActionResult> Index()
    {
        var nickname = (await sessions.FindAsync(HttpContext))?.Nickname;
        return View(new HomeViewModel
        {
            Create = { Nickname = nickname, CategoryIds = ["sports", "holiday", "food", "fun"] },
            Join = { Nickname = nickname },
        });
    }

    [HttpGet("lang/{code}")]
    public IActionResult Language(string code, string? returnUrl)
    {
        var culture = code == "en" ? "en" : "tr";
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                HttpOnly = true,
            });
        // Only local paths: an open redirect would let others abuse this link.
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
