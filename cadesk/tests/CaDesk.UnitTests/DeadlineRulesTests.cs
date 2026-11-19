using CaDesk.Application.Compliance;
using CaDesk.Domain.Compliance;

namespace CaDesk.UnitTests;

/// <summary>Expected dates were cross-checked with an independent Python version of the same rules.</summary>
public class DeadlineRulesTests
{
    private static readonly DateOnly[] IndiaHolidays = [new(2026, 8, 15), new(2026, 10, 2), new(2027, 1, 26)];
    private static readonly Calendar India = Calendar.FromWeekend([0], IndiaHolidays);           // Sunday off
    private static readonly Calendar Office = new([1, 2, 3, 4, 5, 6], IndiaHolidays);            // Mon–Sat
    private static readonly Calendar Uae = Calendar.FromWeekend([0, 6], []);                    // Sat–Sun off
    private static readonly Calendar Saudi = Calendar.FromWeekend([5, 6], []);                  // Fri–Sat off

    private static DueRule Rule(int monthsAfter, int day, bool shift, int? daysAfter = null, string overrides = "{}") =>
        DueRule.From(new ComplianceType
        {
            DueMonthsAfter = (short)monthsAfter, DueDay = (short)day, DueDaysAfter = (short?)daysAfter,
            PeriodOverrides = overrides, ShiftOnHoliday = shift,
        });

    private static Period Month(int y, int m) => DeadlineRules.Periods("monthly", new(y, m, 1), new(y, m, 1), 4).Single();
    private static Period QuarterEnding(int y, int m, int qe = 3) =>
        DeadlineRules.Periods("quarterly", new(y, m, 1), new(y, m, 1), 4, qe).Single(p => p.End.Month == m);

    private static void Expect(DueDates d, (int, int, int) statutory, (int, int, int) due, (int, int, int) target)
    {
        Assert.Equal(new DateOnly(statutory.Item1, statutory.Item2, statutory.Item3), d.Statutory);
        Assert.Equal(new DateOnly(due.Item1, due.Item2, due.Item3), d.Due);
        Assert.Equal(new DateOnly(target.Item1, target.Item2, target.Item3), d.Internal);
    }

    // ------------------------------------------------------------ periods

    [Fact]
    public void Monthly_periods_cover_the_range()
    {
        var keys = DeadlineRules.Periods("monthly", new(2026, 10, 15), new(2026, 12, 1), 4).Select(p => p.Key);
        Assert.Equal(new[] { "M2026-10", "M2026-11", "M2026-12" }, keys);
    }

    [Fact]
    public void Quarters_follow_the_chosen_pattern()
    {
        var calendarQuarters = DeadlineRules.Periods("quarterly", new(2026, 5, 1), new(2026, 11, 30), 4).ToList();
        Assert.Equal(new[] { "Q2026-06", "Q2026-09", "Q2026-12" }, calendarQuarters.Select(p => p.Key));
        Assert.Equal(new DateOnly(2026, 4, 1), calendarQuarters[0].Start);
        Assert.Equal(new DateOnly(2026, 6, 30), calendarQuarters[0].End);

        // UAE staggered tax periods ending Feb / May / Aug / Nov
        Assert.Equal(new[] { "Q2026-05", "Q2026-08", "Q2026-11" },
            DeadlineRules.Periods("quarterly", new(2026, 5, 1), new(2026, 11, 30), 4, 2).Select(p => p.Key));
    }

    [Fact]
    public void Annual_periods_follow_the_financial_year()
    {
        Assert.Equal(new[] { "FY2025-26", "FY2026-27", "FY2027-28" },
            DeadlineRules.Periods("annual", new(2026, 3, 1), new(2027, 6, 1), 4).Select(p => p.Key));
        Assert.Equal(new[] { "FY2026", "FY2027" },
            DeadlineRules.Periods("annual", new(2026, 3, 1), new(2027, 6, 1), 1).Select(p => p.Key));
    }

    [Theory]
    [InlineData("M2026-10", "Oct 2026")]
    [InlineData("Q2026-12", "Oct–Dec 2026")]
    [InlineData("Q2027-01", "Nov 2026–Jan 2027")]
    [InlineData("FY2026-27", "FY 2026-27")]
    public void Labels(string key, string label) => Assert.Equal(label, DeadlineRules.Label(key));

    // ------------------------------------------------------------ India

    [Fact]
    public void Gstr3b_is_due_on_the_20th_and_the_internal_target_skips_sunday()
    {
        Expect(DeadlineRules.Compute(Rule(1, 20, false), Month(2026, 10), null, India, Office, 3), (2026, 11, 20), (2026, 11, 20), (2026, 11, 17));
        // 20 Jan 2027 minus 3 days = Sunday 17 Jan → Saturday 16 Jan
        Expect(DeadlineRules.Compute(Rule(1, 20, false), Month(2026, 12), null, India, Office, 3), (2027, 1, 20), (2027, 1, 20), (2027, 1, 16));
    }

