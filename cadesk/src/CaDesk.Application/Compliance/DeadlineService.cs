using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Compliance;
using CaDesk.Application.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Compliance;

public sealed record DeadlineQuery(DateOnly? From = null, DateOnly? To = null, Guid? ClientId = null, string? TypeCode = null,
    string? Status = null, Guid? AssignedUserId = null, int Skip = 0, int Take = 100);

public sealed record DeadlineDto(Guid Id, Guid ClientId, string ClientCode, string ClientName, string TypeCode, string TypeName,
    string PeriodKey, string PeriodLabel, DateOnly PeriodEnd, DateOnly StatutoryDue, DateOnly DueDate, DateOnly InternalDue,
    string Status, bool IsOverdue, Guid? AssignedUserId, string? AssignedUserName, DateOnly? FiledOn, string? AckNo, string? Remarks,
    bool Extended, uint Version);

public sealed record DeadlineSummary(int Overdue, int DueThisWeek, int Open, int FiledThisMonth);
public sealed record DeadlinePage(IReadOnlyList<DeadlineDto> Items, int Total, DeadlineSummary Summary);

public sealed record UpdateDeadlineRequest(string Status, DateOnly? FiledOn, string? AckNo, string? Remarks, Guid? AssignedUserId, uint? Version);

public sealed record GenerateResult(int Clients, int Created, int Updated, int Removed, int TasksCreated = 0);

/// <summary>
/// Makes each client's deadlines from the country rules, keeps open ones in step with rule changes and
/// government extensions, and records filing. Filed deadlines are never changed by the generator.
/// </summary>
public sealed class DeadlineService(IAppDbContext db, ISettings settings, ITenantContext tenant, TimeProvider clock, TaskService tasks)
{
    /// <summary>New deadlines are created from this many days back (so a new client sees what is due now).</summary>
    public const int LookBackDays = 31;

    // ------------------------------------------------------------ generation

    /// <summary>Generates for the given clients, or every client of the firm when null.</summary>
    public async Task<GenerateResult> GenerateAsync(IReadOnlyCollection<Guid>? clientIds, CancellationToken ct)
    {
        // one generation per firm at a time (daily job on several servers, a request, a client edit)
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.Database.ExecuteSqlAsync($"select pg_advisory_xact_lock(hashtext('deadlines:' || app_current_firm()::text))", ct);

        var ctx = await LoadContextAsync(ct);
        var clientsQ = db.Clients.AsNoTracking();
        var only = clientIds?.ToList();
        if (only is not null) clientsQ = clientsQ.Where(c => only.Contains(c.Id));
        var clients = await clientsQ.ToListAsync(ct);

        var ids = clients.Select(c => c.Id).ToList();
        var choices = (await db.ClientCompliances.AsNoTracking().Where(x => ids.Contains(x.ClientId)).ToListAsync(ct))
            .ToLookup(x => x.ClientId);
        var horizon = ctx.Today.AddDays(-400);
        var existing = (await db.Deadlines.Where(d => ids.Contains(d.ClientId) && d.PeriodEnd >= horizon).ToListAsync(ct))
            .ToLookup(d => d.ClientId);

        int created = 0, updated = 0, removed = 0;
        var removable = new List<Deadline>();
        foreach (var client in clients)
        {
            var offset = await settings.GetAsync(Keys.ComplianceInternalOffsetDays, client.Id, ct: ct);
            var wanted = new Dictionary<(string, string), (Period P, DueDates D)>();
            foreach (var plan in EffectiveFor(client, choices[client.Id], ctx))
            {
                var countryCal = ctx.CountryCalendar(client.CountryCode.Trim());
                foreach (var p in DeadlineRules.Periods(plan.Frequency, ctx.Today.AddDays(-LookBackDays - 400), ctx.WindowEnd,
                             plan.FyStartMonth, plan.QuarterEndMonth))
                {
                    DateOnly? ext = ctx.Extensions.TryGetValue((plan.Type.Code, p.Key), out var e) ? e : null;
                    var dates = DeadlineRules.Compute(plan.Rule, p, ext, countryCal, ctx.Office, offset);
                    if (dates.Due < ctx.Today.AddDays(-LookBackDays) || dates.Due > ctx.WindowEnd) continue;
                    wanted[(plan.Type.Code, p.Key)] = (p, dates);
                }
            }

            var have = existing[client.Id].ToDictionary(d => (d.TypeCode, d.PeriodKey));
            foreach (var ((code, key), (p, dates)) in wanted)
            {
                if (have.TryGetValue((code, key), out var d))
                {
                    if (!DeadlineStatus.IsOpen(d.Status)) continue;     // filed / not applicable: leave alone
                    if (d.StatutoryDue == dates.Statutory && d.DueDate == dates.Due && d.InternalDue == dates.Internal) continue;
                    d.StatutoryDue = dates.Statutory; d.DueDate = dates.Due; d.InternalDue = dates.Internal;
                    updated++;
                }
                else
                {
                    db.Deadlines.Add(new Deadline
                    {
                        ClientId = client.Id, TypeCode = code, PeriodKey = key, PeriodStart = p.Start, PeriodEnd = p.End,
                        StatutoryDue = dates.Statutory, DueDate = dates.Due, InternalDue = dates.Internal,
                        AssignedUserId = client.AssignedUserId,
                    });
                    created++;
                }
            }

            // no longer applies (client lost its GSTIN, compliance switched off): drop untouched future ones only
            foreach (var d in have.Values)
            {
                if (wanted.ContainsKey((d.TypeCode, d.PeriodKey))) continue;
                if (d.Status == DeadlineStatus.Pending && d.DueDate >= ctx.Today && d.AckNo is null && d.Remarks is null)
                    removable.Add(d);
            }
        }

        // ... and not if someone has started its task (ticked a step, commented, started it)
        var worked = removable.Count == 0 ? [] : await tasks.DeadlinesWithWorkAsync(removable.Select(d => d.Id).ToList(), ct);
        foreach (var d in removable.Where(d => !worked.Contains(d.Id)))
        {
            db.Deadlines.Remove(d);      // its untouched task goes with it (on delete cascade)
            removed++;
        }

        await db.SaveChangesAsync(ct);

        // deadlines coming close get their task (assigned by the firm's rule, checklist from the template)
        var tasksCreated = await tasks.SyncFromDeadlinesAsync(ctx.Today, ct);
        await db.SaveChangesAsync(ct);

        if (tx is not null) await tx.CommitAsync(ct);
        return new GenerateResult(clients.Count, created, updated, removed, tasksCreated);
    }

