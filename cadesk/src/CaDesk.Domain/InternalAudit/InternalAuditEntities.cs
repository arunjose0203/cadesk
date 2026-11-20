using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.InternalAudit;

public class InternalAuditEngagement : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = "2024-25";
    public string AuditPeriod { get; set; } = "Annual"; // Q1, Q2, Q3, Q4, H1, H2, Annual
    public string Title { get; set; } = string.Empty;
    public string EngagementLeadName { get; set; } = string.Empty;
    public string AuditScopeDescription { get; set; } = string.Empty;
    public string Status { get; set; } = "Planning"; // Planning, Fieldwork, Review, Reporting, Closed
    public DateTime? ReportDate { get; set; }
    public decimal HealthScorePercentage { get; set; }
    public string? GeneratedReportText { get; set; }

    public List<RcmControlItem> Controls { get; set; } = [];
    public List<InternalAuditFinding> Findings { get; set; } = [];
}

public class RcmControlItem : TenantEntity
{
    public Guid EngagementId { get; set; }
    public InternalAuditEngagement? Engagement { get; set; }

    public string ControlCode { get; set; } = string.Empty; // e.g. P2P-CTL-01
    public string ProcessCycle { get; set; } = "P2P"; // P2P, O2C, H2R, R2R, Treasury, ITGC, InventoryFixedAssets
    public string SubProcess { get; set; } = string.Empty;
    public string RiskDescription { get; set; } = string.Empty;
    public string RiskCategory { get; set; } = "FinancialReporting"; // FinancialReporting, Operational, Compliance, Fraud, IT
    public string InherentRiskRating { get; set; } = "High"; // High, Medium, Low
    public string ControlActivity { get; set; } = string.Empty;
    public string ControlNature { get; set; } = "Preventive"; // Preventive, Detective, Corrective
    public string ControlFrequency { get; set; } = "Monthly"; // Transactional, Daily, Weekly, Monthly, Quarterly, Annual, AdHoc
    public string ControlType { get; set; } = "Automated"; // Automated, SemiAutomated, Manual
    public string ControlOwner { get; set; } = string.Empty;
    public string Assertions { get; set; } = "Completeness, Accuracy";

    // Design Evaluation (Walkthrough)
    public bool WalkthroughPerformed { get; set; }
    public DateTime? WalkthroughDate { get; set; }
    public string WalkthroughReference { get; set; } = string.Empty;
    public string DesignEffectiveness { get; set; } = "NotTested"; // Effective, Deficient, NotTested
    public string DesignNotes { get; set; } = string.Empty;

    // Test of Operating Effectiveness (ToC)
    public string TestingMethod { get; set; } = "Inspection"; // Inspection, Reperformance, Observation, Inquiry
    public int SampleSize { get; set; }
    public int ExceptionsCount { get; set; }
    public string OperatingEffectiveness { get; set; } = "NotTested"; // OperatingEffectively, Ineffective, NotTested
    public string TestingNotes { get; set; } = string.Empty;
    public DateTime? TestedAt { get; set; }
    public string TestedBy { get; set; } = string.Empty;

    public string ResidualRiskRating { get; set; } = "Medium"; // High, Medium, Low
    public bool HasFinding { get; set; }
}

public class InternalAuditFinding : TenantEntity
{
    public Guid EngagementId { get; set; }
    public InternalAuditEngagement? Engagement { get; set; }

    public Guid? ControlItemId { get; set; }
    public string ControlCode { get; set; } = string.Empty;
    public string FindingReference { get; set; } = string.Empty; // e.g. IA-FIND-2024-001
    public string Title { get; set; } = string.Empty;
    public string ProcessCycle { get; set; } = "P2P";
    public string Severity { get; set; } = "High"; // Critical, High, Medium, Low
    public string DetailedObservation { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public decimal? EstimatedRiskAmount { get; set; } // INR
    public string IfcClassification { get; set; } = "SignificantDeficiency"; // MaterialWeakness, SignificantDeficiency, OperationalDeficiency, ProcessImprovement
    public string AuditorRecommendation { get; set; } = string.Empty;
    public string ManagementActionPlan { get; set; } = string.Empty;
    public string ActionOwner { get; set; } = string.Empty;
    public DateTime? TargetCompletionDate { get; set; }
    public string ImplementationStatus { get; set; } = "Open"; // Open, InProgress, Implemented, Overdue
}

public static class InternalAuditEngine
{
    public static void EvaluateResidualRisk(RcmControlItem c)
    {
        bool isEffectiveOperating = (c.OperatingEffectiveness == "Effective" || c.OperatingEffectiveness == "OperatingEffectively") && c.ExceptionsCount == 0;
        bool isDeficient = c.DesignEffectiveness == "Deficient" || c.OperatingEffectiveness == "Ineffective" || c.ExceptionsCount > 0;

        // 1. If design is deficient or operating effectiveness is ineffective or exceptions found, residual risk is High
        if (isDeficient)
        {
            c.ResidualRiskRating = "High";
            c.HasFinding = true;
        }
        else if (c.DesignEffectiveness == "Effective" && isEffectiveOperating)
        {
            // Fully effective control mitigates inherent risk
            c.ResidualRiskRating = "Low";
            c.HasFinding = false;
        }
        else
        {
            // Untested or partial
            c.ResidualRiskRating = c.InherentRiskRating;
            c.HasFinding = false;
        }
    }

