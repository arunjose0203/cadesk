using System.Text.RegularExpressions;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.UaeTax;

public enum UaeTaxpayerCategory
{
    ResidentJuridicalPerson = 1, // UAE Mainland LLC / PJSC
    QualifyingFreeZonePerson = 2, // QFZP (0% Qualifying, 9% Non-qualifying)
    NonQualifyingFreeZonePerson = 3, // Free Zone entity taxed at normal mainland rates
    NonResidentPermanentEstablishment = 4, // Foreign company branch / PE in UAE
    ResidentNaturalPerson = 5 // UAE resident individual in business > AED 1M turnover
}

public enum FreeZoneRegime
{
    Mainland = 1,
    CommercialDesignatedFreeZone = 2, // JAFZA, DAFZA, DMCC, KIZAD, etc.
    FinancialFreeZone = 3 // DIFC, ADGM
}

public enum SmallBusinessReliefStatus
{
    NotEligible = 1,
    EligibleNotElected = 2,
    Elected = 3 // Taxable Income treated as AED 0 under Ministerial Decision No. 73 of 2023
}

public enum ReturnFilingStatus
{
    Draft = 1,
    UnderReview = 2,
    PartnerApproved = 3,
    SubmittedToEmaraTax = 4,
    Acknowledged = 5
}

public class UaeTaxProfile : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string CorporateTaxTrn { get; set; } = ""; // 15-digit FTA Tax Registration Number
    public string Emirate { get; set; } = "Dubai";
    public string FreeZoneName { get; set; } = "Mainland";
    public UaeTaxpayerCategory Category { get; set; } = UaeTaxpayerCategory.ResidentJuridicalPerson;
    public FreeZoneRegime FreeZoneRegime { get; set; } = FreeZoneRegime.Mainland;
    public int FinancialYearStartMonth { get; set; } = 1; // 1 for January (Jan-Dec), 4 for April (Apr-Mar)
    public bool IsMneGroupConstituent { get; set; } = false; // Consolidated group revenue >= AED 3.15 Billion
    public string AccountingStandard { get; set; } = "IFRS";
    public bool AuditedFinancialsAvailable { get; set; } = true;
}

public class UaeCorporateTaxReturn : TenantEntity, ISoftDelete
{
    public Guid TaxProfileId { get; set; }
    public UaeTaxProfile? TaxProfile { get; set; }

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string ReturnReference { get; set; } = ""; // e.g. CT-2024-0001
    public int ReturnYear { get; set; } = 2024;
    public DateOnly TaxPeriodStart { get; set; }
    public DateOnly TaxPeriodEnd { get; set; }
    public DateOnly DueDate { get; set; } // 9 months from tax period end date

    public ReturnFilingStatus Status { get; set; } = ReturnFilingStatus.Draft;
    public string FunctionalCurrency { get; set; } = "AED";
    public decimal ExchangeRateToInr { get; set; } = 22.85m;

    // Financial Metrics (in AED)
    public decimal GrossRevenue { get; set; }
    public decimal CostOfSales { get; set; }
    public decimal GrossProfit => GrossRevenue - CostOfSales;
    public decimal OperatingExpenses { get; set; }
    public decimal NetAccountingProfitLoss { get; set; }

    // Small Business Relief (SBR) under Ministerial Decision No. 73 of 2023
    public SmallBusinessReliefStatus SmallBusinessReliefElection { get; set; } = SmallBusinessReliefStatus.NotEligible;
    public decimal PriorYear1Revenue { get; set; }
    public decimal PriorYear2Revenue { get; set; }

    // QFZP Evaluation under Cabinet Decision No. 55/2023 & MD No. 139/2023
    public bool IsQfzp { get; set; }
    public decimal QualifyingRevenue { get; set; }
    public decimal NonQualifyingRevenue { get; set; }
    public decimal DeMinimisThreshold { get; set; } // Min(5% of total revenue, AED 5M)
    public bool DeMinimisSatisfied { get; set; }
    public decimal QualifyingIncome { get; set; } // Taxed at 0%
    public decimal TaxableNonQualifyingIncome { get; set; } // Taxed at 9%

    // Statutory Adjustments (in AED)
    public decimal ExemptIncome { get; set; } // Art 22/23 Participation Exemption
    public decimal UnrealizedGainsLossesAdjustment { get; set; } // Art 20 Realization basis
    public decimal EntertainmentExpenditureTotal { get; set; }
    public decimal EntertainmentExpenditureDisallowed { get; set; } // 50% disallowed under Art 32
    public decimal FinesAndPenaltiesDisallowed { get; set; } // 100% disallowed under Art 33
    public decimal RecoverableVatDisallowed { get; set; } // 100% disallowed under Art 33
    public decimal DonationsToUnapprovedEntitiesDisallowed { get; set; } // 100% disallowed under Art 33

