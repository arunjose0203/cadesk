using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Api.Endpoints;
using CaDesk.Application.Clients;
using CaDesk.Application.Schedule3;
using CaDesk.Domain.Schedule3;
using Xunit;

namespace CaDesk.IntegrationTests;

public class Schedule3Tests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "EY Assurance Services LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Mahindra Precision Engineering Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Schedule3_Assessment_ComputeRatios_ExplainVariances_UpdateAgeing_Finalize_And_TenantIsolation()
    {
        var (httpA, _) = await NewFirmAsync("Deloitte Statutory Audit Practice");
        var clientA = await CreateClientAsync(httpA, "Tata Advance Robotics Ltd");

        // 1. Create Assessment with inputs
        var createReq = new SaveSchedule3AssessmentRequest
        {
            ClientId = clientA.Id,
            FinancialYear = "2024-25",
            PrecedingFinancialYear = "2023-24",
            AccountingStandard = Schedule3Division.DivisionI_AS,
            RoundingUnit = Schedule3RoundingUnit.Lakhs,

            TotalIncomeCurrent = 85_00_00_000m,
            TotalIncomePreceding = 60_00_00_000m,

            CurrentAssetsCurrent = 30_00_00_000m,
            CurrentAssetsPreceding = 20_00_00_000m,
            CurrentLiabilitiesCurrent = 15_00_00_000m,
            CurrentLiabilitiesPreceding = 15_00_00_000m, // Current Ratio: CY = 2.00 vs PY = 1.33 (+50.38% >= 25%)

            TotalDebtCurrent = 25_00_00_000m,
            TotalDebtPreceding = 30_00_00_000m,
            ShareholdersEquityCurrent = 50_00_00_000m,
            ShareholdersEquityPreceding = 40_00_00_000m,

            NetProfitAfterTaxesCurrent = 10_00_00_000m,
            NetProfitAfterTaxesPreceding = 6_00_00_000m,
            RevenueFromOperationsCurrent = 80_00_00_000m,
            RevenueFromOperationsPreceding = 55_00_00_000m,

            CostOfGoodsSoldCurrent = 48_00_00_000m,
            CostOfGoodsSoldPreceding = 35_00_00_000m,
            AverageInventoryCurrent = 12_00_00_000m,
            AverageInventoryPreceding = 10_00_00_000m,

            AverageTradeReceivablesCurrent = 16_00_00_000m,
            AverageTradeReceivablesPreceding = 12_00_00_000m,
            NetCreditPurchasesCurrent = 40_00_00_000m,
            NetCreditPurchasesPreceding = 30_00_00_000m,
            AverageTradePayablesCurrent = 8_00_00_000m,
            AverageTradePayablesPreceding = 7_00_00_000m,

            EbitCurrent = 15_00_00_000m,
            EbitPreceding = 10_00_00_000m,
            DebtServiceCurrent = 5_00_00_000m,
            DebtServicePreceding = 5_00_00_000m,
            CapitalEmployedCurrent = 75_00_00_000m,
            CapitalEmployedPreceding = 70_00_00_000m,

            IncomeFromInvestmentsCurrent = 2_00_00_000m,
            IncomeFromInvestmentsPreceding = 1_50_00_000m,
            AverageInvestmentsCurrent = 20_00_00_000m,
            AverageInvestmentsPreceding = 18_00_00_000m
        };

        var res = await httpA.PostAsJsonAsync("/api/schedule3/assessments", createReq);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<Schedule3AssessmentDto>();
        Assert.NotNull(created);
        Assert.True(created.IsRoundingRuleCompliant);
        Assert.Equal(11, created.Ratios.Count);
        Assert.NotEmpty(created.AgeingItems);
        Assert.True(created.SignificantVariancesCount > 0);

        // 2. Find significant ratio (Current Ratio R01) and update mandatory explanation
        var cr = created.Ratios.First(r => r.RatioCode == "R01");
        Assert.True(cr.IsVarianceSignificant);

        var updateExpReq = new UpdateRatioExplanationRequest
        {
            VarianceExplanation = "Current Ratio improved due to liquidation of customer receivables and retention of operational profits."
        };

        var expRes = await httpA.PutAsJsonAsync($"/api/schedule3/assessments/{created.Id}/ratios/{cr.Id}", updateExpReq);
        expRes.EnsureSuccessStatusCode();
        var afterExp = await expRes.Content.ReadFromJsonAsync<Schedule3AssessmentDto>();
        Assert.NotNull(afterExp);
        var updatedCr = afterExp.Ratios.First(r => r.RatioCode == "R01");
        Assert.True(updatedCr.IsExplanationProvided);
        Assert.Contains("Current Ratio improved", afterExp.GeneratedDisclosureNoteText);

        // 3. Update Ageing Item (Trade Receivables)
        var trAgeing = afterExp.AgeingItems.First(x => x.ScheduleType == "TradeReceivables");
        var updateAgeingReq = new UpdateAgeingItemRequest
        {
            Less6MonthsOr1Year = 12_00_000m,
            OneToTwoYears = 3_00_000m,
            TwoToThreeYears = 50_000m,
            MoreThanThreeYears = 0m,
            UnbilledDues = 1_50_000m
        };

        var ageingRes = await httpA.PutAsJsonAsync($"/api/schedule3/assessments/{created.Id}/ageing/{trAgeing.Id}", updateAgeingReq);
        ageingRes.EnsureSuccessStatusCode();
        var afterAgeing = await ageingRes.Content.ReadFromJsonAsync<Schedule3AssessmentDto>();
        Assert.NotNull(afterAgeing);
        Assert.Contains("TRADE RECEIVABLES AGEING SCHEDULE", afterAgeing.GeneratedDisclosureNoteText);

        // 4. Finalize Assessment
        var finalizeRes = await httpA.PostAsJsonAsync($"/api/schedule3/assessments/{created.Id}/finalize", new FinalizeSchedule3Request(
            "Schedule III financial ratios and additional regulatory disclosures verified and signed off."
        ));
        finalizeRes.EnsureSuccessStatusCode();
        var finalized = await finalizeRes.Content.ReadFromJsonAsync<Schedule3AssessmentDto>();
        Assert.NotNull(finalized);
        Assert.Equal(Schedule3Status.Finalized, finalized.AssessmentStatus);
        Assert.NotNull(finalized.FinalizedAt);

        // 5. Verify Summary Endpoint
        var sumRes = await httpA.GetAsync("/api/schedule3/summary");
        sumRes.EnsureSuccessStatusCode();
        var sum = await sumRes.Content.ReadFromJsonAsync<Schedule3SummaryDto>();
        Assert.NotNull(sum);
        Assert.Equal(1, sum.TotalAssessments);
        Assert.Equal(1, sum.FinalizedCount);

        // 6. Tenant Isolation: Firm B cannot access Firm A's assessment
        var (httpB, _) = await NewFirmAsync("KPMG Corporate Reporting Practice");
        var resFirmB = await httpB.GetAsync($"/api/schedule3/assessments/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resFirmB.StatusCode);
    }
}
