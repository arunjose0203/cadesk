using System;
using System.Collections.Generic;

namespace CaDesk.Application.SecretarialAudit;

public record SecretarialAuditSummaryDto(
    int TotalEngagements,
    int ActiveEngagements,
    int CertifiedEngagements,
    int MandatoryApplicableCount,
    int TotalQualificationsLogged,
    int DelayedFilingsCount,
    int MeetingIrregularitiesCount,
    decimal AverageComplianceScore);

public record SecretarialAuditEngagementDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string EngagementReference,
    string Title,
    string FinancialYear,
    DateTime AuditPeriodStart,
    DateTime AuditPeriodEnd,
    string Cin,
    string CompanyType,
    decimal PaidUpShareCapital,
    decimal Turnover,
    decimal OutstandingBorrowings,
    bool IsApplicable,
    string ApplicabilityReason,
    string AuditStatus,
    string AuditorName,
    string AuditorFirmName,
    string MembershipType,
    string MembershipNumber,
    string CertificateOfPracticeNumber,
    string PeerReviewCertificateNumber,
    string? Udin,
    DateTime? CertifiedAt,
    string? CertifiedBy,
    int TotalChecklistCount,
    int CompliedCount,
    int NotCompliedCount,
    int PartiallyCompliedCount,
    int NotApplicableCount,
    int QualificationsCount,
    decimal OverallComplianceScore,
    int TotalFilingsAudited,
    int FilingsDelayedCount,
    int TotalMeetingsAudited,
    int MeetingDeficienciesCount,
    string? FormMR3ReportText,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record SecretarialChecklistItemDto(
    Guid Id,
    Guid EngagementId,
    string StatuteCategory,
    string ClauseCode,
    string SubjectArea,
    string StatutoryRequirement,
    string ComplianceStatus,
    string AuditorRemarks,
    bool IsQualification,
    string DeviationRisk,
    DateTime? TargetCompletionDate,
    string? ActionOwner,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record McaFormFilingAuditDto(
    Guid Id,
    Guid EngagementId,
    string FormName,
    string Description,
    DateTime EventDate,
    int StatutoryDaysAllowed,
    DateTime StatutoryDueDate,
    DateTime? ActualFilingDate,
    string Srn,
    string FilingStatus,
    int DelayDays,
    decimal AdditionalFeePaid,
    string AuditorRemarks,
    DateTime CreatedAt);

public record BoardMeetingAuditRecordDto(
    Guid Id,
    Guid EngagementId,
    string MeetingType,
    DateTime MeetingDate,
    DateTime? PreviousMeetingDate,
    int? GapFromPreviousMeetingDays,
    bool IsGapCompliant,
    DateTime? NoticeDispatchDate,
    int? NoticeDaysGiven,
    bool IsNoticeCompliant,
    int TotalMembersCount,
    int AttendedMembersCount,
    int QuorumRequiredCount,
    bool IsQuorumCompliant,
    int IndependentDirectorsAttended,
    bool SecretarialStandardComplied,
    string AuditorRemarks,
    DateTime CreatedAt);

public record SaveEngagementRequest(
    Guid ClientId,
    string EngagementReference,
    string Title,
    string FinancialYear,
    DateTime AuditPeriodStart,
    DateTime AuditPeriodEnd,
    string Cin,
    string CompanyType,
    decimal PaidUpShareCapital,
    decimal Turnover,
    decimal OutstandingBorrowings,
    string AuditorName,
    string AuditorFirmName,
    string MembershipType,
    string MembershipNumber,
    string CertificateOfPracticeNumber,
    string PeerReviewCertificateNumber);

public record CertifyEngagementRequest(
    string Udin,
    string CertifiedBy);

public record SaveChecklistItemRequest(
    string StatuteCategory,
    string ClauseCode,
    string SubjectArea,
    string StatutoryRequirement,
    string ComplianceStatus,
    string AuditorRemarks,
    bool IsQualification,
    string DeviationRisk,
    DateTime? TargetCompletionDate,
    string? ActionOwner);

public record BatchUpdateChecklistRequest(
    List<BatchChecklistUpdateItem> Items);

public record BatchChecklistUpdateItem(
    Guid Id,
    string ComplianceStatus,
    string AuditorRemarks,
    bool IsQualification,
    string DeviationRisk);

public record SaveMcaFilingRequest(
    string FormName,
    string Description,
    DateTime EventDate,
    int StatutoryDaysAllowed,
    DateTime? ActualFilingDate,
    string Srn,
    decimal AdditionalFeePaid,
    string AuditorRemarks);

public record SaveBoardMeetingAuditRequest(
    string MeetingType,
    DateTime MeetingDate,
    DateTime? PreviousMeetingDate,
    DateTime? NoticeDispatchDate,
    int TotalMembersCount,
    int AttendedMembersCount,
    int IndependentDirectorsAttended,
    bool SecretarialStandardComplied,
    string AuditorRemarks);

public record ApplicabilityCheckRequest(
    string CompanyType,
    decimal PaidUpShareCapital,
    decimal Turnover,
    decimal OutstandingBorrowings);

public record ApplicabilityCheckResult(
    bool IsApplicable,
    string Reason,
    List<string> ThresholdsExceeded);
