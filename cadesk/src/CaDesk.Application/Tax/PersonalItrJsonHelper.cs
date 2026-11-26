using System.Text.Json.Nodes;
using CaDesk.Domain.Clients;
using static CaDesk.Application.Tax.BusinessItrJsonHelper;
using static CaDesk.Application.Tax.CbdtJsonExportHelper;

namespace CaDesk.Application.Tax;

/// <summary>
/// Shared CBDT AY 2026-27 schedule builders shared between ITR-2 and ITR-3
/// (PersonalInfo, ScheduleS, ScheduleHP, ScheduleCGFor23, Schedule112A, ScheduleVDA,
/// ScheduleOS, ScheduleCYLA, ScheduleBFLA, ScheduleVIA, ScheduleSI, ScheduleFA, ScheduleAL, ScheduleTDS1).
/// </summary>
internal static class PersonalItrJsonHelper
{
    public static JsonObject BuildPersonalInfo(
        Client client,
        PersonalExtendedTaxResultDto ext,
        ExportItrJsonRequest? req,
        List<IncomeTaxJsonExportIssue> issues,
        string panPath)
    {
        string pan = NormalizePan(ExtractTaxId(client, "PAN"), issues, panPath);
        string name = Trunc(client.LegalName ?? client.Name, 125, "Assessee");
        string dob = !string.IsNullOrWhiteSpace(req?.Dob) ? req.Dob! : "1985-04-01";

        return new JsonObject
        {
            ["AssesseeName"] = new JsonObject { ["SurNameOrOrgName"] = name },
            ["PAN"] = pan,
            ["Address"] = BuildBusinessAddress(client, req),
            ["SecondaryAdd"] = "N",
            ["DOB"] = dob,
            ["Status"] = ext.StatusType == "H" ? "H" : "I",
        };
    }

    public static JsonObject ZeroDateRange(bool useUp16Of6Key = false) => new()
    {
        ["DateRange"] = new JsonObject
        {
            ["Upto15Of6"] = 0,
            [useUp16Of6Key ? "Up16Of6To15Of9" : "Upto15Of9"] = 0,
            ["Up16Of9To15Of12"] = 0,
            ["Up16Of12To15Of3"] = 0,
            ["Up16Of3To31Of3"] = 0,
        }
    };

    public static JsonObject SingleBucketDateRange(long amount, bool useUp16Of6Key = false) => new()
    {
        ["DateRange"] = new JsonObject
        {
            ["Upto15Of6"] = 0,
            [useUp16Of6Key ? "Up16Of6To15Of9" : "Upto15Of9"] = 0,
            ["Up16Of9To15Of12"] = 0,
            ["Up16Of12To15Of3"] = 0,
            ["Up16Of3To31Of3"] = Math.Max(0L, amount),
        }
    };

    public static JsonObject? BuildScheduleS(CalculatePersonalExtendedTaxRequest input, PersonalRegimeBreakdownDto active, Client client, ExportItrJsonRequest? req)
    {
        if (input.GrossSalary <= 0m && (input.Employers is null || input.Employers.Count == 0))
            return null;

        var empArr = new JsonArray();
        var employers = input.Employers is { Count: > 0 }
            ? input.Employers
            : [new SalaryEmployerInput("Primary Employer", null, "OTH", input.GrossSalary, input.TdsSalary)];

        foreach (var emp in employers)
        {
            long sal = ToRupees(emp.GrossSalary > 0m ? emp.GrossSalary : input.GrossSalary);
            empArr.Add(new JsonObject
            {
                ["NameOfEmployer"] = Trunc(emp.EmployerName, 125, "Employer"),
                ["NatureOfEmployment"] = string.IsNullOrWhiteSpace(emp.NatureOfEmployment) ? "OTH" : emp.NatureOfEmployment.Trim().ToUpperInvariant(),
                ["AddressDetail"] = new JsonObject
                {
                    ["AddrDetail"] = Trunc(emp.Address, 200, req?.LocalityOrArea ?? "Business Address"),
                    ["CityOrTownOrDistrict"] = Trunc(emp.City, 50, req?.City ?? "Kochi"),
                    ["StateCode"] = ResolveStateCode(emp.StateCode ?? req?.StateCode, client),
                    ["PinCode"] = ResolvePinInt(emp.PinCode ?? req?.PinCode),
                },
                ["Salarys"] = new JsonObject
                {
                    ["GrossSalary"] = sal,
                    ["Salary"] = sal,
                    ["ValueOfPerquisites"] = 0,
                    ["ProfitsinLieuOfSalary"] = 0,
                    ["IncomeNotified89A"] = 0,
                    ["IncomeNotifiedOther89A"] = 0,
                }
            });
        }

        long grossSal = ToRupees(active.GrossSalary);
        long dedUs16 = ToRupees(active.SalaryDeductions);
        long std16ia = Math.Min(grossSal, active.Regime == "new" ? 75_000L : 50_000L);
        long pt16iii = active.Regime == "old" ? ToRupees(Math.Min(2_500m, input.ProfessionalTax)) : 0L;
        long exemptUs10 = Math.Max(0L, dedUs16 - std16ia - pt16iii);
        long netSalAfter10 = Math.Max(0L, grossSal - exemptUs10);
        long actualDed16 = std16ia + pt16iii;

        return new JsonObject
        {
            ["Salaries"] = empArr,
            ["TotalGrossSalary"] = grossSal,
            ["AllwncExtentExemptUs10"] = exemptUs10,
            ["NetSalary"] = netSalAfter10,
            ["DeductionUS16"] = actualDed16,
            ["DeductionUnderSection16ia"] = std16ia,
            ["EntertainmntalwncUs16ii"] = 0,
            ["ProfessionalTaxUs16iii"] = pt16iii,
            ["TotIncUnderHeadSalaries"] = ToRupees(active.NetSalary),
        };
    }

