using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Audit;
using CaDesk.Application.Clients;
using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 15: Statutory Audit & Audit Working Papers (ICAI SA 200/300/320/500/530).
/// Tests engagement lifecycle, SA 320 materiality calculation, auto-seeded working papers,
/// multi-tier review workflow, and firm-level tenant isolation.
/// </summary>
public class StatutoryAuditTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Audit Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Apex Technologies Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Audit_FullEngagementAndWorkingPaperLifecycle_Succeeds()
    {
        var (http, userId) = await NewFirmAsync("Deloitte Associates LLP");
        var client = await CreateClientAsync(http, "Reliance Digital Ventures");

        // 1. Create audit engagement with 50 Crores turnover (500,000,000)
        var createReq = new CreateAuditEngagementRequest(
            ClientId: client.Id,
            FinancialYear: "2024-25",
            AuditType: AuditType.Statutory,
            Title: "Statutory Audit FY 2024-25",
            LeadPartnerId: userId,
            ManagerId: null,
            PlannedStartDate: new DateOnly(2025, 4, 1),
            PlannedEndDate: new DateOnly(2025, 9, 30),
            BenchmarkType: BenchmarkType.Revenue,
            BenchmarkAmount: 500_000_000m,
            Notes: "Listed entity audit engagement under Section 139 of Companies Act 2013"
        );

        var createRes = await http.PostAsJsonAsync("/api/audit/engagements", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var eng = (await createRes.Content.ReadFromJsonAsync<AuditEngagementDto>())!;

        Assert.Equal(client.Id, eng.ClientId);
        Assert.Equal(5_000_000m, eng.OverallMateriality);      // 1% of 500M
        Assert.Equal(3_750_000m, eng.PerformanceMateriality);  // 75% of 5M
        Assert.Equal(250_000m, eng.TrivialThreshold);          // 5% of 5M
        Assert.Equal(EngagementStatus.Planning, eng.Status);

        // 2. Verify auto-seeded standard ICAI working paper templates
        var wps = await http.GetFromJsonAsync<List<AuditWorkingPaperDto>>($"/api/audit/engagements/{eng.Id}/working-papers");
        Assert.NotNull(wps);
        // Sprint 28 template set: Revenue, Receivables, Bank, Fixed Assets, Inventory, Statutory dues
        Assert.Equal(6, wps.Count);
        Assert.Contains(wps, x => x.RefCode == "WP-BNK-01");
        Assert.Contains(wps, x => x.RefCode == "WP-REV-01");

        // 3. Create custom working paper (e.g. WP/INV/01 Inventory Count)
        var invWpReq = new SaveWorkingPaperRequest(
            RefCode: "WP/INV/01",
            Area: "Inventories & WIP",
            Title: "Physical Inventory Attendance & Net Realizable Value Testing",
            Assertion: AuditAssertion.Existence,
            Objective: "Attend year-end physical inventory count at manufacturing plant per SA 501.",
            ProceduresPerformed: "Observed management count procedures, performed test counts for 25 high-value raw material lots.",
            SamplingMethod: SamplingMethod.RiskTargeted,
            PopulationValue: 45_000_000m,
            SampleValue: 32_000_000m,
            ItemsTestedCount: 25,
            ExceptionsNoted: "No material discrepancies identified.",
            Conclusion: AuditConclusion.Satisfactory
        );

        var createWpRes = await http.PostAsJsonAsync($"/api/audit/engagements/{eng.Id}/working-papers", invWpReq);
        Assert.Equal(HttpStatusCode.Created, createWpRes.StatusCode);
        var invWp = (await createWpRes.Content.ReadFromJsonAsync<AuditWorkingPaperDto>())!;
        Assert.Equal("WP/INV/01", invWp.RefCode);
        Assert.Equal(WorkingPaperStatus.Draft, invWp.Status);

        // 4. Working paper review workflow: Review & Signoff
        var reviewReq = new ReviewWorkingPaperRequest(
            Status: WorkingPaperStatus.Reviewed,
            ReviewNotes: "Substantive count procedures adequate. NRV testing corroborated with subsequent sales invoices."
        );
        var reviewRes = await http.PutAsJsonAsync($"/api/audit/working-papers/{invWp.Id}/review", reviewReq);
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);
        var reviewedWp = (await reviewRes.Content.ReadFromJsonAsync<AuditWorkingPaperDto>())!;
        Assert.Equal(WorkingPaperStatus.Reviewed, reviewedWp.Status);
        Assert.NotNull(reviewedWp.ReviewedAt);
        Assert.Equal(userId, reviewedWp.ReviewerId);

        // 5. Engagement status update: Move to Partner Signoff & issue Unmodified Audit Opinion
        var statusReq = new UpdateEngagementStatusRequest(
            Status: EngagementStatus.PartnerSignoff,
            AuditOpinion: AuditOpinion.Unmodified
        );
        var statusRes = await http.PutAsJsonAsync($"/api/audit/engagements/{eng.Id}/status", statusReq);
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        var finalEng = (await statusRes.Content.ReadFromJsonAsync<AuditEngagementDto>())!;
        Assert.Equal(EngagementStatus.PartnerSignoff, finalEng.Status);
        Assert.Equal(AuditOpinion.Unmodified, finalEng.AuditOpinion);

        // 6. Summary KPI
        var summary = await http.GetFromJsonAsync<AuditSummaryDto>("/api/audit/summary");
        Assert.NotNull(summary);
        Assert.True(summary.TotalEngagements >= 1);
        Assert.True(summary.TotalWorkingPapers >= 6);
    }

    [Fact]
    public async Task Audit_TenantIsolation_CrossFirmAccessBlocked()
    {
        var (firmA, _) = await NewFirmAsync("Firm A Audit");
        var (firmB, _) = await NewFirmAsync("Firm B Audit");
        var clientA = await CreateClientAsync(firmA, "Client Alpha Industries");

        var createReq = new CreateAuditEngagementRequest(
            ClientId: clientA.Id,
            FinancialYear: "2024-25",
            AuditType: AuditType.Statutory,
            Title: "Statutory Audit Firm A",
            LeadPartnerId: null,
            ManagerId: null,
            PlannedStartDate: null,
            PlannedEndDate: null,
            BenchmarkType: BenchmarkType.Revenue,
            BenchmarkAmount: 10_000_000m,
            Notes: null
        );

        var resA = await firmA.PostAsJsonAsync("/api/audit/engagements", createReq);
        var engA = (await resA.Content.ReadFromJsonAsync<AuditEngagementDto>())!;

        // Firm B tries to get Firm A's audit engagement
        var getRes = await firmB.GetAsync($"/api/audit/engagements/{engA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Firm B tries to list Firm A's working papers
        var getWpsRes = await firmB.GetAsync($"/api/audit/engagements/{engA.Id}/working-papers");
        var wpsB = await getWpsRes.Content.ReadFromJsonAsync<List<AuditWorkingPaperDto>>();
        Assert.True(wpsB == null || wpsB.Count == 0);

        // Firm B tries to delete Firm A's engagement
        var delRes = await firmB.DeleteAsync($"/api/audit/engagements/{engA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }
}
