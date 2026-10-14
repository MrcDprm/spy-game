using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using SpyGame.Web.Data;
using SpyGame.Web.Infrastructure;
using SpyGame.Web.Localization;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Models.ViewModels;
using SpyGame.Web.Services;
using SpyGame.Web.Services.Email;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Controllers;

/// <summary>
/// Optional accounts: sign up, confirm the e-mail, log in and out, reset the password.
/// None of these pages tells whether an e-mail address is registered.
/// </summary>
[Route("account")]
public class AccountController(
    UserManager<Account> users,
    SignInManager<Account> signIn,
    PlayerSessionService sessions,
    INicknameRegistry nicknames,
    AccountEmails emails,
    TimeProvider clock) : Controller
{
    public const string AuthPolicy = "auth";
    private static readonly EmailAddressAttribute EmailCheck = new();

    private bool English => Culture.IsEnglish;

    [HttpGet("register")]
    public async Task<IActionResult> Register() =>
        View(new RegisterForm { Nickname = (await sessions.FindAsync(HttpContext))?.Nickname });

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthPolicy)]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        var nickname = Nickname.Clean(form.Nickname);
        var email = (form.Email ?? "").Trim();
        var error = Nickname.Problem(nickname)
            ?? (email.Length is 0 or > 254 || !EmailCheck.IsValid(email) ? "email_invalid" : null)
            ?? (form.Password != form.ConfirmPassword ? "password_mismatch" : null)
            ?? await PasswordProblemAsync(form.Password);
        if (error is null && await users.FindByNameAsync(nickname) is not null) error = "nickname_registered";
        if (error is null)
        {
            // A guest who is using this name right now keeps it: only its own session may register it.
            var (_, session) = await sessions.GetOrCreateAsync(HttpContext);
            if (!await nicknames.TryReserveAsync(nickname, session.PlayerId)) error = "nickname_in_use";
        }
        if (error is not null)
        {
            ViewData["Error"] = error;
            return View(form with { Password = null, ConfirmPassword = null });
        }

        if (await users.FindByEmailAsync(email) is { } existing)
        {
            users.PasswordHasher.HashPassword(existing, form.Password!); // as slow as a real sign-up, so timing tells nothing
            // Same answer as a new sign-up; the owner of the address gets a note instead.
            await emails.AlreadyRegisteredAsync(email, await ResetLinkAsync(existing), English);
            return RedirectToAction(nameof(CheckEmail));
        }

        var account = new Account { UserName = nickname, Email = email, CreatedAt = clock.GetUtcNow() };
        var result = await users.CreateAsync(account, form.Password!);
        if (!result.Succeeded)
        {
            ViewData["Error"] = IdentityErrorCode(result);
            return View(form with { Password = null, ConfirmPassword = null });
        }
        await emails.ConfirmAsync(email, nickname, await ConfirmLinkAsync(account), English);
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpGet("check-email")]
    public IActionResult CheckEmail() => View();

    [HttpGet("confirm")]
    public async Task<IActionResult> Confirm(string? userId, string? code)
    {
        var account = userId is null ? null : await users.FindByIdAsync(userId);
        if (account is null || code is null || !(await users.ConfirmEmailAsync(account, Decode(code))).Succeeded)
            return View("Message", "account.confirm.failed");
        sessions.Forget(HttpContext); // a fresh game session, linked to the account on the next request
        await signIn.SignInAsync(account, isPersistent: true);
        TempData["Notice"] = "account.confirm.done";
        return Redirect("/");
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl) => View(new LoginForm { ReturnUrl = returnUrl });

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthPolicy)]
    public async Task<IActionResult> Login(LoginForm form)
    {
        var account = await users.FindByEmailAsync((form.Email ?? "").Trim());
        string? error;
        if (account is null)
        {
            // Spend about the same time as a real check, so timing does not reveal registered addresses.
            users.PasswordHasher.VerifyHashedPassword(new Account(), DummyHash, form.Password ?? "");
            error = "login_failed";
        }
        else if (await users.IsLockedOutAsync(account)) error = "login_locked";
        else if (!await users.CheckPasswordAsync(account, form.Password ?? ""))
        {
            await users.AccessFailedAsync(account);
            error = await users.IsLockedOutAsync(account) ? "login_locked" : "login_failed";
        }
        // Only after the right password: otherwise this would tell strangers that the address exists.
        else if (!account.EmailConfirmed) error = "email_not_confirmed";
        else
        {
            await users.ResetAccessFailedCountAsync(account);
            sessions.Forget(HttpContext);
            await signIn.SignInAsync(account, isPersistent: true);
            return LocalRedirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl : "/");
        }
        ViewData["Error"] = error;
        return View(form with { Password = null });
    }

    [HttpPost("resend")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthPolicy)]
    public async Task<IActionResult> Resend(string? email)
    {
        if (await users.FindByEmailAsync((email ?? "").Trim()) is { EmailConfirmed: false } account)
            await emails.ConfirmAsync(account.Email!, account.UserName!, await ConfirmLinkAsync(account), English);
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        sessions.Forget(HttpContext); // play on as a new guest, not as the account
        return Redirect("/");
    }

    [HttpGet("forgot")]
    public IActionResult Forgot() => View(new ForgotForm());

    [HttpPost("forgot")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthPolicy)]
    public async Task<IActionResult> Forgot(ForgotForm form)
    {
        if (await users.FindByEmailAsync((form.Email ?? "").Trim()) is { EmailConfirmed: true } account)
            await emails.ResetAsync(account.Email!, await ResetLinkAsync(account), English);
        return RedirectToAction(nameof(CheckEmail)); // the same answer for any address
    }

    [HttpGet("reset")]
    public IActionResult Reset(string? userId, string? code) => View(new ResetForm { UserId = userId, Code = code });

    [HttpPost("reset")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AuthPolicy)]
    public async Task<IActionResult> Reset(ResetForm form)
    {
        var error = form.Password != form.ConfirmPassword ? "password_mismatch" : await PasswordProblemAsync(form.Password);
        var account = form.UserId is null ? null : await users.FindByIdAsync(form.UserId);
        if (error is null)
        {
            if (account is null || form.Code is null) error = "link_invalid";
            else
            {
                var result = await users.ResetPasswordAsync(account, Decode(form.Code), form.Password!);
                if (!result.Succeeded) error = IdentityErrorCode(result);
            }
        }
        if (error is not null)
        {
            ViewData["Error"] = error;
            return View(form with { Password = null, ConfirmPassword = null });
        }
        // Resetting changed the security stamp, which signs out every other device.
        await users.SetLockoutEndDateAsync(account!, null);
        TempData["Notice"] = "account.reset.done";
        return RedirectToAction(nameof(Login));
    }

    // --- helpers ---

    private static readonly string DummyHash = new PasswordHasher<Account>().HashPassword(new Account(), "not-a-real-password-1");

    private async Task<string?> PasswordProblemAsync(string? password)
    {
        if (string.IsNullOrEmpty(password)) return "password_required";
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, new Account(), password);
            if (!result.Succeeded) return IdentityErrorCode(result);
        }
        return null;
    }

    private static string IdentityErrorCode(IdentityResult result) => result.Errors.First().Code switch
    {
        "PasswordTooShort" => "password_too_short",
        "PasswordRequiresDigit" => "password_requires_digit",
        "PasswordRequiresLower" => "password_requires_letter",
        "InvalidToken" => "link_invalid",
        "DuplicateUserName" => "nickname_registered",
        _ => "try_again",
    };

    private async Task<string> ConfirmLinkAsync(Account account) =>
        PublicOrigin.Of(HttpContext) + Url.Action(nameof(Confirm), "Account", new { userId = account.Id, code = Encode(await users.GenerateEmailConfirmationTokenAsync(account)) });

    private async Task<string> ResetLinkAsync(Account account) =>
        PublicOrigin.Of(HttpContext) + Url.Action(nameof(Reset), "Account", new { userId = account.Id, code = Encode(await users.GeneratePasswordResetTokenAsync(account)) });

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string Decode(string code)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (FormatException) { return ""; }
    }
}
