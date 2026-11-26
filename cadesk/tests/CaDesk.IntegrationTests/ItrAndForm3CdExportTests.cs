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

    [Fact]
    public async Task Itr2_CapGainsVdaForeignAssets_And_Itr3_ProprietorPrefill_ValidateAgainstAy2026Schemas()
    {
        var auth = await api.SignUpAsync("Malabar Tax & Advisory LLP");
        var http = api.WithToken(auth.AccessToken);

        // 1. ITR-2 Client: Individual with Multiple Employers, Multi-HP, STCG 111A, LTCG 112A/112, VDA 115BBH, ScheduleFA & ScheduleAL
        var hniRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Vikram Menon", null, null, "Individual",
            new Dictionary<string, string> { ["PAN"] = "ABCPV2222K" },
            "vikram@menon.test", "9847011223", null, null));
        hniRes.EnsureSuccessStatusCode();
        var hniClient = (await hniRes.Content.ReadFromJsonAsync<ClientDto>())!;

        var itr2Req = new CalculatePersonalExtendedTaxRequest(
            ClientId: hniClient.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            ReturnFormType: "ITR-2",
            TaxpayerCategory: "individual",
            StatusType: "I",
            OptedRegime: "auto",
            GrossSalary: 4_800_000m,
            ProfessionalTax: 2_500m,
            Employers:
            [
                new SalaryEmployerInput("Infosys Ltd", "BLRI01234A", "OTH", 2_800_000m, 420_000m),
                new SalaryEmployerInput("Zoho Corp Pvt Ltd", "CHEZ09876B", "OTH", 2_000_000m, 310_000m),
            ],
            HouseProperties:
            [
                new HousePropertyInput("Flat 101 Panampilly Nagar, Kochi", "Kochi", "16", "682036", "S", 0m, 0m, 180_000m),
                new HousePropertyInput("Villa 12 Kakkanad, Kochi", "Kochi", "16", "682030", "L", 360_000m, 15_000m, 90_000m),
            ],
            Stcg111ASaleValue: 600_000m,
            Stcg111ACost: 380_000m,
            Stcg111A: 220_000m,
            Ltcg112ASaleValue: 1_250_000m,
            Ltcg112ACost: 750_000m,
            Ltcg112A: 500_000m,
            Ltcg112SaleValue: 1_500_000m,
            Ltcg112Cost: 1_100_000m,
            Ltcg112: 400_000m,
            VdaConsideration: 450_000m,
            VdaCostOfAcquisition: 250_000m,
            VdaIncome115BBH: 200_000m,
            SavingsInterest: 24_000m,
            FdInterest: 65_000m,
            Section80C: 150_000m,
            Section80D: 50_000m,
            Section80Tta: 10_000m,
            TdsSalary: 730_000m,
            AdvanceTaxPaid: 150_000m,
            ForeignAssets:
            [
                new ForeignAssetInput("United States", "2", "Charles Schwab", "San Francisco CA", "94105", "CS99881", "2022-04-10", 2_100_000m, 1_850_000m, 0m),
            ],
            ScheduleAl: new ScheduleAlInput(12_000_000m, "Panampilly Nagar Residence", 1_500_000m, 4_500_000m, 500_000m, 0m, 75_000m, 400_000m, 1_200_000m, 2_400_000m));

        var itr2Save = await http.PostAsJsonAsync("/api/tax/personal/computations", itr2Req);
        Assert.Equal(HttpStatusCode.Created, itr2Save.StatusCode);
        var itr2Comp = (await itr2Save.Content.ReadFromJsonAsync<TaxComputationDto>())!;
        Assert.NotNull(itr2Comp.ExtendedPersonalComputation);
        Assert.Equal("ITR-2", itr2Comp.ExtendedPersonalComputation!.ReturnFormType);
        Assert.True(itr2Comp.ExtendedPersonalComputation.ActiveSummary.TaxOnVda115BBH >= 60_000m);

        var itr2Http = await http.PostAsJsonAsync($"/api/tax/computations/{itr2Comp.Id}/export-itr2",
            new ExportItrJsonRequest(VerificationName: "Vikram Menon", VerificationPan: "ABCPV2222K", Capacity: "S", City: "Kochi", PinCode: "682036"));
        Assert.Equal(HttpStatusCode.OK, itr2Http.StatusCode);
        var itr2 = (await itr2Http.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("ITR-2", itr2.FormType);
        Assert.True(itr2.ValidAgainstSchema, string.Join("; ", itr2.Issues.Select(i => $"{i.Path}: {i.Message}")));
        Assert.Equal(0, itr2.SchemaErrorCount);
        var itr2Node = JsonNode.Parse(itr2.JsonContent)!["ITR"]!["ITR2"]!;
        Assert.Equal("ITR-2", itr2Node["Form_ITR2"]!["FormName"]!.GetValue<string>());
        Assert.NotNull(itr2Node["ScheduleVDA"]);
        Assert.NotNull(itr2Node["ScheduleFA"]);
        Assert.NotNull(itr2Node["ScheduleAL"]);

        var vaultItr2 = await http.PostAsJsonAsync($"/api/clients/{hniClient.Id}/documents/generated", new
        {
            fileName = itr2.FileName,
            content = itr2.JsonContent,
            typeCode = "ITR",
            fy = "2025-26",
            name = "ITR-2 AY 2026-27 Official JSON"
        });
        Assert.Equal(HttpStatusCode.Created, vaultItr2.StatusCode);

        // 2. ITR-3 Proprietor Client: Form 3CD prefill -> save -> export-itr3 (schema validated)
        var propRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Nair Hardware HUF", null, null, "HUF",
            new Dictionary<string, string> { ["PAN"] = "AAAHN7777L" },
            "anoop@nairtrading.test", "9847033445", null, null));
        propRes.EnsureSuccessStatusCode();
        var propClient = (await propRes.Content.ReadFromJsonAsync<ClientDto>())!;

        var f3PropRes = await http.PostAsJsonAsync("/api/taxaudit/assessments", new SaveForm3CdAssessmentRequest
        {
            ClientId = propClient.Id,
            FinancialYear = "2025-26",
            AssessmentYear = "2026-27",
            AuditType = "Form3CB",
            Section44AbClause = "44AB(a)",
            TotalTurnoverOrGrossReceipts = 22_000_000m,
            AssesseeName = "Nair Hardware Trading",
            Pan = "ABCPN7777L",
            Address = "MG Road, Kochi",
        });
        Assert.Equal(HttpStatusCode.Created, f3PropRes.StatusCode);

        var prefillItr3Http = await http.PostAsJsonAsync("/api/tax/personal/prefill?returnFormType=ITR-3",
            new BusinessPrefillRequest(propClient.Id, "2025-26", "2026-27", "firm"));
        Assert.Equal(HttpStatusCode.OK, prefillItr3Http.StatusCode);
        var prefillItr3 = (await prefillItr3Http.Content.ReadFromJsonAsync<PersonalPrefillResponseDto>())!;
        Assert.Contains(prefillItr3.Sources, s => s.Module == "TaxAudit3CD" && s.Found);

        var itr3Req = prefillItr3.PrefilledRequest with
        {
            BusinessName = "Nair Hardware Trading",
            IsTaxAuditApplicable = true,
            PropCapital = 3_200_000m,
            ReservesAndSurplus = 800_000m,
            TradePayables = 950_000m,
            FixedAssetsNetBlock = 1_800_000m,
            Inventories = 1_650_000m,
            TradeReceivables = 900_000m,
            CashAndBankBalances = 600_000m,
            RevenueFromOperations = 22_000_000m,
            PurchasesAndDirectCost = 16_800_000m,
            EmployeeBenefitExpense = 1_450_000m,
            DepreciationPerBooks = 220_000m,
            OtherBusinessExpenses = 980_000m,
            IncomeTaxDepreciationSec32 = 270_000m,
            DisallowSec43BhMsme = 120_000m,
            DepreciationBlocks =
            [
                new DepreciationBlockInput("PM15", "Shop Equipment 15%", 15m, 1_800_000m, 0m, 0m, 0m, 0m, 270_000m),
            ],
            AdvanceTaxPaid = 400_000m,
        };

        var itr3Save = await http.PostAsJsonAsync("/api/tax/personal/computations", itr3Req);
        Assert.Equal(HttpStatusCode.Created, itr3Save.StatusCode);
        var itr3Comp = (await itr3Save.Content.ReadFromJsonAsync<TaxComputationDto>())!;
        Assert.NotNull(itr3Comp.ExtendedPersonalComputation);
        Assert.Equal("ITR-3", itr3Comp.ExtendedPersonalComputation!.ReturnFormType);

        var itr3Http = await http.PostAsJsonAsync($"/api/tax/computations/{itr3Comp.Id}/export-itr3",
            new ExportItrJsonRequest(VerificationName: "Anoop Nair", VerificationPan: "ABCPN7777L", Capacity: "K", City: "Kochi", PinCode: "682016"));
        Assert.Equal(HttpStatusCode.OK, itr3Http.StatusCode);
        var itr3 = (await itr3Http.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("ITR-3", itr3.FormType);
        Assert.True(itr3.ValidAgainstSchema, string.Join("; ", itr3.Issues.Select(i => $"{i.Path}: {i.Message}")));
        Assert.Equal(0, itr3.SchemaErrorCount);
        var itr3Node = JsonNode.Parse(itr3.JsonContent)!["ITR"]!["ITR3"]!;
        Assert.Equal("ITR-3", itr3Node["Form_ITR3"]!["FormName"]!.GetValue<string>());
        Assert.NotNull(itr3Node["ITR3ScheduleBP"]);
        Assert.NotNull(itr3Node["ScheduleDPM"]);
    }
}
