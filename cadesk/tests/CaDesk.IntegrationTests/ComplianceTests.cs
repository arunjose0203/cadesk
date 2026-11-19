using System.Net;
using System.Net.Http.Json;
using CaDesk.Api.Endpoints;
using CaDesk.Application.Clients;
using CaDesk.Application.Compliance;
using CaDesk.Infrastructure.Configuration;
using System.Text.Json;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 6: deadlines generated from country rules, client choices, filing, holidays, isolation.</summary>
public class ComplianceTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> GstCompany = new() { ["PAN"] = "AAACA1234F", ["GSTIN"] = "32AAACA1234F1ZJ" };

    private async Task<HttpClient> NewFirm() => api.WithToken((await api.SignUpAsync("Deadline Co")).AccessToken);

    private static async Task<ClientDto> NewClient(HttpClient c, string type, Dictionary<string, string> taxIds)
    {
        var res = await c.PostAsJsonAsync("/api/clients",
            new SaveClientRequest(null, $"{type} client", null, null, type, taxIds, null, null, null, null));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    private static async Task<List<DeadlineDto>> Deadlines(HttpClient c, Guid clientId, string query = "") =>
        (await c.GetFromJsonAsync<DeadlinePage>($"/api/deadlines?clientId={clientId}&take=500{query}"))!.Items.ToList();

    [Fact]
    public async Task A_company_with_gstin_gets_a_year_of_gst_and_income_tax_deadlines()
    {
        var c = await NewFirm();
        var client = await NewClient(c, "Company", GstCompany);
        var d = await Deadlines(c, client.Id);

        Assert.InRange(d.Count(x => x.TypeCode == "GSTR3B_M"), 12, 14);
        Assert.InRange(d.Count(x => x.TypeCode == "GSTR1_M"), 12, 14);
        Assert.Contains(d, x => x.TypeCode == "ADV_TAX");
        Assert.Contains(d, x => x.TypeCode == "ITR_AUDIT");
        Assert.DoesNotContain(d, x => x.TypeCode is "TDS_PAY" or "ITR" or "GSTR3B_Q");   // no TAN; not an individual; QRMP is opt-in

        var oct = d.Where(x => x.TypeCode == "GSTR3B_M").OrderBy(x => x.DueDate).Last();
        Assert.Equal(20, oct.StatutoryDue.Day);
        Assert.True(oct.InternalDue < oct.DueDate);
        Assert.All(d, x => Assert.True(x.DueDate >= x.StatutoryDue));
    }

    [Fact]
    public async Task An_individual_gets_the_itr_and_nothing_for_gst()
    {
        var c = await NewFirm();
        var client = await NewClient(c, "Individual", new() { ["PAN"] = "BXMPM4521K" });
        var d = await Deadlines(c, client.Id);
        Assert.Contains(d, x => x.TypeCode == "ITR");
        Assert.DoesNotContain(d, x => x.TypeCode.StartsWith("GSTR"));
    }

    [Fact]
    public async Task Removing_the_gstin_removes_future_gst_deadlines_but_keeps_filed_ones()
    {
        var c = await NewFirm();
        // filing straight on the deadline is only allowed when the firm doesn't require review (Sprint 7)
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync("/api/settings/task.review_required",
            new SaveSettingRequest(JsonDocument.Parse("false").RootElement.Clone(), null, null, null, "test"))).StatusCode);
        var client = await NewClient(c, "Company", GstCompany);
        var first = (await Deadlines(c, client.Id)).Where(x => x.TypeCode == "GSTR3B_M").OrderBy(x => x.DueDate).First();

        var filed = await c.PutAsJsonAsync($"/api/deadlines/{first.Id}",
            new UpdateDeadlineRequest("filed", null, "AA3210261234567", "Filed via portal", null, first.Version));
        Assert.Equal(HttpStatusCode.OK, filed.StatusCode);

        var upd = await c.PutAsJsonAsync($"/api/clients/{client.Id}",
            new SaveClientRequest(client.Code, client.Name, null, null, "Company", new() { ["PAN"] = "AAACA1234F" }, null, null, true, null));
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);

        var after = (await Deadlines(c, client.Id)).Where(x => x.TypeCode.StartsWith("GSTR")).ToList();
        var kept = Assert.Single(after, x => x.Id == first.Id);
        Assert.Equal("filed", kept.Status);
        Assert.Equal("AA3210261234567", kept.AckNo);
        Assert.DoesNotContain(after, x => x.DueDate > first.DueDate && x.Status == "pending" && x.TypeCode == "GSTR3B_M"
                                          && x.DueDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task Switching_to_qrmp_replaces_monthly_returns_with_quarterly_ones()
    {
        var c = await NewFirm();
        var client = await NewClient(c, "Company", GstCompany);
        var url = $"/api/clients/{client.Id}/compliances";

        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"{url}/GSTR3B_M",
            new DeadlineService.SaveClientComplianceRequest(false, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"{url}/GSTR3B_Q",
            new DeadlineService.SaveClientComplianceRequest(true, null, null, null))).StatusCode);

        var items = (await c.GetFromJsonAsync<List<DeadlineService.ClientComplianceItem>>(url))!;
        Assert.False(items.Single(i => i.Code == "GSTR3B_M").Enabled);
        Assert.True(items.Single(i => i.Code == "GSTR3B_Q").Chosen);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var d = await Deadlines(c, client.Id);
        Assert.DoesNotContain(d, x => x.TypeCode == "GSTR3B_M" && x.DueDate > today.AddDays(1));
        Assert.InRange(d.Count(x => x.TypeCode == "GSTR3B_Q"), 4, 5);
        Assert.All(d.Where(x => x.TypeCode == "GSTR3B_Q"), x => Assert.Equal(22, x.StatutoryDue.Day));

        // a frequency the rule doesn't allow is refused
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"{url}/GSTR1_M",
            new DeadlineService.SaveClientComplianceRequest(true, "annual", null, null))).StatusCode);
    }

    [Fact]
    public async Task A_firm_holiday_moves_internal_targets_earlier()
    {
        var c = await NewFirm();
        var client = await NewClient(c, "Company", GstCompany);
        var target = (await Deadlines(c, client.Id)).Where(x => x.Status == "pending").OrderBy(x => x.DueDate).Last();

        var res = await c.PostAsJsonAsync("/api/holidays", new AddHolidayRequest(target.InternalDue, "Office closed"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var moved = (await Deadlines(c, client.Id)).Single(x => x.Id == target.Id);
        Assert.True(moved.InternalDue < target.InternalDue);
        Assert.Equal(target.DueDate, moved.DueDate);   // the government due date doesn't change for a firm holiday
    }

    [Fact]
    public async Task Overdue_and_status_filters_and_bad_filing_dates()
    {
        var c = await NewFirm();
        var client = await NewClient(c, "Company", GstCompany);
        var d = (await Deadlines(c, client.Id)).OrderBy(x => x.DueDate).Last();

        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/deadlines/{d.Id}",
            new UpdateDeadlineRequest("filed", future, null, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/deadlines/{d.Id}",
            new UpdateDeadlineRequest("done", null, null, null, null, null))).StatusCode);

        var page = (await c.GetFromJsonAsync<DeadlinePage>("/api/deadlines?status=open"))!;
        Assert.Equal(page.Summary.Open, page.Total);
        Assert.All(page.Items, x => Assert.Contains(x.Status, new[] { "pending", "in_progress" }));
    }

    [Fact]
    public async Task Another_firm_sees_none_of_it()
    {
        var a = await NewFirm();
        var b = await NewFirm();
        var client = await NewClient(a, "Company", GstCompany);
        var one = (await Deadlines(a, client.Id)).First();

        Assert.Empty((await b.GetFromJsonAsync<DeadlinePage>("/api/deadlines"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/clients/{client.Id}/compliances")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/deadlines/{one.Id}",
            new UpdateDeadlineRequest("filed", null, null, null, null, null))).StatusCode);
    }
}
