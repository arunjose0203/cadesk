using System.Text.Json;
using CaDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Audit;

public sealed record AuditItem(
    Guid Id, DateTime At, Guid? ActorId, string? ActorName, string ActorType,
    string Entity, string? EntityId, string Action, JsonElement? OldValues, JsonElement? NewValues, string? Ip);

public sealed record AuditPage(IReadOnlyList<AuditItem> Items, DateTime? NextBefore);

public sealed record AuditQuery(string? Entity, string? EntityId, Guid? ActorId, string? Action,
    DateTime? From, DateTime? To, DateTime? Before, int? Take);

/// <summary>Reads the firm's audit log, newest first, with simple filters and "load more" paging.</summary>
public sealed class AuditQueryService(AppDbContext db)
{
    public async Task<AuditPage> ListAsync(AuditQuery q, CancellationToken ct)
    {
        var take = Math.Clamp(q.Take ?? 50, 1, 200);
        var rows = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Entity)) rows = rows.Where(a => a.Entity == q.Entity);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) rows = rows.Where(a => a.EntityId == q.EntityId);
        if (q.ActorId is { } actor) rows = rows.Where(a => a.ActorId == actor);
        if (!string.IsNullOrWhiteSpace(q.Action)) rows = rows.Where(a => a.Action == q.Action);
        if (q.From is { } from) rows = rows.Where(a => a.At >= ToUtc(from));
        if (q.To is { } to) rows = rows.Where(a => a.At < ToUtc(to));
        if (q.Before is { } before) rows = rows.Where(a => a.At < ToUtc(before));

        var page = await rows.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Take(take + 1).ToListAsync(ct);
        var more = page.Count > take;
        if (more) page.RemoveAt(page.Count - 1);

        var actorIds = page.Where(a => a.ActorId != null).Select(a => a.ActorId!.Value).Distinct().ToList();
        var names = await db.Users.IgnoreQueryFilters()           // include removed users' names
            .Where(u => actorIds.Contains(u.Id) && u.FirmId == db.CurrentFirmId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var items = page.Select(a => new AuditItem(a.Id, a.At, a.ActorId,
            a.ActorId is { } id && names.TryGetValue(id, out var n) ? n : null, a.ActorType,
            a.Entity, a.EntityId, a.Action,
            a.OldValues is null ? null : Parse(a.OldValues), a.NewValues is null ? null : Parse(a.NewValues),
            a.Ip?.ToString())).ToList();

        // paging by time; rows sharing the boundary timestamp are rare (microsecond precision)
        return new AuditPage(items, more ? items[^1].At : null);
    }

    private static DateTime ToUtc(DateTime d) => d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d.ToUniversalTime(), DateTimeKind.Utc);

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

/// <summary>Once a day, makes sure the audit log has partitions for this month and the next three.</summary>
public sealed class AuditPartitionJob(DbMigrator migrator, ILogger<AuditPartitionJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try { await migrator.EnsureAuditPartitionsAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Audit partition upkeep failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
