namespace CaDesk.Application.Tax;

public static class TaxEngine
{
    public static RegimeComparisonDto ComputeComparison(CalculateTaxRequest req)
    {
        var oldResult = ComputeRegime(req, isNewRegime: false);
        var newResult = ComputeRegime(req, isNewRegime: true);

        string recommended;
        decimal savings;
        string reason;

        if (newResult.TotalTaxLiability < oldResult.TotalTaxLiability)
        {
            recommended = "new";
            savings = oldResult.TotalTaxLiability - newResult.TotalTaxLiability;
            var rules = NewRegimeRules.For(req.FinancialYear, req.AssessmentYear);
            reason = $"New Tax Regime saves ₹{savings:N0} in tax due to higher standard deduction (₹75,000), concessional tax slab rates, and ₹{rules.RebateIncomeLimit:N0} zero-tax threshold u/s 87A.";
        }
        else if (oldResult.TotalTaxLiability < newResult.TotalTaxLiability)
        {
            recommended = "old";
            savings = newResult.TotalTaxLiability - oldResult.TotalTaxLiability;
            reason = $"Old Tax Regime saves ₹{savings:N0} in tax because your total deductions under Section 80C, 80D, HRA, and home loan interest exceed the break-even deduction threshold.";
        }
        else
        {
            recommended = "new";
            savings = 0;
            reason = "Both regimes result in the exact same tax liability. New Regime is recommended for simpler compliance and no need to preserve deduction receipts.";
        }

        return new RegimeComparisonDto(oldResult, newResult, recommended, savings, reason);
    }

