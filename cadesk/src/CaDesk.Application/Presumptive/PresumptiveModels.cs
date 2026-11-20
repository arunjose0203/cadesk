using System;
using System.Collections.Generic;

namespace CaDesk.Application.Presumptive;

public record Presumptive44AeVehicleDto(
    Guid Id,
    Guid ComputationId,
    string RegistrationNumber,
    string VehicleType,
    decimal GrossVehicleWeightTons,
    int MonthsOwned,
    decimal RatePerMonth,
    decimal DeemedMonthlyProfit,
    decimal DeemedAnnualProfit,
    decimal DeclaredProfit,
    DateTime CreatedAt
);

public record PresumptiveTaxComputationDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string? ClientPan,
    string FinancialYear,
    string AssessmentYear,
    string Section,
    string EntityType,
    string BusinessNatureCode,
    string TradeName,
    decimal GrossTurnover,
    decimal DigitalTurnover,
    decimal CashTurnover,
    decimal DigitalPercentage,
    bool IsDigitalThresholdEligible,
    decimal TurnoverLimit,
    bool IsTurnoverWithinLimit,
    decimal DeemedProfitDigitalRate,
    decimal DeemedProfitCashRate,
    decimal MinimumDeemedProfit,
    decimal DeclaredProfit,
    bool OptedOutPriorYear,
    string? OptOutAssessmentYear,
    bool IsLockoutActive,
    string? LockoutExpiryAy,
    bool IsAuditMandatory,
    string? AuditTriggerReason,
    decimal SundryDebtors,
    decimal SundryCreditors,
    decimal StockInTrade,
    decimal CashBalance,
    decimal BankBalance,
    decimal FixedAssets,
    decimal OtherAssets,
    decimal PartnerOrProprietorCapital,
    decimal LoansAndBorrowings,
    string Status,
    string? Notes,
    Guid? CreatedByUserId,
    string? CreatedByName,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<Presumptive44AeVehicleDto> Vehicles
);

public class SavePresumptiveComputationRequest
{
    public Guid ClientId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";
    public string Section { get; set; } = "44AD";
    public string? EntityType { get; set; }
    public string? BusinessNatureCode { get; set; }
    public string? TradeName { get; set; }
    public decimal GrossTurnover { get; set; }
    public decimal DigitalTurnover { get; set; }
    public decimal CashTurnover { get; set; }
    public decimal DeclaredProfit { get; set; }
    public bool OptedOutPriorYear { get; set; }
    public string? OptOutAssessmentYear { get; set; }
    public decimal SundryDebtors { get; set; }
    public decimal SundryCreditors { get; set; }
    public decimal StockInTrade { get; set; }
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal FixedAssets { get; set; }
    public decimal OtherAssets { get; set; }
    public decimal PartnerOrProprietorCapital { get; set; }
    public decimal LoansAndBorrowings { get; set; }
    public string? Notes { get; set; }
}

public class SaveVehicleRequest
{
    public string RegistrationNumber { get; set; } = "";
    public string VehicleType { get; set; } = "other"; // heavy, other
    public decimal GrossVehicleWeightTons { get; set; }
    public int MonthsOwned { get; set; } = 12;
    public decimal DeclaredProfit { get; set; }
}

public record MinimalFinancialsStatementDto(
    Guid ComputationId,
    string ClientName,
    string? ClientPan,
    string FinancialYear,
    string AssessmentYear,
    string Section,
    string TradeName,
    decimal GrossTurnover,
    decimal DeclaredProfit,
    decimal TotalAssets,
    decimal TotalLiabilitiesAndCapital,
    decimal Difference,
    bool IsBalanced,
    List<string> ValidationAlerts,
    decimal SundryDebtors,
    decimal SundryCreditors,
    decimal StockInTrade,
    decimal CashBalance,
    decimal BankBalance,
    decimal FixedAssets,
    decimal OtherAssets,
    decimal PartnerOrProprietorCapital,
    decimal LoansAndBorrowings
);

public record PresumptiveSummaryDto(
    int TotalComputations,
    int Sec44AdCount,
    int Sec44AdaCount,
    int Sec44AeCount,
    int AuditMandatoryCount,
    int LockoutActiveCount,
    decimal TotalPresumptiveTurnover,
    decimal TotalDeclaredProfit
);
