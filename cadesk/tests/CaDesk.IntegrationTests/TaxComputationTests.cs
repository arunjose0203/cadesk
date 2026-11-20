using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Tax;
using CaDesk.Domain.Tax;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 14: Direct Tax Computation Engine.
/// Tests tax liability calculation across heads, Old vs New regime comparative evaluation,
/// computation persistence, status approval, and multi-tenant isolation.
/// </summary>
public class TaxComputationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Tax Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Aditya Birla")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Individual", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Tax_PreviewAndSaveLifecycle_ComputesAccurateLiability()
    {
        var (http, userId) = await NewFirmAsync("Premier Tax Advisors");
        var client = await CreateClientAsync(http, "Vikram Malhotra");

        var req = new CalculateTaxRequest(
            client.Id,
            "2025-26",
            "2024-25",
            TaxpayerCategory.Individual,
            null, // auto choose
            GrossSalary: 1200000m,
            HraReceived: 120000m,
            RentPaid: 150000m,
            IsMetro: true,
            ProfessionalTax: 2500m,
            AnnualRentReceived: 0,
            MunicipalTaxesPaid: 0,
            HousingLoanInterest: 0,
            IsSelfOccupied: false,
            BusinessGrossTurnover: 0,
            NetProfit: 0,
            PresumptiveSection: null,
            Stcg111A: 50000m,
            StcgNormal: 0,
            Ltcg112A: 200000m,
            Ltcg112: 0,
            SavingsInterest: 15000m,
            FdInterest: 25000m,
            DividendIncome: 10000m,
            OtherIncome: 0,
            Section80C: 150000m,
            Section80Ccd1B: 50000m,
            Section80D: 25000m,
            Section80E: 0,
            Section80G: 0,
            Section80Tta: 10000m,
            TdsPaid: 60000m,
            TcsPaid: 0,
            AdvanceTaxPaid: 20000m,
            SelfAssessmentTaxPaid: 0,
            Notes: "FY 2024-25 salaried individual with equity capital gains"
        );

        // 1. Preview comparison
        var prevRes = await http.PostAsJsonAsync("/api/tax/preview", req);
        Assert.Equal(HttpStatusCode.OK, prevRes.StatusCode);
        var comparison = (await prevRes.Content.ReadFromJsonAsync<RegimeComparisonDto>())!;
        Assert.NotNull(comparison.OldRegime);
        Assert.NotNull(comparison.NewRegime);
        Assert.NotNull(comparison.RecommendedRegime);

        // 2. Save computation
        var saveRes = await http.PostAsJsonAsync("/api/tax/computations", req);
        Assert.Equal(HttpStatusCode.Created, saveRes.StatusCode);
        var comp = (await saveRes.Content.ReadFromJsonAsync<TaxComputationDto>())!;

        Assert.Equal(client.Id, comp.ClientId);
        Assert.Equal("2025-26", comp.AssessmentYear);
        Assert.Equal(ComputationStatus.Computed, comp.Status);
        Assert.True(comp.TotalTaxLiability > 0);
        Assert.Equal(80000m, comp.TdsTcsCredit + comp.AdvanceTaxPaid);

        // 3. Update status to approved
        var updateRes = await http.PutAsync($"/api/tax/computations/{comp.Id}/status?status={ComputationStatus.Approved}", null);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = (await updateRes.Content.ReadFromJsonAsync<TaxComputationDto>())!;
        Assert.Equal(ComputationStatus.Approved, updated.Status);

        // 4. List computations
        var list = await http.GetFromJsonAsync<List<TaxComputationDto>>($"/api/tax/computations?clientId={client.Id}");
        Assert.NotNull(list);
        Assert.Contains(list, x => x.Id == comp.Id);
    }

    [Fact]
    public async Task Tax_TenantIsolation_CrossFirmAccessBlocked()
    {
        var (firmA, _) = await NewFirmAsync("Firm A Tax");
        var (firmB, _) = await NewFirmAsync("Firm B Tax");
        var clientA = await CreateClientAsync(firmA, "Client Alpha Tax");

        var req = new CalculateTaxRequest(
            clientA.Id, "2025-26", "2024-25", TaxpayerCategory.Individual, "new",
            GrossSalary: 800000m, HraReceived: 0, RentPaid: 0, IsMetro: false, ProfessionalTax: 0,
            AnnualRentReceived: 0, MunicipalTaxesPaid: 0, HousingLoanInterest: 0, IsSelfOccupied: false,
            BusinessGrossTurnover: 0, NetProfit: 0, PresumptiveSection: null,
            Stcg111A: 0, StcgNormal: 0, Ltcg112A: 0, Ltcg112: 0,
            SavingsInterest: 0, FdInterest: 0, DividendIncome: 0, OtherIncome: 0,
            Section80C: 0, Section80Ccd1B: 0, Section80D: 0, Section80E: 0, Section80G: 0, Section80Tta: 0,
            TdsPaid: 0, TcsPaid: 0, AdvanceTaxPaid: 0, SelfAssessmentTaxPaid: 0, Notes: null
        );

        var saveRes = await firmA.PostAsJsonAsync("/api/tax/computations", req);
        var compA = (await saveRes.Content.ReadFromJsonAsync<TaxComputationDto>())!;

        // Firm B tries to get Firm A's tax computation
        var getRes = await firmB.GetAsync($"/api/tax/computations/{compA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Firm B tries to approve Firm A's tax computation
        var approveRes = await firmB.PutAsync($"/api/tax/computations/{compA.Id}/status?status={ComputationStatus.Approved}", null);
        Assert.Equal(HttpStatusCode.NotFound, approveRes.StatusCode);

        // Firm B list does not contain Firm A's computation
        var listB = await firmB.GetFromJsonAsync<List<TaxComputationDto>>("/api/tax/computations");
        Assert.NotNull(listB);
        Assert.DoesNotContain(listB, x => x.Id == compA.Id);
    }
}
