using CaDesk.Domain.Gstr9;
using Xunit;

namespace CaDesk.UnitTests;

public class Gstr9UnitTests
{
    [Fact]
    public void Gstr9ThresholdEngine_TurnoverBelow2Cr_IsExempt()
    {
        var turnover = 15_000_000m; // ₹1.5 Crores
        var isMandatory9 = Gstr9ThresholdEngine.IsGstr9Mandatory(turnover);
        var isMandatory9c = Gstr9ThresholdEngine.IsGstr9cMandatory(turnover);
        var status = Gstr9ThresholdEngine.GetExemptionStatus(turnover);

        Assert.False(isMandatory9);
        Assert.False(isMandatory9c);
        Assert.Contains("Exempt", status);
    }

    [Fact]
    public void Gstr9ThresholdEngine_TurnoverBetween2CrAnd5Cr_IsGstr9Only()
    {
        var turnover = 35_000_000m; // ₹3.5 Crores
        var isMandatory9 = Gstr9ThresholdEngine.IsGstr9Mandatory(turnover);
        var isMandatory9c = Gstr9ThresholdEngine.IsGstr9cMandatory(turnover);
        var status = Gstr9ThresholdEngine.GetExemptionStatus(turnover);

        Assert.True(isMandatory9);
        Assert.False(isMandatory9c);
        Assert.Contains("GSTR-9C Self-Certification Exempt", status);
    }

    [Fact]
    public void Gstr9ThresholdEngine_TurnoverAbove5Cr_IsGstr9And9cMandatory()
    {
        var turnover = 85_000_000m; // ₹8.5 Crores
        var isMandatory9 = Gstr9ThresholdEngine.IsGstr9Mandatory(turnover);
        var isMandatory9c = Gstr9ThresholdEngine.IsGstr9cMandatory(turnover);
        var status = Gstr9ThresholdEngine.GetExemptionStatus(turnover);

        Assert.True(isMandatory9);
        Assert.True(isMandatory9c);
        Assert.Contains("Mandatory GSTR-9 and GSTR-9C", status);
    }

    [Fact]
    public void Table5Turnover_CalculatesStatutoryFormulaCorrectly()
    {
        // Table 5N = Outward Taxable (4N) + Outward Exempt (5M) - Inward RCM (4G)
        var outwardTaxable4N = 1_000_000m;
        var outwardExempt5M = 200_000m;
        var rcmInward4G = 50_000m;

        var totalTurnover5N = Gstr9CalculationEngine.CalculateTable5Turnover(outwardTaxable4N, outwardExempt5M, rcmInward4G);

        Assert.Equal(1_150_000m, totalTurnover5N);
    }

    [Fact]
    public void Table8ItcVariance_CalculatesDifferenceCorrectly()
    {
        // Table 8D = 8A - (8B + 8C)
        var itc2a8A = 500_000m;
        var itcClaimed8B = 450_000m;
        var itcNextFy8C = 30_000m;

        var variance8D = Gstr9CalculationEngine.CalculateTable8ItcVariance(itc2a8A, itcClaimed8B, itcNextFy8C);

        Assert.Equal(20_000m, variance8D);
    }

    [Fact]
    public void Table8ItcVariance_NegativeDifference_DetectsExcessClaimRisk()
    {
        // If 2A is less than 3B claimed -> negative variance (demand risk)
        var itc2a8A = 400_000m;
        var itcClaimed8B = 460_000m;
        var itcNextFy8C = 0m;

        var variance8D = Gstr9CalculationEngine.CalculateTable8ItcVariance(itc2a8A, itcClaimed8B, itcNextFy8C);

        Assert.Equal(-60_000m, variance8D);
    }

    [Fact]
    public void Table5GrossTurnoverReconciliation_CalculatesAdditionsDeductionsAndVariance()
    {
        var auditedGross5A = 10_000_000m;
        var unbilledStart5B = 100_000m;
        var unadjustedAdvEnd5C = 200_000m;
        var deemed5D = 50_000m;
        var creditNotesAfterFy5E = 150_000m;
        var tradeDiscountsDisallowed5F = 50_000m;
        var unbilledEnd5H = 80_000m;
        var unadjustedAdvStart5I = 120_000m;
        var otherAdjustments5O = 20_000m;
        var gstr9Turnover5Q = 10_300_000m;

        var (reconciled5P, variance5R) = Gstr9CalculationEngine.CalculateTable5GrossTurnover(
            auditedGross5A, unbilledStart5B, unadjustedAdvEnd5C, deemed5D, creditNotesAfterFy5E,
            tradeDiscountsDisallowed5F, unbilledEnd5H, unadjustedAdvStart5I, otherAdjustments5O, gstr9Turnover5Q);

        // 10,000,000 + 570,000 - 200,000 = 10,370,000
        Assert.Equal(10_370_000m, reconciled5P);
        Assert.Equal(70_000m, variance5R);
    }

    [Fact]
    public void Table7TaxableTurnoverReconciliation_DeductsExemptionsAndRcm()
    {
        var reconciledGross5P = 10_370_000m;
        var exempt7B = 1_000_000m;
        var zeroRated7C = 500_000m;
        var rcm7D = 200_000m;
        var gstr9Taxable7F = 8_600_000m;

        var (reconciledTaxable7E, variance7G) = Gstr9CalculationEngine.CalculateTable7TaxableTurnover(
            reconciledGross5P, exempt7B, zeroRated7C, rcm7D, gstr9Taxable7F);

        // 10,370,000 - 1,700,000 = 8,670,000
        Assert.Equal(8_670_000m, reconciledTaxable7E);
        Assert.Equal(70_000m, variance7G);
    }

    [Fact]
    public void Table12ItcReconciliation_CalculatesBookVsGstr9Variance()
    {
        var bookItc12A = 1_500_000m;
        var gstr9Itc12E = 1_480_000m;

        var variance12F = Gstr9CalculationEngine.CalculateTable12ItcVariance(bookItc12A, gstr9Itc12E);

        Assert.Equal(20_000m, variance12F);
    }

    [Fact]
    public void TableEntry_TotalTax_SumsAllFourComponents()
    {
        var entry = new Gstr9TableEntry
        {
            IgstAmount = 100m,
            CgstAmount = 50m,
            SgstAmount = 50m,
            CessAmount = 10m
        };

        Assert.Equal(210m, entry.TotalTax);
    }
}
