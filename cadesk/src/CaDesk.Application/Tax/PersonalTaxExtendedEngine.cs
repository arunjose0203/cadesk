using CaDesk.Domain.Common;

namespace CaDesk.Application.Tax;

/// <summary>
/// Full ITR-2 (Multiple House Properties, Detailed Capital Gains, VDA 115BBH, Foreign Assets, Schedule AL)
/// and ITR-3 (Individual/HUF Proprietor Books + 44AB Schedule BP + Old/New 115BAC + AMT 115JC) tax engine.
/// </summary>
public static class PersonalTaxExtendedEngine
{
    public static PersonalExtendedTaxResultDto Compute(CalculatePersonalExtendedTaxRequest rawReq)
    {
        var req = Normalize(rawReq);
        var oldRes = ComputeRegime(req, isNewRegime: false);
        var newRes = ComputeRegime(req, isNewRegime: true);

        string recommended = newRes.TotalTaxLiability <= oldRes.TotalTaxLiability ? "new" : "old";
        decimal savings = Math.Abs(oldRes.TotalTaxLiability - newRes.TotalTaxLiability);
        var rules = TaxEngine.NewRegimeRules.For(req.FinancialYear, req.AssessmentYear);
        string reason = recommended == "new"
            ? $"New Regime (Sec 115BAC) saves ₹{savings:N0} due to ₹75,000 standard deduction, concessional slabs, and ₹{rules.RebateIncomeLimit:N0} u/s 87A limit."
            : $"Old Regime saves ₹{savings:N0} after factoring HRA, Section 24(b) home loan loss, Chapter VI-A, and business/property deductions.";

        string selected = req.OptedRegime?.ToLowerInvariant() switch
        {
            "old" => "old",
            "new" => "new",
            _ => recommended
        };

        var active = selected == "old" ? oldRes : newRes;

        return new PersonalExtendedTaxResultDto(
            ReturnFormType: req.ReturnFormType,
            StatusType: req.StatusType,
            AssessmentYear: req.AssessmentYear,
            FinancialYear: req.FinancialYear,
            OldRegime: oldRes,
            NewRegime: newRes,
            SelectedRegime: selected,
            RecommendedRegime: recommended,
            TaxSavings: Math.Round(savings, 0, MidpointRounding.AwayFromZero),
            RecommendationReason: reason,
            ActiveSummary: active,
            NormalizedInput: req
        );
    }

    private static CalculatePersonalExtendedTaxRequest Normalize(CalculatePersonalExtendedTaxRequest req)
    {
        decimal salary = req.GrossSalary;
        decimal tdsSal = req.TdsSalary;
        if (req.Employers is { Count: > 0 })
        {
            if (salary == 0m) salary = req.Employers.Sum(e => Math.Max(0m, e.GrossSalary));
            if (tdsSal == 0m) tdsSal = req.Employers.Sum(e => Math.Max(0m, e.TdsDeducted));
        }

        decimal stcg111a = req.Stcg111A > 0m ? req.Stcg111A : Math.Max(0m, req.Stcg111ASaleValue - req.Stcg111ACost);
        decimal ltcg112a = req.Ltcg112A > 0m ? req.Ltcg112A : Math.Max(0m, req.Ltcg112ASaleValue - req.Ltcg112ACost);
        decimal ltcg112 = req.Ltcg112 > 0m ? req.Ltcg112 : Math.Max(0m, req.Ltcg112SaleValue - req.Ltcg112Cost);
        decimal vda = req.VdaIncome115BBH > 0m ? req.VdaIncome115BBH : Math.Max(0m, req.VdaConsideration - req.VdaCostOfAcquisition);

        decimal itDep = req.IncomeTaxDepreciationSec32;
        if (req.DepreciationBlocks is { Count: > 0 } && itDep == 0m)
            itDep = req.DepreciationBlocks.Sum(BusinessTaxEngine.ComputeBlockDepreciation);

        decimal advTax = req.AdvanceTaxPaid;
        if (req.AdvanceTaxChallans is { Count: > 0 } && advTax == 0m)
            advTax = req.AdvanceTaxChallans.Sum(c => Math.Max(0m, c.Amount));

        decimal tdsOth = req.TdsOtherThanSalary;
        if (req.TdsCreditEntries is { Count: > 0 } && tdsOth == 0m)
            tdsOth = req.TdsCreditEntries.Sum(t => Math.Max(0m, t.TdsClaimed));

        string normForm = (string.Equals(req.ReturnFormType, "ITR-3", StringComparison.OrdinalIgnoreCase)
            || req.RevenueFromOperations > 0m
            || req.IsTaxAuditApplicable)
            ? "ITR-3"
            : "ITR-2";
        string statusType = string.Equals(req.TaxpayerCategory, "huf", StringComparison.OrdinalIgnoreCase) || string.Equals(req.StatusType, "H", StringComparison.OrdinalIgnoreCase)
            ? "H" : "I";

        return req with
        {
            ReturnFormType = normForm,
            StatusType = statusType,
            GrossSalary = salary,
            TdsSalary = tdsSal,
            Stcg111A = stcg111a,
            Ltcg112A = ltcg112a,
            Ltcg112 = ltcg112,
            VdaIncome115BBH = vda,
            IncomeTaxDepreciationSec32 = itDep,
            AdvanceTaxPaid = advTax,
            TdsOtherThanSalary = tdsOth,
        };
    }

