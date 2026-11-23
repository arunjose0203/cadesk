using CaDesk.Domain.PracticeHealth;
using Xunit;

namespace CaDesk.UnitTests;

public class PracticeHealthUnitTests
{
    [Fact]
    public void ComputeValuation_CalculatesDcfAndMultipleValuationAccurately()
    {
        // Arrange: Firm with ₹2.0 Cr gross revenue, ₹50L EBITDA, 15% growth, 14% WACC, 5% terminal growth
        decimal grossRevenue = 20000000m; // 2 Cr
        decimal recurringRevenue = 14000000m; // 70% recurring
        decimal advisoryRevenue = 6000000m;
        decimal ebitda = 5000000m; // 50L (25% margin)
        decimal partnerComp = 3000000m;
        decimal netTangibleAssets = 4000000m; // 40L
        decimal growth = 0.12m;
        decimal wacc = 0.14m;
        decimal terminalG = 0.05m;
        int years = 5;

        // Act
        var result = PracticeValuationEngine.ComputeValuation(
            grossRevenue: grossRevenue,
            recurringRevenue: recurringRevenue,
            advisoryRevenue: advisoryRevenue,
            ebitda: ebitda,
            partnerNormalizedComp: partnerComp,
            netTangibleAssets: netTangibleAssets,
            revenueGrowthRate: growth,
            discountRateWacc: wacc,
            terminalGrowthRate: terminalG,
            forecastYears: years,
            revenueMultipleBase: 1.35m,
            ebitdaMultipleBase: 5.50m,
            auditFranchiseStickinessPremiumPct: 0.10m,
            partnerConcentrationDiscountPct: 0.10m,
            recurringRevenuePremiumPct: 0.15m,
            method: ValuationMethod.BlendedWeight
        );

        // Assert
        Assert.True(result.DcfEnterpriseValue > 0, "DCF valuation should be positive");
        Assert.True(result.MultipleEnterpriseValue > 0, "Multiple valuation should be positive");
        Assert.True(result.BlendedFirmFairMarketValue > 0, "Blended FMV should be positive");
        Assert.True(result.FirmGoodwillValue > 0, "Goodwill should be positive");
        Assert.Equal(result.BlendedFirmFairMarketValue - netTangibleAssets, result.FirmGoodwillValue);
        Assert.Equal(Math.Round(result.BlendedFirmFairMarketValue / 100m, 2), result.ValuePerEquityPoint);
        Assert.Equal(5, result.ProjectedCashFlows.Count);
    }

    [Fact]
    public void ComputeValuation_AppliesRecurringRevenueAndAuditStickinessPremiums()
    {
        decimal grossRevenue = 10000000m; // 1 Cr
        decimal ebitda = 2500000m;

        // High quality firm: 80% recurring, +10% audit stickiness, 0% concentration discount
        var premiumResult = PracticeValuationEngine.ComputeValuation(
            grossRevenue: grossRevenue,
            recurringRevenue: 8000000m,
            advisoryRevenue: 2000000m,
            ebitda: ebitda,
            partnerNormalizedComp: 1000000m,
            netTangibleAssets: 1000000m,
            revenueGrowthRate: 0.10m,
            discountRateWacc: 0.14m,
            terminalGrowthRate: 0.05m,
            forecastYears: 5,
            revenueMultipleBase: 1.20m,
            ebitdaMultipleBase: 5.0m,
            auditFranchiseStickinessPremiumPct: 0.10m,
            partnerConcentrationDiscountPct: 0m,
            recurringRevenuePremiumPct: 0.15m,
            method: ValuationMethod.RevenueMultiple
        );

        // Low quality firm: 30% recurring (no recurring premium), 20% concentration discount
        var discountedResult = PracticeValuationEngine.ComputeValuation(
            grossRevenue: grossRevenue,
            recurringRevenue: 3000000m,
            advisoryRevenue: 7000000m,
            ebitda: ebitda,
            partnerNormalizedComp: 1000000m,
            netTangibleAssets: 1000000m,
            revenueGrowthRate: 0.10m,
            discountRateWacc: 0.14m,
            terminalGrowthRate: 0.05m,
            forecastYears: 5,
            revenueMultipleBase: 1.20m,
            ebitdaMultipleBase: 5.0m,
            auditFranchiseStickinessPremiumPct: 0m,
            partnerConcentrationDiscountPct: 0.20m,
            recurringRevenuePremiumPct: 0.15m,
            method: ValuationMethod.RevenueMultiple
        );

        Assert.True(premiumResult.EffectiveRevenueMultiple > discountedResult.EffectiveRevenueMultiple);
        Assert.True(premiumResult.BlendedFirmFairMarketValue > discountedResult.BlendedFirmFairMarketValue);
    }

