using CaDesk.Domain.Subscriptions;

namespace CaDesk.Application.Subscriptions;

public record SubscriptionStatusDto(
    string PlanTier,
    string DisplayName,
    string Status,
    string BillingInterval,
    DateTime TrialStartsAt,
    DateTime TrialEndsAt,
    int DaysRemainingInTrial,
    bool IsTrialActive,
    DateTime CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    int CurrentUsersCount,
    int UserLimit,
    int CurrentClientsCount,
    int ClientLimit,
    int ActiveAuditsCount,
    int ActiveAuditsLimit,
    bool IsDemoDataSeeded,
    IReadOnlyList<string> IncludedFeatures,
    decimal LastPaymentAmount,
    DateTime? LastPaymentDate,
    string? LastPaymentInvoiceNumber
);

public record PlanPricingOptionDto(
    string TierCode,
    string DisplayName,
    string Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int UserLimit,
    int ClientLimit,
    int ActiveAuditsLimit,
    IReadOnlyList<string> IncludedFeatures,
    bool IsPopular
);

public record UpgradePlanRequest(
    string TargetTier,
    string BillingInterval = SubscriptionBillingInterval.Monthly,
    string PaymentMethod = "UPI AutoPay",
    string? FirmGstin = null,
    string? TransactionReference = null
);

public record UpgradePlanResponseDto(
    bool Success,
    string PlanTier,
    string Status,
    string InvoiceNumber,
    decimal TotalAmountPaid,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    string Message
);

public record AdminApplyUpgradeRequest(
    Guid FirmId,
    string TargetTier,
    string InvoiceNumber
);

public record SubscriptionInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string PlanTier,
    string BillingInterval,
    decimal BaseAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TotalAmount,
    string SacCode,
    string PaymentStatus,
    string PaymentMethod,
    string TransactionReference,
    DateTime CreatedAt
);

public record SeedDemoDataResultDto(
    bool Success,
    int ClientsCreated,
    int AuditEngagementsCreated,
    int BankTransactionsCreated,
    int ConfirmationsCreated,
    string Message
);

public record PurgeDemoDataResultDto(
    bool Success,
    int ClientsPurged,
    string Message
);
