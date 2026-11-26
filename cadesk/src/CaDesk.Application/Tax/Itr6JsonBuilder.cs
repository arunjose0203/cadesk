using System.Text.Json.Nodes;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Tax;
using static CaDesk.Application.Tax.BusinessItrJsonHelper;
using static CaDesk.Application.Tax.CbdtJsonExportHelper;

namespace CaDesk.Application.Tax;

/// <summary>
/// Builds official CBDT AY 2026-27 ITR-6 JSON (Domestic Companies: Normal vs Sec 115BAA/115BAB + MAT 115JB)
/// validated against embedded <c>ITR-6_2026_Main_V1.0.json</c>.
/// </summary>
public static class Itr6JsonBuilder
{
    public const string EmbeddedSchemaName = "ITR-6_2026_Main_V1.0.json";

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
        string pan = NormalizePan(ExtractTaxId(client, "PAN"), issues, "ITR.ITR6.PartA_GEN1.OrgFirmInfo.PAN");
        string name = Trunc(client.LegalName ?? client.Name, 125, "PRIVATE LIMITED COMPANY");
        string subTypeLower = (input.SubType ?? "").Trim().ToLowerInvariant();
        string companyTypeCode = subTypeLower.Contains("public") ? "6" : "7";

        string incorpDate = string.IsNullOrWhiteSpace(req?.DateOfIncorporation)
            ? (string.IsNullOrWhiteSpace(input.IncorporationDate) ? "2020-04-01" : input.IncorporationDate)
            : req.DateOfIncorporation!;

        var orgInfo = new JsonObject
        {
            ["AssesseeName"] = new JsonObject { ["SurNameOrOrgName"] = name },
            ["PAN"] = pan,
            ["Address"] = BuildBusinessAddress(client, req),
            ["DateOFFormOrIncorp"] = incorpDate,
            ["StatusOrCompanyType"] = companyTypeCode,
            ["DomesticCompFlg"] = "Y",
        };
        string cin = ExtractTaxId(client, "CIN");
        if (cin.Length == 21) orgInfo["CINissuedByMCA"] = cin.ToUpperInvariant();

        bool useIndAs = req?.UseIndAs ?? input.IsIndAsApplicable;
        int returnFileSec = req?.ReturnFileSec is >= 11 and <= 20 ? req.ReturnFileSec.Value : 11;
        string dueDate = req?.ItrFilingDueDate is "2026-10-31" or "2026-11-30" ? req.ItrFilingDueDate : "2026-10-31";
        string sec115Ba = biz.SelectedRegimeCode switch
        {
            BusinessTaxEngine.RegimeCorp115Baa => "115BAA",
            BusinessTaxEngine.RegimeCorp115Bab => "115BAB",
            _ => "NA"
        };

        var filingStatus = new JsonObject
        {
            ["ReturnFileSec"] = new JsonObject { ["IncomeTaxSec"] = returnFileSec },
            ["ResidentialStatus"] = "RES",
            ["Section115BA"] = sec115Ba,
            ["FinancialStmtFlag"] = useIndAs ? "Y" : "N",
            ["UnderLiquidation"] = "N",
            ["FiiFpiFlag"] = "N",
            ["Sec581AFlag"] = "N",
            ["AsseseeRepFlg"] = "N",
            ["StartUpDPIITFlag"] = "N",
            ["ItrFilingDueDate"] = dueDate,
            ["ifMSME"] = "Y",
        };

        var partAGen2 = new JsonObject
        {
            ["LiableSec44AAflg"] = "Y",
            ["IncDclrdUs"] = "N",
            ["LiableSec44ABflg"] = input.IsTaxAuditApplicable ? "Y" : "N",
            ["LiableSec92Eflg"] = "N",
            ["HoldingStatus"] = new JsonObject { ["NatOfCompFlg"] = "4" },
            ["NatureOfComp"] = new JsonObject
            {
                ["PubSectCompUs2_36AFlg"] = "N",
                ["RBICompFlg"] = "N",
                ["CompLes40PercSharGovRBIFlg"] = "N",
                ["BankCompUs5Flg"] = "N",
                ["SchedBankOfRBIActFlg"] = "N",
                ["CompWithIRDARegisterFlg"] = "N",
                ["NonBankFIICompFlg"] = "N",
                ["CompanyUnlistedFlag"] = companyTypeCode == "7" ? "Y" : "N",
            },
            ["NatOfBus"] = BuildNatOfBus(input.NatureOfBusinessCode, name),
        };