    private static PersonalRegimeBreakdownDto ComputeRegime(CalculatePersonalExtendedTaxRequest req, bool isNewRegime)
    {
        // 1. Salary Head
        decimal stdDeduction = 0m, hraExemption = 0m, profTax = 0m;
        if (req.GrossSalary > 0m)
        {
            if (isNewRegime)
            {
                stdDeduction = Math.Min(req.GrossSalary, 75_000m);
            }
            else
            {
                stdDeduction = Math.Min(req.GrossSalary, 50_000m);
                if (req.HraReceived > 0m && req.RentPaid > 0m)
                {
                    decimal basicEst = req.GrossSalary * 0.50m;
                    decimal rentMinus10 = Math.Max(0m, req.RentPaid - basicEst * 0.10m);
                    decimal pct = basicEst * (req.IsMetro ? 0.50m : 0.40m);
                    hraExemption = Math.Min(req.HraReceived, Math.Min(rentMinus10, pct));
                }
                profTax = Math.Min(2_500m, req.ProfessionalTax);
            }
        }
        decimal salaryDed = stdDeduction + hraExemption + profTax;
        decimal netSalary = Math.Max(0m, req.GrossSalary - salaryDed);

        // 2. House Property Head (single or multiple)
        decimal hpNet = ComputeHouseProperty(req, isNewRegime);

        // 3. ITR-3 Regular Books PGBP
        decimal bookPbt = 0m, totalDisallow = 0m, pgbpNet = 0m;
        if (req.ReturnFormType == "ITR-3")
        {
            decimal totRev = req.RevenueFromOperations + req.OtherBusinessIncome;
            decimal totExp = req.PurchasesAndDirectCost + req.ChangesInInventories + req.EmployeeBenefitExpense
                + req.FinanceCost + req.DepreciationPerBooks + req.AuditFee + req.OtherBusinessExpenses;
            bookPbt = totRev - totExp;
            totalDisallow = req.DisallowSec36 + req.DisallowSec37 + req.DisallowSec40a
                + req.DisallowSec40A3 + req.DisallowSec43B + req.DisallowSec43BhMsme;

            decimal pgbpBeforeLoss = bookPbt + req.DepreciationPerBooks + totalDisallow - req.IncomeTaxDepreciationSec32;
            decimal bfSetOff = pgbpBeforeLoss > 0m ? Math.Min(pgbpBeforeLoss, Math.Max(0m, req.BroughtForwardBusinessLoss)) : 0m;
            pgbpNet = pgbpBeforeLoss - bfSetOff;
        }

        // 4. Capital Gains & VDA (Crypto 115BBH)
        decimal stcg111a = Math.Max(0m, req.Stcg111A);
        decimal stcgNorm = Math.Max(0m, req.StcgNormal);
        decimal ltcg112a = Math.Max(0m, req.Ltcg112A);
        decimal ltcg112 = Math.Max(0m, req.Ltcg112);
        decimal vda = Math.Max(0m, req.VdaIncome115BBH);
        decimal totCgAndVda = stcg111a + stcgNorm + ltcg112a + ltcg112 + vda;

        // 5. Other Sources
        decimal ifos = Math.Max(0m, req.SavingsInterest) + Math.Max(0m, req.FdInterest) + Math.Max(0m, req.DividendIncome) + Math.Max(0m, req.OtherSourcesIncome);

        decimal ordinaryIncome = Math.Max(0m, netSalary + hpNet + pgbpNet + stcgNorm + ifos);
        decimal gti = ordinaryIncome + stcg111a + ltcg112a + ltcg112 + vda;

        // 6. Chapter VI-A (only Old Regime for 80C/80D/80G/80TTA etc.)
        decimal chapVia = 0m;
        if (!isNewRegime)
        {
            decimal d80C = Math.Min(150_000m, Math.Max(0m, req.Section80C));
            decimal d80Ccd1B = Math.Min(50_000m, Math.Max(0m, req.Section80Ccd1B));
            decimal d80D = Math.Min(75_000m, Math.Max(0m, req.Section80D));
            decimal d80E = Math.Max(0m, req.Section80E);
            decimal d80G = Math.Max(0m, req.Section80G);
            decimal d80Tta = Math.Min(10_000m, Math.Max(0m, req.Section80Tta));
            chapVia = Math.Min(ordinaryIncome, d80C + d80Ccd1B + d80D + d80E + d80G + d80Tta);
        }

        decimal taxableNormal = Math.Max(0m, ordinaryIncome - chapVia);
        decimal totalTaxable = BusinessTaxEngine.RoundSec288A(taxableNormal + stcg111a + ltcg112a + ltcg112 + vda);

        // 7. Slab Tax
        var newRules = TaxEngine.NewRegimeRules.For(req.FinancialYear, req.AssessmentYear);
        var (slabTax, slabs) = isNewRegime
            ? TaxEngine.ComputeNewRegimeSlabs(taxableNormal, newRules)
            : TaxEngine.ComputeOldRegimeSlabs(taxableNormal, req.TaxpayerCategory);

        // 8. Special Rate Tax
        decimal tax111a = Math.Round(stcg111a * newRules.Stcg111ARate, 0, MidpointRounding.AwayFromZero);
        decimal tax112a = Math.Round(Math.Max(0m, ltcg112a - 125_000m) * 0.125m, 0, MidpointRounding.AwayFromZero);
        decimal tax112 = Math.Round(ltcg112 * 0.125m, 0, MidpointRounding.AwayFromZero);
        decimal taxVda = Math.Round(vda * 0.30m, 0, MidpointRounding.AwayFromZero); // Sec 115BBH @ 30%
        decimal specialTax = tax111a + tax112a + tax112 + taxVda;
        decimal taxBeforeRebate = slabTax + specialTax;

        // 9. Rebate u/s 87A (Resident only; New excludes special-rate income FY 2025-26+)
        decimal rebate87A = 0m;
        if (isNewRegime)
        {
            decimal rebateBase = newRules.RebateExcludesSpecialRateTax ? slabTax : taxBeforeRebate;
            if (totalTaxable <= newRules.RebateIncomeLimit)
                rebate87A = Math.Min(newRules.MaxRebate, rebateBase);
            else
            {
                decimal above = totalTaxable - newRules.RebateIncomeLimit;
                if (rebateBase > above) rebate87A = rebateBase - above;
            }
        }
        else if (totalTaxable <= 500_000m)
        {
            rebate87A = Math.Min(12_500m, taxBeforeRebate);
        }

        decimal taxAfterRebate = Math.Max(0m, taxBeforeRebate - rebate87A);

        // 10. Surcharge + Marginal Relief (reusing TaxEngine.CalculateSurchargeWithMarginalRelief)
        var (surcharge, _) = TaxEngine.CalculateSurchargeWithMarginalRelief(
            totalTaxable,
            taxAfterRebate,
            isNewRegime,
            inc => isNewRegime
                ? TaxEngine.ComputeNewRegimeSlabs(inc, newRules).TotalTax
                : TaxEngine.ComputeOldRegimeSlabs(inc, req.TaxpayerCategory).TotalTax);

        decimal cess = Math.Round((taxAfterRebate + surcharge) * 0.04m, 0, MidpointRounding.AwayFromZero);
        decimal grossNormalTax = taxAfterRebate + surcharge + cess;

        // 11. AMT u/s 115JC under Old Regime (ITR-3 when 10AA / 35AD > 0 and ATI > 20L)
        decimal amtTotalTax = 0m;
        bool isAmtApplied = false;
        if (!isNewRegime && req.ReturnFormType == "ITR-3" && (req.Deduction10AA > 0m || req.Deduction35AD > 0m))
        {
            decimal ati = totalTaxable + req.Deduction10AA + req.Deduction35AD;
            if (ati > 2_000_000m)
            {
                decimal amtBase = Math.Round(ati * 0.185m, 0, MidpointRounding.AwayFromZero);
                var (amtSc, _) = TaxEngine.CalculateSurchargeWithMarginalRelief(ati, amtBase, false, inc => Math.Round(inc * 0.185m, 0));
                decimal amtCess = Math.Round((amtBase + amtSc) * 0.04m, 0, MidpointRounding.AwayFromZero);
                amtTotalTax = amtBase + amtSc + amtCess;
                if (amtTotalTax > grossNormalTax) isAmtApplied = true;
            }
        }

        decimal effectiveTax = Math.Max(grossNormalTax, amtTotalTax);
        decimal intFee = Math.Max(0m, req.Interest234A) + Math.Max(0m, req.Interest234B) + Math.Max(0m, req.Interest234C) + Math.Max(0m, req.LateFee234F);
        decimal totalLiab = effectiveTax + intFee;
        decimal prepaid = Math.Max(0m, req.TdsSalary) + Math.Max(0m, req.TdsOtherThanSalary) + Math.Max(0m, req.TcsPaid)
            + Math.Max(0m, req.AdvanceTaxPaid) + Math.Max(0m, req.SelfAssessmentTaxPaid);

        return new PersonalRegimeBreakdownDto(
            Regime: isNewRegime ? "new" : "old",
            GrossSalary: Math.Round(req.GrossSalary, 0),
            SalaryDeductions: Math.Round(salaryDed, 0),
            NetSalary: Math.Round(netSalary, 0),
            HousePropertyIncome: Math.Round(hpNet, 0),
            BusinessBookProfit: Math.Round(bookPbt, 0),
            BusinessDisallowances: Math.Round(totalDisallow, 0),
            DepreciationBooks: Math.Round(req.DepreciationPerBooks, 0),
            DepreciationIncomeTax32: Math.Round(req.IncomeTaxDepreciationSec32, 0),
            BusinessIncomeNetPgbp: Math.Round(pgbpNet, 0),
            Stcg111A: Math.Round(stcg111a, 0),
            StcgNormal: Math.Round(stcgNorm, 0),
            Ltcg112A: Math.Round(ltcg112a, 0),
            Ltcg112: Math.Round(ltcg112, 0),
            VdaIncome115BBH: Math.Round(vda, 0),
            TotalCapitalGainsAndVda: Math.Round(totCgAndVda, 0),
            OtherSourcesIncome: Math.Round(ifos, 0),
            GrossTotalIncome: Math.Round(gti, 0),
            ChapterViaDeductions: Math.Round(chapVia, 0),
            TaxableIncome: Math.Round(totalTaxable, 0),
            SlabTaxOnNormalIncome: Math.Round(slabTax, 0),
            TaxOnStcg111A: tax111a,
            TaxOnLtcg112A: tax112a,
            TaxOnLtcg112: tax112,
            TaxOnVda115BBH: taxVda,
            SpecialRateTax: Math.Round(specialTax, 0),
            Rebate87A: Math.Round(rebate87A, 0),
            Surcharge: Math.Round(surcharge, 0),
            Cess: Math.Round(cess, 0),
            AmtTotalTax115JC: amtTotalTax,
            IsAmtApplied: isAmtApplied,
            InterestAndFee: Math.Round(intFee, 0),
            TotalTaxLiability: Math.Round(totalLiab, 0),
            PrepaidTaxes: Math.Round(prepaid, 0),
            NetPayableOrRefund: Math.Round(totalLiab - prepaid, 0),
            SlabBreakdown: slabs
        );
    }

