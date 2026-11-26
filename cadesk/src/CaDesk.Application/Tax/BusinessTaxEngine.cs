namespace CaDesk.Application.Tax;

/// <summary>
/// Statutory Indian Direct Tax engine for Partnership Firms / LLPs / AOPs (ITR-5)
/// and Domestic Companies (ITR-6) for AY 2026-27 (FY 2025-26) and AY 2025-26.
/// </summary>
public static class BusinessTaxEngine
{
    public const string RegimeFirmNormal = "FIRM_NORMAL";
    public const string RegimeCorpNormal = "CORP_NORMAL";
    public const string RegimeCorp115Baa = "CORP_115BAA";
    public const string RegimeCorp115Bab = "CORP_115BAB";

    public static BusinessTaxComputationResultDto Compute(CalculateBusinessTaxRequest rawReq)
    {
        var req = NormalizeDepreciationAndPartnerTotals(rawReq);
        bool isFirm = string.Equals(req.EntityType, "firm", StringComparison.OrdinalIgnoreCase);

        // 1. Book Profit & P&L totals
        decimal totalRevenue = req.RevenueFromOperations + req.OtherIncome + req.InterestIncome + req.DividendIncome + req.RentIncome;
        decimal expensesBeforeTax = req.PurchasesAndDirectCost
            + req.ChangesInInventories
            + req.EmployeeBenefitExpense
            + req.PartnerRemunerationDebited
            + req.FinanceCost
            + req.PartnerInterestDebited
            + req.DepreciationPerBooks
            + req.AuditFee
            + req.OtherExpenses;
        decimal bookProfitBeforeTax = totalRevenue - expensesBeforeTax;
        decimal bookProfitAfterTax = bookProfitBeforeTax - req.TaxProvisionCurrent - req.TaxProvisionDeferred;

        // 2. Non-business incomes credited in P&L (excluded from PGBP so they are taxed under HP / CG / OS)
        decimal excludedOtherHeadsInPl = req.RentIncome + req.DividendIncome + req.InterestIncome;

        // 3. Disallowances & Section 40(b) for Firms
        decimal disallowedInterest40b = 0m;
        decimal firmBookProfit40b = 0m;
        decimal maxAllowableRemun40bv = 0m;
        decimal allowableRemun40b = 0m;
        decimal disallowedRemun40b = 0m;

        decimal statutoryDisallowances = req.DisallowSec36
            + req.DisallowSec37
            + req.DisallowSec40a
            + req.DisallowSec40A3
            + req.DisallowSec43B
            + req.DisallowSec43BhMsme
            + req.OtherDisallowances;

        if (isFirm)
        {
            disallowedInterest40b = ComputeFirmInterestDisallowance40b(req);
            // Book Profit u/s 40(b) = Net Profit + Partner Remuneration + Book Dep - IT Act Dep + statutory disallowances - excluded non-business incomes
            firmBookProfit40b = bookProfitBeforeTax
                + req.PartnerRemunerationDebited
                + req.DepreciationPerBooks
                - req.IncomeTaxDepreciationSec32
                + statutoryDisallowances
                + disallowedInterest40b
                - excludedOtherHeadsInPl
                - req.AllowableDeductionsUs32Etc;

            // Finance (No. 2) Act 2024 amended Sec 40(b)(v) (applicable FY 2024-25 / AY 2025-26 onward):
            // 90% on first Rs 6,00,000 of book profit or Rs 3,00,000 whichever is more + 60% on balance book profit
            maxAllowableRemun40bv = ComputeSection40bvCeiling(firmBookProfit40b);
            allowableRemun40b = Math.Min(Math.Max(0m, req.PartnerRemunerationDebited), maxAllowableRemun40bv);
            disallowedRemun40b = Math.Max(0m, req.PartnerRemunerationDebited - allowableRemun40b);
        }

        decimal totalDisallowances = statutoryDisallowances + disallowedInterest40b + disallowedRemun40b;

        // 4. PGBP Net Income
        decimal pgbpBeforeLosses = bookProfitBeforeTax
            + req.DepreciationPerBooks
            + totalDisallowances
            - req.IncomeTaxDepreciationSec32
            - excludedOtherHeadsInPl
            - req.AllowableDeductionsUs32Etc;

        decimal totalBfLossAvailable = Math.Max(0m, req.BroughtForwardBusinessLoss) + Math.Max(0m, req.UnabsorbedDepreciation);
        decimal bfLossSetOff = pgbpBeforeLosses > 0m ? Math.Min(pgbpBeforeLosses, totalBfLossAvailable) : 0m;
        decimal netPgbp = pgbpBeforeLosses - bfLossSetOff;

        // 5. Other Heads
        decimal hp = req.HousePropertyIncome;
        decimal cgStcg = Math.Max(0m, req.Stcg111A) + Math.Max(0m, req.StcgNormal);
        decimal cgLtcg = Math.Max(0m, req.Ltcg112A) + Math.Max(0m, req.Ltcg112);
        decimal cgTotal = cgStcg + cgLtcg;
        decimal otherSources = Math.Max(0m, req.OtherSourcesIncome);
        decimal gti = Math.Max(0m, netPgbp + hp + cgTotal + otherSources);

        decimal normalGti = Math.Max(0m, gti - req.Stcg111A - req.Ltcg112A - req.Ltcg112);
        decimal chapViaAll = Math.Min(normalGti, Math.Max(0m, req.Deduction80G) + Math.Max(0m, req.Deduction80Jjaa) + Math.Max(0m, req.Deduction80M) + Math.Max(0m, req.OtherChapViaDeductions));
        decimal chapVia115Baa = Math.Min(normalGti, Math.Max(0m, req.Deduction80Jjaa) + Math.Max(0m, req.Deduction80M));

        decimal matBookProfit = ComputeMatBookProfit115JB(req, bookProfitAfterTax);
        decimal amtAdjustedTi = RoundSec288A(Math.Max(0m, gti - chapViaAll) + Math.Max(0m, req.DeductionSec10AA) + Math.Max(0m, req.DeductionSec35AD));
        decimal interestFee = Math.Max(0m, req.Interest234A) + Math.Max(0m, req.Interest234B) + Math.Max(0m, req.Interest234C) + Math.Max(0m, req.LateFee234F);
        decimal prepaid = Math.Max(0m, req.AdvanceTaxPaid) + Math.Max(0m, req.TdsCredit) + Math.Max(0m, req.TcsCredit) + Math.Max(0m, req.SelfAssessmentTaxPaid);

        if (isFirm)
        {
            var firmSummary = ComputeFirmSummary(req, gti, chapViaAll, amtAdjustedTi, interestFee, prepaid);
            return new BusinessTaxComputationResultDto(
                EntityType: "firm",
                SubType: req.SubType,
                AssessmentYear: req.AssessmentYear,
                FinancialYear: req.FinancialYear,
                TotalRevenue: RoundR(totalRevenue),
                TotalExpensesBeforeTax: RoundR(expensesBeforeTax),
                BookProfitBeforeTax: RoundR(bookProfitBeforeTax),
                BookProfitAfterTax: RoundR(bookProfitAfterTax),
                TotalDisallowancesAddedBack: RoundR(totalDisallowances),
                FirmBookProfitUs40b: RoundR(firmBookProfit40b),
                MaxAllowableRemuneration40bv: RoundR(maxAllowableRemun40bv),
                AllowableRemuneration40b: RoundR(allowableRemun40b),
                DisallowedRemuneration40b: RoundR(disallowedRemun40b),
                DisallowedPartnerInterest40b: RoundR(disallowedInterest40b),
                DepreciationPerBooks: RoundR(req.DepreciationPerBooks),
                IncomeTaxDepreciationSec32: RoundR(req.IncomeTaxDepreciationSec32),
                OtherHeadIncomesExcludedFromPl: RoundR(excludedOtherHeadsInPl),
                ComputedBusinessIncomeBeforeLoss: RoundR(pgbpBeforeLosses),
                BroughtForwardLossSetOff: RoundR(bfLossSetOff),
                NetPgbpIncome: RoundR(netPgbp),
                HousePropertyIncome: RoundR(hp),
                CapitalGainsTotal: RoundR(cgTotal),
                OtherSourcesIncome: RoundR(otherSources),
                GrossTotalIncome: RoundR(gti),
                ChapterViaAllowed: RoundR(chapViaAll),
                TotalTaxableIncome: firmSummary.TaxableIncome,
                MatBookProfit115JB: 0m,
                AmtAdjustedTotalIncome115JC: RoundR(amtAdjustedTi),
                SelectedRegimeCode: RegimeFirmNormal,
                RecommendedRegimeCode: RegimeFirmNormal,
                SavingsVsAlternative: 0m,
                ActiveSummary: firmSummary,
                Alternatives: [firmSummary],
                NormalizedInput: req
            );
        }

        // Company: compare Normal vs Section 115BAA vs Section 115BAB
        var normalCorp = ComputeCompanyNormalSummary(req, gti, chapViaAll, matBookProfit, interestFee, prepaid);
        var baaCorp = ComputeCompanyConcessionalSummary(req, RegimeCorp115Baa, "Section 115BAA (22% + 10% SC)", 0.22m, gti, chapVia115Baa, interestFee, prepaid);
        var babCorp = ComputeCompanyConcessionalSummary(req, RegimeCorp115Bab, "Section 115BAB (15% Mfg + 10% SC)", 0.15m, gti, chapVia115Baa, interestFee, prepaid);

        var alternatives = new List<BusinessRegimeSummaryDto> { normalCorp, baaCorp, babCorp };

        // Recommended between widely available Normal & 115BAA (include 115BAB only if selected)
        string recommendedCode = baaCorp.TotalTaxLiability <= normalCorp.TotalTaxLiability ? RegimeCorp115Baa : RegimeCorpNormal;
        string selectedCode = req.CorporateRegime?.ToUpperInvariant() switch
        {
            "NORMAL" => RegimeCorpNormal,
            "115BAA" => RegimeCorp115Baa,
            "115BAB" => RegimeCorp115Bab,
            _ => recommendedCode
        };

        var active = alternatives.First(a => a.RegimeCode == selectedCode);
        decimal altBaseline = selectedCode == RegimeCorpNormal ? baaCorp.TotalTaxLiability : normalCorp.TotalTaxLiability;
        decimal savings = Math.Max(0m, altBaseline - active.TotalTaxLiability);
        decimal activeChapVia = selectedCode == RegimeCorpNormal ? chapViaAll : chapVia115Baa;

        return new BusinessTaxComputationResultDto(
            EntityType: "company",
            SubType: req.SubType,
            AssessmentYear: req.AssessmentYear,
            FinancialYear: req.FinancialYear,
            TotalRevenue: RoundR(totalRevenue),
            TotalExpensesBeforeTax: RoundR(expensesBeforeTax),
            BookProfitBeforeTax: RoundR(bookProfitBeforeTax),
            BookProfitAfterTax: RoundR(bookProfitAfterTax),
            TotalDisallowancesAddedBack: RoundR(totalDisallowances),
            FirmBookProfitUs40b: 0m,
            MaxAllowableRemuneration40bv: 0m,
            AllowableRemuneration40b: 0m,
            DisallowedRemuneration40b: 0m,
            DisallowedPartnerInterest40b: 0m,
            DepreciationPerBooks: RoundR(req.DepreciationPerBooks),
            IncomeTaxDepreciationSec32: RoundR(req.IncomeTaxDepreciationSec32),
            OtherHeadIncomesExcludedFromPl: RoundR(excludedOtherHeadsInPl),
            ComputedBusinessIncomeBeforeLoss: RoundR(pgbpBeforeLosses),
            BroughtForwardLossSetOff: RoundR(bfLossSetOff),
            NetPgbpIncome: RoundR(netPgbp),
            HousePropertyIncome: RoundR(hp),
            CapitalGainsTotal: RoundR(cgTotal),
            OtherSourcesIncome: RoundR(otherSources),
            GrossTotalIncome: RoundR(gti),
            ChapterViaAllowed: RoundR(activeChapVia),
            TotalTaxableIncome: active.TaxableIncome,
            MatBookProfit115JB: RoundR(matBookProfit),
            AmtAdjustedTotalIncome115JC: 0m,
            SelectedRegimeCode: selectedCode,
            RecommendedRegimeCode: recommendedCode,
            SavingsVsAlternative: RoundR(savings),
            ActiveSummary: active,
            Alternatives: alternatives,
            NormalizedInput: req
        );
    }

