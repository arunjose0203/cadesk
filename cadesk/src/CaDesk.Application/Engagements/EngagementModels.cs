namespace CaDesk.Application.Engagements;

public sealed record EngagementLetterDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string LetterNumber,
    string EngagementType,
    string FinancialYear,
    string ScopeOfWork,
    string ManagementResponsibilities,
    string AuditorResponsibilities,
    string FeeStructure,
    decimal AgreedFeeAmount,
    bool OutOfPocketIncluded,
    string Status,
    DateTime? SentDate,
    DateTime? AcceptedDate,
    string? ClientSignatoryName,
    string? ClientSignatoryDesignation,
    string? AcceptanceIp,
    string LetterContent,
    DateTime CreatedAt
);

public sealed record CreateEngagementLetterRequest(
    Guid ClientId,
    string EngagementType,
    string FinancialYear,
    string? ScopeOfWork,
    string? ManagementResponsibilities,
    string? AuditorResponsibilities,
    string? FeeStructure,
    decimal AgreedFeeAmount,
    bool? OutOfPocketIncluded,
    string? LetterContent
);

public sealed record AcceptEngagementLetterRequest(
    string SignatoryName,
    string SignatoryDesignation,
    string? IpAddress
);

public sealed record ClientKycRecordDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string RiskRating,
    string VerificationStatus,
    string ConstitutionDocumentType,
    string? ConstitutionDocumentNumber,
    bool PanVerified,
    bool GstinVerified,
    string? AddressProofType,
    string? AddressProofNumber,
    string? UboName,
    decimal? UboShareholdingPercentage,
    string? UboPan,
    bool SanctionsCheckCleared,
    bool AdverseMediaCheckCleared,
    string? RiskRationale,
    DateOnly LastReviewedDate,
    DateOnly NextReviewDueDate,
    DateTime CreatedAt
);

public sealed record SaveClientKycRequest(
    Guid ClientId,
    string RiskRating,
    string? VerificationStatus,
    string ConstitutionDocumentType,
    string? ConstitutionDocumentNumber,
    bool? PanVerified,
    bool? GstinVerified,
    string? AddressProofType,
    string? AddressProofNumber,
    string? UboName,
    decimal? UboShareholdingPercentage,
    string? UboPan,
    bool? SanctionsCheckCleared,
    bool? AdverseMediaCheckCleared,
    string? RiskRationale
);

public sealed record EngagementKycSummaryDto(
    int TotalLettersCount,
    int AcceptedLettersCount,
    int PendingLettersCount,
    int TotalKycRecordsCount,
    int VerifiedKycCount,
    int HighRiskKycCount,
    decimal TotalContractedFeeValue
);

public sealed record GenerateTemplateRequest(
    string EngagementType,
    string ClientName,
    string FinancialYear,
    string? FeeStructure,
    decimal AgreedFeeAmount
);

public sealed record GeneratedEngagementTemplate(
    string EngagementType,
    string DefaultScope,
    string DefaultManagementResponsibilities,
    string DefaultAuditorResponsibilities,
    string FullLetterContent
);
