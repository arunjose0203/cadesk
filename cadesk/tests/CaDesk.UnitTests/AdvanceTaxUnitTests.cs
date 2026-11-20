using CaDesk.Domain.AdvanceTax;
using Xunit;

namespace CaDesk.UnitTests;

public class AdvanceTaxUnitTests
{
    [Fact]
    public void Section208_AssessedTaxLessThan10000_ExemptsFromAdvanceTaxAndInterest()
    {
        decimal assessedTax = 9_500m; // Less than 10,000 threshold
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            Guid.NewGuid(), "2024-25", assessedTax, false);

        AdvanceTaxInterestEngine.RecalculateSection234C(installments, assessedTax, false);

        foreach (var inst in installments)
        {
            Assert.Equal(0m, inst.Interest234c);
            Assert.Equal(0m, inst.ShortfallAmount);
        }

        var (is234bApp, months234b, interest234b) = AdvanceTaxInterestEngine.CalculateSection234B(
            assessedTax, 0m, new DateOnly(2025, 7, 31), 2025);

        Assert.False(is234bApp);
        Assert.Equal(0, months234b);
        Assert.Equal(0m, interest234b);
    }

    [Fact]
    public void Section211_StandardSchedule_GeneratesFourInstallments()
    {
        decimal assessedTax = 100_000m;
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            Guid.NewGuid(), "2024-25", assessedTax, false);

        Assert.Equal(4, installments.Count);

        // Inst 1: June 15, 15% = 15,000
        Assert.Equal(1, installments[0].InstallmentNumber);
        Assert.Equal(new DateOnly(2024, 6, 15), installments[0].DueDate);
        Assert.Equal(15m, installments[0].TargetPercentage);
        Assert.Equal(15_000m, installments[0].CumulativeTargetAmount);

        // Inst 2: Sept 15, 45% = 45,000
        Assert.Equal(2, installments[1].InstallmentNumber);
        Assert.Equal(new DateOnly(2024, 9, 15), installments[1].DueDate);
        Assert.Equal(45m, installments[1].TargetPercentage);
        Assert.Equal(45_000m, installments[1].CumulativeTargetAmount);

        // Inst 3: Dec 15, 75% = 75,000
        Assert.Equal(3, installments[2].InstallmentNumber);
        Assert.Equal(new DateOnly(2024, 12, 15), installments[2].DueDate);
        Assert.Equal(75m, installments[2].TargetPercentage);
        Assert.Equal(75_000m, installments[2].CumulativeTargetAmount);

        // Inst 4: March 15, 100% = 100,000
        Assert.Equal(4, installments[3].InstallmentNumber);
        Assert.Equal(new DateOnly(2025, 3, 15), installments[3].DueDate);
        Assert.Equal(100m, installments[3].TargetPercentage);
        Assert.Equal(100_000m, installments[3].CumulativeTargetAmount);
    }

    [Fact]
    public void Section44AD_Presumptive_GeneratesSingleMarch15Installment()
    {
        decimal assessedTax = 150_000m;
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            Guid.NewGuid(), "2024-25", assessedTax, isPresumptive: true);

        Assert.Single(installments);
        Assert.Equal(1, installments[0].InstallmentNumber);
        Assert.Equal(new DateOnly(2025, 3, 15), installments[0].DueDate);
        Assert.Equal(100m, installments[0].TargetPercentage);
        Assert.Equal(150_000m, installments[0].CumulativeTargetAmount);
    }

    [Fact]
    public void Section234C_SafeHarbor_12Percent_InJune_WaivesInterest()
    {
        decimal assessedTax = 100_000m;
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            Guid.NewGuid(), "2024-25", assessedTax, false);

        // Paid 12,000 (exactly 12% safe harbor)
        installments[0].AmountPaid = 12_000m;

        AdvanceTaxInterestEngine.RecalculateSection234C(installments, assessedTax, false);

        Assert.Equal(0m, installments[0].Interest234c);
        Assert.Equal(0m, installments[0].ShortfallAmount);
    }

    [Fact]
    public void Section234C_Shortfall_InJune_Charges3MonthsInterestAt1Percent()
    {
        decimal assessedTax = 100_000m;
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            Guid.NewGuid(), "2024-25", assessedTax, false);

        // Paid 5,000 (< 12,000 safe harbor). Target is 15,000. Shortfall is 10,000.
        installments[0].AmountPaid = 5_000m;

        AdvanceTaxInterestEngine.RecalculateSection234C(installments, assessedTax, false);

        Assert.Equal(10_000m, installments[0].ShortfallAmount);
        // 10,000 * 1% * 3 months = 300
        Assert.Equal(300m, installments[0].Interest234c);
    }

    [Fact]
    public void Section234C_CumulativeInstallments_September_SafeHarbor36Percent()
    {
        decimal assessedTax = 100_000m;
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            Guid.NewGuid(), "2024-25", assessedTax, false);

        // June paid: 15,000. Sept paid: 22,000 -> Cumulative = 37,000 (>= 36,000 safe harbor)
        installments[0].AmountPaid = 15_000m;
        installments[1].AmountPaid = 22_000m;

        AdvanceTaxInterestEngine.RecalculateSection234C(installments, assessedTax, false);

        Assert.Equal(0m, installments[0].Interest234c);
        Assert.Equal(0m, installments[1].Interest234c);
    }

    [Fact]
    public void Section234B_AdvanceTaxPaidAtLeast90Percent_NoInterestCharged()
    {
        decimal assessedTax = 100_000m;
        // Total advance tax paid by March 31 = 90,000 (90%)
        decimal totalAdvancePaid = 90_000m;

        var (isApplicable, months, interest) = AdvanceTaxInterestEngine.CalculateSection234B(
            assessedTax, totalAdvancePaid, new DateOnly(2025, 7, 31), 2025);

        Assert.False(isApplicable);
        Assert.Equal(0, months);
        Assert.Equal(0m, interest);
    }

    [Fact]
    public void Section234B_AdvanceTaxPaidLessThan90Percent_Charges1PercentPerMonthFromApril1()
    {
        decimal assessedTax = 100_000m;
        // Total advance tax paid = 60,000 (< 90%). Shortfall = 40,000.
        decimal totalAdvancePaid = 60_000m;
        // Assessment date = July 15, 2025. April 1 to July 15 = 4 months (Apr, May, Jun, Jul).
        DateOnly calcDate = new(2025, 7, 15);

        var (isApplicable, months, interest) = AdvanceTaxInterestEngine.CalculateSection234B(
            assessedTax, totalAdvancePaid, calcDate, 2025);

        Assert.True(isApplicable);
        Assert.Equal(4, months);
        // 40,000 * 1% * 4 months = 1,600
        Assert.Equal(1_600m, interest);
    }

    [Fact]
    public void Section234A_DelayInFilingReturn_Charges1PercentPerMonthFromDueDate()
    {
        decimal assessedTax = 100_000m;
        decimal advanceTaxPaid = 70_000m;
        decimal selfAssessmentBeforeDueDate = 10_000m;
        // Remaining tax payable = 100,000 - 70,000 - 10,000 = 20,000
        DateOnly dueDate = new(2025, 7, 31);
        // Actual filing date: October 15, 2025. Delay: Aug, Sept, Oct = 3 months
        DateOnly filingDate = new(2025, 10, 15);

        var (months, interest) = AdvanceTaxInterestEngine.CalculateSection234A(
            assessedTax, advanceTaxPaid, selfAssessmentBeforeDueDate, dueDate, filingDate);

        Assert.Equal(3, months);
        // 20,000 * 1% * 3 months = 600
        Assert.Equal(600m, interest);
    }

    [Fact]
    public void Section207_SeniorCitizen_NoBusinessIncome_ExemptFromAdvanceTax()
    {
        decimal assessedTax = 100_000m;
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            Guid.NewGuid(), "2024-25", assessedTax, false);

        // Senior citizen marked exempt
        AdvanceTaxInterestEngine.RecalculateSection234C(installments, assessedTax, false, isSeniorCitizenExempt: true);

        foreach (var inst in installments)
        {
            Assert.Equal(0m, inst.Interest234c);
            Assert.Equal(0m, inst.ShortfallAmount);
        }
    }
}
