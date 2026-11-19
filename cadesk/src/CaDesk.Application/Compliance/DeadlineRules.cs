using System.Globalization;
using System.Text.Json;
using CaDesk.Application.Clients;
using CaDesk.Domain.Compliance;

namespace CaDesk.Application.Compliance;

/// <summary>One filing period: M2026-10 (month), Q2026-12 (quarter ending Dec 2026), FY2026-27 (financial year).</summary>
public sealed record Period(string Key, DateOnly Start, DateOnly End);

/// <summary>A due-date rule read from compliance_types.</summary>
public sealed record DueRule(int MonthsAfter, int Day, int? DaysAfter, IReadOnlyDictionary<int, (int MonthsAfter, int Day)> Overrides,
    bool ShiftOnHoliday)
{
    public static DueRule From(ComplianceType t)
    {
        var overrides = new Dictionary<int, (int, int)>();
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(t.PeriodOverrides) ? "{}" : t.PeriodOverrides);
        foreach (var p in doc.RootElement.EnumerateObject())
            if (int.TryParse(p.Name, out var month))
                overrides[month] = (p.Value.GetProperty("monthsAfter").GetInt32(), p.Value.GetProperty("day").GetInt32());
        return new DueRule(t.DueMonthsAfter, t.DueDay, t.DueDaysAfter, overrides, t.ShiftOnHoliday);
    }
}

/// <summary>Who a compliance applies to, from compliance_types.applies_when.</summary>
public sealed record Applicability(string? TaxId, IReadOnlyList<string> EntityTypes, IReadOnlyList<string> ExcludeEntityTypes)
{
    public static Applicability From(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var r = doc.RootElement;
        static List<string> List(JsonElement r, string name) =>
            r.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
        return new Applicability(r.TryGetProperty("taxId", out var t) ? t.GetString() : null,
            List(r, "entityTypes"), List(r, "excludeEntityTypes"));
    }

    public bool Matches(string entityType, IReadOnlyDictionary<string, string> taxIds) =>
        (TaxId is null || (taxIds.TryGetValue(TaxId, out var v) && !string.IsNullOrWhiteSpace(v))) &&
        (EntityTypes.Count == 0 || EntityTypes.Contains(entityType, StringComparer.OrdinalIgnoreCase)) &&
        !ExcludeEntityTypes.Contains(entityType, StringComparer.OrdinalIgnoreCase);

    public string Describe()
    {
        var parts = new List<string>();
        if (TaxId is not null) parts.Add($"has {TaxId}");
        if (EntityTypes.Count > 0) parts.Add(string.Join(" / ", EntityTypes));
        if (ExcludeEntityTypes.Count > 0) parts.Add("not " + string.Join(" / ", ExcludeEntityTypes));
        return parts.Count == 0 ? "everyone" : string.Join(", ", parts);
    }
}

/// <summary>Days the office or the government is closed.</summary>
public sealed class Calendar(IEnumerable<int> openWeekdays, IEnumerable<DateOnly> holidays)
{
    private readonly HashSet<int> _open = openWeekdays.ToHashSet();
    private readonly HashSet<DateOnly> _holidays = holidays.ToHashSet();

    /// <summary>Country calendar: everything except the weekend days and public holidays is open.</summary>
    public static Calendar FromWeekend(IEnumerable<int> weekendDays, IEnumerable<DateOnly> holidays) =>
        new(Enumerable.Range(0, 7).Except(weekendDays), holidays);

    public bool IsOpen(DateOnly d) => _open.Contains((int)d.DayOfWeek) && !_holidays.Contains(d);

    public DateOnly NextOpen(DateOnly d)
    {
        for (var i = 0; i < 31 && !IsOpen(d); i++) d = d.AddDays(1);
        return d;
    }

    public DateOnly PreviousOpen(DateOnly d)
    {
        for (var i = 0; i < 31 && !IsOpen(d); i++) d = d.AddDays(-1);
        return d;
    }
}

public sealed record DueDates(DateOnly Statutory, DateOnly Due, DateOnly Internal);

