using System;
using System.Collections.Generic;
using System.Linq;
using CaDesk.Domain.BranchAudit;
using Xunit;

namespace CaDesk.UnitTests;

public class BranchAuditUnitTests
{
    [Fact]
    public void ComputeConsolidationTotals_AggregatesHeadOfficeAndBranchesCorrectly()
    {
        var engagement = new BranchAuditEngagement
        {
            FinancialYear = "2024-25",
            Title = "Statutory Consolidation 2024-25",
            HeadOfficeAssets = 100_000_000m,      // ₹10 Cr
            HeadOfficeLiabilities = 40_000_000m,  // ₹4 Cr
            HeadOfficeRevenue = 200_000_000m,     // ₹20 Cr
            HeadOfficeExpenses = 160_000_000m,    // ₹16 Cr
            HeadOfficeNetProfit = 40_000_000m     // ₹4 Cr
        };

        var branches = new List<BranchAuditOffice>
        {
            // Branch 1: Mumbai (Accepted)
            new()
            {
                BranchCode = "BR-MUM",
                BranchName = "Mumbai Branch",
                AuditorFirmName = "K. S. Sharma & Associates",
                TotalAssets = 30_000_000m,      // ₹3 Cr
                TotalLiabilities = 10_000_000m,
                TotalRevenue = 60_000_000m,     // ₹6 Cr
                TotalExpenses = 45_000_000m,
                NetProfit = 15_000_000m,        // ₹1.5 Cr
                Status = BranchOfficeStatus.Accepted,
                BranchReportType = BranchReportType.Clean
            },
            // Branch 2: Delhi (SubmittedByBranch)
            new()
            {
                BranchCode = "BR-DEL",
                BranchName = "Delhi NCR Branch",
                AuditorFirmName = "R. K. Gupta & Co.",
                TotalAssets = 20_000_000m,      // ₹2 Cr
                TotalLiabilities = 10_000_000m,
                TotalRevenue = 40_000_000m,     // ₹4 Cr
                TotalExpenses = 35_000_000m,
                NetProfit = 5_000_000m,         // ₹50 Lakhs
                Status = BranchOfficeStatus.SubmittedByBranch,
                BranchReportType = BranchReportType.Clean
            },
            // Branch 3: Bangalore (Assigned - not yet submitted -> excluded from consolidation)
            new()
            {
                BranchCode = "BR-BLR",
                BranchName = "Bangalore Tech Branch",
                AuditorFirmName = "N. Rao & Associates",
                TotalAssets = 15_000_000m,
                TotalLiabilities = 5_000_000m,
                TotalRevenue = 30_000_000m,
                TotalExpenses = 25_000_000m,
                NetProfit = 5_000_000m,
                Status = BranchOfficeStatus.Assigned
            }
        };

        Sa600Engine.ComputeConsolidationTotals(engagement, branches);

        // Branch magnitude counts
        Assert.Equal(2, engagement.BranchesAuditedCount);
        Assert.Equal(2, engagement.BranchAuditorsCount);

        // Branch sums (Mumbai ₹3Cr + Delhi ₹2Cr = ₹5Cr)
        Assert.Equal(50_000_000m, engagement.TotalBranchAssets);
        // Revenue (Mumbai ₹6Cr + Delhi ₹4Cr = ₹10Cr)
        Assert.Equal(100_000_000m, engagement.TotalBranchRevenue);
        // Net profit (Mumbai ₹1.5Cr + Delhi ₹0.5Cr = ₹2Cr)
        Assert.Equal(20_000_000m, engagement.TotalBranchNetProfit);

        // Consolidated totals (Head office + Branches)
        Assert.Equal(150_000_000m, engagement.TotalConsolidatedAssets);     // 10 + 5 Cr
        Assert.Equal(60_000_000m, engagement.TotalConsolidatedLiabilities); // 4 + 2 Cr
        Assert.Equal(300_000_000m, engagement.TotalConsolidatedRevenue);    // 20 + 10 Cr
        Assert.Equal(240_000_000m, engagement.TotalConsolidatedExpenses);   // 16 + 8 Cr
        Assert.Equal(60_000_000m, engagement.TotalConsolidatedNetProfit);   // 4 + 2 Cr

        // SA 600 magnitude percentages
        Assert.Equal(33.33m, engagement.BranchAssetsPercentage);     // 5 / 15 * 100
        Assert.Equal(33.33m, engagement.BranchRevenuePercentage);    // 10 / 30 * 100
        Assert.Equal(33.33m, engagement.BranchNetProfitPercentage);  // 2 / 6 * 100
        Assert.Equal(0, engagement.BranchQualificationsCount);
    }

