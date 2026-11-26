using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CaDesk.Domain.Clients;
using static CaDesk.Application.Tax.CbdtJsonExportHelper;

namespace CaDesk.Application.Tax;

/// <summary>
/// Shared CBDT AY 2026-27 JSON schedule builders shared between ITR-5 (Firm/LLP/AOP) and ITR-6 (Company)
/// to ensure a single source of truth for P&L, Schedule BP, Depreciation, Chapter VI-A, Schedule SI,
/// Part B-TI, Tax Paid, Schedule IT/TDS2, Bank Accounts, and Verification.
/// </summary>
internal static partial class BusinessItrJsonHelper
{
    private const decimal Stcg111ARatePercent = 20m;
    private const decimal LtcgRatePercent = 12.5m;

    [GeneratedRegex(@"^[A-Z]{3}P[A-Z][0-9]{4}[A-Z]$")]
    private static partial Regex IndividualPanRegex();

    [GeneratedRegex(@"^[A-Z]{4}[0-9]{5}[A-Z]$")]
    private static partial Regex TanRegex();

    public static JsonObject BuildBusinessAddress(Client client, ExportItrJsonRequest? req)
    {
        string mobileStr = ResolveMobileLong(req?.MobileNo, client.Phone).ToString();
        long mobileNum = long.Parse(mobileStr);
        return new JsonObject
        {
            ["ResidenceNo"] = Trunc(req?.ResidenceNo, 50, "Office No. 1"),
            ["LocalityOrArea"] = Trunc(req?.LocalityOrArea, 50, "Main Road"),
            ["CityOrTownOrDistrict"] = Trunc(req?.City, 50, "Kochi"),
            ["StateCode"] = ResolveStateCode(req?.StateCode, client),
            ["CountryCode"] = "91",
            ["PinCode"] = ResolvePinInt(req?.PinCode),
            ["CountryCodeMobile"] = 91,
            ["MobileNo"] = mobileNum,
            ["EmailAddress"] = ResolveEmail(req?.EmailAddress, client.Email),
        };
    }

    public static JsonObject BuildAddressDetailWithZipCode(
        string? address,
        string? city,
        string? stateCode,
        string? pinCode,
        Client client) => new()
    {
        ["AddrDetail"] = Trunc(address, 200, "Registered Office"),
        ["CityOrTownOrDistrict"] = Trunc(city, 50, "Kochi"),
        ["StateCode"] = ResolveStateCode(stateCode, client),
        ["CountryCode"] = "91",
        ["PinCode"] = ResolvePinInt(pinCode),
    };

    public static JsonObject BuildNatOfBus(string? code, string name) => new()
    {
        ["NatureOfBusiness"] = new JsonArray
        {
            new JsonObject
            {
                ["Code"] = string.IsNullOrWhiteSpace(code) ? "16013" : code.Trim(),
                ["TradeName1"] = Trunc(name, 75, "Business Operations"),
                ["Description"] = "Primary Business or Profession",
            }
        }
    };

