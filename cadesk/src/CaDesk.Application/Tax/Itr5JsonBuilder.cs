using System.Text.Json.Nodes;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Tax;
using static CaDesk.Application.Tax.BusinessItrJsonHelper;
using static CaDesk.Application.Tax.CbdtJsonExportHelper;

namespace CaDesk.Application.Tax;

/// <summary>
/// Builds official CBDT AY 2026-27 ITR-5 JSON (Firms / LLPs / AOP / BOI)
/// validated against embedded <c>ITR-5_2026_Main_V1.1.json</c>.
/// </summary>
public static class Itr5JsonBuilder
{
    public const string EmbeddedSchemaName = "ITR-5_2026_Main_V1.1.json";

    public static IncomeTaxJsonExportResult Build(
        TaxComputation comp,
        Client client,
        BusinessTaxComputationResultDto biz,
        ExportItrJsonRequest? req = null)
    {
        var issues = new List<IncomeTaxJsonExportIssue>();
        var input = biz.NormalizedInput;
        var active = biz.ActiveSummary;
        var (ayStart4, ayDashed, _, _) = ParseAyWindow(comp.AssessmentYear);
        string pan = NormalizePan(ExtractTaxId(client, "PAN"), issues, "ITR.ITR5.PartA_GEN1.OrgFirmInfo.PAN");
        string name = Trunc(client.LegalName ?? client.Name, 125, "PARTNERSHIP FIRM");
        string subTypeLower = (input.SubType ?? "").Trim().ToLowerInvariant();
        string orgTypeCode = subTypeLower switch
        {
            "aop" or "boi" => "14",
            _ => "1" // Firm / LLP
        };

        var addr = BuildBusinessAddress(client, req);
        string incorpDate = string.IsNullOrWhiteSpace(req?.DateOfIncorporation)
            ? (string.IsNullOrWhiteSpace(input.IncorporationDate) ? "2020-04-01" : input.IncorporationDate)
            : req.DateOfIncorporation!;

        var orgInfo = new JsonObject
        {
            ["AssesseeName"] = new JsonObject { ["SurNameOrOrgName"] = name },
            ["PAN"] = pan,
            ["Address"] = addr,
            ["DateOFFormOrIncorp"] = incorpDate,
            ["StatusOrCompanyType"] = orgTypeCode,
        };

        int returnFileSec = req?.ReturnFileSec is >= 11 and <= 20 ? req.ReturnFileSec.Value : 11;
        string dueDate = !string.IsNullOrWhiteSpace(req?.ItrFilingDueDate)
            ? req.ItrFilingDueDate!
            : (input.IsTaxAuditApplicable ? "2026-10-31" : "2026-08-31");

        var filingStatus = new JsonObject
        {
            ["ReturnFileSec"] = new JsonObject { ["IncomeTaxSec"] = returnFileSec },
            ["BusinessTrustFlag"] = "N",
            ["InvstmntFundRefrdSec115UB"] = "N",
            ["ResidentialStatus"] = "RES",
            ["ForeignExchangeFlag"] = "N",
            ["StartUpDPIITFlag"] = "N",
            ["InterMinisterialCertFlag"] = "N",
            ["ifMSME"] = "Y",
            ["FiiFpiFlag"] = "N",
            ["AsseseeRepFlg"] = "N",
            ["PartnerInFirmFlg"] = "N",
            ["HeldUnlistedEqShrPrYrFlg"] = "N",
            ["ItrFilingDueDate"] = dueDate,
        };

        var partAGen2 = new JsonObject
        {
            ["LiableSec44AAflg"] = "Y",
            ["IncDclrdUs"] = "N",
            ["LiableSec44ABflg"] = input.IsTaxAuditApplicable ? "Y" : "N",
            ["LiableSec92Eflg"] = "N",
            ["PrevYrMemPartChange"] = "N",
            ["NatOfBus"] = BuildNatOfBus(input.NatureOfBusinessCode, name),
        };

        if (input.Partners is { Count: > 0 })
        {
            var partnerArr = new JsonArray();
            foreach (var p in input.Partners)
            {
                var pObj = new JsonObject
                {
                    ["PartnerOrMemberName"] = Trunc(p.Name, 125, "Partner"),
                    ["AddressDetailWithZipCode"] = BuildAddressDetailWithZipCode(p.Address, p.City, p.StateCode, p.PinCode, client),
                    ["SharePercentage"] = Math.Round(p.SharePercent, 2),
                    ["Status"] = string.IsNullOrWhiteSpace(p.Status) ? "IND_WORKING" : p.Status,
                    ["RateOfInterest"] = Math.Round(p.InterestRatePercent, 2),
                    ["RemunerationPaid"] = ToRupees(p.RemunerationPaid),
                };
                if (!string.IsNullOrWhiteSpace(p.Pan))
                    pObj["PAN"] = p.Pan.Trim().ToUpperInvariant();
                partnerArr.Add(pObj);
            }
            partAGen2["PartnerOrMemberInfo"] = partnerArr;
        }

        // PARTA_BS (Firm / Non-Corporate Balance Sheet)
        long cap = ToRupees(input.PartnerOrShareCapital);
        long res = ToSignedRupees(input.ReservesAndSurplus);
        long ltBorrow = ToRupees(input.LongTermBorrowings);
        long stBorrow = ToRupees(input.ShortTermBorrowings);
        long dtl = ToRupees(input.DeferredTaxLiability);
        long totPartnerFund = cap + res;
        long totLoan = ltBorrow + stBorrow;
        long totSrc = totPartnerFund + totLoan + dtl;

        long faGross = ToRupees(input.FixedAssetsGrossBlock > 0m ? input.FixedAssetsGrossBlock : input.FixedAssetsNetBlock);
        long faDep = ToRupees(input.FixedAssetsAccumulatedDep);
        long faNet = Math.Max(0L, faGross - faDep);
        long cwipp = ToRupees(input.CapitalWip);
        long ltInv = ToRupees(input.NonCurrentInvestments);
        long stInv = ToRupees(input.CurrentInvestments);
        long invnt = ToRupees(input.Inventories);
        long debtors = ToRupees(input.TradeReceivables);
        long cash = ToRupees(input.CashAndBankBalances);
        long othCa = ToRupees(input.OtherCurrentAssets);
        long totCa = invnt + debtors + cash + othCa;
        long loansAdv = ToRupees(input.LoansAndAdvances);
        long totCaLoan = totCa + loansAdv;
        long cred = ToRupees(input.TradePayables);
        long othCl = ToRupees(input.OtherCurrentLiabilities);
        long totClProv = cred + othCl;
        long netCa = totCaLoan - totClProv;
        long totApply = (faNet + cwipp) + (ltInv + stInv) + netCa;

        var partABs = new JsonObject
        {
            ["FundSrc"] = new JsonObject
            {
                ["PartnerOrMemberFund"] = new JsonObject
                {
                    ["PartnerOrMemberCap"] = cap,
                    ["ResrNSurp"] = new JsonObject
                    {
                        ["RevResr"] = 0,
                        ["CapResr"] = 0,
                        ["StatResr"] = 0,
                        ["OthResr"] = 0,
                        ["CreditBalOfPLAccount"] = res,
                        ["TotResrNSurp"] = res,
                    },
                    ["TotPartnerOrMemberFund"] = totPartnerFund,
                },
                ["LoanFunds"] = new JsonObject
                {
                    ["SecrLoan"] = new JsonObject
                    {
                        ["ForeignCurrLoan"] = 0,
                        ["RupeeLoan"] = new JsonObject { ["FrmBank"] = ltBorrow, ["FrmOthrs"] = 0, ["TotRupeeLoan"] = ltBorrow },
                        ["TotSecrLoan"] = ltBorrow,
                    },
                    ["UnsecrLoan"] = new JsonObject
                    {
                        ["ForeignCurrencyLoans"] = 0,
                        ["RupeeLoan"] = new JsonObject { ["FrmBank"] = 0, ["FrmPersonSpcfdUs40A2b"] = 0, ["FrmOthrs"] = stBorrow, ["TotRupeeLoan"] = stBorrow },
                        ["TotUnSecrLoan"] = stBorrow,
                    },
                    ["TotLoanFund"] = totLoan,
                },
                ["DeferredTax"] = dtl,
                ["Advances"] = new JsonObject { ["FrmPersonSpcfdUs40A2b"] = 0, ["FrmOthers"] = 0, ["TotalAdvances"] = 0 },
                ["TotFundSrc"] = totSrc,
            },
            ["FundApply"] = new JsonObject
            {
                ["FixedAsset"] = new JsonObject
                {
                    ["GrossBlock"] = faGross,
                    ["Depreciation"] = faDep,
                    ["NetBlock"] = faNet,
                    ["CapWrkProg"] = cwipp,
                    ["TotFixedAsset"] = faNet + cwipp,
                },
                ["Investments"] = new JsonObject
                {
                    ["LongTermInv"] = new JsonObject
                    {
                        ["InvInProperty"] = 0,
                        ["EquityInstruments"] = new JsonObject { ["ListedEquities"] = 0, ["UnListedEquities"] = 0, ["Total"] = 0 },
                        ["PreferenceShares"] = 0,
                        ["GovtOrTrustSecurities"] = 0,
                        ["DebenturesOrBonds"] = 0,
                        ["MutualFunds"] = 0,
                        ["Others"] = ltInv,
                        ["TotLongTermInv"] = ltInv,
                    },
                    ["ShortTermInv"] = new JsonObject
                    {
                        ["EquityInstruments"] = new JsonObject { ["ListedEquities"] = 0, ["UnListedEquities"] = 0, ["Total"] = 0 },
                        ["PreferenceShares"] = 0,
                        ["GovtOrTrustSecurities"] = 0,
                        ["DebenturesOrBonds"] = 0,
                        ["MutualFunds"] = 0,
                        ["Others"] = stInv,
                        ["TotShortTermInv"] = stInv,
                    },
                    ["TotInvestments"] = ltInv + stInv,
                },
                ["CurrAssetLoanAdv"] = new JsonObject
                {
                    ["CurrAsset"] = new JsonObject
                    {
                        ["Inventories"] = new JsonObject
                        {
                            ["RawMatl"] = 0, ["WorkInProgress"] = 0, ["FinOrTradGood"] = invnt, ["StkInTrade"] = 0,
                            ["StoresConsumables"] = 0, ["LooseTools"] = 0, ["Others"] = 0, ["TotInventries"] = invnt,
                        },
                        ["SundryDebtorDtls"] = new JsonObject { ["OutstandindMorethanOneYr"] = 0, ["Others"] = debtors, ["TotalSundryDebtors"] = debtors },
                        ["CashOrBankBal"] = new JsonObject { ["BankBal"] = cash, ["CashinHand"] = 0, ["Others"] = 0, ["TotCashOrBankBal"] = cash },
                        ["OthCurrAsset"] = othCa,
                        ["TotCurrAsset"] = totCa,
                    },
                    ["LoanAdv"] = new JsonObject
                    {
                        ["AdvRecoverable"] = loansAdv, ["Deposits"] = 0, ["BalWithRevAuth"] = 0, ["TotLoanAdv"] = loansAdv,
                        ["LoanAdvIncluded"] = new JsonObject { ["PurposeOFBusOrProf"] = loansAdv, ["NotForPurposeOFBusOrProf"] = 0 },
                    },
                    ["TotCurrAssetLoanAdv"] = totCaLoan,
                    ["CurrLiabilitiesProv"] = new JsonObject
                    {
                        ["CurrLiabilities"] = new JsonObject
                        {
                            ["SundryCreditorDtls"] = new JsonObject { ["OutstandindMorethanOneYr"] = 0, ["Others"] = cred, ["TotalSundryCreditors"] = cred },
                            ["LiabForLeasedAsset"] = 0, ["AccrIntonLeasedAsset"] = 0, ["AccrIntNotDue"] = 0, ["IncRecvdInAdv"] = 0,
                            ["OtherPayables"] = othCl, ["TotCurrLiabilities"] = cred + othCl,
                        },
                        ["Provisions"] = new JsonObject { ["ITProvision"] = ToRupees(input.TaxProvisionCurrent), ["ELSuperAnnGratProvision"] = 0, ["OthProvision"] = 0, ["TotProvisions"] = ToRupees(input.TaxProvisionCurrent) },
                        ["TotCurrLiabilitiesProvision"] = totClProv + ToRupees(input.TaxProvisionCurrent),
                    },
                    ["NetCurrAsset"] = netCa - ToRupees(input.TaxProvisionCurrent),
                },
                ["MiscAdjust"] = new JsonObject { ["MiscExpndr"] = 0, ["DefTaxAsset"] = 0, ["AccumultedLosses"] = 0, ["TotMiscAdjust"] = 0 },
                ["TotFundApply"] = Math.Max(0L, totApply - ToRupees(input.TaxProvisionCurrent)),
            },
        };

        var partAPl = BuildPartAPl(input, biz, isItr6: false, isIndAs: false);
        var corpBp = BuildCorpScheduleBp(input, biz, isItr6: false);
        var (dpm, doa, dep) = BuildDepreciationSchedules(input, biz);
        var scheduleVia = BuildScheduleVia(input, biz);
        var scheduleSi = BuildScheduleSi(input, isItr6: false);
        var partBTi = BuildPartBTi(input, biz, isItr6: false);

        var scheduleAmt = new JsonObject
        {
            ["TotalIncItem13"] = ToRupees(biz.TotalTaxableIncome),
            ["AdjustedUnderSec115JC"] = ToRupees(biz.AmtAdjustedTotalIncome115JC),
            ["AdjustedUnderSec115JCIFSC"] = 0,
            ["AdjustedUnderSec115JCOther"] = ToRupees(biz.AmtAdjustedTotalIncome115JC),
            ["TaxPayableUnderSec115JC"] = ToRupees(active.MatOrAmtBaseTax),
        };

        long intr234A = ToRupees(input.Interest234A);
        long intr234B = ToRupees(input.Interest234B);
        long intr234C = ToRupees(input.Interest234C);
        long fee234F = ToRupees(input.LateFee234F);
        long grossTaxPayable = Math.Max(ToRupees(active.GrossTaxBeforeMatAmt), ToRupees(active.MatOrAmtTotalTax));
        long netLiability = Math.Max(0L, grossTaxPayable - ToRupees(active.MatOrAmtCreditUtilized));

        var compOfTaxLiab = new JsonObject
        {
            ["TaxPayableOnDeemedTI"] = new JsonObject
            {
                ["TaxDeemedTISec115JC"] = ToRupees(active.MatOrAmtBaseTax),
                ["Surcharge"] = ToRupees(active.MatOrAmtSurcharge),
                ["EducationCess"] = ToRupees(active.MatOrAmtCess),
                ["TotalTax"] = ToRupees(active.MatOrAmtTotalTax),
            },
            ["TaxPayableOnTI"] = new JsonObject
            {
                ["TaxAtNormalRates"] = ToRupees(active.TaxAtNormalRates),
                ["TaxAtSpecialRates"] = ToRupees(active.TaxAtSpecialRates),
                ["RebateOnAgriInc"] = 0,
                ["TaxPayableOnTotInc"] = ToRupees(active.BaseTax),
                ["Surcharge25ofSI"] = 0,
                ["SurchargeOnTaxPayable"] = ToRupees(active.Surcharge),
                ["Surcharge25ofSIBeforeMarginal"] = 0,
                ["SurchargeOnTaxPayableBeforeMarginal"] = ToRupees(active.SurchargeBeforeMarginalRelief),
                ["TotalSurcharge"] = ToRupees(active.Surcharge),
                ["EducationCess"] = ToRupees(active.HealthAndEducationCess),
                ["GrossTaxLiability"] = ToRupees(active.GrossTaxBeforeMatAmt),
            },
            ["GrossTaxPayable"] = grossTaxPayable,
            ["CreditUS115JD"] = ToRupees(active.MatOrAmtCreditUtilized),
            ["TaxPaidUnderCredit"] = netLiability,
            ["TaxRelief"] = new JsonObject { ["Section90"] = 0, ["Section91"] = 0, ["TotTaxRelief"] = 0 },
            ["NetTaxLiability"] = netLiability,
            ["IntrstPay"] = new JsonObject
            {
                ["IntrstPayUs234A"] = intr234A,
                ["IntrstPayUs234B"] = intr234B,
                ["IntrstPayUs234C"] = intr234C,
                ["LateFilingFee234F"] = fee234F,
                ["TotalIntrstPay"] = intr234A + intr234B + intr234C + fee234F,
            },
            ["AggregateTaxInterestLiability"] = ToRupees(active.TotalTaxLiability),
        };

        var partBTti = new JsonObject
        {
            ["ComputationOfTaxLiability"] = compOfTaxLiab,
            ["TaxPaid"] = BuildTaxPaid(input, active),
            ["Refund"] = BuildRefund(active, req),
        };

        var itr5 = new JsonObject
        {
            ["CreationInfo"] = BuildCreationInfo(req?.IntermediaryCity ?? req?.City ?? "Kochi"),
            ["Form_ITR5"] = new JsonObject
            {
                ["FormName"] = "ITR-5",
                ["Description"] = "For Firms, LLPs, AOPs and BOIs",
                ["AssessmentYear"] = ayStart4,
                ["SchemaVer"] = "Ver1.0",
                ["FormVer"] = "Ver1.0",
            },
            ["PartA_GEN1"] = new JsonObject
            {
                ["OrgFirmInfo"] = orgInfo,
                ["FilingStatus"] = filingStatus,
            },
            ["PartA_GEN2"] = partAGen2,
            ["PARTA_BS"] = partABs,
            ["PARTA_PL"] = partAPl,
            ["CorpScheduleBP"] = corpBp,
            ["ScheduleDPM"] = dpm,
            ["ScheduleDOA"] = doa,
            ["ScheduleDEP"] = dep,
            ["ScheduleAMT"] = scheduleAmt,
            ["ScheduleVIA"] = scheduleVia,
        };

        if (scheduleSi is not null) itr5["ScheduleSI"] = scheduleSi;

        itr5["PartB-TI"] = partBTi;
        itr5["PartB_TTI"] = partBTti;
        itr5["ScheduleIT"] = BuildScheduleIt(input);
        var tds2 = BuildScheduleTds2(input, issues, "ITR.ITR5.ScheduleTDS2");
        if (tds2 is not null) itr5["ScheduleTDS2"] = tds2;
        itr5["Verification"] = BuildBusinessVerification(client, req, isItr6: false, issues, "ITR.ITR5");

        var root = new JsonObject
        {
            ["ITR"] = new JsonObject { ["ITR5"] = itr5 }
        };

        string fileName = $"{pan}_ITR5_AY{ayDashed}.json";
        return ValidateAndFinalize(root, EmbeddedSchemaName, "ITR-5", "Ver1.0 (V1.1 Schema)", ayDashed, comp.FinancialYear, fileName, issues);
    }
}