    // ------------------------------------------------------------ reading and filing

    public async Task<DeadlinePage> ListAsync(DeadlineQuery q, CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var rows = from d in db.Deadlines.AsNoTracking()
                   join c in db.Clients.AsNoTracking() on d.ClientId equals c.Id     // hides deleted clients
                   select new { d, c.Code, c.Name };

        var summaryBase = rows;
        if (q.From is { } from) rows = rows.Where(x => x.d.DueDate >= from);
        if (q.To is { } to) rows = rows.Where(x => x.d.DueDate <= to);
        if (q.ClientId is { } cid) rows = rows.Where(x => x.d.ClientId == cid);
        if (!string.IsNullOrWhiteSpace(q.TypeCode)) rows = rows.Where(x => x.d.TypeCode == q.TypeCode);
        if (q.AssignedUserId is { } uid) rows = rows.Where(x => x.d.AssignedUserId == uid);
        rows = q.Status switch
        {
            "open" => rows.Where(x => x.d.Status == DeadlineStatus.Pending || x.d.Status == DeadlineStatus.InProgress),
            "overdue" => rows.Where(x => (x.d.Status == DeadlineStatus.Pending || x.d.Status == DeadlineStatus.InProgress) && x.d.DueDate < today),
            "pending" or "in_progress" or "filed" or "not_applicable" => rows.Where(x => x.d.Status == q.Status),
            _ => rows,
        };

        var total = await rows.CountAsync(ct);
        var page = await rows.OrderBy(x => x.d.DueDate).ThenBy(x => x.Name).ThenBy(x => x.d.TypeCode)
            .Skip(Math.Max(0, q.Skip)).Take(Math.Clamp(q.Take, 1, 500)).ToListAsync(ct);

        var types = await db.ComplianceTypes.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.Name, ct);
        var userIds = page.Where(x => x.d.AssignedUserId != null).Select(x => x.d.AssignedUserId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var extKeys = await ExtendedKeysAsync(page.Select(x => x.d), ct);

        var items = page.Select(x => ToDto(x.d, x.Code, x.Name, types, users, today, extKeys)).ToList();

        // summary over the same client / assignee filters, ignoring dates and status
        var s = summaryBase;
        if (q.ClientId is { } c2) s = s.Where(x => x.d.ClientId == c2);
        if (q.AssignedUserId is { } u2) s = s.Where(x => x.d.AssignedUserId == u2);
        var open = s.Where(x => x.d.Status == DeadlineStatus.Pending || x.d.Status == DeadlineStatus.InProgress);
        var weekEnd = today.AddDays(7);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var summary = new DeadlineSummary(
            await open.CountAsync(x => x.d.DueDate < today, ct),
            await open.CountAsync(x => x.d.DueDate >= today && x.d.DueDate <= weekEnd, ct),
            await open.CountAsync(ct),
            await s.CountAsync(x => x.d.Status == DeadlineStatus.Filed && x.d.FiledOn >= monthStart, ct));

        return new DeadlinePage(items, total, summary);
    }

