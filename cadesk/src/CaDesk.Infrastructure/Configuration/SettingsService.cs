using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Configuration;
using CaDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CaDesk.Infrastructure.Configuration;

public sealed record SettingItem(
    string Key, string Area, string Label, string ValueType, JsonElement Schema, int SortOrder,
    JsonElement Value, string Source,
    JsonElement? FirmValue, JsonElement Inherited, string InheritedSource,
    bool IsLocked, string? LockedBy, bool Editable, bool LockedForLowerLevels,
    bool ClientOverrideAllowed, bool UserOverrideAllowed, bool Capped);

public sealed record LevelEntry(string Level, string? Ref, JsonElement? Value, bool IsLocked);
public sealed record SettingResolution(string Key, JsonElement Value, string Source, bool IsLocked, bool Capped, IReadOnlyList<LevelEntry> Levels);

public sealed record SaveSettingRequest(JsonElement Value, string? Scope, Guid? ScopeId, bool? LockForLowerLevels, string? Reason);

public sealed record ChangeItem(Guid Id, string Key, string Level, string ScopeRef, JsonElement? OldValue, JsonElement? NewValue,
    bool OldLocked, bool NewLocked, string? Reason, Guid? ChangedBy, DateTime ChangedAt);

/// <summary>
/// Reads and writes settings for the current firm.
/// Reads come from a per-firm snapshot cached in memory; the cache key includes the firm's and the
/// platform's config_version, which a database trigger bumps on every change, so a change made on one
/// server is seen by every server on its next request.
/// </summary>
public sealed class SettingsService(AppDbContext db, ITenantContext tenant, IMemoryCache cache, TimeProvider clock) : ISettings
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private Snapshot? _snapshot; // memoised for this request

    // ================================================================ reading

    public async Task<T> GetAsync<T>(ConfigKey<T> key, Guid? clientId = null, Guid? userId = null, CancellationToken ct = default)
    {
        var r = await ResolveAsync(key.Name, clientId, userId, ct);
        return r.Value.Deserialize<T>(SettingsJson.Options)!;
    }

    public async Task<ResolvedValue> ResolveAsync(string key, Guid? clientId = null, Guid? userId = null, CancellationToken ct = default)
    {
        var s = await SnapshotAsync(ct);
        var def = s.Definition(key);
        return ConfigResolver.Resolve(def, s.ValuesFor(key), s.Context(clientId, userId));
    }

    /// <summary>Everything the firm Settings screen needs, grouped by area.</summary>
    public async Task<IReadOnlyList<SettingItem>> ListForFirmAsync(string? area, CancellationToken ct)
    {
        var s = await SnapshotAsync(ct);
        var ctx = s.Context(null, null);
        return s.Definitions.Values
            .Where(d => d.IsActive && (area is null || d.Area == area))
            .OrderBy(d => d.Area).ThenBy(d => d.SortOrder)
            .Select(d =>
            {
                var values = s.ValuesFor(d.Key);
                var effective = ConfigResolver.Resolve(d, values, ctx, ConfigScope.Firm);
                var inherited = ConfigResolver.Resolve(d, values, ctx, ConfigScope.Plan);
                var firmRow = values.FirstOrDefault(v => v.Scope == ConfigScope.Firm);
                var lockedAbove = inherited.IsLocked || inherited.LockedAt is not null;
                return new SettingItem(
                    d.Key, d.Area, Label(d), d.ValueType, ConfigResolver.Parse(d.JsonSchema), d.SortOrder,
                    effective.Value, SourceName(effective.Source),
                    firmRow is null ? null : ConfigResolver.Parse(firmRow.Value),
                    inherited.Value, SourceName(inherited.Source),
                    lockedAbove, lockedAbove ? SourceName(inherited.LockedAt) : null,
                    Editable: d.LowestScope >= ConfigScope.Firm && !lockedAbove,
                    LockedForLowerLevels: firmRow?.IsLocked ?? false,
                    ClientOverrideAllowed: d.LowestScope >= ConfigScope.Client && !lockedAbove && !(firmRow?.IsLocked ?? false),
                    UserOverrideAllowed: d.LowestScope >= ConfigScope.User && !lockedAbove && !(firmRow?.IsLocked ?? false),
                    Capped: effective.Capped);
            })
            .ToList();
    }

    /// <summary>Where a value comes from at every level, for "why is it this?" in the UI.</summary>
    public async Task<SettingResolution> ExplainAsync(string key, Guid? clientId, Guid? userId, CancellationToken ct)
    {
        var s = await SnapshotAsync(ct);
        var def = s.Definition(key);
        var ctx = s.Context(clientId, userId);
        var values = s.ValuesFor(key);
        var r = ConfigResolver.Resolve(def, values, ctx);
        var levels = ConfigResolver.Levels(ctx)
            .Where(l => l.Scope <= def.LowestScope)
            .Select(l =>
            {
                var v = values.FirstOrDefault(x => x.Scope == l.Scope && x.ScopeRef == l.Ref);
                return new LevelEntry(SourceName(l.Scope), l.Ref, v is null ? null : ConfigResolver.Parse(v.Value), v?.IsLocked ?? false);
            })
            .ToList();
        return new SettingResolution(key, r.Value, SourceName(r.Source), r.IsLocked, r.Capped, levels);
    }

    // ================================================================ writing

    /// <summary>
    /// Sets a value at firm, client or user level after checking: the level is allowed for this key,
    /// nothing above has locked it, the value passes validation, and the client/user belongs to this firm.
    /// </summary>
    public async Task SetAsync(string key, SaveSettingRequest r, Guid? actingUserId, ISecretStore secrets, CancellationToken ct)
    {
        var s = await SnapshotAsync(ct);
        var def = s.Definition(key);
        var scope = ParseScope(r.Scope);
        var scopeRef = await ScopeRefAsync(scope, r.ScopeId, actingUserId, ct);
        await EnsureWritableAsync(def, scope, r.ScopeId, actingUserId, ct);

        var errors = ConfigValidator.Validate(r.Value, ConfigResolver.Parse(def.JsonSchema));
        if (errors.Count > 0) throw new RequestValidationException(new Dictionary<string, string[]> { ["value"] = errors.ToArray() });

        if (def.ValueType == "secret_ref" && r.Value.GetString() is { Length: > 0 } secretName &&
            !await secrets.ExistsAsync(secretName, ct))
            throw new RequestValidationException("value", $"No secret named '{secretName}' exists. Save the secret first.");

        if (r.LockForLowerLevels is true && scope != ConfigScope.Firm)
            throw new RequestValidationException("lockForLowerLevels", "Only firm-level values can be locked.");

        var json = r.Value.GetRawText();
        var row = await db.ConfigValues.FirstOrDefaultAsync(v => v.Key == key && v.Scope == scope && v.ScopeRef == scopeRef, ct);
        var old = row?.Value;
        var oldLocked = row?.IsLocked ?? false;
        if (row is null)
        {
            row = new ConfigValue { Key = key, Scope = scope, ScopeRef = scopeRef, FirmId = tenant.FirmId };
            db.ConfigValues.Add(row);
        }
        row.Value = json;
        row.IsLocked = scope == ConfigScope.Firm && (r.LockForLowerLevels ?? row.IsLocked);
        row.UpdatedBy = actingUserId;
        row.UpdatedAt = Now;

        db.ConfigChangeLog.Add(new ConfigChangeLog
        {
            FirmId = tenant.FirmId, Key = key, Scope = scope, ScopeRef = scopeRef,
            OldValue = old, NewValue = json, OldLocked = oldLocked, NewLocked = row.IsLocked,
            Reason = Trim(r.Reason), ChangedBy = actingUserId, ChangedAt = Now,
        });
        await db.SaveChangesAsync(ct);
        _snapshot = null;
    }

    /// <summary>Removes the value at a level so the inherited one applies again.</summary>
    public async Task<bool> ResetAsync(string key, string? scopeName, Guid? scopeId, Guid? actingUserId, string? reason, CancellationToken ct)
    {
        var s = await SnapshotAsync(ct);
        var def = s.Definition(key);
        var scope = ParseScope(scopeName);
        var scopeRef = await ScopeRefAsync(scope, scopeId, actingUserId, ct);
        await EnsureWritableAsync(def, scope, scopeId, actingUserId, ct, resetting: true);

        var row = await db.ConfigValues.FirstOrDefaultAsync(v => v.Key == key && v.Scope == scope && v.ScopeRef == scopeRef, ct);
        if (row is null) return false;
        db.ConfigValues.Remove(row);
        db.ConfigChangeLog.Add(new ConfigChangeLog
        {
            FirmId = tenant.FirmId, Key = key, Scope = scope, ScopeRef = scopeRef,
            OldValue = row.Value, NewValue = null, OldLocked = row.IsLocked, NewLocked = false,
            Reason = Trim(reason), ChangedBy = actingUserId, ChangedAt = Now,
        });
        await db.SaveChangesAsync(ct);
        _snapshot = null;
        return true;
    }

    public async Task<IReadOnlyList<ChangeItem>> ChangesAsync(string? key, int take, CancellationToken ct)
    {
        var q = db.ConfigChangeLog.AsNoTracking();
        if (key is not null) q = q.Where(c => c.Key == key);
        var rows = await q.OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id) // ids are UUID v7: time-ordered
            .Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        return rows.Select(c => new ChangeItem(c.Id, c.Key, SourceName(c.Scope), c.ScopeRef,
            c.OldValue is null ? null : ConfigResolver.Parse(c.OldValue),
            c.NewValue is null ? null : ConfigResolver.Parse(c.NewValue),
            c.OldLocked, c.NewLocked, c.Reason, c.ChangedBy, c.ChangedAt)).ToList();
    }

    /// <summary>Puts back the value a change replaced (or removes it if it was inherited before).</summary>
    public async Task<bool> RevertAsync(Guid changeId, Guid? actingUserId, ISecretStore secrets, CancellationToken ct)
    {
        var c = await db.ConfigChangeLog.AsNoTracking().FirstOrDefaultAsync(x => x.Id == changeId, ct);
        if (c is null) return false;
        if (c.Scope == ConfigScope.User && c.ScopeRef != actingUserId?.ToString())
            throw new BusinessRuleException("Only the user themselves can change their preferences.", 403);
        Guid? scopeId = c.Scope is ConfigScope.Client or ConfigScope.User ? Guid.Parse(c.ScopeRef) : null;
        var reason = $"Undo change from {c.ChangedAt:yyyy-MM-dd HH:mm} UTC";

        if (c.OldValue is null)
            await ResetAsync(c.Key, SourceName(c.Scope), scopeId, actingUserId, reason, ct);
        else
            await SetAsync(c.Key, new SaveSettingRequest(ConfigResolver.Parse(c.OldValue), SourceName(c.Scope), scopeId,
                c.Scope == ConfigScope.Firm ? c.OldLocked : null, reason), actingUserId, secrets, ct);
        return true;
    }

    // ================================================================ helpers

    private async Task EnsureWritableAsync(ConfigDefinition def, ConfigScope scope, Guid? scopeId, Guid? actingUserId,
        CancellationToken ct, bool resetting = false)
    {
        if (scope < ConfigScope.Firm)
            throw new BusinessRuleException("Platform, country and plan values can only be changed by the platform team.", 403);
        if (scope > def.LowestScope)
            throw new RequestValidationException("scope", $"This setting can't be set per {SourceName(scope)}.");

        var s = await SnapshotAsync(ct);
        var ctx = s.Context(scope == ConfigScope.Client ? scopeId : null, scope == ConfigScope.User ? scopeId ?? actingUserId : null);
        var above = ConfigResolver.Resolve(def, s.ValuesFor(def.Key), ctx, (ConfigScope)((short)scope - 1));
        if (above.LockedAt is not null && !resetting)
            throw new BusinessRuleException($"This setting is fixed at {SourceName(above.LockedAt)} level and can't be changed here.");
    }

    private async Task<string> ScopeRefAsync(ConfigScope scope, Guid? scopeId, Guid? actingUserId, CancellationToken ct)
    {
        switch (scope)
        {
            case ConfigScope.Firm:
                return tenant.FirmId.ToString();
            case ConfigScope.Client:
                if (scopeId is not { } clientId || !await db.Clients.AnyAsync(c => c.Id == clientId, ct))
                    throw new RequestValidationException("scopeId", "Choose a client of this firm.");
                return clientId.ToString();
            case ConfigScope.User:
                var userId = scopeId ?? actingUserId ?? throw new RequestValidationException("scopeId", "Choose a user.");
                if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
                    throw new RequestValidationException("scopeId", "Choose a user of this firm.");
                return userId.ToString();
            default:
                throw new BusinessRuleException("Platform, country and plan values can only be changed by the platform team.", 403);
        }
    }

    private static ConfigScope ParseScope(string? name) => (name ?? "firm").ToLowerInvariant() switch
    {
        "firm" => ConfigScope.Firm,
        "client" => ConfigScope.Client,
        "user" => ConfigScope.User,
        "platform" => ConfigScope.Platform,
        "country" => ConfigScope.Country,
        "plan" => ConfigScope.Plan,
        _ => throw new RequestValidationException("scope", "Use firm, client or user."),
    };

    public static string SourceName(ConfigScope? scope) => scope switch
    {
        null => "default",
        ConfigScope.Platform => "platform",
        ConfigScope.Country => "country",
        ConfigScope.Plan => "plan",
        ConfigScope.Firm => "firm",
        ConfigScope.Client => "client",
        ConfigScope.User => "user",
        _ => "unknown",
    };

    private static string Label(ConfigDefinition d)
    {
        var labels = ConfigResolver.Parse(d.Labels);
        return labels.ValueKind == JsonValueKind.Object && labels.TryGetProperty("en", out var en) ? en.GetString() ?? d.Key : d.Key;
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, 300)];

    // ---------------------------------------------------------------- snapshot + cache

    private async Task<Snapshot> SnapshotAsync(CancellationToken ct)
    {
        if (_snapshot is not null) return _snapshot;

        var firm = await db.Firms.AsNoTracking()
            .Select(f => new { f.Id, f.CountryCode, f.PlanCode, f.ConfigVersion })
            .FirstOrDefaultAsync(ct) ?? throw new TenantNotSetException();
        var platformVersion = await db.Database
            .SqlQuery<int>($"select config_version as \"Value\" from platform_state where id = 1").FirstAsync(ct);

        var definitions = await cache.GetOrCreateAsync($"cfgdefs:{platformVersion}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await db.ConfigDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Key, ct);
        });

        var values = await cache.GetOrCreateAsync($"cfgvals:{firm.Id}:{firm.ConfigVersion}:{platformVersion}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var rows = await db.ConfigValues.AsNoTracking().ToListAsync(ct); // RLS + filter: global rows + this firm's
            return rows.GroupBy(v => v.Key).ToDictionary(g => g.Key, g => (IReadOnlyCollection<ConfigValue>)g.ToList());
        });

        return _snapshot = new Snapshot(firm.Id, firm.CountryCode.Trim(), firm.PlanCode, definitions!, values!);
    }

    private sealed record Snapshot(
        Guid FirmId, string Country, string Plan,
        Dictionary<string, ConfigDefinition> Definitions,
        Dictionary<string, IReadOnlyCollection<ConfigValue>> Values)
    {
        public ConfigDefinition Definition(string key) =>
            Definitions.TryGetValue(key, out var d) && d.IsActive
                ? d
                : throw new BusinessRuleException($"Unknown setting '{key}'.", 404);

        public IReadOnlyCollection<ConfigValue> ValuesFor(string key) =>
            Values.TryGetValue(key, out var v) ? v : [];

        public SettingsContext Context(Guid? clientId, Guid? userId) => new(FirmId, Country, Plan, clientId, userId);
    }
}
