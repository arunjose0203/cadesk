using System.Text.Json;
using CaDesk.Domain.Configuration;

namespace CaDesk.Application.Configuration;

/// <summary>Who a setting is being resolved for.</summary>
public sealed record SettingsContext(Guid FirmId, string CountryCode, string PlanCode, Guid? ClientId = null, Guid? UserId = null);

/// <summary>A setting's effective value and where it came from.</summary>
public sealed record ResolvedValue(
    string Key,
    JsonElement Value,
    ConfigScope? Source,          // null = catalogue default
    bool IsLocked,                // fixed by a level above the one asked about
    ConfigScope? LockedAt,
    bool Capped);                 // reduced to the plan ceiling

/// <summary>
/// Pure resolution rules (no database): given the definition and every visible value for the key,
/// work out the effective value at a level.
///
/// 1. Levels are Platform → Country → Plan → Firm → Client → User. Only levels up to the
///    definition's lowest scope (and up to the level asked about) count.
/// 2. A value marked locked stops every level below it.
/// 3. Otherwise the most specific value wins; with none, the catalogue default.
/// 4. When the plan value is a ceiling (plan_value_is_cap), it isn't a default: it caps numbers.
/// </summary>
public static class ConfigResolver
{
    public static ResolvedValue Resolve(
        ConfigDefinition def, IReadOnlyCollection<ConfigValue> values, SettingsContext ctx,
        ConfigScope upTo = ConfigScope.User)
    {
        var limit = (ConfigScope)Math.Min((short)upTo, (short)def.LowestScope);
        var levels = Levels(ctx).Where(l => l.Scope <= limit).ToList();

        ConfigValue? Find(ConfigScope scope, string? scopeRef) =>
            scopeRef is null ? null : values.FirstOrDefault(v => v.Key == def.Key && v.Scope == scope && v.ScopeRef == scopeRef);

        // 1. a lock at a broader level wins (plan locks count too)
        foreach (var (scope, scopeRef) in levels)
        {
            var v = Find(scope, scopeRef);
            if (v is { IsLocked: true })
                return Capped(def, values, ctx, new ResolvedValue(def.Key, Parse(v.Value), scope, scope < limit, scope, false));
        }

        // 2. most specific value wins (plan level skipped when it is a ceiling)
        for (var i = levels.Count - 1; i >= 0; i--)
        {
            var (scope, scopeRef) = levels[i];
            if (scope == ConfigScope.Plan && def.PlanValueIsCap) continue;
            var v = Find(scope, scopeRef);
            if (v is not null)
                return Capped(def, values, ctx, new ResolvedValue(def.Key, Parse(v.Value), scope, false, null, false));
        }

        // 3. catalogue default
        return Capped(def, values, ctx, new ResolvedValue(def.Key, Parse(def.DefaultValue), null, false, null, false));
    }

    /// <summary>The level a value would be stored at for each scope, in broad → specific order.</summary>
    public static IEnumerable<(ConfigScope Scope, string? Ref)> Levels(SettingsContext ctx)
    {
        yield return (ConfigScope.Platform, "*");
        yield return (ConfigScope.Country, ctx.CountryCode);
        yield return (ConfigScope.Plan, ctx.PlanCode);
        yield return (ConfigScope.Firm, ctx.FirmId.ToString());
        yield return (ConfigScope.Client, ctx.ClientId?.ToString());
        yield return (ConfigScope.User, ctx.UserId?.ToString());
    }

    public static string? RefFor(ConfigScope scope, SettingsContext ctx) =>
        Levels(ctx).First(l => l.Scope == scope).Ref;

    private static ResolvedValue Capped(ConfigDefinition def, IReadOnlyCollection<ConfigValue> values, SettingsContext ctx, ResolvedValue r)
    {
        if (!def.PlanValueIsCap || r.Value.ValueKind != JsonValueKind.Number) return r;
        var plan = values.FirstOrDefault(v => v.Key == def.Key && v.Scope == ConfigScope.Plan && v.ScopeRef == ctx.PlanCode);
        if (plan is null) return r;
        var cap = Parse(plan.Value);
        if (cap.ValueKind != JsonValueKind.Number || !cap.TryGetDecimal(out var c) ||
            !r.Value.TryGetDecimal(out var v) || v <= c) return r;
        return r with { Value = cap, Capped = true };
    }

    public static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
        return doc.RootElement.Clone();
    }
}