    // Interest Capping under Article 30 (GIDLR)
    public decimal NetInterestExpense { get; set; }
    public decimal AdjustedEbitda { get; set; }
    public decimal InterestCapThreshold { get; set; } // Max(AED 12,000,000, 30% of Adjusted EBITDA)
    public decimal DisallowedInterestExpense { get; set; }

    // Tax Loss Relief under Article 37
    public decimal BroughtForwardTaxLosses { get; set; }
    public decimal TaxLossesUtilized { get; set; } // Max 75% of taxable income before losses
    public decimal CarriedForwardTaxLosses { get; set; }

    // Tax Computation
    public decimal AdjustedTaxableIncome { get; set; }
    public decimal TaxableIncomeSubjectToZeroPct { get; set; } // Up to AED 375,000 or qualifying income
    public decimal TaxableIncomeSubjectToNinePct { get; set; } // Exceeding AED 375,000
    public decimal CorporateTaxPayableAed { get; set; }
    public decimal ForeignTaxCreditReliefAed { get; set; }
    public decimal NetTaxPayableAed { get; set; }
    public decimal NetTaxPayableInr => NetTaxPayableAed * ExchangeRateToInr;

    // EmaraTax Submission
    public string? FtaSubmissionReference { get; set; }
    public string? SubmissionPayloadJson { get; set; }
    public Guid? PreparedByUserId { get; set; }
    public string? PreparedByName { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? PartnerNotes { get; set; }

    public DateTime? DeletedAt { get; set; }
}

public static class UaeTaxEngine
{
    public const decimal BasicExemptionThresholdAed = 375000m; // 0% up to AED 375,000
    public const decimal StatutoryCorporateTaxRate = 0.09m; // 9%
    public const decimal SbrRevenueCeilingAed = 3000000m; // AED 3 Million
    public const decimal InterestDeMinimisThresholdAed = 12000000m; // AED 12 Million
    public const decimal MaxInterestEbitdaRatio = 0.30m; // 30% EBITDA
    public const decimal MaxTaxLossUtilizationRate = 0.75m; // 75% Tax Loss Cap
    public const decimal QfzpDeMinimisPct = 0.05m; // 5% of Total Revenue
    public const decimal QfzpDeMinimisMaxAed = 5000000m; // AED 5 Million

    private static readonly Regex TrnRegex = new(@"^100\d{12}$", RegexOptions.Compiled);

    public static bool ValidateTrn(string? trn) =>
        !string.IsNullOrWhiteSpace(trn) && TrnRegex.IsMatch(trn.Trim());

    public record ComputationResult(
        bool SbrEligible,
        bool QfzpDeMinimisPassed,
        decimal DeMinimisThresholdAed,
        decimal DisallowedInterestAed,
        decimal DisallowedEntertainmentAed,
        decimal TotalAdditionsAed,
        decimal TotalDeductionsAed,
        decimal AdjustedTaxableIncomeAed,
        decimal TaxLossesUtilizedAed,
        decimal RemainingTaxLossesAed,
        decimal FinalTaxableIncomeAed,
        decimal IncomeSubjectToZeroPctAed,
        decimal IncomeSubjectToNinePctAed,
        decimal GrossTaxPayableAed,
        decimal NetTaxPayableAed,
        decimal NetTaxPayableInr
    );