    public static JsonObject BuildPartAPl(CalculateBusinessTaxRequest input, BusinessTaxComputationResultDto biz, bool isItr6, bool isIndAs)
    {
        long gp = Math.Max(0L, ToRupees(input.RevenueFromOperations - input.PurchasesAndDirectCost - input.ChangesInInventories));
        long rent = ToRupees(input.RentIncome);
        long div = ToRupees(input.DividendIncome);
        long intr = ToRupees(input.InterestIncome);
        long miscOth = ToRupees(input.OtherIncome);
        long totOth = rent + div + intr + miscOth;
        long totCredits = gp + totOth;

        long empCompTotal = ToRupees(input.EmployeeBenefitExpense);
        long remunPartner = ToRupees(input.PartnerRemunerationDebited);
        long interestTotal = ToRupees(input.FinanceCost + input.PartnerInterestDebited);
        long pbidta = ToSignedRupees(biz.BookProfitBeforeTax + input.DepreciationPerBooks + input.FinanceCost + input.PartnerInterestDebited);
        long depBooks = ToRupees(input.DepreciationPerBooks);
        long pbt = ToSignedRupees(biz.BookProfitBeforeTax);
        long pat = ToSignedRupees(biz.BookProfitAfterTax);

        var debitPlAcnt = new JsonObject
        {
            ["Freight"] = 0,
            ["ConsumptionOfStores"] = 0,
            ["PowerFuel"] = 0,
            ["RentExpdr"] = 0,
            ["RepairsBldg"] = 0,
            ["RepairMach"] = 0,
            ["EmployeeComp"] = new JsonObject
            {
                ["SalsWages"] = empCompTotal,
                ["Bonus"] = 0,
                ["MedExpReimb"] = 0,
                ["LeaveEncash"] = 0,
                ["LeaveTravelBenft"] = 0,
                ["ContToSuperAnnFund"] = 0,
                ["ContToPF"] = 0,
                ["ContToGratFund"] = 0,
                ["ContToOthFund"] = 0,
                ["OthEmpBenftExpdr"] = 0,
                ["TotEmployeeComp"] = empCompTotal,
            },
            ["Insurances"] = new JsonObject
            {
                ["MedInsur"] = 0,
                ["LifeInsur"] = 0,
                ["KeyManInsur"] = 0,
                ["OthInsur"] = 0,
                ["TotInsurances"] = 0,
            },
            ["StaffWelfareExp"] = 0,
            ["Entertainment"] = 0,
            ["Hospitality"] = 0,
            ["Conference"] = 0,
            ["SalePromoExp"] = 0,
            ["Advertisement"] = 0,
            ["CommissionExpdrDtls"] = new JsonObject { ["NonResOtherCompany"] = 0, ["Others"] = 0, ["Total"] = 0 },
            ["RoyalityDtls"] = new JsonObject { ["NonResOtherCompany"] = 0, ["Others"] = 0, ["Total"] = 0 },
            ["ProfessionalConstDtls"] = new JsonObject { ["NonResOtherCompany"] = 0, ["Others"] = 0, ["Total"] = 0 },
            ["HotelBoardLodge"] = 0,
            ["TravelExp"] = 0,
            ["ForeignTravelExp"] = 0,
            ["ConveyanceExp"] = 0,
            ["TelephoneExp"] = 0,
            ["GuestHouseExp"] = 0,
            ["ClubExp"] = 0,
            ["FestivalCelebExp"] = 0,
            ["Scholarship"] = 0,
            ["Gift"] = 0,
            ["Donation"] = 0,
            ["RatesTaxesPays"] = new JsonObject
            {
                ["ExciseCustomsVAT"] = new JsonObject
                {
                    ["UnionExciseDuty"] = 0,
                    ["ServiceTax"] = 0,
                    ["VATorSaleTax"] = 0,
                    ["CentralGoodServiceTax"] = 0,
                    ["StateGoodServiceTax"] = 0,
                    ["IntegratedGoodServiceTax"] = 0,
                    ["UnionTerrGoodServiceTax"] = 0,
                    ["OthDutyTaxCess"] = 0,
                    ["TotExciseCustomsVAT"] = 0,
                }
            },
            ["AuditFee"] = ToRupees(input.AuditFee),
        };

        if (!isItr6)
            debitPlAcnt["SalRemuneration"] = remunPartner;

        debitPlAcnt["OtherExpenses"] = ToRupees(input.OtherExpenses);
        debitPlAcnt["BadDebtDtls"] = new JsonObject
        {
            ["BadDebtAmtDtlsTotal"] = 0,
            ["OthersPANNotAvlblDtlTotal"] = 0,
            ["OthersAmtLt1Lakh"] = 0,
            ["BadDebt"] = 0,
        };
        debitPlAcnt["ProvForBadDoubtDebt"] = 0;
        debitPlAcnt["OthProvisionsExpdr"] = 0;
        debitPlAcnt["PBIDTA"] = pbidta;

        var interestObj = new JsonObject
        {
            ["NonResOtherCompany"] = 0,
            ["Others"] = ToRupees(input.FinanceCost),
        };
        if (!isItr6)
        {
            interestObj["ResPartners"] = ToRupees(input.PartnerInterestDebited);
            interestObj["ResOthers"] = ToRupees(input.FinanceCost);
        }
        interestObj["InterestExpdr"] = interestTotal;
        debitPlAcnt["InterestExpdrtDtls"] = interestObj;
        debitPlAcnt["DepreciationAmort"] = depBooks;
        debitPlAcnt["PBT"] = pbt;

        var appropriations = new JsonObject { ["TrfToReserves"] = 0 };
        if (isItr6) appropriations["TotAppropriations"] = 0;

        var taxProvAppr = new JsonObject
        {
            ["ProvForCurrTax"] = ToRupees(input.TaxProvisionCurrent),
            ["ProvDefTax"] = ToRupees(input.TaxProvisionDeferred),
            ["ProfitAfterTax"] = pat,
            ["BalBFPrevYr"] = ToSignedRupees(input.BalanceBroughtForwardPl),
            ["AmtAvlAppr"] = pat + ToSignedRupees(input.BalanceBroughtForwardPl),
            ["Appropriations"] = appropriations,
            ["PartnerAccBalTrf"] = pat + ToSignedRupees(input.BalanceBroughtForwardPl),
        };

        var credits = new JsonObject
        {
            ["GrossProfitTrnsfFrmTrdAcc"] = gp,
            ["OthIncome"] = new JsonObject
            {
                ["RentInc"] = rent,
                ["Comissions"] = 0,
                ["Dividends"] = div,
                ["InterestInc"] = intr,
                ["ProfitOnSaleFixedAsset"] = 0,
                ["ProfitOnInvChrSTT"] = 0,
                ["ProfitOnOthInv"] = 0,
                ["ProfitOnCurrFluct"] = 0,
                ["ProfitOnCnvInvntryToCapAsst"] = 0,
                ["ProfitOnAgriIncome"] = 0,
                ["MiscOthIncome"] = miscOth,
                ["TotOthIncome"] = totOth,
            },
            ["TotCreditsToPL"] = totCredits,
        };

        var pl = new JsonObject
        {
            ["CreditsToPL"] = credits,
            ["DebitsToPL"] = new JsonObject
            {
                ["DebitPlAcnt"] = debitPlAcnt,
                ["TaxProvAppr"] = taxProvAppr,
            }
        };

        if (isItr6)
        {
            if (isIndAs)
            {
                pl["OtherComprnsvInc"] = new JsonObject
                {
                    ["TotalComprIncome"] = 0,
                };
            }
            else
            {
                pl["TotalNumOfMonths"] = 12;
            }
        }
        else
        {
            pl["PersumptiveInc44AD"] = new JsonObject { ["GrsTrnOverOrReceipt"] = 0, ["TotPersumptiveInc44AD"] = 0 };
            pl["PersumptiveInc44ADA"] = new JsonObject { ["GrsReceipt"] = 0 };
            pl["TotalPrsumptvIncUs44E"] = 0;
            pl["NoBooksOfAccPL"] = new JsonObject
            {
                ["GrossReceipt"] = 0,
                ["GrsRcptAccPayeeOrBankMode"] = 0,
                ["GrsRcptOtherMode"] = 0,
                ["GrossProfit"] = 0,
                ["Expenses"] = 0,
                ["NetProfit"] = 0,
                ["GrossReceiptPrf"] = 0,
                ["GrsRcptAccPayeeOrBankModePrf"] = 0,
                ["GrsRcptOtherModePrf"] = 0,
                ["GrossProfitPrf"] = 0,
                ["ExpensesPrf"] = 0,
                ["NetProfitPrf"] = 0,
                ["TotBusinessProfession"] = 0,
            };
            pl["TurnverFrmSpecActivity"] = 0;
            pl["NetIncomeFrmSpecActivity"] = 0;
        }

        return pl;
    }

