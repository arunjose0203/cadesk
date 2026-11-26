using System.Text.Json.Nodes;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Tax;
using static CaDesk.Application.Tax.BusinessItrJsonHelper;
using static CaDesk.Application.Tax.CbdtJsonExportHelper;
using static CaDesk.Application.Tax.PersonalItrJsonHelper;

namespace CaDesk.Application.Tax;

/// <summary>
/// Builds official CBDT AY 2026-27 ITR-2 JSON (Individuals/HUFs with Multiple House Properties,
/// Capital Gains u/s 111A/112A/112, VDA 115BBH, Foreign Assets ScheduleFA & ScheduleAL)
/// validated against embedded <c>ITR-2_2026_Main_V1.2.json</c>.
/// </summary>
public static class Itr2JsonBuilder
{
    public const string EmbeddedSchemaName = "ITR-2_2026_Main_V1.2.json";

    public static IncomeTaxJsonExportResult Build(
        TaxComputation comp,
        Client client,
        PersonalExtendedTaxResultDto ext,
        ExportItrJsonRequest? req = null)
    {
        var issues = new List<IncomeTaxJsonExportIssue>();
        var input = ext.NormalizedInput;
        var active = ext.ActiveSummary;
        var (ayStart4, ayDashed, _, _) = ParseAyWindow(comp.AssessmentYear);
        string pan = NormalizePan(ExtractTaxId(client, "PAN"), issues, "ITR.ITR2.PartA_GEN1.PersonalInfo.PAN");

        var personalInfo = BuildPersonalInfo(client, ext, req, issues, "ITR.ITR2.PartA_GEN1.PersonalInfo.PAN");
        int returnFileSec = req?.ReturnFileSec is >= 11 and <= 20 ? req.ReturnFileSec.Value : 11;
        bool isOptOut = active.Regime == "old";

        var filingStatus = new JsonObject
        {
            ["ReturnFileSec"] = returnFileSec,
            ["OptOutNewTaxRegime"] = isOptOut ? "Y" : "N",
            ["SeventhProvisio139"] = "N",
            ["ResidentialStatus"] = "RES",
            ["HeldUnlistedEqShrPrYrFlg"] = "N",
            ["FiiFpiFlag"] = "N",
            ["ItrFilingDueDate"] = "2026-07-31",
        };

        var schedS = BuildScheduleS(input, active, client, req);
        var schedHp = BuildScheduleHp(input, active, client, req);
        var (schedCg, sched112A, schedVda) = BuildCapitalGainsAndVda(input, active, isItr3: false);
        var schedOs = BuildScheduleOs(input, active, isItr3: false);
        var (cyla, bfla) = BuildCylaAndBfla(active, isItr3: false);
        var schedVia = BuildPersonalScheduleVia(input, active);
        var schedSi = BuildPersonalScheduleSi(active);
        var schedFa = BuildScheduleFa(input);
        var schedAl = BuildScheduleAl(input, active, client, req, isItr3: false);

        long salNet = ToRupees(active.NetSalary);
        long hpNet = ToSignedRupees(active.HousePropertyIncome);
        long st20 = ToRupees(active.Stcg111A);
        long stNorm = ToRupees(active.StcgNormal);
        long ltTot = ToRupees(active.Ltcg112A + active.Ltcg112);
        long vda = ToRupees(active.VdaIncome115BBH);
        long cgTot = st20 + stNorm + ltTot + vda;
        long osNet = ToRupees(active.OtherSourcesIncome);
        long gti = ToRupees(active.GrossTotalIncome);
        long chapVia = ToRupees(active.ChapterViaDeductions);
        long taxable = ToRupees(active.TaxableIncome);
        long splInc = st20 + ltTot + vda;
        var hpSplit = ComputeCurrentYearHpSetOff(active);

        var partBTi = new JsonObject
        {
            ["Salaries"] = salNet,
            ["IncomeFromHP"] = hpSplit.HpPositive,
            ["CapGain"] = new JsonObject
            {
                ["ShortTerm"] = new JsonObject
                {
                    ["ShortTerm20Per"] = st20, ["ShortTerm30Per"] = 0, ["ShortTermAppRate"] = stNorm,
                    ["ShortTermSplRateDTAA"] = 0, ["TotalShortTerm"] = st20 + stNorm,
                },
                ["LongTerm"] = new JsonObject
                {
                    ["LongTerm12_5Per"] = ltTot, ["LongTermSplRateDTAA"] = 0, ["TotalLongTerm"] = ltTot,
                },
                ["ShortTermLongTermTotal"] = st20 + stNorm + ltTot,
                ["CapGains30Per115BBH"] = vda,
                ["TotalCapGains"] = cgTot,
            },
            ["IncFromOS"] = new JsonObject
            {
                ["OtherSrcThanOwnRaceHorse"] = osNet, ["IncChargblSplRate"] = 0, ["FromOwnRaceHorse"] = 0, ["TotIncFromOS"] = osNet,
            },
            ["TotalTI"] = hpSplit.TotalPositiveTi,
            ["CurrentYearLoss"] = hpSplit.HpSetOff,
            ["BalanceAfterSetoffLosses"] = hpSplit.BalanceAfterLoss,
            ["BroughtFwdLossesSetoff"] = 0,
            ["GrossTotalIncome"] = gti,
            ["IncChargeTaxSplRate111A112"] = st20 + ltTot,
            ["DeductionsUnderScheduleVIA"] = chapVia,
            ["TotalIncome"] = taxable,
            ["IncChargeableTaxSplRates"] = splInc,
            ["NetAgricultureIncomeOrOtherIncomeForRate"] = 0,
            ["AggregateIncome"] = taxable,
            ["LossesOfCurrentYearCarriedFwd"] = hpSplit.HpCarryFwd,
            ["DeemedIncomeUs115JC"] = 0,
        };

        long slabTax = ToRupees(active.SlabTaxOnNormalIncome);
        long splTax = ToRupees(active.SpecialRateTax);
        long rebate87A = ToRupees(active.Rebate87A);
        long taxAfterRebate = Math.Max(0L, slabTax + splTax - rebate87A);
        long sc = ToRupees(active.Surcharge);
        long cess = ToRupees(active.Cess);
        long grossTax = taxAfterRebate + sc + cess;
        long i234a = ToRupees(input.Interest234A);
        long i234b = ToRupees(input.Interest234B);
        long i234c = ToRupees(input.Interest234C);
        long f234f = ToRupees(input.LateFee234F);

        var partBTti = new JsonObject
        {
            ["TaxPayDeemedTotIncUs115JC"] = 0,
            ["Surcharge"] = 0,
            ["HealthEduCess"] = 0,
            ["TotalTaxPayablDeemedTotInc"] = 0,
            ["ComputationOfTaxLiability"] = new JsonObject
            {
                ["TaxPayableOnTI"] = new JsonObject
                {
                    ["TaxAtNormalRatesOnAggrInc"] = slabTax,
                    ["TaxAtSpecialRates"] = splTax,
                    ["RebateOnAgriInc"] = 0,
                    ["TaxPayableOnTotInc"] = slabTax + splTax,
                },
                ["Rebate87A"] = rebate87A,
                ["TaxPayableOnRebate"] = taxAfterRebate,
                ["Surcharge25ofSI"] = 0,
                ["SurchargeOnAboveCrore"] = sc,
                ["Surcharge25ofSIBeforeMarginal"] = 0,
                ["SurchargeOnAboveCroreBeforeMarginal"] = sc,
                ["TotalSurcharge"] = sc,
                ["EducationCess"] = cess,
                ["GrossTaxLiability"] = grossTax,
                ["GrossTaxPayable"] = grossTax,
                ["CreditUS115JD"] = 0,
                ["TaxPayAfterCreditUs115JD"] = grossTax,
                ["NetTaxLiability"] = grossTax,
                ["IntrstPay"] = new JsonObject
                {
                    ["IntrstPayUs234A"] = i234a, ["IntrstPayUs234B"] = i234b, ["IntrstPayUs234C"] = i234c,
                    ["LateFilingFee234F"] = f234f, ["TotalIntrstPay"] = i234a + i234b + i234c + f234f,
                },
                ["AggregateTaxInterestLiability"] = ToRupees(active.TotalTaxLiability),
            },
            ["TaxPaid"] = BuildPersonalTaxesPaid(input, active),
            ["Refund"] = new JsonObject
            {
                ["RefundDue"] = active.NetPayableOrRefund < 0m ? ToRupees(-active.NetPayableOrRefund) : 0L,
                ["BankAccountDtls"] = new JsonObject
                {
                    ["BankDtlsFlag"] = !string.IsNullOrWhiteSpace(req?.IfscCode) && !string.IsNullOrWhiteSpace(req?.BankAccountNo) ? "Y" : "N",
                    ["AddtnlBankDetails"] = !string.IsNullOrWhiteSpace(req?.IfscCode) && !string.IsNullOrWhiteSpace(req?.BankAccountNo)
                        ? new JsonArray
                        {
                            new JsonObject
                            {
                                ["IFSCCode"] = req.IfscCode.Trim().ToUpperInvariant(),
                                ["BankName"] = Trunc(req.BankName, 125, "State Bank of India"),
                                ["BankAccountNo"] = Trunc(req.BankAccountNo, 20),
                                ["AccountType"] = "SB",
                                ["UseForRefund"] = "true",
                            }
                        }
                        : null,
                }
            },
            ["AssetOutIndiaFlag"] = (input.ForeignAssets is { Count: > 0 }) ? "YES" : "NO",
        };
        if (partBTti["Refund"]!["BankAccountDtls"]!["AddtnlBankDetails"] is null)
            partBTti["Refund"]!["BankAccountDtls"]!.AsObject().Remove("AddtnlBankDetails");

        var itr2 = new JsonObject
        {
            ["CreationInfo"] = BuildCreationInfo(req?.IntermediaryCity ?? req?.City ?? "Kochi"),
            ["Form_ITR2"] = new JsonObject
            {
                ["FormName"] = "ITR-2",
                ["Description"] = "For Individuals and HUFs not having income from profits and gains of business or profession",
                ["AssessmentYear"] = ayStart4,
                ["SchemaVer"] = "Ver1.0",
                ["FormVer"] = "Ver1.0",
            },
            ["PartA_GEN1"] = new JsonObject
            {
                ["PersonalInfo"] = personalInfo,
                ["FilingStatus"] = filingStatus,
            },
        };

        if (schedS is not null) itr2["ScheduleS"] = schedS;
        if (schedHp is not null) itr2["ScheduleHP"] = schedHp;
        if (schedCg is not null) itr2["ScheduleCGFor23"] = schedCg;
        if (sched112A is not null) itr2["Schedule112A"] = sched112A;
        if (schedVda is not null) itr2["ScheduleVDA"] = schedVda;
        if (schedOs is not null) itr2["ScheduleOS"] = schedOs;
        itr2["ScheduleCYLA"] = cyla;
        itr2["ScheduleBFLA"] = bfla;
        itr2["ScheduleVIA"] = schedVia;
        if (schedSi is not null) itr2["ScheduleSI"] = schedSi;
        if (schedFa is not null) itr2["ScheduleFA"] = schedFa;
        if (schedAl is not null) itr2["ScheduleAL"] = schedAl;
        itr2["PartB-TI"] = partBTi;
        itr2["PartB_TTI"] = partBTti;

        if (input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid > 0m || (input.AdvanceTaxChallans is { Count: > 0 }))
        {
            itr2["ScheduleIT"] = BuildScheduleIt(new CalculateBusinessTaxRequest(
                ClientId: comp.ClientId,
                AdvanceTaxPaid: input.AdvanceTaxPaid,
                SelfAssessmentTaxPaid: input.SelfAssessmentTaxPaid,
                AdvanceTaxChallans: input.AdvanceTaxChallans));
        }

        var tds1 = BuildScheduleTds1(input, active);
        if (tds1 is not null) itr2["ScheduleTDS1"] = tds1;
        var tds2 = BuildScheduleTds2(new CalculateBusinessTaxRequest(
            ClientId: comp.ClientId,
            TdsCredit: input.TdsOtherThanSalary,
            TdsCreditEntries: input.TdsCreditEntries), issues, "ITR.ITR2.ScheduleTDS2");
        if (tds2 is not null) itr2["ScheduleTDS2"] = tds2;

        string signerPan = !string.IsNullOrWhiteSpace(req?.VerificationPan) && req.VerificationPan.Trim().Length == 10
            ? req.VerificationPan.Trim().ToUpperInvariant()
            : (pan[3] == 'P' ? pan : "ABCPA1234A");
        itr2["Verification"] = new JsonObject
        {
            ["Declaration"] = new JsonObject
            {
                ["AssesseeVerName"] = Trunc(req?.VerificationName, 125, client.LegalName ?? client.Name),
                ["FatherName"] = Trunc(req?.FatherName, 125, "NA"),
                ["AssesseeVerPAN"] = signerPan,
            },
            ["Capacity"] = ext.StatusType == "H" ? "K" : "S",
            ["Place"] = Trunc(req?.Place ?? req?.City, 50, "Kochi"),
            ["Date"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        };

        var root = new JsonObject { ["ITR"] = new JsonObject { ["ITR2"] = itr2 } };
        string fileName = $"{pan}_ITR2_AY{ayDashed}.json";
        return ValidateAndFinalize(root, EmbeddedSchemaName, "ITR-2", "Ver1.0 (V1.2 Schema)", ayDashed, comp.FinancialYear, fileName, issues);
    }
}
