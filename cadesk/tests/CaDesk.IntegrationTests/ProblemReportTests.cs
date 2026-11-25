using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 72: crash reports and "Report a problem".</summary>
public class ProblemReportTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Repeated_crashes_are_counted_and_feedback_is_listed_for_the_firm()
    {
        var http = api.WithToken(api.FirmA.AccessToken);
        var crash = new { kind = "crash", message = "TypeError: x is undefined", page = "gstr1-export", detail = "at foo", appVersion = "abc123" };

        var first = await (await http.PostAsJsonAsync("/api/problem-reports", crash)).Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await http.PostAsJsonAsync("/api/problem-reports", crash)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.StartsWith("PR-", first.GetProperty("reference").GetString());

        var fb = await http.PostAsJsonAsync("/api/problem-reports", new { kind = "feedback", message = "The GSTR-1 file was rejected by the offline tool", page = "gstr1-export" });
        fb.EnsureSuccessStatusCode();

        var list = await http.GetFromJsonAsync<JsonElement>("/api/problem-reports");
        var rows = list.EnumerateArray().ToList();
        Assert.Contains(rows, r => r.GetProperty("kind").GetString() == "crash" && r.GetProperty("occurrences").GetInt32() == 2);
        Assert.Contains(rows, r => r.GetProperty("kind").GetString() == "feedback");

        // another firm sees none of it
        var other = await api.WithToken(api.FirmB.AccessToken).GetFromJsonAsync<JsonElement>("/api/problem-reports");
        Assert.DoesNotContain(other.EnumerateArray(), r => r.GetProperty("message").GetString() == "TypeError: x is undefined");
    }

    [Fact]
    public async Task Bad_reports_are_refused()
    {
        var http = api.WithToken(api.FirmA.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/problem-reports", new { kind = "spam", message = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/problem-reports", new { kind = "feedback", message = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Raw().PostAsJsonAsync("/api/problem-reports", new { kind = "feedback", message = "hi" })).StatusCode);
    }
}
