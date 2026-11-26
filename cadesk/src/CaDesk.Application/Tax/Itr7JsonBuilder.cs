using System.Text.Json.Nodes;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Tax;
using static CaDesk.Application.Tax.BusinessItrJsonHelper;
using static CaDesk.Application.Tax.CbdtJsonExportHelper;

namespace CaDesk.Application.Tax;

/// <summary>
/// Builds official CBDT AY 2026-27 ITR-7 JSON (Charitable/Religious Trusts, Sec 8 Companies,
/// Research/Educational Institutions u/s 139(4A)/(4B)/(4C)/(4D)) validated against <c>ITR-7_2026_Main_V0.1.json</c>.
/// </summary>
public static class Itr7JsonBuilder
{
    public const string EmbeddedSchemaName = "ITR-7_2026_Main_V0.1.json";

    public static IncomeTaxJsonExportResult Build(
        TaxComputation comp,
        Client client,
        TrustTaxResultDto trust,
        ExportItrJsonRequest? req = null)
    {
        var issues = new List<IncomeTaxJsonExportIssue>();
        var input = trust.NormalizedInput;
        var (ayStart4, ayDashed, _, _) = ParseAyWindow(comp.AssessmentYear);
        string pan = NormalizePan(ExtractTaxId(client, "PAN"), issues, "ITR.ITR7.PartA_GEN1.OrgFirmInfo.PAN");
        string name = Trunc(client.LegalName ?? client.Name, 125, "Charitable Trust");

        int returnSec = req?.ReturnFileSec is >= 11 and <= 20 ? req.ReturnFileSec.Value : 11;
        string statusCode = input.StatusOrCompanyType is "4" or "5" or "6" or "7" ? input.StatusOrCompanyType : "5";
        string furnSec = input.ReturnFurnishedSection is "139-4A" or "139-4B" or "139-4C" or "139-4D" ? input.ReturnFurnishedSection : "139-4A";
        string exempSec = string.IsNullOrWhiteSpace(input.SectionExemptionClaimed) ? "11" : input.SectionExemptionClaimed;

        var orgInfo = new JsonObject
        {
            ["AssesseeName"] = new JsonObject { ["SurNameOrOrgName"] = name },
            ["PAN"] = pan,
            ["Address"] = BuildBusinessAddress(client, req),
            ["DateOFFormOrIncorp"] = !string.IsNullOrWhiteSpace(input.IncorporationDate) ? input.IncorporationDate : "2018-04-01",
            ["StatusOrCompanyType"] = statusCode,
            ["ReturnFurnishedSec"] = furnSec,
            ["SecExemptionClaimed"] = exempSec,
        };

        JsonArray? regApprArr = !string.IsNullOrWhiteSpace(input.Registration12AbNo)
            ? new JsonArray
            {
                new JsonObject
                {
                    ["SectionRegistered"] = "I",
                    ["RegSecExmpClaimed"] = "true",
                    ["RegApprovalDate"] = !string.IsNullOrWhiteSpace(input.Registration12AbDate) ? input.Registration12AbDate : "2021-06-15",
                    ["ApprovalRegistrationNo"] = Trunc(input.Registration12AbNo, 50, "AAABT1234AE20214"),
                    ["ApprovingAuthority"] = "CIT Exemption",
                }
            }
            : null;

        var filingStatus = new JsonObject
        {
            ["ReturnFileSec"] = new JsonObject { ["IncomeTaxSec"] = returnSec },
            ["ResidentialStatus"] = "RES",
            ["AsseseeRepFlg"] = "N",
            ["PartnerInFirmFlg"] = "N",
            ["HeldUnlistedEqShrPrYrFlg"] = "N",
        };

        var partAGen2 = new JsonObject
        {
            ["OtherDetailsFor7"] = new JsonObject
            {
                ["OtherDetailsUs2_15"] = new JsonObject { ["CharitablePurposeOfGeneralPublic"] = "N" },
                ["FirstReturnFlag"] = "N",
                ["ProvisionsSec1310Applcbl"] = "N",
            },
            ["LiableSec44ABflg"] = "N",
            ["LiableAnyOthThnINTActflg"] = "N",
        };

        // PARTA_BS (Trust Balance Sheet)
        long c80g = ToRupees(input.CorpusDonations80G);
        long cOth = ToRupees(Math.Max(input.CorpusFundClosing, input.CorpusDonationsOther));
        long accInc = ToRupees(input.AccumulatedFundClosing);
        long totOwn = c80g + cOth + accInc;
        long secLoan = ToRupees(input.SecuredLoans);
        long unsecLoan = ToRupees(input.UnsecuredLoans);
        long totLoan = secLoan + unsecLoan;
        long totSrc = totOwn + totLoan;

        long faGross = ToRupees(input.FixedAssetsGrossBlock > 0m ? input.FixedAssetsGrossBlock : input.FixedAssetsNetBlock);
        long faDep = ToRupees(input.FixedAssetsDepreciation);
        long faNet = Math.Max(0L, faGross - faDep);
        long inv115 = ToRupees(input.InvestmentsSec11_5);
        long debtors = ToRupees(input.SundryDebtors);
        long cash = ToRupees(input.CashAndBankBalances);
        long totCa = debtors + cash;
        long loansAdv = ToRupees(input.LoansAndAdvances);
        long totCaLoan = totCa + loansAdv;
        long cred = ToRupees(input.SundryCreditors);
        long prov = ToRupees(input.Provisions);
        long totCl = cred + prov;
        long netCa = totCaLoan - totCl;
        long totApp = faNet + inv115 + netCa;

        var partABs = new JsonObject
        {
            ["SourcesOfFund"] = new JsonObject
            {
                ["OwnFund"] = new JsonObject
                {
                    ["Corpus80G"] = c80g,
                    ["OtherCorpus"] = cOth,
                    ["AccumulatedInc"] = accInc,
                    ["AccumulatedIncUS10_11"] = 0,
                    ["BalDeemedInc"] = 0,
                    ["TotalOtherReserve"] = 0,
                    ["TotalFund"] = totOwn,
                },
                ["LongTermBorrowings"] = new JsonObject { ["SecuredLoan"] = secLoan, ["UnSecuredLoan"] = unsecLoan, ["TotalLoanFund"] = totLoan },
                ["Advances"] = 0,
                ["TotSourceFund"] = totSrc,
            },
            ["ApplicationOfFunds"] = new JsonObject
            {
                ["FixedAsset"] = new JsonObject { ["GrossBlock"] = faGross, ["Depreciation"] = faDep, ["NetBlock"] = faNet },
                ["Investements"] = inv115,
                ["CurrentAssetsLoanAdv"] = new JsonObject
                {
                    ["CurrentAssets"] = new JsonObject
                    {
                        ["Inventory"] = 0,
                        ["SundryDebtor"] = debtors,
                        ["CashNCashEquivalents"] = new JsonObject { ["BalWithBanks"] = cash, ["CashInHand"] = 0, ["Others"] = 0, ["TotCashNCashEquivalents"] = cash },
                        ["OtherCurrAssets"] = 0,
                        ["TotCurrAssets"] = totCa,
                    },
                    ["LoansandAdvances"] = loansAdv,
                    ["Total"] = totCaLoan,
                    ["CurrLiabilitiesProviosions"] = new JsonObject
                    {
                        ["CurrLiability"] = new JsonObject { ["SundryCreditor"] = cred, ["OtherPayable"] = 0, ["TotalCurrLiabilitiesProviosions"] = cred },
                        ["Provisions"] = prov,
                        ["TotCurrLiabilitiesandprovisions"] = totCl,
                    },
                    ["NetCurrAssets"] = netCa,
                },
                ["TotalApplicationOfFunds"] = totApp,
            },
        };

        // ScheduleVC
        long gGovt = ToRupees(input.GrantsReceivedGovt);
        long gCorp = ToRupees(input.GrantsReceivedCorporate);
        long dOth = ToRupees(input.OtherSpecificAndGeneralDonations);
        long locOthCorpus = ToRupees(input.CorpusDonationsOther);
        long locNonCorpus = gGovt + gCorp + dOth;
        long locTot = c80g + locOthCorpus + locNonCorpus;
        long fgn = ToRupees(input.ForeignContributions);
        long totContrib = locTot + fgn;
        long anonTot = ToRupees(trust.AnonymousDonationsTotal);
        long anon115Bbc = ToRupees(trust.AnonymousDonationsTaxable115BBC);

        var schedVc = new JsonObject
        {
            ["Local"] = new JsonObject
            {
                ["CorpusFundDonation"] = c80g + locOthCorpus,
                ["CorpusFundDonationUS80G2b"] = c80g,
                ["CorpusFundDonationOther80G2b"] = locOthCorpus,
                ["GrantsReceivedFormGovt"] = gGovt,
                ["GrantsReceivedFromCompanie"] = gCorp,
                ["OtherSpecificGrants"] = 0,
                ["OtherDonation"] = dOth,
                ["TotalOtherThanCorpusFund"] = locNonCorpus,
                ["VoluntaryContribution"] = locTot,
            },
            ["Foreign"] = new JsonObject
            {
                ["CorpusFundDonation"] = 0,
                ["CorpusFundDonationUS80G2b"] = 0,
                ["CorpusFundDonationOther80G2b"] = 0,
                ["OtherThanCorpusFund"] = fgn,
                ["ForeignContribution"] = fgn,
            },
            ["TotalContribution"] = totContrib,
            ["AnonymousDonations"] = new JsonObject
            {
                ["AggregateAnonymousDonations"] = anonTot,
                ["TotalDonationsReceived"] = totContrib + anonTot,
                ["AnonymousDonations115BBC"] = anon115Bbc,
                ["AnonymousDonationsOthr115BBC"] = Math.Max(0L, anonTot - anon115Bbc),
            },
        };

        // ScheduleAI
        long rMain = ToRupees(input.ReceiptsFromMainObjects);
        long rInc = ToRupees(input.ReceiptsIncidental);
        long rRent = ToRupees(input.RentIncome);
        long rDiv = ToRupees(input.DividendIncome);
        long rInt = ToRupees(input.InterestIncome);
        long aiOther = rRent + rDiv + rInt;
        long aiTot = rMain + rInc + aiOther;

        var schedAi = new JsonObject
        {
            ["RecptMainObj"] = rMain,
            ["RecptsIncidentalObj"] = rInc,
            ["Rent"] = rRent,
            ["Commission"] = 0,
            ["DividendIncome"] = rDiv,
            ["InterestIncome"] = rInt,
            ["AgricultureIncome"] = 0,
            ["NetConsdrnTrnsfrCapAsst"] = 0,
            ["PassThroughIncome"] = 0,
            ["TotalofOtherIncomes"] = aiOther,
            ["TotalofAggregateIncomes"] = aiTot,
        };

        // ScheduleA
        long revApp = ToRupees(input.RevenueApplicationCharitable);
        long capApp = ToRupees(input.CapitalApplicationCharitable);
        long totAppClaimed = revApp + capApp;
        long disallowApp = ToRupees(trust.DisallowedApplication);
        long netAllowedApp = Math.Max(0L, totAppClaimed - disallowApp);
        string actKey = input.ActivityCategory is "MedicalRelief" or "ReliefOfPoor" or "Religious" or "GeneralPublicUtility"
            ? input.ActivityCategory : "Educational";

        var schedA = new JsonObject
        {
            ["AppTowExpTrstInst"] = new JsonObject
            {
                [actKey] = new JsonObject { ["Revenue"] = revApp, ["Capital"] = capApp, ["Total"] = totAppClaimed },
                ["TotalA1toA11"] = new JsonObject { ["Revenue"] = revApp, ["Capital"] = capApp, ["Total"] = totAppClaimed },
            },
            ["ExpNotAllowedApplication"] = new JsonObject
            {
                ["TotExpNotAllowedApplication"] = new JsonObject { ["Revenue"] = disallowApp, ["Capital"] = 0, ["Total"] = disallowApp },
            },
            ["SrcRevCapApplctn"] = new JsonObject
            {
                ["TotSrcRevCapApplctn"] = new JsonObject { ["Revenue"] = revApp, ["Capital"] = capApp, ["Total"] = totAppClaimed },
            },
            ["TotAmtAppDrngPrevYr"] = new JsonObject { ["Revenue"] = revApp, ["Capital"] = capApp, ["Total"] = totAppClaimed },
            ["TotAmountAllowedApplication"] = new JsonObject
            {
                ["Revenue"] = Math.Max(0L, revApp - disallowApp),
                ["Capital"] = capApp,
                ["Total"] = netAllowedApp,
            },
        };

        // Schedule115BBI & PartB_TI
        long spec115Bbi = ToRupees(trust.SpecifiedIncome115BBI);
        var sched115Bbi = new JsonObject
        {
            ["DeemedIncSec1023C_113"] = spec115Bbi,
            ["DeemedIncSec111B"] = 0,
            ["IncDeemedSec131c"] = 0,
            ["IncNotExemptSec131d"] = 0,
            ["IncNotExcludedSec111c"] = 0,
            ["IncAccInExcess"] = 0,
            ["Total"] = spec115Bbi,
        };

        long elig = ToRupees(trust.TotalEligibleIncome);
        long stat15 = ToRupees(trust.StatutoryAccumulation15Pct);
        long form10 = ToRupees(trust.Form10Accumulation);
        long totDed = netAllowedApp + form10;
        long shortfall = ToRupees(trust.ShortfallTaxableNormalIncome);
        long totInc = ToRupees(trust.TotalTaxableIncome);

        var partBTi = new JsonObject
        {
            ["VcCorpusSec11"] = ToRupees(trust.CorpusExempt11_1d),
            ["VoluntaryContributions"] = new JsonObject
            {
                ["TotIncFromVC"] = ToRupees(trust.GrossVoluntaryNonCorpus) + Math.Max(0L, anonTot - anon115Bbc),
                ["CorpusDonationUS80G"] = c80g,
                ["CorpusOtherThan80G"] = locOthCorpus,
            },
            ["AggregateIncomeUs1112"] = aiTot,
            ["AmtForCharitableUs111"] = stat15,
            ["IncToBeApplied"] = elig,
            ["TIDeductions"] = new JsonObject
            {
                ["AmtAppliedtForCharitablePurpose"] = netAllowedApp,
                ["AmtAppForCharitablePurposeRepayment"] = 0,
                ["AmtAppliedSpecifiedMode"] = 0,
                ["AmtDeemedForCharitable"] = 0,
                ["AmtAccumulatedForCharitable"] = form10,
                ["AmtFulfilledUs11_2"] = 0,
                ["TotalDeductions"] = totDed,
            },
            ["TIAdditions"] = new JsonObject
            {
                ["IncChargeableUs115BBI"] = spec115Bbi,
                ["ExemptionUs11_13Dtl"] = new JsonObject { ["AnonymousDonationVC"] = anon115Bbc },
                ["IncChargeableUs12_2"] = 0,
                ["AmtDsllwblUs111RWS40AIA"] = disallowApp,
                ["AmtDsllwblUs111RWS40A3"] = 0,
                ["IncExp3BUS80G"] = 0,
                ["IncExp1BUS80G"] = 0,
                ["AnyOthrIncome"] = 0,
                ["TotalAdditions"] = spec115Bbi + anon115Bbc + disallowApp,
            },
            ["IncChargeableUs11_4"] = 0,
            ["IncChargUs115BBIIncld13"] = spec115Bbi,
            ["TotalTI"] = shortfall,
            ["IncomeFromHP"] = 0,
            ["ProfBusGain"] = new JsonObject { ["ProfGainNoSpecBus"] = 0 },
            ["CapGain"] = new JsonObject
            {
                ["ShortTerm"] = new JsonObject { ["ShortTerm20Per"] = 0, ["ShortTerm30Per"] = 0, ["ShortTermAppRate"] = 0, ["ShortTermSplRateDTAA"] = 0, ["TotalShortTerm"] = 0 },
                ["LongTerm"] = new JsonObject { ["LongTerm12_5Per"] = 0, ["LongTermSplRateDTAA"] = 0, ["TotalLongTerm"] = 0 },
                ["ShortTermLongTermTotal"] = 0,
                ["CapGains30Per115BBH"] = 0,
                ["TotalCapGains"] = 0,
            },
            ["IncFromOS"] = new JsonObject { ["TotIncFromOS"] = 0 },
            ["TotIncNotPart7And11Abv"] = 0,
            ["GrossIncome"] = totInc,
            ["CurrentYearLoss"] = 0,
            ["TotalIncome"] = totInc,
            ["IncChargeableTaxSplRates"] = anon115Bbc + spec115Bbi,
            ["DonationsUs115BBC"] = anon115Bbc,
            ["AggIncothSpecInc115BBI"] = Math.Max(0L, totInc - spec115Bbi - anon115Bbc),
        };

        long normTax = ToRupees(trust.TaxOnNormalShortfall);
        long t115Bbc = ToRupees(trust.TaxOnAnonymous115BBC);
        long t115Bbi = ToRupees(trust.TaxOn115BBI);
        long totBase = normTax + t115Bbc + t115Bbi;
        long sc = ToRupees(trust.Surcharge);
        long cess = ToRupees(trust.Cess);
        long grossTax = totBase + sc + cess;
        long i234a = ToRupees(input.Interest234A);
        long i234b = ToRupees(input.Interest234B);
        long i234c = ToRupees(input.Interest234C);
        long f234f = ToRupees(input.LateFee234F);
        long prepaid = ToRupees(trust.PrepaidTaxes);
        long adv = ToRupees(input.AdvanceTaxPaid);
        long tds = ToRupees(input.TdsCredit);
        long tcs = ToRupees(input.TcsCredit);
        long sat = ToRupees(input.SelfAssessmentTaxPaid);

        var partBTti = new JsonObject
        {
            ["ComputationOfTaxLiability"] = new JsonObject
            {
                ["TaxPayableOnTI"] = new JsonObject
                {
                    ["TaxAtNormalRates"] = normTax,
                    ["TaxAtSpecialRates"] = 0,
                    ["DonationUs115BC"] = t115Bbc,
                    ["TaxIncChargUs115BBI"] = t115Bbi,
                    ["TaxAtMarginalRate"] = 0,
                    ["RebateOnAgricultureInc"] = 0,
                    ["TaxPayableOnTotInc"] = totBase,
                },
                ["Surcharge25ofSI"] = 0,
                ["SurchargeOnTaxPayable"] = sc,
                ["TotalSurcharge"] = sc,
                ["EducationCess"] = cess,
                ["GrossTaxLiability"] = grossTax,
                ["TaxRelief"] = new JsonObject { ["Section90"] = 0, ["Section91"] = 0, ["TotTaxRelief"] = 0 },
                ["NetTaxLiability"] = grossTax,
                ["IntrstPay"] = new JsonObject
                {
                    ["IntrstPayUs234A"] = i234a, ["IntrstPayUs234B"] = i234b, ["IntrstPayUs234C"] = i234c,
                    ["LateFilingFee234F"] = f234f, ["TotalIntrstPay"] = i234a + i234b + i234c + f234f,
                },
                ["AggregateTaxInterestLiability"] = ToRupees(trust.TotalTaxLiability),
            },
            ["TaxPaid"] = new JsonObject
            {
                ["TaxesPaid"] = new JsonObject
                {
                    ["AdvanceTax"] = adv, ["TDS"] = tds, ["TCS"] = tcs, ["SelfAssessmentTax"] = sat, ["TotalTaxesPaid"] = prepaid,
                },
                ["BalTaxPayable"] = Math.Max(0L, ToRupees(trust.TotalTaxLiability) - prepaid),
            },
            ["Refund"] = new JsonObject
            {
                ["RefundDue"] = trust.NetPayableOrRefund < 0m ? ToRupees(-trust.NetPayableOrRefund) : 0L,
                ["NetTaxPyblOn115TDInc"] = 0,
                ["BankAccountDtls"] = new JsonObject { ["BankDtlsFlag"] = "N" },
            },
            ["AssetOutsideIndiaFlg"] = "NO",
        };

        string signerPan = !string.IsNullOrWhiteSpace(req?.VerificationPan) && req.VerificationPan.Trim().Length == 10 && req.VerificationPan.Trim().ToUpperInvariant()[3] == 'P'
            ? req.VerificationPan.Trim().ToUpperInvariant()
            : "ABCPT1234A";

        var partAGen1 = new JsonObject { ["OrgFirmInfo"] = orgInfo, ["FilingStatus"] = filingStatus };
        if (regApprArr is not null)
        {
            partAGen1["RegApprUnderITADtls"] = regApprArr;
        }

        var itr7 = new JsonObject
        {
            ["CreationInfo"] = BuildCreationInfo(req?.IntermediaryCity ?? req?.City ?? "Kochi"),
            ["Form_ITR7"] = new JsonObject
            {
                ["FormName"] = "ITR-7",
                ["Description"] = "For persons including companies required to furnish return under sections 139(4A), 139(4B), 139(4C) or 139(4D)",
                ["AssessmentYear"] = ayStart4,
                ["SchemaVer"] = "Ver1.0",
                ["FormVer"] = "Ver1.0",
            },
            ["PartA_GEN1"] = partAGen1,
            ["PartA_GEN2"] = partAGen2,
            ["PARTA_BS"] = partABs,
            ["ScheduleVC"] = schedVc,
            ["ScheduleAI"] = schedAi,
            ["ScheduleA"] = schedA,
            ["Schedule115BBI"] = sched115Bbi,
            ["PartB_TI"] = partBTi,
            ["PartB_TTI"] = partBTti,
            ["Verification"] = new JsonObject
            {
                ["Declaration"] = new JsonObject
                {
                    ["AssesseeVerName"] = Trunc(req?.VerificationName, 127, "Managing Trustee"),
                    ["FatherName"] = Trunc(req?.FatherName, 125, "NA"),
                    ["AssesseeVerPAN"] = signerPan,
                    ["Capacity"] = req?.Capacity is "MD" or "DR" or "PO" or "CE" or "RE" or "OT" ? req.Capacity : "DR",
                },
                ["Date"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                ["Place"] = Trunc(req?.Place ?? req?.City, 50, "Kochi"),
            },
        };

        var root = new JsonObject { ["ITR"] = new JsonObject { ["ITR7"] = itr7 } };
        string fileName = $"{pan}_ITR7_AY{ayDashed}.json";
        return ValidateAndFinalize(root, EmbeddedSchemaName, "ITR-7", "Ver1.0 (V0.1 Schema)", ayDashed, comp.FinancialYear, fileName, issues);
    }
}
