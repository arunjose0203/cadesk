using CaDesk.Domain.Common;

namespace CaDesk.Domain.Aqmm;

public static class AqmmSection
{
    public const string PracticeManagement = "PracticeManagement"; // Section A - 190 points
    public const string HumanResources = "HumanResources";         // Section B - 110 points
    public const string AuditExecution = "AuditExecution";         // Section C - 300 points
}

public static class AqmmMaturityLevel
{
    public const string Level1 = "Level 1"; // < 450 points: Needs Improvement
    public const string Level2 = "Level 2"; // 450 - 509 points: Good Maturity
    public const string Level3 = "Level 3"; // 510 - 600 points: Quality Excellence
}

public static class AqmmAssessmentStatus
{
    public const string Draft = "Draft";
    public const string UnderReview = "UnderReview";
    public const string Certified = "Certified";
}

public class AqmmAssessment : TenantEntity
{
    public string FinancialYear { get; set; } = "2024-25";
    public string Status { get; set; } = AqmmAssessmentStatus.Draft;

    public int TotalScore { get; set; } = 0;
    public int MaxPossibleScore { get; set; } = 600;
    public decimal OverallPercentage { get; set; } = 0m;
    public string MaturityLevel { get; set; } = AqmmMaturityLevel.Level1;

    public int SectionAScore { get; set; } = 0; // Max 190
    public int SectionBScore { get; set; } = 0; // Max 110
    public int SectionCScore { get; set; } = 0; // Max 300

    public string? LeadPartnerName { get; set; }
    public string? PartnerMembershipNo { get; set; }
    public string? FirmRegistrationNo { get; set; }
    public string? Udin { get; set; }
    public DateTime? CertifiedAt { get; set; }
}

public class AqmmResponse : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public string QuestionCode { get; set; } = string.Empty;
    public string Section { get; set; } = AqmmSection.PracticeManagement;
    public int MaxPoints { get; set; }
    public int AwardedPoints { get; set; } = 0;
    public string? PartnerRemarks { get; set; }
    public string? EvidenceReference { get; set; }
    public bool IsAutoEvaluated { get; set; } = false;
}

public record AqmmQuestionDef(
    string Code,
    string Section,
    string Title,
    string Description,
    int MaxPoints,
    IReadOnlyList<AqmmScoringCriterion> Criteria,
    string? AutoEvaluationKey = null
);

public record AqmmScoringCriterion(
    int Points,
    string Label,
    string Description
);

public static class AqmmEngine
{
    public const int MaxSectionA = 190;
    public const int MaxSectionB = 110;
    public const int MaxSectionC = 300;
    public const int MaxTotal = 600;

