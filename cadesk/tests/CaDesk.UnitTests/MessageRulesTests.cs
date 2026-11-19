using CaDesk.Application.Notifications;

namespace CaDesk.UnitTests;

public class MessageRulesTests
{
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    [Fact]
    public void Render_fills_known_placeholders_and_blanks_unknown_ones() =>
        Assert.Equal("GSTR-3B due 20 Nov. ",
            MessageRules.Render("{{compliance}} due {{ due_date }}. {{nope}}", new Dictionary<string, string> { ["compliance"] = "GSTR-3B", ["due_date"] = "20 Nov" }));

    [Fact]
    public void Placeholders_are_listed_once() =>
        Assert.Equal(new[] { "a", "b" }, MessageRules.PlaceholdersIn("{{a}} {{b}} {{a}}").ToArray());

    [Fact]
    public void Html_body_is_escaped() =>
        Assert.Contains("&lt;script&gt;", MessageRules.ToHtml("<script>\nx"));

    [Theory]
    [InlineData(9, null)]
    [InlineData(7, 7)]
    [InlineData(5, 7)]
    [InlineData(3, 3)]
    [InlineData(2, 3)]
    [InlineData(0, 0)]
    [InlineData(-1, null)]
    public void Only_the_latest_reached_reminder_is_picked(int daysLeft, int? expected) =>
        Assert.Equal(expected, MessageRules.PickOffset([7, 3, 0], daysLeft));

    [Theory]
    [InlineData("2026-11-19T10:00:00Z", "2026-11-19T10:00:00Z")]   // 15:30 IST: allowed
    [InlineData("2026-11-19T16:00:00Z", "2026-11-20T02:30:00Z")]   // 21:30 IST: quiet → 08:00 IST next day
    [InlineData("2026-11-19T20:00:00Z", "2026-11-20T02:30:00Z")]   // 01:30 IST: quiet → 08:00 IST same day
    [InlineData("2026-11-20T02:30:00Z", "2026-11-20T02:30:00Z")]   // exactly 08:00 IST: allowed
    public void Quiet_hours_across_midnight(string now, string expected) =>
        Assert.Equal(DateTime.Parse(expected).ToUniversalTime(),
            MessageRules.NextAllowedUtc(DateTime.Parse(now).ToUniversalTime(), Ist, new TimeOnly(21, 0), new TimeOnly(8, 0)));

    [Fact]
    public void Quiet_hours_within_a_day_and_none() 
    {
        var noonIst = DateTime.Parse("2026-11-19T06:30:00Z").ToUniversalTime();
        Assert.Equal(DateTime.Parse("2026-11-19T08:30:00Z").ToUniversalTime(),
            MessageRules.NextAllowedUtc(noonIst, Ist, new TimeOnly(12, 0), new TimeOnly(14, 0)));      // lunch hours
        Assert.Equal(noonIst, MessageRules.NextAllowedUtc(noonIst, Ist, new TimeOnly(0, 0), new TimeOnly(0, 0)));
    }

    [Theory]
    [InlineData("098470 12345", "91", "919847012345")]
    [InlineData("9847012345", "91", "919847012345")]
    [InlineData("919847012345", "91", "919847012345")]
    [InlineData("+91 98470-12345", "91", "919847012345")]
    [InlineData("050 123 4567", "971", "971501234567")]
    [InlineData("00971501234567", "971", "971501234567")]
    [InlineData("12", "91", null)]
    [InlineData("", "91", null)]
    public void WhatsApp_numbers(string phone, string cc, string? expected) =>
        Assert.Equal(expected, MessageRules.ToWhatsAppNumber(phone, cc));
}
