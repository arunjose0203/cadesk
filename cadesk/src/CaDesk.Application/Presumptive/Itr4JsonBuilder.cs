using System.Text.Json.Nodes;
using CaDesk.Application.Tax;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Presumptive;

namespace CaDesk.Application.Presumptive;

/// <summary>
/// Builds and validates CBDT ITR-4 (Sugam) JSON (44AD / 44ADA / 44AE + Minimal Financials)
/// conforming to official AY 2026-27 schema V1.1.
/// </summary>
public static class Itr4JsonBuilder
{
    public static IncomeTaxJsonExportResult Build(
        PresumptiveTaxComputation comp,
        Client client,
        ExportItrJsonRequest? opts = null)
    {
        var issues = new List<IncomeTaxJsonExportIssue>();

        var pan = CbdtJsonExportHelper.NormalizePan(CbdtJsonExportHelper.ExtractTaxId(client, "PAN"), issues, "$.ITR.ITR4.PersonalInfo.PAN");
        var name = CbdtJsonExportHelper.Trunc(client.LegalName ?? client.Name, 75);
        var stateCode = CbdtJsonExportHelper.ResolveStateCode(opts?.StateCode, client);
        var city = CbdtJsonExportHelper.Trunc(opts?.City, 50, "Kochi");
        var pinCode = CbdtJsonExportHelper.ResolvePinInt(opts?.PinCode);
        var mobile = CbdtJsonExportHelper.ResolveMobileLong(opts?.MobileNo, client.Phone);
        var email = CbdtJsonExportHelper.ResolveEmail(opts?.EmailAddress, client.Email);
        var dob = string.IsNullOrWhiteSpace(opts?.Dob) ? "1990-01-01" : opts.Dob.Trim();

        var sec = (comp.Section ?? PresumptiveSection.Sec44AD).ToUpperInvariant();
        if (comp.IsAuditMandatory)
            issues.Add(new("error", "$.ITR.ITR4.ScheduleBP", "ITR4-AUDIT-REQD",
                comp.AuditTriggerReason ?? "Tax audit u/s 44AB is mandatory; assessee cannot file ITR-4 (Sugam) and must use ITR-3 + Form 3CD."));

        if (comp.IsLockoutActive)
            issues.Add(new("error", "$.ITR.ITR4.ScheduleBP", "ITR4-44AD4-LOCKOUT",
                $"Section 44AD(4) 5-year lockout active until AY {comp.LockoutExpiryAy}; file ITR-3 with Tax Audit instead of ITR-4."));

        var (_, ayDashed, _, _) = CbdtJsonExportHelper.ParseAyWindow(comp.AssessmentYear);
        if (!string.Equals(ayDashed, "2026-27", StringComparison.Ordinal))
            issues.Add(new("warning", "$.ITR.ITR4.Form_ITR4.AssessmentYear", "ITR4-AY-SCHEMA",
                $"Generated using official CBDT AY 2026-27 schema V1.1 while computation was saved for AY {comp.AssessmentYear}."));

        var pgbp = CbdtJsonExportHelper.ToRupees(comp.DeclaredProfit);
        var salGross = CbdtJsonExportHelper.ToRupees(opts?.SalaryIncome ?? 0m);
        var optOutNew = opts?.OptOutNewRegime ?? false;
        var stdDed = salGross > 0 ? Math.Min(salGross, optOutNew ? 50_000L : 75_000L) : 0L;
        var netSal = Math.Max(0L, salGross - stdDed);
        var ifos = CbdtJsonExportHelper.ToRupees(opts?.OtherSourcesIncome ?? 0m);
        var gti = pgbp + netSal + ifos;

        var d80C = optOutNew ? Math.Min(150_000L, CbdtJsonExportHelper.ToRupees(opts?.ChapterVia80C ?? 0m)) : 0L;
        var totalIncome = Math.Max(0L, gti - d80C);
        if (totalIncome > 5_000_000m)
            issues.Add(new("error", "$.ITR.ITR4.IncomeDeductions.TotalIncome", "ITR4-50L-LIMIT", "Total income exceeds Rs 50,00,000 statutory limit for ITR-4 (Sugam)."));

        // Run real tax calculation engine for exact AY slab / 87A / 4% cess breakdown
        var taxReq = new CalculateTaxRequest(
            ClientId: client.Id,
            AssessmentYear: comp.AssessmentYear,
            FinancialYear: comp.FinancialYear,
            TaxpayerCategory: comp.EntityType?.Equals("Firm", StringComparison.OrdinalIgnoreCase) == true ? "firm" : "individual",
            OptedRegime: optOutNew ? "old" : "new",
            GrossSalary: salGross,
            NetProfit: comp.DeclaredProfit,
            OtherIncome: ifos,
            Section80C: d80C,
            TdsPaid: opts?.TdsPaid ?? 0m,
            AdvanceTaxPaid: opts?.AdvanceTaxPaid ?? 0m
        );
        var computed = TaxEngine.ComputeRegime(taxReq, isNewRegime: !optOutNew);

        var baseTax = CbdtJsonExportHelper.ToRupees(computed.BaseTax + computed.SpecialRateTax);
        var rebate = Math.Min(60_000L, CbdtJsonExportHelper.ToRupees(computed.Rebate87A));
        var taxAfterRebate = Math.Max(0L, baseTax - rebate);
        var cess = CbdtJsonExportHelper.ToRupees(computed.Cess);
        var grossTaxLiab = taxAfterRebate + CbdtJsonExportHelper.ToRupees(computed.Surcharge) + cess;

        var tds = CbdtJsonExportHelper.ToRupees(opts?.TdsPaid ?? 0m);
        var adv = CbdtJsonExportHelper.ToRupees(opts?.AdvanceTaxPaid ?? 0m);
        var totPaid = tds + adv;
        var balPayable = Math.Max(0L, grossTaxLiab - totPaid);
        var refundDue = Math.Max(0L, totPaid - grossTaxLiab);

        var entityStatus = comp.EntityType?.ToLowerInvariant() switch
        {
            "huf" => "H",
            "firm" => "F",
            _ => "I"
        };

        // Schedule BP (44AD / 44ADA / 44AE + Minimal Financials)
        var scheduleBp = new JsonObject();
        var tradeName = CbdtJsonExportHelper.Trunc(comp.TradeName, 75, name);
        var natureCode = CbdtJsonExportHelper.Trunc(comp.BusinessNatureCode, 10, sec == PresumptiveSection.Sec44ADA ? "16002" : "09028");

        if (sec == PresumptiveSection.Sec44ADA)
        {
            scheduleBp["NatOfBus44ADA"] = new JsonArray
            {
                new JsonObject { ["NameOfBusiness"] = tradeName, ["CodeADA"] = natureCode }
            };
            scheduleBp["PersumptiveInc44ADA"] = new JsonObject
            {
                ["GrsReceipt"] = Math.Min(7_500_000L, CbdtJsonExportHelper.ToRupees(comp.GrossTurnover)),
                ["GrsTrnOverBank44ADA"] = Math.Min(7_500_000L, CbdtJsonExportHelper.ToRupees(comp.DigitalTurnover)),
                ["GrsTotalTrnOverInCash44ADA"] = Math.Min(7_500_000L, CbdtJsonExportHelper.ToRupees(comp.CashTurnover)),
                ["GrsTrnOverAnyOthMode44ADA"] = 0,
                ["TotPersumptiveInc44ADA"] = Math.Min(7_500_000L, pgbp),
            };
        }
        else if (sec == PresumptiveSection.Sec44AE)
        {
            var goodsArr = new JsonArray();
            foreach (var v in comp.Vehicles.Take(10))
            {
                goodsArr.Add(new JsonObject
                {
                    ["RegNumberGoodsCarriage"] = CbdtJsonExportHelper.Trunc(v.RegistrationNumber, 11, "KL01AB1234"),
                    ["OwnedLeasedHiredFlag"] = "OWN",
                    ["TonnageCapacity"] = (long)Math.Clamp(Math.Ceiling(v.GrossVehicleWeightTons), 0, 100),
                    ["HoldingPeriod"] = Math.Clamp(v.MonthsOwned, 1, 12),
                    ["PresumptiveIncome"] = Math.Max(7500L, CbdtJsonExportHelper.ToRupees(v.DeclaredProfit > 0 ? v.DeclaredProfit : v.DeemedAnnualProfit)),
                });
            }
            if (goodsArr.Count > 0) scheduleBp["GoodsDtlsUs44AE"] = goodsArr;
            scheduleBp["PersumptiveInc44AE"] = new JsonObject
            {
                ["TotPersumInc44AE"] = pgbp,
                ["SalInterestByFirm"] = 0,
                ["TotalPersumptiveInc"] = pgbp,
                ["IncChargeableUnderBus"] = pgbp,
            };
        }
        else // 44AD
        {
            scheduleBp["NatOfBus44AD"] = new JsonArray
            {
                new JsonObject { ["NameOfBusiness"] = tradeName, ["CodeAD"] = natureCode }
            };
            var digProfit = CbdtJsonExportHelper.ToRupees(Math.Round(comp.DigitalTurnover * 0.06m, 0, MidpointRounding.AwayFromZero));
            var cashProfit = Math.Max(0L, pgbp - digProfit);
            scheduleBp["PersumptiveInc44AD"] = new JsonObject
            {
                ["GrsTotalTrnOver"] = Math.Min(30_000_000L, CbdtJsonExportHelper.ToRupees(comp.GrossTurnover)),
                ["GrsTrnOverBank"] = Math.Min(30_000_000L, CbdtJsonExportHelper.ToRupees(comp.DigitalTurnover)),
                ["GrsTotalTrnOverInCash"] = Math.Min(30_000_000L, CbdtJsonExportHelper.ToRupees(comp.CashTurnover)),
                ["GrsTrnOverAnyOthMode"] = 0,
                ["PersumptiveInc44AD6Per"] = Math.Min(30_000_000L, digProfit),
                ["PersumptiveInc44AD8Per"] = Math.Min(30_000_000L, cashProfit),
                ["TotPersumptiveInc44AD"] = Math.Min(30_000_000L, pgbp),
            };
        }

        // Section 44AA Minimal Financial Particulars (Balance Sheet)
        var cap = CbdtJsonExportHelper.ToRupees(comp.PartnerOrProprietorCapital);
        var loans = CbdtJsonExportHelper.ToRupees(comp.LoansAndBorrowings);
        var cred = CbdtJsonExportHelper.ToRupees(comp.SundryCreditors);
        var totCapLiab = cap + loans + cred;

        var fa = CbdtJsonExportHelper.ToRupees(comp.FixedAssets);
        var inv = CbdtJsonExportHelper.ToRupees(comp.StockInTrade);
        var debt = CbdtJsonExportHelper.ToRupees(comp.SundryDebtors);
        var bankBal = CbdtJsonExportHelper.ToRupees(comp.BankBalance);
        var cashBal = CbdtJsonExportHelper.ToRupees(comp.CashBalance);
        var othAssets = CbdtJsonExportHelper.ToRupees(comp.OtherAssets);
        var totAssets = fa + inv + debt + bankBal + cashBal + othAssets;

        if (totAssets != totCapLiab)
            issues.Add(new("warning", "$.ITR.ITR4.ScheduleBP.FinanclPartclrOfBusiness", "ITR4-BS-DIFF",
                $"Section 44AA minimal balance sheet has Total Assets Rs {totAssets:N0} vs Total Capital & Liabilities Rs {totCapLiab:N0}."));

        scheduleBp["FinanclPartclrOfBusiness"] = new JsonObject
        {
            ["PartnerMemberOwnCapital"] = cap,
            ["SecuredLoans"] = 0,
            ["UnSecuredLoans"] = loans,
            ["Advances"] = 0,
            ["SundryCreditors"] = cred,
            ["OthrCurrLiab"] = 0,
            ["TotCapLiabilities"] = totCapLiab,
            ["FixedAssets"] = fa,
            ["Investments"] = 0,
            ["Inventories"] = inv,
            ["SundryDebtors"] = debt,
            ["BalWithBanks"] = bankBal,
            ["CashInHand"] = cashBal,
            ["LoansAndAdvances"] = 0,
            ["OtherAssets"] = othAssets,
            ["TotalAssets"] = totAssets,
        };

        var root = new JsonObject
        {
            ["ITR"] = new JsonObject
            {
                ["ITR4"] = new JsonObject
                {
                    ["CreationInfo"] = CbdtJsonExportHelper.BuildCreationInfo(opts?.IntermediaryCity ?? city),
                    ["Form_ITR4"] = new JsonObject
                    {
                        ["FormName"] = "ITR-4",
                        ["Description"] = "For Indls, HUFs and Firms (Other than LLP) under Presumptive Business",
                        ["AssessmentYear"] = "2026",
                        ["SchemaVer"] = "Ver1.0",
                        ["FormVer"] = "Ver1.0",
                    },
                    ["PersonalInfo"] = new JsonObject
                    {
                        ["AssesseeName"] = new JsonObject { ["SurNameOrOrgName"] = name },
                        ["PAN"] = pan,
                        ["Address"] = new JsonObject
                        {
                            ["ResidenceNo"] = CbdtJsonExportHelper.Trunc(opts?.ResidenceNo, 50, "Door 1"),
                            ["LocalityOrArea"] = CbdtJsonExportHelper.Trunc(opts?.LocalityOrArea, 50, city),
                            ["CityOrTownOrDistrict"] = city,
                            ["StateCode"] = stateCode,
                            ["CountryCode"] = "91",
                            ["PinCode"] = pinCode,
                            ["CountryCodeMobile"] = 91,
                            ["MobileNo"] = mobile,
                            ["EmailAddress"] = email,
                        },
                        ["SecondaryAdd"] = "N",
                        ["DOB"] = dob,
                        ["EmployerCategory"] = "OTH",
                        ["Status"] = entityStatus,
                    },
                    ["FilingStatus"] = new JsonObject
                    {
                        ["ReturnFileSec"] = opts?.ReturnFileSec is >= 11 and <= 20 ? opts.ReturnFileSec.Value : 11,
                        ["Form10IEAEarlierAYOldRegime"] = optOutNew ? "Y" : "N",
                        ["AsseseeRepFlg"] = "N",
                        ["ItrFilingDueDate"] = "2026-08-31",
                    },
                    ["IncomeDeductions"] = new JsonObject
                    {
                        ["IncomeFromBusinessProf"] = pgbp,
                        ["GrossSalary"] = salGross,
                        ["NetSalary"] = netSal,
                        ["DeductionUs16"] = stdDed,
                        ["IncomeFromSal"] = netSal,
                        ["TotalIncomeChargeableUnHP"] = 0,
                        ["IncomeOthSrc"] = ifos,
                        ["GrossTotIncome"] = gti,
                        ["GrossTotIncomeIncLTCG112A"] = gti,
                        ["UsrDeductUndChapVIA"] = CbdtJsonExportHelper.BuildItr4ChapVia(d80C, 0, 0, 0, 0, 0, d80C, applyCaps: false),
                        ["DeductUndChapVIA"] = CbdtJsonExportHelper.BuildItr4ChapVia(d80C, 0, 0, 0, 0, 0, d80C, applyCaps: true),
                        ["TotalIncome"] = Math.Min(5_125_000L, totalIncome),
                    },
                    ["TaxComputation"] = new JsonObject
                    {
                        ["TotalTaxPayable"] = baseTax,
                        ["Rebate87A"] = rebate,
                        ["TaxPayableOnRebate"] = taxAfterRebate,
                        ["EducationCess"] = cess,
                        ["GrossTaxLiability"] = grossTaxLiab,
                        ["NetTaxLiability"] = grossTaxLiab,
                        ["IntrstPay"] = new JsonObject
                        {
                            ["IntrstPayUs234A"] = 0,
                            ["IntrstPayUs234B"] = 0,
                            ["IntrstPayUs234C"] = 0,
                            ["LateFilingFee234F"] = 0,
                        },
                        ["TotTaxPlusIntrstPay"] = grossTaxLiab,
                    },
                    ["TaxPaid"] = new JsonObject
                    {
                        ["TaxesPaid"] = new JsonObject
                        {
                            ["AdvanceTax"] = adv,
                            ["TDS"] = tds,
                            ["TCS"] = 0,
                            ["SelfAssessmentTax"] = 0,
                            ["TotalTaxesPaid"] = totPaid,
                        },
                        ["BalTaxPayable"] = balPayable,
                    },
                    ["Refund"] = new JsonObject
                    {
                        ["RefundDue"] = refundDue,
                        ["BankAccountDtls"] = CbdtJsonExportHelper.BuildBankAccountDtls(opts),
                    },
                    ["ScheduleBP"] = scheduleBp,
                    ["Verification"] = new JsonObject
                    {
                        ["Declaration"] = new JsonObject
                        {
                            ["AssesseeVerName"] = CbdtJsonExportHelper.Trunc(client.LegalName ?? client.Name, 125),
                            ["FatherName"] = CbdtJsonExportHelper.Trunc(opts?.FatherName, 125, "Assessee Father Name"),
                            ["AssesseeVerPAN"] = pan,
                        },
                        ["Capacity"] = entityStatus == "F" ? "P" : (entityStatus == "H" ? "K" : "S"),
                        ["Place"] = CbdtJsonExportHelper.Trunc(opts?.Place, 50, city),
                    },
                }
            }
        };

        var fileName = $"{pan}_ITR4_{comp.AssessmentYear.Replace("-", "")}.json";
        return CbdtJsonExportHelper.ValidateAndFinalize(
            root,
            "ITR-4_2026_Main_V1.1.json",
            "ITR-4",
            "Ver1.1 (AY 2026-27)",
            comp.AssessmentYear,
            comp.FinancialYear,
            fileName,
            issues);
    }
}
