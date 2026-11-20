using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.Presumptive;
using CaDesk.Domain.Presumptive;
using Xunit;

namespace CaDesk.IntegrationTests;

public class PresumptiveTaxTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Arun & Associates CA")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Shree Balaji Traders")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Individual", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task PresumptiveTax_Section44AD_Lifecycle_MinimalFinancials_Approve_And_TenantIsolation()
    {
        var (httpA, _) = await NewFirmAsync("KPMG Tax Advisory Firm");
        var clientA = await CreateClientAsync(httpA, "Gupta Electronics & Hardware");

        // 1. Create Section 44AD computation
        var saveReq = new SavePresumptiveComputationRequest
        {
            ClientId = clientA.Id,
            FinancialYear = "2024-25",
            Section = PresumptiveSection.Sec44AD,
            EntityType = "Individual",
            BusinessNatureCode = "01001",
            TradeName = "Gupta Electronics Retail",
            DigitalTurnover = 18_000_000m,
            CashTurnover = 500_000m,
            DeclaredProfit = 1_200_000m,
            SundryDebtors = 400_000m,
            SundryCreditors = 300_000m,
            StockInTrade = 200_000m,
            CashBalance = 50_000m,
            BankBalance = 650_000m,
            FixedAssets = 0m,
            OtherAssets = 0m,
            PartnerOrProprietorCapital = 1_000_000m,
            LoansAndBorrowings = 0m,
            Notes = "Retail electronics store opting for Section 44AD"
        };

        var res = await httpA.PostAsJsonAsync("/api/presumptive/computations", saveReq);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<PresumptiveTaxComputationDto>();
        Assert.NotNull(created);
        Assert.Equal(18_500_000m, created.GrossTurnover);
        Assert.True(created.IsDigitalThresholdEligible); // Cash 5L / 185L = 2.7% <= 5%
        Assert.Equal(PresumptiveTaxEngine.Sec44ADEnhancedLimit, created.TurnoverLimit); // ₹3 Cr
        Assert.True(created.IsTurnoverWithinLimit);
        // Min profit: 1.8 Cr * 6% = 10.8L + 5L * 8% = 40K -> 11.20L
        Assert.Equal(1_120_000m, created.MinimumDeemedProfit);
        Assert.False(created.IsAuditMandatory);

        // 2. Fetch Minimal Financials Statement
        var stmtRes = await httpA.GetAsync($"/api/presumptive/computations/{created.Id}/statement");
        stmtRes.EnsureSuccessStatusCode();
        var stmt = await stmtRes.Content.ReadFromJsonAsync<MinimalFinancialsStatementDto>();
        Assert.NotNull(stmt);
        Assert.Equal(1_300_000m, stmt.TotalAssets); // Debtors 4L + Stock 2L + Cash 50K + Bank 6.5L = 13L
        Assert.Equal(1_300_000m, stmt.TotalLiabilitiesAndCapital); // Creditors 3L + Capital 10L = 13L
        Assert.True(stmt.IsBalanced);
        Assert.Empty(stmt.ValidationAlerts);

        // 3. Approve computation
        var appRes = await httpA.PostAsync($"/api/presumptive/computations/{created.Id}/approve", null);
        appRes.EnsureSuccessStatusCode();
        var approved = await appRes.Content.ReadFromJsonAsync<PresumptiveTaxComputationDto>();
        Assert.NotNull(approved);
        Assert.Equal(PresumptiveStatus.Approved, approved.Status);

        // 4. Check summary
        var sumRes = await httpA.GetAsync("/api/presumptive/summary");
        sumRes.EnsureSuccessStatusCode();
        var summary = await sumRes.Content.ReadFromJsonAsync<PresumptiveSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalComputations);
        Assert.Equal(1, summary.Sec44AdCount);
        Assert.Equal(18_500_000m, summary.TotalPresumptiveTurnover);

        // 5. Tenant Isolation: Firm B cannot access Firm A's computation
        var (httpB, _) = await NewFirmAsync("Deloitte Tax Services LLP");
        var crossRes = await httpB.GetAsync($"/api/presumptive/computations/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossRes.StatusCode);
    }

    [Fact]
    public async Task PresumptiveTax_Section44AE_Vehicles_HeavyAndLight_DeemedProfitCalculation()
    {
        var (http, _) = await NewFirmAsync("BDO Logistics Audit Desk");
        var client = await CreateClientAsync(http, "Mahavir Roadlines Transporters");

        // 1. Create Section 44AE computation
        var saveReq = new SavePresumptiveComputationRequest
        {
            ClientId = client.Id,
            FinancialYear = "2024-25",
            Section = PresumptiveSection.Sec44AE,
            EntityType = "Firm",
            BusinessNatureCode = "07001",
            TradeName = "Mahavir Roadlines",
            Notes = "Goods transport fleet"
        };

        var res = await http.PostAsJsonAsync("/api/presumptive/computations", saveReq);
        res.EnsureSuccessStatusCode();
        var comp = await res.Content.ReadFromJsonAsync<PresumptiveTaxComputationDto>();
        Assert.NotNull(comp);

        // 2. Add Heavy Goods Vehicle (16 MT, 12 months) -> ₹1,000 * 16 * 12 = ₹1,92,000
        var v1Res = await http.PostAsJsonAsync($"/api/presumptive/computations/{comp.Id}/vehicles", new SaveVehicleRequest
        {
            RegistrationNumber = "MH-12-AB-1234",
            VehicleType = VehicleType.Heavy,
            GrossVehicleWeightTons = 16m,
            MonthsOwned = 12
        });
        v1Res.EnsureSuccessStatusCode();
        var afterV1 = await v1Res.Content.ReadFromJsonAsync<PresumptiveTaxComputationDto>();
        Assert.NotNull(afterV1);
        Assert.Single(afterV1.Vehicles);
        Assert.Equal(192_000m, afterV1.MinimumDeemedProfit);

        // 3. Add Light Goods Vehicle (8 MT, 12 months) -> ₹7,500 * 12 = ₹90,000
        var v2Res = await http.PostAsJsonAsync($"/api/presumptive/computations/{comp.Id}/vehicles", new SaveVehicleRequest
        {
            RegistrationNumber = "MH-12-CD-5678",
            VehicleType = VehicleType.Other,
            GrossVehicleWeightTons = 8m,
            MonthsOwned = 12
        });
        v2Res.EnsureSuccessStatusCode();
        var afterV2 = await v2Res.Content.ReadFromJsonAsync<PresumptiveTaxComputationDto>();
        Assert.NotNull(afterV2);
        Assert.Equal(2, afterV2.Vehicles.Count);
        // Total: 1,92,000 + 90,000 = 2,82,000
        Assert.Equal(282_000m, afterV2.MinimumDeemedProfit);
        Assert.False(afterV2.IsAuditMandatory);

        // 4. Remove vehicle 1
        var delRes = await http.DeleteAsync($"/api/presumptive/computations/{comp.Id}/vehicles/{afterV2.Vehicles[0].Id}");
        delRes.EnsureSuccessStatusCode();
        var afterDel = await delRes.Content.ReadFromJsonAsync<PresumptiveTaxComputationDto>();
        Assert.NotNull(afterDel);
        Assert.Single(afterDel.Vehicles);
        Assert.Equal(90_000m, afterDel.MinimumDeemedProfit);
    }
}
