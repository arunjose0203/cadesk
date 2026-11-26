using System.Text.Json.Nodes;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Tax;
using static CaDesk.Application.Tax.BusinessItrJsonHelper;
using static CaDesk.Application.Tax.CbdtJsonExportHelper;
using static CaDesk.Application.Tax.PersonalItrJsonHelper;

namespace CaDesk.Application.Tax;

/// <summary>
/// Builds official CBDT AY 2026-27 ITR-3 JSON (Proprietors & Professionals maintaining Books u/s 44AA / 44AB)
/// validated against embedded <c>ITR-3_2026_Main_V1.1.json</c>.
/// </summary>
public static class Itr3JsonBuilder
{
    public const string EmbeddedSchemaName = "ITR-3_2026_Main_V1.1.json";

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
        string pan = NormalizePan(ExtractTaxId(client, "PAN"), issues, "ITR.ITR3.PartA_GEN1.PersonalInfo.PAN");

        var personalInfo = BuildPersonalInfo(client, ext, req, issues, "ITR.ITR3.PartA_GEN1.PersonalInfo.PAN");
        int returnFileSec = req?.ReturnFileSec is >= 11 and <= 21 ? req.ReturnFileSec.Value : 11;
        bool isOldRegime = active.Regime == "old";
        string dueDate = input.IsTaxAuditApplicable ? "2026-10-31" : "2026-08-31";

        var filingStatus = new JsonObject
        {
            ["ReturnFileSec"] = returnFileSec,
            ["IncFrmBusOrProf"] = "Y",
            ["OptOldRegimeCurrAY"] = isOldRegime ? "Y" : "N",
            ["SeventhProvisio139"] = "N",
            ["ResidentialStatus"] = "RES",
            ["HeldUnlistedEqShrPrYrFlg"] = "N",
            ["ForeignExchangeFlag"] = "N",
            ["FiiFpiFlag"] = "N",
            ["ItrFilingDueDate"] = dueDate,
        };

        var partAGen2 = new JsonObject
        {
            ["AuditInfo"] = new JsonObject
            {
                ["LiableSec44AAflg"] = "Y",
                ["IncDclrdUs"] = "N",
                ["LiableSec44ABflg"] = input.IsTaxAuditApplicable ? "Y" : "N",
                ["LiableSec92Eflg"] = "N",
                ["AccountAuditFlag"] = input.IsTaxAuditApplicable ? "Y" : "N",
            },
            ["NatOfBus"] = BuildNatOfBus(input.NatureOfBusinessCode, input.BusinessName),
        };

        // PARTA_BS (Proprietorship / HUF Balance Sheet with PropFund)
        long cap = ToRupees(input.PropCapital);
        long res = ToSignedRupees(input.ReservesAndSurplus);
        long totPropFund = cap + res;
        long secLoan = ToRupees(input.SecuredLoans);
        long unsecLoan = ToRupees(input.UnsecuredLoans);
        long totLoan = secLoan + unsecLoan;
        long totFundSrc = totPropFund + totLoan;

        long faGross = ToRupees(input.FixedAssetsGrossBlock > 0m ? input.FixedAssetsGrossBlock : input.FixedAssetsNetBlock);
        long faDep = ToRupees(input.FixedAssetsAccumulatedDep);
        long faNet = Math.Max(0L, faGross - faDep);
        long inv = ToRupees(input.Inventories);
        long debtors = ToRupees(input.TradeReceivables);
        long cash = ToRupees(input.CashAndBankBalances);
        long totCa = inv + debtors + cash;
        long loansAdv = ToRupees(input.LoansAndAdvancesAsset);
        long totCaLoan = totCa + loansAdv;
        long cred = ToRupees(input.TradePayables);
        long othCl = ToRupees(input.OtherCurrentLiabilities);
        long totCl = cred + othCl;
        long netCa = totCaLoan - totCl;

