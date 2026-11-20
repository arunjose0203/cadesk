using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Notifications;

/// <summary>What a channel needs to deliver one message, including the firm's own channel settings.</summary>
public sealed record OutgoingMessage(Guid Id, string To, string? Subject, string Body, string? ReplyTo, string FromName,
    string? WaTemplate, IReadOnlyList<string> WaValues, string? WaLanguage, string? WaPhoneNumberId, string? WaToken);

public sealed record SendResult(bool Ok, string? ProviderId = null, string? Error = null, bool Permanent = false, bool Skipped = false)
{
    /// <summary>Not sent and won't be tried again because the channel isn't available or set up.</summary>
    public static SendResult Skip(string reason) => new(false, null, reason, true, true);
    public static SendResult Sent(string? id) => new(true, id);
    public static SendResult Retry(string error) => new(false, null, error);
    public static SendResult GiveUp(string error) => new(false, null, error, true);
}

/// <summary>Delivers messages for one channel (email, whatsapp). Implemented in the API project.</summary>
public interface INotificationChannel
{
    string Channel { get; }
    Task<SendResult> SendAsync(OutgoingMessage m, CancellationToken ct);
}

/// <summary>
/// Sends due messages from the outbox. Messages are claimed with FOR UPDATE SKIP LOCKED, so several servers never send
/// the same one. Temporary failures retry with back-off (2, 4, 8, 16 minutes); after 5 attempts the message is "failed".
/// </summary>
public sealed class NotificationDispatcher(IAppDbContext db, ISettings settings, ISecretStore secrets, TimeProvider clock,
    IEnumerable<INotificationChannel> channels)
{
    public const int MaxAttempts = 5;

    public async Task<(int Sent, int Failed)> DispatchAsync(int max, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var stale = now.AddMinutes(-15);

        // a server that died mid-send leaves rows in "sending": put them back
        await db.Database.ExecuteSqlAsync($"""
            update notifications set status = 'queued', locked_at = null
             where firm_id = app_current_firm() and status = 'sending' and locked_at < {stale}
            """, ct);

        var ids = await db.Database.SqlQuery<Guid>($"""
            with picked as (
              select id from notifications
               where firm_id = app_current_firm() and status = 'queued' and scheduled_at <= {now}
               order by scheduled_at limit {max} for update skip locked)
            update notifications n set status = 'sending', locked_at = {now}, attempts = n.attempts + 1
              from picked where n.id = picked.id
            returning n.id as "Value"
            """).ToListAsync(ct);
        if (ids.Count == 0) return (0, 0);

        var rows = await db.Notifications.Where(n => ids.Contains(n.Id)).ToListAsync(ct);
        var cfg = await ChannelConfigAsync(ct);
        int sent = 0, failed = 0;

        // a reminder that waited (quiet hours, retry) is dropped if the deadline was filed meanwhile or the client was switched off
        var deadlineIds = rows.Where(n => n.RelatedType == "deadline" && n.RelatedId != null).Select(n => n.RelatedId!.Value).Distinct().ToList();
        var stillOpen = (await db.Deadlines.AsNoTracking()
                .Where(d => deadlineIds.Contains(d.Id) && (d.Status == "pending" || d.Status == "in_progress"))
                .Select(d => d.Id).ToListAsync(ct)).ToHashSet();
        var optedOut = (await db.ClientContacts.AsNoTracking().Where(c => !c.ReceivesReminders).Select(c => c.Id).ToListAsync(ct)).ToHashSet();

        foreach (var n in rows)
        {
            var toClient = n.RecipientType is RecipientTypes.Client or RecipientTypes.ClientContact;
            if (toClient && ((n.RelatedType == "deadline" && n.RelatedId is { } did && !stillOpen.Contains(did)) ||
                             (n.RecipientType == RecipientTypes.ClientContact && n.RecipientId is { } cid && optedOut.Contains(cid)) ||
                             (n.ClientId is { } client && !await settings.GetAsync(Keys.ReminderClientEnabled, client, ct: ct))))
            {
                await db.Database.ExecuteSqlAsync($"update notifications set status = {NotificationStatus.Cancelled}, last_error = 'No longer needed (filed, or reminders switched off).', locked_at = null where id = {n.Id}", ct);
                continue;
            }

            var channel = channels.FirstOrDefault(c => c.Channel == n.Channel);
            SendResult result;
            if (channel is null) result = SendResult.Skip($"{n.Channel} messages are not available yet.");
            else if (n.Channel == Channels.WhatsApp && (cfg.WaPhoneNumberId is null || cfg.WaToken is null))
                result = SendResult.Skip("WhatsApp is not set up: add the phone number ID and API token in Settings → Integrations.");
            else
            {
                try { result = await channel.SendAsync(ToMessage(n, cfg), ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { result = SendResult.Retry(ex.Message); }
            }

            if (result.Ok)
            {
                var sentAt = clock.GetUtcNow().UtcDateTime;
                await db.Database.ExecuteSqlAsync($"update notifications set status = 'sent', sent_at = {sentAt}, provider_message_id = {result.ProviderId}, last_error = null, locked_at = null where id = {n.Id}", ct);
                sent++;
            }
            else if (result.Permanent || n.Attempts >= MaxAttempts)
            {
                var st = result.Skipped ? NotificationStatus.Skipped : NotificationStatus.Failed;
                var err = Trim(result.Error);
                await db.Database.ExecuteSqlAsync($"update notifications set status = {st}, last_error = {err}, locked_at = null where id = {n.Id}", ct);
                failed++;
            }
            else
            {
                var nextSchedule = now.AddMinutes(Math.Pow(2, n.Attempts));
                var err = Trim(result.Error);
                await db.Database.ExecuteSqlAsync($"update notifications set status = 'queued', scheduled_at = {nextSchedule}, last_error = {err}, locked_at = null where id = {n.Id}", ct);
            }
        }
        return (sent, failed);
    }

    private sealed record Cfg(string FromName, string? ReplyTo, string? WaPhoneNumberId, string? WaToken, string WaLanguage);

    private async Task<Cfg> ChannelConfigAsync(CancellationToken ct)
    {
        var firmName = await db.Firms.AsNoTracking().Select(f => f.Name).FirstAsync(ct);
        var fromName = await settings.GetAsync(Keys.EmailFromName, ct: ct);
        var replyTo = await settings.GetAsync(Keys.EmailReplyTo, ct: ct);
        var phoneId = await settings.GetAsync(Keys.WhatsAppPhoneNumberId, ct: ct);
        var tokenRef = await settings.GetAsync(Keys.WhatsAppCredentialsRef, ct: ct);
        var token = string.IsNullOrWhiteSpace(tokenRef) ? null : await secrets.GetAsync(tokenRef, ct);
        return new Cfg(string.IsNullOrWhiteSpace(fromName) ? firmName : fromName,
            MessageRules.LooksLikeEmail(replyTo) ? replyTo : null,
            string.IsNullOrWhiteSpace(phoneId) ? null : phoneId, string.IsNullOrWhiteSpace(token) ? null : token,
            await settings.GetAsync(Keys.WhatsAppLanguage, ct: ct));
    }

    private static OutgoingMessage ToMessage(Notification n, Cfg cfg)
    {
        string? tpl = null;
        var values = new List<string>();
        if (n.Channel == Channels.WhatsApp)
        {
            using var doc = JsonDocument.Parse(n.Params);
            if (doc.RootElement.TryGetProperty("waTemplate", out var t)) tpl = t.GetString();
            if (doc.RootElement.TryGetProperty("waValues", out var v)) values = v.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        }
        return new OutgoingMessage(n.Id, n.ToAddress, n.Subject, n.Body, cfg.ReplyTo, cfg.FromName, tpl, values,
            cfg.WaLanguage, cfg.WaPhoneNumberId, cfg.WaToken);
    }

    private static string? Trim(string? s) => s is { Length: > 1000 } ? s[..1000] : s;
}
