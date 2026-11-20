using System;
using System.Collections.Generic;

namespace CaDesk.Application.AuditCommittee;

public record InternalControlDeficiencyDto(
    Guid Id,
    Guid CommitteeEngagementId,
    string DeficiencyCode,
    string Title,
    string ProcessArea,
    string DeficiencyType,
    string Severity,
    string Description,
    string PotentialFinancialEffect,
    decimal? EstimatedRiskAmount,
    string AuditorRecommendation,
    string? ManagementResponse,
    DateTime? RemediationTargetDate,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record SignificantAuditMatterDto(
    Guid Id,
    Guid CommitteeEngagementId,
    string MatterCode,
    string Subject,
    string Category,
    string Details,
    string AuditorAssessment,
    string? AuditCommitteeFeedback,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UncorrectedMisstatementDto(
    Guid Id,
    Guid CommitteeEngagementId,
    string AccountDescription,
    decimal MisstatementAmount,
    string Nature,
    bool IsMaterial,
    string? ManagementJustificationForNonAdjustment,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record AuditCommitteeEngagementDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string? ClientPan,
    Guid? AuditEngagementId,
    string? AuditEngagementTitle,
    string FinancialYear,
    string Title,
    string MeetingType,
    DateTime? MeetingDate,
    string? MeetingLocation,
    string CommitteeChairName,
    string LeadPartnerName,
    string Status,
    string? MeetingMinutes,
    string? GeneratedGovernancePackText,
    string? GeneratedSa265LetterText,
    Guid? SignedOffByUserId,
    string? SignedOffByUserName,
    DateTime? SignedOffAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int TotalDeficienciesCount,
    int SignificantDeficienciesCount,
    decimal TotalEstimatedRiskAmount,
    int SignificantMattersCount,
    int UncorrectedMisstatementsCount,
    decimal TotalUncorrectedMisstatementAmount,
    List<InternalControlDeficiencyDto> Deficiencies,
    List<SignificantAuditMatterDto> SignificantMatters,
    List<UncorrectedMisstatementDto> UncorrectedMisstatements
);

public record SaveCommitteeEngagementRequest(
    Guid ClientId,
    Guid? AuditEngagementId,
    string FinancialYear,
    string Title,
    string MeetingType,
    DateTime? MeetingDate,
    string? MeetingLocation,
    string CommitteeChairName,
    string LeadPartnerName,
    string? MeetingMinutes
);

public record SaveDeficiencyRequest(
    string DeficiencyCode,
    string Title,
    string ProcessArea,
    string DeficiencyType,
    string Severity,
    string Description,
    string PotentialFinancialEffect,
    decimal? EstimatedRiskAmount,
    string AuditorRecommendation,
    string? ManagementResponse,
    DateTime? RemediationTargetDate,
    string Status
);

public record SaveSignificantMatterRequest(
    string MatterCode,
    string Subject,
    string Category,
    string Details,
    string AuditorAssessment,
    string? AuditCommitteeFeedback
);

public record SaveUncorrectedMisstatementRequest(
    string AccountDescription,
    decimal MisstatementAmount,
    string Nature,
    bool IsMaterial,
    string? ManagementJustificationForNonAdjustment
);

public record SignOffCommitteeEngagementRequest(
    string? Minutes
);

public record AuditCommitteeSummaryDto(
    int TotalEngagements,
    int PackGeneratedCount,
    int SignedOffCount,
    int TotalDeficiencies,
    int TotalSignificantDeficiencies,
    decimal TotalEstimatedRiskAmount,
    int TotalSignificantMatters,
    int TotalUncorrectedMisstatements,
    decimal TotalUncorrectedMisstatementValue
);