        if (input.KeyPersons is { Count: > 0 })
        {
            var kpArr = new JsonArray();
            foreach (var kp in input.KeyPersons)
            {
                var kpObj = new JsonObject
                {
                    ["PersonName"] = Trunc(kp.Name, 125, "Director"),
                    ["Designation"] = string.IsNullOrWhiteSpace(kp.Designation) ? "DIR" : kp.Designation.Trim().ToUpperInvariant(),
                    ["AddressDetailWithZipCode"] = BuildAddressDetailWithZipCode(kp.Address, kp.City, kp.StateCode, kp.PinCode, client),
                };
                if (!string.IsNullOrWhiteSpace(kp.Pan)) kpObj["KeyPerPAN"] = kp.Pan.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(kp.Din) && kp.Din.Trim().Length == 8 && kp.Din.All(char.IsAsciiDigit))
                    kpObj["DirectorIdNo"] = kp.Din.Trim();
                kpArr.Add(kpObj);
            }
            partAGen2["KeyPersons"] = kpArr;
        }

        // Balance Sheet totals shared for Schedule III Non-IndAS & IndAS (both are required keys on ITR6)
        long capPaid = ToRupees(input.PartnerOrShareCapital);
        long capAuth = Math.Max(capPaid, ToRupees(input.AuthorisedShareCapital));
        long res = ToSignedRupees(input.ReservesAndSurplus);
        long totEq = capPaid + res;
        long ltBorrow = ToRupees(input.LongTermBorrowings);
        long dtl = ToRupees(input.DeferredTaxLiability);
        long totNonCurrLiab = ltBorrow + dtl;
        long stBorrow = ToRupees(input.ShortTermBorrowings);
        long cred = ToRupees(input.TradePayables);
        long othCl = ToRupees(input.OtherCurrentLiabilities);
        long taxProv = ToRupees(input.TaxProvisionCurrent);
        long totCurrLiab = stBorrow + cred + othCl + taxProv;
        long totEqAndLiab = Math.Max(0L, totEq + totNonCurrLiab + totCurrLiab);

        long faGross = ToRupees(input.FixedAssetsGrossBlock > 0m ? input.FixedAssetsGrossBlock : input.FixedAssetsNetBlock);
        long faDep = ToRupees(input.FixedAssetsAccumulatedDep);
        long faNet = Math.Max(0L, faGross - faDep);
        long cwipp = ToRupees(input.CapitalWip);
        long totFixed = faNet + cwipp;
        long ncInv = ToRupees(input.NonCurrentInvestments);
        long cInv = ToRupees(input.CurrentInvestments);
        long invnt = ToRupees(input.Inventories);
        long debtors = ToRupees(input.TradeReceivables);
        long cash = ToRupees(input.CashAndBankBalances);
        long loansAdv = ToRupees(input.LoansAndAdvances);
        long othCa = ToRupees(input.OtherCurrentAssets);
        long totNonCurrAssets = totFixed + ncInv;
        long totCurrAssets = cInv + invnt + debtors + cash + loansAdv + othCa;
        long totAssets = totNonCurrAssets + totCurrAssets;

        var partABsNonIndAs = BuildNonIndAsBalanceSheet(capAuth, capPaid, res, totEq, ltBorrow, dtl, totNonCurrLiab, stBorrow, cred, othCl, taxProv, totCurrLiab, totEqAndLiab,
            faGross, faDep, faNet, cwipp, totFixed, ncInv, cInv, invnt, debtors, cash, loansAdv, othCa, totNonCurrAssets, totCurrAssets, totAssets);

        var partABsIndAs = BuildIndAsBalanceSheet(capAuth, capPaid, res, totEq, ltBorrow, dtl, totNonCurrLiab, stBorrow, cred, othCl, taxProv, totCurrLiab, totEqAndLiab,
            faGross, faDep, faNet, cwipp, ncInv, cInv, invnt, debtors, cash, loansAdv, othCa, totNonCurrAssets, totCurrAssets, totAssets);

        var partAPlNonIndAs = BuildPartAPl(input, biz, isItr6: true, isIndAs: false);
        var partAPlIndAs = BuildPartAPl(input, biz, isItr6: true, isIndAs: true);
        var corpBp = BuildCorpScheduleBp(input, biz, isItr6: true);
        var (dpm, doa, dep) = BuildDepreciationSchedules(input, biz);
        var scheduleVia = BuildScheduleVia(input, biz);
        var scheduleSi = BuildScheduleSi(input, isItr6: true);
        var partBTi = BuildPartBTi(input, biz, isItr6: true);

        bool isMatApplicable = sec115Ba == "NA";
        long matAddTax = ToRupees(input.TaxProvisionCurrent + input.TaxProvisionDeferred);
        long matAddOther = ToRupees(input.MatAdditionsOther);
        long matDedLoss = ToRupees(Math.Min(input.BroughtForwardBusinessLoss, input.UnabsorbedDepreciation));
        long matDedOther = ToRupees(input.MatDeductionsOther);
        long matBookProf = isMatApplicable ? ToRupees(biz.MatBookProfit115JB) : 0;

        var scheduleMat = new JsonObject
        {
            ["PLAcntPrepSchedVICompAct"] = "1",
            ["PLAcntPrepAsperAGM"] = "1",
            ["ProfAfterTaxPLAcnt"] = ToSignedRupees(biz.BookProfitAfterTax),
            ["Additions"] = new JsonObject
            {
                ["ITPaidInclDefTax"] = matAddTax,
                ["ResvrNo33AC"] = 0,
                ["ProvUncertainLiab"] = 0,
                ["ProvLossOfSubsComp"] = 0,
                ["DividendPaidOrProposed"] = 0,
                ["ExpendExempIncUs10s"] = 0,
                ["ExpAopBoi"] = 0,
                ["ExpClauseFb"] = 0,
                ["NotLossClauseFc"] = 0,
                ["NotLossUs115bbf"] = 0,
                ["DepreciatAttribToRevalAsset"] = 0,
                ["GainClauseK"] = 0,
                ["Others"] = matAddOther,
                ["TotAdditions"] = matAddTax + matAddOther,
            },
            ["Deducts"] = new JsonObject
            {
                ["AmtWithdrawFromResvrIfCredPL"] = 0,
                ["IncExempIncUs10s"] = 0,
                ["AmtWithdrawFromResvrIfCredPLNoAttrib"] = 0,
                ["ShareIncAopBoi"] = 0,
                ["IncClauseiid"] = 0,
                ["NotGainClauseiie"] = 0,
                ["LossTrnsClauseiif"] = 0,
                ["UnAbsorbedDepreciat"] = matDedLoss,
                ["ProSickIndustryOrExcedAccumLos"] = 0,
                ["Others"] = matDedOther,
                ["TotDeducts"] = matDedLoss + matDedOther,
            },
            ["BookProfUs115JB"] = matBookProf,
            ["DeemedTotalIncUs115JB"] = matBookProf,
            ["DeemedTotalIncUs115JBIFSC"] = 0,
            ["DeemedTotalIncUs115JBOther"] = matBookProf,
            ["TaxPayableUs115JB"] = ToRupees(active.MatOrAmtBaseTax),
        };

        long intr234A = ToRupees(input.Interest234A);
        long intr234B = ToRupees(input.Interest234B);
        long intr234C = ToRupees(input.Interest234C);
        long fee234F = ToRupees(input.LateFee234F);
        long grossTaxPayable = Math.Max(ToRupees(active.GrossTaxBeforeMatAmt), ToRupees(active.MatOrAmtTotalTax));
        long matCreditUsed = ToRupees(active.MatOrAmtCreditUtilized);
        long netAfterMatCredit = Math.Max(0L, grossTaxPayable - matCreditUsed);

        var compOfTaxLiab = new JsonObject
        {
            ["TaxPayableOnDeemedTI"] = new JsonObject
            {
                ["TaxDeemedTISec115JB"] = ToRupees(active.MatOrAmtBaseTax),
                ["Surcharge"] = ToRupees(active.MatOrAmtSurcharge),
                ["EducationCess"] = ToRupees(active.MatOrAmtCess),
                ["TotalTax"] = ToRupees(active.MatOrAmtTotalTax),
            },
            ["TaxPayableOnTI"] = new JsonObject
            {
                ["TaxAtNormalRates"] = ToRupees(active.TaxAtNormalRates),
                ["TaxAtSpecialRates"] = ToRupees(active.TaxAtSpecialRates),
                ["TaxPayableOnTotInc"] = ToRupees(active.BaseTax),
                ["Surcharge25ofSI"] = 0,
                ["SurchargeOnTaxPayable"] = ToRupees(active.Surcharge),
                ["TotalSurcharge"] = ToRupees(active.Surcharge),
                ["EducationCess"] = ToRupees(active.HealthAndEducationCess),
                ["GrossTaxLiability"] = ToRupees(active.GrossTaxBeforeMatAmt),
            },
            ["GrossTaxPayable"] = grossTaxPayable,
            ["CredUs115JAATaxPaid"] = matCreditUsed,
            ["TaxPayableAfterCredUs115JAA"] = netAfterMatCredit,
            ["TaxRelief"] = new JsonObject { ["Section90"] = 0, ["Section91"] = 0, ["TotTaxRelief"] = 0 },
            ["NetTaxLiability"] = netAfterMatCredit,
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
            ["AssetOutsideIndiaFlg"] = "NO",
        };

        var itr6 = new JsonObject
        {
            ["CreationInfo"] = BuildCreationInfo(req?.IntermediaryCity ?? req?.City ?? "Kochi"),
            ["Form_ITR6"] = new JsonObject
            {
                ["FormName"] = "ITR-6",
                ["Description"] = "For Companies other than companies claiming exemption under section 11",
                ["AssessmentYear"] = ayStart4,
                ["SchemaVer"] = "Ver1.0",
                ["FormVer"] = "Ver1.0",
            },
            ["PartA_GEN1"] = new JsonObject
            {
                ["OrgFirmInfo"] = orgInfo,
                ["FilingStatus"] = filingStatus,
            },
            ["PartA_GEN2For6"] = partAGen2,
            ["PARTA_BSFor6FrmAY13"] = partABsNonIndAs,
            ["PARTA_BSIndAS"] = partABsIndAs,
            ["PARTA_PL"] = partAPlNonIndAs,
            ["PARTA_PLIndAS"] = partAPlIndAs,
            ["CorpScheduleBP"] = corpBp,
            ["ScheduleDPM"] = dpm,
            ["ScheduleDOA"] = doa,
            ["ScheduleDEP"] = dep,
            ["ScheduleMAT"] = scheduleMat,
            ["ScheduleVIA"] = scheduleVia,
        };

        if (input.Shareholders is { Count: > 0 } && companyTypeCode == "7")
        {
            var shArr = new JsonArray();
            foreach (var sh in input.Shareholders)
            {
                shArr.Add(new JsonObject
                {
                    ["ShareholderName"] = Trunc(sh.Name, 125, "Shareholder"),
                    ["ResidentialStatus"] = string.IsNullOrWhiteSpace(sh.ResidentialStatus) ? "RES" : sh.ResidentialStatus,
                    ["ShareType"] = sh.ShareType == "Preference" ? "PS" : "ES",
                    ["PAN"] = string.IsNullOrWhiteSpace(sh.Pan) ? "AAAAA0000A" : sh.Pan.Trim().ToUpperInvariant(),
                    ["AllotmentDate"] = string.IsNullOrWhiteSpace(sh.AllotmentDate) ? incorpDate : sh.AllotmentDate,
                    ["NumberOfSharesHeld"] = Math.Max(1, sh.NumberOfShares),
                    ["FaceValuePerShare"] = ToRupees(sh.FaceValuePerShare),
                    ["IssuePricePerShare"] = ToRupees(sh.IssuePricePerShare),
                    ["AmountReceived"] = ToRupees(sh.NumberOfShares * sh.IssuePricePerShare),
                });
            }
            itr6["ScheduleSH"] = new JsonObject
            {
                ["ShrhldngUnlistedCompany"] = new JsonObject
                {
                    ["SHUnlistedCompanyFlag"] = "Y",
                    ["DtlsSHEndPreviousYearUC"] = shArr,
                }
            };
        }

        if (scheduleSi is not null) itr6["ScheduleSI"] = scheduleSi;

        itr6["PartB-TI"] = partBTi;
        itr6["PartB_TTI"] = partBTti;
        itr6["ScheduleIT"] = BuildScheduleIt(input);
        var tds2 = BuildScheduleTds2(input, issues, "ITR.ITR6.ScheduleTDS2");
        if (tds2 is not null) itr6["ScheduleTDS2"] = tds2;
        itr6["Verification"] = BuildBusinessVerification(client, req, isItr6: true, issues, "ITR.ITR6");

        var root = new JsonObject
        {
            ["ITR"] = new JsonObject { ["ITR6"] = itr6 }
        };

        string fileName = $"{pan}_ITR6_AY{ayDashed}.json";
        return ValidateAndFinalize(root, EmbeddedSchemaName, "ITR-6", "Ver1.0", ayDashed, comp.FinancialYear, fileName, issues);
    }

    private static JsonObject BuildNonIndAsBalanceSheet(
        long capAuth, long capPaid, long res, long totEq,
        long ltBorrow, long dtl, long totNonCurrLiab,
        long stBorrow, long cred, long othCl, long taxProv, long totCurrLiab, long totEqAndLiab,
        long faGross, long faDep, long faNet, long cwipp, long totFixed,
        long ncInv, long cInv, long invnt, long debtors, long cash, long loansAdv, long othCa,
        long totNonCurrAssets, long totCurrAssets, long totAssets) => new()
    {
        ["EquityAndLiablities"] = new JsonObject
        {
            ["ShareHolderFund"] = new JsonObject
            {
                ["ShareCapital"] = new JsonObject { ["Authorised"] = capAuth, ["IssuedSubsPaidUp"] = capPaid, ["SubscribedNotFullyPaid"] = 0, ["TotShareCapital"] = capPaid },
                ["ResrNSurp"] = new JsonObject { ["CapResr"] = 0, ["CapRedempResr"] = 0, ["SecurPremResr"] = 0, ["DebunRedResr"] = 0, ["RevResr"] = 0, ["ShareOptOSAmount"] = 0, ["OtherResrvTotal"] = 0, ["PLAccount"] = res, ["TotResrNSurp"] = res },
                ["MoneyRecvdAgainstShares"] = 0,
                ["TotShareHolderFund"] = totEq,
            },
            ["ShareAppMoneyAllot"] = new JsonObject { ["PendingLtOneYr"] = 0, ["PendingMtOneYr"] = 0, ["Total"] = 0 },
            ["NonCurrLiabilities"] = new JsonObject
            {
                ["LongTermBorrowings"] = new JsonObject
                {
                    ["BondsDebentures"] = new JsonObject { ["ForeignCurrency"] = 0, ["Rupee"] = 0, ["Total"] = 0 },
                    ["TermLoans"] = new JsonObject { ["ForeignCurrency"] = 0, ["RupeeLoans"] = new JsonObject { ["FromBanks"] = ltBorrow, ["FromOthers"] = 0, ["Total"] = ltBorrow }, ["TotalTermLoans"] = ltBorrow },
                    ["DeferredPymtLiabilities"] = 0, ["DepositsFrmRelatedParties"] = 0, ["OtherDeposits"] = 0,
                    ["LoansAndAdv"] = 0, ["OthersLoanAdv"] = 0, ["LongTermMaturities"] = 0, ["TotalLTBorrowings"] = ltBorrow,
                },
                ["NetDefferedTaxLiability"] = dtl,
                ["OthLongTermLiablities"] = new JsonObject { ["TradePayables"] = 0, ["Others"] = 0, ["TotalOthLtLiabilities"] = 0 },
                ["LongTermProvisions"] = new JsonObject { ["ProvEmpBenefits"] = 0, ["Others"] = 0, ["Total"] = 0 },
                ["TotalNonCurrLiabilites"] = totNonCurrLiab,
            },
            ["CurrentLiabilities"] = new JsonObject
            {
                ["ShortTrmBorrowings"] = new JsonObject
                {
                    ["LoansRepaybleOnDemand"] = new JsonObject { ["FromBanks"] = stBorrow, ["FrmNonBanking"] = 0, ["OthFinanceInst"] = 0, ["Others"] = 0, ["TotLoansRepaybleOnDemand"] = stBorrow },
                    ["DepositsFrmRelatedParties"] = 0, ["LoansAndAdv"] = 0, ["OthLoansAndAdv"] = 0, ["OthDeposits"] = 0, ["TotShortTrmBorrowings"] = stBorrow,
                },
                ["TradePayables"] = new JsonObject { ["OSMoreThanOneYr"] = 0, ["Others"] = cred, ["TotalTradePayables"] = cred },
                ["OthCurrLiabilities"] = new JsonObject
                {
                    ["CurrMatOnLTDebt"] = 0, ["CurrMatFinanceOblg"] = 0, ["AccrInterestNotDue"] = 0, ["AccrInterest"] = 0,
                    ["IncRecvdAdvance"] = 0, ["UnpaidDividend"] = 0, ["AppMonyRecvdAllotSecurities"] = 0,
                    ["UnpaidMatDeposits"] = 0, ["UnpaidMatureDebenture"] = 0, ["OthPayables"] = othCl, ["TotOthCurrLiabilities"] = othCl,
                },
                ["ShortTermProv"] = new JsonObject { ["EmpBenefitProv"] = 0, ["ITProvision"] = taxProv, ["ProposedDividend"] = 0, ["TaxOnDividend"] = 0, ["OthProvision"] = 0, ["TotShortTermProvisions"] = taxProv },
                ["TotCurrLiabilitiesProvision"] = totCurrLiab,
            },
            ["TotEquityAndLiabilities"] = totEqAndLiab,
        },
        ["Assets"] = new JsonObject
        {
            ["NonCurrAssets"] = new JsonObject
            {
                ["FixedAsset"] = new JsonObject
                {
                    ["Tangible"] = new JsonObject { ["GrossBlock"] = faGross, ["Depreciation"] = faDep, ["ImpairmentLosses"] = 0, ["NetBlock"] = faNet },
                    ["InTangible"] = new JsonObject { ["GrossBlock"] = 0, ["Amortization"] = 0, ["ImpairmentLosses"] = 0, ["NetBlock"] = 0 },
                    ["CapWrkProg"] = cwipp, ["IntangibleAssetUnDev"] = 0, ["TotFixedAsset"] = totFixed,
                },
                ["NonCurrInvstmnts"] = new JsonObject
                {
                    ["InvInProperty"] = 0,
                    ["EquityInstruments"] = new JsonObject { ["ListedEquities"] = 0, ["UnListedEquities"] = 0, ["Total"] = 0 },
                    ["PreferenceShares"] = 0, ["GovtOrTrustSecurities"] = 0, ["DebenturesOrBonds"] = 0, ["MutualFunds"] = 0,
                    ["InvstmntInPrtnrShipFirm"] = 0, ["OtherInvstmnts"] = ncInv, ["TotNonCurrInvstmnts"] = ncInv,
                },
                ["NetDeferredTaxAssets"] = 0,
                ["LongTrmLoanAdv"] = new JsonObject
                {
                    ["CapitalAdv"] = 0, ["SecurityDeposits"] = 0, ["LoanAdvRelatedParties"] = 0, ["OthLoanAdv"] = 0, ["TotLTLoanAdv"] = 0,
                    ["LTLoanAdvDtls"] = new JsonObject { ["BusOrProf"] = 0, ["NotForBusOrProf"] = 0, ["ShareHolderUs2_22"] = 0 },
                },
                ["OthNonCurrAssets"] = new JsonObject
                {
                    ["LTTradeReceivables"] = new JsonObject { ["Secured"] = 0, ["Unsecured"] = 0, ["Doubtful"] = 0, ["TotOthNonCurrAssets"] = 0 },
                    ["Others"] = 0, ["Total"] = 0, ["NonCurrAssetUs2_22"] = 0,
                },
                ["TotNonCurrAssets"] = totNonCurrAssets,
            },
            ["CurrentAssets"] = new JsonObject
            {
                ["CurrInvstmnts"] = new JsonObject
                {
                    ["EquityInstruments"] = new JsonObject { ["ListedEquities"] = 0, ["UnListedEquities"] = 0, ["Total"] = 0 },
                    ["PreferenceShares"] = 0, ["GovtOrTrustSecurities"] = 0, ["DebenturesOrBonds"] = 0, ["MutualFunds"] = 0,
                    ["InvstmntInPrtnrShipFirm"] = 0, ["OtherInvstmnts"] = cInv, ["TotCurrInvstmnts"] = cInv,
                },
                ["Inventories"] = new JsonObject
                {
                    ["RawMatl"] = 0, ["WorkInProgress"] = 0, ["FinOrTradGood"] = invnt, ["StkInTrade"] = 0,
                    ["StoresConsumables"] = 0, ["LooseTools"] = 0, ["Others"] = 0, ["TotInventries"] = invnt,
                },
                ["TradeReceivables"] = new JsonObject { ["OSMoreThanSixMonths"] = 0, ["Others"] = debtors, ["TotalTradeReceivables"] = debtors },
                ["CashNCashEquivalents"] = new JsonObject { ["BalWithBanks"] = cash, ["ChequesDrafts"] = 0, ["CashInHand"] = 0, ["Others"] = 0, ["TotCashNCashEquivalents"] = cash },
                ["TotShortTermLoanAdv"] = new JsonObject
                {
                    ["LoanAdv"] = loansAdv, ["Others"] = 0, ["TotShrtTermLoans"] = loansAdv,
                    ["STLoanAdvDtls"] = new JsonObject { ["BusOrProf"] = loansAdv, ["NotForBusOrProf"] = 0, ["ShareHolderUs2_22"] = 0 },
                },
                ["OtherCurrAssets"] = othCa,
                ["TotCurrAssets"] = totCurrAssets,
            }
        },
        ["TotalAssets"] = totAssets,
    };

    private static JsonObject BuildIndAsBalanceSheet(
        long capAuth, long capPaid, long res, long totEq,
        long ltBorrow, long dtl, long totNonCurrLiab,
        long stBorrow, long cred, long othCl, long taxProv, long totCurrLiab, long totEqAndLiab,
        long faGross, long faDep, long faNet, long cwipp,
        long ncInv, long cInv, long invnt, long debtors, long cash, long loansAdv, long othCa,
        long totNonCurrAssets, long totCurrAssets, long totAssets) => new()
    {
        ["EquityAndLiablities"] = new JsonObject
        {
            ["Equity"] = new JsonObject
            {
                ["EquityShareCapital"] = new JsonObject { ["Authorised"] = capAuth, ["IssuedSubsPaidUp"] = capPaid, ["SubscribedNotFullyPaid"] = 0, ["TotShareCapital"] = capPaid },
                ["OtherEquityReserv"] = new JsonObject { ["CapRedempResr"] = 0, ["DebunRedResr"] = 0, ["ShareOptOSAmount"] = 0, ["TotalOtherResrv"] = 0, ["RetainedEarngs"] = res, ["TotResrNRetEar"] = res, ["TotalEquity"] = totEq },
            },
            ["Liabilities"] = new JsonObject
            {
                ["NonCurrLiabilities"] = new JsonObject
                {
                    ["FinancialLiabilities"] = new JsonObject
                    {
                        ["BondsDebentures"] = new JsonObject { ["ForeignCurrency"] = 0, ["Rupee"] = 0, ["Total"] = 0 },
                        ["TermLoans"] = new JsonObject { ["ForeignCurrency"] = 0, ["RupeeLoans"] = new JsonObject { ["FromBanks"] = ltBorrow, ["FromOthers"] = 0, ["Total"] = ltBorrow }, ["TotalTermLoans"] = ltBorrow },
                        ["DeferredPymtLiabilities"] = 0, ["Deposits"] = 0, ["LoansReltdParties"] = 0, ["LongTermMaturities"] = 0,
                        ["LiabilityComp"] = 0, ["OtherLoans"] = 0, ["TotalLTBorrowings"] = ltBorrow, ["TradePayables"] = 0, ["OtherFinancialLiab"] = 0,
                    },
                    ["Provisions"] = new JsonObject { ["ProvEmpBenefits"] = 0, ["TotalProvisions"] = 0 },
                    ["DefrdTaxCurrLiabilites"] = dtl,
                    ["OtherNonCurLiabilites"] = new JsonObject { ["Advances"] = 0, ["TotalOthNonCurrLiab"] = 0 },
                    ["TotalNonCurrLiab"] = totNonCurrLiab,
                },
                ["CurrentLiabilities"] = new JsonObject
                {
                    ["FinancialLiabBorrowings"] = new JsonObject
                    {
                        ["LoansRepaybleOnDemand"] = new JsonObject { ["FromBanks"] = stBorrow, ["FrmOtherParties"] = 0, ["TotLoansRepaybleOnDemand"] = stBorrow },
                        ["LoansFrmRelatedParties"] = 0, ["Deposits"] = 0, ["TotalBorrowings"] = stBorrow, ["TradePayables"] = cred,
                    },
                    ["OthFinancialLiabilities"] = new JsonObject
                    {
                        ["CurrMatOnLTDebt"] = 0, ["CurrMatFinanceOblg"] = 0, ["AccrInterest"] = 0, ["UnpaidDividend"] = 0,
                        ["AppMonyRecvdAllotSecurities"] = 0, ["UnpaidMatDeposits"] = 0, ["UnpaidMatureDebenture"] = 0, ["TotOthFinancialLiab"] = 0,
                    },
                    ["TottalFinancialLiab"] = stBorrow + cred,
                    ["OtherCuurLiabilities"] = new JsonObject { ["RevenueRecvdAdvance"] = 0, ["OthersAdvTotal"] = othCl, ["TotalOthCurrLiab"] = othCl },
                    ["Provosions"] = new JsonObject { ["ProvosionEmpBenft"] = 0, ["TotalProvosions"] = 0 },
                    ["CurrTaxLiabilities"] = taxProv,
                    ["TotalCurrentLiab"] = totCurrLiab,
                    ["TotalEquityLiab"] = totEqAndLiab,
                },
            }
        },
        ["Assets"] = new JsonObject
        {
            ["NonCurrAssets"] = new JsonObject
            {
                ["PropertyPlantEquip"] = new JsonObject
                {
                    ["GrossBlock"] = faGross, ["Depreciation"] = faDep, ["ImpairmentLosses"] = 0, ["NetBlock"] = faNet, ["CapWrkProg"] = cwipp,
                    ["InvstPropGrossBlock"] = 0, ["InvstPropDepreciation"] = 0, ["InvstPropImprLosses"] = 0, ["InvstPropNetBlock"] = 0,
                    ["GoodWlGrossBlock"] = 0, ["GoodWlImprLosses"] = 0, ["GoodWlNetBlock"] = 0,
                    ["OthIntAstGrossBlock"] = 0, ["OthIntAstAmortisation"] = 0, ["OthIntAstImprLosses"] = 0, ["OthIntAstNetBlock"] = 0,
                    ["IntAstUndrDevlpmnt"] = 0, ["BioAstGrossBlock"] = 0, ["BioAstImprLosses"] = 0, ["BioAstNetBlock"] = 0,
                    ["FinancialAssets"] = new JsonObject
                    {
                        ["Investments"] = new JsonObject
                        {
                            ["ListedEquities"] = 0, ["UnListedEquities"] = 0, ["Total"] = 0,
                            ["InvstPrfShares"] = 0, ["InvstGovtTrust"] = 0, ["InvstInDebenture"] = 0, ["InvstInMutualFunds"] = 0, ["InvstInPartnershpFirm"] = ncInv, ["TotalNonCurrentInvst"] = ncInv,
                        },
                        ["TradeReceivables"] = new JsonObject { ["SecuredConsGoods"] = 0, ["UnSecuredConsGoods"] = 0, ["Doubtful"] = 0, ["TotalTradeReceivbls"] = 0 },
                        ["Loans"] = new JsonObject { ["SecurityDepsts"] = 0, ["LoansRltdParties"] = 0, ["TotalLoans"] = 0, ["LoansBPPurpose"] = 0, ["LoansNotBPPurpose"] = 0, ["LoansToShrHolders"] = 0 },
                        ["OtherFinacialAssets"] = new JsonObject { ["BankDeposits"] = 0, ["OtherDeposits"] = 0, ["TotalOthFinancialAsst"] = 0, ["DefrdTaxAsst"] = 0 },
                        ["OtherNonCurrentAssets"] = new JsonObject { ["CapitalAdvanc"] = 0, ["AdvancOthCapital"] = 0, ["TotalNonCurrAsst"] = 0, ["NonCurrAsstDueShrHldr"] = 0 },
                        ["TotalNonCurrntAsst"] = totNonCurrAssets,
                    }
                }
            },
            ["CurrentAssets"] = new JsonObject
            {
                ["Inventories"] = new JsonObject
                {
                    ["RawMaterials"] = 0, ["WorkInProgress"] = 0, ["FinishedGoods"] = invnt, ["StockInTrade"] = 0,
                    ["StoresSpares"] = 0, ["LooseTools"] = 0, ["Others"] = 0, ["TotalInventories"] = invnt,
                },
                ["FinancialAssets"] = new JsonObject
                {
                    ["Investments"] = new JsonObject
                    {
                        ["ListedEquities"] = 0, ["UnListedEquities"] = 0, ["Total"] = 0,
                        ["InvstPrfShares"] = 0, ["InvstGovtTrust"] = 0, ["InvstInDebenture"] = 0, ["InvstInMutualFunds"] = 0, ["InvstInPartnershpFirm"] = 0,
                        ["OtherInvestment"] = cInv, ["TotalCurrentInvst"] = cInv,
                    },
                    ["TradeReceivables"] = new JsonObject { ["SecuredConsGoods"] = 0, ["UnSecuredConsGoods"] = debtors, ["Doubtful"] = 0, ["TotalTradeReceivbls"] = debtors },
                    ["CashEquivalents"] = new JsonObject { ["BalancesWithBanks"] = cash, ["ChequeDraftsInHand"] = 0, ["CashOnHand"] = 0, ["TotalCashEquivalents"] = cash, ["BankBalanceOther"] = 0 },
                    ["Loans"] = new JsonObject { ["SecurityDepsts"] = 0, ["LoansRltdParties"] = 0, ["TotalLoans"] = loansAdv, ["LoansBPPurpose"] = loansAdv, ["LoansNotBPPurpose"] = 0, ["LoansToShrHolders"] = 0 },
                    ["OtherFinancialAsst"] = 0,
                    ["TotalFinancialAsst"] = cInv + debtors + cash + loansAdv,
                    ["CurrentTaxAsst"] = 0,
                    ["OtherCurrentAssets"] = new JsonObject { ["AdvancOthCapital"] = othCa, ["TotalOthCurrentAsst"] = othCa, ["TotalCurrAsst"] = totCurrAssets },
                }
            }
        },
        ["TotalAssets"] = totAssets,
    };
}