    public static decimal CalculateHealthScore(List<RcmControlItem> controls)
    {
        var tested = controls.Where(x => x.OperatingEffectiveness != "NotTested").ToList();
        if (tested.Count == 0) return 100.0m;

        int effectiveCount = tested.Count(x => (x.OperatingEffectiveness == "Effective" || x.OperatingEffectiveness == "OperatingEffectively") && x.ExceptionsCount == 0);
        return Math.Round(((decimal)effectiveCount / tested.Count) * 100m, 2);
    }

    public static List<RcmControlItem> GenerateStandardRcmTemplate(Guid firmId, Guid engagementId)
    {
        return
        [
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                ControlCode = "P2P-CTL-01",
                ProcessCycle = "P2P",
                SubProcess = "Purchase Order & Three-Way Matching",
                RiskDescription = "Invoices paid without verified delivery or at unapproved prices, leading to unauthorized cash outflow.",
                RiskCategory = "FinancialReporting",
                InherentRiskRating = "High",
                ControlActivity = "ERP automated 3-way match verifying approved PO price, Goods Receipt Note (GRN) quantity, and Vendor Invoice prior to voucher posting.",
                ControlNature = "Preventive",
                ControlFrequency = "Transactional",
                ControlType = "Automated",
                ControlOwner = "Accounts Payable Team Lead",
                Assertions = "Accuracy, Occurrence, Completeness",
                WalkthroughPerformed = true,
                WalkthroughDate = DateTime.UtcNow.AddDays(-10),
                DesignEffectiveness = "Effective",
                TestingMethod = "Inspection",
                SampleSize = 25,
                ExceptionsCount = 0,
                OperatingEffectiveness = "OperatingEffectively",
                ResidualRiskRating = "Low"
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                ControlCode = "O2C-CTL-01",
                ProcessCycle = "O2C",
                SubProcess = "Customer Credit Approval & Order Release",
                RiskDescription = "Sales orders released to customers exceeding sanctioned credit limits or overdue terms, resulting in bad debts.",
                RiskCategory = "Operational",
                InherentRiskRating = "High",
                ControlActivity = "System blocks automated order dispatch if customer outstanding balance exceeds credit limit, requiring CFO manual override.",
                ControlNature = "Preventive",
                ControlFrequency = "Transactional",
                ControlType = "Automated",
                ControlOwner = "Credit Control Manager",
                Assertions = "Valuation, Rights & Obligations",
                WalkthroughPerformed = true,
                WalkthroughDate = DateTime.UtcNow.AddDays(-10),
                DesignEffectiveness = "Effective",
                TestingMethod = "Reperformance",
                SampleSize = 25,
                ExceptionsCount = 0,
                OperatingEffectiveness = "OperatingEffectively",
                ResidualRiskRating = "Low"
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                ControlCode = "H2R-CTL-01",
                ProcessCycle = "H2R",
                SubProcess = "Payroll Master & Biometric Reconciliation",
                RiskDescription = "Ghost employees or incorrect attendance leading to unauthorized salary disbursements and statutory non-compliance.",
                RiskCategory = "Fraud",
                InherentRiskRating = "High",
                ControlActivity = "Monthly payroll variance review and biometric attendance reconciliation signed off by HR Head and Plant Manager prior to bank transfer.",
                ControlNature = "Detective",
                ControlFrequency = "Monthly",
                ControlType = "SemiAutomated",
                ControlOwner = "HR & Payroll Head",
                Assertions = "Occurrence, Completeness, Accuracy",
                WalkthroughPerformed = true,
                WalkthroughDate = DateTime.UtcNow.AddDays(-8),
                DesignEffectiveness = "Effective",
                TestingMethod = "Inspection",
                SampleSize = 12,
                ExceptionsCount = 0,
                OperatingEffectiveness = "OperatingEffectively",
                ResidualRiskRating = "Low"
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                ControlCode = "R2R-CTL-01",
                ProcessCycle = "R2R",
                SubProcess = "Manual Journal Entries & Period-End Closing",
                RiskDescription = "Unauthorized manual journal entries booked to manipulate financial statements or bypass controls.",
                RiskCategory = "FinancialReporting",
                InherentRiskRating = "High",
                ControlActivity = "Dual authorization for all manual journal entries above ₹50,000 by Financial Controller with mandatory supporting document upload.",
                ControlNature = "Preventive",
                ControlFrequency = "Monthly",
                ControlType = "SemiAutomated",
                ControlOwner = "Financial Controller",
                Assertions = "Occurrence, Measurement, Cut-off",
                WalkthroughPerformed = true,
                WalkthroughDate = DateTime.UtcNow.AddDays(-7),
                DesignEffectiveness = "Effective",
                TestingMethod = "Inspection",
                SampleSize = 25,
                ExceptionsCount = 0,
                OperatingEffectiveness = "OperatingEffectively",
                ResidualRiskRating = "Low"
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                ControlCode = "TR-CTL-01",
                ProcessCycle = "Treasury",
                SubProcess = "Bank Reconciliation Statement (BRS)",
                RiskDescription = "Unreconciled bank differences and unrecorded banking transactions leading to misstated cash balances or fraud.",
                RiskCategory = "FinancialReporting",
                InherentRiskRating = "High",
                ControlActivity = "Monthly Bank Reconciliation Statement prepared within 7 days of month-end and reviewed/approved by Finance Director.",
                ControlNature = "Detective",
                ControlFrequency = "Monthly",
                ControlType = "Manual",
                ControlOwner = "Treasury Manager",
                Assertions = "Completeness, Existence",
                WalkthroughPerformed = true,
                WalkthroughDate = DateTime.UtcNow.AddDays(-6),
                DesignEffectiveness = "Effective",
                TestingMethod = "Inspection",
                SampleSize = 12,
                ExceptionsCount = 0,
                OperatingEffectiveness = "OperatingEffectively",
                ResidualRiskRating = "Low"
            },
            new()
            {
                FirmId = firmId,
                EngagementId = engagementId,
                ControlCode = "ITGC-CTL-01",
                ProcessCycle = "ITGC",
                SubProcess = "User Access Management & Segregation of Duties",
                RiskDescription = "Incompatible access rights and lack of Segregation of Duties (SoD) permitting unauthorized master or transaction changes.",
                RiskCategory = "IT",
                InherentRiskRating = "High",
                ControlActivity = "Quarterly user access rights review and conflict matrix certification by IT Security Officer and Process Owners.",
                ControlNature = "Preventive",
                ControlFrequency = "Quarterly",
                ControlType = "SemiAutomated",
                ControlOwner = "IT Head / CISO",
                Assertions = "Rights & Obligations, Accuracy",
                WalkthroughPerformed = true,
                WalkthroughDate = DateTime.UtcNow.AddDays(-5),
                DesignEffectiveness = "Effective",
                TestingMethod = "Inspection",
                SampleSize = 4,
                ExceptionsCount = 0,
                OperatingEffectiveness = "OperatingEffectively",
                ResidualRiskRating = "Low"
            }
        ];
    }

