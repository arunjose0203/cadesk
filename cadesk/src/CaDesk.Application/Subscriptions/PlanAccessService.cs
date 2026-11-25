using CaDesk.Application.Abstractions;
using CaDesk.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Subscriptions;

public sealed record PlanDesksDto(string PlanTier, string DisplayName, IReadOnlyDictionary<string, string> DeskPlans,
    int? UserLimit, int? ClientLimit, int Users, int Clients);

/// <summary>The firm's plan: which desks it opens and how many users and clients it allows (Sprint 71).</summary>
public sealed class PlanAccessService(IAppDbContext db, ITenantContext tenant, TimeProvider clock)
{
    /// <summary>
    /// The plan in force: DEMO/DEV/INTERNAL firms keep that code; otherwise the higher of the subscription's tier and
    /// the firm's plan code (an operator can set either; the status screen creates a Starter trial row on first visit).
    /// </summary>
    public async Task<string> GetTierAsync(CancellationToken ct = default)
    {
        if (!tenant.HasTenant) return SubscriptionPlanTier.Starter;
        var planCode = await db.Firms.AsNoTracking().Where(f => f.Id == tenant.FirmId).Select(f => f.PlanCode).FirstOrDefaultAsync(ct);
        if (planCode is not null && PlanDesks.Unrestricted.Contains(planCode.ToUpperInvariant())) return planCode.ToUpperInvariant();
        var subscribed = await db.FirmSubscriptions.AsNoTracking().Where(s => s.FirmId == tenant.FirmId).Select(s => s.PlanTier).FirstOrDefaultAsync(ct);
        return new[] { subscribed, planCode }
            .Select(t => t?.ToUpperInvariant())
            .Where(t => t is not null && SubscriptionPlanTier.All.Contains(t))
            .OrderByDescending(PlanDesks.Rank)
            .FirstOrDefault() ?? SubscriptionPlanTier.Starter;
    }

    public async Task<bool> AllowsDeskAsync(string desk, CancellationToken ct = default) =>
        PlanDesks.Allows(await GetTierAsync(ct), desk);

    public async Task<PlanDesksDto> GetDesksAsync(CancellationToken ct = default)
    {
        var tier = await GetTierAsync(ct);
        var unrestricted = PlanDesks.Rank(tier) > PlanDesks.Rank(SubscriptionPlanTier.Enterprise);
        var plan = SubscriptionEngine.GetPlan(unrestricted ? SubscriptionPlanTier.Enterprise : tier);
        return new PlanDesksDto(tier, unrestricted ? $"{tier} (all desks)" : plan.DisplayName,
            PlanDesks.MinimumTier,
            unrestricted ? null : PlanDesks.Limit(plan.UserLimit),
            unrestricted ? null : PlanDesks.Limit(plan.ClientLimit),
            await CountUsersAsync(ct), await CountClientsAsync(ct));
    }

    /// <summary>Refuses when adding <paramref name="adding"/> clients would pass the plan's client limit.</summary>
    public async Task EnsureClientRoomAsync(int adding, CancellationToken ct = default)
    {
        var limit = await LimitAsync(p => p.ClientLimit, ct);
        if (limit is null || adding <= 0) return;
        var have = await CountClientsAsync(ct);
        if (have + adding > limit)
            throw new BusinessRuleException(adding == 1
                ? $"Your plan allows {limit} clients and you have {have}. Upgrade the plan to add more."
                : $"Your plan allows {limit} clients. You have {have}, so {Math.Max(0, limit.Value - have)} more can be added, not {adding}. Upgrade the plan or import fewer.", 403);
    }

    /// <summary>Refuses an invitation when users plus open invitations would pass the plan's user limit.</summary>
    public async Task EnsureUserRoomAsync(CancellationToken ct = default)
    {
        var limit = await LimitAsync(p => p.UserLimit, ct);
        if (limit is null) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var pending = await db.Invitations.CountAsync(i => i.AcceptedAt == null && i.ExpiresAt > now, ct);
        var have = await CountUsersAsync(ct);
        if (have + pending + 1 > limit)
            throw new BusinessRuleException($"Your plan allows {limit} users. You have {have} and {pending} open invitations. Upgrade the plan or cancel an invitation.", 403);
    }

    private async Task<int?> LimitAsync(Func<PlanDefinition, int> pick, CancellationToken ct)
    {
        var tier = await GetTierAsync(ct);
        if (PlanDesks.Rank(tier) > PlanDesks.Rank(SubscriptionPlanTier.Enterprise)) return null;
        return PlanDesks.Limit(pick(SubscriptionEngine.GetPlan(tier)));
    }

    private Task<int> CountUsersAsync(CancellationToken ct) => db.Users.CountAsync(u => u.IsActive && u.DeletedAt == null, ct);
    private Task<int> CountClientsAsync(CancellationToken ct) => db.Clients.CountAsync(c => c.DeletedAt == null, ct);
}
