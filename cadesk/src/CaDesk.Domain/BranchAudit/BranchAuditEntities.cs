using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.BranchAudit;

public static class BranchAuditEngagementStatus
{
    public const string Draft = "draft";
    public const string BranchesInProgress = "branches_in_progress";
    public const string ReviewReady = "review_ready";
    public const string Consolidated = "consolidated";
    public const string Finalized = "finalized";

    public static readonly string[] All = [Draft, BranchesInProgress, ReviewReady, Consolidated, Finalized];
}

public static class BranchOfficeStatus
{
    public const string Assigned = "Assigned";
    public const string PackageSent = "PackageSent";
    public const string SubmittedByBranch = "SubmittedByBranch";
    public const string UnderReview = "UnderReview";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Assigned, PackageSent, SubmittedByBranch, UnderReview, Accepted, Rejected];
}

public static class BranchReportType
{
    public const string Clean = "Clean";
    public const string Qualified = "Qualified";
    public const string Adverse = "Adverse";
    public const string Disclaimer = "Disclaimer";

    public static readonly string[] All = [Clean, Qualified, Adverse, Disclaimer];
}

public class BranchAuditEngagement : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? AuditEngagementId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";
    public string Title { get; set; } = "";

    // Head Office Financials (standalone base before consolidation)
    public decimal HeadOfficeAssets { get; set; }
    public decimal HeadOfficeLiabilities { get; set; }
    public decimal HeadOfficeRevenue { get; set; }
    public decimal HeadOfficeExpenses { get; set; }
    public decimal HeadOfficeNetProfit { get; set; }

    // Consolidated Financials (Head Office + Branches)
    public decimal TotalConsolidatedAssets { get; set; }
    public decimal TotalConsolidatedLiabilities { get; set; }
    public decimal TotalConsolidatedRevenue { get; set; }
    public decimal TotalConsolidatedExpenses { get; set; }
    public decimal TotalConsolidatedNetProfit { get; set; }

    // Magnitude of Branches Audited by Other Auditors (SA 600 disclosures)
    public int BranchesAuditedCount { get; set; }
    public int BranchAuditorsCount { get; set; }
    public decimal TotalBranchAssets { get; set; }
    public decimal BranchAssetsPercentage { get; set; }
    public decimal TotalBranchRevenue { get; set; }
    public decimal BranchRevenuePercentage { get; set; }
    public decimal TotalBranchNetProfit { get; set; }
    public decimal BranchNetProfitPercentage { get; set; }

    // Status and Consolidation
    public string ConsolidationStatus { get; set; } = BranchAuditEngagementStatus.Draft;
    public int BranchQualificationsCount { get; set; }
    public string? PrincipalAuditorNotes { get; set; }
    public string? GeneratedSa600ReportText { get; set; }

    public Guid? FinalizedByUserId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
    public User? FinalizedByUser { get; set; }

    public List<BranchAuditOffice> Branches { get; set; } = new();
}

public class BranchAuditOffice : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }

    // Branch Information
    public string BranchCode { get; set; } = ""; // e.g. "BR-01"
    public string BranchName { get; set; } = "";
    public string BranchLocation { get; set; } = "";
    public bool IsForeignBranch { get; set; }

    // Branch Auditor Information (Section 143(8))
    public string AuditorFirmName { get; set; } = "";
    public string AuditorFirmRegistrationNumber { get; set; } = "";
    public string AuditorName { get; set; } = "";
    public string AuditorMembershipNumber { get; set; } = "";
    public string AuditorEmail { get; set; } = "";
    public string? AuditorPhone { get; set; }

    // Delegated External Portal Token Access
    public string? AccessTokenHash { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime? LastSubmittedAt { get; set; }

    // Branch Financial Returns
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public string BranchReportType { get; set; } = BranchAuditReportType.Clean;
    public bool HasQualifications { get; set; }
    public string? QualificationsSummary { get; set; }
    public string? BranchAuditReportRef { get; set; }

    // SA 600 Questionnaire & Checklist Confirmations
    public bool AccountingPoliciesConsistent { get; set; } = true;
    public bool RelatedPartyTransactionsDisclosed { get; set; } = true;
    public bool ContingentLiabilitiesReported { get; set; }
    public bool SubsequentEventsReported { get; set; }
    public bool FraudOrIrregularitiesReported { get; set; }
    public bool InternalControlsCommensurate { get; set; } = true;
    public bool CaroApplicableToBranch { get; set; }
    public string? CaroReportSummary { get; set; }

    // Review Workflow
    public string Status { get; set; } = BranchOfficeStatus.Assigned;
    public string? PrincipalAuditorReviewNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public BranchAuditEngagement? Engagement { get; set; }
    public User? ReviewedByUser { get; set; }
}