        var partABs = new JsonObject
        {
            ["FundSrc"] = new JsonObject
            {
                ["PropFund"] = new JsonObject
                {
                    ["PropCap"] = cap,
                    ["ResrNSurp"] = new JsonObject { ["RevResr"] = 0, ["CapResr"] = 0, ["StatResr"] = 0, ["OthResr"] = res, ["TotResrNSurp"] = res },
                    ["TotPropFund"] = totPropFund,
                },
                ["LoanFunds"] = new JsonObject
                {
                    ["SecrLoan"] = new JsonObject
                    {
                        ["ForeignCurrLoan"] = 0,
                        ["RupeeLoan"] = new JsonObject { ["FrmBank"] = secLoan, ["FrmOthrs"] = 0, ["TotRupeeLoan"] = secLoan },
                        ["TotSecrLoan"] = secLoan,
                    },
                    ["UnsecrLoan"] = new JsonObject { ["FrmBank"] = 0, ["FrmOthrs"] = unsecLoan, ["TotUnSecrLoan"] = unsecLoan },
                    ["TotLoanFund"] = totLoan,
                },
                ["DeferredTax"] = 0,
                ["Advances"] = new JsonObject { ["TotalAdvances"] = 0 },
                ["TotFundSrc"] = Math.Max(0L, totFundSrc),
            },
            ["FundApply"] = new JsonObject
            {
                ["FixedAsset"] = new JsonObject
                {
                    ["GrossBlock"] = faGross, ["Depreciation"] = faDep, ["NetBlock"] = faNet, ["CapWrkProg"] = 0, ["TotFixedAsset"] = faNet,
                },
                ["Investments"] = new JsonObject
                {
                    ["LongTermInv"] = new JsonObject { ["GovtOthSecQuoted"] = 0, ["GovOthSecUnQoted"] = 0, ["TotLongTermInv"] = 0 },
                    ["TradeInv"] = new JsonObject { ["EquityShares"] = 0, ["PreferShares"] = 0, ["Debenture"] = 0, ["TotTradeInv"] = 0 },
                    ["TotInvestments"] = 0,
                },
                ["CurrAssetLoanAdv"] = new JsonObject
                {
                    ["CurrAsset"] = new JsonObject
                    {
                        ["Inventories"] = new JsonObject { ["StoresConsumables"] = 0, ["RawMatl"] = 0, ["StkInProcess"] = 0, ["FinOrTradGood"] = inv, ["TotInventries"] = inv },
                        ["SndryDebtors"] = debtors,
                        ["CashOrBankBal"] = new JsonObject { ["CashinHand"] = 0, ["BankBal"] = cash, ["TotCashOrBankBal"] = cash },
                        ["OthCurrAsset"] = 0,
                        ["TotCurrAsset"] = totCa,
                    },
                    ["LoanAdv"] = new JsonObject { ["AdvRecoverable"] = loansAdv, ["Deposits"] = 0, ["BalWithRevAuth"] = 0, ["TotLoanAdv"] = loansAdv },
                    ["TotCurrAssetLoanAdv"] = totCaLoan,
                    ["CurrLiabilitiesProv"] = new JsonObject
                    {
                        ["CurrLiabilities"] = new JsonObject { ["SundryCred"] = cred, ["LiabForLeasedAsset"] = 0, ["AccrIntonLeasedAsset"] = 0, ["AccrIntNotDue"] = othCl, ["TotCurrLiabilities"] = totCl },
                        ["Provisions"] = new JsonObject { ["ITProvision"] = 0, ["ELSuperAnnGratProvision"] = 0, ["OthProvision"] = 0, ["TotProvisions"] = 0 },
                        ["TotCurrLiabilitiesProvision"] = totCl,
                    },
                    ["NetCurrAsset"] = netCa,
                },
                ["MiscAdjust"] = new JsonObject { ["MiscExpndr"] = 0, ["DefTaxAsset"] = 0, ["AccumaltedLosses"] = 0, ["TotMiscAdjust"] = 0 },
                ["TotFundApply"] = Math.Max(0L, faNet + netCa),
            },
        };