    public static JsonObject BuildCorpScheduleBp(CalculateBusinessTaxRequest input, BusinessTaxComputationResultDto biz, bool isItr6)
    {
        long pbt = ToSignedRupees(biz.BookProfitBeforeTax);
        long exclOther = ToRupees(biz.OtherHeadIncomesExcludedFromPl);
        long balanceAfterExcl = pbt - exclOther;
        long depBooks = ToRupees(biz.DepreciationPerBooks);
        long depIt = ToRupees(biz.IncomeTaxDepreciationSec32);

        long d36 = ToRupees(input.DisallowSec36);
        long d37 = ToRupees(input.DisallowSec37);
        long d40 = ToRupees(input.DisallowSec40a + biz.DisallowedRemuneration40b + biz.DisallowedPartnerInterest40b);
        long d40a = ToRupees(input.DisallowSec40A3);
        long d43b = ToRupees(input.DisallowSec43B);
        long d43bh = ToRupees(input.DisallowSec43BhMsme);
        long othDisallow = ToRupees(input.OtherDisallowances);
        long totAdd = depBooks + d36 + d37 + d40 + d40a + d43b + d43bh + othDisallow;
        long allowDed = depIt + ToRupees(input.AllowableDeductionsUs32Etc);
        long pgbpAfterDed = ToSignedRupees(biz.ComputedBusinessIncomeBeforeLoss);
        long bfLossSetOff = ToRupees(biz.BroughtForwardLossSetOff);
        long netPgbp = ToSignedRupees(biz.NetPgbpIncome);

        var profRefrdSec = new JsonObject();
        if (!isItr6)
        {
            profRefrdSec["ProfitLossUs44AD"] = 0;
            profRefrdSec["ProfitLossUs44ADA"] = 0;
        }
        profRefrdSec["ProfitLossUs44AE"] = 0;
        profRefrdSec["ProfitLossUs44B"] = 0;
        profRefrdSec["ProfitLossUs44BB"] = 0;
        profRefrdSec["ProfitLossUs44BBA"] = 0;
        if (isItr6) profRefrdSec["ProfitLossUs44BBB"] = 0;
        profRefrdSec["ProfitLossUs44BBC"] = 0;
        profRefrdSec["ProfitLossUs44BBD"] = 0;
        if (isItr6) profRefrdSec["ProfitLossUs44D"] = 0;
        profRefrdSec["ProfitLossUs44DA"] = 0;
        if (isItr6) profRefrdSec["ProfitChapterXIIG"] = 0;
        profRefrdSec["FirstSchITActOthr115B"] = 0;

        var deemedProfit = new JsonObject();
        if (!isItr6)
        {
            deemedProfit["Section44AD"] = 0;
            deemedProfit["Section44ADA"] = 0;
        }
        deemedProfit["Section44AE"] = 0;
        deemedProfit["Section44B"] = 0;
        deemedProfit["Section44BB"] = 0;
        deemedProfit["Section44BBA"] = 0;
        deemedProfit["Section44BBC"] = 0;
        deemedProfit["Section44BBD"] = 0;
        if (isItr6) deemedProfit["Section44BBB"] = 0;
        if (isItr6) deemedProfit["Section44D"] = 0;
        deemedProfit["Section44DA"] = 0;
        if (isItr6) deemedProfit["ChapterXIIG"] = 0;
        deemedProfit["FirstSchTActOther"] = 0;
        deemedProfit["TotDeemedProfitBusUs"] = 0;

        var busIncOth = new JsonObject
        {
            ["ProfBfrTaxPL"] = pbt,
            ["NetPLFromSpecBus"] = 0,
            ["NetProfLossSpecifiedBus"] = 0,
            ["IncRecCredPLOthHeadDtls"] = new JsonObject
            {
                ["HouseProperty"] = ToRupees(input.RentIncome),
                ["CapitalGains"] = 0,
                ["OtherSources"] = ToRupees(input.InterestIncome),
                ["UnderSec115BBF"] = 0,
                ["UnderSec115BBG"] = 0,
                ["Dividend"] = ToRupees(input.DividendIncome),
                ["OtherThanDividend"] = ToRupees(input.RentIncome + input.InterestIncome),
            },
            ["PLUs44sChapXIIGOthrUs115B"] = 0,
            ["ProfitLossInclRefrdSec"] = profRefrdSec,
        };

        if (isItr6)
            busIncOth["PLUs44sChapXIIGUs115B"] = 0;

        busIncOth["TotalProfitFrmActCvrd"] = 0;
        if (isItr6)
            busIncOth["ProfitFrmEligBus10TIA"] = 0;

        busIncOth["ProfitFrmActCvrd"] = new JsonObject
        {
            ["ProfitFrmActCvrdUndrRule7"] = 0,
            ["ProfitFrmActCvrdUndrRule7A"] = 0,
            ["ProfitFrmActCvrdUndrRule7B1"] = 0,
            ["ProfitFrmActCvrdUndrRule7B1A"] = 0,
            ["ProfitFrmActCvrdUndrRule8"] = 0,
        };
        busIncOth["IncCredPL"] = new JsonObject
        {
            ["FirmShareInc"] = 0,
            ["AOPBOISharInc"] = 0,
            ["OthExempInc"] = 0,
            ["TotExempInc"] = 0,
        };
        busIncOth["BalancePLOthThanSpecBus"] = balanceAfterExcl;
        busIncOth["ExpDebToPLOthHeadDtls"] = new JsonObject
        {
            ["HouseProperty"] = 0,
            ["CapitalGains"] = 0,
            ["OtherSources"] = 0,
            ["UnderSec115BBF"] = 0,
            ["UnderSec115BBG"] = 0,
        };
        busIncOth["ExpDebToPLExemptInc"] = 0;
        busIncOth["ExpDebToPLExemptIncDisAllwUs14A"] = 0;
        busIncOth["TotExpDebPL"] = 0;
        busIncOth["AdjustedPLOthThanSpecBus"] = balanceAfterExcl;
        busIncOth["DepreciationDebPLCosAct"] = depBooks;
        busIncOth["DepreciationAllowITAct32"] = new JsonObject
        {
            ["DepreciationAllowUs32_1_ii"] = depIt,
            ["DepreciationAllowUs32_1_i"] = 0,
            ["TotDeprAllowITAct"] = depIt,
        };
        busIncOth["AdjustPLAfterDeprOthSpecInc"] = balanceAfterExcl + depBooks;
        busIncOth["AmtDebPLDisallowUs36"] = d36;
        busIncOth["AmtDebPLDisallowUs37"] = d37;
        busIncOth["AmtDebPLDisallowUs40"] = d40;
        busIncOth["AmtDebPLDisallowUs40A"] = d40a;
        busIncOth["AmtDebPLDisallowUs43B"] = d43b;
        busIncOth["InterestDisAllowUs23SMEAct"] = d43bh;
        busIncOth["DeemIncUs41"] = 0;
        busIncOth["DeemIncUs3380HHD80IA"] = 0;
        busIncOth["DeemIncUs43CA"] = 0;
        busIncOth[isItr6 ? "OthItemDisallowUs28To44DA" : "OthItemDisallowUs28To44DB"] = othDisallow;
        busIncOth["AnyOthIncNotInclInExpDisallowPL"] = 0;
        if (!isItr6)
        {
            busIncOth["SalaryExpDisallowPL"] = 0;
            busIncOth["BonusExpDisallowPL"] = 0;
        }
        busIncOth["CommissionExpDisallowPL"] = 0;
        busIncOth["InterestExpDisallowPL"] = 0;
        busIncOth["OthersExpDisallowPL"] = othDisallow;
        busIncOth["IncProfDecLossAccICDSAdj"] = 0;
        busIncOth["TotAfterAddToPLDeprOthSpecInc"] = balanceAfterExcl + totAdd;
        busIncOth["DeductUs32_1_iii"] = 0;
        if (isItr6) busIncOth["Amt32AC"] = 0;
        busIncOth["DebPLUs35ExcessAmt"] = 0;
        busIncOth["AmtDisallUs40NowAllow"] = 0;
        busIncOth["AmtDisallUs43BNowAllow"] = 0;
        busIncOth["AnyOthAmtAllDeduct"] = ToRupees(input.AllowableDeductionsUs32Etc);
        busIncOth["DecProfIncLossAccICDSAdj"] = 0;
        busIncOth["TotDeductionAmts"] = allowDed;
        busIncOth["PLAftAdjDedBusOthThanSpec"] = pgbpAfterDed;
        busIncOth["DeemedProfitBusUs"] = deemedProfit;
        busIncOth["NetPLAftAdjBusOthThanSpec"] = pgbpAfterDed;
        busIncOth["NetPLBusOthThanSpec7A7B7C"] = pgbpAfterDed;
        busIncOth["ChrgblIncUndrRule7"] = 0;
        busIncOth["DeemedChrgblIncUndrRule7A"] = 0;
        busIncOth["DeemedChrgblIncUndrRule7B1"] = 0;
        busIncOth["DeemedChrgblIncUndrRule7B1A"] = 0;
        busIncOth["DeemedChrgblIncUndrRule8"] = 0;
        busIncOth["IncomeOtherThanRule"] = pgbpAfterDed;
        busIncOth["BalIncDeemedFrmAgri"] = 0;

        var specBus = new JsonObject
        {
            ["NetPLFrmSpecBus"] = 0,
            [isItr6 ? "AdditionUs28to44DA" : "AdditionUs28to44DB"] = 0,
            [isItr6 ? "DeductUs28to44DA" : "DeductUs28to44DB"] = 0,
            ["AdjustedPLFrmSpecuBus"] = 0,
        };

        var incSpecBus = new JsonObject
        {
            ["NetPLFrmSpecifiedBus"] = 0,
            [isItr6 ? "AddSec28to44DA" : "AddSec28to44DB"] = 0,
            [isItr6 ? "DedSec28to44DAOTDedSec35AD" : "DedSec28to44DBOTDedSec35AD"] = ToRupees(input.DeductionSec35AD),
            ["ProfitLossSpecifiedBusiness"] = -ToSignedRupees(input.DeductionSec35AD),
        };
        if (isItr6) incSpecBus["DedSec35AD1"] = 0;
        incSpecBus["ProfitLossSpecifiedBusFinal"] = -ToSignedRupees(input.DeductionSec35AD);

        return new JsonObject
        {
            ["BusinessIncOthThanSpec"] = busIncOth,
            ["SpecBusinessInc"] = specBus,
            ["IncSpecifiedBusiness"] = incSpecBus,
            ["IncChrgUnHdProftGain"] = pgbpAfterDed,
            ["BusSetoffCurrYr"] = new JsonObject
            {
                ["LossSetOffOnBusLoss"] = bfLossSetOff,
                ["TotLossSetOffOnBus"] = bfLossSetOff,
                ["LossRemainSetOffOnBus"] = netPgbp,
            },
        };
    }

