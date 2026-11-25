using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaDesk.Api.Auth;
using CaDesk.Api.Endpoints;
using CaDesk.Api.Users;
using CaDesk.Application.Clients;
using CaDesk.Infrastructure.Configuration;
using Npgsql;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 3: per-firm settings, levels, locks, validation, history, secrets.</summary>
public class SettingsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<SettingItem> Item(HttpClient c, string key) =>
        (await c.GetFromJsonAsync<List<SettingItem>>("/api/settings"))!.Single(i => i.Key == key);

    private static Task<HttpResponseMessage> Put(HttpClient c, string key, string json, string? scope = null, Guid? scopeId = null, bool? lockLower = null) =>
        c.PutAsJsonAsync($"/api/settings/{key}", new SaveSettingRequest(J(json), scope, scopeId, lockLower, "test"));

    [Fact]
    public async Task Partner_changes_reminder_days_and_sees_where_values_come_from()
    {
        var u = await api.SignUpAsync("Settings Co");
        var c = api.WithToken(u.AccessToken);

        var before = await Item(c, "reminder.schedule_days");
        Assert.Equal("default", before.Source);
        Assert.Equal("[7,3,0]", before.Value.GetRawText());

        Assert.Equal(HttpStatusCode.NoContent, (await Put(c, "reminder.schedule_days", "[10,5,1]")).StatusCode);

        var after = await Item(c, "reminder.schedule_days");   // takes effect on the very next request
        Assert.Equal("firm", after.Source);
        Assert.Equal("[10,5,1]", after.Value.GetRawText());
        Assert.Equal("[7,3,0]", after.Inherited.GetRawText()); // what Reset would go back to

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync("/api/settings/reminder.schedule_days")).StatusCode);
        Assert.Equal("default", (await Item(c, "reminder.schedule_days")).Source);
    }

    [Fact]
    public async Task Country_values_apply_to_a_new_indian_firm()
    {
        var c = api.WithToken(api.FirmA.AccessToken);
        var tax = await Item(c, "billing.fee_tax_rate");
        Assert.Equal("country", tax.Source);
        Assert.Equal(18m, tax.Value.GetDecimal());
    }

    [Fact]
    public async Task Locked_settings_and_country_only_settings_cannot_be_changed_by_a_firm()
    {
        var c = api.WithToken(api.FirmA.AccessToken);

        var review = await Item(c, "ai.review_required");
        Assert.True(review.IsLocked);
        Assert.False(review.Editable);
        Assert.Equal(HttpStatusCode.Conflict, (await Put(c, "ai.review_required", "false")).StatusCode);

        // data region can only be set at country level
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, "security.data_region", "\"ae\"")).StatusCode);
    }

    [Fact]
    public async Task Invalid_values_are_rejected_with_reasons()
    {
        var c = api.WithToken(api.FirmA.AccessToken);
        var res = await Put(c, "reminder.schedule_days", "[99]");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("at most 60", await res.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, "billing.invoice_number_format", "\"INV-{FY}\"")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(c, "no.such.key", "1")).StatusCode);
    }

    [Fact]
    public async Task Plan_ceiling_caps_the_ai_quota()
    {
        var u = await api.SignUpAsync("Quota Co", plan: "STARTER"); // Starter plan: 200 pages
        var c = api.WithToken(u.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await Put(c, "ai.monthly_page_quota", "5000")).StatusCode);
        var q = await Item(c, "ai.monthly_page_quota");
        Assert.True(q.Capped);
        Assert.Equal(200, q.Value.GetInt32());
    }

    [Fact]
    public async Task Client_override_and_firm_lock()
    {
        var u = await api.SignUpAsync("Client Override Co");
        var c = api.WithToken(u.AccessToken);
        var client = (await (await c.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            "C1", "Big Client", null, "IN", "Company", new() { ["PAN"] = "AAACB1234C" }, null, null, true, null))).Content.ReadFromJsonAsync<ClientDto>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await Put(c, "reminder.channels", "[\"whatsapp\"]", "client", client.Id)).StatusCode);
        var explained = await c.GetFromJsonAsync<SettingResolution>($"/api/settings/reminder.channels/explain?clientId={client.Id}");
        Assert.Equal("client", explained!.Source);
        Assert.Equal("[\"whatsapp\"]", explained.Value.GetRawText());

        // firm locks its value: client overrides now lose, and new ones are refused
        Assert.Equal(HttpStatusCode.NoContent, (await Put(c, "reminder.channels", "[\"email\"]", lockLower: true)).StatusCode);
        var locked = await c.GetFromJsonAsync<SettingResolution>($"/api/settings/reminder.channels/explain?clientId={client.Id}");
        Assert.Equal("firm", locked!.Source);
        Assert.Equal(HttpStatusCode.Conflict, (await Put(c, "reminder.channels", "[\"sms\"]", "client", client.Id)).StatusCode);

        // a client id from another firm is refused
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, "doc.client_upload_enabled", "false", "client", Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Changes_are_logged_and_can_be_undone()
    {
        var u = await api.SignUpAsync("History Co");
        var c = api.WithToken(u.AccessToken);
        await Put(c, "compliance.generate_ahead_days", "60");
        await Put(c, "compliance.generate_ahead_days", "90");

        var changes = await c.GetFromJsonAsync<List<ChangeItem>>("/api/settings/changes?key=compliance.generate_ahead_days");
        Assert.Equal(2, changes!.Count);
        Assert.Equal(90, changes[0].NewValue!.Value.GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/settings/changes/{changes[0].Id}/revert", null)).StatusCode);
        Assert.Equal(60, (await Item(c, "compliance.generate_ahead_days")).Value.GetInt32());
    }

    [Fact]
    public async Task Firms_never_see_each_others_settings()
    {
        var a = api.WithToken(api.FirmA.AccessToken);
        var b = api.WithToken(api.FirmB.AccessToken);
        await Put(a, "doc.max_size_mb", "40");
        Assert.Equal("firm", (await Item(a, "doc.max_size_mb")).Source);
        Assert.NotEqual("firm", (await Item(b, "doc.max_size_mb")).Source);
    }

    [Fact]
    public async Task Staff_cannot_change_firm_settings_but_can_set_their_own_preferences()
    {
        var partner = api.WithToken(api.FirmA.AccessToken);
        var staffRole = (await partner.GetFromJsonAsync<List<RoleItem>>("/api/roles"))!.Single(r => r.Name == "Staff").Id;
        var invite = (await (await partner.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@staff.example", null, staffRole))).Content.ReadFromJsonAsync<InviteCreated>())!;
        var staff = (await (await api.Raw().PostAsJsonAsync("/api/auth/accept-invite",
            new AcceptInviteRequest(invite.InviteToken, "Staff", "Staff-password-77"))).Content.ReadFromJsonAsync<AuthResult>())!;
        var s = api.WithToken(staff.AccessToken!);

        Assert.Equal(HttpStatusCode.Forbidden, (await s.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.PutAsJsonAsync("/api/me/settings/firm.date_format",
            new SaveSettingRequest(J("\"yyyy-MM-dd\""), null, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.PutAsJsonAsync("/api/me/settings/doc.max_size_mb",
            new SaveSettingRequest(J("5"), null, null, null, null))).StatusCode); // not a personal setting
    }

    [Fact]
    public async Task Secrets_are_write_only_and_encrypted_at_rest()
    {
        var u = await api.SignUpAsync("Secret Co");
        var c = api.WithToken(u.AccessToken);

        // a secret_ref setting must point at an existing secret
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, "whatsapp.credentials_ref", "\"wa_token\"")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await c.PutAsJsonAsync("/api/settings/secrets/wa_token", new SaveSecretRequest("super-secret-value-123"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Put(c, "whatsapp.credentials_ref", "\"wa_token\"")).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync("/api/settings/secrets/wa_token")).StatusCode); // still in use

        var list = await c.GetStringAsync("/api/settings/secrets");
        Assert.Contains("wa_token", list);
        Assert.DoesNotContain("super-secret-value-123", list);

        await using var conn = new NpgsqlConnection(ApiFactory.OwnerCs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("select ciphertext from secrets where firm_id = @f and name = 'wa_token'", conn);
        cmd.Parameters.AddWithValue("f", u.FirmId);
        var stored = (string)(await cmd.ExecuteScalarAsync())!;
        Assert.DoesNotContain("super-secret-value-123", stored);
    }

    [Fact]
    public async Task Password_minimum_length_follows_the_firm_setting()
    {
        var u = await api.SignUpAsync("Strict Co");
        var c = api.WithToken(u.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await Put(c, "security.password_min_length", "16")).StatusCode);

        var res = await c.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(ApiFactory.Password, "Twelve-chars"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("16", await res.Content.ReadAsStringAsync());
    }
}