    public static RegimeTaxResult ComputeRegime(CalculateTaxRequest req, bool isNewRegime)
    {
        // 1. Salary Head
        decimal stdDeduction = 0;
        decimal hraExemption = 0;
        decimal profTax = 0;

        if (req.GrossSalary > 0)
        {
            if (isNewRegime)
            {
                stdDeduction = Math.Min(req.GrossSalary, 75000m); // Budget 2024 New Regime std deduction ₹75,000
            }
            else
            {
                stdDeduction = Math.Min(req.GrossSalary, 50000m); // Old Regime std deduction ₹50,000

                // HRA Exemption u/s 10(13A)
                if (req.HraReceived > 0 && req.RentPaid > 0)
                {
                    decimal basicEst = req.GrossSalary * 0.50m;
                    decimal rentMinus10Percent = Math.Max(0, req.RentPaid - (basicEst * 0.10m));
                    decimal percentOfSalary = basicEst * (req.IsMetro ? 0.50m : 0.40m);
                    hraExemption = Math.Min(req.HraReceived, Math.Min(rentMinus10Percent, percentOfSalary));
                }

                profTax = Math.Min(2500m, req.ProfessionalTax);
            }
        }

        decimal salaryDeductions = stdDeduction + hraExemption + profTax;
        decimal netSalary = Math.Max(0, req.GrossSalary - salaryDeductions);

        // 2. House Property Head
        decimal hpIncome = 0;
        if (req.IsSelfOccupied)
        {
            // Self-occupied: interest on housing loan
            if (!isNewRegime && req.HousingLoanInterest > 0)
            {
                // Old regime allows up to ₹2,00,000 interest deduction on self-occupied
                hpIncome = -Math.Min(200000m, req.HousingLoanInterest);
            }
            // New regime does NOT allow self-occupied home loan interest loss
        }
        else if (req.AnnualRentReceived > 0)
        {
            // Let-out property
            decimal nav = Math.Max(0, req.AnnualRentReceived - req.MunicipalTaxesPaid);
            decimal statutory30Percent = nav * 0.30m;
            decimal letOutHp = nav - statutory30Percent - req.HousingLoanInterest;

            if (isNewRegime)
            {
                // Under new regime, loss under HP cannot be set off against other heads
                hpIncome = Math.Max(0, letOutHp);
            }
            else
            {
                // Old regime caps HP loss set-off against other heads at ₹2,00,000
                hpIncome = letOutHp < 0 ? -Math.Min(200000m, Math.Abs(letOutHp)) : letOutHp;
            }
        }

        // 3. Business / Profession Head (PGBP)
        decimal bizIncome = 0;
        if (req.PresumptiveSection == "44AD" && req.BusinessGrossTurnover > 0)
        {
            bizIncome = req.BusinessGrossTurnover * 0.06m; // 6% digital turnover presumptive
        }
        else if (req.PresumptiveSection == "44ADA" && req.BusinessGrossTurnover > 0)
        {
            bizIncome = req.BusinessGrossTurnover * 0.50m; // 50% professional presumptive
        }
        else
        {
            bizIncome = req.NetProfit;
        }

        // 4. Capital Gains Head
        decimal stcgSpecial = Math.Max(0, req.Stcg111A);
        decimal stcgNormal = Math.Max(0, req.StcgNormal);
        decimal ltcg112A = Math.Max(0, req.Ltcg112A);
        decimal ltcg112 = Math.Max(0, req.Ltcg112);

        decimal totalStcg = stcgSpecial + stcgNormal;
        decimal totalLtcg = ltcg112A + ltcg112;

        // 5. Other Sources Head (IFOS)
        decimal ifos = Math.Max(0, req.SavingsInterest) + Math.Max(0, req.FdInterest) +
                       Math.Max(0, req.DividendIncome) + Math.Max(0, req.OtherIncome);

        // Gross Total Income
        decimal ordinaryIncome = netSalary + hpIncome + bizIncome + stcgNormal + ifos;
        decimal grossTotalIncome = Math.Max(0, ordinaryIncome + stcgSpecial + totalLtcg);

        // 6. Chapter VI-A Deductions
        decimal chapterVia = 0;
        if (!isNewRegime)
        {
            decimal d80C = Math.Min(150000m, Math.Max(0, req.Section80C));
            decimal d80Ccd1B = Math.Min(50000m, Math.Max(0, req.Section80Ccd1B));
            decimal d80D = Math.Min(75000m, Math.Max(0, req.Section80D)); // self + senior parents
            decimal d80E = Math.Max(0, req.Section80E); // no upper limit
            decimal d80G = Math.Max(0, req.Section80G);
            decimal d80Tta = Math.Min(10000m, Math.Max(0, req.Section80Tta));

            decimal claimed = d80C + d80Ccd1B + d80D + d80E + d80G + d80Tta;
            // Chapter VI-A deductions cannot exceed ordinary income
            chapterVia = Math.Min(Math.Max(0, ordinaryIncome), claimed);
        }

        // Taxable Normal Income
        decimal taxableNormalIncome = Math.Max(0, ordinaryIncome - chapterVia);
        decimal totalTaxableIncome = taxableNormalIncome + stcgSpecial + totalLtcg;

        // 7. Base Slab Tax Computation
        var newRules = NewRegimeRules.For(req.FinancialYear, req.AssessmentYear);
        var (slabTax, slabs) = isNewRegime
            ? ComputeNewRegimeSlabs(taxableNormalIncome, newRules)
            : ComputeOldRegimeSlabs(taxableNormalIncome, req.TaxpayerCategory ?? "individual");

        // 8. Special Rate Taxes (Capital Gains)
        // STCG 111A: 20% for transfers from 23 July 2024 (Finance (No. 2) Act 2024), 15% before. FY 2024-25 straddles
        // the change and this engine has no transfer date, so that year keeps 15%; FY 2025-26 onward is 20%.
        decimal stcgTax = Math.Round(stcgSpecial * newRules.Stcg111ARate, 2); // same rate in both regimes

        // LTCG 112A: 12.5% on gains exceeding ₹1,25,000
        decimal ltcg112ATaxable = Math.Max(0, ltcg112A - 125000m);
        decimal ltcg112ATax = Math.Round(ltcg112ATaxable * 0.125m, 2);

        // LTCG 112: 12.5% / 20%
        decimal ltcg112Tax = Math.Round(ltcg112 * 0.125m, 2);

        decimal specialTax = stcgTax + ltcg112ATax + ltcg112Tax;
        decimal baseTax = slabTax;
        decimal totalTaxBeforeRebate = baseTax + specialTax;

        // 9. Rebate u/s 87A
        decimal rebate87A = 0;
        if (isNewRegime)
        {
            // New Regime 87A (Sec 115BAC(1A)): full rebate up to the year's limit when total income is within the
            // threshold (FY 2025-26 onward: ₹12,00,000 / ₹60,000; earlier: ₹7,00,000 / ₹25,000), with marginal relief
            // just above it so the tax never exceeds the income above the threshold.
            // From FY 2025-26 the rebate is not available against tax on special-rate income (111A / 112 / 112A).
            decimal rebateBase = newRules.RebateExcludesSpecialRateTax ? slabTax : totalTaxBeforeRebate;
            if (totalTaxableIncome <= newRules.RebateIncomeLimit)
            {
                rebate87A = Math.Min(newRules.MaxRebate, rebateBase);
            }
            else
            {
                decimal incomeAboveLimit = totalTaxableIncome - newRules.RebateIncomeLimit;
                if (rebateBase > incomeAboveLimit) rebate87A = rebateBase - incomeAboveLimit; // marginal relief
            }
        }
        else
        {
            // Old Regime 87A: If total taxable income <= ₹5,00,000, 100% rebate (up to ₹12,500)
            if (totalTaxableIncome <= 500000m)
            {
                rebate87A = Math.Min(12500m, totalTaxBeforeRebate);
            }
        }

        decimal taxAfterRebate = Math.Max(0, totalTaxBeforeRebate - rebate87A);

        // 10. Surcharge
        decimal surchargeRate = 0;
        if (totalTaxableIncome > 50000000m) surchargeRate = isNewRegime ? 0.25m : 0.37m; // > 5 Cr: 37% old regime, capped at 25% new regime
        else if (totalTaxableIncome > 20000000m) surchargeRate = 0.25m; // > 2 Cr
        else if (totalTaxableIncome > 10000000m) surchargeRate = 0.15m; // > 1 Cr
        else if (totalTaxableIncome > 5000000m) surchargeRate = 0.10m;  // > 50 Lakhs

        decimal surcharge = Math.Round(taxAfterRebate * surchargeRate, 2);

        // 11. Health & Education Cess (4%)
        decimal cess = Math.Round((taxAfterRebate + surcharge) * 0.04m, 2);
        decimal totalLiability = taxAfterRebate + surcharge + cess;

        // 12. Prepaid Taxes offset
        decimal prepaid = req.TdsPaid + req.TcsPaid + req.AdvanceTaxPaid + req.SelfAssessmentTaxPaid;
        decimal netPayableOrRefund = totalLiability - prepaid;

        return new RegimeTaxResult(
            isNewRegime ? "new" : "old",
            req.GrossSalary,
            stdDeduction,
            netSalary,
            hpIncome,
            bizIncome,
            totalStcg,
            totalLtcg,
            ifos,
            grossTotalIncome,
            chapterVia,
            totalTaxableIncome,
            baseTax,
            specialTax,
            rebate87A,
            surcharge,
            cess,
            totalLiability,
            prepaid,
            netPayableOrRefund,
            slabs
        );
    }

