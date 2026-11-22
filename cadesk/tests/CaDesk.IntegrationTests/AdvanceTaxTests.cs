using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.AdvanceTax;
using CaDesk.Application.Clients;
using CaDesk.Domain.AdvanceTax;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 26: Advance Tax Planner, Self-Assessment & Section 234A/B/C Interest Engine.
/// Tests quarterly installment schedule generation, challan payment recording,
/// Section 234C, 234B, and 234A interest computations, and multi-tenant isolation.
/// </summary>
public class AdvanceTaxTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Tax Practice LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Apex Infotech Private Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task AdvanceTax_Lifecycle_GeneratesInstallments_RecordsPayment_And_CalculatesInterest()
    {
        var (http, _) = await NewFirmAsync("Deloitte Advance Tax Desk");
        var client = await CreateClientAsync(http, "Infosys BPO Solutions");

        // 1. Create Advance Tax Plan for FY 2024-25
        var planReq = new SaveAdvanceTaxPlanRequest(
            ClientId: client.Id,
            FinancialYear: "2024-25",
            AssessmentYear: "2025-26",
            Regime: AdvanceTaxRegime.NewRegime,
            IsPresumptive: false,
            IsSeniorCitizen: false,
            EstimatedGrossIncome: 1_200_000m,
            EstimatedDeductions: 100_000m,
            EstimatedTaxBeforeCredits: 150_000m,
            EstimatedTdsTcs: 50_000m,
            OtherReliefs: 0m,
            Notes: "FY 2024-25 Advance Tax Estimation"
        );

        var createRes = await http.PostAsJsonAsync("/api/advance-tax/plans", planReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var plan = await createRes.Content.ReadFromJsonAsync<AdvanceTaxPlanDto>();
        Assert.NotNull(plan);
        Assert.Equal(100_000m, plan.AssessedTax);

        // 2. Verify 4 installments generated
        var instRes = await http.GetAsync($"/api/advance-tax/plans/{plan.Id}/installments");
        Assert.Equal(HttpStatusCode.OK, instRes.StatusCode);
        var installments = await instRes.Content.ReadFromJsonAsync<List<AdvanceTaxInstallmentDto>>();
        Assert.NotNull(installments);
        Assert.Equal(4, installments.Count);
        Assert.Equal(15_000m, installments[0].CumulativeTargetAmount);
        Assert.Equal(45_000m, installments[1].CumulativeTargetAmount);
        Assert.Equal(75_000m, installments[2].CumulativeTargetAmount);
        Assert.Equal(100_000m, installments[3].CumulativeTargetAmount);

        // 3. Record installment payment of 5,000 in June (shortfall of 10,000 -> 300 interest)
        var payReq = new RecordInstallmentPaymentRequest(
            AmountPaid: 5_000m,
            ChallanBsrCode: "0210045",
            ChallanNumber: "CH-00123",
            ChallanDate: new DateOnly(2024, 6, 14)
        );

        var payRes = await http.PostAsJsonAsync($"/api/advance-tax/installments/{installments[0].Id}/pay", payReq);
        Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);
        var updatedInst = await payRes.Content.ReadFromJsonAsync<AdvanceTaxInstallmentDto>();
        Assert.NotNull(updatedInst);
        Assert.Equal(5_000m, updatedInst.AmountPaid);
        Assert.Equal(10_000m, updatedInst.ShortfallAmount);
        Assert.Equal(300m, updatedInst.Interest234c);

        // 4. Calculate full statutory interest u/s 234A, 234B, 234C
        var interestReq = new CalculateStatutoryInterestRequest(
            ReturnDueDate: new DateOnly(2025, 7, 31),
            ActualReturnFilingDate: new DateOnly(2025, 10, 15),
            SelfAssessmentTaxPaid: 10_000m
        );

        var calcRes = await http.PostAsJsonAsync($"/api/advance-tax/plans/{plan.Id}/calculate-interest", interestReq);
        Assert.Equal(HttpStatusCode.OK, calcRes.StatusCode);
        var comp = await calcRes.Content.ReadFromJsonAsync<TaxInterestComputationDto>();
        Assert.NotNull(comp);
        Assert.Equal(100_000m, comp.AssessedTax);
        Assert.Equal(5_000m, comp.AdvanceTaxPaid);
        Assert.True(comp.Is234bApplicable);
        Assert.True(comp.Interest234b > 0m);
        Assert.True(comp.Interest234a > 0m);
        Assert.True(comp.TotalStatutoryInterest > 0m);
        Assert.True(comp.NetTaxPayable > 0m);
    }

    [Fact]
    public async Task AdvanceTax_MultiTenant_Isolation_FirmB_Cannot_Access_FirmA_Plan()
    {
        var (httpA, _) = await NewFirmAsync("Firm A Advance Tax Advisory");
        var (httpB, _) = await NewFirmAsync("Firm B Unauthorized Entity");

        var clientA = await CreateClientAsync(httpA, "Client Alpha Tech");

        // Firm A creates plan
        var planReq = new SaveAdvanceTaxPlanRequest(
            ClientId: clientA.Id,
            FinancialYear: "2024-25",
            AssessmentYear: "2025-26",
            Regime: AdvanceTaxRegime.NewRegime,
            IsPresumptive: false,
            IsSeniorCitizen: false,
            EstimatedGrossIncome: 500_000m,
            EstimatedDeductions: 50_000m,
            EstimatedTaxBeforeCredits: 60_000m,
            EstimatedTdsTcs: 20_000m,
            OtherReliefs: 0m,
            Notes: "Confidential Alpha Plan"
        );

        var resA = await httpA.PostAsJsonAsync("/api/advance-tax/plans", planReq);
        Assert.Equal(HttpStatusCode.Created, resA.StatusCode);
        var planA = await resA.Content.ReadFromJsonAsync<AdvanceTaxPlanDto>();
        Assert.NotNull(planA);

        // Firm B tries to get Firm A's plan -> 404 NotFound
        var resBGet = await httpB.GetAsync($"/api/advance-tax/plans/{planA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resBGet.StatusCode);

        // Firm B lists plans -> empty list
        var resBList = await httpB.GetAsync("/api/advance-tax/plans");
        Assert.Equal(HttpStatusCode.OK, resBList.StatusCode);
        var listB = await resBList.Content.ReadFromJsonAsync<List<AdvanceTaxPlanDto>>();
        Assert.NotNull(listB);
        Assert.Empty(listB);

        // Firm B tries to calculate interest on Firm A's plan -> 404 NotFound
        var resBCalc = await httpB.PostAsJsonAsync($"/api/advance-tax/plans/{planA.Id}/calculate-interest", new CalculateStatutoryInterestRequest(
            ReturnDueDate: new DateOnly(2025, 7, 31),
            ActualReturnFilingDate: new DateOnly(2025, 8, 15),
            SelfAssessmentTaxPaid: 0m
        ));
        Assert.True(resBCalc.StatusCode == HttpStatusCode.NotFound, await resBCalc.Content.ReadAsStringAsync());
    }
}