    public static ComputationResult Compute(
        UaeTaxpayerCategory category,
        decimal grossRevenue,
        decimal netAccountingProfit,
        SmallBusinessReliefStatus sbrElection,
        decimal priorYear1Revenue,
        decimal priorYear2Revenue,
        bool isMneConstituent,
        decimal qualifyingRevenue,
        decimal nonQualifyingRevenue,
        decimal exemptIncome,
        decimal unrealizedGainsLossesAdj,
        decimal entertainmentExpense,
        decimal finesAndPenalties,
        decimal recoverableVat,
        decimal unapprovedDonations,
        decimal netInterestExpense,
        decimal adjustedEbitda,
        decimal broughtForwardLosses,
        decimal foreignTaxCredit,
        decimal exchangeRateToInr
    )
    {
        // 1. SBR Eligibility Test (MD No. 73 of 2023)
        // Revenue <= AED 3M in current and all prior periods, not QFZP, not MNE constituent
        var sbrEligible = category != UaeTaxpayerCategory.QualifyingFreeZonePerson &&
                          !isMneConstituent &&
                          grossRevenue <= SbrRevenueCeilingAed &&
                          priorYear1Revenue <= SbrRevenueCeilingAed &&
                          priorYear2Revenue <= SbrRevenueCeilingAed;

        var isSbrActive = sbrEligible && sbrElection == SmallBusinessReliefStatus.Elected;

        if (isSbrActive)
        {
            return new ComputationResult(
                SbrEligible: true,
                QfzpDeMinimisPassed: true,
                DeMinimisThresholdAed: 0,
                DisallowedInterestAed: 0,
                DisallowedEntertainmentAed: 0,
                TotalAdditionsAed: 0,
                TotalDeductionsAed: 0,
                AdjustedTaxableIncomeAed: 0,
                TaxLossesUtilizedAed: 0,
                RemainingTaxLossesAed: broughtForwardLosses,
                FinalTaxableIncomeAed: 0,
                IncomeSubjectToZeroPctAed: 0,
                IncomeSubjectToNinePctAed: 0,
                GrossTaxPayableAed: 0,
                NetTaxPayableAed: 0,
                NetTaxPayableInr: 0
            );
        }

        // 2. QFZP De Minimis Test (Cabinet Decision No. 55/2023)
        var isQfzp = category == UaeTaxpayerCategory.QualifyingFreeZonePerson;
        var deMinimisThreshold = Math.Min(grossRevenue * QfzpDeMinimisPct, QfzpDeMinimisMaxAed);
        var qfzpDeMinimisPassed = !isQfzp || nonQualifyingRevenue <= deMinimisThreshold;

        // 3. Specific Disallowances
        var disallowedEntertainment = entertainmentExpense * 0.50m; // 50% disallowed under Art 32
        var totalAdditions = disallowedEntertainment + finesAndPenalties + recoverableVat + unapprovedDonations;

        // 4. Interest Capping under Article 30 (GIDLR)
        var interestCap = Math.Max(InterestDeMinimisThresholdAed, Math.Max(0, adjustedEbitda) * MaxInterestEbitdaRatio);
        var disallowedInterest = netInterestExpense > interestCap ? netInterestExpense - interestCap : 0m;
        totalAdditions += disallowedInterest;

        // 5. Statutory Deductions
        var totalDeductions = exemptIncome + (unrealizedGainsLossesAdj > 0 ? unrealizedGainsLossesAdj : 0);

        // 6. Adjusted Taxable Income
        var adjustedTaxable = netAccountingProfit + totalAdditions - totalDeductions;

        // 7. Tax Loss Relief (Article 37) - max 75% of taxable income
        decimal taxLossesUtilized = 0;
        decimal remainingLosses = broughtForwardLosses;

        if (adjustedTaxable > 0 && broughtForwardLosses > 0)
        {
            var maxLossAllowable = adjustedTaxable * MaxTaxLossUtilizationRate;
            taxLossesUtilized = Math.Min(broughtForwardLosses, maxLossAllowable);
            remainingLosses = broughtForwardLosses - taxLossesUtilized;
        }

        var finalTaxableIncome = Math.Max(0, adjustedTaxable - taxLossesUtilized);

        // 8. Tax Calculation
        decimal zeroPctIncome = 0;
        decimal ninePctIncome = 0;

        if (isQfzp)
        {
            if (qfzpDeMinimisPassed)
            {
                // Qualifying Income at 0%
                // Non-qualifying income taxed at 9% (no AED 375k threshold for non-qualifying income of QFZP per FTA guide)
                zeroPctIncome = Math.Max(0, finalTaxableIncome - nonQualifyingRevenue);
                ninePctIncome = Math.Min(finalTaxableIncome, nonQualifyingRevenue);
            }
            else
            {
                // QFZP disqualified -> Entire income taxed at 9%
                ninePctIncome = finalTaxableIncome;
            }
        }
        else
        {
            // Standard Mainland / Non-QFZP
            if (finalTaxableIncome <= BasicExemptionThresholdAed)
            {
                zeroPctIncome = finalTaxableIncome;
                ninePctIncome = 0;
            }
            else
            {
                zeroPctIncome = BasicExemptionThresholdAed;
                ninePctIncome = finalTaxableIncome - BasicExemptionThresholdAed;
            }
        }

        var grossTaxPayable = ninePctIncome * StatutoryCorporateTaxRate;
        var netTaxPayableAed = Math.Max(0, grossTaxPayable - foreignTaxCredit);
        var rate = exchangeRateToInr > 0 ? exchangeRateToInr : 22.85m;
        var netTaxPayableInr = netTaxPayableAed * rate;

        return new ComputationResult(
            SbrEligible: sbrEligible,
            QfzpDeMinimisPassed: qfzpDeMinimisPassed,
            DeMinimisThresholdAed: deMinimisThreshold,
            DisallowedInterestAed: disallowedInterest,
            DisallowedEntertainmentAed: disallowedEntertainment,
            TotalAdditionsAed: totalAdditions,
            TotalDeductionsAed: totalDeductions,
            AdjustedTaxableIncomeAed: adjustedTaxable,
            TaxLossesUtilizedAed: taxLossesUtilized,
            RemainingTaxLossesAed: remainingLosses,
            FinalTaxableIncomeAed: finalTaxableIncome,
            IncomeSubjectToZeroPctAed: zeroPctIncome,
            IncomeSubjectToNinePctAed: ninePctIncome,
            GrossTaxPayableAed: grossTaxPayable,
            NetTaxPayableAed: netTaxPayableAed,
            NetTaxPayableInr: netTaxPayableInr
        );
    }
}
