using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Models.ViewModels;
using SpyGame.Web.Services;

namespace SpyGame.Web.Controllers;

/// <summary>"Spies of the week": this week's or last week's top 20, and where you stand.</summary>
public class LeaderboardController(LeaderboardService leaderboard) : Controller
{
    public const int Size = 20;

    [HttpGet("/leaderboard")]
    public async Task<IActionResult> Index(bool last = false)
    {
        var week = last ? Week.Previous(leaderboard.CurrentWeek) : leaderboard.CurrentWeek;
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View(new LeaderboardViewModel
        {
            Week = week,
            IsLastWeek = last,
            Starts = Week.StartOf(week),
            Lines = await leaderboard.TopAsync(week, Size),
            You = accountId is null ? null : await leaderboard.PositionAsync(week, accountId),
            YourAccountId = accountId,
        });
    }
}