    public static decimal ComputeSection40bvCeiling(decimal bookProfit)
    {
        if (bookProfit <= 600_000m)
            return Math.Max(300_000m, RoundR(Math.Max(0m, bookProfit) * 0.90m));

        return 540_000m + RoundR((bookProfit - 600_000m) * 0.60m);
    }

    private static CalculateBusinessTaxRequest NormalizeDepreciationAndPartnerTotals(CalculateBusinessTaxRequest req)
    {
        decimal remun = req.PartnerRemunerationDebited;
        decimal pInterest = req.PartnerInterestDebited;
        decimal itDep = req.IncomeTaxDepreciationSec32;

        if (req.Partners is { Count: > 0 })
        {
            if (remun == 0m) remun = req.Partners.Sum(p => Math.Max(0m, p.RemunerationPaid));
            if (pInterest == 0m) pInterest = req.Partners.Sum(p => Math.Max(0m, p.InterestPaid));
        }

        if (req.DepreciationBlocks is { Count: > 0 } && itDep == 0m)
        {
            itDep = req.DepreciationBlocks.Sum(ComputeBlockDepreciation);
        }

        decimal advTax = req.AdvanceTaxPaid;
        if (req.AdvanceTaxChallans is { Count: > 0 } && advTax == 0m)
            advTax = req.AdvanceTaxChallans.Sum(c => Math.Max(0m, c.Amount));

        decimal tds = req.TdsCredit;
        if (req.TdsCreditEntries is { Count: > 0 } && tds == 0m)
            tds = req.TdsCreditEntries.Sum(t => Math.Max(0m, t.TdsClaimed));

        return req with
        {
            PartnerRemunerationDebited = remun,
            PartnerInterestDebited = pInterest,
            IncomeTaxDepreciationSec32 = itDep,
            AdvanceTaxPaid = advTax,
            TdsCredit = tds,
        };
    }

