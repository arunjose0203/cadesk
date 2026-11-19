using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using CaDesk.Application.Notifications;
using CaDesk.Domain.Notifications;

namespace CaDesk.Api.Notifications;

/// <summary>Notifications:Email in configuration. Mode "Smtp" sends; "Log" only writes to the log (development, tests).</summary>
public sealed class EmailOptions
{
    public string Mode { get; set; } = "Log";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>The platform's sending address (e.g. reminders@cadesk.in). The firm's name is shown as the sender name.</summary>
    public string FromAddress { get; set; } = "";
}

/// <summary>Notifications:WhatsApp in configuration. Mode "Cloud" calls the WhatsApp Cloud API; "Log" only logs.</summary>
public sealed class WhatsAppOptions
{
    public string Mode { get; set; } = "Log";
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v21.0/";
}

/// <summary>Email through an SMTP relay (Amazon SES, Zoho, Postmark SMTP, your own server ...).</summary>
public sealed class SmtpEmailChannel(EmailOptions o) : INotificationChannel
{
    public string Channel => Channels.Email;

    public async Task<SendResult> SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        MailAddress to;
        try { to = new MailAddress(m.To); } catch (FormatException) { return SendResult.GiveUp($"'{m.To}' is not a valid email address."); }

        static string OneLine(string? s) => new((s ?? "").Where(ch => !char.IsControl(ch)).ToArray());
        MailAddress from;
        try { from = new MailAddress(o.FromAddress, OneLine(m.FromName)); }
        catch (FormatException ex) { return SendResult.GiveUp($"Sender address problem: {ex.Message}"); }

        using var msg = new MailMessage
        {
            From = from,
            Subject = OneLine(m.Subject),
            Body = MessageRules.ToHtml(m.Body),
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
        };
        msg.To.Add(to);
        msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(m.Body, Encoding.UTF8, "text/plain"));
        if (m.ReplyTo is { } reply)
            try { msg.ReplyToList.Add(reply); } catch (FormatException) { /* a bad reply-to never blocks the reminder */ }
        msg.Headers.Add("X-CaDesk-Message", m.Id.ToString());

        using var smtp = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.EnableSsl,
            Credentials = string.IsNullOrEmpty(o.User) ? null : new NetworkCredential(o.User, o.Password),
            Timeout = 30_000,
        };
        try
        {
            await smtp.SendMailAsync(msg, ct);
            return SendResult.Sent(m.Id.ToString());
        }
        catch (SmtpFailedRecipientException ex) when (ex.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MailboxNameNotAllowed)
        {
            return SendResult.GiveUp($"Mailbox not available: {ex.Message}");
        }
        catch (SmtpException ex)
        {
            return SendResult.Retry(ex.Message);
        }
    }
}

/// <summary>WhatsApp Business Cloud API: template messages (needed to message clients outside a 24-hour chat window).</summary>
public sealed class WhatsAppCloudChannel(HttpClient http) : INotificationChannel
{
    public string Channel => Channels.WhatsApp;

    public async Task<SendResult> SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(m.WaTemplate)) return SendResult.Skip("No WhatsApp template is set for this message.");
        var payload = new
        {
            messaging_product = "whatsapp",
            to = m.To,
            type = "template",
            template = new
            {
                name = m.WaTemplate,
                language = new { code = m.WaLanguage ?? "en" },
                components = m.WaValues.Count == 0 ? null : new[]
                {
                    new { type = "body", parameters = m.WaValues.Select(v => new { type = "text", text = v }).ToArray() },
                },
            },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Uri.EscapeDataString(m.WaPhoneNumberId!)}/messages")
        {
            Content = JsonContent.Create(payload, options: new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", m.WaToken);

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0
                ? msgs[0].GetProperty("id").GetString() : null;
            return SendResult.Sent(id);
        }
        var error = body.Length > 500 ? body[..500] : body;
        // 4xx (bad number, template not approved, token wrong) won't fix itself; 429 / 5xx will be retried
        return (int)res.StatusCode is >= 400 and < 500 and not 429 ? SendResult.GiveUp($"WhatsApp {(int)res.StatusCode}: {error}")
                                                                   : SendResult.Retry($"WhatsApp {(int)res.StatusCode}: {error}");
    }
}

/// <summary>Development and tests: nothing leaves the server. Addresses containing "fail" fail, to exercise retries.</summary>
public sealed class LogChannel(string channel, ILogger<LogChannel> log) : INotificationChannel
{
    public string Channel => channel;

    public Task<SendResult> SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        if (m.To.Contains("fail", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(SendResult.Retry("Simulated failure (log mode)."));
        log.LogInformation("[{Channel} not sent - log mode] to {To}: {Subject}", channel, m.To, m.Subject ?? m.WaTemplate);
        return Task.FromResult(SendResult.Sent($"log-{m.Id}"));
    }
}
