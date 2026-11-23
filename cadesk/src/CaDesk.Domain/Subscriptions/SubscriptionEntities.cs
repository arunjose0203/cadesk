using System.Text.Json;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.Subscriptions;

public static class SubscriptionPlanTier
{
    public const string Starter = "STARTER";
    public const string Growth = "GROWTH";
    public const string Enterprise = "ENTERPRISE";

    public static readonly string[] All = [Starter, Growth, Enterprise];
}

public static class SubscriptionBillingInterval
{
    public const string Monthly = "Monthly";
    public const string Annual = "Annual";
}

public static class SubscriptionStatus
{
    public const string Trialing = "Trialing";
    public const string Active = "Active";
    public const string PastDue = "PastDue";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}

/// <summary>
/// SaaS subscription for the CA firm itself.
/// </summary>
public class FirmSubscription : TenantEntity
{
    public string PlanTier { get; set; } = SubscriptionPlanTier.Starter;
    public string Status { get; set; } = SubscriptionStatus.Trialing;
    public string BillingInterval { get; set; } = SubscriptionBillingInterval.Monthly;

    public DateTime TrialStartsAt { get; set; } = DateTime.UtcNow;
    public DateTime TrialEndsAt { get; set; } = DateTime.UtcNow.AddDays(14);

    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddDays(14);

    public bool CancelAtPeriodEnd { get; set; } = false;

    public string? RazorpaySubscriptionId { get; set; }
    public string? RazorpayCustomerId { get; set; }

    public decimal LastPaymentAmount { get; set; } = 0m;
    public DateTime? LastPaymentDate { get; set; }
    public string? LastPaymentInvoiceNumber { get; set; }

    public bool IsDemoDataSeeded { get; set; } = false;
}

/// <summary>
/// Tax invoice generated for CaDesk platform subscription fee (SAC 998311).
/// </summary>
public class SubscriptionInvoice : TenantEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public string PlanTier { get; set; } = SubscriptionPlanTier.Starter;
    public string BillingInterval { get; set; } = SubscriptionBillingInterval.Monthly;

    public decimal BaseAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "INR";
    public string SacCode { get; set; } = "998311"; // IT & Software SaaS
    public string PaymentStatus { get; set; } = "Paid";
    public string PaymentMethod { get; set; } = "UPI AutoPay"; // UPI, NetBanking, CreditCard, PO
    public string TransactionReference { get; set; } = string.Empty;

    public string? FirmGstin { get; set; }
}

public record PlanDefinition(
    string TierCode,
    string DisplayName,
    string Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int UserLimit,
    int ClientLimit,
    int ActiveAuditsLimit,
    IReadOnlyList<string> IncludedFeatures,
    bool IsPopular = false
);

public static class SubscriptionEngine
{
    public static readonly PlanDefinition StarterPlan = new(
        TierCode: SubscriptionPlanTier.Starter,
        DisplayName: "Starter (Solo CA)",
        Description: "Designed for sole practitioners, independent CAs, and boutique firms up to 3 staff.",
        MonthlyPrice: 1_499m,
        AnnualPrice: 14_999m, // 20% discount
        UserLimit: 3,
        ClientLimit: 100,
        ActiveAuditsLimit: 2,
        IncludedFeatures:
        [
            "Practice Management & Timesheets",
            "Universal Trial Balance Importer (Tally, Busy, SAP, Excel)",
            "Direct Tax Computation & 115BAC Regime Comparison",
            "TDS & Form 26Q Withholding Desk",
            "Form 3CD Tax Audit Statement (Clauses 1–44)",
            "Client Self-Service Portal",
            "Document Vault & Two-Lock Security"
        ],
        IsPopular: false
    );

    public static readonly PlanDefinition GrowthPlan = new(
        TierCode: SubscriptionPlanTier.Growth,
        DisplayName: "Growth (Mid Firm)",
        Description: "The complete statutory operating system for 3–6 partner firms and growing audit teams.",
        MonthlyPrice: 4_999m,
        AnnualPrice: 49_999m, // 20% discount
        UserLimit: 15,
        ClientLimit: 500,
        ActiveAuditsLimit: -1, // Unlimited
        IncludedFeatures:
        [
            "All Starter Features Included",
            "CARO 2020 Automated Audit Checklist (21 Clauses)",
            "Bank Statement Analyzer & Forensic Fraud Engine (Sec 40A(3), 269ST)",
            "SA 505 Automated External Confirmations Desk & Public Portal",
            "1-Click ICAI Independent Auditor's Report (SA 700/701/705)",
            "Schedule III Notes to Accounts & 11 Mandatory Ratios",
            "Multi-Firm Branch Audit Consolidation (Sec 143(8) / SA 600)",
            "GST 2A/2B Auto-Reconciliation & GSTR-9/9C"
        ],
        IsPopular: true
    );

    public static readonly PlanDefinition EnterprisePlan = new(
        TierCode: SubscriptionPlanTier.Enterprise,
        DisplayName: "Enterprise (Corporate & Large Practice)",
        Description: "Unlimited power for multi-branch firms, corporate tax teams, and cross-border specialists.",
        MonthlyPrice: 14_999m,
        AnnualPrice: 149_999m, // 20% discount
        UserLimit: -1, // Unlimited
        ClientLimit: -1, // Unlimited
        ActiveAuditsLimit: -1, // Unlimited
        IncludedFeatures:
        [
            "All Growth Features Included",
            "Transfer Pricing Study & Form 3CEB Certification Desk (Rule 10CA)",
            "Cost Audit & Form CRA-1 to CRA-4 Desk (Sec 148 / Rules 2014)",
            "1-Click SA 230 Digital Audit Dossier & NFRA Defense Vault (SHA-256)",
            "Secretarial Audit & Form MR-3 Compliance Engine",
            "Anti-Profiteering & Rate Notification Engine (Sec 171)",
            "Custom Domain (CNAME Whitelabel) & Firm Branding",
            "Priority 24/7 CA Technical Concierge Support"
        ],
        IsPopular: false
    );

    public static IReadOnlyList<PlanDefinition> GetAllPlans() =>
    [
        StarterPlan,
        GrowthPlan,
        EnterprisePlan
    ];

    public static PlanDefinition GetPlan(string tierCode)
    {
        return tierCode.ToUpperInvariant() switch
        {
            SubscriptionPlanTier.Growth => GrowthPlan,
            SubscriptionPlanTier.Enterprise => EnterprisePlan,
            _ => StarterPlan
        };
    }

    public static (decimal BasePrice, decimal Cgst, decimal Sgst, decimal Igst, decimal Total) CalculateGstBreakdown(
        decimal basePrice,
        bool isInterState = false)
    {
        // 18% GST rate on software SaaS under SAC 998311
        if (isInterState)
        {
            var igst = Math.Round(basePrice * 0.18m, 2);
            return (basePrice, 0m, 0m, igst, basePrice + igst);
        }
        else
        {
            var cgst = Math.Round(basePrice * 0.09m, 2);
            var sgst = Math.Round(basePrice * 0.09m, 2);
            return (basePrice, cgst, sgst, 0m, basePrice + cgst + sgst);
        }
    }

    public static int GetTrialDaysRemaining(DateTime trialEndsAt)
    {
        var diff = (trialEndsAt - DateTime.UtcNow).TotalDays;
        return diff < 0 ? 0 : (int)Math.Ceiling(diff);
    }
}
