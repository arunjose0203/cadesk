using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.AuditCommittee;

public static class AuditCommitteeEngagementStatus
{
    public const string Draft = "Draft";
    public const string PackGenerated = "PackGenerated";
    public const string Presented = "Presented";
    public const string SignedOff = "SignedOff";

    public static readonly string[] All = [Draft, PackGenerated, Presented, SignedOff];
}

public static class CommitteeMeetingType
{
    public const string Annual = "Annual";
    public const string Quarterly = "Quarterly";
    public const string PreIssuance = "Pre-Issuance";
    public const string Special = "Special";

    public static readonly string[] All = [Annual, Quarterly, PreIssuance, Special];
}

public static class DeficiencyType
{
    public const string DesignDeficiency = "DesignDeficiency";
    public const string OperatingDeficiency = "OperatingDeficiency";

    public static readonly string[] All = [DesignDeficiency, OperatingDeficiency];
}

public static class DeficiencySeverity
{
    public const string SignificantDeficiency = "SignificantDeficiency";
    public const string OtherDeficiency = "OtherDeficiency";

    public static readonly string[] All = [SignificantDeficiency, OtherDeficiency];
}

public static class DeficiencyStatus
{
    public const string Identified = "Identified";
    public const string CommunicatedToManagement = "CommunicatedToManagement";
    public const string Remediated = "Remediated";
    public const string CarriedForward = "CarriedForward";

    public static readonly string[] All = [Identified, CommunicatedToManagement, Remediated, CarriedForward];
}

public static class AuditMatterCategory
{
    public const string AccountingPractices = "AccountingPractices";
    public const string AuditDifficulties = "AuditDifficulties";
    public const string ManagementConsultation = "ManagementConsultation";
    public const string WrittenRepresentations = "WrittenRepresentations";
    public const string KeyAuditMatter = "KeyAuditMatter";

    public static readonly string[] All = [AccountingPractices, AuditDifficulties, ManagementConsultation, WrittenRepresentations, KeyAuditMatter];
}

public static class MisstatementNature
{
    public const string Factual = "Factual";
    public const string Judgmental = "Judgmental";
    public const string Projected = "Projected";

    public static readonly string[] All = [Factual, Judgmental, Projected];
}

public class AuditCommitteeEngagement : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? AuditEngagementId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";
    public string Title { get; set; } = "";
    public string MeetingType { get; set; } = CommitteeMeetingType.PreIssuance;
    public DateTime? MeetingDate { get; set; }
    public string? MeetingLocation { get; set; }
    public string CommitteeChairName { get; set; } = "";
    public string LeadPartnerName { get; set; } = "";
    public string Status { get; set; } = AuditCommitteeEngagementStatus.Draft;
    public string? MeetingMinutes { get; set; }
    public string? GeneratedGovernancePackText { get; set; }
    public string? GeneratedSa265LetterText { get; set; }

    public Guid? SignedOffByUserId { get; set; }
    public DateTime? SignedOffAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
    public User? SignedOffByUser { get; set; }

    public List<InternalControlDeficiency> Deficiencies { get; set; } = new();
    public List<SignificantAuditMatter> SignificantMatters { get; set; } = new();
    public List<UncorrectedMisstatement> UncorrectedMisstatements { get; set; } = new();
}

public class InternalControlDeficiency : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid CommitteeEngagementId { get; set; }
    public string DeficiencyCode { get; set; } = ""; // e.g. "ICD-01"
    public string Title { get; set; } = "";
    public string ProcessArea { get; set; } = "Revenue Recognition"; // Revenue, Procurement, Inventory, ITGC, Financial Close, Statutory
    public string DeficiencyType { get; set; } = AuditCommittee.DeficiencyType.DesignDeficiency;
    public string Severity { get; set; } = AuditCommittee.DeficiencySeverity.SignificantDeficiency;
    public string Description { get; set; } = "";
    public string PotentialFinancialEffect { get; set; } = "";
    public decimal? EstimatedRiskAmount { get; set; }
    public string AuditorRecommendation { get; set; } = "";
    public string? ManagementResponse { get; set; }
    public DateTime? RemediationTargetDate { get; set; }
    public string Status { get; set; } = AuditCommittee.DeficiencyStatus.Identified;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public AuditCommitteeEngagement? Engagement { get; set; }
}