    public async Task<DeadlineDto?> UpdateAsync(Guid id, UpdateDeadlineRequest r, CancellationToken ct)
    {
        var d = await db.Deadlines.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        if (r.Version is { } v && v != d.Version) throw new DbUpdateConcurrencyException("Deadline was changed by someone else.");

        var today = await TodayAsync(ct);
        var errors = new Dictionary<string, string[]>();
        if (!DeadlineStatus.All.Contains(r.Status)) errors["status"] = [$"Status must be one of: {string.Join(", ", DeadlineStatus.All)}."];
        var filedOn = r.Status == DeadlineStatus.Filed ? r.FiledOn ?? today : (DateOnly?)null;
        if (filedOn is { } f && (f > today || f < d.PeriodStart)) errors["filedOn"] = ["Filed date can't be in the future or before the period starts."];
        if (r.AckNo is { Length: > 100 }) errors["ackNo"] = ["Acknowledgement number is too long."];
        if (r.Remarks is { Length: > 1000 }) errors["remarks"] = ["Remarks can be at most 1000 characters."];
        if (r.AssignedUserId is { } uid && uid != d.AssignedUserId && !await db.Users.AnyAsync(u => u.Id == uid && u.IsActive, ct))
            errors["assignedUserId"] = ["Assigned user not found or inactive."];
        if (errors.Count > 0) throw new RequestValidationException(errors);

        await tasks.OnDeadlineStatusChangingAsync(d, r.Status, ct);
        d.Status = r.Status;
        d.FiledOn = filedOn;
        d.AckNo = string.IsNullOrWhiteSpace(r.AckNo) ? null : r.AckNo.Trim();
        d.Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim();
        d.AssignedUserId = r.AssignedUserId;
        await db.SaveChangesAsync(ct);

        var client = await db.Clients.AsNoTracking().Where(c => c.Id == d.ClientId).Select(c => new { c.Code, c.Name }).FirstAsync(ct);
        var types = await db.ComplianceTypes.AsNoTracking().Where(t => t.Code == d.TypeCode).ToDictionaryAsync(t => t.Code, t => t.Name, ct);
        var users = await db.Users.AsNoTracking().Where(u => u.Id == d.AssignedUserId).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return ToDto(d, client.Code, client.Name, types, users, today, await ExtendedKeysAsync([d], ct));
    }

    // ------------------------------------------------------------ per-client compliance choices

    public sealed record ClientComplianceItem(string Code, string Name, string Authority, string? Description, bool Enabled,
        bool ByRule, bool Chosen, string Frequency, IReadOnlyList<string> Frequencies, int? QuarterEndMonth, int? FyStartMonth,
        string AppliesTo);

    public sealed record SaveClientComplianceRequest(bool IsEnabled, string? Frequency, int? QuarterEndMonth, int? FyStartMonth);

    public async Task<IReadOnlyList<ClientComplianceItem>?> ClientCompliancesAsync(Guid clientId, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return null;
        var country = client.CountryCode.Trim();
        var types = await db.ComplianceTypes.AsNoTracking().Where(t => t.CountryCode == country && t.IsActive)
            .OrderBy(t => t.SortOrder).ToListAsync(ct);
        var choices = await db.ClientCompliances.AsNoTracking().Where(x => x.ClientId == clientId).ToDictionaryAsync(x => x.TypeCode, ct);

        return types.Select(t =>
        {
            var rule = Applicability.From(t.AppliesWhen);
            var byRule = t.AutoApply && rule.Matches(client.EntityType, client.TaxIds);
            choices.TryGetValue(t.Code, out var ch);
            return new ClientComplianceItem(t.Code, t.Name, t.Authority, t.Description, ch?.IsEnabled ?? byRule, byRule, ch is not null,
                ch?.Frequency ?? t.DefaultFrequency, t.Frequencies, ch?.QuarterEndMonth, ch?.FyStartMonth,
                (t.AutoApply ? "Automatic for: " : "Turn on when needed. Usually for: ") + rule.Describe());
        }).ToList();
    }

    public async Task<bool> SaveClientComplianceAsync(Guid clientId, string code, SaveClientComplianceRequest r, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return false;
        var type = await db.ComplianceTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code && t.IsActive, ct);
        if (type is null || type.CountryCode.Trim() != client.CountryCode.Trim()) return false;

