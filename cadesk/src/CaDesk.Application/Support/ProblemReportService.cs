using CaDesk.Application.Abstractions;
using CaDesk.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CaDesk.Application.Support;

public sealed record ProblemReportRequest(string Kind, string Message, string? Page, string? Detail, string? AppVersion);
public sealed record ProblemReportDto(Guid Id, string Kind, string Message, string? Page, string? Detail, string? AppVersion,
    string? UserName, int Occurrences, string Status, DateTime CreatedAt, DateTime LastSeenAt);
public sealed record ProblemReportCreated(Guid Id, string Reference);

/// <summary>Crash reports from the browser and "Report a problem" messages (Sprint 72).</summary>
public sealed class ProblemReportService(IAppDbContext db, ITenantContext tenant, TimeProvider clock, ILogger<ProblemReportService> log)
{
    public const int MaxMessage = 2000, MaxDetail = 8000, MaxPage = 300;

    public async Task<ProblemReportCreated> CreateAsync(ProblemReportRequest r, string? userAgent, CancellationToken ct)
    {
        var kind = (r.Kind ?? "").Trim().ToLowerInvariant();
        if (kind is not (ProblemKinds.Crash or ProblemKinds.Feedback))
            throw new RequestValidationException("kind", "Kind must be crash or feedback.");
        var message = Clip(r.Message, MaxMessage);
        if (string.IsNullOrWhiteSpace(message)) throw new RequestValidationException("message", "Describe the problem.");
        var page = Clip(r.Page, MaxPage);
        var now = clock.GetUtcNow().UtcDateTime;

        if (kind == ProblemKinds.Crash)
        {
            var since = now.AddHours(-1);
            var same = await db.ProblemReports.FirstOrDefaultAsync(p => p.Kind == ProblemKinds.Crash && p.UserId == tenant.UserId
                && p.Message == message && p.Page == page && p.LastSeenAt >= since, ct);
            if (same is not null)
            {
                same.Occurrences++;
                same.LastSeenAt = now;
                await db.SaveChangesAsync(ct);
                return new ProblemReportCreated(same.Id, Reference(same.Id));
            }
        }

        var report = new ProblemReport
        {
            FirmId = tenant.FirmId, UserId = tenant.UserId, Kind = kind, Message = message!, Page = page,
            Detail = Clip(r.Detail, MaxDetail), UserAgent = Clip(userAgent, 300), AppVersion = Clip(r.AppVersion, 40),
            CreatedAt = now, LastSeenAt = now,
        };
        db.ProblemReports.Add(report);
        await db.SaveChangesAsync(ct);
        // in the server log too, so it shows up wherever the logs are watched
        log.LogWarning("Problem report {Reference} ({Kind}) from firm {FirmId} on {Page}: {Message}",
            Reference(report.Id), kind, tenant.FirmId, page, message);
        return new ProblemReportCreated(report.Id, Reference(report.Id));
    }

    public async Task<IReadOnlyList<ProblemReportDto>> ListAsync(string? status, int take, CancellationToken ct)
    {
        var q = db.ProblemReports.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.Status == status);
        var rows = await q.OrderByDescending(p => p.LastSeenAt).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        var userIds = rows.Where(r => r.UserId != null).Select(r => r.UserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return rows.Select(p => new ProblemReportDto(p.Id, p.Kind, p.Message, p.Page, p.Detail, p.AppVersion,
            p.UserId is { } id && names.TryGetValue(id, out var n) ? n : null, p.Occurrences, p.Status, p.CreatedAt, p.LastSeenAt)).ToList();
    }

    public async Task<bool> SetStatusAsync(Guid id, string status, CancellationToken ct)
    {
        if (status is not ("open" or "resolved")) throw new RequestValidationException("status", "Status must be open or resolved.");
        var p = await db.ProblemReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        p.Status = status;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Short reference a user can read out on a call: first 8 hex characters of the id.</summary>
    public static string Reference(Guid id) => "PR-" + id.ToString("N")[^8..].ToUpperInvariant();

    private static string? Clip(string? s, int max)
    {
        if (s is null) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }
}