    public static (JsonObject Dpm, JsonObject Doa, JsonObject Dep) BuildDepreciationSchedules(
        CalculateBusinessTaxRequest input,
        BusinessTaxComputationResultDto biz,
        bool isItr3 = false)
    {
        var blocks = input.DepreciationBlocks ?? [];
        long r15 = 0, r30 = 0, r40 = 0, r45 = 0;
        long b5 = 0, b10 = 0, b40 = 0, f10 = 0, i25 = 0;

        JsonObject MakeDetail(IEnumerable<DepreciationBlockInput> items, decimal ratePct, bool isRate45 = false, bool isDoa = false)
        {
            var list = items.ToList();
            long wdvOpen = ToRupees(list.Sum(x => x.OpeningWdv));
            long addGt = ToRupees(list.Sum(x => x.AdditionsMoreThan180Days));
            long addLt = ToRupees(list.Sum(x => x.AdditionsLessThan180Days));
            long del = ToRupees(list.Sum(x => x.Deletions));
            long fullBase = Math.Max(0L, wdvOpen + addGt - del);
            long addln = (isRate45 || isDoa) ? 0L : ToRupees(list.Sum(x => x.AdditionalDepreciation));
            long fullDep = ToRupees(fullBase * (ratePct / 100m));
            long halfDep = isRate45 ? 0L : ToRupees(addLt * (ratePct / 200m));
            long totDep = list.Sum(x => x.ExplicitTaxDepreciation) is decimal exp && exp > 0m
                ? ToRupees(exp)
                : fullDep + halfDep + addln;
            long wdvClose = Math.Max(0L, fullBase + (isRate45 ? 0L : addLt) - totDep);

            var detail = new JsonObject
            {
                ["WDVFirstDay"] = wdvOpen,
            };
            if (!isRate45) detail["AdditionsGrThan180Days"] = addGt;
            detail["RealizationTotalPeriod"] = del;
            detail["FullRateDeprAmt"] = fullBase;
            if (!isRate45)
            {
                detail["AdditionsLessThan180Days"] = addLt;
                detail[isItr3 ? "RealizationPeriodLessThan180days" : "RealizationPeriodDuringYear"] = 0;
                detail["HalfRateDeprAmt"] = addLt;
            }
            detail["DepreciationAtFullRate"] = fullDep;
            if (!isRate45) detail["DepreciationAtHalfRate"] = halfDep;
            if (!isRate45 && !isDoa)
            {
                detail["AddlnDeprOnGT180DayAdditions"] = addln;
                if (isItr3)
                {
                    detail["AddlnDeprOnLessThan180DayAdditions"] = 0;
                    detail["AddlnDeprOnAssetLessThan180Days"] = 0;
                }
                else
                {
                    detail["AddlnDeprDuringYearAdditions"] = 0;
                    detail["AddlnDeprOnLessThan180DayAdditions"] = 0;
                }
            }
            detail["TotalDepreciation"] = totDep;
            detail["DepDisAllowUs38_2"] = 0;
            detail["NetAggregateDepreciation"] = totDep;
            detail["ProportionateAggDepreciation"] = totDep;
            detail["ExpdrOnTrforSaleAsset"] = 0;
            detail["CapGainUs50"] = 0;
            detail["WDVLastDay"] = wdvClose;
            return detail;
        }

        var pm15D = MakeDetail(blocks.Where(b => b.BlockKey == "PM15"), 15m);
        var pm30D = MakeDetail(blocks.Where(b => b.BlockKey == "PM30"), 30m);
        var pm40D = MakeDetail(blocks.Where(b => b.BlockKey == "PM40"), 40m);
        var pm45D = MakeDetail(blocks.Where(b => b.BlockKey == "PM45"), 45m, isRate45: true);

        r15 = pm15D["TotalDepreciation"]!.GetValue<long>();
        r30 = pm30D["TotalDepreciation"]!.GetValue<long>();
        r40 = pm40D["TotalDepreciation"]!.GetValue<long>();
        r45 = pm45D["TotalDepreciation"]!.GetValue<long>();

        var b5D = MakeDetail(blocks.Where(b => b.BlockKey == "Bldg5"), 5m, isDoa: true);
        var b10D = MakeDetail(blocks.Where(b => b.BlockKey == "Bldg10"), 10m, isDoa: true);
        var b40D = MakeDetail(blocks.Where(b => b.BlockKey == "Bldg40"), 40m, isDoa: true);
        var f10D = MakeDetail(blocks.Where(b => b.BlockKey == "Furn10"), 10m, isDoa: true);
        var i25D = MakeDetail(blocks.Where(b => b.BlockKey == "Intang25"), 25m, isDoa: true);

        b5 = b5D["TotalDepreciation"]!.GetValue<long>();
        b10 = b10D["TotalDepreciation"]!.GetValue<long>();
        b40 = b40D["TotalDepreciation"]!.GetValue<long>();
        f10 = f10D["TotalDepreciation"]!.GetValue<long>();
        i25 = i25D["TotalDepreciation"]!.GetValue<long>();

        long totalDep = Math.Max(r15 + r30 + r40 + r45 + b5 + b10 + b40 + f10 + i25, ToRupees(biz.IncomeTaxDepreciationSec32));

        var dpm = new JsonObject
        {
            ["PlantMachinery"] = new JsonObject
            {
                ["Rate15"] = new JsonObject { ["DepreciationDetail"] = pm15D },
                ["Rate30"] = new JsonObject { ["DepreciationDetail"] = pm30D },
                ["Rate40"] = new JsonObject { ["DepreciationDetail"] = pm40D },
                ["Rate45"] = new JsonObject { ["DepreciationDetail"] = pm45D },
            }
        };

        var doa = new JsonObject
        {
            ["Land"] = new JsonObject
            {
                ["DepreciationDetail"] = new JsonObject { ["WDVFirstDay"] = 0, ["WDVLastDay"] = 0 }
            },
            ["Building"] = new JsonObject
            {
                ["Rate5"] = new JsonObject { ["DepreciationDetail"] = b5D },
                ["Rate10"] = new JsonObject { ["DepreciationDetail"] = b10D },
                ["Rate40"] = new JsonObject { ["DepreciationDetail"] = b40D },
            },
            ["FurnitureFittings"] = new JsonObject { ["Rate10"] = new JsonObject { ["DepreciationDetail"] = f10D } },
            ["IntangibleAssets"] = new JsonObject { ["Rate25"] = new JsonObject { ["DepreciationDetail"] = i25D } },
        };

        var dep = new JsonObject
        {
            ["SummaryFromDeprSch"] = new JsonObject
            {
                ["PlantMachinerySummary"] = new JsonObject
                {
                    ["DeprBlockTot15Percent"] = r15,
                    ["DeprBlockTot30Percent"] = r30,
                    ["DeprBlockTot40Percent"] = r40,
                    ["DeprBlockTot45Percent"] = r45,
                    ["TotPlntMach"] = r15 + r30 + r40 + r45,
                },
                ["BuildingSummary"] = new JsonObject
                {
                    ["DeprBlockTot5Percent"] = b5,
                    ["DeprBlockTot10Percent"] = b10,
                    ["DeprBlockTot40Percent"] = b40,
                    ["TotBuildng"] = b5 + b10 + b40,
                },
                ["FurnitureSummary"] = f10,
                ["IntangibleAssetSummary"] = i25,
                ["ShipsSummary"] = 0,
                ["TotalDepreciation"] = totalDep,
            }
        };

        return (dpm, doa, dep);
    }

