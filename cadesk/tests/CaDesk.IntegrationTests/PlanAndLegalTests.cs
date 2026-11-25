using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 71: plan-based desk gating and acceptance of the legal documents.</summary>
public class PlanAndLegalTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_starter_firm_cannot_open_growth_desks()
    {
        var firm = await api.SignUpAsync("Starter", plan: "STARTER");
        var http = api.WithToken(firm.AccessToken);

        var desks = await http.GetFromJsonAsync<JsonElement>("/api/subscriptions/desks");
        Assert.Equal("STARTER", desks.GetProperty("planTier").GetString());
        Assert.Equal("GROWTH", desks.GetProperty("deskPlans").GetProperty("caro").GetString());
        Assert.Equal(100, desks.GetProperty("clientLimit").GetInt32());

        var caro = await http.GetAsync("/api/caro/summary");
        Assert.Equal(HttpStatusCode.Forbidden, caro.StatusCode);
        var body = await caro.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GROWTH", body.GetProperty("requiredPlan").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/transfer-pricing/summary")).StatusCode);
    }

    [Fact]
    public async Task An_internal_firm_opens_every_desk()
    {
        var http = api.WithToken(api.FirmA.AccessToken);
        var desks = await http.GetFromJsonAsync<JsonElement>("/api/subscriptions/desks");
        Assert.Equal("INTERNAL", desks.GetProperty("planTier").GetString());
        Assert.Equal(JsonValueKind.Null, desks.GetProperty("clientLimit").ValueKind);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await http.GetAsync("/api/caro/summary")).StatusCode);
    }

    [Fact]
    public async Task Legal_documents_are_accepted_once_per_version()
    {
        var firm = await api.SignUpAsync("Legal");
        var http = api.WithToken(firm.AccessToken);

        var docs = await api.Raw().GetFromJsonAsync<JsonElement>("/api/legal/documents");
        Assert.Equal(3, docs.GetArrayLength());

        var before = await http.GetFromJsonAsync<JsonElement>("/api/legal/status");
        Assert.Equal(3, before.GetProperty("pending").GetArrayLength());   // the partner also accepts the DPA

        var res = await http.PostAsJsonAsync("/api/legal/accept", new { documents = new[] { "terms", "privacy", "dpa" } });
        res.EnsureSuccessStatusCode();
        var after = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, after.GetProperty("pending").GetArrayLength());

        // again: no duplicate rows, no error
        (await http.PostAsJsonAsync("/api/legal/accept", new { documents = new[] { "terms" } })).EnsureSuccessStatusCode();

        var bad = await http.PostAsJsonAsync("/api/legal/accept", new { documents = new[] { "cookies" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