    public static JsonObject? BuildScheduleHp(CalculatePersonalExtendedTaxRequest input, PersonalRegimeBreakdownDto active, Client client, ExportItrJsonRequest? req)
    {
        bool hasHpInput = input.HouseProperties is { Count: > 0 } || input.AnnualRentReceived > 0m || input.HousingLoanInterest > 0m;
        if (!hasHpInput) return null;

        var props = input.HouseProperties is { Count: > 0 }
            ? input.HouseProperties
            : [new HousePropertyInput(
                req?.ResidenceNo ?? "Residential Property", req?.City, req?.StateCode, req?.PinCode,
                input.IsSelfOccupied ? "S" : "L", input.AnnualRentReceived, input.MunicipalTaxesPaid, input.HousingLoanInterest)];

        var arr = new JsonArray();
        int idx = 1;
        foreach (var hp in props)
        {
            decimal share = Math.Clamp(hp.AssesseeSharePercent, 1m, 100m) / 100m;
            long rent = ToRupees(hp.UsageType == "S" ? 0m : hp.AnnualRent * share);
            long tax = ToRupees(hp.UsageType == "S" ? 0m : hp.MunicipalTaxes * share);
            long balAlv = Math.Max(0L, rent - tax);
            long pct30 = ToRupees(balAlv * 0.30m);
            long loanInt = ToRupees(hp.LoanInterest * share);
            long totDed = pct30 + loanInt;
            long hpInc = hp.UsageType == "S"
                ? (active.Regime == "old" ? -Math.Min(200_000L, loanInt) : 0L)
                : (balAlv - totDed);

            arr.Add(new JsonObject
            {
                ["HPSNo"] = idx++,
                ["AddressDetailWithZipCode"] = BuildAddressDetailWithZipCode(hp.Address, hp.City ?? req?.City, hp.StateCode ?? req?.StateCode, hp.PinCode ?? req?.PinCode, client),
                ["PropertyOwner"] = "SE",
                ["PropCoOwnedFlg"] = hp.IsCoOwned ? "YES" : "NO",
                ["AsseseeShareProperty"] = Math.Round(hp.AssesseeSharePercent, 2),
                ["ifLetOut"] = string.IsNullOrWhiteSpace(hp.UsageType) ? "S" : hp.UsageType.Trim().ToUpperInvariant(),
                ["Rentdetails"] = BuildRentDetails(rent, tax, balAlv, pct30, loanInt, totDed, hpInc)
            });
        }

        return new JsonObject
        {
            ["PropertyDetails"] = arr,
            ["TotalIncomeChargeableUnHP"] = ToSignedRupees(active.HousePropertyIncome),
        };
    }

