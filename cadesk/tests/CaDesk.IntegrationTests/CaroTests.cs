using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Caro;
using CaDesk.Application.Clients;
using CaDesk.Domain.Caro;
using Xunit;

namespace CaDesk.IntegrationTests;

public class CaroTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "KPMG Audit & Assurance LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Titan Engineering Pvt Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Caro_Applicability_Assessment_ClauseVerification_AnnexureGeneration_Finalize_And_TenantIsolation()
    {
        var (httpA, _) = await NewFirmAsync("BDO Statutory Audit Desk");
        var clientA = await CreateClientAsync(httpA, "Bharat Heavy Fabricators Pvt Ltd");

        // 1. Create CARO Assessment with Borrowings ₹1.5 Cr (breaching ₹1 Cr limit -> CARO applicable)
        var createReq = new SaveCaroAssessmentRequest
        {
            ClientId = clientA.Id,
            FinancialYear = "2024-25",
            CompanyType = CaroCompanyType.Private,
            IsHoldingOrSubsidiaryOfPublic = false,
            PaidUpCapitalAndReserves = 5_000_000m, // ₹50L <= ₹1 Cr
            TotalBorrowings = 15_000_000m,         // ₹1.5 Cr > ₹1 Cr (Breached)
            TotalRevenue = 60_000_000m             // ₹6 Cr <= ₹10 Cr
        };

        var res = await httpA.PostAsJsonAsync("/api/caro/assessments", createReq);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<CaroAssessmentDto>();
        Assert.NotNull(created);
        Assert.True(created.IsCaroApplicable);
        Assert.Contains("Total borrowings", created.ExemptionReason);
        Assert.NotEmpty(created.ClauseResponses);
        Assert.True(created.ClauseResponses.Count >= 21);

        // 2. Update Clause ii(b) (Quarterly returns with banks) with a Qualification
        var clauseIib = created.ClauseResponses.First(c => c.ClauseNumber == "ii(b)");
        var updateClauseReq = new UpdateClauseResponseRequest
        {
            Status = CaroClauseStatus.Qualified,
            AuditorRemark = "Quarterly stock statements submitted to State Bank of India showed variance of ₹15 Lakhs against book records.",
            HasDiscrepancy = true,
            DiscrepancyAmount = 1_500_000m,
            SupportingWpsRef = "WP-INV-04"
        };

        var clauseRes = await httpA.PutAsJsonAsync($"/api/caro/assessments/{created.Id}/clauses/{clauseIib.Id}", updateClauseReq);
        clauseRes.EnsureSuccessStatusCode();
        var afterClause = await clauseRes.Content.ReadFromJsonAsync<CaroAssessmentDto>();
        Assert.NotNull(afterClause);
        Assert.Equal(1, afterClause.QualificationsCount);
        Assert.Contains("QUALIFICATION:", afterClause.GeneratedAnnexureText);
        Assert.Contains("variance of ₹15 Lakhs", afterClause.GeneratedAnnexureText);

        // 3. Finalize Assessment
        var finalizeRes = await httpA.PostAsJsonAsync($"/api/caro/assessments/{created.Id}/finalize", new FinalizeCaroRequest
        {
            Conclusion = "CARO 2020 Annexure finalized with 1 qualification on Clause ii(b)."
        });
        finalizeRes.EnsureSuccessStatusCode();
        var finalized = await finalizeRes.Content.ReadFromJsonAsync<CaroAssessmentDto>();
        Assert.NotNull(finalized);
        Assert.Equal(CaroAssessmentStatus.Finalized, finalized.Status);
        Assert.NotNull(finalized.FinalizedAt);

        // 4. Check summary
        var sumRes = await httpA.GetAsync("/api/caro/summary");
        sumRes.EnsureSuccessStatusCode();
        var summary = await sumRes.Content.ReadFromJsonAsync<CaroSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalAssessments);
        Assert.Equal(1, summary.ApplicableCount);
        Assert.Equal(1, summary.FinalizedCount);
        Assert.Equal(1, summary.TotalQualifications);

        // 5. Tenant Isolation: Firm B cannot access Firm A's CARO assessment
        var (httpB, _) = await NewFirmAsync("Deloitte Audit Practice LLP");
        var crossRes = await httpB.GetAsync($"/api/caro/assessments/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossRes.StatusCode);
    }
}
