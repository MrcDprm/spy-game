using System.Text.Encodings.Web;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Services.Email;

/// <summary>
/// The account e-mails, in the language the page was in. User data is HTML-encoded. At most
/// 3 e-mails per address per hour, so the forms cannot be used to flood someone's inbox;
/// the pages answer the same either way.
/// </summary>
public sealed class AccountEmails(IEmailSender mail, IRateCounter counter)
{
    public const int PerAddressPerHour = 3;
    private static readonly HtmlEncoder Html = HtmlEncoder.Default;

    private readonly ThrottledSender sender = new(mail, counter);

    private sealed class ThrottledSender(IEmailSender mail, IRateCounter counter)
    {
        public async Task SendAsync(string to, string subject, string html)
        {
            if (await counter.TryHitAsync("mail:" + to.Trim().ToLowerInvariant(), PerAddressPerHour, TimeSpan.FromHours(1)))
                await mail.SendAsync(to, subject, html);
        }
    }

    public Task ConfirmAsync(string to, string nickname, string link, bool english) => sender.SendAsync(to,
        english ? "Confirm your e-mail · Casus" : "E-postanı doğrula · Casus",
        Page(english
            ? $"Hi {Html.Encode(nickname)}, confirm your e-mail to keep your nickname and appear on the leaderboard. The link works for 24 hours."
            : $"Merhaba {Html.Encode(nickname)}, takma adını korumak ve sıralamaya girmek için e-postanı doğrula. Bağlantı 24 saat geçerli.",
            english ? "Confirm e-mail" : "E-postamı doğrula", link));

    public Task ResetAsync(string to, string link, bool english) => sender.SendAsync(to,
        english ? "Reset your password · Casus" : "Şifreni sıfırla · Casus",
        Page(english
            ? "Someone asked to reset the password of your Casus account. If it was not you, ignore this e-mail."
            : "Casus hesabının şifresini sıfırlama isteği geldi. Bu sen değilsen bu e-postayı yok sayabilirsin.",
            english ? "Choose a new password" : "Yeni şifre belirle", link));

    /// <summary>Sent instead of a second sign-up, so the form never tells whether an address is registered.</summary>
    public Task AlreadyRegisteredAsync(string to, string link, bool english) => sender.SendAsync(to,
        english ? "You already have an account · Casus" : "Zaten bir hesabın var · Casus",
        Page(english
            ? "Someone tried to sign up with this e-mail, but it already has a Casus account. If you forgot your password, you can reset it."
            : "Bu e-postayla yeniden kayıt olunmaya çalışıldı ama adresin zaten bir Casus hesabı var. Şifreni unuttuysan sıfırlayabilirsin.",
            english ? "Reset password" : "Şifremi sıfırla", link));

    private static string Page(string text, string button, string link) => $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;color:#1b1915">
          <p style="font:700 13px monospace;letter-spacing:3px;color:#c2362c">CASUS</p>
          <p>{text}</p>
          <p><a href="{Html.Encode(link)}" style="display:inline-block;padding:12px 20px;background:#c2362c;color:#fff;border-radius:10px;text-decoration:none;font-weight:700">{button}</a></p>
          <p style="font-size:12px;color:#777;word-break:break-all">{Html.Encode(link)}</p>
        </div>
        """;
}