public static class BranchAuditReportType
{
    public const string Clean = "Clean";
    public const string Qualified = "Qualified";
    public const string Adverse = "Adverse";
    public const string Disclaimer = "Disclaimer";
}

public static class Sa600Engine
{
    private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");

    public static string GenerateBranchAccessToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void ComputeConsolidationTotals(BranchAuditEngagement engagement, IEnumerable<BranchAuditOffice> branches)
    {
        var branchList = branches.ToList();

        decimal sumBranchAssets = 0m;
        decimal sumBranchLiabilities = 0m;
        decimal sumBranchRevenue = 0m;
        decimal sumBranchExpenses = 0m;
        decimal sumBranchNetProfit = 0m;
        int qualifications = 0;
        var distinctAuditors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var b in branchList)
        {
            // Only aggregate branches that have been submitted or accepted
            if (b.Status == BranchOfficeStatus.SubmittedByBranch ||
                b.Status == BranchOfficeStatus.UnderReview ||
                b.Status == BranchOfficeStatus.Accepted)
            {
                sumBranchAssets += b.TotalAssets;
                sumBranchLiabilities += b.TotalLiabilities;
                sumBranchRevenue += b.TotalRevenue;
                sumBranchExpenses += b.TotalExpenses;
                sumBranchNetProfit += b.NetProfit;

                if (!string.IsNullOrWhiteSpace(b.AuditorFirmName))
                {
                    distinctAuditors.Add(b.AuditorFirmName.Trim());
                }

                if (b.HasQualifications || b.BranchReportType == BranchReportType.Qualified || b.BranchReportType == BranchReportType.Adverse)
                {
                    qualifications++;
                }
            }
        }

        engagement.BranchesAuditedCount = branchList.Count(b =>
            b.Status == BranchOfficeStatus.SubmittedByBranch ||
            b.Status == BranchOfficeStatus.UnderReview ||
            b.Status == BranchOfficeStatus.Accepted);

        engagement.BranchAuditorsCount = distinctAuditors.Count;
        engagement.TotalBranchAssets = sumBranchAssets;
        engagement.TotalBranchRevenue = sumBranchRevenue;
        engagement.TotalBranchNetProfit = sumBranchNetProfit;
        engagement.BranchQualificationsCount = qualifications;

        // Consolidated totals = Head Office + Branches
        engagement.TotalConsolidatedAssets = engagement.HeadOfficeAssets + sumBranchAssets;
        engagement.TotalConsolidatedLiabilities = engagement.HeadOfficeLiabilities + sumBranchLiabilities;
        engagement.TotalConsolidatedRevenue = engagement.HeadOfficeRevenue + sumBranchRevenue;
        engagement.TotalConsolidatedExpenses = engagement.HeadOfficeExpenses + sumBranchExpenses;
        engagement.TotalConsolidatedNetProfit = engagement.HeadOfficeNetProfit + sumBranchNetProfit;

        // Compute Magnitude Percentages
        engagement.BranchAssetsPercentage = engagement.TotalConsolidatedAssets > 0
            ? Math.Round((sumBranchAssets / engagement.TotalConsolidatedAssets) * 100m, 2)
            : 0m;

        engagement.BranchRevenuePercentage = engagement.TotalConsolidatedRevenue > 0
            ? Math.Round((sumBranchRevenue / engagement.TotalConsolidatedRevenue) * 100m, 2)
            : 0m;