    private static decimal ComputeHouseProperty(CalculatePersonalExtendedTaxRequest req, bool isNewRegime)
    {
        if (req.HouseProperties is { Count: > 0 })
        {
            decimal totalHp = 0m;
            foreach (var hp in req.HouseProperties)
            {
                decimal share = Math.Clamp(hp.AssesseeSharePercent, 1m, 100m) / 100m;
                if (hp.UsageType == "S")
                {
                    if (!isNewRegime && hp.LoanInterest > 0m)
                        totalHp -= Math.Min(200_000m, hp.LoanInterest * share);
                }
                else
                {
                    decimal nav = Math.Max(0m, (hp.AnnualRent - hp.MunicipalTaxes) * share);
                    decimal net = nav - nav * 0.30m - hp.LoanInterest * share;
                    totalHp += isNewRegime ? Math.Max(0m, net) : net;
                }
            }
            if (isNewRegime) return Math.Max(0m, totalHp);
            return totalHp < 0m ? -Math.Min(200_000m, Math.Abs(totalHp)) : totalHp;
        }

        if (req.IsSelfOccupied)
            return !isNewRegime && req.HousingLoanInterest > 0m ? -Math.Min(200_000m, req.HousingLoanInterest) : 0m;

        if (req.AnnualRentReceived > 0m)
        {
            decimal nav = Math.Max(0m, req.AnnualRentReceived - req.MunicipalTaxesPaid);
            decimal net = nav - nav * 0.30m - req.HousingLoanInterest;
            return isNewRegime ? Math.Max(0m, net) : (net < 0m ? -Math.Min(200_000m, Math.Abs(net)) : net);
        }
        return 0m;
    }
}
