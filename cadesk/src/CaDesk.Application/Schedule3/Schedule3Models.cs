using System;
using System.Collections.Generic;

namespace CaDesk.Application.Schedule3;

public record Schedule3RatioItemDto(
    Guid Id,
    Guid AssessmentId,
    string RatioCode,
    string RatioName,
    string NumeratorDescription,
    string DenominatorDescription,
    decimal CurrentYearValue,
    decimal PrecedingYearValue,
    decimal VariancePercent,
    bool IsVarianceSignificant,
    string? VarianceExplanation,
    bool IsExplanationProvided
);

public record Schedule3AgeingItemDto(
    Guid Id,
    Guid AssessmentId,
    string ScheduleType,
    string ParticularsCategory,
    decimal Less6MonthsOr1Year,
    decimal OneToTwoYears,
    decimal TwoToThreeYears,
    decimal MoreThanThreeYears,
    decimal UnbilledDues,
    decimal Total
);

public record Schedule3AssessmentDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    Guid? AuditEngagementId,
    string? AuditEngagementTitle,
    string FinancialYear,
    string PrecedingFinancialYear,
    string AccountingStandard,
    string RoundingUnit,

    decimal TotalIncomeCurrent,
    decimal TotalIncomePreceding,
    decimal CurrentAssetsCurrent,
    decimal CurrentAssetsPreceding,
    decimal CurrentLiabilitiesCurrent,
    decimal CurrentLiabilitiesPreceding,
    decimal TotalDebtCurrent,
    decimal TotalDebtPreceding,
    decimal ShareholdersEquityCurrent,
    decimal ShareholdersEquityPreceding,
    decimal NetProfitAfterTaxesCurrent,
    decimal NetProfitAfterTaxesPreceding,
    decimal EbitCurrent,
    decimal EbitPreceding,
    decimal DebtServiceCurrent,
    decimal DebtServicePreceding,
    decimal CostOfGoodsSoldCurrent,
    decimal CostOfGoodsSoldPreceding,
    decimal AverageInventoryCurrent,
    decimal AverageInventoryPreceding,
    decimal RevenueFromOperationsCurrent,
    decimal RevenueFromOperationsPreceding,
    decimal AverageTradeReceivablesCurrent,
    decimal AverageTradeReceivablesPreceding,
    decimal NetCreditPurchasesCurrent,
    decimal NetCreditPurchasesPreceding,
    decimal AverageTradePayablesCurrent,
    decimal AverageTradePayablesPreceding,
    decimal CapitalEmployedCurrent,
    decimal CapitalEmployedPreceding,
    decimal IncomeFromInvestmentsCurrent,
    decimal IncomeFromInvestmentsPreceding,
    decimal AverageInvestmentsCurrent,
    decimal AverageInvestmentsPreceding,

    bool IsRoundingRuleCompliant,
    string? RoundingRuleRecommendation,
    int SignificantVariancesCount,
    int MissingExplanationsCount,
    string AssessmentStatus,
    string? AuditorRemarks,
    string? GeneratedDisclosureNoteText,
    Guid? FinalizedByUserId,
    string? FinalizedByName,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<Schedule3RatioItemDto> Ratios,
    List<Schedule3AgeingItemDto> AgeingItems
);

public class SaveSchedule3AssessmentRequest
{
    public Guid ClientId { get; set; }
    public Guid? AuditEngagementId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";
    public string PrecedingFinancialYear { get; set; } = "2023-24";
    public string AccountingStandard { get; set; } = "Division I - AS (Non-Ind AS)";
    public string RoundingUnit { get; set; } = "Lakhs";

    public decimal TotalIncomeCurrent { get; set; }
    public decimal TotalIncomePreceding { get; set; }
    public decimal CurrentAssetsCurrent { get; set; }
    public decimal CurrentAssetsPreceding { get; set; }
    public decimal CurrentLiabilitiesCurrent { get; set; }
    public decimal CurrentLiabilitiesPreceding { get; set; }
    public decimal TotalDebtCurrent { get; set; }
    public decimal TotalDebtPreceding { get; set; }
    public decimal ShareholdersEquityCurrent { get; set; }
    public decimal ShareholdersEquityPreceding { get; set; }
    public decimal NetProfitAfterTaxesCurrent { get; set; }
    public decimal NetProfitAfterTaxesPreceding { get; set; }
    public decimal EbitCurrent { get; set; }
    public decimal EbitPreceding { get; set; }
    public decimal DebtServiceCurrent { get; set; }
    public decimal DebtServicePreceding { get; set; }
    public decimal CostOfGoodsSoldCurrent { get; set; }
    public decimal CostOfGoodsSoldPreceding { get; set; }
    public decimal AverageInventoryCurrent { get; set; }
    public decimal AverageInventoryPreceding { get; set; }
    public decimal RevenueFromOperationsCurrent { get; set; }
    public decimal RevenueFromOperationsPreceding { get; set; }
    public decimal AverageTradeReceivablesCurrent { get; set; }
    public decimal AverageTradeReceivablesPreceding { get; set; }
    public decimal NetCreditPurchasesCurrent { get; set; }
    public decimal NetCreditPurchasesPreceding { get; set; }
    public decimal AverageTradePayablesCurrent { get; set; }
    public decimal AverageTradePayablesPreceding { get; set; }
    public decimal CapitalEmployedCurrent { get; set; }
    public decimal CapitalEmployedPreceding { get; set; }
    public decimal IncomeFromInvestmentsCurrent { get; set; }
    public decimal IncomeFromInvestmentsPreceding { get; set; }
    public decimal AverageInvestmentsCurrent { get; set; }
    public decimal AverageInvestmentsPreceding { get; set; }
    public string? AuditorRemarks { get; set; }
}

public class UpdateRatioExplanationRequest
{
    public string? VarianceExplanation { get; set; }
}

public class UpdateAgeingItemRequest
{
    public decimal Less6MonthsOr1Year { get; set; }
    public decimal OneToTwoYears { get; set; }
    public decimal TwoToThreeYears { get; set; }
    public decimal MoreThanThreeYears { get; set; }
    public decimal UnbilledDues { get; set; }
}

public record Schedule3SummaryDto(
    int TotalAssessments,
    int InReviewCount,
    int FinalizedCount,
    int TotalSignificantVariances,
    int TotalMissingExplanations,
    int RoundingCompliantCount
);