        engagement.BranchNetProfitPercentage = engagement.TotalConsolidatedNetProfit != 0
            ? Math.Round((sumBranchNetProfit / Math.Abs(engagement.TotalConsolidatedNetProfit)) * 100m, 2)
            : 0m;
    }

    public static string GenerateSa600OtherMatterParagraph(
        BranchAuditEngagement engagement,
        IEnumerable<BranchAuditOffice> branches)
    {
        var sb = new StringBuilder();
        var branchList = branches
            .Where(b => b.Status == BranchOfficeStatus.SubmittedByBranch ||
                        b.Status == BranchOfficeStatus.UnderReview ||
                        b.Status == BranchOfficeStatus.Accepted)
            .ToList();

        sb.AppendLine("OTHER MATTER - USING THE WORK OF OTHER AUDITORS");
        sb.AppendLine("(Pursuant to Section 143(8) of the Companies Act, 2013 and ICAI SA 600)");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();

        sb.AppendLine("1. We did not audit the financial statements / financial information of the following branch offices included in the financial statements of the Company:");
        sb.AppendLine();

        sb.AppendLine(string.Format("   * Number of branch offices audited by other auditors : {0}", engagement.BranchesAuditedCount));
        sb.AppendLine(string.Format("   * Number of independent branch auditor firms          : {0}", engagement.BranchAuditorsCount));
        sb.AppendLine(string.Format("   * Total assets audited by other branch auditors       : {0} ({1:F2}% of total consolidated assets)",
            engagement.TotalBranchAssets.ToString("C2", IndianCulture), engagement.BranchAssetsPercentage));
        sb.AppendLine(string.Format("   * Total revenues audited by other branch auditors     : {0} ({1:F2}% of total consolidated revenues)",
            engagement.TotalBranchRevenue.ToString("C2", IndianCulture), engagement.BranchRevenuePercentage));
        sb.AppendLine(string.Format("   * Net profit contributed by other branch offices      : {0} ({1:F2}% of total consolidated net profit)",
            engagement.TotalBranchNetProfit.ToString("C2", IndianCulture), engagement.BranchNetProfitPercentage));
        sb.AppendLine();

        sb.AppendLine("2. The financial statements / financial information of these branches have been audited by the branch auditors whose reports have been furnished to us by the Management, and our opinion on the financial statements of the Company, in so far as it relates to the amounts and disclosures included in respect of these branches, is based solely on the reports of such branch auditors.");
        sb.AppendLine();

        // Branch-wise summary schedule
        sb.AppendLine("3. SCHEDULE OF BRANCH OFFICES AUDITED UNDER SECTION 143(8):");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-20} | {3,-10} | {4,-6}",
            "Code", "Branch Office", "Branch Auditor", "Report Type", "Qual."));
        sb.AppendLine(new string('-', 78));

        foreach (var b in branchList)
        {
            string qualFlag = b.HasQualifications ? "YES" : "NO";
            sb.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-20} | {3,-10} | {4,-6}",
                b.BranchCode,
                b.BranchName.Length > 24 ? b.BranchName.Substring(0, 24) : b.BranchName,
                b.AuditorFirmName.Length > 20 ? b.AuditorFirmName.Substring(0, 20) : b.AuditorFirmName,
                b.BranchReportType,
                qualFlag));
        }
        sb.AppendLine(new string('-', 78));
        sb.AppendLine();

        // Branch auditor qualifications
        var qualifiedBranches = branchList.Where(b => b.HasQualifications || b.BranchReportType != BranchReportType.Clean).ToList();
        sb.AppendLine("4. OBSERVATIONS / QUALIFICATIONS IN BRANCH AUDIT REPORTS:");
        sb.AppendLine(new string('-', 78));
        if (qualifiedBranches.Count == 0)
        {
            sb.AppendLine("   * None. The independent reports of all branch auditors were unmodified (Clean).");
        }
        else
        {
            int idx = 1;
            foreach (var qb in qualifiedBranches)
            {
                sb.AppendLine($"   ({idx++}) {qb.BranchName} [{qb.BranchCode}]:");
                sb.AppendLine($"       Auditor Firm  : {qb.AuditorFirmName} (FRN: {qb.AuditorFirmRegistrationNumber})");
                sb.AppendLine($"       Report Type   : {qb.BranchReportType.ToUpper()}");
                sb.AppendLine($"       Finding/Issue : {qb.QualificationsSummary ?? "Branch qualification noted on branch assets/statutory dues."}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("5. Our opinion on the financial statements is not modified in respect of the above matters with respect to our reliance on the work done and the reports of the other auditors.");
        sb.AppendLine();
        sb.AppendLine("For and on behalf of:");
        sb.AppendLine("Chartered Accountants");
        sb.AppendLine("ICAI Firm Registration Number: 000000N");
        sb.AppendLine();
        sb.AppendLine("Principal Auditor / Partner");
        sb.AppendLine("Membership Number: 000000");
        sb.AppendLine($"Date: {DateTime.UtcNow:dd-MM-yyyy}");

        return sb.ToString();
    }
}
