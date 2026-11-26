using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CaDesk.Application.Clients;
using CaDesk.Application.Presumptive;
using CaDesk.Application.Tax;
using CaDesk.Application.TaxAudit;
using CaDesk.Domain.Presumptive;
using CaDesk.Domain.Tax;
using Xunit;

namespace CaDesk.IntegrationTests;

public class ItrAndForm3CdExportTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Sprint67_Itr1_Itr4_And_Form3Cd_SchemaValidatedJsonExports_WorkEndToEnd()
    {
        var auth = await api.SignUpAsync("Travancore Tax Partners");
        var http = api.WithToken(auth.AccessToken);

        var indRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Suresh Kumar", null, null, "Individual",
            new Dictionary<string, string> { ["PAN"] = "AAAPK1234D" },
            "suresh@travancore.test", "9847011223", null, null));
        indRes.EnsureSuccessStatusCode();
        var indClient = (await indRes.Content.ReadFromJsonAsync<ClientDto>())!;

        // 1. ITR-1 export from Tax Computation
        var taxCompRes = await http.PostAsJsonAsync("/api/tax/computations", new CalculateTaxRequest(
            ClientId: indClient.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            TaxpayerCategory: TaxpayerCategory.Individual,
            OptedRegime: "new",
            GrossSalary: 1_150_000m,
            SavingsInterest: 18_000m,
            TdsPaid: 42_000m));
        Assert.Equal(HttpStatusCode.Created, taxCompRes.StatusCode);
        var taxComp = (await taxCompRes.Content.ReadFromJsonAsync<TaxComputationDto>())!;

        var itr1Http = await http.PostAsJsonAsync($"/api/tax/computations/{taxComp.Id}/export-itr1",
            new ExportItrJsonRequest(FatherName: "Krishnan Nair", City: "Kochi", PinCode: "682031"));
        Assert.Equal(HttpStatusCode.OK, itr1Http.StatusCode);
        var itr1 = (await itr1Http.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("ITR-1", itr1.FormType);
        Assert.True(itr1.ValidAgainstSchema, string.Join("; ", itr1.Issues.Select(i => i.Message)));
        Assert.Equal(0, itr1.SchemaErrorCount);
        Assert.Equal("ITR-1", JsonNode.Parse(itr1.JsonContent)!["ITR"]!["ITR1"]!["Form_ITR1"]!["FormName"]!.GetValue<string>());

        // 2. ITR-4 export from Presumptive Tax (44AD)
        var presReq = new SavePresumptiveComputationRequest
        {
            ClientId = indClient.Id,
            FinancialYear = "2025-26",
            Section = PresumptiveSection.Sec44AD,
            EntityType = "Individual",
            BusinessNatureCode = "09028",
            TradeName = "Kumar Electricals",
            DigitalTurnover = 4_000_000m,
            CashTurnover = 200_000m,
            DeclaredProfit = 260_000m,
            SundryDebtors = 120_000m,
            StockInTrade = 180_000m,
            CashBalance = 15_000m,
            BankBalance = 85_000m,
            FixedAssets = 100_000m,
            SundryCreditors = 50_000m,
            PartnerOrProprietorCapital = 450_000m,
        };
        var presRes = await http.PostAsJsonAsync("/api/presumptive/computations", presReq);
        Assert.Equal(HttpStatusCode.Created, presRes.StatusCode);
        var presComp = (await presRes.Content.ReadFromJsonAsync<PresumptiveTaxComputationDto>())!;

        var itr4Http = await http.PostAsJsonAsync($"/api/presumptive/computations/{presComp.Id}/export-itr4",
            new ExportItrJsonRequest(FatherName: "Krishnan Nair", City: "Kochi", PinCode: "682031"));
        Assert.Equal(HttpStatusCode.OK, itr4Http.StatusCode);
        var itr4 = (await itr4Http.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("ITR-4", itr4.FormType);
        Assert.True(itr4.ValidAgainstSchema, string.Join("; ", itr4.Issues.Select(i => i.Message)));
        Assert.Equal(0, itr4.SchemaErrorCount);
        Assert.Equal(450_000L, JsonNode.Parse(itr4.JsonContent)!["ITR"]!["ITR4"]!["ScheduleBP"]!["FinanclPartclrOfBusiness"]!["PartnerMemberOwnCapital"]!.GetValue<long>());

        // 3. Form 3CA-3CD V2.5 JSON export & Vault Save
        var coRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Ernakulam Foods Pvt Ltd", null, null, "Company",
            new Dictionary<string, string> { ["PAN"] = "AAACA1234F", ["GSTIN"] = "32AAACA1234F1ZJ" },
            "audit@efoods.test", "9847055443", null, null));
        coRes.EnsureSuccessStatusCode();
        var coClient = (await coRes.Content.ReadFromJsonAsync<ClientDto>())!;

        var f3Res = await http.PostAsJsonAsync("/api/taxaudit/assessments", new SaveForm3CdAssessmentRequest
        {
            ClientId = coClient.Id,
            FinancialYear = "2025-26",
            AssessmentYear = "2026-27",
            AuditType = "Form3CA",
            TotalTurnoverOrGrossReceipts = 150_000_000m,
            TotalPayments = 110_000_000m,
            AssesseeName = "Ernakulam Foods Pvt Ltd",
            Pan = "AAACA1234F",
            Address = "MG Road, Kochi",
            Gstin = "32AAACA1234F1ZJ",
            Status = "Company",
            IsBusiness = true,
            IsAuditedUnderOtherLaw = true,
        });
        Assert.Equal(HttpStatusCode.Created, f3Res.StatusCode);
        var f3Assessment = (await f3Res.Content.ReadFromJsonAsync<Form3CdAssessmentDto>())!;

        var f3JsonHttp = await http.PostAsJsonAsync($"/api/taxaudit/assessments/{f3Assessment.Id}/export-json",
            new ExportForm3CdJsonRequest(AuditorName: "Meera CA", AuditorMembershipNo: "234567", FirmRegNum: "004321S", AuditorCity: "Kochi"));
        Assert.Equal(HttpStatusCode.OK, f3JsonHttp.StatusCode);
        var f3Json = (await f3JsonHttp.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("FORM3CA", f3Json.FormType);
        Assert.True(f3Json.ValidAgainstSchema, string.Join("; ", f3Json.Issues.Select(i => i.Message)));
        Assert.Equal(0, f3Json.SchemaErrorCount);

        // Save to client document vault
        var vaultRes = await http.PostAsJsonAsync($"/api/clients/{indClient.Id}/documents/generated", new
        {
            fileName = itr1.FileName,
            content = itr1.JsonContent,
            typeCode = "ITR",
            fy = "2025-26",
            name = "ITR-1 AY 2026-27 Official JSON"
        });
        Assert.Equal(HttpStatusCode.Created, vaultRes.StatusCode);

        // 4. Corporate ITR-6 prefill (pulling Form 3CD disallowances) -> save -> export-itr6 -> Vault Save
        var prefillCoRes = await http.PostAsJsonAsync("/api/tax/business/prefill", new BusinessPrefillRequest(coClient.Id, "2025-26", "2026-27", "company"));
        Assert.Equal(HttpStatusCode.OK, prefillCoRes.StatusCode);
        var prefillCo = (await prefillCoRes.Content.ReadFromJsonAsync<BusinessPrefillResponseDto>())!;
        Assert.Contains(prefillCo.Sources, s => s.Module == "TaxAudit3CD" && s.Found);

        var coTaxReq = prefillCo.PrefilledRequest with
        {
            RevenueFromOperations = 45_000_000m,
            PurchasesAndDirectCost = 30_000_000m,
            EmployeeBenefitExpense = 6_000_000m,
            DepreciationPerBooks = 700_000m,
            IncomeTaxDepreciationSec32 = 850_000m,
            OtherExpenses = 2_300_000m,
            TaxProvisionCurrent = 1_400_000m,
            AdvanceTaxPaid = 900_000m,
            TdsCredit = 120_000m,
            KeyPersons = [new KeyPersonInput("Rajan Varma", "MD", "ABCPV1234Q", "01234567")],
        };

        var coSaveRes = await http.PostAsJsonAsync("/api/tax/business/computations", coTaxReq);
        Assert.Equal(HttpStatusCode.Created, coSaveRes.StatusCode);
        var coTaxComp = (await coSaveRes.Content.ReadFromJsonAsync<TaxComputationDto>())!;
        Assert.Equal(TaxpayerCategory.Company, coTaxComp.TaxpayerCategory);
        Assert.NotNull(coTaxComp.BusinessComputation);

        var itr6Http = await http.PostAsJsonAsync($"/api/tax/computations/{coTaxComp.Id}/export-itr6",
            new ExportItrJsonRequest(VerificationName: "Rajan Varma", VerificationPan: "ABCPV1234Q", Capacity: "MD", City: "Kochi", PinCode: "682031"));
        Assert.Equal(HttpStatusCode.OK, itr6Http.StatusCode);
        var itr6 = (await itr6Http.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("ITR-6", itr6.FormType);
        Assert.True(itr6.ValidAgainstSchema, string.Join("; ", itr6.Issues.Select(i => i.Message)));
        Assert.Equal(0, itr6.SchemaErrorCount);
        Assert.Equal("ITR-6", JsonNode.Parse(itr6.JsonContent)!["ITR"]!["ITR6"]!["Form_ITR6"]!["FormName"]!.GetValue<string>());

        // 5. Firm ITR-5 Sec 40(b) computation -> save -> export-itr5
        var firmRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Kochi Pepper LLP", null, null, "LLP",
            new Dictionary<string, string> { ["PAN"] = "AAAFK5555B" },
            "partners@pepperllp.test", "9847099887", null, null));
        firmRes.EnsureSuccessStatusCode();
        var firmClient = (await firmRes.Content.ReadFromJsonAsync<ClientDto>())!;

        var firmReq = new CalculateBusinessTaxRequest(
            ClientId: firmClient.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            EntityType: "firm",
            SubType: "LLP",
            RevenueFromOperations: 18_000_000m,
            PurchasesAndDirectCost: 11_000_000m,
            EmployeeBenefitExpense: 2_500_000m,
            PartnerRemunerationDebited: 1_400_000m,
            PartnerInterestDebited: 240_000m,
            DepreciationPerBooks: 250_000m,
            IncomeTaxDepreciationSec32: 280_000m,
            OtherExpenses: 1_200_000m,
            AdvanceTaxPaid: 200_000m,
            Partners:
            [
                new PartnerShareInput("Arun Menon", "ABCPM1111K", "IND_WORKING", 50m, 1_000_000m, 12m, 120_000m, 700_000m),
                new PartnerShareInput("Meera Nair", "ABCPN2222L", "IND_WORKING", 50m, 1_000_000m, 12m, 120_000m, 700_000m),
            ]
        );

        var firmSaveRes = await http.PostAsJsonAsync("/api/tax/business/computations", firmReq);
        Assert.Equal(HttpStatusCode.Created, firmSaveRes.StatusCode);
        var firmComp = (await firmSaveRes.Content.ReadFromJsonAsync<TaxComputationDto>())!;
        Assert.Equal(TaxpayerCategory.Firm, firmComp.TaxpayerCategory);

        var itr5Http = await http.PostAsJsonAsync($"/api/tax/computations/{firmComp.Id}/export-itr5",
            new ExportItrJsonRequest(VerificationName: "Arun Menon", VerificationPan: "ABCPM1111K", Capacity: "DP", City: "Kochi", PinCode: "682031"));
        Assert.Equal(HttpStatusCode.OK, itr5Http.StatusCode);
        var itr5 = (await itr5Http.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("ITR-5", itr5.FormType);
        Assert.True(itr5.ValidAgainstSchema, string.Join("; ", itr5.Issues.Select(i => i.Message)));
        Assert.Equal(0, itr5.SchemaErrorCount);
        Assert.Equal("ITR-5", JsonNode.Parse(itr5.JsonContent)!["ITR"]!["ITR5"]!["Form_ITR5"]!["FormName"]!.GetValue<string>());
    }
}
