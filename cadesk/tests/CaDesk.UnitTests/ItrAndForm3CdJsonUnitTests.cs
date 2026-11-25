using System.Text.Json.Nodes;
using CaDesk.Application.Common;
using CaDesk.Application.Presumptive;
using CaDesk.Application.Tax;
using CaDesk.Application.TaxAudit;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Presumptive;
using CaDesk.Domain.Tax;
using CaDesk.Domain.TaxAudit;
using Xunit;

namespace CaDesk.UnitTests;

public class ItrAndForm3CdJsonUnitTests
{
    private static Client SampleClient(string entityType = "Individual", string pan = "ABCDE1234F", string? gstin = "32ABCDE1234F1Z5") => new()
    {
        Id = Guid.NewGuid(),
        Name = "Arjun Menon",
        LegalName = "Arjun Menon",
        EntityType = entityType,
        Email = "arjun@kochi.test",
        Phone = "9847011223",
        TaxIds = gstin is null
            ? new Dictionary<string, string> { ["PAN"] = pan }
            : new Dictionary<string, string> { ["PAN"] = pan, ["GSTIN"] = gstin },
    };

    [Fact]
    public void Itr1_Build_ProducesOfficialSchemaCompliantJson_AndFlagsIneligibleIncomes()
    {
        var client = SampleClient("Individual");
        var req = new CalculateTaxRequest(
            ClientId: client.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            TaxpayerCategory: TaxpayerCategory.Individual,
            OptedRegime: "new",
            GrossSalary: 1_100_000m,
            SavingsInterest: 20_000m,
            TdsPaid: 35_000m);
        var compResult = TaxEngine.ComputeComparison(req);
        var chosen = compResult.NewRegime;

        var entity = new TaxComputation
        {
            ClientId = client.Id,
            AssessmentYear = "2026-27",
            FinancialYear = "2025-26",
            TaxpayerCategory = TaxpayerCategory.Individual,
            OptedRegime = "new",
            GrossSalary = chosen.GrossSalary,
            SalaryDeductions = chosen.StandardDeduction,
            NetSalary = chosen.NetSalary,
            HousePropertyIncome = chosen.HousePropertyIncome,
            BusinessIncome = 0m,
            OtherSourcesIncome = chosen.OtherSourcesIncome,
            GrossTotalIncome = chosen.GrossTotalIncome,
            ChapterViaDeductions = chosen.ChapterViaDeductions,
            TotalTaxableIncome = chosen.TaxableIncome,
            TaxOnIncome = chosen.BaseTax,
            Rebate87A = chosen.Rebate87A,
            Surcharge = chosen.Surcharge,
            Cess = chosen.Cess,
            TotalTaxLiability = chosen.TotalTaxLiability,
            TdsTcsCredit = 35_000m,
        };

        var res = Itr1JsonBuilder.Build(entity, client, new ExportItrJsonRequest(FatherName: "Narayanan Menon", City: "Kochi", PinCode: "682031"));
        Assert.Equal("ITR-1", res.FormType);
        Assert.True(res.SchemaErrorCount == 0, string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(res.ValidAgainstSchema);

        var root = JsonNode.Parse(res.JsonContent)!.AsObject();
        var itr1 = root["ITR"]!["ITR1"]!.AsObject();
        Assert.Equal("ITR-1", itr1["Form_ITR1"]!["FormName"]!.GetValue<string>());
        Assert.Equal("16", itr1["PersonalInfo"]!["Address"]!["StateCode"]!.GetValue<string>()); // GSTIN 32 -> CBDT 16 Kerala
        Assert.Equal("N", itr1["FilingStatus"]!["OptOutNewTaxRegime"]!.GetValue<string>());

        // Schema failure check on corrupted payload
        itr1.Remove("PersonalInfo");
        var errs = JsonSchemaValidator.Validate(root, JsonSchemaValidator.LoadEmbeddedSchema("ITR-1_2026_Main_V1.1.json"));
        Assert.NotEmpty(errs);

        // Ineligible Business Income + >50L checks
        entity.BusinessIncome = 250_000m;
        entity.TotalTaxableIncome = 5_500_000m;
        var invalidRes = Itr1JsonBuilder.Build(entity, client);
        Assert.False(invalidRes.ValidAgainstSchema);
        Assert.Contains(invalidRes.Issues, i => i.Code == "ITR1-NO-PGBP");
        Assert.Contains(invalidRes.Issues, i => i.Code == "ITR1-50L-LIMIT");
    }

    [Theory]
    [InlineData(PresumptiveSection.Sec44AD, "09028")]
    [InlineData(PresumptiveSection.Sec44ADA, "16002")]
    [InlineData(PresumptiveSection.Sec44AE, "07001")]
    public void Itr4_Build_ValidatesAcross44Ad44AdaAnd44AeAgainstOfficialSchema(string section, string natureCode)
    {
        var client = SampleClient("Individual");
        var comp = new PresumptiveTaxComputation
        {
            ClientId = client.Id,
            FinancialYear = "2025-26",
            AssessmentYear = "2026-27",
            Section = section,
            EntityType = "Individual",
            BusinessNatureCode = natureCode,
            TradeName = "Malabar Traders",
            DigitalTurnover = section == PresumptiveSection.Sec44ADA ? 2_000_000m : 4_000_000m,
            CashTurnover = section == PresumptiveSection.Sec44ADA ? 0m : 200_000m,
            GrossTurnover = section == PresumptiveSection.Sec44ADA ? 2_000_000m : 4_200_000m,
            DeclaredProfit = section == PresumptiveSection.Sec44ADA ? 1_000_000m : 300_000m,
            SundryDebtors = 150_000m,
            StockInTrade = 250_000m,
            CashBalance = 20_000m,
            BankBalance = 180_000m,
            FixedAssets = 400_000m,
            SundryCreditors = 100_000m,
            PartnerOrProprietorCapital = 800_000m,
            LoansAndBorrowings = 100_000m,
            Vehicles = section == PresumptiveSection.Sec44AE
                ? [new Presumptive44AeVehicle { RegistrationNumber = "KL07CD1234", VehicleType = "other", MonthsOwned = 12, DeclaredProfit = 90_000m, DeemedAnnualProfit = 90_000m }]
                : [],
        };

        var res = Itr4JsonBuilder.Build(comp, client, new ExportItrJsonRequest(FatherName: "Narayanan Menon", City: "Kochi", PinCode: "682031"));
        Assert.Equal("ITR-4", res.FormType);
        Assert.True(res.SchemaErrorCount == 0, string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(res.ValidAgainstSchema);

        var itr4 = JsonNode.Parse(res.JsonContent)!["ITR"]!["ITR4"]!;
        Assert.Equal("ITR-4", itr4["Form_ITR4"]!["FormName"]!.GetValue<string>());
        Assert.Equal(800_000L, itr4["ScheduleBP"]!["FinanclPartclrOfBusiness"]!["PartnerMemberOwnCapital"]!.GetValue<long>());
    }

    [Fact]
    public void Form3Cd_Build_ValidatesBothForm3CaAndForm3CbAgainstOfficialV25Schemas()
    {
        var coClient = SampleClient("Company", "AAACB1234F", "32AAACB1234F1Z2");
        var coAssessment = new Form3CdAssessment
        {
            ClientId = coClient.Id,
            FinancialYear = "2025-26",
            AssessmentYear = "2026-27",
            AuditType = "Form3CA",
            AssesseeName = "Backwater Logistics Pvt Ltd",
            Pan = "AAACB1234F",
            Address = "22 Marine Drive, Kochi",
            Gstin = "32AAACB1234F1Z2",
            Status = "Company",
            TotalTurnoverOrGrossReceipts = 120_000_000m,
            TotalPayments = 90_000_000m,
            TotalDisallowanceSec40a = 45_000m,
            TotalDisallowanceSec40A3 = 25_000m,
            TotalDisallowanceSec43B = 60_000m,
            TotalDisallowanceSec43Bh = 15_000m,
        };
        var clauses = new List<Form3CdClauseResponse>
        {
            new() { ClauseNumber = "26", ClauseTitle = "Section 43B", Status = "Qualified", AuditorRemarks = "MSME overdue > 45 days." }
        };

        var r3Ca = Form3CdJsonBuilder.Build(coAssessment, clauses, coClient, new ExportForm3CdJsonRequest(AuditorMembershipNo: "234567", FirmRegNum: "004321S", AuditorCity: "Kochi"));
        Assert.Equal("FORM3CA", r3Ca.FormType);
        Assert.True(r3Ca.SchemaErrorCount == 0, string.Join("\n", r3Ca.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(r3Ca.ValidAgainstSchema);
        var f3caNode = JsonNode.Parse(r3Ca.JsonContent)!["FORM3CA"]!["F3CA"]!;
        Assert.Equal("2026-27", f3caNode["PartA"]!["AssessmentYear"]!.GetValue<string>());
        Assert.Equal(5, f3caNode["PartA"]!["Status"]!.GetValue<int>());
        Assert.Equal(90_000_000L, f3caNode["Form3cdBreakUpGST"]![0]!["TotAmtExp"]!.GetValue<long>());

        var firmClient = SampleClient("Firm", "AAAFB9999K", null);
        var firmAssessment = new Form3CdAssessment
        {
            ClientId = firmClient.Id,
            FinancialYear = "2025-26",
            AssessmentYear = "2026-27",
            AuditType = "Form3CB",
            AssesseeName = "Kochi Spice Traders",
            Pan = "AAAFB9999K",
            Address = "Mattancherry, Kochi",
            Status = "Firm",
            TotalPayments = 12_000_000m,
        };

        var r3Cb = Form3CdJsonBuilder.Build(firmAssessment, clauses, firmClient, new ExportForm3CdJsonRequest(AuditorMembershipNo: "234567", AuditorCity: "Kochi"));
        Assert.Equal("FORM3CB", r3Cb.FormType);
        Assert.Equal(0, r3Cb.SchemaErrorCount);
        Assert.True(r3Cb.ValidAgainstSchema);
        var f3cbNode = JsonNode.Parse(r3Cb.JsonContent)!["FORM3CB"]!["F3CB"]!;
        Assert.Equal(3, f3cbNode["PartA"]!["Status"]!.GetValue<int>());
        Assert.Single(f3cbNode["Declaration"]!["Point5"]!["Observations"]!.AsArray());
    }
}
