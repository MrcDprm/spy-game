using Microsoft.AspNetCore.Mvc;
using SpyGame.Web.Models.ViewModels;
using SpyGame.Web.Services;

namespace SpyGame.Web.Controllers;

public class HomeController(PlayerSessionService sessions) : Controller
{
    /// <summary>The start page: create a room or join one. A returning player's nickname is filled in.</summary>
    public async Task<IActionResult> Index()
    {
        var nickname = (await sessions.FindAsync(HttpContext))?.Nickname;
        return View(new HomeViewModel
        {
            Create = { Nickname = nickname, CategoryIds = ["sports", "holiday", "food", "fun"] },
            Join = { Nickname = nickname },
        });
    }

    public IActionResult Error() => View();
}
