using CaDesk.Domain.AdvanceTax;

namespace CaDesk.Application.AdvanceTax;

public record AdvanceTaxPlanDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string FinancialYear,
    string AssessmentYear,
    AdvanceTaxRegime Regime,
    bool IsPresumptive,
    bool IsSeniorCitizen,
    decimal EstimatedGrossIncome,
    decimal EstimatedDeductions,
    decimal EstimatedTaxableIncome,
    decimal EstimatedTaxBeforeCredits,
    decimal EstimatedTdsTcs,
    decimal OtherReliefs,
    decimal AssessedTax,
    AdvanceTaxPlanStatus Status,
    string? Notes,
    decimal TotalAdvanceTaxPaid,
    decimal TotalInterest234c,
    DateTime CreatedAt
);

public record AdvanceTaxInstallmentDto(
    Guid Id,
    Guid PlanId,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal TargetPercentage,
    decimal CumulativeTargetAmount,
    decimal AmountPaid,
    decimal ShortfallAmount,
    decimal Interest234c,
    string? ChallanBsrCode,
    string? ChallanNumber,
    DateOnly? ChallanDate,
    bool IsPaid,
    DateTime CreatedAt
);

public record TaxInterestComputationDto(
    Guid Id,
    Guid PlanId,
    string AssessmentYear,
    decimal AssessedTax,
    decimal AdvanceTaxPaid,
    decimal TdsTcsCredit,
    decimal SelfAssessmentTaxPaid,
    DateOnly ReturnDueDate,
    DateOnly ActualReturnFilingDate,
    int MonthsDelayed234a,
    decimal Interest234a,
    bool Is234bApplicable,
    int MonthsDelayed234b,
    decimal Interest234b,
    decimal Interest234cTotal,
    decimal TotalStatutoryInterest,
    decimal NetTaxPayable,
    DateTime ComputedAt
);

public record AdvanceTaxSummaryDto(
    int TotalPlans,
    int ActivePlans,
    decimal TotalEstimatedIncome,
    decimal TotalAssessedTax,
    decimal TotalAdvanceTaxCollected,
    decimal TotalShortfall,
    decimal TotalInterest234c,
    decimal TotalInterest234b,
    decimal TotalInterest234a
);

public record SaveAdvanceTaxPlanRequest(
    Guid ClientId,
    string FinancialYear,
    string AssessmentYear,
    AdvanceTaxRegime Regime,
    bool IsPresumptive,
    bool IsSeniorCitizen,
    decimal EstimatedGrossIncome,
    decimal EstimatedDeductions,
    decimal EstimatedTaxBeforeCredits,
    decimal EstimatedTdsTcs,
    decimal OtherReliefs,
    string? Notes
);

public record RecordInstallmentPaymentRequest(
    decimal AmountPaid,
    string? ChallanBsrCode,
    string? ChallanNumber,
    DateOnly? ChallanDate
);

public record CalculateStatutoryInterestRequest(
    DateOnly ReturnDueDate,
    DateOnly ActualReturnFilingDate,
    decimal SelfAssessmentTaxPaid
);
