using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Audit;

public static class AuditType
{
    public const string Statutory = "statutory";
    public const string TaxAudit44Ab = "tax_audit_44ab";
    public const string Internal = "internal";
    public const string BankConcurrent = "bank_concurrent";
    public const string LimitedReview = "limited_review";

    public static readonly string[] All = [Statutory, TaxAudit44Ab, Internal, BankConcurrent, LimitedReview];
}

public static class EngagementStatus
{
    public const string Planning = "planning";
    public const string Fieldwork = "fieldwork";
    public const string Review = "review";
    public const string PartnerSignoff = "partner_signoff";
    public const string Archived = "archived";

    public static readonly string[] All = [Planning, Fieldwork, Review, PartnerSignoff, Archived];
}

public static class BenchmarkType
{
    public const string Revenue = "revenue";
    public const string ProfitBeforeTax = "profit_before_tax";
    public const string TotalAssets = "total_assets";
    public const string Equity = "equity";

    public static readonly string[] All = [Revenue, ProfitBeforeTax, TotalAssets, Equity];
}

public static class AuditAssertion
{
    public const string Completeness = "completeness";
    public const string Existence = "existence";
    public const string Accuracy = "accuracy";
    public const string Valuation = "valuation";
    public const string RightsObligations = "rights_obligations";
    public const string Presentation = "presentation";

    public static readonly string[] All = [Completeness, Existence, Accuracy, Valuation, RightsObligations, Presentation];
}

public static class SamplingMethod
{
    public const string HundredPercent = "100_percent";
    public const string MonetaryUnitSampling = "monetary_unit_sampling";
    public const string Haphazard = "haphazard";
    public const string Stratified = "stratified";
    public const string RiskTargeted = "risk_targeted";

    public static readonly string[] All = [HundredPercent, MonetaryUnitSampling, Haphazard, Stratified, RiskTargeted];
}

public static class AuditConclusion
{
    public const string Satisfactory = "satisfactory";
    public const string QualifiedFinding = "qualified_finding";
    public const string MaterialMisstatement = "material_misstatement";
    public const string PendingEvidence = "pending_evidence";

    public static readonly string[] All = [Satisfactory, QualifiedFinding, MaterialMisstatement, PendingEvidence];
}

public static class WorkingPaperStatus
{
    public const string Draft = "draft";
    public const string ReadyForReview = "ready_for_review";
    public const string Reviewed = "reviewed";
    public const string PartnerApproved = "partner_approved";

    public static readonly string[] All = [Draft, ReadyForReview, Reviewed, PartnerApproved];
}

public static class AuditOpinion
{
    public const string Unmodified = "unmodified";
    public const string Qualified = "qualified";
    public const string Adverse = "adverse";
    public const string Disclaimer = "disclaimer";

    public static readonly string[] All = [Unmodified, Qualified, Adverse, Disclaimer];
}

public class AuditEngagement : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";
    public string AuditType { get; set; } = Domain.Audit.AuditType.Statutory;
    public string Title { get; set; } = "";
    public Guid? LeadPartnerId { get; set; }
    public Guid? ManagerId { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public string Status { get; set; } = EngagementStatus.Planning;

    // SA 320 Materiality
    public string BenchmarkType { get; set; } = Domain.Audit.BenchmarkType.Revenue;
    public decimal BenchmarkAmount { get; set; }
    public decimal OverallMateriality { get; set; }
    public decimal PerformanceMateriality { get; set; }
    public decimal TrivialThreshold { get; set; }

    public string? AuditOpinion { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public User? LeadPartner { get; set; }
    public User? Manager { get; set; }
    public List<AuditWorkingPaper> WorkingPapers { get; set; } = new();
}

public class AuditWorkingPaper : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public string RefCode { get; set; } = "";
    public string Area { get; set; } = "";
    public string Title { get; set; } = "";
    public string Assertion { get; set; } = AuditAssertion.Existence;
    public string Objective { get; set; } = "";
    public string ProceduresPerformed { get; set; } = "";

    // SA 530 Sampling & Testing
    public string SamplingMethod { get; set; } = Domain.Audit.SamplingMethod.Haphazard;
    public decimal PopulationValue { get; set; }
    public decimal SampleValue { get; set; }
    public int ItemsTestedCount { get; set; }
    public string? ExceptionsNoted { get; set; }
    public string Conclusion { get; set; } = AuditConclusion.PendingEvidence;

    // Workflow
    public string Status { get; set; } = WorkingPaperStatus.Draft;
    public Guid? PreparerId { get; set; }
    public DateTime? PreparedAt { get; set; }
    public Guid? ReviewerId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public string Attachments { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public AuditEngagement? Engagement { get; set; }
    public User? Preparer { get; set; }
    public User? Reviewer { get; set; }
}