        // PARTA_PL (ITR-3 schema has CreditsToPL, DebitsToPL, TaxProvAppr as top-level children of PARTA_PL)
        var bizEquivalentReq = new CalculateBusinessTaxRequest(
            ClientId: comp.ClientId,
            RevenueFromOperations: input.RevenueFromOperations,
            OtherIncome: input.OtherBusinessIncome,
            PurchasesAndDirectCost: input.PurchasesAndDirectCost,
            ChangesInInventories: input.ChangesInInventories,
            EmployeeBenefitExpense: input.EmployeeBenefitExpense,
            FinanceCost: input.FinanceCost,
            DepreciationPerBooks: input.DepreciationPerBooks,
            AuditFee: input.AuditFee,
            OtherExpenses: input.OtherBusinessExpenses,
            DisallowSec36: input.DisallowSec36,
            DisallowSec37: input.DisallowSec37,
            DisallowSec40a: input.DisallowSec40a,
            DisallowSec40A3: input.DisallowSec40A3,
            DisallowSec43B: input.DisallowSec43B,
            DisallowSec43BhMsme: input.DisallowSec43BhMsme,
            IncomeTaxDepreciationSec32: input.IncomeTaxDepreciationSec32,
            BroughtForwardBusinessLoss: input.BroughtForwardBusinessLoss,
            DepreciationBlocks: input.DepreciationBlocks,
            AdvanceTaxPaid: input.AdvanceTaxPaid,
            SelfAssessmentTaxPaid: input.SelfAssessmentTaxPaid,
            TdsCredit: input.TdsOtherThanSalary,
            AdvanceTaxChallans: input.AdvanceTaxChallans,
            TdsCreditEntries: input.TdsCreditEntries);

        var bizEquivalentRes = BusinessTaxEngine.Compute(bizEquivalentReq);
        var sharedPl = BuildPartAPl(bizEquivalentReq, bizEquivalentRes, isItr6: true, isIndAs: false);
        var debitAcnt = sharedPl["DebitsToPL"]!["DebitPlAcnt"]!.AsObject();
        long pbt = ToSignedRupees(active.BusinessBookProfit);

        var itr3Pl = new JsonObject
        {
            ["CreditsToPL"] = sharedPl["CreditsToPL"]!.DeepClone(),
            ["DebitsToPL"] = debitAcnt.DeepClone(),
            ["TaxProvAppr"] = new JsonObject
            {
                ["ProvForCurrTax"] = 0, ["ProvDefTax"] = 0, ["ProfitAfterTax"] = pbt, ["BalBFPrevYr"] = 0,
                ["AmtAvlAppr"] = pbt, ["TrfToReserves"] = 0, ["ProprietorAccBalTrf"] = pbt,
            },
            ["NoBooksOfAccPL"] = new JsonObject
            {
                ["GrossReceipt"] = 0, ["GrsRcptAccPayeeOrBankMode"] = 0, ["GrsRcptOtherMode"] = 0,
                ["GrossProfit"] = 0, ["Expenses"] = 0, ["NetProfit"] = 0,
                ["GrossReceiptPrf"] = 0, ["GrsRcptAccPayeeOrBankModePrf"] = 0, ["GrsRcptOtherModePrf"] = 0,
                ["GrossProfitPrf"] = 0, ["ExpensesPrf"] = 0, ["NetProfitPrf"] = 0, ["TotBusinessProfession"] = 0,
            },
            ["TurnverFrmSpecActivity"] = 0,
            ["NetIncomeFrmSpecActivity"] = 0,
        };

        // ITR3ScheduleBP
        long depBooks = ToRupees(input.DepreciationPerBooks);
        long depIt = ToRupees(input.IncomeTaxDepreciationSec32);
        long d36 = ToRupees(input.DisallowSec36);
        long d37 = ToRupees(input.DisallowSec37);
        long d40 = ToRupees(input.DisallowSec40a);
        long d40a = ToRupees(input.DisallowSec40A3);
        long d43b = ToRupees(input.DisallowSec43B);
        long d43bh = ToRupees(input.DisallowSec43BhMsme);
        long pgbpNet = ToSignedRupees(active.BusinessIncomeNetPgbp);
        long totAdd = pbt + depBooks + d36 + d37 + d40 + d40a + d43b + d43bh;