    public static JsonObject BuildScheduleVia(CalculateBusinessTaxRequest input, BusinessTaxComputationResultDto biz)
    {
        long partB = ToRupees(input.Deduction80G + input.OtherChapViaDeductions);
        long partC = ToRupees(input.Deduction80Jjaa + input.Deduction80M);
        long allowedTot = ToRupees(biz.ChapterViaAllowed);
        long usrTot = partB + partC;
        bool isConcessional = biz.SelectedRegimeCode is BusinessTaxEngine.RegimeCorp115Baa or BusinessTaxEngine.RegimeCorp115Bab;
        long allowedPartB = isConcessional ? 0 : Math.Min(partB, allowedTot);
        long allowedPartC = Math.Max(0, allowedTot - allowedPartB);

        return new JsonObject
        {
            ["UsrDeductUndChapVIA"] = new JsonObject
            {
                ["TotPartBchapterVIA"] = partB,
                ["TotPartCchapterVIA"] = partC,
                ["TotalChapVIADeductions"] = usrTot,
            },
            ["DeductUndChapVIA"] = new JsonObject
            {
                ["TotPartBchapterVIA"] = allowedPartB,
                ["TotPartCchapterVIA"] = allowedPartC,
                ["TotalChapVIADeductions"] = allowedTot,
            }
        };
    }

