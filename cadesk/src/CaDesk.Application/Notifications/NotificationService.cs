using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Compliance;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Notifications;
using CaDesk.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Notifications;

public sealed record NotificationDto(Guid Id, string RecipientType, string? RecipientName, Guid? ClientId, string? ClientName,
    string Channel, string ToAddress, string TemplateKey, string? Subject, string Body, string? RelatedType, Guid? RelatedId,
    string Status, DateTime ScheduledAt, int Attempts, string? LastError, DateTime? SentAt, DateTime CreatedAt);

public sealed record NotificationPage(IReadOnlyList<NotificationDto> Items, DateTime? NextBefore);

public sealed record PlanResult(int Queued, int Skipped);
public sealed record RunResult(int Queued, int Sent, int Failed, int Waiting);

public sealed record TemplateItem(Guid Id, string Key, string Channel, string? Subject, string Body, string? WaTemplate,
    IReadOnlyList<string>? WaParams, bool FirmOwn, IReadOnlyList<string> Placeholders);

public sealed record SaveMessageTemplateRequest(string? Subject, string Body, string? WaTemplate, List<string>? WaParams);
public sealed record PreviewRequest(string? Subject, string Body);
public sealed record PreviewResult(string? Subject, string Body);

/// <summary>
/// Queues reminders (client deadline reminders, overdue notices, staff digest, escalation, task events) into the outbox.
/// Every message has a dedupe key, so planning can run as often as needed without sending anything twice.
/// Sending is done by <see cref="NotificationDispatcher"/>.
/// </summary>
public sealed class NotificationService(IAppDbContext db, ISettings settings, ITenantContext tenant, TimeProvider clock)
{
    private DateTime NowUtc => clock.GetUtcNow().UtcDateTime;
    private IAppDbContext Db => db;

    // ------------------------------------------------------------ planning (job or "run now")

    public async Task<PlanResult> PlanAsync(CancellationToken ct)
    {
        // one planner per firm at a time (5-minute job on several servers, "send now")
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.Database.ExecuteSqlAsync($"select pg_advisory_xact_lock(hashtext('notify:' || app_current_firm()::text))", ct);

        var ctx = await FirmContextAsync(ct);
        var q = new Queue(this, ctx);

        await PlanClientRemindersAsync(ctx, q, ct);
        await PlanEscalationsAsync(ctx, q, ct);
        await PlanDigestsAsync(ctx, q, ct);

        await q.FlushAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new PlanResult(q.Added, q.Duplicates);
    }

    private async Task PlanClientRemindersAsync(FirmCtx ctx, Queue q, CancellationToken ct)
    {
        var fromDate = ctx.Today.AddDays(-7);
        var to = ctx.Today.AddDays(60);
        var rows = await (from d in db.Deadlines.AsNoTracking()
                          join c in db.Clients.AsNoTracking() on d.ClientId equals c.Id
                          where (d.Status == DeadlineStatus.Pending || d.Status == DeadlineStatus.InProgress)
                                && d.DueDate >= fromDate && d.DueDate <= to && c.IsActive
                                // the work is with the firm already (submitted for review): don't chase the client
                                && !db.Tasks.Any(t => t.DeadlineId == d.Id && (t.Status == WorkTaskStatus.InReview || t.Status == WorkTaskStatus.Done))
                          select new { d, c.Name, c.Email, c.Phone, CountryCode = c.CountryCode }).ToListAsync(ct);
        if (rows.Count == 0) return;

        var clientIds = rows.Select(r => r.d.ClientId).Distinct().ToList();
        // all contacts: a client whose contacts all opted out gets nothing (no fall-back to the client's own address)
        var contacts = (await db.ClientContacts.AsNoTracking().Where(x => clientIds.Contains(x.ClientId)).ToListAsync(ct))
            .ToLookup(x => x.ClientId);
        var typeNames = await db.ComplianceTypes.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.Name, ct);
        var calling = await CallingCodesAsync(ct);
        var perClient = new Dictionary<Guid, ClientRules>();