    public static decimal ComputeBlockDepreciation(DepreciationBlockInput b)
    {
        if (b.ExplicitTaxDepreciation.HasValue) return Math.Max(0m, b.ExplicitTaxDepreciation.Value);
        decimal rate = Math.Max(0m, b.RatePercent) / 100m;
        decimal afterOpeningAdd = Math.Max(0m, b.OpeningWdv + b.AdditionsMoreThan180Days - b.Deletions);
        decimal fullBase = afterOpeningAdd;
        decimal halfBase = Math.Max(0m, b.AdditionsLessThan180Days);
        decimal remainingAfterFull = afterOpeningAdd + b.AdditionsLessThan180Days;
        if (b.AdditionsLessThan180Days < b.Deletions && b.Deletions > b.OpeningWdv + b.AdditionsMoreThan180Days)
            halfBase = Math.Max(0m, remainingAfterFull);

        return RoundR(fullBase * rate + halfBase * (rate * 0.5m) + Math.Max(0m, b.AdditionalDepreciation));
    }

    private static decimal ComputeFirmInterestDisallowance40b(CalculateBusinessTaxRequest req)
    {
        if (req.Partners is { Count: > 0 })
        {
            decimal disallow = 0m;
            foreach (var p in req.Partners)
            {
                if (p.InterestPaid <= 0m) continue;
                decimal maxAt12 = p.CapitalBalance > 0m
                    ? RoundR(p.CapitalBalance * 0.12m)
                    : (p.InterestRatePercent > 12m ? RoundR(p.InterestPaid * 12m / p.InterestRatePercent) : p.InterestPaid);
                disallow += Math.Max(0m, p.InterestPaid - maxAt12);
            }
            return disallow;
        }
        return 0m;
    }

