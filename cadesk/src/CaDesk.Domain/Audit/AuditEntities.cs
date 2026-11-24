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

public static class EqrReviewType
{
    public const string HotReview = "hot_review";
    public const string ColdReview = "cold_review";

    public static readonly string[] All = [HotReview, ColdReview];
}

public static class EqrReviewStatus
{
    public const string NotApplicable = "not_applicable";
    public const string Assigned = "assigned";
    public const string InProgress = "in_progress";
    public const string ClearanceGranted = "clearance_granted";
    public const string QualificationsNoted = "qualifications_noted";

    public static readonly string[] All = [NotApplicable, Assigned, InProgress, ClearanceGranted, QualificationsNoted];
}

public static class ReviewNoteRole
{
    public const string Manager = "manager";
    public const string Eqr = "eqr";
    public const string LeadPartner = "lead_partner";

    public static readonly string[] All = [Manager, Eqr, LeadPartner];
}

public static class ReviewNoteCategory
{
    public const string DocumentationGap = "documentation_gap";
    public const string InsufficientTesting = "insufficient_testing";
    public const string AssertionUnverified = "assertion_unverified";
    public const string AccountingPolicyMismatch = "accounting_policy_mismatch";
    public const string CAROClauseOmission = "caro_clause_omission";
    public const string Other = "other";

    public static readonly string[] All = [DocumentationGap, InsufficientTesting, AssertionUnverified, AccountingPolicyMismatch, CAROClauseOmission, Other];
}

public static class ReviewNotePriority
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";

    public static readonly string[] All = [Low, Medium, High, Critical];
}

public static class ReviewNoteStatus
{
    public const string Open = "open";
    public const string ResponseProvided = "response_provided";
    public const string Cleared = "cleared";
    public const string Reopened = "reopened";

    public static readonly string[] All = [Open, ResponseProvided, Cleared, Reopened];
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

    // Dedicated Partner Sign-off & ICAI UDIN
    public string? Udin { get; set; }
    public string? PartnerMembershipNo { get; set; }
    public string? PartnerSignoffRemarks { get; set; }

    // ICAI SQC 1 & SA 220 Engagement Quality Control Review & Archival
    public DateOnly? AuditReportDate { get; set; }
    public DateOnly? ArchivalDeadline { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedBy { get; set; }
    public bool EqrRequired { get; set; }
    public Guid? EqrPartnerId { get; set; }
    public string EqrStatus { get; set; } = EqrReviewStatus.NotApplicable;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public User? LeadPartner { get; set; }
    public User? Manager { get; set; }
    public User? ArchivedByUser { get; set; }
    public User? EqrPartner { get; set; }
    public List<AuditWorkingPaper> WorkingPapers { get; set; } = new();
    public List<AuditEqrReview> EqrReviews { get; set; } = new();
    public List<AuditReviewNote> ReviewNotes { get; set; } = new();
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
    public List<AuditReviewNote> ReviewNotesList { get; set; } = new();
}

public class AuditEqrReview : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public string ReviewType { get; set; } = EqrReviewType.HotReview;
    public Guid EqrPartnerId { get; set; }
    public string ReviewStatus { get; set; } = EqrReviewStatus.Assigned;

    public string? EvaluationMateriality { get; set; }
    public string? EvaluationSignificantRisks { get; set; }
    public string? EvaluationGoingConcern { get; set; }
    public string? EvaluationAuditOpinion { get; set; }
    public string PreIssuanceChecklist { get; set; } = "[]";
    public DateTime? EqrSignoffDate { get; set; }
    public string? EqrConclusionRemarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public AuditEngagement? Engagement { get; set; }
    public User? EqrPartner { get; set; }
}