        var itr3Bp = new JsonObject
        {
            ["BusinessIncOthThanSpec"] = new JsonObject
            {
                ["ProfBfrTaxPL"] = pbt,
                ["NetPLFromSpecBus"] = 0,
                ["NetPLFromSpecifiedBus"] = 0,
                ["IncRecCredPLOthHeadDtls"] = new JsonObject { ["Salary"] = 0, ["HouseProperty"] = 0, ["CapitalGains"] = 0, ["OtherSources"] = 0, ["Dividend"] = 0, ["OtherThanDividend"] = 0, ["Us115BBF"] = 0, ["Us115BBG"] = 0, ["115BBH"] = 0 },
                ["PLUs44sChapXIIG"] = 0,
                ["ProfitLossInclRefrdSec"] = new JsonObject
                {
                    ["ProfitLossUs44AD"] = 0, ["ProfitLossUs44ADA"] = 0, ["ProfitLossUs44AE"] = 0, ["ProfitLossUs44B"] = 0,
                    ["ProfitLossUs44BB"] = 0, ["ProfitLossUs44BBA"] = 0, ["ProfitLossUs44BBC"] = 0, ["ProfitLossUs44BBD"] = 0, ["ProfitLossUs44DA"] = 0,
                },
                ["TotalProfitFrmActCvrd"] = 0,
                ["ProfitFrmActCvrd"] = new JsonObject { ["ProfitFrmActCvrdUndrRule7"] = 0, ["ProfitFrmActCvrdUndrRule7A"] = 0, ["ProfitFrmActCvrdUndrRule7B1"] = 0, ["ProfitFrmActCvrdUndrRule7B1A"] = 0, ["ProfitFrmActCvrdUndrRule8"] = 0 },
                ["IncCredPL"] = new JsonObject { ["FirmShareInc"] = 0, ["AOPBOISharInc"] = 0, ["OthExempInc"] = 0, ["TotExempIncPL"] = 0 },
                ["BalancePLOthThanSpecBus"] = pbt,
                ["ExpDebToPLOthHeadDtls"] = new JsonObject { ["Salary"] = 0, ["HouseProperty"] = 0, ["CapitalGains"] = 0, ["OtherSources"] = 0, ["Us115BBF"] = 0, ["Us115BBG"] = 0, ["115BBH"] = 0 },
                ["ExpDebToPLExemptInc"] = 0,
                ["ExpDebToPLExemptIncDisAllwUs14A"] = 0,
                ["TotExpDebPL"] = 0,
                ["AdjustedPLOthThanSpecBus"] = pbt,
                ["DepreciationDebPLCosAct"] = depBooks,
                ["DepreciationAllowITAct32"] = new JsonObject { ["DepreciationAllowUs32_1_ii"] = depIt, ["DepreciationAllowUs32_1_i"] = 0, ["TotDeprAllowITAct"] = depIt },
                ["AdjustPLAfterDeprOthSpecInc"] = pbt + depBooks,
                ["AmtDebPLDisallowUs36"] = d36,
                ["AmtDebPLDisallowUs37"] = d37,
                ["AmtDebPLDisallowUs40"] = d40,
                ["AmtDebPLDisallowUs40A"] = d40a,
                ["AmtDebPLDisallowUs43B"] = d43b,
                ["InterestDisAllowUs23SMEAct"] = d43bh,
                ["DeemIncUs41"] = 0,
                ["DeemIncUs3380HHD80IA"] = 0,
                ["DeemIncUs43CA"] = 0,
                ["OthItemDisallowUs28To44DA"] = 0,
                ["AnyOthIncNotInclInExpDisallowPL"] = 0,
                ["AnyOthIncNotInclInSalary"] = 0,
                ["AnyOthIncNotInclInBonus"] = 0,
                ["AnyOthIncNotInclInCommission"] = 0,
                ["AnyOthIncNotInclInInterest"] = 0,
                ["AnyOthIncNotInclInOthers"] = 0,
                ["IncProfDecLossAccICDSAdj"] = 0,
                ["TotAfterAddToPLDeprOthSpecInc"] = totAdd,
                ["DeductUs32_1_iii"] = 0,
                ["DebPLUs35ExcessAmt"] = 0,
                ["AmtDisallUs40NowAllow"] = 0,
                ["AmtDisallUs43BNowAllow"] = 0,
                ["AnyOthAmtAllDeduct"] = 0,
                ["DecProfIncLossAccICDSAdj"] = 0,
                ["TotDeductionAmts"] = depIt,
                ["PLAftAdjDedBusOthThanSpec"] = pgbpNet,
                ["DeemedProfitBusUs"] = new JsonObject
                {
                    ["Section44AD"] = 0, ["Section44ADA"] = 0, ["Section44AE"] = 0, ["Section44B"] = 0,
                    ["Section44BB"] = 0, ["Section44BBA"] = 0, ["Section44BBC"] = 0, ["Section44BBD"] = 0, ["Section44DA"] = 0, ["TotDeemedProfitBusUs"] = 0,
                },
                ["NetPLAftAdjBusOthThanSpec"] = pgbpNet,
                ["NetPLBusOthThanSpec7A7B7C"] = pgbpNet,
                ["ChrgblIncUndrRule7"] = 0, ["DeemedChrgblIncUndrRule7A"] = 0, ["DeemedChrgblIncUndrRule7B1"] = 0, ["DeemedChrgblIncUndrRule7B1A"] = 0, ["DeemedChrgblIncUndrRule8"] = 0,
                ["IncomeOtherThanRule"] = pgbpNet,
                ["BalIncDeemedFrmAgri"] = 0,
            },
            ["SpecBusinessInc"] = new JsonObject { ["NetPLFrmSpecBus"] = 0, ["AdditionUs28to44DA"] = 0, ["DeductUs28to44DA"] = 0, ["AdjustedPLFrmSpecuBus"] = 0 },
            ["SpecifiedBusinessInc"] = new JsonObject { ["NetPLFrmSpecifiedBus"] = 0, ["AddSec28to44DA"] = 0, ["DedSec28to44DAOTDedSec35AD"] = 0, ["ProfitLossSpecifiedBusiness"] = 0, ["PLFrmSpecifiedBus"] = 0 },
            ["IncChrgUnHdProftGain"] = pgbpNet,
            ["BusSetoffCurrYr"] = new JsonObject { ["LossSetOffOnBusLoss"] = ToRupees(input.BroughtForwardBusinessLoss), ["TotLossSetOffOnBus"] = ToRupees(input.BroughtForwardBusinessLoss), ["LossRemainSetOffOnBus"] = pgbpNet },
        };