    [Fact]
    public void Tds_payment_for_march_is_due_30_april_and_a_sunday_due_date_moves_to_monday()
    {
        var tds = Rule(1, 7, true, overrides: """{"3":{"monthsAfter":1,"day":30}}""");
        Expect(DeadlineRules.Compute(tds, Month(2027, 3), null, India, Office, 3), (2027, 4, 30), (2027, 4, 30), (2027, 4, 27));
        Expect(DeadlineRules.Compute(tds, Month(2027, 2), null, India, Office, 3), (2027, 3, 7), (2027, 3, 8), (2027, 3, 5));
    }

    [Fact]
    public void Tds_returns_use_31_may_for_the_last_quarter()
    {
        var rule = Rule(1, 31, true, overrides: """{"3":{"monthsAfter":2,"day":31}}""");
        Expect(DeadlineRules.Compute(rule, QuarterEnding(2027, 3), null, India, Office, 3), (2027, 5, 31), (2027, 5, 31), (2027, 5, 28));
        Expect(DeadlineRules.Compute(rule, QuarterEnding(2026, 12), null, India, Office, 3), (2027, 1, 31), (2027, 2, 1), (2027, 1, 29));
    }

    [Fact]
    public void Advance_tax_falls_inside_the_quarter()
    {
        Expect(DeadlineRules.Compute(Rule(0, 15, true), QuarterEnding(2026, 6), null, India, Office, 3), (2026, 6, 15), (2026, 6, 15), (2026, 6, 12));
        Expect(DeadlineRules.Compute(Rule(0, 15, true), QuarterEnding(2027, 3), null, India, Office, 3), (2027, 3, 15), (2027, 3, 15), (2027, 3, 12));
    }

    [Fact]
    public void Annual_returns_count_from_the_financial_year_end()
    {
        var fy = DeadlineRules.Periods("annual", new(2026, 4, 1), new(2026, 4, 1), 4).Single();
        Expect(DeadlineRules.Compute(Rule(4, 31, true), fy, null, India, Office, 3), (2027, 7, 31), (2027, 7, 31), (2027, 7, 28));
        Expect(DeadlineRules.Compute(Rule(9, 31, false), fy, null, India, Office, 3), (2027, 12, 31), (2027, 12, 31), (2027, 12, 28));
    }

    [Fact]
    public void A_government_extension_replaces_the_rule_date() =>
        Expect(DeadlineRules.Compute(Rule(1, 20, false), Month(2026, 10), new DateOnly(2026, 11, 25), India, Office, 3),
            (2026, 11, 25), (2026, 11, 25), (2026, 11, 21));

    // ------------------------------------------------------------ GCC

    [Fact]
    public void Uae_vat_moves_off_the_weekend()
    {
        Expect(DeadlineRules.Compute(Rule(1, 28, true), QuarterEnding(2026, 9), null, Uae, Uae, 3), (2026, 10, 28), (2026, 10, 28), (2026, 10, 23));
        // 28 Mar 2027 is a Sunday → Monday 29 Mar
        Expect(DeadlineRules.Compute(Rule(1, 28, true), QuarterEnding(2027, 2, 2), null, Uae, Uae, 0), (2027, 3, 28), (2027, 3, 29), (2027, 3, 29));
    }

    [Fact]
    public void Saudi_zakat_is_due_120_days_after_year_end_and_skips_friday_and_saturday()
    {
        var fy2026 = DeadlineRules.Periods("annual", new(2026, 1, 1), new(2026, 1, 1), 1).Single();
        Expect(DeadlineRules.Compute(Rule(0, 31, true, daysAfter: 120), fy2026, null, Saudi, Saudi, 3), (2027, 4, 30), (2027, 5, 2), (2027, 4, 29));
    }

    // ------------------------------------------------------------ who it applies to

    [Fact]
    public void Applicability_checks_tax_id_and_entity_type()
    {
        var gst = Applicability.From("""{"taxId":"GSTIN"}""");
        Assert.True(gst.Matches("Company", new Dictionary<string, string> { ["GSTIN"] = "32AAACA1234F1ZJ" }));
        Assert.False(gst.Matches("Company", new Dictionary<string, string> { ["PAN"] = "AAACA1234F" }));

        var ct = Applicability.From("""{"excludeEntityTypes":["Individual"]}""");
        Assert.True(ct.Matches("LLC", new Dictionary<string, string>()));
        Assert.False(ct.Matches("individual", new Dictionary<string, string>()));

        var itr = Applicability.From("""{"entityTypes":["Individual","HUF"]}""");
        Assert.True(itr.Matches("HUF", new Dictionary<string, string>()));
        Assert.False(itr.Matches("Company", new Dictionary<string, string>()));
    }
}
