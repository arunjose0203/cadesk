using CaDesk.Application.Mca;
using CaDesk.Domain.Mca;
using Xunit;

namespace CaDesk.UnitTests;

public class McaComplianceUnitTests
{
    [Theory]
    [InlineData("U72200MH2020PTC123456", true)]
    [InlineData("L17110MH1973PLC019786", true)]
    [InlineData("U01100DL2018OPC334455", true)]
    [InlineData("U74999KA2015NPL082341", true)]
    [InlineData("invalid-cin", false)]
    [InlineData("U72200MH2020PTC12345", false)] // 20 chars
    [InlineData("X72200MH2020PTC123456", false)] // starts with X
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCin_Validates_Format(string? cin, bool expected)
    {
        var result = McaFeeCalculator.IsValidCin(cin);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("01234567", true)]
    [InlineData("09876543", true)]
    [InlineData("00000001", true)]
    [InlineData("1234567", false)] // 7 digits
    [InlineData("123456789", false)] // 9 digits
    [InlineData("0123ABCD", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidDin_Validates_8_Digits(string? din, bool expected)
    {
        var result = McaFeeCalculator.IsValidDin(din);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(50_000, 200)]
    [InlineData(100_000, 300)]
    [InlineData(1_000_000, 400)]
    [InlineData(5_000_000, 500)]
    [InlineData(20_000_000, 600)]
    public void DetermineStandardNormalFee_Maps_Capital_Tiers(decimal capital, decimal expected)
    {
        var fee = McaFeeCalculator.DetermineStandardNormalFee(McaFormType.Aoc4, capital);
        Assert.Equal(expected, fee);
    }

    [Fact]
    public void CalculateLateFee_OnTime_Returns_Zero_Late_Fee()
    {
        var due = new DateOnly(2024, 10, 30);
        var filing = new DateOnly(2024, 10, 25);

        var result = McaFeeCalculator.CalculateLateFee(McaFormType.Aoc4, due, filing, 100_000m);

        Assert.Equal(0, result.DaysDelayed);
        Assert.Equal(0m, result.LateFee);
        Assert.Equal(300m, result.NormalFee);
        Assert.Equal(300m, result.TotalFee);
    }

    [Fact]
    public void CalculateLateFee_Aoc4_Computes_100_Per_Day_Delay()
    {
        var due = new DateOnly(2024, 10, 30);
        var filing = new DateOnly(2024, 11, 14); // 15 days delay

        var result = McaFeeCalculator.CalculateLateFee(McaFormType.Aoc4, due, filing, 100_000m);

        Assert.Equal(15, result.DaysDelayed);
        Assert.Equal(1500m, result.LateFee); // 15 * 100
        Assert.Equal(300m, result.NormalFee);
        Assert.Equal(1800m, result.TotalFee);
    }

    [Fact]
    public void CalculateLateFee_Mgt7_Computes_100_Per_Day_Delay()
    {
        var due = new DateOnly(2024, 11, 29);
        var filing = new DateOnly(2025, 1, 28); // 60 days delay

        var result = McaFeeCalculator.CalculateLateFee(McaFormType.Mgt7, due, filing, 100_000m);

        Assert.Equal(60, result.DaysDelayed);
        Assert.Equal(6000m, result.LateFee); // 60 * 100
        Assert.Equal(300m, result.NormalFee);
        Assert.Equal(6300m, result.TotalFee);
    }

    [Fact]
    public void CalculateLateFee_Dir3Kyc_Post_Due_Date_Applies_5000_Flat_Fee()
    {
        var due = new DateOnly(2024, 9, 30);
        var filing = new DateOnly(2024, 10, 15);

        var result = McaFeeCalculator.CalculateLateFee("DIR-3_KYC", due, filing);

        Assert.Equal(15, result.DaysDelayed);
        Assert.Equal(5000m, result.LateFee);
        Assert.Equal(0m, result.NormalFee);
        Assert.Equal(5000m, result.TotalFee);
    }

    [Theory]
    [InlineData(15, 600)]    // <= 30 days -> 2x normal fee (300 * 2 = 600)
    [InlineData(45, 1200)]   // 31-60 days -> 4x normal fee (300 * 4 = 1200)
    [InlineData(75, 1800)]   // 61-90 days -> 6x normal fee (300 * 6 = 1800)
    [InlineData(120, 3000)] // 91-180 days -> 10x normal fee (300 * 10 = 3000)
    [InlineData(200, 3600)] // > 180 days -> 12x normal fee (300 * 12 = 3600)
    public void CalculateLateFee_EventForms_Applies_Slab_Multiplier(int delayDays, decimal expectedLateFee)
    {
        var due = new DateOnly(2024, 5, 1);
        var filing = due.AddDays(delayDays);

        var result = McaFeeCalculator.CalculateLateFee(McaFormType.Dir12, due, filing, 100_000m);

        Assert.Equal(delayDays, result.DaysDelayed);
        Assert.Equal(expectedLateFee, result.LateFee);
        Assert.Equal(300m, result.NormalFee);
        Assert.Equal(300m + expectedLateFee, result.TotalFee);
    }
}
