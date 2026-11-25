using System.Text.Json;
using System.Text.Json.Nodes;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Tax;

namespace CaDesk.Application.Tax;

/// <summary>
/// Builds and validates CBDT ITR-1 (Sahaj) JSON conforming to official AY 2026-27 schema V1.1.
/// </summary>
public static class Itr1JsonBuilder
{
    public static IncomeTaxJsonExportResult Build(
        TaxComputation comp,
        Client client,
        ExportItrJsonRequest? opts = null)
    {
        var issues = new List<IncomeTaxJsonExportIssue>();
        var comparison = JsonSerializer.Deserialize<RegimeComparisonDto>(
            comp.ComparisonData,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var pan = CbdtJsonExportHelper.NormalizePan(CbdtJsonExportHelper.ExtractTaxId(client, "PAN"), issues, "$.ITR.ITR1.PersonalInfo.PAN");
        var name = CbdtJsonExportHelper.Trunc(client.LegalName ?? client.Name, 75);
        var stateCode = CbdtJsonExportHelper.ResolveStateCode(opts?.StateCode, client);
        var city = CbdtJsonExportHelper.Trunc(opts?.City, 50, "Kochi");
        var pinCode = CbdtJsonExportHelper.ResolvePinInt(opts?.PinCode);
        var mobile = CbdtJsonExportHelper.ResolveMobileLong(opts?.MobileNo, client.Phone);
        var email = CbdtJsonExportHelper.ResolveEmail(opts?.EmailAddress, client.Email);
        var dob = string.IsNullOrWhiteSpace(opts?.Dob) ? "1990-01-01" : opts.Dob.Trim();
        var optOutNew = opts?.OptOutNewRegime ?? string.Equals(comp.OptedRegime, "old", StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(comp.TaxpayerCategory, TaxpayerCategory.Individual, StringComparison.OrdinalIgnoreCase))
            issues.Add(new("error", "$.ITR.ITR1.PersonalInfo", "ITR1-CATEGORY", $"ITR-1 Sahaj is restricted to Resident Individuals (client category is '{comp.TaxpayerCategory}')."));

        if (comp.BusinessIncome > 0m)
            issues.Add(new("error", "$.ITR.ITR1.ITR1_IncomeDeductions", "ITR1-NO-PGBP", "ITR-1 cannot include Business or Profession income. Export ITR-4 (Sugam) or ITR-3 instead."));

        if (comp.CapitalGainsStcg > 0m || comp.CapitalGainsLtcg > 125_000m)
            issues.Add(new("error", "$.ITR.ITR1.ITR1_IncomeDeductions", "ITR1-CAPGAINS", "Taxable STCG or LTCG exceeding Rs 1,25,000 exemption requires ITR-2 / ITR-3 instead of ITR-1."));

        if (comp.TotalTaxableIncome > 5_000_000m)
            issues.Add(new("error", "$.ITR.ITR1.ITR1_IncomeDeductions.TotalIncome", "ITR1-50L-LIMIT", "Total taxable income exceeds Rs 50,00,000 limit for ITR-1 (Sahaj)."));

        var (_, ayDashed, _, _) = CbdtJsonExportHelper.ParseAyWindow(comp.AssessmentYear);
        if (!string.Equals(ayDashed, "2026-27", StringComparison.Ordinal))
            issues.Add(new("warning", "$.ITR.ITR1.Form_ITR1.AssessmentYear", "ITR1-AY-SCHEMA",
                $"Generated using official CBDT AY 2026-27 schema V1.1 while computation was saved for AY {comp.AssessmentYear}."));

        var grossSal = CbdtJsonExportHelper.ToRupees(comp.GrossSalary);
        var ded16 = CbdtJsonExportHelper.ToRupees(comp.SalaryDeductions);
        var netSal = CbdtJsonExportHelper.ToRupees(comp.NetSalary);
        var ifos = CbdtJsonExportHelper.ToRupees(comp.OtherSourcesIncome);
        var hp = CbdtJsonExportHelper.ToSignedRupees(comp.HousePropertyIncome);
        var gti = CbdtJsonExportHelper.ToSignedRupees(comp.NetSalary + comp.HousePropertyIncome + comp.OtherSourcesIncome);

        // Recover Chapter VI-A items if Old Regime was chosen (or zero in New Regime)
        var chapTotal = optOutNew ? CbdtJsonExportHelper.ToRupees(comp.ChapterViaDeductions) : 0L;
        var totalIncome = Math.Max(0L, Math.Min(5_125_000L, gti - chapTotal));

        var baseTax = CbdtJsonExportHelper.ToRupees(comp.TaxOnIncome);
        var rebate = Math.Min(60_000L, CbdtJsonExportHelper.ToRupees(comp.Rebate87A));
        var taxAfterRebate = Math.Max(0L, baseTax - rebate);
        var cess = CbdtJsonExportHelper.ToRupees(comp.Cess);
        var grossTaxLiab = taxAfterRebate + CbdtJsonExportHelper.ToRupees(comp.Surcharge) + cess;

        var tds = CbdtJsonExportHelper.ToRupees(opts?.TdsPaid ?? comp.TdsTcsCredit);
        var adv = CbdtJsonExportHelper.ToRupees(opts?.AdvanceTaxPaid ?? comp.AdvanceTaxPaid);
        var sat = CbdtJsonExportHelper.ToRupees(comp.SelfAssessmentTax);
        var totTaxesPaid = tds + adv + sat;

        var balPayable = Math.Max(0L, grossTaxLiab - totTaxesPaid);
        var refundDue = Math.Max(0L, totTaxesPaid - grossTaxLiab);

        if (refundDue > 0 && string.IsNullOrWhiteSpace(opts?.BankAccountNo))
            issues.Add(new("warning", "$.ITR.ITR1.Refund.BankAccountDtls", "ITR1-BANK-REFUND",
                $"Refund of Rs {refundDue:N0} is due; supply Bank IFSC & Account No. before uploading JSON to e-Filing."));

        var empCat = string.IsNullOrWhiteSpace(opts?.EmployerCategory) ? "OTH" : opts.EmployerCategory.Trim().ToUpperInvariant();
        var incomeDeductions = new JsonObject
        {
            ["GrossSalary"] = grossSal,
            ["NetSalary"] = netSal,
            ["DeductionUs16"] = ded16,
            ["IncomeFromSal"] = netSal,
            ["IncomeOthSrc"] = ifos,
            ["GrossTotIncome"] = gti,
            ["GrossTotIncomeIncLTCG112A"] = gti,
            ["UsrDeductUndChapVIA"] = CbdtJsonExportHelper.BuildItr1UsrChapVia(chapTotal, 0, 0, 0, 0, 0, chapTotal),
            ["DeductUndChapVIA"] = CbdtJsonExportHelper.BuildItr1AllowedChapVia(chapTotal, 0, 0, 0, 0, 0, chapTotal),
            ["TotalIncome"] = totalIncome,
        };
        if (hp != 0)
        {
            incomeDeductions["TypeOfHP"] = hp < 0 ? "S" : "L";
            incomeDeductions["TotalIncomeOfHP"] = hp;
        }

        var root = new JsonObject
        {
            ["ITR"] = new JsonObject
            {
                ["ITR1"] = new JsonObject
                {
                    ["CreationInfo"] = CbdtJsonExportHelper.BuildCreationInfo(opts?.IntermediaryCity ?? city),
                    ["Form_ITR1"] = new JsonObject
                    {
                        ["FormName"] = "ITR-1",
                        ["Description"] = "For Indls having Income from Salary, one House Property and Other Sources",
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
                        ["EmployerCategory"] = empCat,
                    },
                    ["FilingStatus"] = new JsonObject
                    {
                        ["ReturnFileSec"] = opts?.ReturnFileSec is >= 11 and <= 20 ? opts.ReturnFileSec.Value : 11,
                        ["OptOutNewTaxRegime"] = optOutNew ? "Y" : "N",
                        ["AsseseeRepFlg"] = "N",
                        ["ItrFilingDueDate"] = "2026-07-31",
                    },
                    ["ITR1_IncomeDeductions"] = incomeDeductions,
                    ["ITR1_TaxComputation"] = new JsonObject
                    {
                        ["TotalTaxPayable"] = baseTax,
                        ["Rebate87A"] = rebate,
                        ["TaxPayableOnRebate"] = taxAfterRebate,
                        ["EducationCess"] = cess,
                        ["GrossTaxLiability"] = grossTaxLiab,
                        ["Section89"] = 0,
                        ["NetTaxLiability"] = grossTaxLiab,
                        ["TotalIntrstPay"] = 0,
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
                            ["SelfAssessmentTax"] = sat,
                            ["TotalTaxesPaid"] = totTaxesPaid,
                        },
                        ["BalTaxPayable"] = balPayable,
                    },
                    ["Refund"] = new JsonObject
                    {
                        ["RefundDue"] = refundDue,
                        ["BankAccountDtls"] = CbdtJsonExportHelper.BuildBankAccountDtls(opts),
                    },
                    ["Verification"] = new JsonObject
                    {
                        ["Declaration"] = new JsonObject
                        {
                            ["AssesseeVerName"] = CbdtJsonExportHelper.Trunc(client.LegalName ?? client.Name, 127),
                            ["FatherName"] = CbdtJsonExportHelper.Trunc(opts?.FatherName, 125, "Assessee Father Name"),
                            ["AssesseeVerPAN"] = pan,
                        },
                        ["Capacity"] = "S",
                        ["Place"] = CbdtJsonExportHelper.Trunc(opts?.Place, 50, city),
                    },
                }
            }
        };

        var fileName = $"{pan}_ITR1_{comp.AssessmentYear.Replace("-", "")}.json";
        _ = comparison;
        return CbdtJsonExportHelper.ValidateAndFinalize(
            root,
            "ITR-1_2026_Main_V1.1.json",
            "ITR-1",
            "Ver1.1 (AY 2026-27)",
            comp.AssessmentYear,
            comp.FinancialYear,
            fileName,
            issues);
    }
}