public class AuditReviewNote : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public Guid? WorkingPaperId { get; set; }
    public Guid RaisedByUserId { get; set; }
    public string RaisedByRole { get; set; } = ReviewNoteRole.Manager;
    public string Category { get; set; } = ReviewNoteCategory.DocumentationGap;
    public string Priority { get; set; } = ReviewNotePriority.Medium;
    public string Query { get; set; } = "";
    public string Status { get; set; } = ReviewNoteStatus.Open;

    public string? Response { get; set; }
    public Guid? ResponseByUserId { get; set; }
    public DateTime? ResponseAt { get; set; }
    public Guid? ClearedByUserId { get; set; }
    public DateTime? ClearedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public AuditEngagement? Engagement { get; set; }
    public AuditWorkingPaper? WorkingPaper { get; set; }
    public User? RaisedByUser { get; set; }
    public User? ResponseByUser { get; set; }
    public User? ClearedByUser { get; set; }
}

public static class AuditQualityEngine
{
    public const int Sqc1ArchivalDaysLimit = 60;

    public static DateOnly CalculateArchivalDeadline(DateOnly auditReportDate)
    {
        return auditReportDate.AddDays(Sqc1ArchivalDaysLimit);
    }

    public static string GetArchivalStatus(bool isArchived, DateOnly? auditReportDate, DateOnly? deadline, DateOnly currentDate)
    {
        if (isArchived)
            return "Archived & Locked (SQC 1 Sealed)";
        if (!auditReportDate.HasValue || !deadline.HasValue)
            return "In Progress (Report not yet issued)";

        var diff = deadline.Value.DayNumber - currentDate.DayNumber;
        if (diff < 0)
            return $"OVERDUE for Archival by {Math.Abs(diff)} days (SQC 1 Breach)";
        if (diff <= 15)
            return $"Archival Due in {diff} days (Urgent)";
        return $"Archival Due in {diff} days ({deadline:dd-MMM-yyyy})";
    }

    public static (bool Allowed, string Reason) CanSignOffAudit(int openReviewNotesCount, bool eqrRequired, string? eqrStatus)
    {
        if (openReviewNotesCount > 0)
        {
            return (false, $"Cannot sign off audit: {openReviewNotesCount} review note(s) remain open or uncleared.");
        }

        if (eqrRequired && eqrStatus != EqrReviewStatus.ClearanceGranted)
        {
            return (false, "Cannot sign off audit: Engagement Quality Review (EQR) hot review clearance is mandatory prior to report issuance under ICAI SA 220.");
        }

        return (true, "Eligible for Partner Sign-off and Report Issuance.");
    }

    public static List<(string Code, string Description, bool Required)> GetStandardPreIssuanceChecklist()
    {
        return
        [
            ("SA_210", "Engagement terms and signed audit engagement letter verified", true),
            ("SA_315", "Identification and assessment of risks of material misstatement documented", true),
            ("SA_320", "Materiality thresholds and performance materiality benchmarked", true),
            ("SA_240", "Auditor responsibilities relating to fraud and management override tested", true),
            ("SA_505", "External bank & trade receivable confirmations dispatched and reconciled", true),
            ("SA_501", "Physical inventory count attendance and valuation verified", true),
            ("SA_570", "Management's assessment of Going Concern for 12 months verified", true),
            ("SA_580", "Written Management Representation Letter obtained and dated appropriately", true),
            ("CARO_2020", "All applicable clauses of Companies (Auditor's Report) Order verified", true),
            ("TB_AGREEMENT", "Financial statements agree in all respects with underlying trial balance and books", true)
        ];
    }
}

public static class UdinValidator
{
    private static readonly System.Text.RegularExpressions.Regex UdinRegex =
        new(@"^[0-9]{2}[0-9A-Za-z]{16}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Validates that an ICAI UDIN is an 18-character alphanumeric string starting with a 2-digit fiscal issuance year.
    /// </summary>
    public static bool IsValid(string? udin)
    {
        if (string.IsNullOrWhiteSpace(udin)) return false;
        var trimmed = udin.Trim();
        return trimmed.Length == 18 && UdinRegex.IsMatch(trimmed);
    }
}