/// <summary>
/// Pure date rules for deadlines; unit-tested. Periods → statutory due date (rule or government extension)
/// → due date moved off weekends/public holidays when the rule says so → the firm's internal target on a working day.
/// </summary>
public static class DeadlineRules
{
    /// <summary>Periods of this frequency that overlap [from, to].</summary>
    public static IEnumerable<Period> Periods(string frequency, DateOnly from, DateOnly to, int fyStartMonth, int quarterEndMonth = 3)
    {
        switch (frequency)
        {
            case Frequency.Monthly:
                for (var m = FirstOfMonth(from); m <= to; m = m.AddMonths(1))
                    yield return new Period($"M{m:yyyy-MM}", m, m.AddMonths(1).AddDays(-1));
                break;

            case Frequency.Quarterly:
                // quarters end in months where month % 3 == quarterEndMonth % 3
                for (var m = FirstOfMonth(from); m <= to.AddMonths(2); m = m.AddMonths(1))
                {
                    if (m.Month % 3 != quarterEndMonth % 3) continue;
                    var start = m.AddMonths(-2);
                    if (start > to) break;
                    yield return new Period($"Q{m:yyyy-MM}", start, m.AddMonths(1).AddDays(-1));
                }
                break;

            case Frequency.Annual:
                var y = from.Month >= fyStartMonth ? from.Year : from.Year - 1;
                for (var s = new DateOnly(y, fyStartMonth, 1); s <= to; s = s.AddYears(1))
                    yield return new Period($"FY{NumberFormat.FinancialYear(s, fyStartMonth)}", s, s.AddYears(1).AddDays(-1));
                break;

            default:
                throw new ArgumentException($"Unknown frequency '{frequency}'.", nameof(frequency));
        }
    }

    public static DateOnly StatutoryDue(DueRule rule, Period p)
    {
        if (rule.DaysAfter is { } days) return p.End.AddDays(days);
        var (monthsAfter, day) = rule.Overrides.TryGetValue(p.End.Month, out var o) ? o : (rule.MonthsAfter, rule.Day);
        var month = FirstOfMonth(p.End).AddMonths(monthsAfter);
        return new DateOnly(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
    }

    /// <param name="extension">A government extension for this period, if any; it replaces the rule's date.</param>
    /// <param name="country">Government calendar (weekend + public holidays).</param>
    /// <param name="office">The firm's calendar (working days + public and firm holidays).</param>
    /// <param name="internalOffsetDays">Finish this many days before the due date.</param>
    public static DueDates Compute(DueRule rule, Period p, DateOnly? extension, Calendar country, Calendar office, int internalOffsetDays)
    {
        var statutory = extension ?? StatutoryDue(rule, p);
        var due = rule.ShiftOnHoliday ? country.NextOpen(statutory) : statutory;
        var target = office.PreviousOpen(due.AddDays(-Math.Max(0, internalOffsetDays)));
        // never before the period ends (e.g. advance tax due inside the quarter)
        if (target < p.End && due >= p.End) target = due;
        return new DueDates(statutory, due, target);
    }

    /// <summary>"Oct 2026", "Oct–Dec 2026", "Nov 2026–Jan 2027", "FY 2026-27".</summary>
    public static string Label(string periodKey)
    {
        var c = CultureInfo.InvariantCulture;
        if (periodKey.StartsWith("FY", StringComparison.Ordinal)) return "FY " + periodKey[2..];
        if (periodKey.Length == 8 && DateOnly.TryParseExact(periodKey[1..] + "-01", "yyyy-MM-dd", c, DateTimeStyles.None, out var m))
        {
            if (periodKey[0] == 'M') return m.ToString("MMM yyyy", c);
            var start = m.AddMonths(-2);
            return start.Year == m.Year
                ? $"{start.ToString("MMM", c)}–{m.ToString("MMM yyyy", c)}"
                : $"{start.ToString("MMM yyyy", c)}–{m.ToString("MMM yyyy", c)}";
        }
        return periodKey;
    }

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);
}
