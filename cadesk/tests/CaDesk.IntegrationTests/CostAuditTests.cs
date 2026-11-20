using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.CostAudit;
using CaDesk.Domain.CostAudit;
using Xunit;

namespace CaDesk.IntegrationTests;

public class CostAuditTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPanAndCin = new()
    {
        ["PAN"] = "AABCT1234F",
        ["CIN"] = "L24230MH1990PLC056789"
    };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "BDO Cost & Management Advisory LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Sun Pharma Industries Limited")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPanAndCin, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task CostAudit_FullLifecycle_Engagement_CRA1to4_Certification_And_TwoLock_Isolation()
    {
        var (httpA, _) = await NewFirmAsync("Deloitte Cost Audit Practice");
        var clientA = await CreateClientAsync(httpA, "Cipla Healthcare Limited");

        // 1. Applicability Check API
        var appCheckRes = await httpA.PostAsJsonAsync("/api/cost-audit/applicability-check", new ApplicabilityCheckRequest(
            SectorType: CostAuditConstants.SectorRegulated,
            OverallTurnoverInr: 120_00_00_000m,
            ProductTurnoverInr: 60_00_00_000m,
            ExportTurnoverInr: 15_00_00_000m,
            IsSezUnit: false,
            IsCaptivePower: false));
        appCheckRes.EnsureSuccessStatusCode();
        var appCheck = (await appCheckRes.Content.ReadFromJsonAsync<ApplicabilityCheckResult>())!;
        Assert.True(appCheck.IsCostRecordsApplicable);
        Assert.True(appCheck.IsCostAuditApplicable);
        Assert.False(appCheck.IsExportExempt);

        // 2. Material Variance Calculator API
        var varRes = await httpA.PostAsJsonAsync("/api/cost-audit/variance-calculate", new MaterialVarianceRequest(
            StandardQuantity: 500m,
            ActualQuantity: 520m,
            StandardPrice: 200m,
            ActualPrice: 195m));
        varRes.EnsureSuccessStatusCode();
        var varCalc = (await varRes.Content.ReadFromJsonAsync<MaterialVarianceResult>())!;
        Assert.Equal(2600m, varCalc.PriceVariance);
        Assert.Equal(-4000m, varCalc.UsageVariance);
        Assert.Equal(-1400m, varCalc.TotalCostVariance);

        // 3. Create Cost Audit Engagement
        var createEngReq = new CreateCostAuditEngagementRequest(
            ClientId: clientA.Id,
            EngagementReference: "CA-2024-CIPLA-01",
            FinancialYear: "2024-25",
            PeriodStart: new DateTime(2024, 4, 1),
            PeriodEnd: new DateTime(2025, 3, 31),
            SectorType: CostAuditConstants.SectorRegulated,
            SectorCategory: CostAuditConstants.CatDrugsPharma,
            OverallTurnoverInr: 120_00_00_000m,
            ProductTurnoverInr: 60_00_00_000m,
            ExportTurnoverInr: 15_00_00_000m,
            IsSezExempt: false,
            IsCaptivePowerExempt: false,
            BoardResolutionDate: new DateTime(2024, 5, 20),
            FyCommencementDate: new DateTime(2024, 4, 1),
            AuditCommitteeRecommended: true,
            CostAuditorName: "CMA Rajesh Verma",
            CostAuditorFirmName: "Verma & Associates, Cost Accountants",
            CostAuditorMembershipNo: "FCMA 45678",
            CostAuditorFirmRegNo: "000567",
            AuditedFinancialProfitInr: 85_00_000m,
            SeedDefaultHeads: true);

        var engRes = await httpA.PostAsJsonAsync("/api/cost-audit/engagements", createEngReq);
        engRes.EnsureSuccessStatusCode();
        var engagement = (await engRes.Content.ReadFromJsonAsync<CostAuditEngagementDto>())!;
        Assert.NotEqual(Guid.Empty, engagement.Id);
        Assert.Equal(30, engagement.RecordHeads.Count); // 30 standard heads seeded
        Assert.Equal(new DateTime(2024, 5, 20).AddDays(30), engagement.Cra2FilingDueDate);

        // 4. Update a CRA-1 Cost Record Head
        var firstHead = engagement.RecordHeads[0];
        var updateHeadRes = await httpA.PutAsJsonAsync(
            $"/api/cost-audit/engagements/{engagement.Id}/heads/{firstHead.Id}",
            new UpdateRecordHeadRequest(
                Status: CostAuditConstants.HeadCompliant,
                RecordMaintenanceSystem: "SAP S/4HANA Material Ledger",
                VerificationNotes: "Stores ledger matched with physical verification sheets without variances.",
                DeficiencyOrAnomaly: null,
                CorrectiveActionRequired: null));
        updateHeadRes.EnsureSuccessStatusCode();

        // 5. Add Product Cost Statement (Part B)
        var addProdRes = await httpA.PostAsJsonAsync(
            $"/api/cost-audit/engagements/{engagement.Id}/products",
            new CreateProductCostStatementRequest(
                ProductCode: "CIP-FORM-01",
                ProductName: "Amoxicillin 250mg Capsules",
                CtaHeading: "3004",
                UnitOfMeasurement: "MT",
                InstalledCapacity: 500m,
                AvailableCapacity: 400m,
                ActualProduction: 350m,
                DirectMaterialsCost: 35_00_000m,
                DirectLaborCost: 12_00_000m,
                UtilitiesCost: 6_00_000m,
                OtherDirectExpenses: 2_00_000m,
                WorksOverheads: 9_00_000m,
                QualityControlCost: 3_00_000m,
                RndExpenses: 2_50_000m,
                AdministrationOverheads: 4_50_000m,
                OpeningWipCost: 3_00_000m,
                ClosingWipCost: 2_00_000m,
                OpeningFinishedGoodsCost: 5_00_000m,
                ClosingFinishedGoodsCost: 3_00_000m,
                SellingAndDistributionOverheads: 8_00_000m,
                NetSalesRealization: 95_00_000m));
        addProdRes.EnsureSuccessStatusCode();
        var prodUpdated = (await addProdRes.Content.ReadFromJsonAsync<CostAuditEngagementDto>())!;
        Assert.Single(prodUpdated.ProductStatements);
        var prod = prodUpdated.ProductStatements[0];
        Assert.Equal(55_00_000m, prod.PrimeCost);
        Assert.Equal(64_00_000m, prod.WorksCost);
        Assert.Equal(75_00_000m, prod.CostOfProduction);
        Assert.Equal(85_00_000m, prod.CostOfSales);
        Assert.Equal(10_00_000m, prod.ProductMarginInr);

        // 6. Add Reconciliation Item (Part D)
        var addRecRes = await httpA.PostAsJsonAsync(
            $"/api/cost-audit/engagements/{engagement.Id}/reconciliation",
            new CreateReconciliationItemRequest(
                ItemType: CostAuditConstants.RecAddIncomeNotCosted,
                Description: "Dividend from Subsidiary Companies",
                AmountInr: 75_00_000m,
                Impact: "Positive",
                FinancialScheduleRef: "Note 22 Other Income",
                Notes: "Credited directly to financial P&L"));
        addRecRes.EnsureSuccessStatusCode();
        var recUpdated = (await addRecRes.Content.ReadFromJsonAsync<CostAuditEngagementDto>())!;
        Assert.Single(recUpdated.ReconciliationItems);
        // Cost profit (10L) + Dividend (75L) = 85L, equals Audited Financial Profit (85L) -> diff = 0
        Assert.Equal(85_00_000m, recUpdated.ReconciledFinancialProfitInr);
        Assert.Equal(0m, recUpdated.NetReconciliationDifferenceInr);

        // 7. Record Form CRA-2 Filing
        var cra2Res = await httpA.PostAsJsonAsync(
            $"/api/cost-audit/engagements/{engagement.Id}/cra2-filing",
            new RecordCra2FilingRequest(
                Cra2Srn: "F12345678",
                Cra2FiledDate: new DateTime(2024, 6, 1),
                Cra2FilingStatus: CostAuditConstants.FilingStatusFiledOnTime));
        cra2Res.EnsureSuccessStatusCode();

        // 8. Generate Form CRA-3 Report
        var genReportRes = await httpA.PostAsJsonAsync($"/api/cost-audit/engagements/{engagement.Id}/generate-cra3", new { });
        genReportRes.EnsureSuccessStatusCode();
        var reportUpdated = (await genReportRes.Content.ReadFromJsonAsync<CostAuditEngagementDto>())!;
        Assert.Contains("FORM NO. CRA-3", reportUpdated.CostAuditReportText);
        Assert.Contains("Verma & Associates, Cost Accountants", reportUpdated.CostAuditReportText);
        Assert.Contains("PART D: PROFIT RECONCILIATION WITH FINANCIAL ACCOUNTS", reportUpdated.CostAuditReportText);

        // 9. Certify with 18-digit UDIN
        var certRes = await httpA.PostAsJsonAsync(
            $"/api/cost-audit/engagements/{engagement.Id}/certify",
            new CertifyEngagementRequest(
                Udin: "24456789BBBBBB1234",
                CertifiedBy: "CMA Rajesh Verma"));
        certRes.EnsureSuccessStatusCode();
        var certified = (await certRes.Content.ReadFromJsonAsync<CostAuditEngagementDto>())!;
        Assert.Equal("Certified", certified.AuditStatus);
        Assert.Equal("24456789BBBBBB1234", certified.Udin);
        Assert.NotNull(certified.CertifiedAt);

        // 10. Multi-tenant two-lock isolation: Firm B cannot access Firm A's engagement
        var (httpB, _) = await NewFirmAsync("KPMG Cost Advisory Practice");
        var getBRes = await httpB.GetAsync($"/api/cost-audit/engagements/{engagement.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getBRes.StatusCode);
    }
}
