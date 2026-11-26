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

    [Fact]
    public void BusinessTaxEngine_FirmSec40b_And_Company115BaaVsMat_CalculatesAccurately()
    {
        // Firm with Book Profit Rs 20,00,000 before remuneration:
        // Sec 40(b)(v) ceiling (AY 2026-27): 90% of Rs 6L (= 5,40,000) + 60% of Rs 14L (= 8,40,000) = Rs 13,80,000
        var firmReq = new CalculateBusinessTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            EntityType: "firm",
            SubType: "Partnership",
            RevenueFromOperations: 12_000_000m,
            PurchasesAndDirectCost: 8_000_000m,
            EmployeeBenefitExpense: 1_500_000m,
            PartnerRemunerationDebited: 1_500_000m, // Rs 1.2L exceeds 13.8L ceiling -> disallowed
            PartnerInterestDebited: 300_000m,
            DepreciationPerBooks: 200_000m,
            IncomeTaxDepreciationSec32: 200_000m,
            OtherExpenses: 300_000m, // PBT before partner remun/interest = 12M - 8M - 1.5M - 0.2M - 0.3M = 20L -> after remun & int PBT = 2L
            Partners:
            [
                new PartnerShareInput("Arun Menon", "ABCPM1111K", "IND_WORKING", 50m, CapitalBalance: 1_000_000m, InterestRatePercent: 15m, InterestPaid: 150_000m, RemunerationPaid: 800_000m),
                new PartnerShareInput("Deepak Nair", "ABCPN2222L", "IND_WORKING", 50m, CapitalBalance: 1_000_000m, InterestRatePercent: 15m, InterestPaid: 150_000m, RemunerationPaid: 700_000m),
            ]
        );

        var firmRes = BusinessTaxEngine.Compute(firmReq);
        // Interest excess = 2 * (1,50,000 - 1,20,000) = 60,000; Firm book profit 40(b) = 2,00,000 + 15,00,000 + 60,000 = 17,60,000
        // Max 40(b)(v) = 5,40,000 + 60% * 11,60,000 = 12,36,000 -> Disallowed remuneration = 15,00,000 - 12,36,000 = 2,64,000
        Assert.Equal(60_000m, firmRes.DisallowedPartnerInterest40b);
        Assert.Equal(1_760_000m, firmRes.FirmBookProfitUs40b);
        Assert.Equal(1_236_000m, firmRes.MaxAllowableRemuneration40bv);
        Assert.Equal(264_000m, firmRes.DisallowedRemuneration40b);
        // Net PGBP = BookProfit(2,00,000) + InterestExcess(60,000) + RemunExcess(2,64,000) = 5,24,000
        Assert.Equal(524_000m, firmRes.NetPgbpIncome);
        Assert.Equal(524_000m, firmRes.TotalTaxableIncome);

        // Company: compare Normal (25% + MAT 115JB) vs Section 115BAA (22% + 10% SC + 4% HEC, zero MAT)
        var coReq = new CalculateBusinessTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            EntityType: "company",
            SubType: "Private",
            CorporateRegime: "auto",
            IsSmallTurnover25Percent: true,
            RevenueFromOperations: 50_000_000m,
            PurchasesAndDirectCost: 35_000_000m,
            EmployeeBenefitExpense: 4_000_000m,
            DepreciationPerBooks: 1_000_000m,
            IncomeTaxDepreciationSec32: 1_200_000m,
            OtherExpenses: 2_000_000m, // Book PBT = 8,000,000; Net PGBP = 7,800,000
            TaxProvisionCurrent: 1_900_000m
        );

        var coRes = BusinessTaxEngine.Compute(coReq);
        Assert.Equal(BusinessTaxEngine.RegimeCorp115Baa, coRes.RecommendedRegimeCode);
        Assert.Equal(2, coRes.Alternatives.Count(a => a.RegimeCode is BusinessTaxEngine.RegimeCorpNormal or BusinessTaxEngine.RegimeCorp115Baa));
        var normal = coRes.Alternatives.First(a => a.RegimeCode == BusinessTaxEngine.RegimeCorpNormal);
        var baa = coRes.Alternatives.First(a => a.RegimeCode == BusinessTaxEngine.RegimeCorp115Baa);
        Assert.True(normal.MatOrAmtTotalTax > 0m);
        Assert.Equal(0m, baa.MatOrAmtTotalTax);
        Assert.True(baa.TotalTaxLiability < normal.TotalTaxLiability);
    }

    [Fact]
    public void Itr5JsonBuilder_Build_ValidatesAgainstOfficialAy2026Schema()
    {
        var client = SampleClient("Firm", "AAAFB9999K", "32AAAFB9999K1Z1");
        var req = new CalculateBusinessTaxRequest(
            ClientId: client.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            EntityType: "firm",
            SubType: "Partnership",
            IsTaxAuditApplicable: true,
            PartnerOrShareCapital: 2_000_000m,
            ReservesAndSurplus: 500_000m,
            FixedAssetsGrossBlock: 1_800_000m,
            FixedAssetsAccumulatedDep: 300_000m,
            FixedAssetsNetBlock: 1_500_000m,
            Inventories: 400_000m,
            TradeReceivables: 600_000m,
            CashAndBankBalances: 500_000m,
            TradePayables: 500_000m,
            RevenueFromOperations: 25_000_000m,
            PurchasesAndDirectCost: 18_000_000m,
            EmployeeBenefitExpense: 2_000_000m,
            PartnerRemunerationDebited: 1_500_000m,
            PartnerInterestDebited: 240_000m,
            DepreciationPerBooks: 180_000m,
            IncomeTaxDepreciationSec32: 220_000m,
            OtherExpenses: 800_000m,
            Stcg111A: 100_000m,
            AdvanceTaxPaid: 200_000m,
            TdsCredit: 50_000m,
            Partners:
            [
                new PartnerShareInput("Arun Menon", "ABCPM1111K", "IND_WORKING", 50m, 1_000_000m, 12m, 120_000m, 750_000m),
                new PartnerShareInput("Deepak Nair", "ABCPN2222L", "IND_WORKING", 50m, 1_000_000m, 12m, 120_000m, 750_000m),
            ],
            DepreciationBlocks:
            [
                new DepreciationBlockInput("PM15", "Plant & Machinery 15%", 15m, 1_200_000m, 200_000m, 0m, 0m, 0m, 210_000m),
                new DepreciationBlockInput("Furn10", "Furniture 10%", 10m, 100_000m, 0m, 0m, 0m, 0m, 10_000m),
            ],
            AdvanceTaxChallans:
            [
                new ChallanTaxPaymentInput("0210045", "2025-12-15", "88192", 200_000m),
            ],
            TdsCreditEntries:
            [
                new TdsCreditLineInput("MUMT01234A", "Kerala Infrastructure Ltd", "94C", 2_500_000m, 50_000m),
            ]
        );

        var biz = BusinessTaxEngine.Compute(req);
        var comp = new TaxComputation
        {
            ClientId = client.Id,
            AssessmentYear = "2026-27",
            FinancialYear = "2025-26",
            TaxpayerCategory = TaxpayerCategory.Firm,
            OptedRegime = "old",
            BusinessIncome = biz.NetPgbpIncome,
            TotalTaxableIncome = biz.TotalTaxableIncome,
            TotalTaxLiability = biz.ActiveSummary.TotalTaxLiability,
        };

        var res = Itr5JsonBuilder.Build(comp, client, biz, new ExportItrJsonRequest(
            FatherName: "K. Menon",
            City: "Kochi",
            PinCode: "682031",
            VerificationName: "Arun Menon",
            VerificationPan: "ABCPM1111K",
            Capacity: "MP",
            IfscCode: "SBIN0008615",
            BankAccountNo: "10293847561"
        ));

        Assert.Equal("ITR-5", res.FormType);
        Assert.True(res.SchemaErrorCount == 0, string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(res.ValidAgainstSchema);

        var itr5 = JsonNode.Parse(res.JsonContent)!["ITR"]!["ITR5"]!;
        Assert.Equal("ITR-5", itr5["Form_ITR5"]!["FormName"]!.GetValue<string>());
        Assert.Equal("1", itr5["PartA_GEN1"]!["OrgFirmInfo"]!["StatusOrCompanyType"]!.GetValue<string>());
        Assert.Equal(2, itr5["PartA_GEN2"]!["PartnerOrMemberInfo"]!.AsArray().Count);
        Assert.Equal(220_000L, itr5["ScheduleDEP"]!["SummaryFromDeprSch"]!["TotalDepreciation"]!.GetValue<long>());
    }

    [Fact]
    public void Itr6JsonBuilder_Build_Validates115BaaAndNormalMatAgainstOfficialAy2026Schema()
    {
        var client = SampleClient("Company", "AAACC4444K", "32AAACC4444K1Z5");
        client.TaxIds["CIN"] = "U72900KL2020PTC061234";

        foreach (var regime in new[] { "115BAA", "normal" })
        {
            var req = new CalculateBusinessTaxRequest(
                ClientId: client.Id,
                AssessmentYear: "2026-27",
                FinancialYear: "2025-26",
                EntityType: "company",
                SubType: "Private",
                CorporateRegime: regime,
                IsSmallTurnover25Percent: true,
                IsTaxAuditApplicable: true,
                PartnerOrShareCapital: 1_000_000m,
                AuthorisedShareCapital: 5_000_000m,
                ReservesAndSurplus: 4_000_000m,
                LongTermBorrowings: 2_000_000m,
                TradePayables: 800_000m,
                FixedAssetsGrossBlock: 5_000_000m,
                FixedAssetsAccumulatedDep: 1_000_000m,
                FixedAssetsNetBlock: 4_000_000m,
                Inventories: 1_200_000m,
                TradeReceivables: 1_500_000m,
                CashAndBankBalances: 1_100_000m,
                RevenueFromOperations: 45_000_000m,
                PurchasesAndDirectCost: 30_000_000m,
                EmployeeBenefitExpense: 6_000_000m,
                DepreciationPerBooks: 800_000m,
                IncomeTaxDepreciationSec32: 900_000m,
                OtherExpenses: 2_200_000m,
                TaxProvisionCurrent: 1_500_000m,
                DisallowSec43BhMsme: 100_000m,
                AdvanceTaxPaid: 1_000_000m,
                TdsCredit: 120_000m,
                KeyPersons:
                [
                    new KeyPersonInput("Rajan Varma", "MD", "ABCPV1234Q", "01234567", "Panampilly Nagar", "Kochi", "16", "682036"),
                ],
                Shareholders:
                [
                    new ShareholderInput("Rajan Varma", "ABCPV1234Q", "Equity", 60_000, 10m, 10m),
                    new ShareholderInput("Meera Varma", "ABCPV5678R", "Equity", 40_000, 10m, 10m),
                ],
                DepreciationBlocks:
                [
                    new DepreciationBlockInput("PM40", "Computers @ 40%", 40m, 1_500_000m, 0m, 0m, 0m, 0m, 600_000m),
                    new DepreciationBlockInput("PM15", "Plant @ 15%", 15m, 2_000_000m, 0m, 0m, 0m, 0m, 300_000m),
                ]
            );

            var biz = BusinessTaxEngine.Compute(req);
            var comp = new TaxComputation
            {
                ClientId = client.Id,
                AssessmentYear = "2026-27",
                FinancialYear = "2025-26",
                TaxpayerCategory = TaxpayerCategory.Company,
                OptedRegime = regime == "115BAA" ? "new" : "old",
                BusinessIncome = biz.NetPgbpIncome,
                TotalTaxableIncome = biz.TotalTaxableIncome,
                TotalTaxLiability = biz.ActiveSummary.TotalTaxLiability,
            };

            var res = Itr6JsonBuilder.Build(comp, client, biz, new ExportItrJsonRequest(
                FatherName: "K. Varma",
                City: "Kochi",
                PinCode: "682036",
                VerificationName: "Rajan Varma",
                VerificationPan: "ABCPV1234Q",
                Capacity: "MD",
                IfscCode: "SBIN0008615",
                BankAccountNo: "30293847561"
            ));

            Assert.Equal("ITR-6", res.FormType);
            Assert.True(res.SchemaErrorCount == 0, $"Regime={regime}:\n" + string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
            Assert.True(res.ValidAgainstSchema);

            var itr6 = JsonNode.Parse(res.JsonContent)!["ITR"]!["ITR6"]!;
            Assert.Equal("ITR-6", itr6["Form_ITR6"]!["FormName"]!.GetValue<string>());
            Assert.Equal(regime == "115BAA" ? "115BAA" : "NA", itr6["PartA_GEN1"]!["FilingStatus"]!["Section115BA"]!.GetValue<string>());
            Assert.Equal("U72900KL2020PTC061234", itr6["PartA_GEN1"]!["OrgFirmInfo"]!["CINissuedByMCA"]!.GetValue<string>());
            Assert.Single(itr6["PartA_GEN2For6"]!["KeyPersons"]!.AsArray());
            if (regime == "normal")
                Assert.True(itr6["ScheduleMAT"]!["BookProfUs115JB"]!.GetValue<long>() > 0);
        }
    }
}
