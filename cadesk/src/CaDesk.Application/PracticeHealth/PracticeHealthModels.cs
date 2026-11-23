using CaDesk.Domain.PracticeHealth;

namespace CaDesk.Application.PracticeHealth;

public record FirmTelemetryDto(
    decimal AnnualGrossBillings,
    decimal AnnualCollections,
    int ActiveClientsCount,
    int ActivePartnersCount,
    int ActiveStaffCount,
    decimal TotalOutstandingReceivables,
    decimal EstimatedDsoDays,
    decimal EstimatedRealizationRate,
    decimal StaffLeverageRatio
);

public record CalculateValuationRequest(
    decimal AnnualGrossRevenue,
    decimal RecurringComplianceRevenue,
    decimal AdvisoryAndOneOffRevenue,
    decimal Ebitda,
    decimal PartnerNormalizedCompensation,
    decimal NetWorkingCapital,
    decimal NetTangibleAssets,
    decimal ProjectedRevenueGrowthRate,
    decimal DiscountRateWacc,
    decimal TerminalGrowthRate,
    int ForecastYears,
    decimal RevenueMultipleBase,
    decimal EbitdaMultipleBase,
    decimal AuditFranchiseStickinessPremiumPct,
    decimal PartnerConcentrationDiscountPct,
    decimal RecurringRevenuePremiumPct,
    ValuationMethod Method
);

public record SaveValuationRequest(
    string ValuationName,
    DateOnly ValuationDate,
    ValuationMethod Method,
    decimal AnnualGrossRevenue,
    decimal RecurringComplianceRevenue,
    decimal AdvisoryAndOneOffRevenue,
    decimal Ebitda,
    decimal PartnerNormalizedCompensation,
    decimal NetWorkingCapital,
    decimal NetTangibleAssets,
    decimal ProjectedRevenueGrowthRate,
    decimal DiscountRateWacc,
    decimal TerminalGrowthRate,
    int ForecastYears,
    decimal RevenueMultipleBase,
    decimal EbitdaMultipleBase,
    decimal AuditFranchiseStickinessPremiumPct,
    decimal PartnerConcentrationDiscountPct,
    decimal RecurringRevenuePremiumPct,
    string? Notes
);

public record PracticeValuationDto(
    Guid Id,
    string ValuationName,
    DateOnly ValuationDate,
    ValuationMethod Method,
    ValuationStatus Status,
    decimal AnnualGrossRevenue,
    decimal RecurringComplianceRevenue,
    decimal AdvisoryAndOneOffRevenue,
    decimal Ebitda,
    decimal PartnerNormalizedCompensation,
    decimal NetWorkingCapital,
    decimal NetTangibleAssets,
    decimal ProjectedRevenueGrowthRate,
    decimal DiscountRateWacc,
    decimal TerminalGrowthRate,
    int ForecastYears,
    decimal RevenueMultipleBase,
    decimal EbitdaMultipleBase,
    decimal AuditFranchiseStickinessPremiumPct,
    decimal PartnerConcentrationDiscountPct,
    decimal RecurringRevenuePremiumPct,
    decimal DcfEnterpriseValue,
    decimal MultipleEnterpriseValue,
    decimal BlendedFirmFairMarketValue,
    decimal FirmGoodwillValue,
    decimal ValuePerEquityPoint,
    List<PracticeValuationEngine.ProjectedCashFlowItem> ProjectedCashFlows,
    string? PreparedByName,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt,
    string? Notes,
    DateTime CreatedAt
);

public record SavePartnerEquityRequest(
    Guid? Id,
    Guid UserId,
    string PartnerName,
    string PartnerEmail,
    string PartnerMembershipNo,
    decimal EquityPercentage,
    decimal CapitalAccountBalance,
    DateOnly JoiningDate,
    DateOnly? TargetRetirementDate,
    PartnerVestingStatus VestingStatus,
    decimal VestedPercentage,
    decimal AnnualDrawShare,
    PartnerTransitionRole Role
);

public record PartnerEquityDto(
    Guid Id,
    Guid UserId,
    string PartnerName,
    string PartnerEmail,
    string PartnerMembershipNo,
    decimal EquityPercentage,
    decimal CapitalAccountBalance,
    DateOnly JoiningDate,
    DateOnly? TargetRetirementDate,
    PartnerVestingStatus VestingStatus,
    decimal VestedPercentage,
    decimal AnnualDrawShare,
    PartnerTransitionRole Role,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateRetirementPlanRequest(
    Guid PartnerEquityId,
    DateOnly RetirementDate,
    decimal AgreedFirmValuationAtRetirement,
    decimal CapitalAccountRefundAmount,
    int PayoutPeriodYears,
    decimal AnnualInterestRatePct,
    string? PartnershipNotes
);

public record PartnerRetirementPlanDto(
    Guid Id,
    Guid PartnerEquityId,
    string PartnerName,
    DateOnly RetirementDate,
    decimal AgreedFirmValuationAtRetirement,
    decimal PartnerEquityPct,
    decimal GrossEquityValue,
    decimal CapitalAccountRefundAmount,
    decimal GoodwillBuyoutAmount,
    int PayoutPeriodYears,
    decimal AnnualInterestRatePct,
    decimal AnnualInstallmentAmount,
    RetirementPlanStatus Status,
    List<PracticeValuationEngine.AmortizationInstallment> AmortizationSchedule,
    string? PartnershipNotes,
    DateTime CreatedAt
);

public record SaveClientTransitionRequest(
    Guid? Id,
    Guid RetiringPartnerId,
    Guid SuccessorPartnerId,
    Guid ClientId,
    decimal AnnualClientFees,
    TransitionStage Stage,
    ClientRetentionRisk Risk,
    DateOnly TargetHandoverDate,
    string? HandoverNotes
);

public record ClientSuccessionTransitionDto(
    Guid Id,
    Guid RetiringPartnerId,
    string RetiringPartnerName,
    Guid SuccessorPartnerId,
    string SuccessorPartnerName,
    Guid ClientId,
    string ClientName,
    decimal AnnualClientFees,
    TransitionStage Stage,
    ClientRetentionRisk Risk,
    DateOnly TargetHandoverDate,
    string? HandoverNotes,
    DateTime CreatedAt
);

public record GenerateBenchmarkRequest(
    int BenchmarkYear,
    FirmTierBenchmark FirmTier,
    decimal? OverrideActualRevenuePerPartner,
    decimal? OverrideActualRealizationRate,
    decimal? OverrideActualDsoDays,
    decimal? OverrideActualStaffLeverage,
    decimal? OverrideActualEbitdaMargin
);

public record PracticeBenchmarkDto(
    Guid Id,
    int BenchmarkYear,
    FirmTierBenchmark FirmTier,
    decimal ActualRevenuePerPartner,
    decimal ActualRevenuePerEmployee,
    decimal ActualRealizationRate,
    decimal ActualDsoDays,
    decimal ActualStaffLeverageRatio,
    decimal ActualWipLockupDays,
    decimal ActualEbitdaMarginPct,
    decimal BenchmarkRevenuePerPartner,
    decimal BenchmarkRevenuePerEmployee,
    decimal BenchmarkRealizationRate,
    decimal BenchmarkDsoDays,
    decimal BenchmarkStaffLeverageRatio,
    decimal BenchmarkWipLockupDays,
    decimal BenchmarkEbitdaMarginPct,
    decimal OverallHealthScore,
    string HealthGrade,
    List<string> InsightsChecklist,
    DateTime CreatedAt
);
