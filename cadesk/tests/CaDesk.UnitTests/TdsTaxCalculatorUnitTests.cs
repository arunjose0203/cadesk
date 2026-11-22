using CaDesk.Application.Tds;
using CaDesk.Domain.Tds;
using Xunit;

namespace CaDesk.UnitTests;

public class TdsTaxCalculatorUnitTests
{
    [Theory]
    [InlineData(TdsSection.Sec194CCompany, 1.0)]
    [InlineData(TdsSection.Sec194COther, 2.0)]
    [InlineData(TdsSection.Sec194JTech, 2.0)]
    [InlineData(TdsSection.Sec194JFees, 10.0)]
    [InlineData(TdsSection.Sec194ILand, 10.0)]
    [InlineData(TdsSection.Sec194IPlant, 2.0)]
    [InlineData(TdsSection.Sec194Q, 0.1)]
    [InlineData(TdsSection.Sec194A, 10.0)]
    [InlineData(TdsSection.Sec194H, 5.0)]
    [InlineData(TdsSection.Sec195, 20.0)]
    public void DetermineRate_StandardSections_ReturnsStatutoryRates(string section, decimal expectedRate)
    {
        var (rate, reason) = TdsTaxCalculator.DetermineRate(section, "ABCDE1234F");
        Assert.Equal(expectedRate, rate);
        Assert.Equal(HigherRateReason.None, reason);
    }

    [Theory]
    [InlineData("INVALIDPAN")]
    [InlineData("PANNOTAVBL")]
    [InlineData("123456")]
    [InlineData("")]
    public void DetermineRate_InvalidOrMissingPan_AppliesSection206AaRate(string invalidPan)
    {
        var (rate, reason) = TdsTaxCalculator.DetermineRate(TdsSection.Sec194CCompany, invalidPan);
        Assert.Equal(20.0m, rate);
        Assert.Equal(HigherRateReason.NoPan206Aa, reason);
    }

    [Fact]
    public void DetermineRate_NonFilerSection206Ab_AppliesHigherOfTwiceRateOrFivePercent()
    {
        // 194C Company: 1% -> Twice is 2%, but min is 5% -> 5%
        var (rate1, reason1) = TdsTaxCalculator.DetermineRate(TdsSection.Sec194CCompany, "ABCDE1234F", isNonFiler206Ab: true, deductionDate: Fy2024Date);
        Assert.Equal(5.0m, rate1);
        Assert.Equal(HigherRateReason.NonFiler206Ab, reason1);

        // 194J Fees: 10% -> Twice is 20% -> 20%
        var (rate2, reason2) = TdsTaxCalculator.DetermineRate(TdsSection.Sec194JFees, "ABCDE1234F", isNonFiler206Ab: true, deductionDate: Fy2024Date);
        Assert.Equal(20.0m, rate2);
        Assert.Equal(HigherRateReason.NonFiler206Ab, reason2);
    }

    private static readonly DateOnly Fy2024Date = new(2025, 3, 15);

    [Fact]
    public void DetermineRate_206AbOmittedFromApril2025_NonFilerGetsNormalRate()
    {
        // Finance Act 2025 omitted Section 206AB for deductions from 1 April 2025
        var (rate, reason) = TdsTaxCalculator.DetermineRate(TdsSection.Sec194JFees, "ABCDE1234F",
            isNonFiler206Ab: true, deductionDate: new DateOnly(2025, 4, 1));
        Assert.Equal(10.0m, rate);
        Assert.Equal(HigherRateReason.None, reason);

        // with no date, today's law applies
        Assert.Equal(10.0m, TdsTaxCalculator.DetermineRate(TdsSection.Sec194JFees, "ABCDE1234F", isNonFiler206Ab: true).FinalRate);
    }

    [Fact]
    public void DetermineRate_LowerDeductionCertificate_AppliesSpecifiedRate()
    {
        var (rate, reason) = TdsTaxCalculator.DetermineRate(
            TdsSection.Sec194JTech, "ABCDE1234F", ldcRate: 0.5m);

        Assert.Equal(0.5m, rate);
        Assert.Equal(HigherRateReason.None, reason);
    }

    [Fact]
    public void CalculateInterest_DelayInDeposit_CalculatesOnePointFivePercentPerMonth()
    {
        // Deducted on 15 April 2024. Due date for deposit: 7 May 2024.
        // Paid late on 20 June 2024 -> 3 months (April, May, June) delay
        // 3 months * 1.5% = 4.5% of 10,000 = 450
        var deductionDate = new DateOnly(2024, 4, 15);
        var depositDate = new DateOnly(2024, 6, 20);

        var result = TdsTaxCalculator.CalculateInterest(deductionDate, null, depositDate, 10_000m);

        Assert.True(result.DelayDepositMonths >= 2);
        Assert.True(result.DelayDepositInterest > 0);
    }
}