    private static JsonObject BuildRentDetails(long rent, long tax, long balAlv, long pct30, long loanInt, long totDed, long hpInc)
    {
        var obj = new JsonObject
        {
            ["AnnualLetableValue"] = rent,
            ["LocalTaxes"] = tax,
            ["TotalUnrealizedAndTax"] = tax,
            ["BalanceALV"] = balAlv,
            ["AnnualOfPropOwned"] = balAlv,
            ["ThirtyPercentOfBalance"] = pct30,
            ["IntOnBorwCap"] = loanInt,
        };
        if (loanInt > 0)
        {
            obj["Section24B"] = new JsonObject
            {
                ["Section24BDtls"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["LoanTknFrom"] = "B",
                        ["BankOrInstnName"] = "Housing Finance Bank",
                        ["LoanAccNoOfBankOrInstnRefNo"] = "HL000001",
                        ["DateofLoan"] = "2021-04-01",
                        ["TotalLoanAmt"] = loanInt * 10,
                        ["LoanOutstndngAmt"] = loanInt * 8,
                        ["InterestUs24B"] = loanInt,
                    }
                },
                ["TotalInterestUs24B"] = loanInt,
            };
        }
        obj["TotalDeduct"] = totDed;
        obj["IncomeOfHP"] = hpInc;
        return obj;
    }

    public static (JsonObject? ScheduleCg, JsonObject? Schedule112A, JsonObject? ScheduleVda) BuildCapitalGainsAndVda(
        CalculatePersonalExtendedTaxRequest input,
        PersonalRegimeBreakdownDto active,
        bool isItr3)
    {
        long st111a = ToRupees(active.Stcg111A);
        long stNorm = ToRupees(active.StcgNormal);
        long totSt = st111a + stNorm;
        long lt112a = ToRupees(active.Ltcg112A);
        long lt112 = ToRupees(active.Ltcg112);
        long totLt = lt112a + lt112;
        long vda = ToRupees(active.VdaIncome115BBH);

        if (totSt == 0 && totLt == 0 && vda == 0)
            return (null, null, null);

        JsonObject DeductSec48Obj(long cost) => new()
        {
            ["AquisitCost"] = cost,
            ["ImproveCost"] = 0,
            ["ExpOnTrans"] = 0,
            ["TotalDedn"] = cost,
        };

        var stcgObj = new JsonObject();
        if (isItr3)
        {
            stcgObj["SlumpSaleInStcg"] = new JsonObject
            {
                ["FMV11UAEii"] = 0, ["FMV11UAEiii"] = 0, ["FullConsideration"] = 0, ["NetWorthOfDivision"] = 0, ["CapgainonAssets"] = 0,
            };
        }
        if (st111a > 0)
        {
            long sale = input.Stcg111ASaleValue > 0m ? ToRupees(input.Stcg111ASaleValue) : st111a;
            long cost = Math.Max(0L, sale - st111a);
            stcgObj["EquityMFonSTT"] = new JsonArray
            {
                new JsonObject
                {
                    ["MFSectionCode"] = "1A",
                    ["EquityMFonSTTDtls"] = new JsonObject
                    {
                        ["FullConsideration"] = sale,
                        ["DeductSec48"] = DeductSec48Obj(cost),
                        ["BalanceCG"] = st111a,
                        ["LossSec94of7Or94of8"] = 0,
                        ["CapgainonAssets"] = st111a,
                    }
                }
            };
        }
        stcgObj["NRITransacSec48Dtl"] = new JsonObject { ["NRItaxSTTPaid"] = 0, ["NRItaxSTTNotPaid"] = 0 };
        stcgObj["NRISecur115AD"] = new JsonObject
        {
            ["FullValueConsdRecvUnqshr"] = 0, ["FairMrktValueUnqshr"] = 0, ["FullValueConsdSec50CA"] = 0, ["FullValueConsdOthUnqshr"] = 0,
            ["FullConsideration"] = 0, ["DeductSec48"] = DeductSec48Obj(0), ["BalanceCG"] = 0, ["LossSec94of7Or94of8"] = 0, ["CapgainonAssets"] = 0,
        };

        long stNormSale = stNorm;
        var saleOth = new JsonObject
        {
            ["FullValueConsdRecvUnqshr"] = stNormSale, ["FairMrktValueUnqshr"] = 0, ["FullValueConsdSec50CA"] = 0, ["FullValueConsdOthUnqshr"] = 0,
            ["FullConsideration"] = stNormSale, ["DeductSec48"] = DeductSec48Obj(0), ["BalanceCG"] = stNorm, ["LossSec94of7Or94of8"] = 0,
        };
        if (isItr3)
        {
            saleOth["DeemedStcgOnAssets"] = 0;
            saleOth["ExemptionOrDednUs54"] = new JsonObject { ["ExemptionGrandTotal"] = 0 };
        }
        saleOth["CapgainonAssets"] = stNorm;
        stcgObj["SaleOnOtherAssets"] = saleOth;
        stcgObj["TotalAmtDeemedStcg"] = 0;
        stcgObj["PassThrIncNatureSTCG"] = 0;
        stcgObj["TotalAmtNotTaxUsDTAAStcg"] = 0;
        stcgObj["TotalAmtTaxUsDTAAStcg"] = 0;
        stcgObj["TotalSTCG"] = totSt;

        var ltcgObj = new JsonObject();
        if (isItr3)
        {
            ltcgObj["SlumpSaleInLtcgDtls"] = new JsonObject
            {
                ["SlumpSaleInLtcg"] = new JsonObject
                {
                    ["FMV11UAEii"] = 0,
                    ["FMV11UAEiii"] = 0,
                    ["FullConsideration"] = 0,
                    ["NetWorthOfDivision"] = 0,
                    ["SlumpBalance"] = 0,
                    ["ExemptionOrDednUs54"] = new JsonObject { ["ExemptionGrandTotal"] = 0 },
                    ["CapgainonAssets"] = 0,
                }
            };
        }
        ltcgObj["SaleOfEquityShareUs112A"] = new JsonObject
        {
            ["BalanceCG"] = lt112a, ["DeductionUs54F"] = 0, ["CapgainonAssets"] = lt112a,
        };
        ltcgObj["NRISaleOfEquityShareUs112A"] = new JsonObject
        {
            ["BalanceCG"] = 0, ["DeductionUs54F"] = 0, ["CapgainonAssets"] = 0,
        };
        ltcgObj["NRISaleofForeignAsset"] = new JsonObject
        {
            ["SaleonSpecAsset"] = 0, ["DednSpecAssetus115"] = 0, ["BalonSpeciAsset"] = 0,
        };
        long lt112Sale = input.Ltcg112SaleValue > 0m ? ToRupees(input.Ltcg112SaleValue) : lt112;
        var saleNa = new JsonObject
        {
            ["FullValueConsdRecvUnqshr"] = lt112Sale,
            ["FairMrktValueUnqshr"] = 0, ["FullValueConsdSec50CA"] = 0, ["FullValueConsdOthUnqshr"] = 0,
            ["FullConsideration"] = lt112Sale,
            ["DeductSec48"] = DeductSec48Obj(Math.Max(0L, lt112Sale - lt112)),
            ["BalanceCG"] = lt112,
        };
        if (isItr3)
            saleNa["ExemptionOrDednUs54"] = new JsonObject { ["ExemptionGrandTotal"] = 0 };
        else
            saleNa["DeductionUs54F"] = 0;
        saleNa["CapgainonAssets"] = lt112;
        ltcgObj["SaleofAssetNADtls"] = new JsonObject { ["SaleofAssetNA"] = saleNa };
        ltcgObj["TotalAmtDeemedLtcg"] = 0;
        ltcgObj["PassThrIncNatureLTCG"] = 0;
        ltcgObj["TotalAmtNotTaxUsDTAALtcg"] = 0;
        ltcgObj["TotalAmtTaxUsDTAALtcg"] = 0;
        ltcgObj["TotalLTCG"] = totLt;

        var currYrLosses = new JsonObject
        {
            ["InLossSetOff"] = new JsonObject { ["StclSetoff20Per"] = 0, ["StclSetoff30Per"] = 0, ["StclSetoffAppRate"] = 0, ["StclSetoffDTAARate"] = 0, ["LtclSetOff12_5Per"] = 0, ["LtclSetOffDTAARate"] = 0 },
            ["InStcg20Per"] = new JsonObject { ["CurrYearIncome"] = st111a, ["StclSetoff30Per"] = 0, ["StclSetoffAppRate"] = 0, ["StclSetoffDTAARate"] = 0, ["CurrYrCapGain"] = st111a },
            ["InStcg30Per"] = new JsonObject { ["CurrYearIncome"] = 0, ["StclSetoff20Per"] = 0, ["StclSetoffAppRate"] = 0, ["StclSetoffDTAARate"] = 0, ["CurrYrCapGain"] = 0 },
            ["InStcgAppRate"] = new JsonObject { ["CurrYearIncome"] = stNorm, ["StclSetoff20Per"] = 0, ["StclSetoff30Per"] = 0, ["StclSetoffDTAARate"] = 0, ["CurrYrCapGain"] = stNorm },
            ["InStcgDTAARate"] = new JsonObject { ["CurrYearIncome"] = 0, ["StclSetoff20Per"] = 0, ["StclSetoff30Per"] = 0, ["StclSetoffAppRate"] = 0, ["CurrYrCapGain"] = 0 },
            ["InLtcg12_5Per"] = new JsonObject { ["CurrYearIncome"] = totLt, ["StclSetoff20Per"] = 0, ["StclSetoff30Per"] = 0, ["StclSetoffAppRate"] = 0, ["StclSetoffDTAARate"] = 0, ["LtclSetOffDTAARate"] = 0, ["CurrYrCapGain"] = totLt },
            ["InLtcgDTAARate"] = new JsonObject { ["CurrYearIncome"] = 0, ["StclSetoff20Per"] = 0, ["StclSetoff30Per"] = 0, ["StclSetoffAppRate"] = 0, ["StclSetoffDTAARate"] = 0, ["LtclSetOff12_5Per"] = 0, ["CurrYrCapGain"] = 0 },
            ["TotLossSetOff"] = new JsonObject { ["StclSetoff20Per"] = 0, ["StclSetoff30Per"] = 0, ["StclSetoffAppRate"] = 0, ["StclSetoffDTAARate"] = 0, ["LtclSetOff12_5Per"] = 0, ["LtclSetOffDTAARate"] = 0 },
            ["LossRemainSetOff"] = new JsonObject { ["StclSetoff20Per"] = 0, ["StclSetoff30Per"] = 0, ["StclSetoffAppRate"] = 0, ["StclSetoffDTAARate"] = 0, ["LtclSetOff12_5Per"] = 0, ["LtclSetOffDTAARate"] = 0 },
        };

        var accru = new JsonObject
        {
            ["ShortTermUnder20Per"] = SingleBucketDateRange(st111a, useUp16Of6Key: false),
            ["ShortTermUnder30Per"] = ZeroDateRange(false),
            ["ShortTermUnderAppRate"] = SingleBucketDateRange(stNorm, useUp16Of6Key: false),
            ["ShortTermUnderDTAARate"] = ZeroDateRange(false),
            ["LongTermUnder12_5Per"] = SingleBucketDateRange(totLt, useUp16Of6Key: false),
            ["LongTermUnderDTAARate"] = ZeroDateRange(false),
        };
        if (isItr3 || vda > 0) accru["VDATrnsfGainsUnder30Per"] = SingleBucketDateRange(vda, useUp16Of6Key: false);

        var schedCg = new JsonObject
        {
            ["ShortTermCapGainFor23"] = stcgObj,
            ["LongTermCapGain23"] = ltcgObj,
            ["SumOfCGIncm"] = totSt + totLt,
            ["IncmFromVDATrnsf"] = vda,
            ["TotScheduleCGFor23"] = totSt + totLt + vda,
        };
        if (isItr3) schedCg["DeducClaimInfo"] = new JsonObject { ["TotDeductClaim"] = 0 };
        schedCg["CurrYrLosses"] = currYrLosses;
        schedCg["AccruOrRecOfCG"] = accru;

        JsonObject? sched112A = null;
        if (lt112a > 0)
        {
            long sale112 = input.Ltcg112ASaleValue > 0m ? ToRupees(input.Ltcg112ASaleValue) : lt112a;
            long cost112 = Math.Max(0L, sale112 - lt112a);
            sched112A = new JsonObject
            {
                ["Schedule112ADtls"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["ShareOnOrBefore"] = "AE",
                        ["ISINCode"] = "INE000A01001",
                        ["ShareUnitName"] = "Listed Equity Shares / Mutual Fund",
                        ["TotSaleValue"] = sale112,
                        ["CostAcqWithoutIndx"] = cost112,
                        ["AcquisitionCost"] = cost112,
                        ["LTCGBeforelowerB1B2"] = cost112,
                        ["FairMktValuePerShareunit"] = 0,
                        ["TotFairMktValueCapAst"] = 0,
                        ["ExpExclCnctTransfer"] = 0,
                        ["TotalDeductions"] = cost112,
                        ["Balance"] = lt112a,
                    }
                },
                ["SaleValue112A"] = sale112,
                ["CostAcqWithoutIndx112A"] = cost112,
                ["AcquisitionCost112A"] = cost112,
                ["LTCGBeforelowerB1B2112A"] = cost112,
                ["FairMktValueCapAst112A"] = 0,
                ["ExpExclCnctTransfer112A"] = 0,
                ["Deductions112A"] = cost112,
                ["Balance112A"] = lt112a,
                ["TotalBalance112A"] = lt112a,
            };
        }

        JsonObject? schedVda = null;
        if (vda > 0)
        {
            long consid = input.VdaConsideration > 0m ? ToRupees(input.VdaConsideration) : vda;
            long acq = Math.Max(0L, consid - vda);
            schedVda = new JsonObject
            {
                ["ScheduleVDADtls"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["DateofAcquisition"] = "2024-06-15",
                        ["DateofTransfer"] = "2026-02-15",
                        ["HeadUndIncTaxed"] = "CG",
                        ["AcquisitionCost"] = acq,
                        ["ConsidReceived"] = consid,
                        ["IncomeFromVDA"] = vda,
                    }
                },
                ["TotIncCapGain"] = vda,
            };
        }

        return (schedCg, sched112A, schedVda);
    }

    public static JsonObject? BuildScheduleOs(CalculatePersonalExtendedTaxRequest input, PersonalRegimeBreakdownDto active, bool isItr3)
    {
        if (active.OtherSourcesIncome <= 0m) return null;
        long sav = ToRupees(input.SavingsInterest);
        long fd = ToRupees(input.FdInterest);
        long div = ToRupees(input.DividendIncome);
        long oth = ToRupees(input.OtherSourcesIncome);
        long totInt = sav + fd;
        long gross = totInt + div + oth;

        return new JsonObject
        {
            ["IncOthThanOwnRaceHorse"] = new JsonObject
            {
                ["GrossIncChrgblTaxAtAppRate"] = gross,
                ["DividendGross"] = div,
                ["InterestGross"] = totInt,
                ["IntrstFrmSavingBank"] = sav,
                ["IntrstFrmTermDeposit"] = fd,
                ["IntrstFrmIncmTaxRefund"] = 0,
                ["NatofPassThrghIncome"] = 0,
                ["IntrstFrmOthers"] = 0,
                ["RentFromMachPlantBldgs"] = 0,
                ["Tot562x"] = 0,
                ["Aggrtvaluewithoutcons562x"] = 0,
                ["Immovpropwithoutcons562x"] = 0,
                ["Immovpropinadeqcons562x"] = 0,
                ["Anyotherpropwithoutcons562x"] = 0,
                ["Anyotherpropinadeqcons562x"] = 0,
                ["FamilyPension"] = 0,
                ["IncomeNotified89AOS"] = 0,
                ["AnyOtherIncome"] = oth,
                ["IncChargeableSpecialRates"] = 0,
                ["LtryPzzlChrgblUs115BB"] = 0,
                ["IncChrgblUs115BBE"] = 0,
                ["CashCreditsUs68"] = 0,
                ["UnExplndInvstmntsUs69"] = 0,
                ["UnExplndMoneyUs69A"] = 0,
                ["UnDsclsdInvstmntsUs69B"] = 0,
                ["UnExplndExpndtrUs69C"] = 0,
                ["AmtBrwdRepaidOnHundiUs69D"] = 0,
                ["OthersGross"] = oth,
                ["PassThrIncOSChrgblSplRate"] = 0,
                ["Deductions"] = new JsonObject { ["Expenses"] = 0, ["DeductionUs57iia"] = 0, ["Depreciation"] = 0, ["TotDeductions"] = 0 },
                ["BalanceNoRaceHorse"] = gross,
                ["TaxAccumulatedBalRecPF"] = new JsonObject { ["TotalIncomeBenefit"] = 0, ["TotalTaxBenefit"] = 0 },
            },
            ["IncFrmLottery"] = ZeroDateRange(isItr3),
            ["DividendIncUs115BBDA"] = ZeroDateRange(isItr3),
            ["DividendIncUs115BBDAaiii"] = ZeroDateRange(isItr3),
            ["DividendIncUs115A1ai"] = ZeroDateRange(isItr3),
            ["DividendIncUs115A1aA"] = ZeroDateRange(isItr3),
            ["DividendIncUs115AC"] = ZeroDateRange(isItr3),
            ["DividendIncUs115ACA"] = ZeroDateRange(isItr3),
            ["DividendIncUs115AD1i"] = ZeroDateRange(isItr3),
            ["DividendDTAA"] = ZeroDateRange(isItr3),
            ["NOT89A"] = ZeroDateRange(isItr3),
            ["IncChargeable"] = gross,
        };
    }

    public static (JsonObject Cyla, JsonObject Bfla) BuildCylaAndBfla(PersonalRegimeBreakdownDto active, bool isItr3)
    {
        JsonObject CylaRow(long amt) => new()
        {
            ["IncCYLA"] = new JsonObject { ["IncOfCurYrUnderThatHead"] = amt, ["IncOfCurYrAfterSetOff"] = amt }
        };
        JsonObject SalaryBflaRow(long amt) => new()
        {
            ["IncBFLA"] = new JsonObject { ["IncOfCurYrUndHeadFromCYLA"] = amt, ["IncOfCurYrAfterSetOffBFLosses"] = amt }
        };
        JsonObject BflaRow(long amt)
        {
            var inner = new JsonObject
            {
                ["IncOfCurYrUndHeadFromCYLA"] = amt,
                ["BFlossPrevYrUndSameHeadSetoff"] = 0,
            };
            if (isItr3)
            {
                inner["BFUnabsorbedDeprSetoff"] = 0;
                inner["BFAllUs35Cl4Setoff"] = 0;
            }
            inner["IncOfCurYrAfterSetOffBFLosses"] = amt;
            return new JsonObject { ["IncBFLA"] = inner };
        }

        long sal = ToRupees(active.NetSalary);
        long st20 = ToRupees(active.Stcg111A);
        long stApp = ToRupees(active.StcgNormal);
        long lt = ToRupees(active.Ltcg112A + active.Ltcg112);
        long afterAll = ToRupees(active.GrossTotalIncome);

        var totCur = new JsonObject { ["TotHPlossCurYr"] = 0 };
        if (isItr3) totCur["TotBusLoss"] = 0;
        totCur["TotOthSrcLossNoRaceHorse"] = 0;

        var totSet = new JsonObject { ["TotHPlossCurYrSetoff"] = 0 };
        if (isItr3) totSet["TotBusLossSetoff"] = 0;
        totSet["TotOthSrcLossNoRaceHorseSetoff"] = 0;

        var lossRem = new JsonObject { ["BalHPlossCurYrAftSetoff"] = 0 };
        if (isItr3) lossRem["BalBusLossAftSetoff"] = 0;
        lossRem["BalOthSrcLossNoRaceHorseAftSetoff"] = 0;

        var cyla = new JsonObject
        {
            ["STCG20Per"] = CylaRow(st20),
            ["STCG30Per"] = CylaRow(0),
            ["STCGAppRate"] = CylaRow(stApp),
            ["STCGDTAARate"] = CylaRow(0),
            ["LTCG12_5Per"] = CylaRow(lt),
            ["LTCGDTAARate"] = CylaRow(0),
            ["TotalCurYr"] = totCur,
            ["TotalLossSetOff"] = totSet,
            ["LossRemAftSetOff"] = lossRem,
        };

        var bfTot = new JsonObject { ["TotBFLossSetoff"] = 0 };
        if (isItr3)
        {
            bfTot["TotUnabsorbedDeprSetoff"] = 0;
            bfTot["TotAllUs35cl4Setoff"] = 0;
        }

        var bfla = new JsonObject
        {
            ["Salary"] = SalaryBflaRow(sal),
            ["STCG20Per"] = BflaRow(st20),
            ["STCG30Per"] = BflaRow(0),
            ["STCGAppRate"] = BflaRow(stApp),
            ["STCGDTAARate"] = BflaRow(0),
            ["LTCG12_5Per"] = BflaRow(lt),
            ["LTCGDTAARate"] = BflaRow(0),
            ["TotalBFLossSetOff"] = bfTot,
            ["IncomeOfCurrYrAftCYLABFLA"] = afterAll,
        };

        return (cyla, bfla);
    }

    public static JsonObject BuildPersonalScheduleVia(CalculatePersonalExtendedTaxRequest input, PersonalRegimeBreakdownDto active, bool isItr3 = false)
    {
        bool isOld = active.Regime == "old";
        long u80c = ToRupees(input.Section80C);
        long u80ccd1b = ToRupees(input.Section80Ccd1B);
        long u80d = ToRupees(input.Section80D);
        long u80e = ToRupees(input.Section80E);
        long u80g = ToRupees(input.Section80G);
        long u80tta = ToRupees(input.Section80Tta);
        long uTotB = u80c + u80ccd1b + u80d + u80e + u80g;
        long uTot = uTotB + u80tta;

        long d80c = isOld ? Math.Min(u80c, 150_000L) : 0L;
        long d80ccd1b = isOld ? Math.Min(u80ccd1b, 50_000L) : 0L;
        long d80d = isOld ? Math.Min(u80d, 75_000L) : 0L;
        long d80e = isOld ? u80e : 0L;
        long d80g = isOld ? u80g : 0L;
        long d80tta = isOld ? Math.Min(u80tta, 10_000L) : 0L;
        long dTot = ToRupees(active.ChapterViaDeductions);
        long dPartB = Math.Min(dTot, d80c + d80ccd1b + d80d + d80e + d80g);
        long dPartCAD = Math.Max(0L, dTot - dPartB);

        var usr = new JsonObject
        {
            ["Section80C"] = u80c,
            ["Section80CCD1B"] = u80ccd1b,
            ["Section80D"] = u80d,
            ["Section80E"] = u80e,
            ["Section80G"] = u80g,
            ["Section80TTA"] = u80tta,
        };
        if (isItr3)
        {
            usr["TotPartBchapterVIA"] = uTotB;
            usr["TotPartCchapterVIA"] = 0;
            usr["TotPartCAandDchapterVIA"] = u80tta;
        }
        usr["TotalChapVIADeductions"] = uTot;

        var ded = new JsonObject
        {
            ["Section80C"] = d80c,
            ["Section80CCD1B"] = d80ccd1b,
            ["Section80D"] = d80d,
            ["Section80E"] = d80e,
            ["Section80G"] = d80g,
            ["Section80GGA"] = 0,
            ["Section80TTA"] = d80tta,
        };
        if (isItr3)
        {
            ded["TotPartBchapterVIA"] = dPartB;
            ded["TotPartCchapterVIA"] = 0;
            ded["TotPartCAandDchapterVIA"] = dPartCAD;
        }
        ded["TotalChapVIADeductions"] = dTot;

        return new JsonObject
        {
            ["UsrDeductUndChapVIA"] = usr,
            ["DeductUndChapVIA"] = ded,
        };
    }

    public static JsonObject? BuildPersonalScheduleSi(PersonalRegimeBreakdownDto active)
    {
        var arr = new JsonArray();
        long st111a = ToRupees(active.Stcg111A);
        long ltcg112aTaxable = ToRupees(Math.Max(0m, active.Ltcg112A - 125_000m));
        long ltcg112 = ToRupees(active.Ltcg112);
        long vda = ToRupees(active.VdaIncome115BBH);

        if (st111a > 0)
            arr.Add(new JsonObject { ["SecCode"] = "1A", ["SplRatePercent"] = 20, ["SplRateInc"] = st111a, ["SplRateIncTax"] = ToRupees(active.TaxOnStcg111A) });
        if (ltcg112aTaxable > 0)
            arr.Add(new JsonObject { ["SecCode"] = "2A", ["SplRatePercent"] = 12.5m, ["SplRateInc"] = ltcg112aTaxable, ["SplRateIncTax"] = ToRupees(active.TaxOnLtcg112A) });
        if (ltcg112 > 0)
            arr.Add(new JsonObject { ["SecCode"] = "22", ["SplRatePercent"] = 12.5m, ["SplRateInc"] = ltcg112, ["SplRateIncTax"] = ToRupees(active.TaxOnLtcg112) });
        if (vda > 0)
            arr.Add(new JsonObject { ["SecCode"] = "5BBH", ["SplRatePercent"] = 30, ["SplRateInc"] = vda, ["SplRateIncTax"] = ToRupees(active.TaxOnVda115BBH) });

        if (arr.Count == 0) return null;
        return new JsonObject
        {
            ["SplCodeRateTax"] = arr,
            ["TotSplRateInc"] = st111a + ltcg112aTaxable + ltcg112 + vda,
            ["TotSplRateIncTax"] = ToRupees(active.SpecialRateTax),
        };
    }

    public static JsonObject? BuildScheduleFa(CalculatePersonalExtendedTaxRequest input)
    {
        if (input.ForeignAssets is null || input.ForeignAssets.Count == 0) return null;
        var bankArr = new JsonArray();
        foreach (var fa in input.ForeignAssets)
        {
            bankArr.Add(new JsonObject
            {
                ["CountryName"] = Trunc(fa.CountryName, 55, "United States"),
                ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(fa.CountryCode) ? "2" : fa.CountryCode.Trim(),
                ["Bankname"] = Trunc(fa.InstitutionName, 125, "Foreign Financial Institution"),
                ["AddressOfBank"] = Trunc(fa.Address, 200, "Overseas"),
                ["ZipCode"] = Trunc(fa.ZipCode, 8, "10001"),
                ["ForeignAccountNumber"] = Trunc(fa.AccountNumber, 34, "ACCT001"),
                ["OwnerStatus"] = "OWNER",
                ["AccOpenDate"] = string.IsNullOrWhiteSpace(fa.OpenDate) ? "2023-01-15" : fa.OpenDate,
                ["PeakBalanceDuringYear"] = ToRupees(fa.PeakBalanceInr),
                ["ClosingBalance"] = ToRupees(fa.ClosingBalanceInr),
                ["IntrstAccured"] = ToRupees(fa.IncomeAccruedInr),
            });
        }
        return new JsonObject { ["DetailsForiegnBank"] = bankArr };
    }

    public static JsonObject? BuildScheduleAl(CalculatePersonalExtendedTaxRequest input, PersonalRegimeBreakdownDto active, Client client, ExportItrJsonRequest? req, bool isItr3)
    {
        if (input.ScheduleAl is null && active.TaxableIncome <= 5_000_000m) return null;
        var al = input.ScheduleAl ?? new ScheduleAlInput();
        var obj = new JsonObject
        {
            ["MovableAsset"] = new JsonObject
            {
                ["DepositsInBank"] = ToRupees(al.BankDeposits),
                ["SharesAndSecurities"] = ToRupees(al.SharesAndSecurities),
                ["InsurancePolicies"] = ToRupees(al.InsurancePolicies),
                ["LoansAndAdvancesGiven"] = ToRupees(al.LoansAndAdvancesGiven),
                ["CashInHand"] = ToRupees(al.CashInHand),
                ["JewelleryBullionEtc"] = ToRupees(al.JewelleryAndBullion),
                ["ArchCollDrawPaintSulpArt"] = 0,
                ["VehiclYachtsBoatsAircrafts"] = ToRupees(al.VehiclesAndOther),
            },
            ["LiabilityInRelatAssets"] = ToRupees(al.LiabilitiesIncurred),
        };
        if (isItr3) obj["InterstAOPFlag"] = "N";
        if (al.ImmovablePropertyValue > 0m)
        {
            obj["ImmovableDetails"] = new JsonArray
            {
                new JsonObject
                {
                    ["Description"] = Trunc(al.ImmovableDescription, 25, "Residential Property"),
                    ["AddressAL"] = new JsonObject
                    {
                        ["ResidenceNo"] = Trunc(req?.ResidenceNo, 50, "House No. 1"),
                        ["LocalityOrArea"] = Trunc(req?.LocalityOrArea, 50, "Main Road"),
                        ["CityOrTownOrDistrict"] = Trunc(req?.City, 50, "Kochi"),
                        ["StateCode"] = ResolveStateCode(req?.StateCode, client),
                        ["CountryCode"] = "91",
                        ["PinCode"] = ResolvePinInt(req?.PinCode),
                    },
                    ["Amount"] = ToRupees(al.ImmovablePropertyValue),
                }
            };
        }
        return obj;
    }

    public static JsonObject? BuildScheduleTds1(CalculatePersonalExtendedTaxRequest input, PersonalRegimeBreakdownDto active)
    {
        if (input.TdsSalary <= 0m) return null;
        var arr = new JsonArray();
        var employers = input.Employers is { Count: > 0 }
            ? input.Employers
            : [new SalaryEmployerInput("Primary Employer", "MUMT01234A", "OTH", active.GrossSalary, input.TdsSalary)];
        long tot = 0;
        foreach (var e in employers)
        {
            long tds = ToRupees(e.TdsDeducted > 0m ? e.TdsDeducted : input.TdsSalary);
            long sal = ToRupees(e.GrossSalary > 0m ? e.GrossSalary : active.GrossSalary);
            string tan = !string.IsNullOrWhiteSpace(e.TanOfEmployer) ? e.TanOfEmployer.Trim().ToUpperInvariant() : "MUMT01234A";
            arr.Add(new JsonObject
            {
                ["EmployerOrDeductorOrCollectDetl"] = new JsonObject
                {
                    ["TAN"] = tan,
                    ["EmployerOrDeductorOrCollecterName"] = Trunc(e.EmployerName, 125, "Employer"),
                },
                ["IncChrgSal"] = sal,
                ["TotalTDSSal"] = tds,
            });
            tot += tds;
        }
        return new JsonObject
        {
            ["TDSonSalary"] = arr,
            ["TotalTDSonSalaries"] = tot,
        };
    }

    public static JsonObject BuildPersonalTaxesPaid(CalculatePersonalExtendedTaxRequest input, PersonalRegimeBreakdownDto active)
    {
        long adv = ToRupees(input.AdvanceTaxPaid);
        long tds = ToRupees(input.TdsSalary + input.TdsOtherThanSalary);
        long tcs = ToRupees(input.TcsPaid);
        long sat = ToRupees(input.SelfAssessmentTaxPaid);
        long tot = adv + tds + tcs + sat;
        return new JsonObject
        {
            ["TaxesPaid"] = new JsonObject
            {
                ["AdvanceTax"] = adv,
                ["TDS"] = tds,
                ["TCS"] = tcs,
                ["SelfAssessmentTax"] = sat,
                ["TotalTaxesPaid"] = tot,
            },
            ["BalTaxPayable"] = Math.Max(0L, ToRupees(active.TotalTaxLiability) - tot),
        };
    }
}