    public static JsonObject? BuildScheduleSi(CalculateBusinessTaxRequest input, bool isItr6)
    {
        var list = new JsonArray();
        long stcg111a = ToRupees(input.Stcg111A);
        long ltcg112a = ToRupees(Math.Max(0m, input.Ltcg112A - 125_000m));
        long ltcg112 = ToRupees(input.Ltcg112);

        if (stcg111a > 0)
        {
            list.Add(new JsonObject
            {
                ["SecCode"] = isItr6 ? "1A" : "1",
                ["SplRatePercent"] = Stcg111ARatePercent,
                ["SplRateInc"] = stcg111a,
                ["SplRateIncTax"] = ToRupees(stcg111a * 0.20m),
            });
        }
        if (ltcg112a > 0)
        {
            list.Add(new JsonObject
            {
                ["SecCode"] = "2A",
                ["SplRatePercent"] = LtcgRatePercent,
                ["SplRateInc"] = ltcg112a,
                ["SplRateIncTax"] = ToRupees(ltcg112a * 0.125m),
            });
        }
        if (ltcg112 > 0)
        {
            list.Add(new JsonObject
            {
                ["SecCode"] = "22",
                ["SplRatePercent"] = LtcgRatePercent,
                ["SplRateInc"] = ltcg112,
                ["SplRateIncTax"] = ToRupees(ltcg112 * 0.125m),
            });
        }

        if (list.Count == 0) return null;

        long totInc = stcg111a + ltcg112a + ltcg112;
        long totTax = list.Sum(x => x!["SplRateIncTax"]!.GetValue<long>());
        return new JsonObject
        {
            ["SplCodeRateTax"] = list,
            ["TotSplRateInc"] = totInc,
            ["TotSplRateIncTax"] = totTax,
        };
    }

