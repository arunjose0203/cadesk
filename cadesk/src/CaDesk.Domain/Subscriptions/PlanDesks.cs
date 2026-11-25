namespace CaDesk.Domain.Subscriptions;

/// <summary>
/// Which plan each desk needs (Sprint 71). The keys are the desk ids the web app uses (web/src/shell/nav.tsx).
/// A desk that is not listed is in every plan. Demo, development and internal firms get every desk.
/// </summary>
public static class PlanDesks
{
    public static readonly IReadOnlyDictionary<string, string> MinimumTier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Growth: audit practice, reconciliation and annual GST
        ["audit-workspace"] = SubscriptionPlanTier.Growth,
        ["aqmm"] = SubscriptionPlanTier.Growth,
        ["caro"] = SubscriptionPlanTier.Growth,
        ["schedule3"] = SubscriptionPlanTier.Growth,
        ["branchaudit"] = SubscriptionPlanTier.Growth,
        ["auditcommittee"] = SubscriptionPlanTier.Growth,
        ["internalaudit"] = SubscriptionPlanTier.Growth,
        ["gst-reconcile"] = SubscriptionPlanTier.Growth,
        ["gstr9"] = SubscriptionPlanTier.Growth,
        ["einvoicing"] = SubscriptionPlanTier.Growth,
        ["banking"] = SubscriptionPlanTier.Growth,
        ["ledger-sync"] = SubscriptionPlanTier.Growth,
        ["tax-litigation"] = SubscriptionPlanTier.Growth,
        ["relatedparty"] = SubscriptionPlanTier.Growth,
        ["foreignremittance"] = SubscriptionPlanTier.Growth,
        ["practice-health"] = SubscriptionPlanTier.Growth,

        // Enterprise: specialist certifications and cross-border work
        ["transferpricing"] = SubscriptionPlanTier.Enterprise,
        ["costaudit"] = SubscriptionPlanTier.Enterprise,
        ["secretarialaudit"] = SubscriptionPlanTier.Enterprise,
        ["antiprofiteering"] = SubscriptionPlanTier.Enterprise,
        ["esg-csr"] = SubscriptionPlanTier.Enterprise,
        ["ibc-cirp"] = SubscriptionPlanTier.Enterprise,
        ["uae-tax"] = SubscriptionPlanTier.Enterprise,
    };

    /// <summary>Plan codes that unlock everything (Firm.PlanCode).</summary>
    public static readonly string[] Unrestricted = ["DEMO", "DEV", "INTERNAL"];

    public static int Rank(string? tier) => tier?.Trim().ToUpperInvariant() switch
    {
        SubscriptionPlanTier.Growth => 1,
        SubscriptionPlanTier.Enterprise => 2,
        "DEMO" or "DEV" or "INTERNAL" => 3,
        _ => 0,
    };

    public static string RequiredTier(string desk) =>
        MinimumTier.TryGetValue(desk, out var t) ? t : SubscriptionPlanTier.Starter;

    public static bool Allows(string? tier, string desk) => Rank(tier) >= Rank(RequiredTier(desk));

    /// <summary>A plan limit as a number, or null for unlimited (stored as -1).</summary>
    public static int? Limit(int value) => value < 0 ? null : value;
}