    private static decimal ComputeMatBookProfit115JB(CalculateBusinessTaxRequest req, decimal bookProfitAfterTax)
    {
        decimal additions = Math.Max(0m, req.TaxProvisionCurrent)
            + Math.Max(0m, req.TaxProvisionDeferred)
            + Math.Max(0m, req.MatAdditionsOther);
        decimal deductions = Math.Max(0m, Math.Min(req.BroughtForwardBusinessLoss, req.UnabsorbedDepreciation))
            + Math.Max(0m, req.MatDeductionsOther);
        return Math.Max(0m, bookProfitAfterTax + additions - deductions);
    }

    private static (decimal TaxAtNormal, decimal TaxAtSpecial) ComputeSpecialAndNormalTaxes(
        decimal taxableIncome,
        CalculateBusinessTaxRequest req,
        decimal normalRate)
    {
        decimal stcg111a = Math.Min(taxableIncome, Math.Max(0m, req.Stcg111A));
        decimal remAfterStcg = Math.Max(0m, taxableIncome - stcg111a);
        decimal ltcg112a = Math.Min(remAfterStcg, Math.Max(0m, req.Ltcg112A));
        decimal ltcg112aTaxable = Math.Max(0m, ltcg112a - 125_000m); // Rs 1.25L exemption u/s 112A
        decimal remAfter112a = Math.Max(0m, remAfterStcg - ltcg112a);
        decimal ltcg112 = Math.Min(remAfter112a, Math.Max(0m, req.Ltcg112));
        decimal normalSlice = Math.Max(0m, taxableIncome - stcg111a - ltcg112a - ltcg112);

        decimal taxNormal = RoundR(normalSlice * normalRate);
        decimal taxSpecial = RoundR(stcg111a * 0.20m) + RoundR(ltcg112aTaxable * 0.125m) + RoundR(ltcg112 * 0.125m);
        return (taxNormal, taxSpecial);
    }

