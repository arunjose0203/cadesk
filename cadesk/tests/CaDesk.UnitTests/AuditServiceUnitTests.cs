using CaDesk.Application.Audit;
using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.UnitTests;

public class AuditServiceUnitTests
{
    [Fact]
    public void Materiality_RevenueBenchmark_CalculatesStandardRates()
    {
        // Gross turnover: 10 Crores (100,000,000)
        // Default: 1% overall = 1,000,000
        // Performance: 75% = 750,000
        // Trivial: 5% = 50,000
        var (overall, perf, trivial) = AuditService.CalculateMateriality(BenchmarkType.Revenue, 100_000_000m);

        Assert.Equal(1_000_000m, overall);
        Assert.Equal(750_000m, perf);
        Assert.Equal(50_000m, trivial);
    }

    [Fact]
    public void Materiality_ProfitBeforeTaxBenchmark_CalculatesFivePercentRate()
    {
        // Profit Before Tax: 2 Crores (20,000,000)
        // Default: 5% of PBT = 1,000,000
        // Performance: 75% = 750,000
        // Trivial: 5% = 50,000
        var (overall, perf, trivial) = AuditService.CalculateMateriality(BenchmarkType.ProfitBeforeTax, 20_000_000m);

        Assert.Equal(1_000_000m, overall);
        Assert.Equal(750_000m, perf);
        Assert.Equal(50_000m, trivial);
    }

    [Fact]
    public void Materiality_CustomPercentages_CalculatesCorrectly()
    {
        // 50,000,000 with custom 2% overall, 80% performance, 3% trivial
        var (overall, perf, trivial) = AuditService.CalculateMateriality(
            BenchmarkType.Revenue,
            50_000_000m,
            customOverallPercent: 2m,
            customPerfPercent: 80m,
            customTrivialPercent: 3m
        );

        Assert.Equal(1_000_000m, overall); // 2% of 50M
        Assert.Equal(800_000m, perf);      // 80% of 1M
        Assert.Equal(30_000m, trivial);     // 3% of 1M
    }

    [Fact]
    public void Materiality_ZeroBenchmark_ReturnsZeroes()
    {
        var (overall, perf, trivial) = AuditService.CalculateMateriality(BenchmarkType.TotalAssets, 0m);

        Assert.Equal(0m, overall);
        Assert.Equal(0m, perf);
        Assert.Equal(0m, trivial);
    }
}
