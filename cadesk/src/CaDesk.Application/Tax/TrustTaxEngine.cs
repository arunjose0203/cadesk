namespace CaDesk.Application.Tax;

/// <summary>
/// Statutory Indian Direct Tax engine for ITR-7 entities
/// (Charitable / Religious Trusts, Sec 8 Companies, Educational/Medical Institutions u/s 11, 12AB, 10(23C), 115BBC, 115BBI).
/// </summary>
public static class TrustTaxEngine
{
    public static TrustTaxResultDto Compute(CalculateTrustTaxRequest req)
    {
        decimal advTax = req.AdvanceTaxPaid;
        if (req.AdvanceTaxChallans is { Count: > 0 } && advTax == 0m)
            advTax = req.AdvanceTaxChallans.Sum(c => Math.Max(0m, c.Amount));

        decimal tds = req.TdsCredit;
        if (req.TdsCreditEntries is { Count: > 0 } && tds == 0m)
            tds = req.TdsCreditEntries.Sum(t => Math.Max(0m, t.TdsClaimed));

        decimal corpusExempt = Math.Max(0m, req.CorpusDonations80G) + Math.Max(0m, req.CorpusDonationsOther);
        decimal nonCorpusVoluntary = Math.Max(0m, req.GrantsReceivedGovt)
            + Math.Max(0m, req.GrantsReceivedCorporate)
            + Math.Max(0m, req.OtherSpecificAndGeneralDonations)
            + Math.Max(0m, req.ForeignContributions);

        decimal anonTotal = Math.Max(0m, req.AnonymousDonationsReceived);
        decimal totalVoluntaryInclAnon = corpusExempt + nonCorpusVoluntary + anonTotal;

        // Sec 115BBC: Anonymous donations exceeding max(5% of total donations, Rs. 1,00,000) are taxed @ 30%
        decimal anonThreshold = anonTotal > 0m ? Math.Max(100_000m, Math.Round(totalVoluntaryInclAnon * 0.05m, 0, MidpointRounding.AwayFromZero)) : 0m;
        decimal anonTaxable115Bbc = anonTotal > 0m ? Math.Max(0m, anonTotal - anonThreshold) : 0m;
        decimal anonAllowedFor11 = Math.Max(0m, anonTotal - anonTaxable115Bbc);

        decimal scheduleAiTotal = Math.Max(0m, req.ReceiptsFromMainObjects)
            + Math.Max(0m, req.ReceiptsIncidental)
            + Math.Max(0m, req.RentIncome)
            + Math.Max(0m, req.DividendIncome)
            + Math.Max(0m, req.InterestIncome);

        decimal eligibleReceipts = nonCorpusVoluntary + anonAllowedFor11 + scheduleAiTotal;
        decimal stat15Pct = Math.Round(eligibleReceipts * 0.15m, 0, MidpointRounding.AwayFromZero);
        decimal req85Pct = Math.Max(0m, eligibleReceipts - stat15Pct);

        decimal totAppClaimed = Math.Max(0m, req.RevenueApplicationCharitable) + Math.Max(0m, req.CapitalApplicationCharitable);
        decimal disallowedApp = Math.Min(totAppClaimed, Math.Max(0m, req.DisallowedApplication40aOr40A3));
        decimal netAllowedApp = Math.Max(0m, totAppClaimed - disallowedApp);
        decimal form10Acc = req.Form10AccumulationUs11_2 > 0m ? req.Form10AccumulationUs11_2 : 0m;

        // Unapplied shortfall below 85% + disallowed applications form normal taxable shortfall
        decimal shortfall = Math.Max(0m, req85Pct - netAllowedApp - form10Acc);
        decimal spec115Bbi = Math.Max(0m, req.SpecifiedViolationIncome115BBI);
        decimal totalTaxable = BusinessTaxEngine.RoundSec288A(shortfall + anonTaxable115Bbc + spec115Bbi);

        // Normal AOP/Trust slab rates (Old regime individual/AOP slabs for trust shortfall)
        var (normalTax, _) = TaxEngine.ComputeOldRegimeSlabs(shortfall, "individual");
        decimal tax115Bbc = Math.Round(anonTaxable115Bbc * 0.30m, 0, MidpointRounding.AwayFromZero);
        decimal tax115Bbi = Math.Round(spec115Bbi * 0.30m, 0, MidpointRounding.AwayFromZero);
        decimal baseTax = normalTax + tax115Bbc + tax115Bbi;

        var (surcharge, _) = TaxEngine.CalculateSurchargeWithMarginalRelief(
            totalTaxable,
            baseTax,
            false,
            inc =>
            {
                decimal nonSpecial = Math.Max(0m, inc - anonTaxable115Bbc - spec115Bbi);
                var (nTax, _) = TaxEngine.ComputeOldRegimeSlabs(nonSpecial, "individual");
                decimal bbcAtInc = Math.Round(Math.Min(anonTaxable115Bbc, inc) * 0.30m, 0, MidpointRounding.AwayFromZero);
                decimal bbiAtInc = Math.Round(Math.Min(spec115Bbi, Math.Max(0m, inc - anonTaxable115Bbc)) * 0.30m, 0, MidpointRounding.AwayFromZero);
                return nTax + bbcAtInc + bbiAtInc;
            });
        decimal cess = Math.Round((baseTax + surcharge) * 0.04m, 0, MidpointRounding.AwayFromZero);
        decimal interestFee = Math.Max(0m, req.Interest234A) + Math.Max(0m, req.Interest234B) + Math.Max(0m, req.Interest234C) + Math.Max(0m, req.LateFee234F);
        decimal totLiability = baseTax + surcharge + cess + interestFee;
        decimal prepaid = tds + Math.Max(0m, req.TcsCredit) + advTax + Math.Max(0m, req.SelfAssessmentTaxPaid);
        decimal netPayable = totLiability - prepaid;

        string summaryNote = shortfall == 0m && anonTaxable115Bbc == 0m && spec115Bbi == 0m
            ? $"Full Sec 11/12AB exemption met (85% req ₹{req85Pct:N0} covered by application ₹{netAllowedApp:N0} & Form 10 ₹{form10Acc:N0})."
            : $"Taxable shortfall ₹{shortfall:N0}, Sec 115BBC anonymous ₹{anonTaxable115Bbc:N0}, Sec 115BBI specified ₹{spec115Bbi:N0}.";

        var norm = req with { AdvanceTaxPaid = advTax, TdsCredit = tds };
        return new TrustTaxResultDto(
            AssessmentYear: req.AssessmentYear,
            FinancialYear: req.FinancialYear,
            ReturnFurnishedSection: req.ReturnFurnishedSection,
            SectionExemptionClaimed: req.SectionExemptionClaimed,
            GrossVoluntaryNonCorpus: nonCorpusVoluntary,
            CorpusExempt11_1d: corpusExempt,
            OtherObjectAndInvestmentReceipts: scheduleAiTotal,
            TotalEligibleIncome: eligibleReceipts,
            StatutoryAccumulation15Pct: stat15Pct,
            Required85PctApplication: req85Pct,
            TotalApplicationClaimed: totAppClaimed,
            DisallowedApplication: disallowedApp,
            NetAllowedApplication: netAllowedApp,
            Form10Accumulation: form10Acc,
            AnonymousDonationsTotal: anonTotal,
            AnonymousDonationsTaxable115BBC: anonTaxable115Bbc,
            ShortfallTaxableNormalIncome: shortfall,
            SpecifiedIncome115BBI: spec115Bbi,
            TotalTaxableIncome: totalTaxable,
            TaxOnNormalShortfall: normalTax,
            TaxOnAnonymous115BBC: tax115Bbc,
            TaxOn115BBI: tax115Bbi,
            Surcharge: surcharge,
            Cess: cess,
            InterestAndFee: interestFee,
            TotalTaxLiability: totLiability,
            PrepaidTaxes: prepaid,
            NetPayableOrRefund: netPayable,
            SummaryNote: summaryNote,
            NormalizedInput: norm
        );
    }
}