        var (dpm, doa, dep) = BuildDepreciationSchedules(bizEquivalentReq, bizEquivalentRes, isItr3: true);
        var schedS = BuildScheduleS(input, active, client, req);
        var schedHp = BuildScheduleHp(input, active, client, req);
        var (schedCg, sched112A, schedVda) = BuildCapitalGainsAndVda(input, active, isItr3: true);
        var schedOs = BuildScheduleOs(input, active, isItr3: true);
        var (cyla, bfla) = BuildCylaAndBfla(active, isItr3: true);
        var schedVia = BuildPersonalScheduleVia(input, active, isItr3: true);
        var schedSi = BuildPersonalScheduleSi(active);
        var schedFa = BuildScheduleFa(input);
        var schedAl = BuildScheduleAl(input, active, client, req, isItr3: true);

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

        var partBTi = new JsonObject
        {
            ["Salaries"] = salNet,
            ["IncomeFromHP"] = hpNet,
            ["ProfBusGain"] = new JsonObject
            {
                ["ProfGainNoSpecBus"] = Math.Max(0L, pgbpNet),
                ["ProfGainSpecBus"] = 0,
                ["ProfGainSpecifiedBus"] = 0,
                ["ProfIncome115BBF"] = 0,
                ["TotProfBusGain"] = Math.Max(0L, pgbpNet),
            },
            ["CapGain"] = new JsonObject
            {
                ["ShortTerm"] = new JsonObject { ["ShortTerm20Per"] = st20, ["ShortTerm30Per"] = 0, ["ShortTermAppRate"] = stNorm, ["ShortTermSplRateDTAA"] = 0, ["TotalShortTerm"] = st20 + stNorm },
                ["LongTerm"] = new JsonObject { ["LongTerm12_5Per"] = ltTot, ["LongTermSplRateDTAA"] = 0, ["TotalLongTerm"] = ltTot },
                ["ShortTermLongTermTotal"] = st20 + stNorm + ltTot,
                ["CapGains30Per115BBH"] = vda,
                ["TotalCapGains"] = cgTot,
            },
            ["IncFromOS"] = new JsonObject { ["OtherSrcThanOwnRaceHorse"] = osNet, ["IncChargblSplRate"] = 0, ["FromOwnRaceHorse"] = 0, ["TotIncFromOS"] = osNet },
            ["TotalTI"] = gti,
            ["CurrentYearLoss"] = 0,
            ["BalanceAfterSetoffLosses"] = gti,
            ["BroughtFwdLossesSetoff"] = ToRupees(input.BroughtForwardBusinessLoss),
            ["GrossTotalIncome"] = gti,
            ["IncChargeTaxSplRate111A112"] = st20 + ltTot,
            ["DeductionsUndSchVIADtl"] = new JsonObject
            {
                ["PartBchapterVIA"] = Math.Min(chapVia, ToRupees(input.Section80C + input.Section80Ccd1B + input.Section80D + input.Section80E + input.Section80G)),
                ["PartCchapterVIA"] = 0,
                ["TotDeductUndSchVIA"] = chapVia,
            },
            ["DeductionsUnder10Aor10AA"] = ToRupees(input.Deduction10AA),
            ["TotalIncome"] = taxable,
            ["AggregateIncome"] = taxable,
            ["DeemedIncomeUs115JC"] = active.IsAmtApplied ? ToRupees(active.TaxableIncome + input.Deduction10AA + input.Deduction35AD) : 0L,
        };

