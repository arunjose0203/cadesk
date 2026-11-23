using CaDesk.Domain.UaeTax;
using Xunit;

namespace CaDesk.UnitTests;

public class UaeCorporateTaxUnitTests
{
    [Theory]
    [InlineData("100123456789003", true)]
    [InlineData("100987654321001", true)]
    [InlineData("200123456789003", false)] // Must start with 100
    [InlineData("10012345", false)] // Too short
    [InlineData("1001234567890031", false)] // Too long
    [InlineData("10012345678900A", false)] // Non-digit
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateTrn_Validates15DigitFtaFormatCorrectly(string? trn, bool expectedValid)
    {
        var isValid = UaeTaxEngine.ValidateTrn(trn);
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void Compute_MainlandStandardTaxCalculation_Applies9PercentAbove375kThreshold()
    {
        // Revenue 10M, Net profit 1,000,000
        // Entertainment 100,000 (50% = 50,000 disallowed)
        // Fines 50,000 (100% disallowed)
        // Adjusted Taxable = 1,100,000
        // 0% on 375,000; 9% on (1,100,000 - 375,000 = 725,000) = 65,250 AED
        var result = UaeTaxEngine.Compute(
            category: UaeTaxpayerCategory.ResidentJuridicalPerson,
            grossRevenue: 10000000m,
            netAccountingProfit: 1000000m,
            sbrElection: SmallBusinessReliefStatus.NotEligible,
            priorYear1Revenue: 8000000m,
            priorYear2Revenue: 7000000m,
            isMneConstituent: false,
            qualifyingRevenue: 0,
            nonQualifyingRevenue: 0,
            exemptIncome: 0,
            unrealizedGainsLossesAdj: 0,
            entertainmentExpense: 100000m,
            finesAndPenalties: 50000m,
            recoverableVat: 0,
            unapprovedDonations: 0,
            netInterestExpense: 0,
            adjustedEbitda: 2000000m,
            broughtForwardLosses: 0,
            foreignTaxCredit: 0,
            exchangeRateToInr: 22.85m
        );

        Assert.False(result.SbrEligible);
        Assert.Equal(50000m, result.DisallowedEntertainmentAed);
        Assert.Equal(100000m, result.TotalAdditionsAed);
        Assert.Equal(1100000m, result.AdjustedTaxableIncomeAed);
        Assert.Equal(1100000m, result.FinalTaxableIncomeAed);
        Assert.Equal(375000m, result.IncomeSubjectToZeroPctAed);
        Assert.Equal(725000m, result.IncomeSubjectToNinePctAed);
        Assert.Equal(65250m, result.GrossTaxPayableAed);
        Assert.Equal(65250m, result.NetTaxPayableAed);
        Assert.Equal(1490962.50m, result.NetTaxPayableInr);
    }

    [Fact]
    public void Compute_SmallBusinessRelief_ElectedUnder3MRevenue_ResultsInZeroTax()
    {
        // Revenue 2.5M (<= 3M AED ceiling under MD 73/2023)
        // Profit 400,000 AED
        // SBR Elected -> Final Taxable = 0, Tax = 0
        var result = UaeTaxEngine.Compute(
            category: UaeTaxpayerCategory.ResidentJuridicalPerson,
            grossRevenue: 2500000m,
            netAccountingProfit: 400000m,
            sbrElection: SmallBusinessReliefStatus.Elected,
            priorYear1Revenue: 2000000m,
            priorYear2Revenue: 1800000m,
            isMneConstituent: false,
            qualifyingRevenue: 0,
            nonQualifyingRevenue: 0,
            exemptIncome: 0,
            unrealizedGainsLossesAdj: 0,
            entertainmentExpense: 20000m,
            finesAndPenalties: 0,
            recoverableVat: 0,
            unapprovedDonations: 0,
            netInterestExpense: 0,
            adjustedEbitda: 500000m,
            broughtForwardLosses: 0,
            foreignTaxCredit: 0,
            exchangeRateToInr: 22.85m
        );

        Assert.True(result.SbrEligible);
        Assert.Equal(0m, result.FinalTaxableIncomeAed);
        Assert.Equal(0m, result.NetTaxPayableAed);
        Assert.Equal(0m, result.NetTaxPayableInr);
    }

    [Fact]
    public void Compute_QualifyingFreeZonePerson_DeMinimisPass_Applies0PercentOnQualifyingIncome()
    {
        // Total Revenue 20M, Net profit 2M
        // Qualifying Revenue = 19,500,000; Non-qualifying = 500,000
        // De Minimis = Min(5% of 20M = 1,000,000, 5M) = 1M
        // 500k <= 1M -> Passed!
        // Non-qualifying taxed at 9% = 45,000 AED
        var result = UaeTaxEngine.Compute(
            category: UaeTaxpayerCategory.QualifyingFreeZonePerson,
            grossRevenue: 20000000m,
            netAccountingProfit: 2000000m,
            sbrElection: SmallBusinessReliefStatus.NotEligible,
            priorYear1Revenue: 15000000m,
            priorYear2Revenue: 12000000m,
            isMneConstituent: false,
            qualifyingRevenue: 19500000m,
            nonQualifyingRevenue: 500000m,
            exemptIncome: 0,
            unrealizedGainsLossesAdj: 0,
            entertainmentExpense: 0,
            finesAndPenalties: 0,
            recoverableVat: 0,
            unapprovedDonations: 0,
            netInterestExpense: 0,
            adjustedEbitda: 2500000m,
            broughtForwardLosses: 0,
            foreignTaxCredit: 0,
            exchangeRateToInr: 22.85m
        );

        Assert.True(result.QfzpDeMinimisPassed);
        Assert.Equal(1000000m, result.DeMinimisThresholdAed);
        Assert.Equal(1500000m, result.IncomeSubjectToZeroPctAed);
        Assert.Equal(500000m, result.IncomeSubjectToNinePctAed);
        Assert.Equal(45000m, result.GrossTaxPayableAed);
        Assert.Equal(45000m, result.NetTaxPayableAed);
    }

    [Fact]
    public void Compute_QualifyingFreeZonePerson_DeMinimisFailed_DisqualifiesAndTaxesAllAt9Percent()
    {
        // Total Revenue 20M, Net profit 2M
        // Non-qualifying = 2M (> 1M threshold) -> Failed!
        // Entire 2M taxed at 9% = 180,000 AED
        var result = UaeTaxEngine.Compute(
            category: UaeTaxpayerCategory.QualifyingFreeZonePerson,
            grossRevenue: 20000000m,
            netAccountingProfit: 2000000m,
            sbrElection: SmallBusinessReliefStatus.NotEligible,
            priorYear1Revenue: 15000000m,
            priorYear2Revenue: 12000000m,
            isMneConstituent: false,
            qualifyingRevenue: 18000000m,
            nonQualifyingRevenue: 2000000m,
            exemptIncome: 0,
            unrealizedGainsLossesAdj: 0,
            entertainmentExpense: 0,
            finesAndPenalties: 0,
            recoverableVat: 0,
            unapprovedDonations: 0,
            netInterestExpense: 0,
            adjustedEbitda: 2500000m,
            broughtForwardLosses: 0,
            foreignTaxCredit: 0,
            exchangeRateToInr: 22.85m
        );

        Assert.False(result.QfzpDeMinimisPassed);
        Assert.Equal(0m, result.IncomeSubjectToZeroPctAed);
        Assert.Equal(2000000m, result.IncomeSubjectToNinePctAed);
        Assert.Equal(1800000m * 0.09m + 200000m * 0.09m, result.GrossTaxPayableAed);
    }

    [Fact]
    public void Compute_Article30InterestCap_DisallowsInterestExceedingThreshold()
    {
        // Net Interest Expense: 15,000,000
        // Adjusted EBITDA: 20,000,000 (30% = 6,000,000)
        // Cap = Max(12M, 6M) = 12M
        // Disallowed = 15M - 12M = 3,000,000 AED
        var result = UaeTaxEngine.Compute(
            category: UaeTaxpayerCategory.ResidentJuridicalPerson,
            grossRevenue: 50000000m,
            netAccountingProfit: 5000000m,
            sbrElection: SmallBusinessReliefStatus.NotEligible,
            priorYear1Revenue: 40000000m,
            priorYear2Revenue: 35000000m,
            isMneConstituent: false,
            qualifyingRevenue: 0,
            nonQualifyingRevenue: 0,
            exemptIncome: 0,
            unrealizedGainsLossesAdj: 0,
            entertainmentExpense: 0,
            finesAndPenalties: 0,
            recoverableVat: 0,
            unapprovedDonations: 0,
            netInterestExpense: 15000000m,
            adjustedEbitda: 20000000m,
            broughtForwardLosses: 0,
            foreignTaxCredit: 0,
            exchangeRateToInr: 22.85m
        );

        Assert.Equal(3000000m, result.DisallowedInterestAed);
        Assert.Equal(8000000m, result.AdjustedTaxableIncomeAed); // 5M + 3M disallowed interest
    }

    [Fact]
    public void Compute_Article37TaxLossRelief_CapsLossUtilizationAt75Percent()
    {
        // Adjusted Taxable = 1,000,000
        // Brought Forward Losses = 900,000
        // 75% cap on 1M = 750,000 max allowable
        // Losses Utilized = 750,000; Remaining Losses = 150,000
        // Final Taxable = 250,000 (<= 375k threshold -> 0% tax)
        var result = UaeTaxEngine.Compute(
            category: UaeTaxpayerCategory.ResidentJuridicalPerson,
            grossRevenue: 5000000m,
            netAccountingProfit: 1000000m,
            sbrElection: SmallBusinessReliefStatus.NotEligible,
            priorYear1Revenue: 4000000m,
            priorYear2Revenue: 3500000m,
            isMneConstituent: false,
            qualifyingRevenue: 0,
            nonQualifyingRevenue: 0,
            exemptIncome: 0,
            unrealizedGainsLossesAdj: 0,
            entertainmentExpense: 0,
            finesAndPenalties: 0,
            recoverableVat: 0,
            unapprovedDonations: 0,
            netInterestExpense: 0,
            adjustedEbitda: 1500000m,
            broughtForwardLosses: 900000m,
            foreignTaxCredit: 0,
            exchangeRateToInr: 22.85m
        );

        Assert.Equal(750000m, result.TaxLossesUtilizedAed);
        Assert.Equal(150000m, result.RemainingTaxLossesAed);
        Assert.Equal(250000m, result.FinalTaxableIncomeAed);
        Assert.Equal(250000m, result.IncomeSubjectToZeroPctAed);
        Assert.Equal(0m, result.IncomeSubjectToNinePctAed);
        Assert.Equal(0m, result.NetTaxPayableAed);
    }
}
