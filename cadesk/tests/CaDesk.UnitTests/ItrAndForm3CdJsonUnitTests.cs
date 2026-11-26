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

    [Fact]
    public void Itr2JsonBuilder_Build_ValidatesCapGainsVdaForeignAssetsAndAlAgainstAy2026Schema()
    {
        var client = SampleClient("Individual", "AAAPV7777P", null);
        var req = new CalculatePersonalExtendedTaxRequest(
            ClientId: client.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            ReturnFormType: "ITR-2",
            TaxpayerCategory: TaxpayerCategory.Individual,
            OptedRegime: "old",
            Employers:
            [
                new SalaryEmployerInput("Infosys Ltd", "BLRI01234A", "OTH", 3_500_000m, 650_000m, "Electronic City", "Bengaluru", "15", "560100"),
                new SalaryEmployerInput("Zoho Corp", "CHEZ05678B", "OTH", 2_000_000m, 350_000m, "Estancia IT Park", "Chennai", "29", "603202"),
            ],
            HraReceived: 400_000m,
            RentPaid: 480_000m,
            IsMetro: true,
            ProfessionalTax: 2_500m,
            HouseProperties:
            [
                new HousePropertyInput("Self House, Panampilly Nagar", "Kochi", "16", "682036", "S", 0m, 0m, 200_000m),
                new HousePropertyInput("Flat B2, Marine Drive", "Kochi", "16", "682031", "L", 600_000m, 20_000m, 150_000m),
            ],
            Stcg111ASaleValue: 900_000m,
            Stcg111ACost: 500_000m,
            Ltcg112ASaleValue: 1_500_000m,
            Ltcg112ACost: 1_000_000m,
            Ltcg112SaleValue: 800_000m,
            Ltcg112Cost: 600_000m,
            VdaConsideration: 450_000m,
            VdaCostOfAcquisition: 250_000m,
            SavingsInterest: 24_000m,
            FdInterest: 80_000m,
            DividendIncome: 45_000m,
            Section80C: 150_000m,
            Section80Ccd1B: 50_000m,
            Section80D: 50_000m,
            Section80Tta: 10_000m,
            AdvanceTaxPaid: 800_000m,
            AdvanceTaxChallans: [new ChallanTaxPaymentInput("0210045", "2026-03-14", "99123", 800_000m)],
            ForeignAssets: [new ForeignAssetInput("United States", "2", "Interactive Brokers LLC", "Greenwich CT", "06830", "U9988776", "2022-05-10", 2_500_000m, 2_200_000m, 0m)],
            ScheduleAl: new ScheduleAlInput(12_000_000m, "Kochi Residential Flat", 1_500_000m, 4_000_000m, 500_000m, 0m, 50_000m, 400_000m, 1_200_000m, 3_000_000m)
        );

        var ext = PersonalTaxExtendedEngine.Compute(req);
        Assert.Equal("ITR-2", ext.ReturnFormType);
        Assert.Equal(60_000m, ext.OldRegime.TaxOnVda115BBH); // 30% of 2,00,000 VDA gain

        var comp = new TaxComputation
        {
            ClientId = client.Id,
            AssessmentYear = "2026-27",
            FinancialYear = "2025-26",
            TaxpayerCategory = TaxpayerCategory.Individual,
            OptedRegime = ext.SelectedRegime,
        };

        var res = Itr2JsonBuilder.Build(comp, client, ext, new ExportItrJsonRequest(
            FatherName: "Narayanan Varma", City: "Kochi", PinCode: "682036", IfscCode: "SBIN0008615", BankAccountNo: "10293847561"));

        Assert.Equal("ITR-2", res.FormType);
        Assert.True(res.SchemaErrorCount == 0, string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(res.ValidAgainstSchema);

        var itr2 = JsonNode.Parse(res.JsonContent)!["ITR"]!["ITR2"]!;
        Assert.Equal("ITR-2", itr2["Form_ITR2"]!["FormName"]!.GetValue<string>());
        Assert.Equal(2, itr2["ScheduleS"]!["Salaries"]!.AsArray().Count);
        Assert.Equal(2, itr2["ScheduleHP"]!["PropertyDetails"]!.AsArray().Count);
        Assert.Equal(200_000L, itr2["ScheduleVDA"]!["TotIncCapGain"]!.GetValue<long>());
        Assert.Single(itr2["ScheduleFA"]!["DetailsForiegnBank"]!.AsArray());
        Assert.NotNull(itr2["ScheduleAL"]);
    }

    [Fact]
    public void Itr3JsonBuilder_Build_ValidatesProprietorBooksAndBpAgainstAy2026Schema()
    {
        var client = SampleClient("Individual", "AAAPR4444L", null);
        var req = new CalculatePersonalExtendedTaxRequest(
            ClientId: client.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            ReturnFormType: "ITR-3",
            TaxpayerCategory: TaxpayerCategory.Individual,
            OptedRegime: "new",
            BusinessName: "Malabar Medical Systems",
            NatureOfBusinessCode: "09027",
            IsTaxAuditApplicable: true,
            PropCapital: 3_500_000m,
            ReservesAndSurplus: 1_200_000m,
            SecuredLoans: 1_000_000m,
            TradePayables: 450_000m,
            FixedAssetsGrossBlock: 2_800_000m,
            FixedAssetsAccumulatedDep: 600_000m,
            FixedAssetsNetBlock: 2_200_000m,
            Inventories: 1_500_000m,
            TradeReceivables: 1_600_000m,
            CashAndBankBalances: 850_000m,
            RevenueFromOperations: 28_000_000m,
            PurchasesAndDirectCost: 20_000_000m,
            EmployeeBenefitExpense: 3_000_000m,
            DepreciationPerBooks: 350_000m,
            IncomeTaxDepreciationSec32: 420_000m,
            OtherBusinessExpenses: 1_800_000m,
            DisallowSec43BhMsme: 120_000m,
            DisallowSec40a: 30_000m,
            Stcg111A: 150_000m,
            AdvanceTaxPaid: 500_000m,
            TdsOtherThanSalary: 60_000m,
            DepreciationBlocks:
            [
                new DepreciationBlockInput("PM15", "Medical Equipment 15%", 15m, 2_000_000m, 200_000m, 0m, 0m, 0m, 330_000m),
                new DepreciationBlockInput("PM40", "Computers 40%", 40m, 225_000m, 0m, 0m, 0m, 0m, 90_000m),
            ]
        );

        var ext = PersonalTaxExtendedEngine.Compute(req);
        // Book PBT = 28M - 20M - 3M - 0.35M - 1.8M = 28,50,000
        // Net PGBP = 28,50,000 + 3,50,000 + 1,50,000 (43Bh+40a) - 4,20,000 = 29,30,000
        Assert.Equal(2_850_000m, ext.NewRegime.BusinessBookProfit);
        Assert.Equal(2_930_000m, ext.NewRegime.BusinessIncomeNetPgbp);

        var comp = new TaxComputation
        {
            ClientId = client.Id,
            AssessmentYear = "2026-27",
            FinancialYear = "2025-26",
            TaxpayerCategory = TaxpayerCategory.Individual,
            OptedRegime = "new",
        };

        var res = Itr3JsonBuilder.Build(comp, client, ext, new ExportItrJsonRequest(
            FatherName: "Raghavan Menon", City: "Kochi", PinCode: "682031"));

        Assert.Equal("ITR-3", res.FormType);
        Assert.True(res.SchemaErrorCount == 0, string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(res.ValidAgainstSchema);

        var itr3 = JsonNode.Parse(res.JsonContent)!["ITR"]!["ITR3"]!;
        Assert.Equal("ITR-3", itr3["Form_ITR3"]!["FormName"]!.GetValue<string>());
        Assert.Equal(2_930_000L, itr3["ITR3ScheduleBP"]!["IncChrgUnHdProftGain"]!.GetValue<long>());
        Assert.Equal(420_000L, itr3["ScheduleDEP"]!["SummaryFromDeprSch"]!["TotalDepreciation"]!.GetValue<long>());
    }

    [Fact]
    public void Itr2JsonBuilder_CapitalGainTrades_EmitsGranularIsin112A_QuarterlyBuckets_And_Vda()
    {
        var client = SampleClient("Individual", "ABCPZ9999K", null);
        var req = new CalculatePersonalExtendedTaxRequest(
            ClientId: client.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            ReturnFormType: "ITR-2",
            TaxpayerCategory: TaxpayerCategory.Individual,
            OptedRegime: "new",
            GrossSalary: 1_200_000m,
            TdsSalary: 80_000m,
            CapitalGainTrades:
            [
                new CapitalGainTradeEntry("Zerodha", "112A", "INE009A01021", "INFOSYS LTD", "2017-05-15", "2025-07-20", 100m, 290_000m, 90_000m, 160_000m, 0m),
                new CapitalGainTradeEntry("Groww", "112A", "INE040A01034", "HDFC BANK LTD", "2021-03-10", "2025-10-20", 50m, 180_000m, 110_000m, 0m, 0m),
                new CapitalGainTradeEntry("Zerodha", "111A", "INE467B01029", "TCS LTD", "2025-01-10", "2025-11-18", 40m, 220_000m, 175_000m, 0m, 0m),
                new CapitalGainTradeEntry("CoinDCX", "VDA", null, "BTC", "2024-06-01", "2026-01-12", 0.2m, 400_000m, 240_000m, 0m, 0m),
            ]
        );

        var ext = PersonalTaxExtendedEngine.Compute(req);
        // INFY LTCG = 290k - max(90k, min(160k, 290k)) = 130k; HDFC = 180k - 110k = 70k => 112A = 200k
        Assert.Equal(200_000m, ext.NewRegime.Ltcg112A);
        Assert.Equal(45_000m, ext.NewRegime.Stcg111A);
        Assert.Equal(160_000m, ext.NewRegime.VdaIncome115BBH);

        var comp = new TaxComputation
        {
            ClientId = client.Id,
            AssessmentYear = "2026-27",
            FinancialYear = "2025-26",
            TaxpayerCategory = TaxpayerCategory.Individual,
            OptedRegime = "new",
        };

        var res = Itr2JsonBuilder.Build(comp, client, ext, new ExportItrJsonRequest(City: "Kochi", PinCode: "682031"));
        Assert.True(res.SchemaErrorCount == 0, string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(res.ValidAgainstSchema);

        var itr2 = JsonNode.Parse(res.JsonContent)!["ITR"]!["ITR2"]!;
        var rows112a = itr2["Schedule112A"]!["Schedule112ADtls"]!.AsArray();
        Assert.Equal(2, rows112a.Count);
        Assert.Equal("BE", rows112a[0]!["ShareOnOrBefore"]!.GetValue<string>());
        Assert.Equal("INE009A01021", rows112a[0]!["ISINCode"]!.GetValue<string>());
        Assert.Equal(160_000L, rows112a[0]!["TotFairMktValueCapAst"]!.GetValue<long>());
        Assert.Equal("AE", rows112a[1]!["ShareOnOrBefore"]!.GetValue<string>());
        Assert.Equal("INE040A01034", rows112a[1]!["ISINCode"]!.GetValue<string>());

        var accru = itr2["ScheduleCGFor23"]!["AccruOrRecOfCG"]!;
        Assert.Equal(130_000L, accru["LongTermUnder12_5Per"]!["DateRange"]!["Upto15Of9"]!.GetValue<long>());
        Assert.Equal(70_000L, accru["LongTermUnder12_5Per"]!["DateRange"]!["Up16Of9To15Of12"]!.GetValue<long>());
        Assert.Equal(45_000L, accru["ShortTermUnder20Per"]!["DateRange"]!["Up16Of9To15Of12"]!.GetValue<long>());
        Assert.Equal(160_000L, accru["VDATrnsfGainsUnder30Per"]!["DateRange"]!["Up16Of12To15Of3"]!.GetValue<long>());
    }

    [Fact]
    public void Itr7JsonBuilder_Build_ValidatesTrust11_12AB_And_115BBC_AgainstAy2026Schema()
    {
        var client = SampleClient("Company", "AAABT8888E", null);
        var req = new CalculateTrustTaxRequest(
            ClientId: client.Id,
            AssessmentYear: "2026-27",
            FinancialYear: "2025-26",
            ReturnFurnishedSection: "139-4A",
            SectionExemptionClaimed: "11",
            StatusOrCompanyType: "5",
            CorpusDonations80G: 500_000m,
            GrantsReceivedGovt: 1_200_000m,
            OtherSpecificAndGeneralDonations: 2_800_000m,
            AnonymousDonationsReceived: 400_000m,
            ReceiptsFromMainObjects: 600_000m,
            InterestIncome: 150_000m,
            RevenueApplicationCharitable: 3_600_000m,
            CapitalApplicationCharitable: 500_000m,
            ActivityCategory: "Educational",
            Form10AccumulationUs11_2: 0m,
            CorpusFundClosing: 2_500_000m,
            AccumulatedFundClosing: 1_500_000m,
            FixedAssetsNetBlock: 1_800_000m,
            InvestmentsSec11_5: 1_500_000m,
            CashAndBankBalances: 700_000m,
            TdsCredit: 15_000m,
            AdvanceTaxPaid: 50_000m);

        var trust = TrustTaxEngine.Compute(req);
        // Total donations = 500k + 1200k + 2800k + 400k = 49,00,000 -> 5% = 2,45,000 -> anon taxable 115BBC = 400k - 245k = 1,55,000
        Assert.Equal(155_000m, trust.AnonymousDonationsTaxable115BBC);
        Assert.Equal(46_500m, trust.TaxOnAnonymous115BBC);
        Assert.True(trust.TotalTaxLiability > 0m);

        var comp = new TaxComputation
        {
            ClientId = client.Id,
            AssessmentYear = "2026-27",
            FinancialYear = "2025-26",
            TaxpayerCategory = "trust",
            OptedRegime = "old",
        };

        var res = Itr7JsonBuilder.Build(comp, client, trust, new ExportItrJsonRequest(
            VerificationName: "K. Subramanian", VerificationPan: "ABCPS9999A", FatherName: "Krishnan", Capacity: "DR", City: "Kochi", PinCode: "682031"));

        Assert.Equal("ITR-7", res.FormType);
        Assert.True(res.SchemaErrorCount == 0, string.Join("\n", res.Issues.Select(i => $"{i.Path}: [{i.Code}] {i.Message}")));
        Assert.True(res.ValidAgainstSchema);

        var itr7 = JsonNode.Parse(res.JsonContent)!["ITR"]!["ITR7"]!;
        Assert.Equal("ITR-7", itr7["Form_ITR7"]!["FormName"]!.GetValue<string>());
        Assert.Equal(155_000L, itr7["PartB_TI"]!["DonationsUs115BBC"]!.GetValue<long>());
    }
}