    private static BusinessRegimeSummaryDto ComputeFirmSummary(
        CalculateBusinessTaxRequest req,
        decimal gti,
        decimal chapVia,
        decimal amtAti,
        decimal interestFee,
        decimal prepaid)
    {
        decimal taxable = RoundSec288A(Math.Max(0m, gti - chapVia));
        var (taxNormal, taxSpecial) = ComputeSpecialAndNormalTaxes(taxable, req, 0.30m);
        decimal baseTax = taxNormal + taxSpecial;

        var (scRaw, mr, sc) = ComputeSingleTierSurcharge(taxable, baseTax, 10_000_000m, 0.12m);
        decimal cess = RoundR((baseTax + sc) * 0.04m);
        decimal grossRegular = baseTax + sc + cess;

        // AMT u/s 115JC applies if deductions u/s 10AA / 35AD / 80IA-P claimed and ATI > Rs 20L
        bool amtApplies = (req.DeductionSec10AA > 0m || req.DeductionSec35AD > 0m) && amtAti > 2_000_000m;
        decimal amtBase = amtApplies ? RoundR(amtAti * 0.185m) : 0m;
        var (_, _, amtSc) = amtApplies ? ComputeSingleTierSurcharge(amtAti, amtBase, 10_000_000m, 0.12m) : (0m, 0m, 0m);
        decimal amtCess = amtApplies ? RoundR((amtBase + amtSc) * 0.04m) : 0m;
        decimal amtTotal = amtBase + amtSc + amtCess;

        bool isAmtHigher = amtApplies && amtTotal > grossRegular;
        decimal effectiveGross = Math.Max(grossRegular, amtTotal);
        decimal amtUtilized = (!isAmtHigher && req.AmtCreditBroughtForward > 0m)
            ? Math.Min(req.AmtCreditBroughtForward, Math.Max(0m, grossRegular - amtTotal))
            : 0m;
        decimal amtGenerated = isAmtHigher ? Math.Max(0m, amtTotal - grossRegular) : 0m;
        decimal netTax = Math.Max(0m, effectiveGross - amtUtilized);
        decimal totalLiab = netTax + interestFee;

        return new BusinessRegimeSummaryDto(
            RegimeCode: RegimeFirmNormal,
            RegimeLabel: "Partnership / LLP (30% + 12% SC > ₹1 Cr)",
            TaxableIncome: taxable,
            TaxAtNormalRates: taxNormal,
            TaxAtSpecialRates: taxSpecial,
            BaseTax: baseTax,
            SurchargeBeforeMarginalRelief: scRaw,
            MarginalRelief: mr,
            Surcharge: sc,
            HealthAndEducationCess: cess,
            GrossTaxBeforeMatAmt: grossRegular,
            MatOrAmtBaseTax: amtBase,
            MatOrAmtSurcharge: amtSc,
            MatOrAmtCess: amtCess,
            MatOrAmtTotalTax: amtTotal,
            IsMatOrAmtApplied: isAmtHigher,
            MatOrAmtCreditUtilized: amtUtilized,
            MatOrAmtCreditGenerated: amtGenerated,
            InterestAndFee: interestFee,
            TotalTaxLiability: totalLiab,
            PrepaidTaxes: prepaid,
            NetTaxPayableOrRefund: totalLiab - prepaid,
            IsEligible: true,
            IneligibilityOrNotes: isAmtHigher
                ? "AMT u/s 115JC (18.5%) exceeds normal tax; difference carries forward as Sec 115JD credit."
                : "Sec 40(b) partner remuneration & 12% interest ceilings enforced."
        );
    }

