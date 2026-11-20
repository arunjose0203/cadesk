using CaDesk.Domain.FixedAssets;
using Xunit;

namespace CaDesk.UnitTests;

public class FixedAssetUnitTests
{
    private static readonly DateOnly FyStart = new(2024, 4, 1);
    private static readonly DateOnly FyEnd = new(2025, 3, 31);

    [Fact]
    public void ScheduleII_SLM_FullYear_CalculatesAccurateDepreciation()
    {
        // Gross Cost: 100,000, Salvage: 5,000 (5%), Life: 5 years -> Depreciable: 95,000 -> Annual: 19,000
        decimal grossCost = 100_000m;
        decimal salvageValue = 5_000m;
        decimal usefulLife = 5m;
        decimal openingBook = 100_000m;
        DateOnly capDate = new(2024, 4, 1);

        var (bookDepr, closingBook, activeDays) = DualDepreciationEngine.CalculateScheduleIIDepreciation(
            grossCost, salvageValue, usefulLife, CompanyDepreciationMethod.SLM,
            ShiftType.SingleShift, openingBook, capDate, null, FyStart, FyEnd);

        Assert.Equal(365, activeDays);
        Assert.Equal(19_000m, bookDepr);
        Assert.Equal(81_000m, closingBook);
    }

    [Fact]
    public void ScheduleII_SLM_ProRata_CapitalizedMidYear()
    {
        // Capitalized October 1, 2024. Active days: Oct 1 to Mar 31 = 182 days.
        decimal grossCost = 100_000m;
        decimal salvageValue = 5_000m;
        decimal usefulLife = 5m;
        decimal openingBook = 100_000m;
        DateOnly capDate = new(2024, 10, 1);

        var (bookDepr, closingBook, activeDays) = DualDepreciationEngine.CalculateScheduleIIDepreciation(
            grossCost, salvageValue, usefulLife, CompanyDepreciationMethod.SLM,
            ShiftType.SingleShift, openingBook, capDate, null, FyStart, FyEnd);

        Assert.Equal(182, activeDays);
        // 19,000 * (182 / 365) = 9,473.97
        Assert.Equal(9_473.97m, bookDepr);
        Assert.Equal(90_526.03m, closingBook);
    }

    [Fact]
    public void ScheduleII_ShiftDepreciation_DoubleShift_IncreasesRateBy50Percent()
    {
        decimal grossCost = 100_000m;
        decimal salvageValue = 5_000m;
        decimal usefulLife = 5m;
        decimal openingBook = 100_000m;
        DateOnly capDate = new(2024, 4, 1);

        var (bookDepr, closingBook, _) = DualDepreciationEngine.CalculateScheduleIIDepreciation(
            grossCost, salvageValue, usefulLife, CompanyDepreciationMethod.SLM,
            ShiftType.DoubleShift, openingBook, capDate, null, FyStart, FyEnd);

        // 19,000 * 1.5 = 28,500
        Assert.Equal(28_500m, bookDepr);
        Assert.Equal(71_500m, closingBook);
    }

    [Fact]
    public void ScheduleII_ShiftDepreciation_TripleShift_DoublesRate()
    {
        decimal grossCost = 100_000m;
        decimal salvageValue = 5_000m;
        decimal usefulLife = 5m;
        decimal openingBook = 100_000m;
        DateOnly capDate = new(2024, 4, 1);

        var (bookDepr, closingBook, _) = DualDepreciationEngine.CalculateScheduleIIDepreciation(
            grossCost, salvageValue, usefulLife, CompanyDepreciationMethod.SLM,
            ShiftType.TripleShift, openingBook, capDate, null, FyStart, FyEnd);

        // 19,000 * 2.0 = 38,000
        Assert.Equal(38_000m, bookDepr);
        Assert.Equal(62_000m, closingBook);
    }

    [Fact]
    public void ScheduleII_WDV_Depreciation_CapsAtSalvageValue()
    {
        decimal grossCost = 100_000m;
        decimal salvageValue = 5_000m;
        decimal usefulLife = 5m;
        decimal openingBook = 7_000m; // Close to 5,000 salvage
        DateOnly capDate = new(2020, 4, 1);

        var (bookDepr, closingBook, _) = DualDepreciationEngine.CalculateScheduleIIDepreciation(
            grossCost, salvageValue, usefulLife, CompanyDepreciationMethod.WDV,
            ShiftType.SingleShift, openingBook, capDate, null, FyStart, FyEnd);

        // Max depreciation allowed is 7,000 - 5,000 = 2,000
        Assert.True(bookDepr <= 2_000m);
        Assert.True(closingBook >= salvageValue);
    }