        long slabTax = ToRupees(active.SlabTaxOnNormalIncome);
        long splTax = ToRupees(active.SpecialRateTax);
        long rebate87A = ToRupees(active.Rebate87A);
        long taxAfterRebate = Math.Max(0L, slabTax + splTax - rebate87A);
        long sc = ToRupees(active.Surcharge);
        long cess = ToRupees(active.Cess);
        long grossTax = taxAfterRebate + sc + cess;
        long amtTax = ToRupees(active.AmtTotalTax115JC);
        long grossPayable = Math.Max(grossTax, amtTax);

        var partBTti = new JsonObject
        {
            ["ComputationOfTaxLiability"] = new JsonObject
            {
                ["TaxPayableOnDeemedTI"] = new JsonObject
                {
                    ["TaxDeemedTISec115JC"] = amtTax > 0 ? ToRupees(amtTax / 1.04m) : 0L,
                    ["SurchargeOnAboveCrore"] = 0,
                    ["EducationCess"] = amtTax > 0 ? ToRupees(amtTax - Math.Round(amtTax / 1.04m, 0)) : 0L,
                    ["TotalTax"] = amtTax,
                },
                ["TaxPayableOnTI"] = new JsonObject
                {
                    ["TaxAtNormalRatesOnAggrInc"] = slabTax,
                    ["TaxAtSpecialRates"] = splTax,
                    ["RebateOnAgriInc"] = 0,
                    ["TaxPayableOnTotInc"] = slabTax + splTax,
                    ["Rebate87A"] = rebate87A,
                    ["TaxPayableOnRebate"] = taxAfterRebate,
                    ["Surcharge25ofSI"] = 0,
                    ["SurchargeOnAboveCrore"] = sc,
                    ["Surcharge25ofSIBeforeMarginal"] = 0,
                    ["SurchargeOnAboveCroreBeforeMarginal"] = sc,
                    ["TotalSurcharge"] = sc,
                    ["EducationCess"] = cess,
                    ["GrossTaxLiability"] = grossTax,
                },
                ["GrossTaxPayable"] = grossPayable,
                ["CreditUS115JD"] = 0,
                ["TaxPayAfterCreditUs115JD"] = grossPayable,
                ["NetTaxLiability"] = grossPayable,
                ["IntrstPay"] = new JsonObject
                {
                    ["IntrstPayUs234A"] = ToRupees(input.Interest234A),
                    ["IntrstPayUs234B"] = ToRupees(input.Interest234B),
                    ["IntrstPayUs234C"] = ToRupees(input.Interest234C),
                    ["LateFilingFee234F"] = ToRupees(input.LateFee234F),
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

        var itr3 = new JsonObject
        {
            ["CreationInfo"] = BuildCreationInfo(req?.IntermediaryCity ?? req?.City ?? "Kochi"),
            ["Form_ITR3"] = new JsonObject
            {
                ["FormName"] = "ITR-3",
                ["Description"] = "For Individuals and HUFs having income from Business or Profession",
                ["AssessmentYear"] = ayStart4,
                ["SchemaVer"] = "Ver1.0",
                ["FormVer"] = "Ver1.0",
            },
            ["PartA_GEN1"] = new JsonObject { ["PersonalInfo"] = personalInfo, ["FilingStatus"] = filingStatus },
            ["PartA_GEN2"] = partAGen2,
            ["PARTA_BS"] = partABs,
            ["PARTA_PL"] = itr3Pl,
        };
        if (schedS is not null) itr3["ScheduleS"] = schedS;
        if (schedHp is not null) itr3["ScheduleHP"] = schedHp;
        itr3["ITR3ScheduleBP"] = itr3Bp;
        itr3["ScheduleDPM"] = dpm;
        itr3["ScheduleDOA"] = doa;
        itr3["ScheduleDEP"] = dep;
        if (schedCg is not null) itr3["ScheduleCGFor23"] = schedCg;
        if (sched112A is not null) itr3["Schedule112A"] = sched112A;
        if (schedVda is not null) itr3["ScheduleVDA"] = schedVda;
        if (schedOs is not null) itr3["ScheduleOS"] = schedOs;
        itr3["ScheduleCYLA"] = cyla;
        itr3["ScheduleBFLA"] = bfla;
        itr3["ScheduleVIA"] = schedVia;
        if (schedSi is not null) itr3["ScheduleSI"] = schedSi;
        if (schedFa is not null) itr3["ScheduleFA"] = schedFa;
        if (schedAl is not null) itr3["ScheduleAL"] = schedAl;
        itr3["PartB-TI"] = partBTi;
        itr3["PartB_TTI"] = partBTti;

        if (input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid > 0m || (input.AdvanceTaxChallans is { Count: > 0 }))
            itr3["ScheduleIT"] = BuildScheduleIt(bizEquivalentReq);
        var tds1 = BuildScheduleTds1(input, active);
        if (tds1 is not null) itr3["ScheduleTDS1"] = tds1;
        var tds2 = BuildScheduleTds2(bizEquivalentReq, issues, "ITR.ITR3.ScheduleTDS2");
        if (tds2 is not null) itr3["ScheduleTDS2"] = tds2;

        string signerPan = !string.IsNullOrWhiteSpace(req?.VerificationPan) && req.VerificationPan.Trim().Length == 10
            ? req.VerificationPan.Trim().ToUpperInvariant()
            : (pan[3] == 'P' ? pan : "ABCPA1234A");
        itr3["Verification"] = new JsonObject
        {
            ["Declaration"] = new JsonObject
            {
                ["AssesseeVerName"] = Trunc(req?.VerificationName, 125, client.LegalName ?? client.Name),
                ["FatherName"] = Trunc(req?.FatherName, 125, "NA"),
                ["AssesseeVerPAN"] = signerPan,
            },
            ["Capacity"] = ext.StatusType == "H" ? "K" : "S",
            ["Date"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            ["Place"] = Trunc(req?.Place ?? req?.City, 50, "Kochi"),
        };

        var root = new JsonObject { ["ITR"] = new JsonObject { ["ITR3"] = itr3 } };
        string fileName = $"{pan}_ITR3_AY{ayDashed}.json";
        return ValidateAndFinalize(root, EmbeddedSchemaName, "ITR-3", "Ver1.0 (V1.1 Schema)", ayDashed, comp.FinancialYear, fileName, issues);
    }
}