        var errors = new Dictionary<string, string[]>();
        if (r.Frequency is not null && !type.Frequencies.Contains(r.Frequency))
            errors["frequency"] = [$"{type.Name} can be filed: {string.Join(", ", type.Frequencies)}."];
        if (r.QuarterEndMonth is not null and not (1 or 2 or 3)) errors["quarterEndMonth"] = ["Choose 1, 2 or 3."];
        if (r.FyStartMonth is not null and (< 1 or > 12)) errors["fyStartMonth"] = ["Month must be 1-12."];
        if (errors.Count > 0) throw new RequestValidationException(errors);

        var row = await db.ClientCompliances.FirstOrDefaultAsync(x => x.ClientId == clientId && x.TypeCode == code, ct);
        if (row is null) db.ClientCompliances.Add(row = new ClientCompliance { ClientId = clientId, TypeCode = code });
        row.IsEnabled = r.IsEnabled;
        row.Frequency = r.Frequency == type.DefaultFrequency ? null : r.Frequency;
        row.QuarterEndMonth = (short?)r.QuarterEndMonth;
        row.FyStartMonth = (short?)r.FyStartMonth;
        await db.SaveChangesAsync(ct);
        await GenerateAsync([clientId], ct);
        return true;
    }

    /// <summary>Back to the rule (removes the client's choice).</summary>
    public async Task<bool> ResetClientComplianceAsync(Guid clientId, string code, CancellationToken ct)
    {
        var row = await db.ClientCompliances.FirstOrDefaultAsync(x => x.ClientId == clientId && x.TypeCode == code, ct);
        if (row is null) return false;
        db.ClientCompliances.Remove(row);
        await db.SaveChangesAsync(ct);
        await GenerateAsync([clientId], ct);
        return true;
    }

    // ------------------------------------------------------------ firm holidays

    public sealed record HolidayDto(Guid Id, DateOnly Date, string Name, bool FirmOnly);

    public async Task<IReadOnlyList<HolidayDto>> HolidaysAsync(int year, CancellationToken ct)
    {
        var country = await FirmCountryAsync(ct);
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        return await db.Holidays.AsNoTracking()
            .Where(h => h.CountryCode == country && h.HolidayDate >= from && h.HolidayDate <= to)
            .OrderBy(h => h.HolidayDate)
            .Select(h => new HolidayDto(h.Id, h.HolidayDate, h.Name, h.FirmId != null)).ToListAsync(ct);
    }

    public async Task<HolidayDto> AddFirmHolidayAsync(DateOnly date, string name, CancellationToken ct)
    {
        name = name?.Trim() ?? "";
        if (name.Length is 0 or > 100) throw new RequestValidationException("name", "Give the holiday a name (max 100 characters).");
        if (await db.Holidays.AnyAsync(h => h.FirmId != null && h.HolidayDate == date, ct))
            throw new RequestValidationException("date", "This day is already a firm holiday.");
        var h = new Holiday { CountryCode = await FirmCountryAsync(ct), FirmId = tenant.FirmId, HolidayDate = date, Name = name };
        db.Holidays.Add(h);
        await db.SaveChangesAsync(ct);
        return new HolidayDto(h.Id, h.HolidayDate, h.Name, true);
    }

    public async Task<bool> DeleteFirmHolidayAsync(Guid id, CancellationToken ct)
    {
        var h = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id && x.FirmId != null, ct);
        if (h is null) return false;
        db.Holidays.Remove(h);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------------------------------------ helpers

    private sealed record Plan(ComplianceType Type, DueRule Rule, string Frequency, int QuarterEndMonth, int FyStartMonth);

    private sealed class GenContext
    {
        public required DateOnly Today { get; init; }
        public required DateOnly WindowEnd { get; init; }
        /// <summary>The tax year the law uses per country (India: April), not the firm's own books.</summary>
        public required Dictionary<string, int> StatutoryFyStart { get; init; }
        public required List<ComplianceType> Types { get; init; }
        public required Dictionary<string, (DueRule Rule, Applicability When)> Rules { get; init; }
        public required Dictionary<(string, string), DateOnly> Extensions { get; init; }
        public required Calendar Office { get; init; }
        public required Dictionary<string, Calendar> Countries { get; init; }
        public Calendar CountryCalendar(string code) => Countries.TryGetValue(code, out var c) ? c : Calendar.FromWeekend([0], []);
    }

    private static IEnumerable<Plan> EffectiveFor(Client client, IEnumerable<ClientCompliance> choices, GenContext ctx)
    {
        var chosen = choices.ToDictionary(c => c.TypeCode);
        foreach (var t in ctx.Types.Where(t => t.CountryCode.Trim() == client.CountryCode.Trim()))
        {
            var (rule, when) = ctx.Rules[t.Code];
            chosen.TryGetValue(t.Code, out var ch);
            var enabled = ch?.IsEnabled ?? (t.AutoApply && when.Matches(client.EntityType, client.TaxIds));
            if (!enabled) continue;
            var freq = ch?.Frequency is { } f && t.Frequencies.Contains(f) ? f : t.DefaultFrequency;
            yield return new Plan(t, rule, freq, ch?.QuarterEndMonth ?? 3,
                ch?.FyStartMonth ?? ctx.StatutoryFyStart.GetValueOrDefault(t.CountryCode.Trim(), 4));
        }
    }

    private async Task<GenContext> LoadContextAsync(CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var ahead = await settings.GetAsync(Keys.ComplianceGenerateAheadDays, ct: ct);
        var windowEnd = today.AddDays(ahead);
        var types = await db.ComplianceTypes.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
        var from = today.AddDays(-LookBackDays - 31);
        var to = windowEnd.AddDays(31);

        var holidays = await db.Holidays.AsNoTracking().Where(h => h.HolidayDate >= from && h.HolidayDate <= to).ToListAsync(ct);
        var countries = new Dictionary<string, Calendar>();
        var fyStart = new Dictionary<string, int>();
        foreach (var code in types.Select(t => t.CountryCode.Trim()).Distinct())
        {
            fyStart[code] = (await db.Database
                .SqlQuery<int>($"select statutory_fy_start_month::int as \"Value\" from countries where code = {code}").ToListAsync(ct))[0];
            var weekend = await db.Database
                .SqlQuery<int>($"select unnest(weekend_days)::int as \"Value\" from countries where code = {code}").ToListAsync(ct);
            countries[code] = Calendar.FromWeekend(weekend,
                holidays.Where(h => h.FirmId == null && h.CountryCode.Trim() == code).Select(h => h.HolidayDate));
        }

        var firmCountry = await FirmCountryAsync(ct);
        var workingDays = await settings.GetAsync(Keys.WorkingDays, ct: ct);
        var office = new Calendar(workingDays,
            holidays.Where(h => h.FirmId != null || h.CountryCode.Trim() == firmCountry).Select(h => h.HolidayDate));

        var extensions = (await db.ComplianceExtensions.AsNoTracking().ToListAsync(ct))
            .ToDictionary(e => (e.TypeCode, e.PeriodKey), e => e.ExtendedDue);

        return new GenContext
        {
            Today = today, WindowEnd = windowEnd,
            StatutoryFyStart = fyStart,
            Types = types,
            Rules = types.ToDictionary(t => t.Code, t => (DueRule.From(t), Applicability.From(t.AppliesWhen))),
            Extensions = extensions, Office = office, Countries = countries,
        };
    }

    private async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        var tzName = await settings.GetAsync(Keys.TimeZone, ct: ct);
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(tzName, out var found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).DateTime);
    }

    private async Task<string> FirmCountryAsync(CancellationToken ct) =>
        (await db.Firms.AsNoTracking().Select(f => f.CountryCode).FirstAsync(ct)).Trim();

    private async Task<HashSet<(string, string)>> ExtendedKeysAsync(IEnumerable<Deadline> ds, CancellationToken ct)
    {
        var codes = ds.Select(d => d.TypeCode).Distinct().ToList();
        return (await db.ComplianceExtensions.AsNoTracking().Where(e => codes.Contains(e.TypeCode))
                .Select(e => new { e.TypeCode, e.PeriodKey }).ToListAsync(ct))
            .Select(e => (e.TypeCode, e.PeriodKey)).ToHashSet();
    }

    private static DeadlineDto ToDto(Deadline d, string clientCode, string clientName, IReadOnlyDictionary<string, string> types,
        IReadOnlyDictionary<Guid, string> users, DateOnly today, HashSet<(string, string)> extended) =>
        new(d.Id, d.ClientId, clientCode, clientName, d.TypeCode, types.GetValueOrDefault(d.TypeCode, d.TypeCode),
            d.PeriodKey, DeadlineRules.Label(d.PeriodKey), d.PeriodEnd, d.StatutoryDue, d.DueDate, d.InternalDue, d.Status,
            DeadlineStatus.IsOpen(d.Status) && d.DueDate < today,
            d.AssignedUserId, d.AssignedUserId is { } u ? users.GetValueOrDefault(u) : null,
            d.FiledOn, d.AckNo, d.Remarks, extended.Contains((d.TypeCode, d.PeriodKey)), d.Version);
}
