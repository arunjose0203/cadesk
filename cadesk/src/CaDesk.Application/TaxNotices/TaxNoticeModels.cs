using CaDesk.Domain.TaxNotices;

namespace CaDesk.Application.TaxNotices;

public record TaxNoticeDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    TaxAuthority Authority,
    NoticeCategory Category,
    string Section,
    string NoticeNumber,
    string? Din,
    DinStatus DinStatus,
    string? AssessmentYear,
    string FinancialYear,
    DateOnly IssueDate,
    DateOnly DueDate,
    int DaysRemaining,
    decimal DemandAmount,
    decimal RefundDisallowed,
    NoticeStatus Status,
    string Subject,
    string? Description,
    Guid? AssignedToUserId,
    string? AssignedToName,
    string? OfficerName,
    string? OfficerJurisdiction,
    int HearingCount,
    int ResponseCount,
    DateTime CreatedAt
);

public record NoticeHearingDto(
    Guid Id,
    Guid NoticeId,
    string NoticeSection,
    string NoticeNumber,
    string ClientName,
    DateTimeOffset HearingDate,
    HearingType HearingType,
    string? OfficerName,
    string? OfficerDesignation,
    string? MeetingLink,
    HearingStatus Status,
    string? AdjournmentReason,
    string? SummaryNotes,
    Guid? AttendedByUserId,
    string? AttendedByName,
    DateTime CreatedAt
);

public record NoticeResponseDto(
    Guid Id,
    Guid NoticeId,
    int ResponseVersion,
    string SubmissionTitle,
    string SummaryOfSubmissions,
    string? LegalGrounds,
    ResponseStatus Status,
    Guid? PreparedByUserId,
    string? PreparedByName,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt,
    string? PortalAckNumber,
    DateTimeOffset? SubmittedAt,
    string? AttachmentNames,
    DateTime CreatedAt
);

public record TaxDemandDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    Guid? NoticeId,
    string? AssessmentYear,
    string FinancialYear,
    string Section,
    decimal OriginalDemand,
    decimal StayedAmount,
    decimal PaidAmount,
    decimal AdjustedRefund,
    decimal OutstandingAmount,
    DateOnly DemandDate,
    DateOnly AppealDueDate,
    int DaysUntilAppealDue,
    AppealStatus AppealStatus,
    string? AppealAckNumber,
    string? Remarks
);

public record NoticeDashboardSummaryDto(
    int TotalActiveNotices,
    int IncomeTaxNotices,
    int GstNotices,
    int PendingHearings,
    int PendingApprovals,
    decimal TotalDemandAmount,
    decimal TotalOutstandingDemand,
    int AppealsDueWithin30Days,
    int InvalidDinCount
);

public record CreateTaxNoticeRequest(
    Guid ClientId,
    TaxAuthority Authority,
    NoticeCategory Category,
    string Section,
    string NoticeNumber,
    string? Din,
    string? AssessmentYear,
    string FinancialYear,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal DemandAmount,
    decimal RefundDisallowed,
    string Subject,
    string? Description,
    Guid? AssignedToUserId,
    string? OfficerName,
    string? OfficerJurisdiction
);

public record UpdateTaxNoticeRequest(
    NoticeStatus Status,
    string? Din,
    DateOnly DueDate,
    decimal DemandAmount,
    decimal RefundDisallowed,
    string Subject,
    string? Description,
    Guid? AssignedToUserId,
    string? OfficerName,
    string? OfficerJurisdiction
);

public record CreateHearingRequest(
    DateTimeOffset HearingDate,
    HearingType HearingType,
    string? OfficerName,
    string? OfficerDesignation,
    string? MeetingLink,
    string? SummaryNotes
);

public record UpdateHearingStatusRequest(
    HearingStatus Status,
    string? AdjournmentReason,
    string? SummaryNotes,
    Guid? AttendedByUserId
);

public record DraftResponseRequest(
    string SubmissionTitle,
    string SummaryOfSubmissions,
    string? LegalGrounds,
    string? AttachmentNames
);

public record RecordSubmissionRequest(
    string PortalAckNumber,
    DateTimeOffset? SubmittedAt
);

public record CreateTaxDemandRequest(
    Guid ClientId,
    Guid? NoticeId,
    string? AssessmentYear,
    string FinancialYear,
    string Section,
    decimal OriginalDemand,
    DateOnly DemandDate,
    DateOnly? AppealDueDate,
    string? Remarks
);

public record RecordDemandPaymentRequest(
    decimal Amount,
    bool IsRefundAdjustment, // True = Sec 245 adjustment; False = Direct payment
    string? Remarks
);

public record UpdateAppealRequest(
    AppealStatus Status,
    string? AppealAckNumber,
    decimal? StayedAmount,
    string? Remarks
);
