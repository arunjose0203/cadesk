using CaDesk.Domain.TaxLitigation;

namespace CaDesk.Application.TaxLitigation;

public record TaxAppealDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientPan,
    Guid? TaxNoticeId,
    string? NoticeReferenceNumber,
    AppealForum Forum,
    string? AppealNumber,
    string AssessmentYear,
    string FinancialYear,
    string AssessingOfficerWardOrCircle,
    string ImpugnedOrderSection,
    string ImpugnedOrderNumber,
    DateOnly ImpugnedOrderDate,
    DateOnly DateOfServiceOfOrder,
    DateOnly StatutoryFilingDeadline,
    int DaysRemaining,
    bool DelayCondonationRequired,
    int DaysOfDelay,
    decimal FilingFeeAmount,
    decimal AssessedTotalIncome,
    decimal ReturnedTotalIncome,
    decimal TotalDisputedAdditions,
    decimal DisputedTaxDemand,
    DateOnly? FilingDate,
    string? DinOrAckNumber,
    AppealStatus Status,
    string? PartnerMembershipNumber,
    string? PartnerCertifiedByName,
    DateTimeOffset? PartnerCertifiedAt,
    int GroundsCount,
    int DocumentsCount,
    int CitationsCount,
    DateTime CreatedAt
);

public record TaxAppealDetailDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientPan,
    Guid? TaxNoticeId,
    string? NoticeReferenceNumber,
    AppealForum Forum,
    string? AppealNumber,
    string AssessmentYear,
    string FinancialYear,
    string AssessingOfficerWardOrCircle,
    string ImpugnedOrderSection,
    string ImpugnedOrderNumber,
    DateOnly ImpugnedOrderDate,
    DateOnly DateOfServiceOfOrder,
    DateOnly StatutoryFilingDeadline,
    int DaysRemaining,
    bool DelayCondonationRequired,
    int DaysOfDelay,
    string? CondonationReason,
    decimal FilingFeeAmount,
    decimal AssessedTotalIncome,
    decimal ReturnedTotalIncome,
    decimal TotalDisputedAdditions,
    decimal DisputedTaxDemand,
    DateOnly? FilingDate,
    string? DinOrAckNumber,
    AppealStatus Status,
    string StatementOfFacts,
    string ReliefSought,
    string? PartnerMembershipNumber,
    string? PartnerCertifiedByName,
    DateTimeOffset? PartnerCertifiedAt,
    List<AppealGroundItemDto> Grounds,
    List<PaperBookDocumentDto> PaperBookDocuments,
    List<AppealJudicialCitationDto> Citations,
    DateTime CreatedAt
);

public record AppealGroundItemDto(
    Guid Id,
    Guid TaxAppealId,
    int GroundNumber,
    GroundType GroundType,
    string Title,
    string GroundText,
    string StatutorySection,
    decimal DisputedAdditionAmount,
    bool IsWithoutPrejudice,
    bool IsAlternativeGround,
    int OrderIndex
);

public record PaperBookDocumentDto(
    Guid Id,
    Guid TaxAppealId,
    int ItemNumber,
    DateOnly DocumentDate,
    string DescriptionOfDocument,
    string Relevance,
    PaperBookEvidenceType EvidenceType,
    DateOnly? LowerAuthorityFilingDate,
    int PageNumberFrom,
    int PageNumberTo,
    int TotalPages,
    bool IsCertified,
    int OrderIndex
);

public record AppealJudicialCitationDto(
    Guid Id,
    Guid TaxAppealId,
    Guid? PrecedentId,
    string CaseTitle,
    string Citation,
    string CourtOrTribunal,
    int JudgmentYear,
    string StatutorySection,
    string KeyRatioProposition,
    string ParagraphReference,
    string RelevanceToGround
);

public record CreateOrUpdateTaxAppealRequest(
    Guid? Id,
    Guid ClientId,
    Guid? TaxNoticeId,
    string? NoticeReferenceNumber,
    AppealForum Forum,
    string? AppealNumber,
    string AssessmentYear,
    string FinancialYear,
    string AssessingOfficerWardOrCircle,
    string ImpugnedOrderSection,
    string ImpugnedOrderNumber,
    DateOnly ImpugnedOrderDate,
    DateOnly DateOfServiceOfOrder,
    decimal AssessedTotalIncome,
    decimal ReturnedTotalIncome,
    decimal TotalDisputedAdditions,
    decimal DisputedTaxDemand,
    string? CondonationReason,
    string StatementOfFacts,
    string ReliefSought
);

public record SaveAppealGroundRequest(
    Guid? Id,
    Guid TaxAppealId,
    int GroundNumber,
    GroundType GroundType,
    string Title,
    string GroundText,
    string StatutorySection,
    decimal DisputedAdditionAmount,
    bool IsWithoutPrejudice,
    bool IsAlternativeGround,
    int OrderIndex
);

public record SavePaperBookDocRequest(
    Guid? Id,
    Guid TaxAppealId,
    int ItemNumber,
    DateOnly DocumentDate,
    string DescriptionOfDocument,
    string Relevance,
    PaperBookEvidenceType EvidenceType,
    DateOnly? LowerAuthorityFilingDate,
    int PageCount,
    bool IsCertified,
    int OrderIndex
);

public record AddJudicialCitationRequest(
    Guid TaxAppealId,
    Guid? PrecedentId,
    string CaseTitle,
    string Citation,
    string CourtOrTribunal,
    int JudgmentYear,
    string StatutorySection,
    string KeyRatioProposition,
    string ParagraphReference,
    string RelevanceToGround
);

public record CertifyAppealRequest(
    string PartnerMembershipNumber,
    string PartnerName,
    DateOnly? FilingDate,
    string? DinOrAckNumber
);

public record CalculateFilingFeeRequest(
    AppealForum Forum,
    decimal AssessedTotalIncome,
    bool IsOtherMatter
);

public record FilingFeeResultDto(
    AppealForum Forum,
    decimal AssessedTotalIncome,
    decimal StatutoryFilingFee,
    string LegalBasis
);
