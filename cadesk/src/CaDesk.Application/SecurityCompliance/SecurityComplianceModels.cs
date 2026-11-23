namespace CaDesk.Application.SecurityCompliance;

public sealed record SecurityAssessmentSummaryDto(
    Guid Id,
    string AssessmentPeriod,
    string Status,
    int OverallScorePercentage,
    int TotalControlsCount,
    int CompliantControlsCount,
    int PartiallyCompliantCount,
    int AccessControlScore,
    int CryptographyScore,
    int OperationsScore,
    int PhysicalScore,
    int PrivacyScore,
    string? CertifiedByPartner,
    string? PartnerMembershipNo,
    DateTime? LastEvaluatedAt,
    DateTime? CertifiedAt
);

public sealed record SecurityControlEvidenceDto(
    Guid Id,
    Guid AssessmentId,
    string ControlCode,
    string Title,
    string Category,
    string FrameworkMapping,
    string Description,
    string VerificationMethod,
    string Status,
    bool IsAutomatedTelemetry,
    string? TelemetrySignal,
    string? EvidenceNotes,
    DateTime? LastTestedAt
);

public sealed record UpdateControlEvidenceRequest(
    string? Status,
    string? EvidenceNotes
);

public sealed record CertifySecurityAssessmentRequest(
    string PartnerName,
    string MembershipNumber,
    string? AssessmentPeriod
);

public sealed record TelemetryScanResultDto(
    int OverallScore,
    int CompliantCount,
    IReadOnlyList<string> EvaluatedSignals,
    DateTime ScanTimestamp
);

public sealed record TrustCenterOverviewDto(
    string FirmName,
    string CertificationStatus,
    int OverallCompliancePercentage,
    IReadOnlyList<string> FrameworksSupported,
    IReadOnlyList<string> CoreHighlights,
    DateTime LastAuditedAt
);