    public static string GenerateExecutiveReport(InternalAuditEngagement engagement, string companyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================================================================");
        sb.AppendLine("                INTERNAL AUDIT & INTERNAL FINANCIAL CONTROLS (IFC) EXECUTIVE REPORT                 ");
        sb.AppendLine("           [Conducted in accordance with ICAI Standards on Internal Audit (SIA) & Sec 138]          ");
        sb.AppendLine("====================================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Entity Name        : {companyName}");
        sb.AppendLine($"Financial Year     : {engagement.FinancialYear} (Audit Period: {engagement.AuditPeriod})");
        sb.AppendLine($"Engagement Title   : {engagement.Title}");
        sb.AppendLine($"Engagement Lead    : {engagement.EngagementLeadName}");
        sb.AppendLine($"Report Date        : {engagement.ReportDate?.ToString("dd-MMM-yyyy") ?? DateTime.UtcNow.ToString("dd-MMM-yyyy")}");
        sb.AppendLine($"Overall IFC Health : {engagement.HealthScorePercentage:F2}% Controls Operating Effectively");
        sb.AppendLine();
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("1. EXECUTIVE SUMMARY & MATURITY ASSESSMENT");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine($"We have completed the internal audit and testing of Internal Financial Controls Over Financial");
        sb.AppendLine($"Reporting (IFC-FR) of {companyName} for {engagement.FinancialYear} pursuant to Section 138 & 143(3)(i).");
        sb.AppendLine();
        sb.AppendLine($"Total Controls in RCM    : {engagement.Controls.Count}");
        int effective = engagement.Controls.Count(x => (x.OperatingEffectiveness == "Effective" || x.OperatingEffectiveness == "OperatingEffectively") && x.ExceptionsCount == 0);
        int ineffective = engagement.Controls.Count(x => x.OperatingEffectiveness == "Ineffective" || x.ExceptionsCount > 0);
        int untested = engagement.Controls.Count(x => x.OperatingEffectiveness == "NotTested");
        sb.AppendLine($"Operating Effectively    : {effective}");
        sb.AppendLine($"Deficient / Ineffective  : {ineffective}");
        sb.AppendLine($"Untested / In Progress   : {untested}");
        sb.AppendLine($"Total Deficiencies Found : {engagement.Findings.Count}");
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("2. CYCLE-WISE CONTROL EFFECTIVENESS MATRIX");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,8} | {2,10} | {3,12} | {4,12}",
            "Process Cycle", "Controls", "Effective", "Ineffective", "Health Score"));
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        var cycleGroups = engagement.Controls.GroupBy(x => x.ProcessCycle).OrderBy(g => g.Key);
        foreach (var cg in cycleGroups)
        {
            int totalInCycle = cg.Count();
            int eff = cg.Count(x => (x.OperatingEffectiveness == "Effective" || x.OperatingEffectiveness == "OperatingEffectively") && x.ExceptionsCount == 0);
            int ineff = cg.Count(x => x.OperatingEffectiveness == "Ineffective" || x.ExceptionsCount > 0);
            decimal pct = totalInCycle > 0 ? Math.Round(((decimal)eff / totalInCycle) * 100m, 1) : 0m;

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} | {1,8} | {2,10} | {3,12} | {4,11:F1}%",
                cg.Key, totalInCycle, eff, ineff, pct));
        }

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("3. SCHEDULE OF INTERNAL AUDIT FINDINGS & MANAGEMENT ACTION PLANS");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        if (engagement.Findings.Count == 0)
        {
            sb.AppendLine("No significant internal control deficiencies or material weaknesses identified during the audit period.");
        }
        else
        {
            foreach (var f in engagement.Findings.OrderBy(x => x.Severity == "Critical" ? 1 : x.Severity == "High" ? 2 : 3))
            {
                sb.AppendLine($"Finding Ref       : {f.FindingReference} - {f.Title}");
                sb.AppendLine($"Control Reference : {f.ControlCode} ({f.ProcessCycle})");
                sb.AppendLine($"Severity / Class  : {f.Severity} [{f.IfcClassification}]");
                if (f.EstimatedRiskAmount.HasValue && f.EstimatedRiskAmount.Value > 0)
                {
                    sb.AppendLine($"Financial Exposure: ₹{f.EstimatedRiskAmount.Value.ToString("N2", CultureInfo.InvariantCulture)}");
                }
                sb.AppendLine($"Detailed Finding  : {f.DetailedObservation}");
                sb.AppendLine($"Root Cause        : {f.RootCause}");
                sb.AppendLine($"Recommendation    : {f.AuditorRecommendation}");
                sb.AppendLine($"Management Plan   : {f.ManagementActionPlan}");
                sb.AppendLine($"Action Owner      : {f.ActionOwner} (Target Date: {f.TargetCompletionDate?.ToString("dd-MMM-yyyy") ?? "TBD"})");
                sb.AppendLine($"Status            : {f.ImplementationStatus}");
                sb.AppendLine("----------------------------------------------------------------------------------------------------");
            }
        }

        sb.AppendLine();
        sb.AppendLine("====================================================================================================");
        sb.AppendLine("                                      END OF EXECUTIVE REPORT                                       ");
        sb.AppendLine("====================================================================================================");

        return sb.ToString();
    }
}
