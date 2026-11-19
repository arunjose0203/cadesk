using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CaDesk.Application.Notifications;

/// <summary>Pure helpers for reminders; unit-tested.</summary>
public static partial class MessageRules
{
    [GeneratedRegex(@"\{\{\s*([a-z_]+)\s*\}\}")]
    private static partial Regex Placeholder();

    /// <summary>Fills {{name}} placeholders. Unknown names become empty, so a typo never leaks raw braces to a client.</summary>
    public static string Render(string text, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : "");

    /// <summary>Placeholder names used in a text.</summary>
    public static IReadOnlyCollection<string> PlaceholdersIn(string text) =>
        Placeholder().Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();

    /// <summary>Plain text → simple HTML email body (escaped, line breaks kept).</summary>
    public static string ToHtml(string text) =>
        "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.5\">" +
        WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>") + "</div>";

    /// <summary>
    /// Which reminder to send now: of the "days before" offsets, the smallest one whose day has arrived.
    /// Schedule [7,3,0]: 9 days left → none; 5 → 7; 2 → 3; 0 → 0. Missed days are not all sent at once.
    /// </summary>
    public static int? PickOffset(IEnumerable<int> schedule, int daysLeft)
    {
        if (daysLeft < 0) return null;
        var due = schedule.Where(d => d >= daysLeft).ToList();
        return due.Count == 0 ? null : due.Min();
    }

    /// <summary>
    /// The earliest moment at or after <paramref name="nowUtc"/> outside the quiet hours (local time in <paramref name="tz"/>).
    /// Quiet hours may cross midnight (21:00–08:00). Equal start and end = no quiet hours.
    /// </summary>
    public static DateTime NextAllowedUtc(DateTime nowUtc, TimeZoneInfo tz, TimeOnly quietStart, TimeOnly quietEnd)
    {
        if (quietStart == quietEnd) return nowUtc;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);
        var t = TimeOnly.FromDateTime(local);
        bool quiet = quietStart < quietEnd ? t >= quietStart && t < quietEnd : t >= quietStart || t < quietEnd;
        if (!quiet) return nowUtc;

        var endDate = DateOnly.FromDateTime(local);
        if (quietStart > quietEnd && t >= quietStart) endDate = endDate.AddDays(1);   // e.g. 22:00 → tomorrow 08:00
        var endLocal = DateTime.SpecifyKind(endDate.ToDateTime(quietEnd), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
    }

    /// <summary>
    /// A phone number for WhatsApp in international form without "+": "098470 12345" (IN) → "919847012345".
    /// Null when it can't be a valid number.
    /// </summary>
    public static string? ToWhatsAppNumber(string? phone, string? callingCode)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var plus = phone.TrimStart().StartsWith('+') || phone.TrimStart().StartsWith("00");
        var digits = new StringBuilder();
        foreach (var ch in phone) if (char.IsAsciiDigit(ch)) digits.Append(ch);
        var d = digits.ToString();
        if (phone.TrimStart().StartsWith("00")) d = d[2..];
        if (!plus)
        {
            if (string.IsNullOrEmpty(callingCode)) return null;
            d = d.TrimStart('0');
            if (!d.StartsWith(callingCode, StringComparison.Ordinal) || d.Length <= 10) d = callingCode + d;
        }
        return d.Length is >= 8 and <= 15 ? d : null;
    }

    public static bool LooksLikeEmail(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var at = s.IndexOf('@');
        return at > 0 && at == s.LastIndexOf('@') && s.IndexOf('.', at) > at + 1 && !s.EndsWith('.') && !s.Any(char.IsWhiteSpace);
    }

    /// <summary>"in 3 days", "tomorrow", "today".</summary>
    public static string DaysLeftText(int days) => days switch { 0 => "today", 1 => "tomorrow", _ => $"in {days} days" };
}