        foreach (var r in rows)
        {
            var d = r.d;
            if (!perClient.TryGetValue(d.ClientId, out var rules)) perClient[d.ClientId] = rules = await ClientRulesAsync(d.ClientId, ct);
            if (!rules.Enabled) continue;

            var daysLeft = d.DueDate.DayNumber - ctx.Today.DayNumber;
            string key, stage;
            if (daysLeft >= 0)
            {
                if (MessageRules.PickOffset(rules.Schedule, daysLeft) is not { } offset) continue;
                (key, stage) = (NotificationKeys.DeadlineReminder, $"r{offset}");
            }
            else (key, stage) = (NotificationKeys.DeadlineOverdue, "overdue");   // once, within a week of the due date

            var values = new Dictionary<string, string>
            {
                ["firm"] = ctx.FirmName, ["client"] = r.Name,
                ["compliance"] = typeNames.GetValueOrDefault(d.TypeCode, d.TypeCode), ["period"] = DeadlineRules.Label(d.PeriodKey),
                ["due_date"] = d.DueDate.ToString("d MMM yyyy"), ["days_left"] = MessageRules.DaysLeftText(Math.Max(0, daysLeft)),
                ["days_overdue"] = Math.Max(0, -daysLeft).ToString(),
            };
            var scheduled = MessageRules.NextAllowedUtc(NowUtc, ctx.Tz, rules.QuietStart, rules.QuietEnd);
            var cc = calling.GetValueOrDefault(r.CountryCode.Trim());

            // contacts who receive reminders; the client's own email / phone when there are none
            var people = contacts[d.ClientId].Where(c => c.ReceivesReminders)
                .Select(c => (Type: RecipientTypes.ClientContact, Id: (Guid?)c.Id, c.Name, c.Email, c.Phone)).ToList();
            if (!contacts[d.ClientId].Any()) people.Add((RecipientTypes.Client, d.ClientId, r.Name, r.Email, r.Phone));

            foreach (var p in people)
                foreach (var channel in rules.Channels)
                {
                    var address = channel switch
                    {
                        Channels.Email => MessageRules.LooksLikeEmail(p.Email) ? p.Email!.Trim() : null,
                        Channels.WhatsApp => MessageRules.ToWhatsAppNumber(p.Phone, cc),
                        _ => null,                                   // SMS: not available yet
                    };
                    if (address is null) continue;
                    await q.AddAsync(key, channel, p.Type, p.Id, p.Name, d.ClientId, address,
                        new Dictionary<string, string>(values) { ["contact"] = p.Name },
                        "deadline", d.Id, $"dl:{d.Id}:{stage}:{channel}:{p.Id}", scheduled, ct);
                }
        }
    }

    private async Task PlanEscalationsAsync(FirmCtx ctx, Queue q, CancellationToken ct)
    {
        var after = await settings.GetAsync(Keys.ReminderEscalateAfterDays, ct: ct);
        if (after <= 0) return;
        var newest = ctx.Today.AddDays(-after);
        var oldest = newest.AddDays(-7);                     // don't flood partners with old overdue items on the first run
        var rows = await (from d in db.Deadlines.AsNoTracking()
                          join c in db.Clients.AsNoTracking() on d.ClientId equals c.Id
                          where (d.Status == DeadlineStatus.Pending || d.Status == DeadlineStatus.InProgress)
                                && d.DueDate <= newest && d.DueDate >= oldest
                          select new { d, c.Name }).ToListAsync(ct);
        if (rows.Count == 0) return;

        var partners = await (from u in db.Users.AsNoTracking()
                              join ur in db.UserRoles.AsNoTracking() on u.Id equals ur.UserId
                              join ro in db.Roles.AsNoTracking() on ur.RoleId equals ro.Id
                              where u.IsActive && ro.Name == "Partner"
                              select new { u.Id, u.Name, u.Email }).Distinct().ToListAsync(ct);
        var typeNames = await db.ComplianceTypes.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.Name, ct);

        foreach (var r in rows)
            foreach (var p in partners)
                await q.AddAsync(NotificationKeys.Escalation, Channels.Email, RecipientTypes.User, p.Id, p.Name, r.d.ClientId, p.Email,
                    new Dictionary<string, string>
                    {
                        ["firm"] = ctx.FirmName, ["user"] = p.Name, ["client"] = r.Name,
                        ["compliance"] = typeNames.GetValueOrDefault(r.d.TypeCode, r.d.TypeCode), ["period"] = DeadlineRules.Label(r.d.PeriodKey),
                        ["due_date"] = r.d.DueDate.ToString("d MMM yyyy"), ["days_overdue"] = (ctx.Today.DayNumber - r.d.DueDate.DayNumber).ToString(),
                    },
                    "deadline", r.d.Id, $"esc:{r.d.Id}:{p.Id}", NowUtc, ct);
    }

    private async Task PlanDigestsAsync(FirmCtx ctx, Queue q, CancellationToken ct)
    {
        var at = TimeOnly.ParseExact(await settings.GetAsync(Keys.StaffDigestTime, ct: ct), "HH:mm");
        if (TimeOnly.FromDateTime(ctx.LocalNow) < at) return;

        var week = ctx.Today.AddDays(7);
        var active = await db.Tasks.AsNoTracking()
            .Where(t => t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled)
            .Select(t => new { t.Title, t.DueDate, t.Status, t.AssignedUserId, t.ReviewerUserId, t.SubmittedBy, t.ClientId })
            .ToListAsync(ct);
        var clientNames = await db.Clients.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var reviewers = (await (from u in db.Users
                                join ur in db.UserRoles on u.Id equals ur.UserId
                                join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                                where rp.PermissionCode == "tasks.review"
                                select u.Id).Distinct().ToListAsync(ct)).ToHashSet();
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive).Select(u => new { u.Id, u.Name, u.Email }).ToListAsync(ct);

        foreach (var u in users)
        {
            if (!await settings.GetAsync(Keys.StaffDailyDigest, userId: u.Id, ct: ct)) continue;
            var mine = active.Where(t => t.AssignedUserId == u.Id && t.Status != WorkTaskStatus.InReview).ToList();
            var overdue = mine.Count(t => t.DueDate < ctx.Today);
            var today = mine.Count(t => t.DueDate == ctx.Today);
            var weekCount = mine.Count(t => t.DueDate > ctx.Today && t.DueDate <= week);
            var review = active.Count(t => t.Status == WorkTaskStatus.InReview && t.SubmittedBy != u.Id &&
                                           (t.ReviewerUserId == u.Id || (t.ReviewerUserId == null && reviewers.Contains(u.Id))));
            if (overdue + today + weekCount + review == 0) continue;

            var list = string.Join("\n", mine.Where(t => t.DueDate <= week).OrderBy(t => t.DueDate).Take(15)
                .Select(t => $"• {t.DueDate:d MMM} — {t.Title}{(t.ClientId is { } c && clientNames.TryGetValue(c, out var n) ? $" ({n})" : "")}"));
            await q.AddAsync(NotificationKeys.DailyDigest, Channels.Email, RecipientTypes.User, u.Id, u.Name, null, u.Email,
                new Dictionary<string, string>
                {
                    ["firm"] = ctx.FirmName, ["user"] = u.Name, ["overdue_count"] = overdue.ToString(), ["today_count"] = today.ToString(),
                    ["week_count"] = weekCount.ToString(), ["review_count"] = review.ToString(), ["task_list"] = list,
                },
                null, null, $"digest:{u.Id}:{ctx.Today:yyyy-MM-dd}", NowUtc, ct);
        }
    }

    // ------------------------------------------------------------ events (called by TaskService; saved with its changes)

    public async Task TaskEventAsync(string key, WorkTask t, Guid toUserId, string? comment, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == toUserId && u.IsActive).Select(u => new { u.Id, u.Name, u.Email }).FirstOrDefaultAsync(ct);
        if (user is null) return;
        var ctx = await FirmContextAsync(ct);
        var clientName = t.ClientId is { } cid ? await db.Clients.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
        var q = new Queue(this, ctx);
        await q.AddAsync(key, Channels.Email, RecipientTypes.User, user.Id, user.Name, t.ClientId, user.Email,
            new Dictionary<string, string>
            {
                ["firm"] = ctx.FirmName, ["user"] = user.Name, ["task"] = t.Title, ["client"] = clientName ?? "—", ["comment"] = comment ?? "",
            },
            "task", t.Id, $"task:{t.Id}:{key}:{Guid.CreateVersion7()}", NowUtc, ct);
        q.Stage();          // saved by the caller's SaveChanges, in the same transaction as the task change
    }

    // ------------------------------------------------------------ the log

    /// <param name="clientFacingOnly">Only messages sent to the client or its contacts (not staff emails about the client).</param>
    public async Task<NotificationPage> ListAsync(string? status, string? channel, Guid? clientId, DateTime? before, int take, CancellationToken ct,
        bool clientFacingOnly = false)
    {
        var q = db.Notifications.AsNoTracking();
        if (clientFacingOnly) q = q.Where(n => n.RecipientType == RecipientTypes.Client || n.RecipientType == RecipientTypes.ClientContact);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(n => n.Status == status);
        if (!string.IsNullOrWhiteSpace(channel)) q = q.Where(n => n.Channel == channel);
        if (clientId is { } cid) q = q.Where(n => n.ClientId == cid);
        if (before is { } b)
        {
            var bu = b.Kind == DateTimeKind.Utc ? b : DateTime.SpecifyKind(b.ToUniversalTime(), DateTimeKind.Utc);
            q = q.Where(n => n.CreatedAt < bu);
        }
        take = Math.Clamp(take, 1, 200);
        var rows = await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(take + 1).ToListAsync(ct);
        var more = rows.Count > take;
        if (more) rows.RemoveAt(rows.Count - 1);
        var ids = rows.Where(r => r.ClientId != null).Select(r => r.ClientId!.Value).Distinct().ToList();
        var names = await db.Clients.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var items = rows.Select(n => new NotificationDto(n.Id, n.RecipientType, n.RecipientName, n.ClientId,
            n.ClientId is { } c ? names.GetValueOrDefault(c) : null, n.Channel, n.ToAddress, n.TemplateKey, n.Subject, n.Body,
            n.RelatedType, n.RelatedId, n.Status, n.ScheduledAt, n.Attempts, n.LastError, n.SentAt, n.CreatedAt)).ToList();
        return new NotificationPage(items, more ? items[^1].CreatedAt : null);
    }

    public async Task<bool> RetryAsync(Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return false;
        if (n.Status is not (NotificationStatus.Failed or NotificationStatus.Cancelled or NotificationStatus.Skipped))
            throw new BusinessRuleException("Only failed, skipped or cancelled messages can be sent again.");
        n.Status = NotificationStatus.Queued; n.ScheduledAt = NowUtc; n.Attempts = 0; n.LastError = null; n.LockedAt = null;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return false;
        if (n.Status != NotificationStatus.Queued) throw new BusinessRuleException("Only a message that is still waiting can be cancelled.");
        n.Status = NotificationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>A test email to the signed-in user, to check the firm's email set-up.</summary>
    public async Task<Guid> QueueTestEmailAsync(CancellationToken ct)
    {
        var me = tenant.UserId ?? throw new BusinessRuleException("Sign in as a user to send a test.", 403);
        var u = await db.Users.AsNoTracking().Where(x => x.Id == me).Select(x => new { x.Id, x.Name, x.Email }).FirstAsync(ct);
        var ctx = await FirmContextAsync(ct);
        var n = new Notification
        {
            RecipientType = RecipientTypes.User, RecipientId = u.Id, RecipientName = u.Name, Channel = Channels.Email, ToAddress = u.Email,
            TemplateKey = "test", Subject = $"Test email from CA Desk for {ctx.FirmName}",
            Body = $"Hello {u.Name},\n\nThis is a test. If you can read this, reminder emails from {ctx.FirmName} will reach your clients.",
            DedupeKey = $"test:{Guid.CreateVersion7()}", ScheduledAt = NowUtc,
        };
        db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);
        return n.Id;
    }

    // ------------------------------------------------------------ templates

    public async Task<IReadOnlyList<TemplateItem>> TemplatesAsync(CancellationToken ct)
    {
        var rows = await db.NotificationTemplates.AsNoTracking().ToListAsync(ct);
        return rows.GroupBy(t => (t.Key, t.Channel))
            .Select(g => g.FirstOrDefault(t => t.FirmId != null) ?? g.First())
            .OrderBy(t => t.Key).ThenBy(t => t.Channel)
            .Select(t => new TemplateItem(t.Id, t.Key, t.Channel, t.Subject, t.Body, t.WaTemplate, t.WaParams, t.FirmId != null,
                NotificationKeys.Placeholders.GetValueOrDefault(t.Key) ?? [])).ToList();
    }

    public async Task<TemplateItem> SaveTemplateAsync(string key, string channel, SaveMessageTemplateRequest r, CancellationToken ct)
    {
        if (!NotificationKeys.Placeholders.TryGetValue(key, out var allowed)) throw new BusinessRuleException("Unknown message.", 404);
        if (channel is not (Channels.Email or Channels.WhatsApp)) throw new BusinessRuleException("Unknown channel.", 404);
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Body) || r.Body.Length > 4000) e["body"] = ["Message text is required (max 4000 characters)."];
        if (channel == Channels.Email && (string.IsNullOrWhiteSpace(r.Subject) || r.Subject.Length > 200)) e["subject"] = ["Subject is required (max 200 characters)."];
        var used = MessageRules.PlaceholdersIn((r.Subject ?? "") + " " + (r.Body ?? "")).Concat(r.WaParams ?? []).Distinct();
        var bad = used.Where(p => !allowed.Contains(p)).ToList();
        if (bad.Count > 0) e["body"] = [$"Unknown placeholder(s): {string.Join(", ", bad)}. You can use: {string.Join(", ", allowed)}."];
        if (channel == Channels.WhatsApp && (string.IsNullOrWhiteSpace(r.WaTemplate) || !System.Text.RegularExpressions.Regex.IsMatch(r.WaTemplate, "^[a-z0-9_]{1,255}$")))
            e["waTemplate"] = ["Give the name of the WhatsApp template approved for your number (lower-case, digits, _)."];
        if (e.Count > 0) throw new RequestValidationException(e);

        var row = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.FirmId != null && t.Key == key && t.Channel == channel, ct);
        if (row is null) db.NotificationTemplates.Add(row = new NotificationTemplate { FirmId = tenant.FirmId, Key = key, Channel = channel });
        row.Subject = channel == Channels.Email ? r.Subject!.Trim() : null;
        row.Body = r.Body.Trim();
        row.WaTemplate = channel == Channels.WhatsApp ? r.WaTemplate : null;
        row.WaParams = channel == Channels.WhatsApp ? r.WaParams?.ToList() ?? [] : null;
        await db.SaveChangesAsync(ct);
        return new TemplateItem(row.Id, key, channel, row.Subject, row.Body, row.WaTemplate, row.WaParams, true, allowed);
    }

    public async Task<bool> ResetTemplateAsync(string key, string channel, CancellationToken ct)
    {
        var row = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.FirmId != null && t.Key == key && t.Channel == channel, ct);
        if (row is null) return false;
        db.NotificationTemplates.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Renders text with sample values, so a firm sees what clients will get.</summary>
    public async Task<PreviewResult> PreviewAsync(string key, PreviewRequest r, CancellationToken ct)
    {
        var ctx = await FirmContextAsync(ct);
        var sample = new Dictionary<string, string>
        {
            ["firm"] = ctx.FirmName, ["client"] = "Anand Textiles Pvt Ltd", ["contact"] = "Suresh Anand", ["compliance"] = "GSTR-3B (monthly)",
            ["period"] = "Oct 2026", ["due_date"] = "20 Nov 2026", ["days_left"] = "in 3 days", ["days_overdue"] = "2", ["user"] = "Rahul",
            ["task"] = "GSTR-3B (monthly) · Oct 2026", ["comment"] = "ITC for one bill is missing.", ["overdue_count"] = "1",
            ["today_count"] = "2", ["week_count"] = "5", ["review_count"] = "1",
            ["task_list"] = "• 20 Nov — GSTR-3B (monthly) · Oct 2026 (Anand Textiles Pvt Ltd)\n• 22 Nov — TDS payment · Oct 2026 (Kerala Spices LLP)",
        };
        var allowed = NotificationKeys.Placeholders.GetValueOrDefault(key) ?? [];
        var values = sample.Where(kv => allowed.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        return new PreviewResult(r.Subject is null ? null : MessageRules.Render(r.Subject, values), MessageRules.Render(r.Body ?? "", values));
    }

    // ------------------------------------------------------------ internals

    internal sealed record FirmCtx(string FirmName, TimeZoneInfo Tz, DateTime LocalNow, DateOnly Today,
        Dictionary<(string, string), NotificationTemplate> Templates);

    private sealed record ClientRules(bool Enabled, int[] Schedule, string[] Channels, TimeOnly QuietStart, TimeOnly QuietEnd);

    private async Task<ClientRules> ClientRulesAsync(Guid clientId, CancellationToken ct) => new(
        await settings.GetAsync(Keys.ReminderClientEnabled, clientId, ct: ct),
        await settings.GetAsync(Keys.ReminderDays, clientId, ct: ct),
        await settings.GetAsync(Keys.ReminderChannels, clientId, ct: ct),
        TimeOnly.ParseExact(await settings.GetAsync(Keys.ReminderQuietStart, clientId, ct: ct), "HH:mm"),
        TimeOnly.ParseExact(await settings.GetAsync(Keys.ReminderQuietEnd, clientId, ct: ct), "HH:mm"));

    private async Task<FirmCtx> FirmContextAsync(CancellationToken ct)
    {
        var firm = await db.Firms.AsNoTracking().Select(f => new { f.Name }).FirstAsync(ct);
        var tzName = await settings.GetAsync(Keys.TimeZone, ct: ct);
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(tzName, out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTimeFromUtc(NowUtc, tz);
        var rows = await db.NotificationTemplates.AsNoTracking().ToListAsync(ct);
        var templates = rows.GroupBy(t => (t.Key, t.Channel)).ToDictionary(g => g.Key, g => g.FirstOrDefault(t => t.FirmId != null) ?? g.First());
        return new FirmCtx(firm.Name, tz, local, DateOnly.FromDateTime(local), templates);
    }

    private async Task<Dictionary<string, string>> CallingCodesAsync(CancellationToken ct) =>
        (await db.Database.SqlQuery<string>($"select code || ':' || coalesce(calling_code, '') as \"Value\" from countries")
            .ToListAsync(ct)).Select(s => s.Split(':')).ToDictionary(p => p[0].Trim(), p => p[1]);

    /// <summary>Collects messages for one run, skipping ones already in the outbox (dedupe key).</summary>
    private sealed class Queue(NotificationService svc, FirmCtx ctx)
    {
        private readonly List<Notification> _pending = [];
        public int Added => _pending.Count;
        public int Duplicates { get; private set; }

        public async Task AddAsync(string key, string channel, string recipientType, Guid? recipientId, string? name, Guid? clientId,
            string address, Dictionary<string, string> values, string? relatedType, Guid? relatedId, string dedupe, DateTime scheduledAt,
            CancellationToken ct)
        {
            if (!ctx.Templates.TryGetValue((key, channel), out var tpl)) return;          // no message for this channel
            if (_pending.Any(n => n.DedupeKey == dedupe) ||
                await svc.Db.Notifications.AnyAsync(n => n.DedupeKey == dedupe, ct)) { Duplicates++; return; }

            var p = channel == Channels.WhatsApp
                ? JsonSerializer.Serialize(new { waTemplate = tpl.WaTemplate, waValues = (tpl.WaParams ?? []).Select(x => values.GetValueOrDefault(x, "")) })
                : "{}";
            _pending.Add(new Notification
            {
                RecipientType = recipientType, RecipientId = recipientId, RecipientName = name, ClientId = clientId, Channel = channel,
                ToAddress = address, TemplateKey = key,
                Subject = tpl.Subject is null ? null : MessageRules.Render(tpl.Subject, values),
                Body = MessageRules.Render(tpl.Body, values), Params = p,
                RelatedType = relatedType, RelatedId = relatedId, DedupeKey = dedupe, ScheduledAt = scheduledAt,
            });
        }

        public void Stage() => svc.Db.Notifications.AddRange(_pending);

        public async Task FlushAsync(CancellationToken ct)
        {
            Stage();
            await svc.Db.SaveChangesAsync(ct);
        }
    }
}
