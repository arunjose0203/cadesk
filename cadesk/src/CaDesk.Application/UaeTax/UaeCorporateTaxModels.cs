using CaDesk.Domain.UaeTax;

namespace CaDesk.Application.UaeTax;

public record UaeTaxProfileDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string CorporateTaxTrn,
    string Emirate,
    string FreeZoneName,
    UaeTaxpayerCategory Category,
    FreeZoneRegime FreeZoneRegime,
    int FinancialYearStartMonth,
    bool IsMneGroupConstituent,
    string AccountingStandard,
    bool AuditedFinancialsAvailable,
    DateTime CreatedAt
);

public record CreateOrUpdateTaxProfileRequest(
    Guid ClientId,
    string CorporateTaxTrn,
    string Emirate,
    string FreeZoneName,
    UaeTaxpayerCategory Category,
    FreeZoneRegime FreeZoneRegime,
    int FinancialYearStartMonth,
    bool IsMneGroupConstituent,
    string AccountingStandard,
    bool AuditedFinancialsAvailable
);

public record CalculateUaeTaxRequest(
    UaeTaxpayerCategory Category,
    decimal GrossRevenue,
    decimal CostOfSales,
    decimal OperatingExpenses,
    decimal NetAccountingProfitLoss,
    SmallBusinessReliefStatus SmallBusinessReliefElection,
    decimal PriorYear1Revenue,
    decimal PriorYear2Revenue,
    bool IsMneConstituent,
    decimal QualifyingRevenue,
    decimal NonQualifyingRevenue,
    decimal ExemptIncome,
    decimal UnrealizedGainsLossesAdjustment,
    decimal EntertainmentExpenditureTotal,
    decimal FinesAndPenaltiesDisallowed,
    decimal RecoverableVatDisallowed,
    decimal DonationsToUnapprovedEntitiesDisallowed,
    decimal NetInterestExpense,
    decimal AdjustedEbitda,
    decimal BroughtForwardTaxLosses,
    decimal ForeignTaxCreditReliefAed,
    decimal ExchangeRateToInr
);

public record SaveUaeCorporateTaxReturnRequest(
    Guid TaxProfileId,
    int ReturnYear,
    DateOnly TaxPeriodStart,
    DateOnly TaxPeriodEnd,
    decimal GrossRevenue,
    decimal CostOfSales,
    decimal OperatingExpenses,
    decimal NetAccountingProfitLoss,
    SmallBusinessReliefStatus SmallBusinessReliefElection,
    decimal PriorYear1Revenue,
    decimal PriorYear2Revenue,
    decimal QualifyingRevenue,
    decimal NonQualifyingRevenue,
    decimal ExemptIncome,
    decimal UnrealizedGainsLossesAdjustment,
    decimal EntertainmentExpenditureTotal,
    decimal FinesAndPenaltiesDisallowed,
    decimal RecoverableVatDisallowed,
    decimal DonationsToUnapprovedEntitiesDisallowed,
    decimal NetInterestExpense,
    decimal AdjustedEbitda,
    decimal BroughtForwardTaxLosses,
    decimal ForeignTaxCreditReliefAed,
    decimal ExchangeRateToInr
);

public record UaeCorporateTaxReturnDto(
    Guid Id,
    Guid TaxProfileId,
    Guid ClientId,
    string ClientName,
    string CorporateTaxTrn,
    string ReturnReference,
    int ReturnYear,
    DateOnly TaxPeriodStart,
    DateOnly TaxPeriodEnd,
    DateOnly DueDate,
    ReturnFilingStatus Status,
    string FunctionalCurrency,
    decimal ExchangeRateToInr,

    // P&L
    decimal GrossRevenue,
    decimal CostOfSales,
    decimal GrossProfit,
    decimal OperatingExpenses,
    decimal NetAccountingProfitLoss,

    // SBR
    SmallBusinessReliefStatus SmallBusinessReliefElection,
    bool SbrEligible,
    decimal PriorYear1Revenue,
    decimal PriorYear2Revenue,

    // QFZP
    bool IsQfzp,
    decimal QualifyingRevenue,
    decimal NonQualifyingRevenue,
    decimal DeMinimisThreshold,
    bool DeMinimisSatisfied,
    decimal QualifyingIncome,
    decimal TaxableNonQualifyingIncome,

    // Adjustments
    decimal ExemptIncome,
    decimal UnrealizedGainsLossesAdjustment,
    decimal EntertainmentExpenditureTotal,
    decimal EntertainmentExpenditureDisallowed,
    decimal FinesAndPenaltiesDisallowed,
    decimal RecoverableVatDisallowed,
    decimal DonationsToUnapprovedEntitiesDisallowed,

    // Interest Cap (Art 30)
    decimal NetInterestExpense,
    decimal AdjustedEbitda,
    decimal InterestCapThreshold,
    decimal DisallowedInterestExpense,

    // Tax Losses (Art 37)
    decimal BroughtForwardTaxLosses,
    decimal TaxLossesUtilized,
    decimal CarriedForwardTaxLosses,

    // Final Tax
    decimal AdjustedTaxableIncome,
    decimal TaxableIncomeSubjectToZeroPct,
    decimal TaxableIncomeSubjectToNinePct,
    decimal CorporateTaxPayableAed,
    decimal ForeignTaxCreditReliefAed,
    decimal NetTaxPayableAed,
    decimal NetTaxPayableInr,

    // Sign-off
    string? FtaSubmissionReference,
    string? PreparedByName,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt,
    string? PartnerNotes,
    DateTime CreatedAt
);

public record ApproveUaeReturnRequest(
    string? PartnerNotes
);

public record EmaraTaxSubmissionExportDto(
    string ReturnReference,
    string CorporateTaxTrn,
    string LegalEntityName,
    string FormVersion,
    string SubmissionXml,
    string SubmissionJson,
    decimal TotalTaxPayableAed,
    decimal TotalTaxPayableInr
);
