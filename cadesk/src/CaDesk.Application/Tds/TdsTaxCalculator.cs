using System.Text.RegularExpressions;
using CaDesk.Domain.Tds;

namespace CaDesk.Application.Tds;

public static class TdsTaxCalculator
{
    private static readonly Regex PanRegex = new("^[A-Z]{5}[0-9]{4}[A-Z]{1}$", RegexOptions.Compiled);

    public static decimal GetStandardSectionRate(string section)
    {
        var s = section.Trim();
        if (string.Equals(s, TdsSection.Sec194CCompany, StringComparison.OrdinalIgnoreCase)) return 1.0m;
        if (string.Equals(s, TdsSection.Sec194COther, StringComparison.OrdinalIgnoreCase)) return 2.0m;
        if (string.Equals(s, TdsSection.Sec194JTech, StringComparison.OrdinalIgnoreCase)) return 2.0m;
        if (string.Equals(s, TdsSection.Sec194JFees, StringComparison.OrdinalIgnoreCase)) return 10.0m;
        if (string.Equals(s, TdsSection.Sec194ILand, StringComparison.OrdinalIgnoreCase)) return 10.0m;
        if (string.Equals(s, TdsSection.Sec194IPlant, StringComparison.OrdinalIgnoreCase)) return 2.0m;
        if (string.Equals(s, TdsSection.Sec194Q, StringComparison.OrdinalIgnoreCase)) return 0.1m;
        if (string.Equals(s, TdsSection.Sec194A, StringComparison.OrdinalIgnoreCase)) return 10.0m;
        if (string.Equals(s, TdsSection.Sec194H, StringComparison.OrdinalIgnoreCase)) return 5.0m;
        if (string.Equals(s, TdsSection.Sec195, StringComparison.OrdinalIgnoreCase)) return 20.0m;
        return 10.0m;
    }

    public static (decimal FinalRate, string Reason) DetermineRate(
        string section,
        string deducteePan,
        decimal? ldcRate = null,
        bool isNonFiler206Ab = false)
    {
        var pan = deducteePan.Trim().ToUpperInvariant();

        // 1. Check Section 206AA (Invalid / Missing PAN -> Minimum 20%)
        if (!PanRegex.IsMatch(pan) || pan == "PANNOTAVBL")
        {
            return (20.0m, HigherRateReason.NoPan206Aa);
        }

        // 2. Check Section 206AB (Non-filer -> Higher of 2x normal rate or 5%)
        if (isNonFiler206Ab)
        {
            var baseRate = GetStandardSectionRate(section);
            var rate206Ab = Math.Max(baseRate * 2m, 5.0m);
            return (rate206Ab, HigherRateReason.NonFiler206Ab);
        }

        // 3. Lower Deduction Certificate (Section 197)
        if (ldcRate.HasValue && ldcRate.Value >= 0)
        {
            return (ldcRate.Value, HigherRateReason.None);
        }

        // 4. Normal Section Rate
        return (GetStandardSectionRate(section), HigherRateReason.None);
    }

    public static decimal ComputeTaxAmount(decimal amountPaid, decimal rate)
    {
        if (amountPaid <= 0 || rate <= 0) return 0m;
        return Math.Round(amountPaid * (rate / 100m), 2);
    }

    /// <summary>
    /// Computes interest under Section 201(1A):
    /// - 1.0% per month/part for delay in deduction (from payment date to deduction date)
    /// - 1.5% per month/part for delay in deposit (from deduction date to payment date)
    /// </summary>
    public static TdsInterestResultDto CalculateInterest(
        DateOnly deductionDate,
        DateOnly? paymentDate,
        DateOnly depositDate,
        decimal tdsAmount)
    {
        if (tdsAmount <= 0) return new TdsInterestResultDto(0, 0, 0, 0, 0);

        // 1. Deduction delay
        var payDate = paymentDate ?? deductionDate;
        var deductionDelayMonths = 0;
        decimal deductionInterest = 0m;

        if (deductionDate > payDate)
        {
            deductionDelayMonths = CountMonthsOrPart(payDate, deductionDate);
            deductionInterest = Math.Round(tdsAmount * (0.01m * deductionDelayMonths), 2);
        }

        // 2. Deposit delay: statutory due date is 7th of next month (April 30th for March)
        var dueDate = GetStatutoryDueDate(deductionDate);
        var depositDelayMonths = 0;
        decimal depositInterest = 0m;

        if (depositDate > dueDate)
        {
            depositDelayMonths = CountMonthsOrPart(deductionDate, depositDate);
            depositInterest = Math.Round(tdsAmount * (0.015m * depositDelayMonths), 2);
        }

        return new TdsInterestResultDto(
            deductionDelayMonths,
            deductionInterest,
            depositDelayMonths,
            depositInterest,
            deductionInterest + depositInterest
        );
    }

    private static DateOnly GetStatutoryDueDate(DateOnly deductionDate)
    {
        if (deductionDate.Month == 3)
        {
            return new DateOnly(deductionDate.Year, 4, 30);
        }

        var nextMonth = deductionDate.AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month, 7);
    }

    private static int CountMonthsOrPart(DateOnly from, DateOnly to)
    {
        if (to <= from) return 0;
        var yearDiff = to.Year - from.Year;
        var monthDiff = to.Month - from.Month;
        var totalMonths = yearDiff * 12 + monthDiff;

        // If to.Day > from.Day, there is an extra partial month
        if (to.Day > from.Day || (to.Day < from.Day && to.Month != from.Month))
        {
            totalMonths += 1;
        }

        return Math.Max(1, totalMonths);
    }
}
