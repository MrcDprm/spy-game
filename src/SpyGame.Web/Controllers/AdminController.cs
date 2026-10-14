using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpyGame.Web.Data;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Models.ViewModels;
using SpyGame.Web.Services;

namespace SpyGame.Web.Controllers;

/// <summary>Keeping the leaderboard honest: remove a week's points or keep an account off the board.</summary>
[Authorize(Policy = AdminPolicy)]
[Route("admin")]
public class AdminController(LeaderboardService leaderboard, AppDbContext db) : Controller
{
    public const string AdminPolicy = "admin";

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(new AdminViewModel
    {
        Week = leaderboard.CurrentWeek,
        Lines = await leaderboard.TopAsync(leaderboard.CurrentWeek, 100),
        Banned = await db.Users.Where(u => u.LeaderboardBanned).OrderBy(u => u.UserName).Select(u => new BannedAccount(u.Id, u.UserName!)).ToListAsync(),
    });

    [HttpPost("remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string accountId, string week)
    {
        if (Week.IsValid(week) && await db.Users.AnyAsync(u => u.Id == accountId)) await leaderboard.RemoveWeekAsync(accountId, week);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ban")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(string accountId, bool banned = true)
    {
        if (await db.Users.AnyAsync(u => u.Id == accountId)) await leaderboard.SetBannedAsync(accountId, banned);
        return RedirectToAction(nameof(Index));
    }
}
