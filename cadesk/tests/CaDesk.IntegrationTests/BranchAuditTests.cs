using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Api.Endpoints;
using CaDesk.Application.BranchAudit;
using CaDesk.Application.Clients;
using CaDesk.Domain.BranchAudit;
using Xunit;

namespace CaDesk.IntegrationTests;

public class BranchAuditTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "PwC Statutory Audit Practice LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Godrej Consumer Holdings Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task BranchAudit_FullLifecycle_BranchAuditorPortal_Consolidation_And_FirmIsolation()
    {
        var (httpA, _) = await NewFirmAsync("BSR & Associates Principal Practice");
        var clientA = await CreateClientAsync(httpA, "Bharat Heavy Infrastructure Ltd");

        // 1. Create Engagement
        var saveEngReq = new SaveBranchEngagementRequest(
            ClientId: clientA.Id,
            AuditEngagementId: null,
            FinancialYear: "2024-25",
            Title: "Statutory Audit FY 2024-25 - Multi-Branch Consolidation",
            HeadOfficeAssets: 500_000_000m,      // ₹50 Cr
            HeadOfficeLiabilities: 200_000_000m,  // ₹20 Cr
            HeadOfficeRevenue: 1_000_000_000m,   // ₹100 Cr
            HeadOfficeExpenses: 800_000_000m,    // ₹80 Cr
            HeadOfficeNetProfit: 200_000_000m,   // ₹20 Cr
            PrincipalAuditorNotes: "Principal audit team covering Head Office operations in Mumbai."
        );

        var createRes = await httpA.PostAsJsonAsync("/api/branchaudit/engagements", saveEngReq);
        createRes.EnsureSuccessStatusCode();
        var engagement = (await createRes.Content.ReadFromJsonAsync<BranchAuditEngagementDto>())!;
        Assert.NotNull(engagement);
        Assert.Equal("draft", engagement.ConsolidationStatus);
        Assert.Equal(500_000_000m, engagement.HeadOfficeAssets);

        // 2. Add Branch 1 (Mumbai Port Branch)
        var saveBranch1Req = new SaveBranchOfficeRequest(
            BranchCode: "BR-MUM",
            BranchName: "Mumbai Port & Logistics Branch",
            BranchLocation: "Navi Mumbai, Maharashtra",
            IsForeignBranch: false,
            AuditorFirmName: "Desai & Co.",
            AuditorFirmRegistrationNumber: "102345W",
            AuditorName: "Sunil Desai",
            AuditorMembershipNumber: "045612",
            AuditorEmail: "sunil@desai-ca.in",
            AuditorPhone: "9820011223",
            TotalAssets: 0m,
            TotalLiabilities: 0m,
            TotalRevenue: 0m,
            TotalExpenses: 0m,
            NetProfit: 0m,
            BranchReportType: BranchReportType.Clean,
            HasQualifications: false,
            QualificationsSummary: null,
            BranchAuditReportRef: null,
            AccountingPoliciesConsistent: true,
            RelatedPartyTransactionsDisclosed: true,
            ContingentLiabilitiesReported: false,
            SubsequentEventsReported: false,
            FraudOrIrregularitiesReported: false,
            InternalControlsCommensurate: true,
            CaroApplicableToBranch: true,
            CaroReportSummary: null
        );

        var branch1Res = await httpA.PostAsJsonAsync($"/api/branchaudit/engagements/{engagement.Id}/branches", saveBranch1Req);
        branch1Res.EnsureSuccessStatusCode();
        var branch1 = (await branch1Res.Content.ReadFromJsonAsync<BranchAuditOfficeDto>())!;
        Assert.NotNull(branch1);
        Assert.Equal("BR-MUM", branch1.BranchCode);
        Assert.Equal(BranchOfficeStatus.Assigned, branch1.Status);

        // 3. Generate External Branch Auditor Token
        var tokenRes = await httpA.PostAsync($"/api/branchaudit/engagements/{engagement.Id}/branches/{branch1.Id}/generate-token", null);
        tokenRes.EnsureSuccessStatusCode();
        var tokenData = (await tokenRes.Content.ReadFromJsonAsync<GenerateTokenResponse>())!;
        Assert.NotNull(tokenData.RawToken);
        Assert.Contains(tokenData.RawToken, tokenData.PortalUrl);

        // 4. External Branch Auditor views branch portal anonymously (without firm JWT token)
        var anonClient = api.CreateClient();
        var portalInfoRes = await anonClient.GetAsync($"/api/branchaudit/portal/branch?token={tokenData.RawToken}");
        Assert.True(portalInfoRes.IsSuccessStatusCode, await portalInfoRes.Content.ReadAsStringAsync());
        var portalBranch = (await portalInfoRes.Content.ReadFromJsonAsync<BranchAuditOfficeDto>())!;
        Assert.Equal("BR-MUM", portalBranch.BranchCode);
        Assert.Equal("Desai & Co.", portalBranch.AuditorFirmName);

        // 5. External Branch Auditor submits branch returns
        var submitReq = new SubmitBranchReturnsRequest(
            Token: tokenData.RawToken,
            TotalAssets: 150_000_000m,      // ₹15 Cr
            TotalLiabilities: 50_000_000m,   // ₹5 Cr
            TotalRevenue: 300_000_000m,     // ₹30 Cr
            TotalExpenses: 250_000_000m,    // ₹25 Cr
            NetProfit: 50_000_000m,         // ₹5 Cr
            BranchReportType: BranchReportType.Clean,
            HasQualifications: false,
            QualificationsSummary: null,
            BranchAuditReportRef: "DESAI/BR/2025/08",
            AccountingPoliciesConsistent: true,
            RelatedPartyTransactionsDisclosed: true,
            ContingentLiabilitiesReported: false,
            SubsequentEventsReported: false,
            FraudOrIrregularitiesReported: false,
            InternalControlsCommensurate: true,
            CaroApplicableToBranch: true,
            CaroReportSummary: "Unmodified CARO report issued for branch assets and inventory."
        );

        var submitRes = await anonClient.PostAsJsonAsync("/api/branchaudit/portal/submit", submitReq);
        submitRes.EnsureSuccessStatusCode();
        var submittedBranch = (await submitRes.Content.ReadFromJsonAsync<BranchAuditOfficeDto>())!;
        Assert.Equal(BranchOfficeStatus.SubmittedByBranch, submittedBranch.Status);
        Assert.Equal(150_000_000m, submittedBranch.TotalAssets);

        // 6. Principal Auditor reviews and accepts Mumbai branch
        var reviewReq = new ReviewBranchOfficeRequest(
            Decision: "accept",
            ReviewNotes: "Certified branch returns and CARO memo received and accepted without modification."
        );
        var reviewRes = await httpA.PostAsJsonAsync($"/api/branchaudit/engagements/{engagement.Id}/branches/{branch1.Id}/review", reviewReq);
        reviewRes.EnsureSuccessStatusCode();
        var reviewedBranch = (await reviewRes.Content.ReadFromJsonAsync<BranchAuditOfficeDto>())!;
        Assert.Equal(BranchOfficeStatus.Accepted, reviewedBranch.Status);

        // 7. Add Branch 2 (Delhi Branch with a qualification)
        var saveBranch2Req = new SaveBranchOfficeRequest(
            BranchCode: "BR-DEL",
            BranchName: "Delhi Distribution Center",
            BranchLocation: "Okhla, New Delhi",
            IsForeignBranch: false,
            AuditorFirmName: "Agarwal & Associates",
            AuditorFirmRegistrationNumber: "009876N",
            AuditorName: "Rajeev Agarwal",
            AuditorMembershipNumber: "089123",
            AuditorEmail: "rajeev@agarwal-ca.com",
            AuditorPhone: "9811223344",
            TotalAssets: 100_000_000m,     // ₹10 Cr
            TotalLiabilities: 30_000_000m,  // ₹3 Cr
            TotalRevenue: 200_000_000m,    // ₹20 Cr
            TotalExpenses: 180_000_000m,   // ₹18 Cr
            NetProfit: 20_000_000m,        // ₹2 Cr
            BranchReportType: BranchReportType.Qualified,
            HasQualifications: true,
            QualificationsSummary: "Shortage of physical stock amounting to ₹12.50 Lakhs under reconciliation.",
            BranchAuditReportRef: "AGARWAL/DEL/2025/14",
            AccountingPoliciesConsistent: true,
            RelatedPartyTransactionsDisclosed: true,
            ContingentLiabilitiesReported: false,
            SubsequentEventsReported: false,
            FraudOrIrregularitiesReported: false,
            InternalControlsCommensurate: true,
            CaroApplicableToBranch: false,
            CaroReportSummary: null
        );

        var branch2Res = await httpA.PostAsJsonAsync($"/api/branchaudit/engagements/{engagement.Id}/branches", saveBranch2Req);
        branch2Res.EnsureSuccessStatusCode();
        var branch2 = (await branch2Res.Content.ReadFromJsonAsync<BranchAuditOfficeDto>())!;

        // Directly mark branch 2 as accepted by reviewing
        var reviewBranch2Res = await httpA.PostAsJsonAsync($"/api/branchaudit/engagements/{engagement.Id}/branches/{branch2.Id}/review", new ReviewBranchOfficeRequest("accept", "Accepted with qualification noted."));
        reviewBranch2Res.EnsureSuccessStatusCode();

        // 8. Consolidate Engagement
        var consolidateRes = await httpA.PostAsync($"/api/branchaudit/engagements/{engagement.Id}/consolidate", null);
        consolidateRes.EnsureSuccessStatusCode();
        var consolidatedEng = (await consolidateRes.Content.ReadFromJsonAsync<BranchAuditEngagementDto>())!;

        // Verify Consolidation Figures
        // Total Consolidated Assets = Head Office (50 Cr) + Mumbai (15 Cr) + Delhi (10 Cr) = 75 Cr (750,000,000)
        Assert.Equal(750_000_000m, consolidatedEng.TotalConsolidatedAssets);
        // Branch Assets = 25 Cr (250,000,000)
        Assert.Equal(250_000_000m, consolidatedEng.TotalBranchAssets);
        // Branch Assets % = 25 / 75 * 100 = 33.33%
        Assert.Equal(33.33m, consolidatedEng.BranchAssetsPercentage);

        // Total Consolidated Revenue = Head Office (100 Cr) + Mumbai (30 Cr) + Delhi (20 Cr) = 150 Cr (1,500,000,000)
        Assert.Equal(1_500_000_000m, consolidatedEng.TotalConsolidatedRevenue);
        // Branch Revenue % = 50 / 150 * 100 = 33.33%
        Assert.Equal(33.33m, consolidatedEng.BranchRevenuePercentage);

        // Total Consolidated Net Profit = Head Office (20 Cr) + Mumbai (5 Cr) + Delhi (2 Cr) = 27 Cr (270,000,000)
        Assert.Equal(270_000_000m, consolidatedEng.TotalConsolidatedNetProfit);

        Assert.Equal(2, consolidatedEng.BranchesAuditedCount);
        Assert.Equal(2, consolidatedEng.BranchAuditorsCount);
        Assert.Equal(1, consolidatedEng.BranchQualificationsCount);
        Assert.NotNull(consolidatedEng.GeneratedSa600ReportText);
        Assert.Contains("OTHER MATTER - USING THE WORK OF OTHER AUDITORS", consolidatedEng.GeneratedSa600ReportText);
        Assert.Contains("Section 143(8)", consolidatedEng.GeneratedSa600ReportText);
        Assert.Contains("Delhi Distribution Center [BR-DEL]", consolidatedEng.GeneratedSa600ReportText);

        // 9. Finalize Engagement
        var finalizeRes = await httpA.PostAsync($"/api/branchaudit/engagements/{engagement.Id}/finalize", null);
        finalizeRes.EnsureSuccessStatusCode();
        var finalizedEng = (await finalizeRes.Content.ReadFromJsonAsync<BranchAuditEngagementDto>())!;
        Assert.Equal(BranchAuditEngagementStatus.Finalized, finalizedEng.ConsolidationStatus);
        Assert.NotNull(finalizedEng.FinalizedAt);

        // 10. Check KPI Summary
        var summaryRes = await httpA.GetAsync("/api/branchaudit/summary");
        summaryRes.EnsureSuccessStatusCode();
        var summary = (await summaryRes.Content.ReadFromJsonAsync<BranchAuditSummaryDto>())!;
        Assert.True(summary.TotalEngagements >= 1);
        Assert.True(summary.FinalizedCount >= 1);
        Assert.True(summary.TotalBranches >= 2);
        Assert.True(summary.BranchesAccepted >= 2);

        // 11. Multi-Tenant Two-Lock Isolation Check
        var (httpB, _) = await NewFirmAsync("KPMG Independent Assurance");
        var crossFirmRes = await httpB.GetAsync($"/api/branchaudit/engagements/{engagement.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossFirmRes.StatusCode);
    }
}