    private static BusinessRegimeSummaryDto ComputeCompanyNormalSummary(
        CalculateBusinessTaxRequest req,
        decimal gti,
        decimal chapVia,
        decimal matBookProfit,
        decimal interestFee,
        decimal prepaid)
    {
        decimal rate = req.IsSmallTurnover25Percent ? 0.25m : 0.30m;
        decimal taxable = RoundSec288A(Math.Max(0m, gti - chapVia));
        var (taxNormal, taxSpecial) = ComputeSpecialAndNormalTaxes(taxable, req, rate);
        decimal baseTax = taxNormal + taxSpecial;

        var (scRaw, mr, sc) = ComputeTwoTierCompanySurcharge(taxable, baseTax);
        decimal cess = RoundR((baseTax + sc) * 0.04m);
        decimal grossRegular = baseTax + sc + cess;

        // MAT u/s 115JB @ 15% of book profit + 7%/12% surcharge + 4% HEC
        decimal matBase = matBookProfit > 0m ? RoundR(matBookProfit * 0.15m) : 0m;
        var (_, _, matSc) = ComputeTwoTierCompanySurcharge(matBookProfit, matBase);
        decimal matCess = RoundR((matBase + matSc) * 0.04m);
        decimal matTotal = matBase + matSc + matCess;

        bool isMatApplied = matTotal > grossRegular;
        decimal grossPayable = Math.Max(grossRegular, matTotal);
        decimal matCreditUsed = (!isMatApplied && req.MatCreditBroughtForward > 0m)
            ? Math.Min(req.MatCreditBroughtForward, Math.Max(0m, grossRegular - matTotal))
            : 0m;
        decimal matCreditGen = isMatApplied ? Math.Max(0m, matTotal - grossRegular) : 0m;
        decimal netTax = Math.Max(0m, grossPayable - matCreditUsed);
        decimal totalLiab = netTax + interestFee;

        return new BusinessRegimeSummaryDto(
            RegimeCode: RegimeCorpNormal,
            RegimeLabel: req.IsSmallTurnover25Percent ? "Normal Corporate (25% + MAT 115JB)" : "Normal Corporate (30% + MAT 115JB)",
            TaxableIncome: taxable,
            TaxAtNormalRates: taxNormal,
            TaxAtSpecialRates: taxSpecial,
            BaseTax: baseTax,
            SurchargeBeforeMarginalRelief: scRaw,
            MarginalRelief: mr,
            Surcharge: sc,
            HealthAndEducationCess: cess,
            GrossTaxBeforeMatAmt: grossRegular,
            MatOrAmtBaseTax: matBase,
            MatOrAmtSurcharge: matSc,
            MatOrAmtCess: matCess,
            MatOrAmtTotalTax: matTotal,
            IsMatOrAmtApplied: isMatApplied,
            MatOrAmtCreditUtilized: matCreditUsed,
            MatOrAmtCreditGenerated: matCreditGen,
            InterestAndFee: interestFee,
            TotalTaxLiability: totalLiab,
            PrepaidTaxes: prepaid,
            NetTaxPayableOrRefund: totalLiab - prepaid,
            IsEligible: true,
            IneligibilityOrNotes: isMatApplied
                ? "MAT u/s 115JB (15%) is higher than regular tax; generates Sec 115JAA credit."
                : "Regular corporate tax exceeds MAT; Sec 115JAA credit eligible if available."
        );
    }

