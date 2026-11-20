using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.SecretarialAudit;

public static class SecretarialAuditConstants
{
    // Company types
    public const string CompanyTypeListedPublic = "ListedPublic";
    public const string CompanyTypeUnlistedPublic = "UnlistedPublic";
    public const string CompanyTypePrivate = "PrivateCompany";

    // Thresholds under Section 204 and Rule 9 (amended)
    public const decimal PublicPaidUpCapitalThreshold = 50_00_00_000m; // ₹50 Crore
    public const decimal PublicTurnoverThreshold = 250_00_00_000m;      // ₹250 Crore
    public const decimal BorrowingsThreshold = 100_00_00_000m;          // ₹100 Crore (banks / PFIs)

    // Audit Statuses
    public const string StatusDraft = "Draft";
    public const string StatusFieldworkInProgress = "FieldworkInProgress";
    public const string StatusReviewPending = "ReviewPending";
    public const string StatusApproved = "Approved";
    public const string StatusCertified = "Certified";

    // Statute categories
    public const string StatuteCompaniesAct = "CompaniesAct2013";
    public const string StatuteScra = "Scra1956";
    public const string StatuteDepositories = "DepositoriesAct1996";
    public const string StatuteFema = "Fema1999";
    public const string StatuteSebiLodr = "SebiLodr2015";
    public const string StatuteSebiSast = "SebiSast2011";
    public const string StatuteSebiPit = "SebiPit2015";
    public const string StatuteSebiIcdr = "SebiIcdr2018";
    public const string StatuteOtherLaws = "IndustrySpecificLaws";

    // Compliance statuses
    public const string ComplianceComplied = "Complied";
    public const string ComplianceNotComplied = "NotComplied";
    public const string CompliancePartiallyComplied = "PartiallyComplied";
    public const string ComplianceNotApplicable = "NotApplicable";

    // Risk levels
    public const string RiskNone = "None";
    public const string RiskLow = "Low";
    public const string RiskMedium = "Medium";
    public const string RiskHigh = "High";
    public const string RiskCritical = "Critical";

    // Filing statuses
    public const string FilingOnTime = "FiledOnTime";
    public const string FilingDelayed = "FiledWithDelay";
    public const string FilingPending = "NotFiled";
    public const string FilingExempt = "Exempt";

    // Meeting Types
    public const string MeetingBoard = "BoardMeeting";
    public const string MeetingAuditCommittee = "AuditCommittee";
    public const string MeetingNrc = "NominationRemunerationCommittee";
    public const string MeetingSrc = "StakeholdersRelationshipCommittee";
    public const string MeetingCsr = "CsrCommittee";
    public const string MeetingAgm = "AnnualGeneralMeeting";
    public const string MeetingEgm = "ExtraordinaryGeneralMeeting";
}

public class SecretarialAuditEngagement : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string EngagementReference { get; set; } = "";
    public string Title { get; set; } = "";
    public string FinancialYear { get; set; } = ""; // e.g., "2024-25"
    public DateTime AuditPeriodStart { get; set; }
    public DateTime AuditPeriodEnd { get; set; }

    // Corporate Profile
    public string Cin { get; set; } = ""; // 21 alphanumeric CIN
    public string CompanyType { get; set; } = SecretarialAuditConstants.CompanyTypeUnlistedPublic;
    public decimal PaidUpShareCapital { get; set; }
    public decimal Turnover { get; set; }
    public decimal OutstandingBorrowings { get; set; }

    // Applicability Evaluation
    public bool IsApplicable { get; set; }
    public string ApplicabilityReason { get; set; } = "";

    // Audit State & Auditor
    public string AuditStatus { get; set; } = SecretarialAuditConstants.StatusDraft;
    public string AuditorName { get; set; } = "";
    public string AuditorFirmName { get; set; } = "";
    public string MembershipType { get; set; } = "FCS"; // FCS or ACS
    public string MembershipNumber { get; set; } = "";
    public string CertificateOfPracticeNumber { get; set; } = "";
    public string PeerReviewCertificateNumber { get; set; } = "";

    // Certification & UDIN (ICSI UDIN is 17 or 18 chars)
    public string? Udin { get; set; }
    public DateTime? CertifiedAt { get; set; }
    public string? CertifiedBy { get; set; }

    // Aggregate Statistics
    public int TotalChecklistCount { get; set; }
    public int CompliedCount { get; set; }
    public int NotCompliedCount { get; set; }
    public int PartiallyCompliedCount { get; set; }
    public int NotApplicableCount { get; set; }
    public int QualificationsCount { get; set; }
    public decimal OverallComplianceScore { get; set; } // Percentage

    public int TotalFilingsAudited { get; set; }
    public int FilingsDelayedCount { get; set; }
    public int TotalMeetingsAudited { get; set; }
    public int MeetingDeficienciesCount { get; set; }

    // Generated Statutory Report
    public string? FormMR3ReportText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation collections
    public List<SecretarialChecklistItem> ChecklistItems { get; set; } = [];
    public List<McaFormFilingAudit> McaFilings { get; set; } = [];
    public List<BoardMeetingAuditRecord> MeetingAudits { get; set; } = [];
}

