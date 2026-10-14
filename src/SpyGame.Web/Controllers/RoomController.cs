using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Models.ViewModels;
using SpyGame.Web.Services;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Controllers;

/// <summary>Creating and joining rooms (plain form posts), and the room page itself.</summary>
public class RoomController(RoomService rooms, PlayerSessionService sessions) : Controller
{
    public const string CreatePolicy = "create-room";
    public const string JoinPolicy = "join-room";

    [HttpPost("/rooms")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(CreatePolicy)]
    public async Task<IActionResult> Create([Bind(Prefix = "Create")] CreateRoomForm form)
    {
        var (session, error) = await WithNicknameAsync(form.Nickname);
        if (error is null)
        {
            var (createError, code) = await rooms.CreateAsync(session, form.ToSettings());
            if (createError is null) return RedirectToAction(nameof(Play), new { code });
            error = createError;
        }
        return View("~/Views/Home/Index.cshtml", new HomeViewModel { Create = form, Join = { Nickname = form.Nickname }, Error = error });
    }

    /// <summary>The link and QR code of a room lead here, with the code filled in.</summary>
    [HttpGet("/join/{code?}")]
    public async Task<IActionResult> Join(string? code)
    {
        var nickname = (await sessions.FindAsync(HttpContext))?.Nickname;
        return View(new JoinRoomForm { Code = RoomCode.Normalize(code), Nickname = nickname });
    }

    [HttpPost("/join")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(JoinPolicy)]
    public async Task<IActionResult> Join(JoinRoomForm form)
    {
        var (session, error) = await WithNicknameAsync(form.Nickname);
        var code = RoomCode.Normalize(form.Code);
        error ??= await rooms.JoinAsync(session, code);
        if (error is null) return RedirectToAction(nameof(Play), new { code });
        ViewData["Error"] = error;
        return View(form);
    }

    [HttpGet("/r/{code}")]
    public async Task<IActionResult> Play(string code)
    {
        code = RoomCode.Normalize(code);
        var session = await sessions.FindAsync(HttpContext);
        if (session is null || !RoomCode.IsValid(code) || await rooms.ViewAsync(code, session.PlayerId) is null)
            return RedirectToAction(nameof(Join), new { code }); // not in this room (yet)
        return View(new PlayViewModel { Code = code });
    }

    /// <summary>Starts or finds the browser's session and gives it the chosen nickname.</summary>
    private async Task<(PlayerSession Session, string? Error)> WithNicknameAsync(string? nickname)
    {
        var (token, session) = await sessions.GetOrCreateAsync(HttpContext);
        var (error, updated) = await rooms.ChooseNicknameAsync(session, nickname);
        if (error is null && updated != session) await sessions.SaveAsync(token, updated);
        return (updated, error);
    }
}
