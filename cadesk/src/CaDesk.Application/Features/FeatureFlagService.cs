using CaDesk.Application.Abstractions;
using CaDesk.Domain.Features;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Features;

public sealed class FeatureFlagService(IAppDbContext db, ITenantContext tenant) : IFeatureFlagService
{
    public async Task<bool> IsEnabledAsync(string featureKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) return false;
        var normalizedKey = NormalizeKey(featureKey);

        if (tenant.FirmId == Guid.Empty)
        {
            return !FeatureKeys.DisabledByDefault.Contains(normalizedKey, StringComparer.OrdinalIgnoreCase) &&
                   !FeatureKeys.DisabledByDefault.Contains(featureKey, StringComparer.OrdinalIgnoreCase);
        }

        // 1. Check if there is an explicit firm-level record in firm_features
        var record = await db.FirmFeatures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FirmId == tenant.FirmId && (f.FeatureKey == normalizedKey || f.FeatureKey == featureKey), ct);

        if (record is not null)
        {
            return record.IsEnabled;
        }

        // 2. Demo and internal test firms have all features unlocked for demos
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        if (firm is not null && (firm.PlanCode == "DEMO" || firm.PlanCode == "DEV" || firm.PlanCode == "INTERNAL"))
        {
            return true;
        }

        // 3. Otherwise check if disabled by default
        if (FeatureKeys.DisabledByDefault.Contains(normalizedKey, StringComparer.OrdinalIgnoreCase) ||
            FeatureKeys.DisabledByDefault.Contains(featureKey, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetFirmFeaturesAsync(CancellationToken ct = default)
    {
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        bool isDemo = firm is not null && (firm.PlanCode == "DEMO" || firm.PlanCode == "DEV" || firm.PlanCode == "INTERNAL");

        var explicitOverrides = await db.FirmFeatures.AsNoTracking()
            .Where(f => f.FirmId == tenant.FirmId)
            .ToDictionaryAsync(f => f.FeatureKey, f => f.IsEnabled, ct);

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in FeatureKeys.AllConfigurable)
        {
            if (explicitOverrides.TryGetValue(key, out var enabled))
            {
                result[key] = enabled;
            }
            else if (isDemo)
            {
                result[key] = true;
            }
            else
            {
                result[key] = !FeatureKeys.DisabledByDefault.Contains(key, StringComparer.OrdinalIgnoreCase);
            }
        }

        // Also ensure banking and connected-banking are consistent
        if (result.TryGetValue(FeatureKeys.ConnectedBanking, out var cb))
        {
            result[FeatureKeys.Banking] = cb;
        }

        return result;
    }

    public async Task SetFeatureAsync(string featureKey, bool enabled, CancellationToken ct = default)
    {
        var normalizedKey = NormalizeKey(featureKey);
        var existing = await db.FirmFeatures
            .FirstOrDefaultAsync(f => f.FirmId == tenant.FirmId && (f.FeatureKey == normalizedKey || f.FeatureKey == featureKey), ct);

        if (existing is null)
        {
            db.FirmFeatures.Add(new FirmFeature
            {
                FirmId = tenant.FirmId,
                FeatureKey = normalizedKey,
                IsEnabled = enabled,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.IsEnabled = enabled;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeKey(string key)
    {
        var k = key.Trim().ToLowerInvariant();
        if (k == "banking") return FeatureKeys.ConnectedBanking;
        return k;
    }
}
