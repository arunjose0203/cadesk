using System;
using System.Collections.Generic;

namespace CaDesk.Application.Caro;

public record CaroClauseResponseDto(
    Guid Id,
    Guid AssessmentId,
    string ClauseNumber,
    string ClauseTitle,
    string Category,
    string Status,
    string AuditorRemark,
    bool HasDiscrepancy,
    decimal? DiscrepancyAmount,
    string? SupportingWpsRef,
    Guid? VerifiedByUserId,
    string? VerifiedByName,
    DateTime? VerifiedAt,
    DateTime? UpdatedAt
);

public record CaroAssessmentDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    Guid? EngagementId,
    string? EngagementTitle,
    string FinancialYear,
    string CompanyType,
    bool IsHoldingOrSubsidiaryOfPublic,
    decimal PaidUpCapitalAndReserves,
    decimal TotalBorrowings,
    decimal TotalRevenue,
    bool IsCaroApplicable,
    string? ExemptionReason,
    string Status,
    int QualificationsCount,
    int AdverseRemarksCount,
    string? GeneratedAnnexureText,
    string? AuditorConclusion,
    Guid? FinalizedByUserId,
    string? FinalizedByName,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<CaroClauseResponseDto> ClauseResponses
);

public class SaveCaroAssessmentRequest
{
    public Guid ClientId { get; set; }
    public Guid? EngagementId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";
    public string CompanyType { get; set; } = "Private";
    public bool IsHoldingOrSubsidiaryOfPublic { get; set; }
    public decimal PaidUpCapitalAndReserves { get; set; }
    public decimal TotalBorrowings { get; set; }
    public decimal TotalRevenue { get; set; }
    public string? AuditorConclusion { get; set; }
}

public class UpdateClauseResponseRequest
{
    public string Status { get; set; } = "Compliant"; // Compliant, Qualified, Adverse, NotApplicable
    public string AuditorRemark { get; set; } = "";
    public bool HasDiscrepancy { get; set; }
    public decimal? DiscrepancyAmount { get; set; }
    public string? SupportingWpsRef { get; set; }
}

public record CaroSummaryDto(
    int TotalAssessments,
    int ApplicableCount,
    int ExemptCount,
    int FinalizedCount,
    int TotalQualifications,
    int TotalAdverseRemarks
);

public record CaroClauseTemplateDto(
    string ClauseNumber,
    string Title,
    string Category,
    string StandardRemark
);