    public static JsonObject BuildPartBTi(CalculateBusinessTaxRequest input, BusinessTaxComputationResultDto biz, bool isItr6)
    {
        long pgbp = ToRupees(Math.Max(0m, biz.NetPgbpIncome));
        long hp = ToSignedRupees(input.HousePropertyIncome);
        long stcg111a = ToRupees(input.Stcg111A);
        long stcgNorm = ToRupees(input.StcgNormal);
        long totStcg = stcg111a + stcgNorm;
        long ltcg112a = ToRupees(input.Ltcg112A);
        long ltcg112 = ToRupees(input.Ltcg112);
        long totLtcg = ltcg112a + ltcg112;
        long totCg = totStcg + totLtcg;
        long os = ToRupees(input.OtherSourcesIncome);
        long gti = ToRupees(biz.GrossTotalIncome);
        long splRateInc = stcg111a + ltcg112a + ltcg112;
        long taxable = ToRupees(biz.TotalTaxableIncome);

        var profBusGain = new JsonObject
        {
            ["ProfGainNoSpecBus"] = pgbp,
        };
        if (isItr6) profBusGain["IncmForeignCompRule10TIA"] = 0;
        profBusGain["ProfGainSpecBus"] = 0;
        profBusGain["ProfGainSpecifiedBus"] = 0;
        profBusGain["IncChrgblTaxSplRate"] = 0;
        profBusGain["TotProfBusGain"] = pgbp;

        var obj = new JsonObject
        {
            ["IncomeFromHP"] = hp,
            ["ProfBusGain"] = profBusGain,
            ["CapGain"] = new JsonObject
            {
                ["ShortTerm"] = new JsonObject
                {
                    ["ShortTerm20Per"] = stcg111a,
                    ["ShortTerm30Per"] = 0,
                    ["ShortTermAppRate"] = stcgNorm,
                    ["ShortTermSplRateDTAA"] = 0,
                    ["TotalShortTerm"] = totStcg,
                },
                ["LongTerm"] = new JsonObject
                {
                    ["LongTerm12_5Per"] = totLtcg,
                    ["LongTermSplRateDTAA"] = 0,
                    ["TotalLongTerm"] = totLtcg,
                },
                ["TotalCapGains"] = totCg,
                ["ShortTermLongTermTotal"] = totCg,
                ["CapGains30Per115BBH"] = 0,
            },
            ["IncFromOS"] = new JsonObject
            {
                ["OtherSrcThanOwnRaceHorse"] = os,
                ["IncChargblSplRate"] = 0,
                ["FromOwnRaceHorse"] = 0,
                ["TotIncFromOS"] = os,
            },
            ["TotalTI"] = gti,
            ["CurrentYearLoss"] = 0,
            ["BalanceAfterSetoffLosses"] = gti,
            ["BroughtFwdLossesSetoff"] = ToRupees(biz.BroughtForwardLossSetOff),
            ["GrossTotalIncome"] = gti,
            ["IncChargeTaxSplRate111A112"] = splRateInc,
            ["DeductionsUndSchVIADtl"] = new JsonObject
            {
                ["PartBchapterVIA"] = ToRupees(input.Deduction80G + input.OtherChapViaDeductions),
                ["PartCchapterVIA"] = ToRupees(input.Deduction80Jjaa + input.Deduction80M),
                ["TotDeductUndSchVIA"] = ToRupees(biz.ChapterViaAllowed),
            },
            ["DeductionsUnder10Aor10AA"] = ToRupees(input.DeductionSec10AA),
            ["TotalIncome"] = taxable,
            ["IncChargeableTaxSplRates"] = splRateInc,
        };

        if (isItr6)
            obj["IncChargeableTaxNormalRates"] = Math.Max(0L, taxable - splRateInc);

        obj["NetAgricultureIncomeOrOtherIncomeForRate"] = 0;
        if (!isItr6) obj["AggregateIncome"] = taxable;
        obj["LossesOfCurrentYearCarriedFwd"] = 0;

        return obj;
    }

    public static JsonObject BuildTaxPaid(CalculateBusinessTaxRequest input, BusinessRegimeSummaryDto active)
    {
        long adv = ToRupees(input.AdvanceTaxPaid);
        long tds = ToRupees(input.TdsCredit);
        long tcs = ToRupees(input.TcsCredit);
        long sat = ToRupees(input.SelfAssessmentTaxPaid);
        long totPaid = adv + tds + tcs + sat;
        long balPayable = Math.Max(0L, ToRupees(active.TotalTaxLiability) - totPaid);

        return new JsonObject
        {
            ["TaxesPaid"] = new JsonObject
            {
                ["AdvanceTax"] = adv,
                ["TDS"] = tds,
                ["TCS"] = tcs,
                ["SelfAssessmentTax"] = sat,
                ["TotalTaxesPaid"] = totPaid,
            },
            ["BalTaxPayable"] = balPayable,
        };
    }

    public static JsonObject BuildRefund(BusinessRegimeSummaryDto active, ExportItrJsonRequest? req)
    {
        long refundDue = active.NetTaxPayableOrRefund < 0m ? ToRupees(-active.NetTaxPayableOrRefund) : 0L;
        var bankDtls = new JsonObject();
        if (!string.IsNullOrWhiteSpace(req?.IfscCode) && !string.IsNullOrWhiteSpace(req?.BankAccountNo))
        {
            bankDtls["BankDtlsFlag"] = "Y";
            bankDtls["AddtnlBankDetails"] = new JsonArray
            {
                new JsonObject
                {
                    ["IFSCCode"] = req.IfscCode.Trim().ToUpperInvariant(),
                    ["BankName"] = Trunc(req.BankName, 125, "State Bank of India"),
                    ["BankAccountNo"] = Trunc(req.BankAccountNo, 20),
                    ["AccountType"] = string.IsNullOrWhiteSpace(req.BankAccountType) ? "CA" : req.BankAccountType.Trim().ToUpperInvariant(),
                    ["UseForRefund"] = "true",
                }
            };
        }
        else
        {
            bankDtls["BankDtlsFlag"] = "N";
        }

        return new JsonObject
        {
            ["RefundDue"] = refundDue,
            ["BankAccountDtls"] = bankDtls,
        };
    }

