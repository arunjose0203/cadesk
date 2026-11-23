using CaDesk.Domain.TaxNotices;

namespace CaDesk.Application.TaxNotices;

public record ParseNoticeRequest(
    string RawText,
    Guid? ClientId
);

public record ParsedNoticeResultDto(
    TaxAuthority Authority,
    NoticeCategory Category,
    string Section,
    string? Din,
    DinStatus DinStatus,
    string? PanOrGstin,
    string? AssessmentYear,
    string? FinancialYear,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    decimal DemandAmount,
    string OfficerJurisdiction,
    string ExtractedIssue,
    string SummaryOfAllegations,
    List<string> RecommendedPreliminaryObjections,
    List<string> RecommendedGrounds,
    List<StatutoryPrecedentDto> MatchedPrecedents,
    int ConfidenceScore
);

public record StatutoryPrecedentDto(
    Guid Id,
    Guid? FirmId,
    TaxAuthority Authority,
    PrecedentCategory Category,
    string CaseTitle,
    string Citation,
    string CourtBench,
    int JudgmentYear,
    string ApplicableSections,
    string KeyRatioDecidendi,
    string DefenseGroundTemplate,
    bool IsLandmark,
    string Keywords
);

public record TaxDefenseDraftDto(
    Guid Id,
    Guid NoticeId,
    Guid ClientId,
    string ClientName,
    string DraftNumber,
    string Title,
    string Subject,
    string AddressedTo,
    string ClientPanOrGstin,
    string AssessmentYear,
    string FinancialYear,
    string NoticeRefNumber,
    string NoticeDin,
    DateOnly NoticeIssueDate,
    DateOnly NoticeDueDate,
    List<string> PreliminaryObjections,
    List<FactualItem> FactualMatrix,
    List<MeritGroundItem> MeritGrounds,
    List<CitedPrecedentItem> CitedPrecedents,
    string PrayerForRelief,
    List<AnnexureItem> AnnexureIndex,
    DefenseDraftStatus Status,
    Guid? PreparedByUserId,
    string? PreparedByName,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt,
    string? PartnerUdin,
    string? GeneratedHtml,
    DateTime CreatedAt
);

public record CreateDefenseDraftRequest(
    Guid NoticeId,
    string? Title,
    string? Subject,
    string? AddressedTo,
    List<string>? PreliminaryObjections,
    List<FactualItem>? FactualMatrix,
    List<MeritGroundItem>? MeritGrounds,
    List<CitedPrecedentItem>? CitedPrecedents,
    string? PrayerForRelief,
    List<AnnexureItem>? AnnexureIndex
);

public record UpdateDefenseDraftRequest(
    string Title,
    string Subject,
    string AddressedTo,
    List<string> PreliminaryObjections,
    List<FactualItem> FactualMatrix,
    List<MeritGroundItem> MeritGrounds,
    List<CitedPrecedentItem> CitedPrecedents,
    string PrayerForRelief,
    List<AnnexureItem> AnnexureIndex
);

public record ApproveDefenseDraftRequest(
    string? PartnerUdin,
    string? PartnerNotes
);

public record CreatePrecedentRequest(
    TaxAuthority Authority,
    PrecedentCategory Category,
    string CaseTitle,
    string Citation,
    string CourtBench,
    int JudgmentYear,
    string ApplicableSections,
    string KeyRatioDecidendi,
    string DefenseGroundTemplate,
    string Keywords
);