public class SecretarialChecklistItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public SecretarialAuditEngagement? Engagement { get; set; }

    public string StatuteCategory { get; set; } = SecretarialAuditConstants.StatuteCompaniesAct;
    public string ClauseCode { get; set; } = "";
    public string SubjectArea { get; set; } = "";
    public string StatutoryRequirement { get; set; } = "";
    public string ComplianceStatus { get; set; } = SecretarialAuditConstants.ComplianceComplied;
    public string AuditorRemarks { get; set; } = "";
    public bool IsQualification { get; set; }
    public string DeviationRisk { get; set; } = SecretarialAuditConstants.RiskNone;

    public DateTime? TargetCompletionDate { get; set; }
    public string? ActionOwner { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class McaFormFilingAudit : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public SecretarialAuditEngagement? Engagement { get; set; }

    public string FormName { get; set; } = ""; // AOC-4, MGT-7, DIR-12, CHG-1, CHG-4, DPT-3, BEN-2, MGT-14, MSME-1, PAS-3
    public string Description { get; set; } = "";
    public DateTime EventDate { get; set; }
    public int StatutoryDaysAllowed { get; set; }
    public DateTime StatutoryDueDate { get; set; }
    public DateTime? ActualFilingDate { get; set; }
    public string Srn { get; set; } = ""; // Service Request Number
    public string FilingStatus { get; set; } = SecretarialAuditConstants.FilingOnTime;
    public int DelayDays { get; set; }
    public decimal AdditionalFeePaid { get; set; }
    public string AuditorRemarks { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BoardMeetingAuditRecord : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public SecretarialAuditEngagement? Engagement { get; set; }

    public string MeetingType { get; set; } = SecretarialAuditConstants.MeetingBoard;
    public DateTime MeetingDate { get; set; }
    public DateTime? PreviousMeetingDate { get; set; }
    public int? GapFromPreviousMeetingDays { get; set; }
    public bool IsGapCompliant { get; set; } = true; // Gap <= 120 days for board meetings

    public DateTime? NoticeDispatchDate { get; set; }
    public int? NoticeDaysGiven { get; set; }
    public bool IsNoticeCompliant { get; set; } = true; // >= 7 days notice

    public int TotalMembersCount { get; set; }
    public int AttendedMembersCount { get; set; }
    public int QuorumRequiredCount { get; set; }
    public bool IsQuorumCompliant { get; set; } = true;

    public int IndependentDirectorsAttended { get; set; }
    public bool SecretarialStandardComplied { get; set; } = true; // ICSI SS-1 / SS-2
    public string AuditorRemarks { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class SecretarialAuditEngine
{
    /// <summary>
    /// Evaluates statutory applicability under Section 204(1) and amended Rule 9.
    /// </summary>
    public static (bool IsApplicable, string Reason, List<string> ThresholdsExceeded) EvaluateApplicability(
        string companyType,
        decimal paidUpCapital,
        decimal turnover,
        decimal outstandingBorrowings)
    {
        var reasons = new List<string>();

        if (string.Equals(companyType, SecretarialAuditConstants.CompanyTypeListedPublic, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Listed Company pursuant to Section 204(1)(a) of Companies Act, 2013 (Mandatory regardless of turnover/capital)");
            return (true, "Mandatory: Listed Entity under Section 204(1)(a)", reasons);
        }

        bool isPublic = string.Equals(companyType, SecretarialAuditConstants.CompanyTypeUnlistedPublic, StringComparison.OrdinalIgnoreCase);

        if (isPublic && paidUpCapital >= SecretarialAuditConstants.PublicPaidUpCapitalThreshold)
        {
            reasons.Add($"Paid-up share capital ₹{paidUpCapital:N0} meets/exceeds statutory threshold of ₹50 Crore (Rule 9(1)(a))");
        }

        if (isPublic && turnover >= SecretarialAuditConstants.PublicTurnoverThreshold)
        {
            reasons.Add($"Annual Turnover ₹{turnover:N0} meets/exceeds statutory threshold of ₹250 Crore (Rule 9(1)(b))");
        }

        if (outstandingBorrowings >= SecretarialAuditConstants.BorrowingsThreshold)
        {
            reasons.Add($"Outstanding loans/borrowings from banks/PFIs ₹{outstandingBorrowings:N0} meets/exceeds threshold of ₹100 Crore (Rule 9(1)(c))");
        }

        if (reasons.Count > 0)
        {
            return (true, string.Join("; ", reasons), reasons);
        }

        return (false, "Exempt: Does not breach Section 204 or Rule 9 criteria for mandatory Secretarial Audit", reasons);
    }

    /// <summary>
    /// Evaluates meeting compliance for Section 173(1) (120-day gap), Section 173(3) (7-day notice), and Section 174 (Quorum).
    /// </summary>
    public static void EvaluateMeetingCompliance(BoardMeetingAuditRecord m)
    {
        // 1. Gap rule: Sec 173(1) max 120 days between consecutive board meetings
        if (m.MeetingType == SecretarialAuditConstants.MeetingBoard && m.PreviousMeetingDate.HasValue)
        {
            var gap = (m.MeetingDate.Date - m.PreviousMeetingDate.Value.Date).Days;
            m.GapFromPreviousMeetingDays = gap;
            m.IsGapCompliant = gap <= 120;
        }
        else
        {
            m.IsGapCompliant = true;
        }

        // 2. Notice rule: Sec 173(3) min 7 days notice in writing
        if (m.NoticeDispatchDate.HasValue)
        {
            var noticeDays = (m.MeetingDate.Date - m.NoticeDispatchDate.Value.Date).Days;
            m.NoticeDaysGiven = noticeDays;
            m.IsNoticeCompliant = noticeDays >= 7;
        }
        else
        {
            m.IsNoticeCompliant = true;
        }

        // 3. Quorum rule: Sec 174 1/3rd of total strength or 2 directors, whichever is higher
        if (m.TotalMembersCount > 0)
        {
            var thirdOfStrength = (int)Math.Ceiling(m.TotalMembersCount / 3.0);
            var quorum = Math.Max(2, thirdOfStrength);
            m.QuorumRequiredCount = quorum;
            m.IsQuorumCompliant = m.AttendedMembersCount >= quorum;
        }
    }

    /// <summary>
    /// Evaluates MCA e-filing statutory due date and delay.
    /// </summary>
    public static void EvaluateMcaFiling(McaFormFilingAudit f)
    {
        f.StatutoryDueDate = f.EventDate.Date.AddDays(f.StatutoryDaysAllowed);

        if (!f.ActualFilingDate.HasValue)
        {
            if (DateTime.UtcNow.Date > f.StatutoryDueDate.Date)
            {
                f.FilingStatus = SecretarialAuditConstants.FilingPending;
                f.DelayDays = (DateTime.UtcNow.Date - f.StatutoryDueDate.Date).Days;
            }
            else
            {
                f.FilingStatus = SecretarialAuditConstants.FilingOnTime;
                f.DelayDays = 0;
            }
            return;
        }

        var delay = (f.ActualFilingDate.Value.Date - f.StatutoryDueDate.Date).Days;
        if (delay > 0)
        {
            f.DelayDays = delay;
            f.FilingStatus = SecretarialAuditConstants.FilingDelayed;
        }
        else
        {
            f.DelayDays = 0;
            f.FilingStatus = SecretarialAuditConstants.FilingOnTime;
        }
    }

    /// <summary>
    /// Seeds baseline checklist items covering Companies Act, SCRA, Depositories, FEMA, SEBI, and general industry laws.
    /// </summary>
    public static List<SecretarialChecklistItem> SeedBaselineChecklist(Guid engagementId, Guid firmId, bool isListed)
    {
        var items = new List<SecretarialChecklistItem>
        {
            // 1. Companies Act, 2013 - Board & Governance
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-149",
                SubjectArea = "Board Composition & Independent / Woman Directors",
                StatutoryRequirement = "Company has optimum combination of executive and non-executive directors with at least one woman director (Sec 149(1)) and required independent directors (Sec 149(4)).",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Verified MCA DIN records and Board Register.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-173",
                SubjectArea = "Board Meetings & 120-Day Consecutive Gap Rule",
                StatutoryRequirement = "Holding of minimum 4 board meetings in the financial year with no more than 120 days interval between consecutive meetings under Section 173(1).",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Examined board minutes and attendance book.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-174",
                SubjectArea = "Quorum of Board Meetings",
                StatutoryRequirement = "Quorum of 1/3rd of total strength or two directors, whichever is higher, met at every meeting (Section 174).",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Quorum verified against signed attendance rolls.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-177",
                SubjectArea = "Audit Committee Constitution & Terms of Reference",
                StatutoryRequirement = "Audit Committee properly constituted with minimum 3 directors, majority independent, with independent directors having financial literacy.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Charter and meeting minutes reviewed.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-178",
                SubjectArea = "NRC & Stakeholders Relationship Committee",
                StatutoryRequirement = "Nomination & Remuneration Committee and Stakeholders Relationship Committee constituted as per Section 178.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "NRC policy on director appointment reviewed.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-180",
                SubjectArea = "Restrictions on Board Powers & Borrowings Threshold",
                StatutoryRequirement = "Borrowings within Section 180(1)(c) limits (paid-up capital + free reserves + securities premium) or covered by Special Resolution in General Meeting.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Inspected sanction letters and general meeting resolutions.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-185-186",
                SubjectArea = "Loans to Directors & Corporate Investments/Guarantees",
                StatutoryRequirement = "Compliance with Section 185 (prohibition on loans to directors) and Section 186 (60% paid-up capital/free reserves or 100% free reserves limits) and Form MBP-2 register.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "MBP-2 register inspected and cross-verified with audited financials.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-188",
                SubjectArea = "Related Party Transactions & Audit Committee Approvals",
                StatutoryRequirement = "RPTs in ordinary course of business at arm's length or approved by Audit Committee / Board / General Meeting as applicable.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Form MBP-4 register and omnibus approvals checked.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-135",
                SubjectArea = "Corporate Social Responsibility (CSR) 2% Obligation",
                StatutoryRequirement = "CSR Committee constitution, annual action plan approval, and 2% average net profit expenditure or transfer to Schedule VII fund.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "CSR report annexed to Board report verified.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                ClauseCode = "SEC-134",
                SubjectArea = "Board's Report Mandatory Disclosures",
                StatutoryRequirement = "Directors' Responsibility Statement, secretarial audit report annexure, IFC statements, and related party disclosures included in Board Report.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Draft Board's report reviewed for compliance.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },

            // 2. Depositories Act & Securities Transfer
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteDepositories,
                ClauseCode = "DEP-RULE-9A-9B",
                SubjectArea = "Mandatory Dematerialization of Securities (PAS Rules)",
                StatutoryRequirement = "All securities of the company held by promoters, directors, and KMPs are in dematerialized form as per Rule 9A/9B of Companies (Prospectus and Allotment) Rules.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "ISIN allotment confirmed with NSDL/CDSL.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },

            // 3. Foreign Exchange Management Act, 1999 (FEMA)
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteFema,
                ClauseCode = "FEMA-FCGPR",
                SubjectArea = "Foreign Direct Investment (FDI) & Form FC-GPR Reporting",
                StatutoryRequirement = "Reporting of FDI share allotments to RBI within 30 days of allotment via FIRMS portal (Form FC-GPR).",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "FDI reporting acknowledgments on FIRMS portal examined.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteFema,
                ClauseCode = "FEMA-FLA",
                SubjectArea = "Annual Return on Foreign Liabilities and Assets (FLA)",
                StatutoryRequirement = "Filing of annual return on Foreign Liabilities and Assets (FLA) on or before 15th July with Reserve Bank of India.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "FLIRMS acknowledgment checked.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteFema,
                ClauseCode = "FEMA-ECB",
                SubjectArea = "External Commercial Borrowings (ECB) & Monthly Form ECB-2",
                StatutoryRequirement = "Compliance with ECB framework parameters and monthly filing of Form ECB-2 return through AD Bank to RBI.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceNotApplicable,
                AuditorRemarks = "Company has not availed any ECB during the audit period.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },

            // 4. Industry Specific & Labor/Environment Laws
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteOtherLaws,
                ClauseCode = "IND-LABOR-01",
                SubjectArea = "Labor & Social Security Statutes (EPF, ESI, Gratuity)",
                StatutoryRequirement = "Timely monthly deposit of statutory contributions under EPF Act, ESI Act, and Payment of Gratuity Act.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Challan proofs and compliance certificates reviewed.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                StatuteCategory = SecretarialAuditConstants.StatuteOtherLaws,
                ClauseCode = "IND-ENV-01",
                SubjectArea = "Environmental Protection & Pollution Control Consents",
                StatutoryRequirement = "Valid Consent to Operate (CTO) / Consent to Establish (CTE) from State Pollution Control Board where applicable.",
                ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                AuditorRemarks = "Pollution control board consent valid through 2026.",
                DeviationRisk = SecretarialAuditConstants.RiskNone
            }
        };

        // SEBI Laws for Listed Entities
        if (isListed)
        {
            items.AddRange(new[]
            {
                new SecretarialChecklistItem
                {
                    FirmId = firmId,
                    EngagementId = engagementId,
                    StatuteCategory = SecretarialAuditConstants.StatuteSebiLodr,
                    ClauseCode = "SEBI-LODR-17",
                    SubjectArea = "LODR Regulation 17 - Board Structure & Minimum Independent Directors",
                    StatutoryRequirement = "At least 50% of the Board consists of Non-Executive Directors and at least 1/3rd (or 50% if Chairman is Executive) are Independent Directors.",
                    ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                    AuditorRemarks = "Board composition verified against SEBI LODR Regulation 17 criteria.",
                    DeviationRisk = SecretarialAuditConstants.RiskNone
                },
                new SecretarialChecklistItem
                {
                    FirmId = firmId,
                    EngagementId = engagementId,
                    StatuteCategory = SecretarialAuditConstants.StatuteSebiLodr,
                    ClauseCode = "SEBI-LODR-30",
                    SubjectArea = "LODR Regulation 30 - Material Event Disclosures to Stock Exchanges",
                    StatutoryRequirement = "Disclosure of events/information to stock exchanges within 30 minutes of board meeting closure or 24 hours of occurrence.",
                    ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                    AuditorRemarks = "BSE/NSE announcements verified for prompt disclosure.",
                    DeviationRisk = SecretarialAuditConstants.RiskNone
                },
                new SecretarialChecklistItem
                {
                    FirmId = firmId,
                    EngagementId = engagementId,
                    StatuteCategory = SecretarialAuditConstants.StatuteSebiPit,
                    ClauseCode = "SEBI-PIT-3-5",
                    SubjectArea = "SEBI Insider Trading - Structured Digital Database (SDD)",
                    StatutoryRequirement = "Maintenance of digital database (SDD) with timestamps and audit trail containing details of Unpublished Price Sensitive Information (UPSI).",
                    ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                    AuditorRemarks = "Inspected automated SDD software logs and quarterly audit certificate.",
                    DeviationRisk = SecretarialAuditConstants.RiskNone
                },
                new SecretarialChecklistItem
                {
                    FirmId = firmId,
                    EngagementId = engagementId,
                    StatuteCategory = SecretarialAuditConstants.StatuteSebiSast,
                    ClauseCode = "SEBI-SAST-29-30",
                    SubjectArea = "SEBI Takeover Regulations - Continual & Event Disclosures",
                    StatutoryRequirement = "Disclosures under Regulation 29 and Regulation 30/31 regarding acquisition/pledge of shares by promoters within statutory 2 days.",
                    ComplianceStatus = SecretarialAuditConstants.ComplianceComplied,
                    AuditorRemarks = "Cross-checked with depository data and exchange filings.",
                    DeviationRisk = SecretarialAuditConstants.RiskNone
                }
            });
        }

        return items;
    }

    /// <summary>
    /// Computes summary numbers and compliance scores.
    /// </summary>
    public static void RecalculateSummary(SecretarialAuditEngagement eng)
    {
        eng.TotalChecklistCount = eng.ChecklistItems.Count;
        eng.CompliedCount = eng.ChecklistItems.Count(c => c.ComplianceStatus == SecretarialAuditConstants.ComplianceComplied);
        eng.NotCompliedCount = eng.ChecklistItems.Count(c => c.ComplianceStatus == SecretarialAuditConstants.ComplianceNotComplied);
        eng.PartiallyCompliedCount = eng.ChecklistItems.Count(c => c.ComplianceStatus == SecretarialAuditConstants.CompliancePartiallyComplied);
        eng.NotApplicableCount = eng.ChecklistItems.Count(c => c.ComplianceStatus == SecretarialAuditConstants.ComplianceNotApplicable);
        eng.QualificationsCount = eng.ChecklistItems.Count(c => c.IsQualification || c.ComplianceStatus == SecretarialAuditConstants.ComplianceNotComplied);

        int applicableCount = eng.TotalChecklistCount - eng.NotApplicableCount;
        if (applicableCount > 0)
        {
            eng.OverallComplianceScore = Math.Round((decimal)eng.CompliedCount / applicableCount * 100m, 2);
        }
        else
        {
            eng.OverallComplianceScore = 100m;
        }

        eng.TotalFilingsAudited = eng.McaFilings.Count;
        eng.FilingsDelayedCount = eng.McaFilings.Count(f => f.FilingStatus == SecretarialAuditConstants.FilingDelayed || f.FilingStatus == SecretarialAuditConstants.FilingPending);

        eng.TotalMeetingsAudited = eng.MeetingAudits.Count;
        eng.MeetingDeficienciesCount = eng.MeetingAudits.Count(m => !m.IsGapCompliant || !m.IsNoticeCompliant || !m.IsQuorumCompliant);
    }

    /// <summary>
    /// Generates standard Form MR-3 Secretarial Audit Report formatted per MCA rules.
    /// </summary>
    public static string GenerateFormMR3(
        SecretarialAuditEngagement eng,
        string companyName,
        string registeredOffice)
    {
        var sb = new StringBuilder();
        var qualifications = eng.ChecklistItems
            .Where(c => c.IsQualification || c.ComplianceStatus == SecretarialAuditConstants.ComplianceNotComplied || c.ComplianceStatus == SecretarialAuditConstants.CompliancePartiallyComplied)
            .ToList();

        sb.AppendLine("================================================================================");
        sb.AppendLine("                               FORM NO. MR-3");
        sb.AppendLine("                         SECRETARIAL AUDIT REPORT");
        sb.AppendLine("   FOR THE FINANCIAL YEAR ENDED 31ST MARCH, " + (eng.FinancialYear.Split('-').LastOrDefault() ?? "2025"));
        sb.AppendLine(" [Pursuant to Section 204(1) of the Companies Act, 2013 and Rule No. 9 of the");
        sb.AppendLine("  Companies (Appointment and Remuneration of Managerial Personnel) Rules, 2014]");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine($"To,");
        sb.AppendLine($"The Members,");
        sb.AppendLine($"{companyName}");
        sb.AppendLine($"CIN: {eng.Cin}");
        sb.AppendLine($"Address: {registeredOffice}");
        sb.AppendLine();
        sb.AppendLine($"I/We have conducted the secretarial audit of the compliance of applicable statutory provisions");
        sb.AppendLine($"and the adherence to good corporate practices by {companyName} (hereinafter called 'the Company').");
        sb.AppendLine($"Secretarial Audit was conducted in a manner that provided me/us a reasonable basis for evaluating");
        sb.AppendLine($"the corporate conducts/statutory compliances and expressing my/our opinion thereon.");
        sb.AppendLine();
        sb.AppendLine($"Based on my/our verification of the Company's books, papers, minute books, forms and returns filed");
        sb.AppendLine($"and other records maintained by the Company and also the information provided by the Company, its");
        sb.AppendLine($"officers, agents and authorized representatives during the conduct of secretarial audit, I/We hereby");
        sb.AppendLine($"report that in my/our opinion, the company has, during the audit period covering the financial year");
        sb.AppendLine($"ended on 31st March, {eng.FinancialYear.Split('-').LastOrDefault() ?? "2025"} complied with the statutory provisions listed hereunder:");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("I. SPECIFIC STATUTES EXAMINED & REPORTED:");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("  (i)   The Companies Act, 2013 (the Act) and the rules made thereunder;");
        sb.AppendLine("  (ii)  The Securities Contracts (Regulation) Act, 1956 ('SCRA') and the rules made thereunder;");
        sb.AppendLine("  (iii) The Depositories Act, 1996 and the Regulations and Bye-laws framed thereunder;");
        sb.AppendLine("  (iv)  Foreign Exchange Management Act, 1999 (FEMA) and the rules and regulations made");
        sb.AppendLine("        thereunder to the extent of Foreign Direct Investment, Overseas Direct Investment");
        sb.AppendLine("        and External Commercial Borrowings;");
        if (eng.CompanyType == SecretarialAuditConstants.CompanyTypeListedPublic)
        {
            sb.AppendLine("  (v)   The Regulations and Guidelines prescribed under the Securities and Exchange Board");
            sb.AppendLine("        of India Act, 1992 ('SEBI Act') viz.:");
            sb.AppendLine("        (a) The SEBI (Listing Obligations and Disclosure Requirements) Regulations, 2015;");
            sb.AppendLine("        (b) The SEBI (Prohibition of Insider Trading) Regulations, 2015;");
            sb.AppendLine("        (c) The SEBI (Substantial Acquisition of Shares and Takeovers) Regulations, 2011;");
            sb.AppendLine("        (d) The SEBI (Issue of Capital and Disclosure Requirements) Regulations, 2018;");
        }
        else
        {
            sb.AppendLine("  (v)   SEBI Regulations (Not Applicable as the Company is an Unlisted Entity during the period);");
        }
        sb.AppendLine("  (vi)  Other sector-specific laws and labor/environmental regulations applicable to the Company.");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("II. OBSERVATIONS, RESERVATIONS, ADVERSE REMARKS & QUALIFICATIONS:");
        sb.AppendLine("--------------------------------------------------------------------------------");

        if (qualifications.Count == 0 && eng.FilingsDelayedCount == 0 && eng.MeetingDeficienciesCount == 0)
        {
            sb.AppendLine("During the period under review, the Company has complied with the provisions of the Act, Rules,");
            sb.AppendLine("Regulations, Guidelines, Standards, etc. mentioned above and no qualifications, reservations,");
            sb.AppendLine("or adverse remarks were observed.");
        }
        else
        {
            sb.AppendLine("I/We report that during the period under review, the following observations/qualifications were noted:");
            int idx = 1;
            foreach (var q in qualifications)
            {
                sb.AppendLine($"  {idx++}. [{q.StatuteCategory} - {q.ClauseCode}] {q.SubjectArea}:");
                sb.AppendLine($"     Requirement: {q.StatutoryRequirement}");
                sb.AppendLine($"     Status: {q.ComplianceStatus} | Risk: {q.DeviationRisk}");
                sb.AppendLine($"     Auditor Remark: {q.AuditorRemarks}");
                sb.AppendLine();
            }

            if (eng.FilingsDelayedCount > 0)
            {
                sb.AppendLine($"  {idx++}. Delay in MCA E-Filings:");
                sb.AppendLine($"     The Company experienced delayed filing in {eng.FilingsDelayedCount} statutory e-form(s),");
                sb.AppendLine($"     which were subsequently regularized with additional statutory fees under the Companies Act, 2013.");
                sb.AppendLine();
            }

            if (eng.MeetingDeficienciesCount > 0)
            {
                sb.AppendLine($"  {idx++}. Board/Committee Meeting Irregularities:");
                sb.AppendLine($"     The Company recorded {eng.MeetingDeficienciesCount} meeting deficiency(ies) regarding notice period,");
                sb.AppendLine($"     inter-meeting gap, or quorum formalities, which have been brought to the attention of management.");
                sb.AppendLine();
            }
        }

        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("III. BOARD PROCESSES & GOVERNANCE MECHANISMS:");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"I/We further report that:");
        sb.AppendLine($"- The Board of Directors of the Company is duly constituted with proper balance of Executive Directors,");
        sb.AppendLine($"  Non-Executive Directors, and Independent Directors.");
        sb.AppendLine($"- Adequate notice is given to all directors to schedule Board Meetings, agenda and detailed notes");
        sb.AppendLine($"  on agenda were sent at least seven days in advance (or with proper shorter notice consent).");
        sb.AppendLine($"- A system exists for seeking and obtaining further information and clarifications on the agenda");
        sb.AppendLine($"  items before the meeting and for meaningful participation at the meeting.");
        sb.AppendLine($"- All decisions at Board Meetings and Committee Meetings were carried out unanimously/with requisite");
        sb.AppendLine($"  majority as recorded in the minutes of the meetings.");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("IV. INTERNAL FINANCIAL CONTROLS & COMPLIANCE SYSTEMS:");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"I/We further report that there are adequate systems and processes in the company commensurate with");
        sb.AppendLine($"the size and operations of the company to monitor and ensure compliance with applicable laws, rules,");
        sb.AppendLine($"regulations and guidelines.");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("CERTIFICATION & SIGN-OFF:");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"For and on behalf of:");
        sb.AppendLine($"Firm: {eng.AuditorFirmName}");
        sb.AppendLine($"Practicing Company Secretary / Auditor: {eng.AuditorName}");
        sb.AppendLine($"Membership: {eng.MembershipType} No.: {eng.MembershipNumber}");
        sb.AppendLine($"CP No.: {eng.CertificateOfPracticeNumber}");
        sb.AppendLine($"Peer Review Cert No.: {eng.PeerReviewCertificateNumber}");
        sb.AppendLine($"UDIN: {eng.Udin ?? "UNASSIGNED - PENDING CERTIFICATION"}");
        sb.AppendLine($"Date: {(eng.CertifiedAt.HasValue ? eng.CertifiedAt.Value.ToString("dd-MM-yyyy") : DateTime.UtcNow.ToString("dd-MM-yyyy"))}");
        sb.AppendLine($"Place: New Delhi");
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }
}
