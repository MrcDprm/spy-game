using System.Net.Http.Json;

namespace SpyGame.Web.Services.Email;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string html);
}

/// <summary>
/// Sends mail through Resend's HTTP API (RESEND_API_KEY). Without a key the message is not sent;
/// in development it goes to the log so the link can still be opened. Production logs never
/// contain links.
/// </summary>
public sealed class ResendEmailSender(HttpClient http, IConfiguration config, IHostEnvironment env, ILogger<ResendEmailSender> log) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string html)
    {
        var key = config["RESEND_API_KEY"];
        if (string.IsNullOrWhiteSpace(key))
        {
            if (env.IsDevelopment()) log.LogWarning("RESEND_API_KEY is not set. E-mail to {To}: {Subject}\n{Body}", to, subject, html);
            else log.LogError("RESEND_API_KEY is not set; an e-mail was not sent ({Subject}).", subject);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new { from = config["EMAIL_FROM"] ?? "Casus <casus@miracdeprem.com>", to = new[] { to }, subject, html }),
        };
        request.Headers.Authorization = new("Bearer", key);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            // Only the status: the response body can echo the address.
            log.LogError("Resend returned {Status} for {Subject}", (int)response.StatusCode, subject);
            throw new InvalidOperationException("E-mail could not be sent.");
        }
    }
}
