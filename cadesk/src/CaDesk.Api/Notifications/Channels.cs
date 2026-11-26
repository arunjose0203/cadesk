using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using CaDesk.Application.Notifications;
using CaDesk.Domain.Notifications;

namespace CaDesk.Api.Notifications;

/// <summary>Notifications:Email in configuration. Mode "Smtp", "SendGrid", "Ses" sends; "Log" only writes to the log (development, tests).</summary>
public sealed class EmailOptions
{
    public string Mode { get; set; } = "Log";
    public string Provider { get; set; } = "Smtp"; // Smtp, SendGrid, Ses
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>The platform's sending address (e.g. reminders@cadesk.in). The firm's name is shown as the sender name.</summary>
    public string FromAddress { get; set; } = "";
    public string SendGridApiKey { get; set; } = "";
    public string SesAccessKey { get; set; } = "";
    public string SesSecretKey { get; set; } = "";
    public string SesRegion { get; set; } = "ap-south-1";
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

        if (m.Attachments is { Count: > 0 } attachments)
        {
            foreach (var att in attachments)
            {
                var stream = new MemoryStream(att.Data);
                var a = new Attachment(stream, att.FileName, att.ContentType);
                msg.Attachments.Add(a);
            }
        }

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

/// <summary>Transactional Email through SendGrid v3 REST API.</summary>
public sealed class SendGridEmailChannel(HttpClient http, EmailOptions o) : INotificationChannel
{
    public string Channel => Channels.Email;

    public async Task<SendResult> SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(o.SendGridApiKey))
            return SendResult.Skip("SendGrid API Key is not configured.");

        var payload = new
        {
            personalizations = new[]
            {
                new { to = new[] { new { email = m.To } } }
            },
            from = new { email = o.FromAddress, name = m.FromName },
            reply_to = string.IsNullOrEmpty(m.ReplyTo) ? null : new { email = m.ReplyTo },
            subject = m.Subject ?? ("Notification from " + m.FromName),
            content = new[]
            {
                new { type = "text/plain", value = m.Body },
                new { type = "text/html", value = MessageRules.ToHtml(m.Body) }
            },
            attachments = m.Attachments?.Select(a => new
            {
                content = Convert.ToBase64String(a.Data),
                type = a.ContentType,
                filename = a.FileName,
                disposition = "attachment"
            }).ToArray()
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
        {
            Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.SendGridApiKey);

        using var res = await http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode)
        {
            var msgId = res.Headers.TryGetValues("X-Message-Id", out var ids) ? ids.FirstOrDefault() : m.Id.ToString();
            return SendResult.Sent(msgId);
        }

        var error = await res.Content.ReadAsStringAsync(ct);
        return (int)res.StatusCode >= 400 && (int)res.StatusCode < 500
            ? SendResult.GiveUp($"SendGrid {(int)res.StatusCode}: {error}")
            : SendResult.Retry($"SendGrid {(int)res.StatusCode}: {error}");
    }
}

/// <summary>Transactional Email through AWS Simple Email Service (SES) v2 REST API.</summary>
public sealed class AwsSesEmailChannel(EmailOptions o) : INotificationChannel
{
    public string Channel => Channels.Email;

    public async Task<SendResult> SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        // Require either SMTP relay host or SES credentials
        if (string.IsNullOrWhiteSpace(o.Host) && string.IsNullOrWhiteSpace(o.SesAccessKey))
            return SendResult.Skip("AWS SES endpoint or credentials are not configured.");

        // Fall back to SMTP relay if host is email-smtp.*.amazonaws.com
        if (!string.IsNullOrWhiteSpace(o.Host))
        {
            var smtpChannel = new SmtpEmailChannel(o);
            return await smtpChannel.SendAsync(m, ct);
        }

        return SendResult.GiveUp("AWS SES requires an SMTP relay host (Notifications:Email:Host) to send email.");
    }
}

/// <summary>WhatsApp Business Cloud API: template messages (needed to message clients outside a 24-hour chat window).</summary>
public sealed class WhatsAppCloudChannel(HttpClient http) : INotificationChannel
{
    public string Channel => Channels.WhatsApp;

    public async Task<SendResult> SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(m.WaTemplate)) return SendResult.Skip("No WhatsApp template is set for this message.");

        var componentsList = new List<object>();

        if (m.WaValues.Count > 0)
        {
            // Separate body text parameters from interactive button url parameters
            var bodyParams = m.WaValues.Where(v => !v.StartsWith("url_button:")).Select(v => new { type = "text", text = v }).ToArray();
            if (bodyParams.Length > 0)
            {
                componentsList.Add(new { type = "body", parameters = bodyParams });
            }

            var buttonParam = m.WaValues.FirstOrDefault(v => v.StartsWith("url_button:"));
            if (buttonParam != null)
            {
                var urlSuffix = buttonParam["url_button:".Length..];
                componentsList.Add(new
                {
                    type = "button",
                    sub_type = "url",
                    index = "0",
                    parameters = new[] { new { type = "text", text = urlSuffix } }
                });
            }
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = m.To,
            type = "template",
            template = new
            {
                name = m.WaTemplate,
                language = new { code = m.WaLanguage ?? "en" },
                components = componentsList.Count == 0 ? null : componentsList.ToArray(),
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