    private static BusinessRegimeSummaryDto ComputeCompanyConcessionalSummary(
        CalculateBusinessTaxRequest req,
        string code,
        string label,
        decimal normalRate,
        decimal gti,
        decimal chapVia115Baa,
        decimal interestFee,
        decimal prepaid)
    {
        decimal taxable = RoundSec288A(Math.Max(0m, gti - chapVia115Baa));
        var (taxNormal, taxSpecial) = ComputeSpecialAndNormalTaxes(taxable, req, normalRate);
        decimal baseTax = taxNormal + taxSpecial;

        // Sec 115BAA & 115BAB carry a mandatory flat 10% surcharge irrespective of income threshold, and NO MAT
        decimal sc = RoundR(baseTax * 0.10m);
        decimal cess = RoundR((baseTax + sc) * 0.04m);
        decimal grossTax = baseTax + sc + cess;
        decimal totalLiab = grossTax + interestFee;

        bool isBab = code == RegimeCorp115Bab;
        string notes = isBab
            ? "New domestic manufacturing companies incorporated on/after 01-Oct-2019 (commenced production before 31-Mar-2024); MAT exempt."
            : "Domestic companies via Form 10-IC; MAT u/s 115JB exempt; only Sec 80JJAA & 80M allowed.";

        return new BusinessRegimeSummaryDto(
            RegimeCode: code,
            RegimeLabel: label,
            TaxableIncome: taxable,
            TaxAtNormalRates: taxNormal,
            TaxAtSpecialRates: taxSpecial,
            BaseTax: baseTax,
            SurchargeBeforeMarginalRelief: sc,
            MarginalRelief: 0m,
            Surcharge: sc,
            HealthAndEducationCess: cess,
            GrossTaxBeforeMatAmt: grossTax,
            MatOrAmtBaseTax: 0m,
            MatOrAmtSurcharge: 0m,
            MatOrAmtCess: 0m,
            MatOrAmtTotalTax: 0m,
            IsMatOrAmtApplied: false,
            MatOrAmtCreditUtilized: 0m,
            MatOrAmtCreditGenerated: 0m,
            InterestAndFee: interestFee,
            TotalTaxLiability: totalLiab,
            PrepaidTaxes: prepaid,
            NetTaxPayableOrRefund: totalLiab - prepaid,
            IsEligible: true,
            IneligibilityOrNotes: notes
        );
    }

    private static (decimal ScRaw, decimal MarginalRelief, decimal ScNet) ComputeSingleTierSurcharge(
        decimal income,
        decimal baseTax,
        decimal threshold,
        decimal rate)
    {
        if (income <= threshold || baseTax <= 0m) return (0m, 0m, 0m);
        decimal scRaw = RoundR(baseTax * rate);
        // Approximate tax at threshold proportionally when mixed normal + special rates exist
        decimal taxAtThreshold = income > 0m ? RoundR(baseTax * (threshold / income)) : 0m;
        decimal maxAllowedTaxPlusSc = taxAtThreshold + (income - threshold);
        decimal actualTaxPlusSc = baseTax + scRaw;
        decimal relief = actualTaxPlusSc > maxAllowedTaxPlusSc ? Math.Min(scRaw, actualTaxPlusSc - maxAllowedTaxPlusSc) : 0m;
        return (scRaw, relief, scRaw - relief);
    }

    private static (decimal ScRaw, decimal MarginalRelief, decimal ScNet) ComputeTwoTierCompanySurcharge(
        decimal income,
        decimal baseTax)
    {
        if (income <= 10_000_000m || baseTax <= 0m) return (0m, 0m, 0m);
        if (income <= 100_000_000m)
            return ComputeSingleTierSurcharge(income, baseTax, 10_000_000m, 0.07m);

        decimal scRaw = RoundR(baseTax * 0.12m);
        decimal taxAt10Cr = RoundR(baseTax * (100_000_000m / income));
        decimal taxPlusScAt10Cr = taxAt10Cr + RoundR(taxAt10Cr * 0.07m);
        decimal maxAllowed = taxPlusScAt10Cr + (income - 100_000_000m);
        decimal actual = baseTax + scRaw;
        decimal relief = actual > maxAllowed ? Math.Min(scRaw, actual - maxAllowed) : 0m;
        return (scRaw, relief, scRaw - relief);
    }

    public static decimal RoundSec288A(decimal amount) =>
        Math.Round(amount / 10m, 0, MidpointRounding.AwayFromZero) * 10m;

    private static decimal RoundR(decimal amount) =>
        Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}