    private static (decimal TotalTax, List<TaxSlabBreakdown> Slabs) ComputeNewRegimeSlabs(decimal income, NewRegimeRules rules)
    {
        var slabs = new List<TaxSlabBreakdown>();
        decimal tax = 0;
        decimal lower = 0;
        foreach (var (upper, ratePct) in rules.Slabs)
        {
            if (income <= lower && lower > 0) break;
            decimal inSlab = Math.Max(0, Math.Min(income, upper ?? decimal.MaxValue) - lower);
            decimal slabTax = inSlab * ratePct / 100m;
            tax += slabTax;
            var label = upper is null ? $"Above ₹{Inr(lower)}" : lower == 0 ? $"₹0 - ₹{Inr(upper.Value)}" : $"₹{Inr(lower + 1)} - ₹{Inr(upper.Value)}";
            slabs.Add(new(label, ratePct, inSlab, slabTax));
            if (upper is null) break;
            lower = upper.Value;
        }
        return (tax, slabs);
    }

    private static readonly System.Globalization.CultureInfo IndianFormat = System.Globalization.CultureInfo.GetCultureInfo("en-IN");

    /// <summary>Indian digit grouping: 300001 → "3,00,001".</summary>
    private static string Inr(decimal amount) => amount.ToString("N0", IndianFormat);

