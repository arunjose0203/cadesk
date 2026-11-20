using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Engagements;

public static class EngagementType
{
    public const string StatutoryAudit = "StatutoryAudit";
    public const string InternalAudit = "InternalAudit";
    public const string TaxAudit = "TaxAudit";
    public const string GstAudit = "GstAudit";
    public const string DirectTaxFiling = "DirectTaxFiling";
    public const string SecretarialCompliance = "SecretarialCompliance";
    public const string AccountingBookkeeping = "AccountingBookkeeping";
    public const string DueDiligence = "DueDiligence";
    public const string Advisory = "Advisory";

    public static readonly string[] All =
    [
        StatutoryAudit, InternalAudit, TaxAudit, GstAudit,
        DirectTaxFiling, SecretarialCompliance, AccountingBookkeeping,
        DueDiligence, Advisory
    ];
}

public static class EngagementFeeStructure
{
    public const string FixedFee = "FixedFee";
    public const string TimeBased = "TimeBased";
    public const string MonthlyRetainer = "MonthlyRetainer";
    public const string QuarterlyRetainer = "QuarterlyRetainer";

    public static readonly string[] All = [FixedFee, TimeBased, MonthlyRetainer, QuarterlyRetainer];
}

public static class EngagementStatus
{
    public const string Draft = "Draft";
    public const string SentToClient = "SentToClient";
    public const string Accepted = "Accepted";
    public const string SignedDigitally = "SignedDigitally";
    public const string Declined = "Declined";
    public const string Superseded = "Superseded";

    public static readonly string[] All = [Draft, SentToClient, Accepted, SignedDigitally, Declined, Superseded];
}

public static class KycRiskRating
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string PoliticallyExposedPerson = "PoliticallyExposedPerson";

    public static readonly string[] All = [Low, Medium, High, PoliticallyExposedPerson];
}

public static class KycVerificationStatus
{
    public const string Verified = "Verified";
    public const string PendingVerification = "PendingVerification";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Verified, PendingVerification, Rejected];
}

/// <summary>
/// Digital Engagement Letter conforming to ICAI SA 210 and SQC 1 standards.
/// </summary>
public class EngagementLetter : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string LetterNumber { get; set; } = string.Empty;
    public string EngagementType { get; set; } = Engagements.EngagementType.StatutoryAudit;
    public string FinancialYear { get; set; } = string.Empty;
    public string ScopeOfWork { get; set; } = string.Empty;
    public string ManagementResponsibilities { get; set; } = string.Empty;
    public string AuditorResponsibilities { get; set; } = string.Empty;
    public string FeeStructure { get; set; } = EngagementFeeStructure.FixedFee;
    public decimal AgreedFeeAmount { get; set; }
    public bool OutOfPocketIncluded { get; set; }
    public string Status { get; set; } = EngagementStatus.Draft;

    public DateTime? SentDate { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public string? ClientSignatoryName { get; set; }
    public string? ClientSignatoryDesignation { get; set; }
    public string? AcceptanceIp { get; set; }
    public string LetterContent { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Client Anti-Money Laundering (AML) / PMLA KYC & Beneficial Ownership (UBO) record.
/// </summary>
public class ClientKycRecord : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string RiskRating { get; set; } = KycRiskRating.Low;
    public string VerificationStatus { get; set; } = KycVerificationStatus.PendingVerification;
    public string ConstitutionDocumentType { get; set; } = "CertificateOfIncorporation";
    public string? ConstitutionDocumentNumber { get; set; }
    public bool PanVerified { get; set; }
    public bool GstinVerified { get; set; }
    public string? AddressProofType { get; set; }
    public string? AddressProofNumber { get; set; }

    public string? UboName { get; set; }
    public decimal? UboShareholdingPercentage { get; set; }
    public string? UboPan { get; set; }
    public bool SanctionsCheckCleared { get; set; } = true;
    public bool AdverseMediaCheckCleared { get; set; } = true;
    public string? RiskRationale { get; set; }

    public DateOnly LastReviewedDate { get; set; }
    public DateOnly NextReviewDueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
