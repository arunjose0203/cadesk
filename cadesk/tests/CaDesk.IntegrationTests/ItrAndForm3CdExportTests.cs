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
    }
}