    [Fact]
    public void Section32_PutToUseLessThan180Days_RestrictsTo50PercentRate()
    {
        // Capitalized on December 1, 2024. Days = 121 (< 180 days).
        // 15% normal rate -> restricted to 7.5%
        decimal grossCost = 100_000m;
        decimal blockRate = 15m;
        decimal openingTaxWdv = 100_000m;
        DateOnly capDate = new(2024, 12, 1);

        var (taxDepr, closingWdv, isUnder180, days) = DualDepreciationEngine.CalculateSection32Depreciation(
            grossCost, blockRate, false, openingTaxWdv, capDate, null, FyStart, FyEnd);

        Assert.True(isUnder180);
        Assert.Equal(121, days);
        // 100,000 * 15% * 0.5 = 7,500
        Assert.Equal(7_500m, taxDepr);
        Assert.Equal(92_500m, closingWdv);
    }

    [Fact]
    public void Section32_PutToUseMoreThan180Days_Allows100PercentRate()
    {
        // Capitalized on May 1, 2024. Days = 335 (>= 180 days).
        // 15% normal rate -> full 15%
        decimal grossCost = 100_000m;
        decimal blockRate = 15m;
        decimal openingTaxWdv = 100_000m;
        DateOnly capDate = new(2024, 5, 1);

        var (taxDepr, closingWdv, isUnder180, days) = DualDepreciationEngine.CalculateSection32Depreciation(
            grossCost, blockRate, false, openingTaxWdv, capDate, null, FyStart, FyEnd);

        Assert.False(isUnder180);
        Assert.Equal(335, days);
        // 100,000 * 15% = 15,000
        Assert.Equal(15_000m, taxDepr);
        Assert.Equal(85_000m, closingWdv);
    }

    [Fact]
    public void Section32_AdditionalDepreciation_Manufacturing_Applies20PercentOr10Percent()
    {
        decimal grossCost = 100_000m;
        decimal blockRate = 15m;
        decimal openingTaxWdv = 100_000m;

        // Case A: >= 180 days (Capitalized April 15) -> 15% normal + 20% additional = 35%
        var (taxDeprA, _, isUnder180A, _) = DualDepreciationEngine.CalculateSection32Depreciation(
            grossCost, blockRate, true, openingTaxWdv, new DateOnly(2024, 4, 15), null, FyStart, FyEnd);
        Assert.False(isUnder180A);
        Assert.Equal(35_000m, taxDeprA);

        // Case B: < 180 days (Capitalized Nov 1) -> 7.5% normal (50% of 15%) + 10% additional (half of 20%) = 17.5%
        var (taxDeprB, _, isUnder180B, _) = DualDepreciationEngine.CalculateSection32Depreciation(
            grossCost, blockRate, true, openingTaxWdv, new DateOnly(2024, 11, 1), null, FyStart, FyEnd);
        Assert.True(isUnder180B);
        Assert.Equal(17_500m, taxDeprB);
    }

    [Fact]
    public void DeferredTax_TaxDeprGreaterThanBookDepr_GeneratesDeferredTaxLiability()
    {
        // Tax Depr = 35,000; Book Depr = 19,000 -> Timing Diff = +16,000
        // Corporate Tax Rate = 25.17% (Section 115BAA)
        // DTL = 16,000 * 0.2517 = 4,027.20 (Positive indicates DTL)
        decimal taxDepr = 35_000m;
        decimal bookDepr = 19_000m;

        var (timingDiff, deferredTax) = DualDepreciationEngine.CalculateDeferredTax(taxDepr, bookDepr, 0.2517m);

        Assert.Equal(16_000m, timingDiff);
        Assert.Equal(4_027.20m, deferredTax);
    }

    [Fact]
    public void DeferredTax_BookDeprGreaterThanTaxDepr_GeneratesDeferredTaxAsset()
    {
        // Tax Depr = 7,500; Book Depr = 19,000 -> Timing Diff = -11,500
        // DTA = -11,500 * 0.2517 = -2,894.55 (Negative indicates DTA)
        decimal taxDepr = 7_500m;
        decimal bookDepr = 19_000m;

        var (timingDiff, deferredTax) = DualDepreciationEngine.CalculateDeferredTax(taxDepr, bookDepr, 0.2517m);

        Assert.Equal(-11_500m, timingDiff);
        Assert.Equal(-2_894.55m, deferredTax);
    }

    [Fact]
    public void FixedAsset_GrossCostCalculation_DeductsGstItcClaimed()
    {
        var asset = new FixedAsset
        {
            PurchaseCost = 100_000m,
            InwardFreight = 5_000m,
            InstallationCost = 10_000m,
            PreOperatingCost = 5_000m,
            GstItcClaimed = 18_000m
        };

        asset.RecalculateGrossCost();

        // 100,000 + 5,000 + 10,000 + 5,000 - 18,000 = 102,000
        Assert.Equal(102_000m, asset.GrossCost);
        // Default salvage (5% of 102,000) = 5,100
        Assert.Equal(5_100m, asset.SalvageValue);
    }
}
