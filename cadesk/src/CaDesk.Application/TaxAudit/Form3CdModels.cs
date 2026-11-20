using System;
using System.Collections.Generic;

namespace CaDesk.Application.TaxAudit;

public record Form3CdClauseResponseDto(
    Guid Id,
    Guid AssessmentId,
    string ClauseNumber,
    string ClauseTitle,
    string Category,
    string Status,
    decimal ReportedAmount,
    string AuditorRemarks,
    bool HasDiscrepancy,
    string? ParticularsJson,
    string? WorkpaperRef,
    Guid? VerifiedByUserId,
    string? VerifiedByName,
    DateTime? VerifiedAt,
    DateTime? UpdatedAt
);

public record Form3CdAssessmentDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    Guid? AuditEngagementId,
    string? AuditEngagementTitle,
    string FinancialYear,
    string AssessmentYear,
    string AuditType,
    string Section44AbClause,
    decimal TotalTurnoverOrGrossReceipts,
    decimal TotalCashReceipts,
    decimal TotalPayments,
    decimal TotalCashPayments,
    decimal CashReceiptPercentage,
    decimal CashPaymentPercentage,
    bool IsTaxAuditMandatory,
    string? ApplicabilityReason,
    string AssesseeName,
    string Pan,
    string Address,
    string? Gstin,
    string Status,
    string MethodOfAccounting,
    string OptedSpecialTaxRegime,
    decimal TotalDisallowanceSec40a,
    decimal TotalDisallowanceSec40A3,
    decimal TotalDisallowanceSec43B,
    decimal TotalDisallowanceSec43Bh,
    decimal TotalDisallowanceSec361va,
    decimal TotalSec269Violations,
    decimal TotalIcdsNetAdjustment,
    decimal TotalNetIncomeAdjustment,
    string AssessmentStatus,
    int QualificationsCount,
    int DiscrepanciesCount,
    string? AuditorObservations,
    string? GeneratedReportText,
    Guid? FinalizedByUserId,
    string? FinalizedByName,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<Form3CdClauseResponseDto> ClauseResponses
);

public class SaveForm3CdAssessmentRequest
{
    public Guid ClientId { get; set; }
    public Guid? AuditEngagementId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";
    public string AssessmentYear { get; set; } = "2025-26";
    public string AuditType { get; set; } = "Form3CA"; // Form3CA, Form3CB
    public string? Section44AbClause { get; set; }
    public decimal TotalTurnoverOrGrossReceipts { get; set; }
    public decimal TotalCashReceipts { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal TotalCashPayments { get; set; }
    public string AssesseeName { get; set; } = "";
    public string Pan { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Gstin { get; set; }
    public string Status { get; set; } = "Company";
    public string MethodOfAccounting { get; set; } = "Mercantile";
    public string OptedSpecialTaxRegime { get; set; } = "None";
    public string? AuditorObservations { get; set; }
    public bool DeclaresLowerPresumptiveProfit { get; set; }
    public bool Is44AdLockout { get; set; }
    public bool IsAuditedUnderOtherLaw { get; set; } = true;
    public bool IsBusiness { get; set; } = true;
}

public class UpdateForm3CdClauseRequest
{
    public string Status { get; set; } = "Clean"; // Clean, Disallowed, Qualified, NotApplicable
    public decimal ReportedAmount { get; set; }
    public string AuditorRemarks { get; set; } = "";
    public bool HasDiscrepancy { get; set; }
    public string? ParticularsJson { get; set; }
    public string? WorkpaperRef { get; set; }
}

public class EvaluateApplicabilityRequest
{
    public string EntityType { get; set; } = "Company";
    public bool IsBusiness { get; set; } = true;
    public decimal TurnoverOrGrossReceipts { get; set; }
    public decimal CashReceipts { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal CashPayments { get; set; }
    public bool IsAuditedUnderOtherLaw { get; set; } = true;
    public bool DeclaresLowerPresumptiveProfit { get; set; }
    public bool Is44AdLockout { get; set; }
}

public record Form3CdSummaryDto(
    int TotalAssessments,
    int MandatoryCount,
    int ExemptCount,
    int FinalizedCount,
    decimal TotalNetDisallowances,
    decimal Total43BhMsmeDisallowances,
    decimal Total269Violations
);