    /// <summary>
    /// New-regime (Sec 115BAC(1A)) slabs and 87A rebate, plus the 111A STCG rate (both regimes), for a financial year. Rules are chosen by year, so
    /// computations for earlier years (revised / belated returns, notices) keep the law that applied then.
    /// </summary>
    public sealed record NewRegimeRules(
        IReadOnlyList<(decimal? UpperLimit, decimal RatePct)> Slabs,
        decimal RebateIncomeLimit, decimal MaxRebate, bool RebateExcludesSpecialRateTax,
        decimal Stcg111ARate)
    {
        // Finance Act 2025: FY 2025-26 onward (the Income-tax Act, 2025 keeps these rates from tax year 2026-27)
        private static readonly NewRegimeRules Fy2025 = new(
            [(400000m, 0m), (800000m, 5m), (1200000m, 10m), (1600000m, 15m), (2000000m, 20m), (2400000m, 25m), (null, 30m)],
            RebateIncomeLimit: 1200000m, MaxRebate: 60000m, RebateExcludesSpecialRateTax: true, Stcg111ARate: 0.20m);

        // Finance (No. 2) Act 2024: FY 2024-25
        private static readonly NewRegimeRules Fy2024 = new(
            [(300000m, 0m), (700000m, 5m), (1000000m, 10m), (1200000m, 15m), (1500000m, 20m), (null, 30m)],
            RebateIncomeLimit: 700000m, MaxRebate: 25000m, RebateExcludesSpecialRateTax: false, Stcg111ARate: 0.15m);

        public static NewRegimeRules For(string? financialYear, string? assessmentYear)
        {
            int? fyStart = StartYear(financialYear) ?? (StartYear(assessmentYear) - 1);
            return fyStart is null or >= 2025 ? Fy2025 : Fy2024;
        }

        /// <summary>"2025-26" → 2025.</summary>
        private static int? StartYear(string? year) =>
            year is { Length: >= 4 } && int.TryParse(year[..4], out var y) ? y : null;
    }

    private static (decimal TotalTax, List<TaxSlabBreakdown> Slabs) ComputeOldRegimeSlabs(decimal income, string category)
    {
        // Old Regime Slabs:
        // Exemption limit:
        // Individual < 60: ₹2,50,000
        // Senior Citizen (60-80): ₹3,00,000
        // Super Senior (80+): ₹5,00,000

        decimal basicExemption = category switch
        {
            "super_senior" => 500000m,
            "senior_citizen" => 300000m,
            _ => 250000m
        };

        var slabs = new List<TaxSlabBreakdown>();
        decimal tax = 0;

        // Slab 1: 0 to basic exemption
        decimal s1 = Math.Min(income, basicExemption);
        slabs.Add(new($"Up to ₹{basicExemption:N0}", 0, s1, 0));

        // Slab 2: basic exemption to 5L (5%)
        if (income > basicExemption && basicExemption < 500000m)
        {
            decimal s2 = Math.Min(income - basicExemption, 500000m - basicExemption);
            decimal t2 = s2 * 0.05m;
            tax += t2;
            slabs.Add(new($"₹{basicExemption + 1:N0} - ₹5,00,000", 5, s2, t2));
        }

        // Slab 3: 5L to 10L (20%)
        if (income > 500000m)
        {
            decimal s3 = Math.Min(income - 500000m, 500000m);
            decimal t3 = s3 * 0.20m;
            tax += t3;
            slabs.Add(new("₹5,00,001 - ₹10,00,000", 20, s3, t3));
        }

        // Slab 4: Above 10L (30%)
        if (income > 1000000m)
        {
            decimal s4 = income - 1000000m;
            decimal t4 = s4 * 0.30m;
            tax += t4;
            slabs.Add(new("Above ₹10,00,000", 30, s4, t4));
        }

        return (tax, slabs);
    }
}
