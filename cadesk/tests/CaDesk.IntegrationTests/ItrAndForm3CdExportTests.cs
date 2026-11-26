using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CaDesk.Application.Clients;
using CaDesk.Application.Presumptive;
using CaDesk.Application.Schedule3;
using CaDesk.Application.Tax;
using CaDesk.Application.TaxAudit;
using CaDesk.Application.TrialBalance;
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

    [Fact]
    public async Task CompetitiveGap_GranularCg_Itr7Trust_And_Schedule3FinancialPack_WorkEndToEnd()
    {
        var auth = await api.SignUpAsync("Ernakulam Advisory LLP");
        var http = api.WithToken(auth.AccessToken);

        // 1. ITR-2 with granular CapitalGainTrades (BE grandfathering + quarterly STCG 111A + VDA 115BBH)
        var investorRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Meera Thomas", null, null, "Individual",
            new Dictionary<string, string> { ["PAN"] = "ABCPM8888Q" },
            "meera@investor.test", "9847055667", null, null));
        investorRes.EnsureSuccessStatusCode();
        var investor = (await investorRes.Content.ReadFromJsonAsync<ClientDto>())!;

        var cgReq = new CalculatePersonalExtendedTaxRequest(
            ClientId: investor.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            ReturnFormType: "ITR-2",
            TaxpayerCategory: "individual",
            StatusType: "I",
            OptedRegime: "new",
            GrossSalary: 1_500_000m,
            TdsSalary: 180_000m,
            CapitalGainTrades:
            [
                new CapitalGainTradeEntry("Zerodha", "112A", "INE002A01018", "RELIANCE INDUSTRIES", "2016-05-10", "2025-11-15", 200m, 550_000m, 160_000m, 260_000m, 1_500m),
                new CapitalGainTradeEntry("Groww", "111A", "INE467B01029", "TCS", "2025-01-10", "2025-05-20", 40m, 220_000m, 150_000m, 0m, 500m),
                new CapitalGainTradeEntry("Zerodha", "VDA", "BTC", "Bitcoin", "2024-06-01", "2025-08-15", 0.2m, 400_000m, 250_000m, 0m, 0m),
            ]);

        var cgSave = await http.PostAsJsonAsync("/api/tax/personal/computations", cgReq);
        Assert.Equal(HttpStatusCode.Created, cgSave.StatusCode);
        var cgComp = (await cgSave.Content.ReadFromJsonAsync<TaxComputationDto>())!;
        Assert.Equal(288_500m, cgComp.ExtendedPersonalComputation!.ActiveSummary.Ltcg112A);
        Assert.Equal(150_000m, cgComp.ExtendedPersonalComputation.ActiveSummary.VdaIncome115BBH);

        var itr2ExportHttp = await http.PostAsJsonAsync($"/api/tax/computations/{cgComp.Id}/export-itr2",
            new ExportItrJsonRequest(VerificationName: "Meera Thomas", VerificationPan: "ABCPM8888Q", Capacity: "S", City: "Kochi", PinCode: "682011"));
        Assert.Equal(HttpStatusCode.OK, itr2ExportHttp.StatusCode);
        var itr2Export = (await itr2ExportHttp.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.True(itr2Export.ValidAgainstSchema, string.Join("; ", itr2Export.Issues.Select(i => $"{i.Path}: {i.Message}")));
        Assert.Equal(0, itr2Export.SchemaErrorCount);
        var itr2Root = JsonNode.Parse(itr2Export.JsonContent)!["ITR"]!["ITR2"]!;
        Assert.Equal("BE", itr2Root["Schedule112A"]!["Schedule112ADtls"]![0]!["ShareOnOrBefore"]!.GetValue<string>());
        Assert.Equal("INE002A01018", itr2Root["Schedule112A"]!["Schedule112ADtls"]![0]!["ISINCode"]!.GetValue<string>());
        Assert.True(itr2Root["ScheduleCGFor23"]!["AccruOrRecOfCG"]!["ShortTermUnder20Per"]!["DateRange"]!["Upto15Of6"]!.GetValue<long>() > 0);
        Assert.True(itr2Root["ScheduleCGFor23"]!["AccruOrRecOfCG"]!["LongTermUnder12_5Per"]!["DateRange"]!["Up16Of9To15Of12"]!.GetValue<long>() > 0);

        // 2. ITR-7 Charitable Trust u/s 11, 12AB & 115BBC -> save -> export-itr7 (schema validated)
        var trustRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Cochin Benevolent Foundation", null, null, "Trust",
            new Dictionary<string, string> { ["PAN"] = "AAATC4444E" },
            "trust@cochinbenevolent.test", "9847066778", null, null));
        trustRes.EnsureSuccessStatusCode();
        var trustClient = (await trustRes.Content.ReadFromJsonAsync<ClientDto>())!;

        var trustReq = new CalculateTrustTaxRequest(
            ClientId: trustClient.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            ReturnFurnishedSection: "139-4A",
            SectionExemptionClaimed: "11",
            StatusOrCompanyType: "5",
            Registration12AbNo: "AAATC4444EE20221",
            CorpusDonations80G: 500_000m,
            GrantsReceivedGovt: 1_200_000m,
            OtherSpecificAndGeneralDonations: 800_000m,
            AnonymousDonationsReceived: 300_000m,
            ReceiptsFromMainObjects: 1_500_000m,
            RevenueApplicationCharitable: 2_800_000m,
            CapitalApplicationCharitable: 400_000m,
            SpecifiedViolationIncome115BBI: 50_000m,
            CorpusFundClosing: 2_500_000m,
            AccumulatedFundClosing: 1_000_000m,
            FixedAssetsNetBlock: 2_000_000m,
            InvestmentsSec11_5: 1_000_000m,
            CashAndBankBalances: 500_000m,
            AdvanceTaxPaid: 60_000m);

        var trustSave = await http.PostAsJsonAsync("/api/tax/trust/computations", trustReq);
        Assert.Equal(HttpStatusCode.Created, trustSave.StatusCode);
        var trustComp = (await trustSave.Content.ReadFromJsonAsync<TaxComputationDto>())!;
        Assert.NotNull(trustComp.TrustComputation);
        Assert.True(trustComp.TrustComputation!.AnonymousDonationsTaxable115BBC > 0m);

        var itr7Http = await http.PostAsJsonAsync($"/api/tax/computations/{trustComp.Id}/export-itr7",
            new ExportItrJsonRequest(VerificationName: "Joseph Mathew", VerificationPan: "ABCPJ9999F", Capacity: "DR", City: "Kochi", PinCode: "682018"));
        Assert.Equal(HttpStatusCode.OK, itr7Http.StatusCode);
        var itr7 = (await itr7Http.Content.ReadFromJsonAsync<IncomeTaxJsonExportResult>())!;
        Assert.Equal("ITR-7", itr7.FormType);
        Assert.True(itr7.ValidAgainstSchema, string.Join("; ", itr7.Issues.Select(i => $"{i.Path}: {i.Message}")));
        Assert.Equal(0, itr7.SchemaErrorCount);
        var itr7Root = JsonNode.Parse(itr7.JsonContent)!["ITR"]!["ITR7"]!;
        Assert.Equal("ITR-7", itr7Root["Form_ITR7"]!["FormName"]!.GetValue<string>());
        Assert.True(itr7Root["ScheduleVC"]!["AnonymousDonations"]!["AnonymousDonations115BBC"]!.GetValue<long>() > 0);
        Assert.Equal(50_000L, itr7Root["Schedule115BBI"]!["Total"]!.GetValue<long>());

        // 3. Schedule III Statutory Financial Pack Data backed by comparative Trial Balances (2024-25 & 2025-26)
        var coRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Vembanad Foods Pvt Ltd", null, null, "Company",
            new Dictionary<string, string> { ["PAN"] = "AAACV6666M", ["CIN"] = "U15400KL2019PTC058123" },
            "accounts@vembanad.test", "9847077889", null, null));
        coRes.EnsureSuccessStatusCode();
        var coClient = (await coRes.Content.ReadFromJsonAsync<ClientDto>())!;

        string prevTbCsv = "Ledger Name,Group,Opening Balance,Debit,Credit,Closing Balance\n" +
                           "Equity Share Capital,Share Capital,0,0,1000000,-1000000\n" +
                           "Cash at Bank,Bank Accounts,0,1000000,0,1000000\n" +
                           "Domestic Sales,Sales Accounts,0,0,4200000,-4200000\n" +
                           "Purchases of RM,Purchase Accounts,0,2800000,0,2800000\n";
        string currTbCsv = "Ledger Name,Group,Opening Balance,Debit,Credit,Closing Balance\n" +
                           "Equity Share Capital,Share Capital,0,0,1000000,-1000000\n" +
                           "Reserves & Surplus,Reserves & Surplus,0,0,850000,-850000\n" +
                           "Cash at Bank,Bank Accounts,0,1850000,0,1850000\n" +
                           "Domestic Sales,Sales Accounts,0,0,5800000,-5800000\n" +
                           "Purchases of RM,Purchase Accounts,0,3400000,0,3400000\n" +
                           "Staff Salaries,Indirect Expenses,0,750000,0,750000\n";

        var tbPrevHttp = await http.PostAsJsonAsync("/api/trial-balance/upload", new UploadTrialBalanceRequest(
            coClient.Id, "2024-25", "TallyCsv", "tb_2024_25.csv", prevTbCsv));
        Assert.Equal(HttpStatusCode.Created, tbPrevHttp.StatusCode);

        var tbCurrHttp = await http.PostAsJsonAsync("/api/trial-balance/upload", new UploadTrialBalanceRequest(
            coClient.Id, "2025-26", "TallyCsv", "tb_2025_26.csv", currTbCsv));
        Assert.Equal(HttpStatusCode.Created, tbCurrHttp.StatusCode);

        var s3Create = await http.PostAsJsonAsync("/api/schedule3/assessments", new SaveSchedule3AssessmentRequest
        {
            ClientId = coClient.Id,
            FinancialYear = "2025-26",
            PrecedingFinancialYear = "2024-25",
            AccountingStandard = "Division I - AS (Non-Ind AS)",
            RoundingUnit = "Actuals",
            RevenueFromOperationsCurrent = 5_800_000m,
            RevenueFromOperationsPreceding = 4_200_000m,
            ShareholdersEquityCurrent = 1_850_000m,
            ShareholdersEquityPreceding = 1_000_000m,
        });
        Assert.Equal(HttpStatusCode.Created, s3Create.StatusCode);
        var s3Assm = (await s3Create.Content.ReadFromJsonAsync<Schedule3AssessmentDto>())!;

        var packHttp = await http.GetAsync($"/api/schedule3/assessments/{s3Assm.Id}/financial-pack-data");
        Assert.Equal(HttpStatusCode.OK, packHttp.StatusCode);
        var pack = (await packHttp.Content.ReadFromJsonAsync<Schedule3FinancialPackDto>())!;
        Assert.True(pack.DerivedFromTrialBalance);
        Assert.Equal("U15400KL2019PTC058123", pack.Cin);
        Assert.True(pack.CurrentYear.ShareCapital >= 1_000_000m);
        Assert.True(pack.CurrentYear.RevenueFromOperations >= 5_800_000m);
        Assert.True(pack.PrecedingYear.RevenueFromOperations >= 4_200_000m);
    }
}
