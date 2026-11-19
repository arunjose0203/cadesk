using System.Net;
using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Common;
using CaDesk.Domain.Configuration;
using CaDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CaDesk.Infrastructure.Audit;

/// <summary>Per-request details the audit log records (set by the API's middleware).</summary>
public sealed class RequestContext
{
    public IPAddress? Ip { get; set; }
}

/// <summary>
/// Writes an audit row for every create, update and delete saved through EF, in the SAME transaction
/// as the change itself, so a change can't be saved without its audit record.
/// Updates store only the fields that changed. Secrets and hashes are masked.
/// Bulk operations (ExecuteUpdate/ExecuteDelete) bypass this: those call AuditWriter explicitly.
/// </summary>
public sealed class AuditInterceptor(ITenantContext tenant, RequestContext request, TimeProvider clock) : SaveChangesInterceptor
{
    /// <summary>Never written to the audit log in clear.</summary>
    public static readonly HashSet<string> Masked = new(StringComparer.Ordinal)
    {
        nameof(User.PasswordHash), nameof(RefreshToken.TokenHash), nameof(UserMfa.SecretProtected),
        nameof(UserMfa.RecoveryCodesHash), nameof(Secret.Ciphertext), nameof(Invitation.TokenHash),
    };

    /// <summary>Not audited here: logs of their own, or high-volume session plumbing.</summary>
    private static readonly HashSet<Type> Skipped = [typeof(AuditLog), typeof(ConfigChangeLog), typeof(RefreshToken),
        typeof(CaDesk.Domain.Notifications.Notification)];   // the outbox is its own log

    /// <summary>
    /// Not recorded: row versions, and sign-in bookkeeping that changes on every login
    /// (login history is in login_events). An update touching only these produces no audit row.
    /// </summary>
    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
    {
        nameof(TenantEntity.Version), nameof(User.LastLoginAt), nameof(User.FailedLoginCount),
        nameof(User.LockoutUntil), nameof(UserMfa.LastUsedStep),
    };

    /// <summary>Dates the generator recalculates (holidays, extensions); a change to only these is not audited.</summary>
    private static readonly HashSet<string> GeneratedDeadlineFields = new(StringComparer.Ordinal)
    {
        nameof(CaDesk.Domain.Compliance.Deadline.StatutoryDue), nameof(CaDesk.Domain.Compliance.Deadline.DueDate),
        nameof(CaDesk.Domain.Compliance.Deadline.InternalDue),
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditRows(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        AddAuditRows(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void AddAuditRows(DbContext? db)
    {
        if (db is null || !tenant.HasTenant) return;
        db.ChangeTracker.DetectChanges();

        var now = clock.GetUtcNow().UtcDateTime;
        var rows = new List<AuditLog>();
        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (Skipped.Contains(entry.Entity.GetType())) continue;
            // deadlines are created by the generator (dozens per client): only people's changes to them are audited
            if (entry.Entity is CaDesk.Domain.Compliance.Deadline && entry.State == EntityState.Added) continue;
            // 1 microsecond apart, so rows from one save keep a strict order (exact paging by time)
            var row = Build(entry, now.AddTicks(10 * rows.Count));
            if (row is not null) rows.Add(row);
        }
        if (rows.Count > 0) db.Set<AuditLog>().AddRange(rows);
    }

    private AuditLog? Build(EntityEntry entry, DateTime now)
    {
        Dictionary<string, object?>? oldValues = null, newValues = null;
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Create,
            EntityState.Deleted => AuditAction.Delete,
            _ => AuditAction.Update,
        };

        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (Ignored.Contains(name)) continue;
            if (entry.Entity is CaDesk.Domain.Compliance.Deadline && GeneratedDeadlineFields.Contains(name)) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    (newValues ??= new())[name] = Value(name, p.CurrentValue);
                    break;
                case EntityState.Deleted:
                    (oldValues ??= new())[name] = Value(name, p.OriginalValue);
                    break;
                case EntityState.Modified when p.IsModified && !Equals(p.OriginalValue, p.CurrentValue):
                    (oldValues ??= new())[name] = Value(name, p.OriginalValue);
                    (newValues ??= new())[name] = Value(name, p.CurrentValue);
                    if (name == nameof(ISoftDelete.DeletedAt) && p.OriginalValue is null && p.CurrentValue is not null)
                        action = AuditAction.Delete; // soft delete reads as a delete
                    break;
            }
        }
        if (entry.State == EntityState.Modified && newValues is null) return null; // nothing really changed

        var key = entry.Metadata.FindPrimaryKey();
        var entityId = key is null ? null : string.Join(":", key.Properties.Select(k => entry.Property(k.Name).CurrentValue));

        return new AuditLog
        {
            FirmId = tenant.FirmId,
            ActorId = tenant.UserId,
            ActorType = ActorTypeOf(tenant),
            Entity = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Ip = request.Ip,
            At = now,
        };
    }

    /// <summary>A person, a client using an upload link (no user account), or the system (jobs).</summary>
    internal static string ActorTypeOf(ITenantContext tenant) =>
        tenant.UserId is not null ? AuditActor.User
        : tenant.Role == AuditActor.ClientUser ? AuditActor.ClientUser
        : AuditActor.System;

    private static object? Value(string name, object? v) => v switch
    {
        null => null,
        _ when Masked.Contains(name) => "***",
        IPAddress ip => ip.ToString(),
        string or bool or int or long or short or decimal or double or Guid or DateTime or DateTimeOffset or uint => v,
        Enum e => e.ToString(),
        _ => JsonSerializer.SerializeToElement(v), // collections, dictionaries
    };
}

/// <summary>For changes that don't go through the change tracker (bulk deletes and updates).</summary>
public sealed class AuditWriter(Persistence.AppDbContext db, ITenantContext tenant, RequestContext request, TimeProvider clock)
    : CaDesk.Application.Documents.IAuditRecorder
{
    public Task RecordAsync(string entity, string? entityId, string action, object? oldValues, object? newValues, CancellationToken ct)
    {
        db.Set<AuditLog>().Add(new AuditLog
        {
            FirmId = tenant.FirmId,
            ActorId = tenant.UserId,
            ActorType = AuditInterceptor.ActorTypeOf(tenant),
            Entity = entity,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Ip = request.Ip,
            At = clock.GetUtcNow().UtcDateTime,
        });
        return db.SaveChangesAsync(ct);
    }
}