    [Fact]
    public void ComputeRetirementAmortization_GeneratesCorrectInstallmentsAndZerosOutEndingBalance()
    {
        // ₹60L buyout over 5 years at 8% annual interest
        decimal totalPayout = 6000000m;
        int years = 5;
        decimal interestRate = 8.0m;

        var schedule = PracticeValuationEngine.ComputeRetirementAmortization(
            totalPayoutAmount: totalPayout,
            payoutYears: years,
            annualInterestRatePct: interestRate
        );

        Assert.Equal(5, schedule.Count);

        // Year 1 begins with full balance
        Assert.Equal(totalPayout, schedule[0].BeginningBalance);

        // Year 5 ends with zero balance
        Assert.Equal(0, schedule[4].EndingBalance);

        // Sum of all principal paid must equal initial total payout
        decimal totalPrincipal = schedule.Sum(x => x.PrincipalPaid);
        Assert.Equal(totalPayout, totalPrincipal);

        // Every installment has interest component > 0
        Assert.All(schedule, item => Assert.True(item.InterestPaid > 0));
    }

    [Fact]
    public void ComputeRetirementAmortization_HandlesZeroInterestGracefully()
    {
        decimal totalPayout = 5000000m;
        int years = 5;
        decimal interestRate = 0m;

        var schedule = PracticeValuationEngine.ComputeRetirementAmortization(totalPayout, years, interestRate);

        Assert.Equal(5, schedule.Count);
        Assert.All(schedule, item => Assert.Equal(1000000m, item.TotalInstallment));
        Assert.All(schedule, item => Assert.Equal(0m, item.InterestPaid));
        Assert.Equal(0m, schedule.Last().EndingBalance);
    }

    [Fact]
    public void ComputePracticeHealthScore_AwardsExcellenceGradeForHighPerformanceFirm()
    {
        // Firm meeting or exceeding all Tier 2 benchmarks
        var (score, grade) = PracticeValuationEngine.ComputePracticeHealthScore(
            actualRevPerPartner: 15000000m,   // Above 120L
            benchmarkRevPerPartner: 12000000m,
            actualRealizationRate: 90.0m,     // Above 84%
            benchmarkRealizationRate: 84.0m,
            actualDsoDays: 45.0m,             // Lower than 60 days
            benchmarkDsoDays: 60.0m,
            actualLeverage: 6.0m,             // Above 5.0
            benchmarkLeverage: 5.0m,
            actualMargin: 30.0m,              // Above 28%
            benchmarkMargin: 28.0m
        );

        Assert.True(score >= 90m, $"Score should be >= 90, got {score}");
        Assert.Contains("Premier Excellence", grade);
    }

    [Fact]
    public void ComputePracticeHealthScore_PenalizesHighDsoAndLowRealization()
    {
        // Stressed firm with poor collection and realization
        var (score, grade) = PracticeValuationEngine.ComputePracticeHealthScore(
            actualRevPerPartner: 5000000m,
            benchmarkRevPerPartner: 12000000m,
            actualRealizationRate: 60.0m,
            benchmarkRealizationRate: 84.0m,
            actualDsoDays: 110.0m,            // Much higher than 60 days
            benchmarkDsoDays: 60.0m,
            actualLeverage: 2.0m,
            benchmarkLeverage: 5.0m,
            actualMargin: 15.0m,
            benchmarkMargin: 28.0m
        );

        Assert.True(score < 50m, $"Score should be under 50, got {score}");
        Assert.True(grade.StartsWith("C") || grade.StartsWith("D"), $"Grade should be C or D, got {grade}");
    }
}