    public static readonly IReadOnlyList<AqmmQuestionDef> StandardQuestions =
    [
        // =====================================================================
        // SECTION A: Practice Management - Operations (190 Points)
        // =====================================================================
        new(
            Code: "AQMM-A1",
            Section: AqmmSection.PracticeManagement,
            Title: "Governance & Tone at the Top",
            Description: "Formal audit quality policy approved by leadership, documented Code of Ethics compliance, and annual audit quality review meetings.",
            MaxPoints: 20,
            Criteria:
            [
                new(20, "Full Compliance", "Documented quality policy, dedicated Quality Control partner appointed, and quarterly quality review records."),
                new(10, "Partial Compliance", "Quality policy exists but reviews are informal or ad-hoc."),
                new(0, "Non-Compliant", "No documented audit quality policy.")
            ]
        ),
        new(
            Code: "AQMM-A2",
            Section: AqmmSection.PracticeManagement,
            Title: "Client Onboarding, KYC & PMLA Risk Categorization",
            Description: "Rigorous client onboarding process including PMLA risk categorization (Low/Medium/High/PEP), SA 210 engagement letters, and pre-acceptance conflict checks.",
            MaxPoints: 35,
            Criteria:
            [
                new(35, "Full Compliance", "100% clients have digitally signed SA 210 letters, PMLA risk matrices, and UBO verification."),
                new(20, "Substantial Compliance", "Formal onboarding exists for 80%+ clients with manual PMLA checklists."),
                new(0, "Non-Compliant", "Ad-hoc onboarding without risk assessment.")
            ],
            AutoEvaluationKey: "KYC_COMPLETION"
        ),
        new(
            Code: "AQMM-A3",
            Section: AqmmSection.PracticeManagement,
            Title: "Independence & Conflict of Interest Monitoring",
            Description: "Annual independence declarations signed by all partners/staff, pre-engagement conflict checks, and Section 144 non-audit service prohibitions.",
            MaxPoints: 30,
            Criteria:
            [
                new(30, "Full Compliance", "Centralized digital registry for independence affirmations, strict prohibition of Sec 144 services, and rotation tracking."),
                new(15, "Partial Compliance", "Annual independence declarations collected manually without centralized registry."),
                new(0, "Non-Compliant", "No periodic independence declarations.")
            ],
            AutoEvaluationKey: "INDEPENDENCE_REGISTRY"
        ),
        new(
            Code: "AQMM-A4",
            Section: AqmmSection.PracticeManagement,
            Title: "Budgeting, Fee Realization & Single-Client Reliance",
            Description: "Evaluation of reliance on single clients (<15% total firm fees rule), timely fee realization, and timesheet-based engagement profitability tracking.",
            MaxPoints: 25,
            Criteria:
            [
                new(25, "Full Compliance", "No single client exceeds 15% of gross firm receipts, timesheets tracked for all audit staff, fee recovery within 60 days."),
                new(12, "Partial Compliance", "Fee reliance monitored annually; manual billing without real-time timesheet margins."),
                new(0, "Non-Compliant", "Single client reliance exceeds 15% or fee recovery uncontrolled.")
            ],
            AutoEvaluationKey: "FEE_RELIANCE"
        ),
        new(
            Code: "AQMM-A5",
            Section: AqmmSection.PracticeManagement,
            Title: "IT Architecture, Digitization & Information Security",
            Description: "Two-lock data isolation, automated daily encrypted backups (AES-256), multi-factor authentication (MFA/TOTP), and virus scanning of client workpapers.",
            MaxPoints: 35,
            Criteria:
            [
                new(35, "Full Compliance", "Cloud/SaaS infrastructure with row-level security, encrypted backups, mandatory MFA, and automated ClamAV scanning."),
                new(20, "Moderate Digitization", "Cloud storage utilized but backups and MFA enforced manually."),
                new(0, "Legacy System", "Local standalone hard drives without encryption or automated backups.")
            ],
            AutoEvaluationKey: "IT_SECURITY"
        ),
        new(
            Code: "AQMM-A6",
            Section: AqmmSection.PracticeManagement,
            Title: "Service Delivery & Practice Diversification",
            Description: "Balanced practice areas across Assurance, Taxation, Corporate Advisory, and Regulatory compliance with dedicated service vertical leads.",
            MaxPoints: 25,
            Criteria:
            [
                new(25, "Well-Diversified", "Assurance and non-assurance services properly segregated with dedicated partner leads."),
                new(12, "Moderate Diversification", "Generalist practice with common staff across all domains."),
                new(0, "Single-Service Concentration", "Practice heavily concentrated in one transactional compliance area.")
            ]
        ),
        new(
            Code: "AQMM-A7",
            Section: AqmmSection.PracticeManagement,
            Title: "Client Feedback & Grievance Redressal Mechanism",
            Description: "Formal post-audit client feedback mechanism, documented complaint redressal SLA, and client satisfaction surveys.",
            MaxPoints: 20,
            Criteria:
            [
                new(20, "Formal Redressal", "Dedicated helpdesk/portal ticketing system with SLA tracking and annual review."),
                new(10, "Informal Feedback", "Feedback collected verbally without formal documentation."),
                new(0, "No Mechanism", "No client feedback or grievance logging.")
            ],
            AutoEvaluationKey: "CLIENT_PORTAL"
        ),

        // =====================================================================
        // SECTION B: Human Resource Management (110 Points)
        // =====================================================================
        new(
            Code: "AQMM-B1",
            Section: AqmmSection.HumanResources,
            Title: "Resource Planning & Partner-to-Staff Ratio",
            Description: "Balanced partner-to-qualified-assistant ratio (typically 1:3 to 1:6), adequate article assistant supervision, and realistic audit engagement scheduling.",
            MaxPoints: 25,
            Criteria:
            [
                new(25, "Optimal Allocation", "Partner-to-staff ratio within recommended guidelines, engagement hours budgeted and monitored via timesheets."),
                new(12, "Acceptable Ratio", "Staff allocation informal during peak audit season."),
                new(0, "Understaffed", "Severe workload imbalance; audit hours not tracked.")
            ],
            AutoEvaluationKey: "RESOURCE_ALLOCATION"
        ),
        new(
            Code: "AQMM-B2",
            Section: AqmmSection.HumanResources,
            Title: "Recruitment, Competence & Background Verification",
            Description: "Structured hiring policy for qualified CAs and audit assistants, verification of credentials with ICAI, and non-disclosure agreements (NDAs).",
            MaxPoints: 20,
            Criteria:
            [
                new(20, "Rigorous Process", "100% staff have verified ICAI membership/registration numbers, signed NDAs, and clear job descriptions."),
                new(10, "Basic Process", "Credentials verified informally without written NDAs."),
                new(0, "Informal Hiring", "No formal background checks or NDAs.")
            ]
        ),
        new(
            Code: "AQMM-B3",
            Section: AqmmSection.HumanResources,
            Title: "CPE Compliance & Mandatory Technical Training",
            Description: "All partners meet ICAI mandatory CPE credit hours (minimum 20-25 structured hours/year), plus internal staff training on new SAs, Ind AS, and CARO 2020.",
            MaxPoints: 30,
            Criteria:
            [
                new(30, "Exemplary Training", "All partners 100% CPE compliant; firm conducts at least 40 hours of internal technical workshops annually."),
                new(15, "Standard Compliance", "Partners meet minimum CPE hours; limited internal training for assistants."),
                new(0, "Deficient CPE", "Partners have shortfall in mandatory CPE credit hours.")
            ]
        ),
        new(
            Code: "AQMM-B4",
            Section: AqmmSection.HumanResources,
            Title: "Performance Appraisal Linked to Audit Quality",
            Description: "Performance evaluations incorporate audit workpaper quality, adherence to SAs, and zero bonus incentive for compromising audit rigor.",
            MaxPoints: 20,
            Criteria:
            [
                new(20, "Quality-Driven", "Appraisal explicitly weights technical accuracy and peer review results over commercial billing volume."),
                new(10, "Balanced", "Appraisal considers both quality and billing equally."),
                new(0, "Revenue-Only", "Appraisal based solely on fee collection and hours logged.")
            ]
        ),
        new(
            Code: "AQMM-B5",
            Section: AqmmSection.HumanResources,
            Title: "Staff Retention & Attrition Management",
            Description: "Staff attrition below 20% p.a., clear career progression pathways for senior managers to partnership, and fair stipend/compensation.",
            MaxPoints: 15,
            Criteria:
            [
                new(15, "Low Attrition", "Annual staff turnover below 15%, structured promotion criteria, competitive compensation."),
                new(8, "Moderate Attrition", "Turnover between 15% and 30% with standard succession planning."),
                new(0, "High Attrition", "Turnover exceeds 30% p.a. causing frequent audit team disruptions.")
            ]
        ),

        // =====================================================================
        // SECTION C: Audit Execution & Review (300 Points)
        // =====================================================================
        new(
            Code: "AQMM-C1",
            Section: AqmmSection.AuditExecution,
            Title: "SQC 1 System of Quality Control Implementation",
            Description: "Comprehensive quality control manual in compliance with ICAI SQC 1 covering leadership responsibilities, ethical requirements, and monitoring.",
            MaxPoints: 45,
            Criteria:
            [
                new(45, "Full SQC 1 Implementation", "Written SQC 1 manual adopted, annual internal monitoring of completed files, and documentation of inspection findings."),
                new(25, "Substantial SQC 1", "SQC 1 manual exists but annual internal monitoring reviews are irregular."),
                new(0, "No SQC 1 Manual", "Firm lacks a documented SQC 1 quality control framework.")
            ],
            AutoEvaluationKey: "SQC1_FRAMEWORK"
        ),
        new(
            Code: "AQMM-C2",
            Section: AqmmSection.AuditExecution,
            Title: "Audit Methodology, Materiality & Risk Assessment",
            Description: "Adherence to SA 315 & SA 320 with documented materiality benchmarks (5% PBT, 0.5% Revenue, 1% Assets), performance materiality, and threshold for clearly trivial matters.",
            MaxPoints: 40,
            Criteria:
            [
                new(40, "Standardized Methodology", "Automated materiality computation linked to Trial Balance, documented risk-of-material-misstatement (RoMM) at assertion level."),
                new(20, "Standard Practice", "Materiality calculated manually on working paper summaries."),
                new(0, "Uncalculated", "No documented materiality thresholds or benchmark justifications.")
            ],
            AutoEvaluationKey: "MATERIALITY_CALC"
        ),
        new(
            Code: "AQMM-C3",
            Section: AqmmSection.AuditExecution,
            Title: "Engagement Quality Review (EQR / EQCR) for High-Risk Audits",
            Description: "Mandatory pre-issuance quality review by an independent EQR partner for all listed entities, public interest entities, and high-risk audits under SA 220.",
            MaxPoints: 45,
            Criteria:
            [
                new(45, "Rigorous EQR", "Independent EQR partner sign-off mandatory before report release, hot reviews on workpapers, and cold reviews on completed files."),
                new(25, "Basic EQR", "EQR performed on selected audits but lacks formal review checklist."),
                new(0, "No EQR", "Reports released without independent quality reviewer concurrence.")
            ],
            AutoEvaluationKey: "EQR_REVIEWS"
        ),
        new(
            Code: "AQMM-C4",
            Section: AqmmSection.AuditExecution,
            Title: "Digital Working Papers & 60-Day Archival Lock (SA 230)",
            Description: "Standardized lead schedules (A–Q), preparer/reviewer sign-offs, immutable 60-day archival lock post report date, and SHA-256 tamper-evident integrity.",
            MaxPoints: 40,
            Criteria:
            [
                new(40, "Complete Digital Vault", "Electronic workpaper system with lead schedules A–Q, SHA-256 seal, immutable 60-day lock, and 7-year retention policy."),
                new(20, "Hybrid Documentation", "Physical files or unsealed PDF folders with manual lock logs."),
                new(0, "Unindexed Files", "Disorganized loose-leaf papers without indexing or archival controls.")
            ],
            AutoEvaluationKey: "SA230_DOSSIER"
        ),
        new(
            Code: "AQMM-C5",
            Section: AqmmSection.AuditExecution,
            Title: "Automated Analytical Audit Tools & Forensics",
            Description: "Deployment of automated software for Trial Balance ingestion, forensic bank statement analysis (Sec 40A(3), round-tripping), and analytical ratio variance checks.",
            MaxPoints: 35,
            Criteria:
            [
                new(35, "Advanced Technology", "Automated TB ingestion (Tally/Busy/SAP), automated forensic bank rules, and Schedule III ratio variance flags (>=25%)."),
                new(18, "Basic Spreadsheets", "Excel spreadsheets with manual formulas for analytical procedures."),
                new(0, "Manual Checking", "Physical vouching and tick-marks without analytical software.")
            ],
            AutoEvaluationKey: "AUTOMATED_AUDIT_TOOLS"
        ),
        new(
            Code: "AQMM-C6",
            Section: AqmmSection.AuditExecution,
            Title: "Automated External Confirmations Desk (SA 505)",
            Description: "Direct auditor control over confirmation requests for bank balances, trade receivables, trade payables, loans, and legal counsel with digital verification.",
            MaxPoints: 25,
            Criteria:
            [
                new(25, "Digital Confirmation Portal", "Tokenized digital verification links, direct third-party responses, and alternative audit procedures for non-responses under SA 505.12."),
                new(12, "Manual Letters", "Physical confirmation letters dispatched via speed post with partial response tracking."),
                new(0, "Client-Routed", "Confirmations gathered through client personnel without direct auditor control.")
            ],
            AutoEvaluationKey: "SA505_CONFIRMATIONS"
        ),
        new(
            Code: "AQMM-C7",
            Section: AqmmSection.AuditExecution,
            Title: "Consultation on Technical Accounting & Auditing Matters",
            Description: "Documented consultation protocol for contentious matters (Ind AS transitions, revenue recognition, going concern, CARO qualifications) with technical advisory cell.",
            MaxPoints: 25,
            Criteria:
            [
                new(25, "Formal Consultation", "Written technical consultation memos with internal/external subject matter experts and documented conclusions."),
                new(12, "Informal Discussions", "Verbal discussions among partners without formal memo trail."),
                new(0, "No Consultation", "Individual partners decide contentious issues in isolation.")
            ]
        ),
        new(
            Code: "AQMM-C8",
            Section: AqmmSection.AuditExecution,
            Title: "Root Cause Analysis (RCA) & Inspection Remediation",
            Description: "Annual review of peer review observations, NFRA circulars, and internal inspection queries with formal Root Cause Analysis and corrective action plans.",
            MaxPoints: 25,
            Criteria:
            [
                new(25, "Continuous Remediation", "Formal RCA conducted on audit deficiencies, corrective SOPs issued, and annual refresher training held."),
                new(12, "Basic Review", "Deficiencies noted during peer review rectified on specific files only."),
                new(0, "No RCA", "No follow-up on audit quality deficiencies.")
            ]
        ),
        new(
            Code: "AQMM-C9",
            Section: AqmmSection.AuditExecution,
            Title: "Auditor Reporting, UDIN Generation & Legal Verbiage",
            Description: "Strict compliance with SA 700/701/705/706, mandatory 18-character ICAI UDIN generation within 60 days, and Rule 11(g) accounting software audit trail verification.",
            MaxPoints: 20,
            Criteria:
            [
                new(20, "100% Compliant", "Automated SA 700 verbiage engine, Rule 11(g) edit log audit, and UDIN validation on every issued report."),
                new(10, "Manual Compilation", "Word templates modified manually with UDIN entered post-facto."),
                new(0, "Deficient Reporting", "Non-standard opinion verbiage or UDIN delays.")
            ],
            AutoEvaluationKey: "SA700_REPORTING"
        )
    ];

    public static string CalculateMaturityLevel(int totalScore)
    {
        return totalScore switch
        {
            >= 510 => AqmmMaturityLevel.Level3,
            >= 450 => AqmmMaturityLevel.Level2,
            _ => AqmmMaturityLevel.Level1
        };
    }

    public static (int Level2Gap, int Level3Gap) CalculateGaps(int totalScore)
    {
        var l2Gap = Math.Max(0, 450 - totalScore);
        var l3Gap = Math.Max(0, 510 - totalScore);
        return (l2Gap, l3Gap);
    }
}