    public static JsonObject BuildScheduleIt(CalculateBusinessTaxRequest input)
    {
        var arr = new JsonArray();
        long tot = 0;
        if (input.AdvanceTaxChallans is { Count: > 0 })
        {
            foreach (var c in input.AdvanceTaxChallans.Where(x => x.Amount > 0m))
            {
                long amt = ToRupees(c.Amount);
                long srl = long.TryParse(DigitsOnly(c.ChallanSerialNo, 10), out var v) && v > 0 ? v : 1;
                string bsr = DigitsOnly(c.BsrCode, 7).PadLeft(7, '0');
                arr.Add(new JsonObject
                {
                    ["BSRCode"] = bsr,
                    ["DateDep"] = c.DepositDate,
                    ["SrlNoOfChaln"] = srl,
                    ["Amt"] = amt,
                });
                tot += amt;
            }
        }
        if (arr.Count == 0 && (input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid) > 0m)
        {
            tot = ToRupees(input.AdvanceTaxPaid + input.SelfAssessmentTaxPaid);
            arr.Add(new JsonObject
            {
                ["BSRCode"] = "0002430",
                ["DateDep"] = "2026-03-15",
                ["SrlNoOfChaln"] = 10001,
                ["Amt"] = tot,
            });
        }

        var obj = new JsonObject { ["TotalTaxPayments"] = tot };
        if (arr.Count > 0) obj["TaxPayment"] = arr;
        return obj;
    }

    public static JsonObject? BuildScheduleTds2(CalculateBusinessTaxRequest input, List<IncomeTaxJsonExportIssue> issues, string schedulePath)
    {
        if (input.TdsCredit <= 0m && (input.TdsCreditEntries is null || input.TdsCreditEntries.Count == 0))
            return null;

        var arr = new JsonArray();
        long tot = 0;
        if (input.TdsCreditEntries is { Count: > 0 })
        {
            foreach (var t in input.TdsCreditEntries.Where(x => x.TdsClaimed > 0m))
            {
                string tan = (t.TanOfDeductor ?? "").Trim().ToUpperInvariant();
                if (!TanRegex().IsMatch(tan))
                {
                    issues.Add(new("warning", $"{schedulePath}.TANOfDeductor", "CBDT-TAN", $"Deductor TAN '{t.TanOfDeductor}' invalid/missing; defaulted for draft preview."));
                    tan = "MUMT01234A";
                }
                long amt = ToRupees(t.TdsClaimed);
                arr.Add(new JsonObject
                {
                    ["TDSCreditName"] = "S",
                    ["TANOfDeductor"] = tan,
                    ["TDSSection"] = string.IsNullOrWhiteSpace(t.TdsSection) ? "94J-B" : t.TdsSection.Trim(),
                    ["TaxDeductCreditDtls"] = new JsonObject { ["TaxClaimedOwnHands"] = amt },
                    ["AmtCarriedFwd"] = 0,
                });
                tot += amt;
            }
        }
        if (arr.Count == 0 && input.TdsCredit > 0m)
        {
            tot = ToRupees(input.TdsCredit);
            issues.Add(new("warning", schedulePath, "CBDT-TDS2", "Aggregate TDS credit populated with placeholder deductor TAN 'MUMT01234A'; update Form 26AS deductor TANs before portal upload."));
            arr.Add(new JsonObject
            {
                ["TDSCreditName"] = "S",
                ["TANOfDeductor"] = "MUMT01234A",
                ["TDSSection"] = "94J-B",
                ["TaxDeductCreditDtls"] = new JsonObject { ["TaxClaimedOwnHands"] = tot },
                ["AmtCarriedFwd"] = 0,
            });
        }

        return new JsonObject
        {
            ["TDSOthThanSalaryDtls"] = arr,
            ["TotalTDSonOthThanSals"] = tot,
        };
    }

    public static JsonObject BuildBusinessVerification(
        Client client,
        ExportItrJsonRequest? req,
        bool isItr6,
        List<IncomeTaxJsonExportIssue> issues,
        string rootName)
    {
        string defaultCap = isItr6 ? "MD" : "MP";
        string cap = string.IsNullOrWhiteSpace(req?.Capacity) ? defaultCap : req.Capacity.Trim().ToUpperInvariant();

        string? candPan = req?.VerificationPan?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(candPan) || !IndividualPanRegex().IsMatch(candPan))
        {
            issues.Add(new("warning", $"{rootName}.Verification.Declaration.AssesseeVerPAN", "CBDT-SIGNER-PAN",
                "CBDT requires the Authorized Signatory / Managing Partner / MD's personal (4th-char 'P') PAN. Placeholder 'ABCPA1234A' used."));
            candPan = "ABCPA1234A";
        }

        string verName = Trunc(req?.VerificationName, 125, client.LegalName ?? client.Name);
        return new JsonObject
        {
            ["Declaration"] = new JsonObject
            {
                ["AssesseeVerName"] = verName,
                ["FatherName"] = Trunc(req?.FatherName, 125, "NA"),
                ["AssesseeVerPAN"] = candPan,
                ["Capacity"] = cap,
                ["Place"] = Trunc(req?.Place ?? req?.City, 50, "Kochi"),
                ["Date"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            }
        };
    }
}
