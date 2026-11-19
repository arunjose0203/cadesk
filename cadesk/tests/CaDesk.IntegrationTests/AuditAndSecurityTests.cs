using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaDesk.Api.Audit;
using CaDesk.Api.Auth;
using CaDesk.Api.Users;
using CaDesk.Application.Clients;
using CaDesk.Infrastructure.Configuration;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 4: audit log, two-step sign-in required by role, viewer permissions.</summary>
public class AuditAndSecurityTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static SaveClientRequest NewClient(string name) =>
        new($"C-{Guid.NewGuid():N}"[..12], name, null, "IN", "Company", new() { ["PAN"] = "AAACB1234C" }, null, null, true, null);

    private static async Task<List<AuditItem>> Audit(HttpClient c, string query) =>
        (await c.GetFromJsonAsync<AuditPage>($"/api/audit-logs?{query}"))!.Items.ToList();

    [Fact]
    public async Task Creating_changing_and_deleting_a_client_is_recorded_with_who_and_what()
    {
        var u = await api.SignUpAsync("Audit Co");
        var c = api.WithToken(u.AccessToken);

        var client = (await (await c.PostAsJsonAsync("/api/clients", NewClient("Old Name"))).Content.ReadFromJsonAsync<ClientDto>())!;
        await c.PutAsJsonAsync($"/api/clients/{client.Id}", NewClient("New Name") with { Code = client.Code });
        await c.DeleteAsync($"/api/clients/{client.Id}");

        var rows = await Audit(c, $"entity=clients&entityId={client.Id}");
        Assert.Equal(new[] { "delete", "update", "create" }, rows.Select(r => r.Action));

        var create = rows[2];
        Assert.Equal(u.UserId, create.ActorId);
        Assert.Equal("Test Partner", create.ActorName);
        Assert.Equal("Old Name", create.NewValues!.Value.GetProperty("Name").GetString());

        var update = rows[1];
        Assert.Equal("Old Name", update.OldValues!.Value.GetProperty("Name").GetString());
        Assert.Equal("New Name", update.NewValues!.Value.GetProperty("Name").GetString());
        Assert.False(update.NewValues.Value.TryGetProperty("Code", out _)); // unchanged fields aren't repeated
    }

    [Fact]
    public async Task Passwords_and_secrets_never_appear_in_the_audit_log()
    {
        var u = await api.SignUpAsync("Masking Co");
        var partner = api.WithToken(u.AccessToken);
        var staffRole = (await partner.GetFromJsonAsync<List<RoleItem>>("/api/roles"))!.Single(r => r.Name == "Staff").Id;
        var invite = (await (await partner.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@staff.example", null, staffRole))).Content.ReadFromJsonAsync<InviteCreated>())!;
        await api.Raw().PostAsJsonAsync("/api/auth/accept-invite", new AcceptInviteRequest(invite.InviteToken, "Staff", "Staff-password-77"));
        await partner.PutAsJsonAsync("/api/settings/secrets/api_key", new { value = "very-secret-value-42" });

        var all = await partner.GetStringAsync("/api/audit-logs?take=200");
        Assert.DoesNotContain("very-secret-value-42", all);
        Assert.DoesNotContain(invite.InviteToken, all);

        var userCreate = (await Audit(partner, "entity=users&action=create")).Single();
        Assert.Equal("***", userCreate.NewValues!.Value.GetProperty("PasswordHash").GetString());
    }

    [Fact]
    public async Task Settings_changes_and_invitation_revokes_are_audited()
    {
        var u = await api.SignUpAsync("Audit Settings Co");
        var c = api.WithToken(u.AccessToken);
        await c.PutAsJsonAsync("/api/settings/doc.max_size_mb", new SaveSettingRequest(J("25"), null, null, null, "bigger scans"));
        Assert.Single(await Audit(c, "entity=config_values"));

        var role = (await c.GetFromJsonAsync<List<RoleItem>>("/api/roles"))!.Single(r => r.Name == "Article").Id;
        var invite = (await (await c.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@x.example", null, role))).Content.ReadFromJsonAsync<InviteCreated>())!;
        await c.DeleteAsync($"/api/invitations/{invite.Id}");
        Assert.Contains(await Audit(c, "entity=invitations"), r => r.Action == "revoke");
    }

    [Fact]
    public async Task Audit_log_is_private_to_the_firm_and_needs_audit_permission()
    {
        var a = await api.SignUpAsync("Private A");
        var b = await api.SignUpAsync("Private B");
        await api.WithToken(a.AccessToken).PostAsJsonAsync("/api/clients", NewClient("Secret client"));

        Assert.Empty(await Audit(api.WithToken(b.AccessToken), "entity=clients"));

        var partner = api.WithToken(a.AccessToken);
        var staffRole = (await partner.GetFromJsonAsync<List<RoleItem>>("/api/roles"))!.Single(r => r.Name == "Staff").Id;
        var invite = (await (await partner.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@staff.example", null, staffRole))).Content.ReadFromJsonAsync<InviteCreated>())!;
        var staff = (await (await api.Raw().PostAsJsonAsync("/api/auth/accept-invite",
            new AcceptInviteRequest(invite.InviteToken, "Staff", "Staff-password-77"))).Content.ReadFromJsonAsync<AuthResult>())!;
        Assert.Equal(HttpStatusCode.Forbidden, (await api.WithToken(staff.AccessToken!).GetAsync("/api/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task Audit_log_pages_newest_first()
    {
        var u = await api.SignUpAsync("Paging Co");
        var c = api.WithToken(u.AccessToken);
        for (var i = 0; i < 3; i++) await c.PostAsJsonAsync("/api/clients", NewClient($"Client {i}"));

        var first = (await c.GetFromJsonAsync<AuditPage>("/api/audit-logs?entity=clients&take=2"))!;
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextBefore);
        var next = (await c.GetFromJsonAsync<AuditPage>(
            $"/api/audit-logs?entity=clients&take=2&before={first.NextBefore!.Value:O}"))!;
        Assert.Single(next.Items);
        Assert.True(first.Items[0].At >= first.Items[1].At);
    }

    [Fact]
    public async Task Required_two_step_sign_in_blocks_everything_until_it_is_set_up()
    {
        var u = await api.SignUpAsync("MFA Policy Co");
        var c = api.WithToken(u.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync("/api/settings/security.mfa_required_roles",
            new SaveSettingRequest(J("[\"Partner\"]"), null, null, null, null))).StatusCode);

        // next sign-in gets a restricted token
        var loginRes = await api.Raw().PostAsJsonAsync("/api/auth/login", new LoginRequest(u.Email, ApiFactory.Password, null));
        var login = (await loginRes.Content.ReadFromJsonAsync<AuthResult>())!;
        Assert.True(login.User!.MfaSetupRequired);
        var restricted = api.WithToken(login.AccessToken!);
        var blocked = await restricted.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("mfa_setup_required", await blocked.Content.ReadAsStringAsync());

        // setting up two-step sign-in is allowed with the restricted token
        var setup = (await (await restricted.PostAsync("/api/auth/mfa/setup", null)).Content.ReadFromJsonAsync<MfaSetupResult>())!;
        var code = Totp.Code(Base32.Decode(setup.Secret), Totp.CurrentStep(api.Clock.GetUtcNow()));
        Assert.Equal(HttpStatusCode.OK, (await restricted.PostAsJsonAsync("/api/auth/mfa/enable", new MfaCodeRequest(code))).StatusCode);

        // refreshing the same session now gives a normal token
        var refreshed = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", TestData.RefreshCookie(loginRes)!));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var full = (await refreshed.Content.ReadFromJsonAsync<AuthResult>())!;
        Assert.False(full.User!.MfaSetupRequired);
        Assert.Equal(HttpStatusCode.OK, (await api.WithToken(full.AccessToken!).GetAsync("/api/clients")).StatusCode);
    }
}