public class SignificantAuditMatter : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid CommitteeEngagementId { get; set; }
    public string MatterCode { get; set; } = ""; // e.g. "SAM-01"
    public string Subject { get; set; } = "";
    public string Category { get; set; } = AuditMatterCategory.AccountingPractices;
    public string Details { get; set; } = "";
    public string AuditorAssessment { get; set; } = "";
    public string? AuditCommitteeFeedback { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public AuditCommitteeEngagement? Engagement { get; set; }
}

public class UncorrectedMisstatement : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid CommitteeEngagementId { get; set; }
    public string AccountDescription { get; set; } = "";
    public decimal MisstatementAmount { get; set; }
    public string Nature { get; set; } = MisstatementNature.Factual;
    public bool IsMaterial { get; set; }
    public string? ManagementJustificationForNonAdjustment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public AuditCommitteeEngagement? Engagement { get; set; }
}

public static class AuditCommitteeEngine
{
    private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");

    public static string GenerateSa265DeficiencyLetter(
        AuditCommitteeEngagement engagement,
        IEnumerable<InternalControlDeficiency> deficiencies)
    {
        var sb = new StringBuilder();
        var defs = deficiencies.ToList();
        var significantDefs = defs.Where(d => d.Severity == DeficiencySeverity.SignificantDeficiency).ToList();
        var otherDefs = defs.Where(d => d.Severity == DeficiencySeverity.OtherDeficiency).ToList();

        sb.AppendLine("COMMUNICATION OF SIGNIFICANT DEFICIENCIES IN INTERNAL CONTROL");
        sb.AppendLine("(Pursuant to ICAI Standard on Auditing 265 & Companies Act, 2013 Section 177)");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();

        sb.AppendLine($"To:");
        sb.AppendLine($"The Audit Committee / Board of Directors");
        sb.AppendLine($"{engagement.Client?.Name ?? "The Company"}");
        sb.AppendLine($"Financial Year: {engagement.FinancialYear}");
        sb.AppendLine($"Date: {DateTime.UtcNow:dd-MM-yyyy}");
        sb.AppendLine();

        sb.AppendLine("Dear Members of the Audit Committee,");
        sb.AppendLine();
        sb.AppendLine("In planning and performing our audit of the financial statements of the Company for the year ended 31st March, in accordance with the Standards on Auditing issued by the Institute of Chartered Accountants of India (ICAI), we considered the Company’s internal financial controls over financial reporting as a basis for designing audit procedures that are appropriate in the circumstances for the purpose of expressing our opinion on the financial statements, but not for the purpose of expressing an opinion on the effectiveness of the Company's internal financial controls.");
        sb.AppendLine();
        sb.AppendLine("Our consideration of internal financial controls was for the limited purpose described in the preceding paragraph and was not designed to identify all deficiencies in internal control that might be significant deficiencies or material weaknesses and therefore there can be no assurance that all such deficiencies have been identified.");
        sb.AppendLine();
        sb.AppendLine($"However, during our audit, we identified the following internal control matters ({defs.Count} total item(s)) that we wish to bring to your attention:");
        sb.AppendLine();

        // Section 1: Significant Deficiencies
        sb.AppendLine("1. SIGNIFICANT DEFICIENCIES IN INTERNAL CONTROL (SA 265)");
        sb.AppendLine(new string('-', 78));
        if (significantDefs.Count == 0)
        {
            sb.AppendLine("   * No significant deficiencies in internal control were identified during our audit.");
            sb.AppendLine();
        }
        else
        {
            int idx = 1;
            foreach (var sd in significantDefs)
            {
                sb.AppendLine($"   ({idx++}) [{sd.DeficiencyCode}] {sd.Title}");
                sb.AppendLine($"       Process Area        : {sd.ProcessArea}");
                sb.AppendLine($"       Deficiency Nature   : {(sd.DeficiencyType == DeficiencyType.DesignDeficiency ? "Deficiency in Design of Control" : "Deficiency in Operating Effectiveness")}");
                sb.AppendLine($"       Description         : {sd.Description}");
                sb.AppendLine($"       Potential Effect    : {sd.PotentialFinancialEffect}");
                if (sd.EstimatedRiskAmount.HasValue && sd.EstimatedRiskAmount.Value > 0)
                {
                    sb.AppendLine($"       Est. Financial Risk : {sd.EstimatedRiskAmount.Value.ToString("C2", IndianCulture)}");
                }
                sb.AppendLine($"       Recommendation      : {sd.AuditorRecommendation}");
                sb.AppendLine($"       Management Response : {sd.ManagementResponse ?? "Under management review; remediation planned."}");
                if (sd.RemediationTargetDate.HasValue)
                {
                    sb.AppendLine($"       Remediation Target  : {sd.RemediationTargetDate.Value:dd-MM-yyyy}");
                }
                sb.AppendLine();
            }
        }

        // Section 2: Other Deficiencies
        sb.AppendLine("2. OTHER DEFICIENCIES COMMUNICATED FOR OPERATIONAL IMPROVEMENT");
        sb.AppendLine(new string('-', 78));
        if (otherDefs.Count == 0)
        {
            sb.AppendLine("   * None noted for this reporting period.");
            sb.AppendLine();
        }
        else
        {
            int oIdx = 1;
            foreach (var od in otherDefs)
            {
                sb.AppendLine($"   ({oIdx++}) [{od.DeficiencyCode}] {od.Title} ({od.ProcessArea})");
                sb.AppendLine($"       Deficiency Type  : {od.DeficiencyType}");
                sb.AppendLine($"       Recommendation   : {od.AuditorRecommendation}");
                sb.AppendLine($"       Management Action: {od.ManagementResponse ?? "Acknowledged by process owner."}");
                sb.AppendLine();
            }
        }

        // Section 3: Inherent limitations disclaimer
        sb.AppendLine("3. INHERENT LIMITATIONS OF INTERNAL CONTROL");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine("Because of their inherent limitations, internal financial controls may not prevent or detect all misstatements or non-compliance. Also, projections of any evaluation of the internal financial controls to future periods are subject to the risk that the internal financial control may become inadequate because of changes in conditions, or that the degree of compliance with the policies or procedures may deteriorate.");
        sb.AppendLine();
        sb.AppendLine("This communication is intended solely for the information and use of the Audit Committee, Board of Directors, and Management, and is not intended to be and should not be used by anyone other than these specified parties.");
        sb.AppendLine();
        sb.AppendLine("For and on behalf of:");
        sb.AppendLine("Chartered Accountants");
        sb.AppendLine("ICAI Firm Registration Number: 000000N");
        sb.AppendLine();
        sb.AppendLine($"CA {engagement.LeadPartnerName}");
        sb.AppendLine("Partner / Lead Engagement Auditor");
        sb.AppendLine("Membership Number: 000000");

        return sb.ToString();
    }

