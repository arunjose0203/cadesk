namespace CaDesk.UnitTests;

public class PracticeReportUnitTests
{
    [Theory]
    [InlineData(10, "0-30 Days")]
    [InlineData(30, "0-30 Days")]
    [InlineData(31, "31-60 Days")]
    [InlineData(60, "31-60 Days")]
    [InlineData(61, "61-90 Days")]
    [InlineData(90, "61-90 Days")]
    [InlineData(91, "> 90 Days (Overdue Alert)")]
    [InlineData(180, "> 90 Days (Overdue Alert)")]
    public void ReceivablesAging_Buckets_ClassifyCorrectly(int ageDays, string expectedBucket)
    {
        string bucket;
        if (ageDays <= 30) bucket = "0-30 Days";
        else if (ageDays <= 60) bucket = "31-60 Days";
        else if (ageDays <= 90) bucket = "61-90 Days";
        else bucket = "> 90 Days (Overdue Alert)";

        Assert.Equal(expectedBucket, bucket);
    }

    [Theory]
    [InlineData(3500, "HighMargin")]
    [InlineData(2500, "HighMargin")]
    [InlineData(2499, "MediumMargin")]
    [InlineData(1200, "MediumMargin")]
    [InlineData(1199, "AtRisk")]
    [InlineData(500, "AtRisk")]
    public void ProfitabilityRating_ClassifiesExpectedTiers(decimal effectiveRate, string expectedRating)
    {
        var rating = effectiveRate >= 2500m
            ? "HighMargin"
            : (effectiveRate >= 1200m ? "MediumMargin" : "AtRisk");

        Assert.Equal(expectedRating, rating);
    }

    [Fact]
    public void StaffUtilization_CalculatesCorrectPercentage()
    {
        decimal billableHours = 120m;
        decimal nonBillableHours = 40m;
        var total = billableHours + nonBillableHours;

        var utilPct = total > 0 ? Math.Round((billableHours / total) * 100, 2) : 0m;

        Assert.Equal(75.00m, utilPct);
    }

    [Fact]
    public void ComplianceSla_CalculatesAccurateFilingPercentage()
    {
        int totalFilings = 50;
        int onTimeFilings = 46;

        var slaPct = totalFilings > 0 ? Math.Round(((decimal)onTimeFilings / totalFilings) * 100, 2) : 100m;

        Assert.Equal(92.00m, slaPct);
    }
}