    [Fact]
    public void TokenGenerationAndHashing_ProducesConsistentSha256()
    {
        var rawToken = Sa600Engine.GenerateBranchAccessToken();

        Assert.NotNull(rawToken);
        Assert.Equal(64, rawToken.Length); // 32 bytes in hex = 64 characters

        var hash1 = Sa600Engine.HashToken(rawToken);
        var hash2 = Sa600Engine.HashToken(rawToken);

        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, hash2);

        // Case-insensitivity check on hashing
        var hashUpper = Sa600Engine.HashToken(rawToken.ToUpperInvariant());
        Assert.NotEqual(hash1, hashUpper); // raw token is lowercase hex
    }

    [Fact]
    public void GenerateSa600OtherMatterParagraph_GeneratesCleanReport()
    {
        var engagement = new BranchAuditEngagement
        {
            BranchesAuditedCount = 2,
            BranchAuditorsCount = 2,
            TotalBranchAssets = 50_000_000m,
            BranchAssetsPercentage = 25.50m,
            TotalBranchRevenue = 80_000_000m,
            BranchRevenuePercentage = 28.00m,
            TotalBranchNetProfit = 12_000_000m,
            BranchNetProfitPercentage = 20.00m
        };

        var branches = new List<BranchAuditOffice>
        {
            new()
            {
                BranchCode = "BR-01",
                BranchName = "Kolkata Branch",
                AuditorFirmName = "Roy & Co.",
                BranchReportType = BranchReportType.Clean,
                Status = BranchOfficeStatus.Accepted
            }
        };

        var text = Sa600Engine.GenerateSa600OtherMatterParagraph(engagement, branches);

        Assert.Contains("OTHER MATTER - USING THE WORK OF OTHER AUDITORS", text);
        Assert.Contains("Section 143(8)", text);
        Assert.Contains("ICAI SA 600", text);
        Assert.Contains("25.50% of total consolidated assets", text);
        Assert.Contains("28.00% of total consolidated revenues", text);
        Assert.Contains("None. The independent reports of all branch auditors were unmodified (Clean).", text);
    }

    [Fact]
    public void GenerateSa600OtherMatterParagraph_DisclosesQualificationsProperly()
    {
        var engagement = new BranchAuditEngagement
        {
            BranchesAuditedCount = 1,
            BranchAuditorsCount = 1,
            TotalBranchAssets = 10_000_000m,
            BranchAssetsPercentage = 15.00m,
            TotalBranchRevenue = 20_000_000m,
            BranchRevenuePercentage = 12.00m,
            TotalBranchNetProfit = 2_000_000m,
            BranchNetProfitPercentage = 10.00m
        };

        var branches = new List<BranchAuditOffice>
        {
            new()
            {
                BranchCode = "BR-CHE",
                BranchName = "Chennai Port Branch",
                AuditorFirmName = "V. Ramanathan & Associates",
                AuditorFirmRegistrationNumber = "004562S",
                BranchReportType = BranchReportType.Qualified,
                HasQualifications = true,
                QualificationsSummary = "Non-reconciliation of physical inventory of ₹15.4 Lakhs with branch general ledger.",
                Status = BranchOfficeStatus.Accepted
            }
        };

        var text = Sa600Engine.GenerateSa600OtherMatterParagraph(engagement, branches);

        Assert.Contains("Chennai Port Branch [BR-CHE]", text);
        Assert.Contains("V. Ramanathan & Associates", text);
        Assert.Contains("QUALIFIED", text);
        Assert.Contains("Non-reconciliation of physical inventory of ₹15.4 Lakhs", text);
    }
}