    public static string GenerateSa260GovernancePack(
        AuditCommitteeEngagement engagement,
        IEnumerable<InternalControlDeficiency> deficiencies,
        IEnumerable<SignificantAuditMatter> matters,
        IEnumerable<UncorrectedMisstatement> misstatements)
    {
        var sb = new StringBuilder();
        var defs = deficiencies.ToList();
        var samList = matters.ToList();
        var misList = misstatements.ToList();

        sb.AppendLine("AUDIT COMMITTEE GOVERNANCE PACK & STATUTORY PRESENTATION");
        sb.AppendLine("(In compliance with ICAI SA 260 (Revised) & Companies Act, 2013 Section 177)");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();

        sb.AppendLine($"Client Organization : {engagement.Client?.Name ?? "The Company"}");
        sb.AppendLine($"Financial Year      : {engagement.FinancialYear}");
        sb.AppendLine($"Meeting Type        : {engagement.MeetingType} Meeting of the Audit Committee");
        sb.AppendLine($"Meeting Date        : {(engagement.MeetingDate.HasValue ? engagement.MeetingDate.Value.ToString("dd-MM-yyyy") : "Scheduled")}");
        sb.AppendLine($"Audit Committee Chair: {engagement.CommitteeChairName}");
        sb.AppendLine($"Lead Audit Partner  : {engagement.LeadPartnerName}");
        sb.AppendLine();

        // 1. Auditor Independence Affirmation
        sb.AppendLine("1. STATEMENT OF AUDITOR INDEPENDENCE (ICAI CODE OF ETHICS & SEC 177(4)(i))");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine("We hereby confirm that, in our professional judgment:");
        sb.AppendLine("  (a) We are independent of the Company in accordance with the ethical requirements of the Chartered Accountants Act, 1949, the Code of Ethics issued by the ICAI, and the provisions of the Companies Act, 2013.");
        sb.AppendLine("  (b) We have communicated to the Audit Committee all relationships and other matters between the audit firm, network firms, and the Company that, in our professional judgment, may reasonably be thought to bear on our independence.");
        sb.AppendLine("  (c) Total non-audit services provided were within the statutory restrictions of Section 144 of the Companies Act, 2013 and received prior approval from the Audit Committee where required.");
        sb.AppendLine("  (d) The related safeguards applied have eliminated identified threats to independence or reduced them to an acceptable level.");
        sb.AppendLine();

        // 2. Significant Findings & Qualitative Accounting Aspects
        sb.AppendLine("2. SIGNIFICANT AUDIT MATTERS & QUALITATIVE ACCOUNTING PRACTICES (SA 260)");
        sb.AppendLine(new string('-', 78));
        if (samList.Count == 0)
        {
            sb.AppendLine("   * No contentious accounting treatments or significant audit difficulties encountered.");
            sb.AppendLine();
        }
        else
        {
            int idx = 1;
            foreach (var m in samList)
            {
                sb.AppendLine($"   ({idx++}) [{m.MatterCode}] {m.Subject} (Category: {m.Category})");
                sb.AppendLine($"       Details            : {m.Details}");
                sb.AppendLine($"       Auditor Assessment : {m.AuditorAssessment}");
                if (!string.IsNullOrWhiteSpace(m.AuditCommitteeFeedback))
                {
                    sb.AppendLine($"       Committee Feedback : {m.AuditCommitteeFeedback}");
                }
                sb.AppendLine();
            }
        }

        // 3. Uncorrected Misstatements (SA 450)
        sb.AppendLine("3. SCHEDULE OF UNCORRECTED MISSTATEMENTS (SA 450 & SA 260)");
        sb.AppendLine(new string('-', 78));
        decimal sumMisstatements = misList.Sum(m => m.MisstatementAmount);
        int materialCount = misList.Count(m => m.IsMaterial);

        if (misList.Count == 0)
        {
            sb.AppendLine("   * None. There are no uncorrected misstatements identified during the course of our audit.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine(string.Format("   * Total Uncorrected Misstatements Count  : {0}", misList.Count));
            sb.AppendLine(string.Format("   * Total Aggregate Uncorrected Value      : {0}", sumMisstatements.ToString("C2", IndianCulture)));
            sb.AppendLine(string.Format("   * Material Misstatements Count           : {0}", materialCount));
            sb.AppendLine();

            sb.AppendLine(string.Format("{0,-10} | {1,-42} | {2,16} | {3,-12} | {4,-8}",
                "Item", "Account / Description", "Amount", "Nature", "Material"));
            sb.AppendLine(new string('-', 92));
            int mIdx = 1;
            foreach (var mis in misList)
            {
                sb.AppendLine(string.Format("{0,-10} | {1,-42} | {2,16} | {3,-12} | {4,-8}",
                    $"MIS-{mIdx++:D2}",
                    mis.AccountDescription.Length > 42 ? mis.AccountDescription.Substring(0, 42) : mis.AccountDescription,
                    mis.MisstatementAmount.ToString("C2", IndianCulture),
                    mis.Nature,
                    mis.IsMaterial ? "YES" : "NO"));
                if (!string.IsNullOrWhiteSpace(mis.ManagementJustificationForNonAdjustment))
                {
                    sb.AppendLine($"     Management Justification: {mis.ManagementJustificationForNonAdjustment}");
                }
            }
            sb.AppendLine(new string('-', 92));
            sb.AppendLine();
        }

        // 4. Summary of Internal Control Deficiencies (SA 265)
        sb.AppendLine("4. INTERNAL CONTROL DEFICIENCY HIGHLIGHTS (SA 265)");
        sb.AppendLine(new string('-', 78));
        int sigCount = defs.Count(d => d.Severity == DeficiencySeverity.SignificantDeficiency);
        int otherCount = defs.Count(d => d.Severity == DeficiencySeverity.OtherDeficiency);
        sb.AppendLine($"   * Significant Deficiencies : {sigCount}");
        sb.AppendLine($"   * Other Deficiencies       : {otherCount}");
        sb.AppendLine("   * Complete formal communication provided in separate SA 265 Letter.");
        sb.AppendLine();

        sb.AppendLine("5. AUDIT OPINION RECOMMENDATION & TCWG SIGN-OFF");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine("Subject to the receipt of final written management representations (SA 580) and execution of agreed adjustments, we anticipate issuing an UNMODIFIED audit opinion on the statutory financial statements.");
        sb.AppendLine();
        sb.AppendLine($"Presented to the Audit Committee by: CA {engagement.LeadPartnerName}");
        sb.AppendLine($"Audit Committee Chair Acknowledgment: {engagement.CommitteeChairName}");
        sb.AppendLine($"Status: {engagement.Status.ToUpperInvariant()}");

        return sb.ToString();
    }
}
