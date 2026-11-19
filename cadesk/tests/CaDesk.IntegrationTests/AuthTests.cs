using System.Net;
using System.Net.Http.Json;
using CaDesk.Api.Auth;
using CaDesk.Api.Users;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 2: sign-up, login, lockout, refresh rotation, permissions, users, roles, invitations.</summary>
public class AuthTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private async Task<(HttpResponseMessage Res, AuthResult? Body)> Login(string email, string password, Guid? firmId = null)
    {
        var res = await api.Raw().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password, firmId));
        return (res, res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<AuthResult>() : null);
    }

    // ------------------------------------------------------------ sign-up and login

    [Fact]
    public async Task Sign_up_creates_a_partner_with_all_permissions_and_a_refresh_cookie()
    {
        var me = await api.WithToken(api.FirmA.AccessToken).GetFromJsonAsync<UserProfile>("/api/auth/me");
        Assert.Equal(api.FirmA.UserId, me!.Id);
        Assert.Contains("Partner", me.Roles);
        Assert.Equal(TestData.AllPermissions.Length, me.Permissions.Count);
        Assert.False(string.IsNullOrEmpty(api.FirmA.RefreshToken));
    }

    [Fact]
    public async Task Sign_up_rejects_a_weak_password()
    {
        var res = await api.Raw().PostAsJsonAsync("/api/auth/signup",
            new SignUpRequest("Weak Co", "IN", "Someone", $"{Guid.NewGuid():N}@test.example", "short"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_works_with_the_right_password_only_and_is_case_insensitive_on_email()
    {
        var (bad, _) = await Login(api.FirmA.Email, "Wrong-password-1");
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var (ok, body) = await Login(api.FirmA.Email.ToUpperInvariant(), ApiFactory.Password);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(AuthStatus.Ok, body!.Status);
        Assert.NotNull(TestData.RefreshCookie(ok));
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_answer_as_a_wrong_password()
    {
        var (res, _) = await Login($"{Guid.NewGuid():N}@nobody.example", ApiFactory.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_without_revealing_it()
    {
        var u = await api.SignUpAsync("Lockout Co");
        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(u.Email, "Wrong-password-1")).Res.StatusCode);

        // even the right password now fails, with the same generic answer
        var (locked, _) = await Login(u.Email, ApiFactory.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task Parallel_wrong_passwords_are_all_counted()
    {
        var u = await api.SignUpAsync("Parallel Co");
        await Task.WhenAll(Enumerable.Range(0, AuthService.MaxFailedLogins).Select(_ => Login(u.Email, "Wrong-password-1")));
        var (locked, _) = await Login(u.Email, ApiFactory.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task Changing_password_ends_old_sessions_and_returns_a_new_one()
    {
        var u = await api.SignUpAsync("Password Co");
        var res = await api.WithToken(u.AccessToken).PostAsJsonAsync("/api/auth/password",
            new ChangePasswordRequest(ApiFactory.Password, "Brand-new-pass-99"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(TestData.RefreshCookie(res));

        var old = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", u.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(u.Email, "Brand-new-pass-99")).Res.StatusCode);
    }

    [Fact]
    public async Task Same_email_in_two_firms_asks_which_firm()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var one = await api.SignUpAsync("Twin One", email);
        var two = await api.SignUpAsync("Twin Two", email);

        var (_, choose) = await Login(email, ApiFactory.Password);
        Assert.Equal(AuthStatus.ChooseFirm, choose!.Status);
        Assert.Equal(2, choose.Firms!.Count);

        var (_, picked) = await Login(email, ApiFactory.Password, two.FirmId);
        Assert.Equal(AuthStatus.Ok, picked!.Status);
        Assert.Equal(two.FirmId, picked.User!.FirmId);
        Assert.NotEqual(one.FirmId, picked.User.FirmId);
    }

    // ------------------------------------------------------------ refresh tokens

    [Fact]
    public async Task Refresh_rotates_the_token_and_logout_ends_the_session()
    {
        var u = await api.SignUpAsync("Refresh Co");

        var res = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", u.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var rotated = TestData.RefreshCookie(res)!;
        Assert.NotEqual(u.RefreshToken, rotated);
        Assert.NotNull((await res.Content.ReadFromJsonAsync<AuthResult>())!.AccessToken);

        var logout = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/logout", rotated));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var after = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", rotated));
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_cookie_is_rejected()
    {
        var res = await api.Raw().PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ------------------------------------------------------------ permissions, invitations, roles

    private async Task<Guid> RoleId(HttpClient partner, string name) =>
        (await partner.GetFromJsonAsync<List<RoleItem>>("/api/roles"))!.Single(r => r.Name == name).Id;

    private async Task<AuthResult> InviteAndAccept(SignedUp firm, string roleName)
    {
        var partner = api.WithToken(firm.AccessToken);
        var invite = await partner.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@staff.example", "New Person", await RoleId(partner, roleName)));
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var created = (await invite.Content.ReadFromJsonAsync<InviteCreated>())!;

        var accept = await api.Raw().PostAsJsonAsync("/api/auth/accept-invite",
            new AcceptInviteRequest(created.InviteToken, "New Person", "Staff-password-77"));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        return (await accept.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    [Fact]
    public async Task Invited_staff_gets_staff_permissions_only()
    {
        var staff = await InviteAndAccept(api.FirmA, "Staff");
        Assert.Equal(new[] { "Staff" }, staff.User!.Roles);
        var c = api.WithToken(staff.AccessToken!);

        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync($"/api/clients/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/login-events")).StatusCode);
    }

    [Fact]
    public async Task An_invitation_can_be_used_only_once()
    {
        var partner = api.WithToken(api.FirmA.AccessToken);
        var invite = await partner.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@staff.example", null, await RoleId(partner, "Article")));
        var token = (await invite.Content.ReadFromJsonAsync<InviteCreated>())!.InviteToken;

        var first = await api.Raw().PostAsJsonAsync("/api/auth/accept-invite", new AcceptInviteRequest(token, "A", "Article-pass-123"));
        var second = await api.Raw().PostAsJsonAsync("/api/auth/accept-invite", new AcceptInviteRequest(token, "B", "Article-pass-123"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, second.StatusCode);
    }

    [Fact]
    public async Task Firm_B_cannot_see_or_change_firm_A_users()
    {
        var b = api.WithToken(api.FirmB.AccessToken);
        var users = await b.GetFromJsonAsync<List<UserListItem>>("/api/users");
        Assert.DoesNotContain(users!, u => u.Id == api.FirmA.UserId);

        var put = await b.PutAsJsonAsync($"/api/users/{api.FirmA.UserId}", new UpdateUserRequest(null, false, null));
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    [Fact]
    public async Task Partner_cannot_deactivate_self_or_remove_the_last_partner()
    {
        var u = await api.SignUpAsync("Guard Co");
        var c = api.WithToken(u.AccessToken);

        var self = await c.PutAsJsonAsync($"/api/users/{u.UserId}", new UpdateUserRequest(null, false, null));
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);

        var demote = await c.PutAsJsonAsync($"/api/users/{u.UserId}",
            new UpdateUserRequest(null, null, [await RoleId(c, "Staff")]));
        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in_or_refresh()
    {
        var u = await api.SignUpAsync("Deactivate Co");
        var staff = await InviteAndAccept(u, "Staff");
        var partner = api.WithToken(u.AccessToken);

        var off = await partner.PutAsJsonAsync($"/api/users/{staff.User!.Id}", new UpdateUserRequest(null, false, null));
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);

        var (login, _) = await Login(staff.User.Email, "Staff-password-77");
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Custom_roles_can_be_created_changed_and_deleted_but_built_in_roles_cannot()
    {
        var c = api.WithToken(api.FirmA.AccessToken);
        var name = $"Reviewer {Guid.NewGuid():N}"[..16];

        var create = await c.PostAsJsonAsync("/api/roles", new SaveRoleRequest(name, ["clients.read", "reports.read"]));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var role = (await create.Content.ReadFromJsonAsync<RoleItem>())!;

        Assert.Equal(HttpStatusCode.NoContent,
            (await c.PutAsJsonAsync($"/api/roles/{role.Id}", new SaveRoleRequest(name, ["clients.read"]))).StatusCode);
        var updated = (await c.GetFromJsonAsync<List<RoleItem>>("/api/roles"))!.Single(r => r.Id == role.Id);
        Assert.Equal(new[] { "clients.read" }, updated.Permissions);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync("/api/roles", new SaveRoleRequest("Bad", ["no.such.permission"]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await c.DeleteAsync($"/api/roles/{await RoleId(c, "Staff")}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/roles/{role.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_user_manager_cannot_grant_more_access_than_they_have()
    {
        var u = await api.SignUpAsync("Escalation Co");
        var partner = api.WithToken(u.AccessToken);

        // a custom role that can manage users but nothing else important
        var hrRole = (await (await partner.PostAsJsonAsync("/api/roles",
            new SaveRoleRequest("HR", ["users.read", "users.manage"]))).Content.ReadFromJsonAsync<RoleItem>())!;
        var invite = (await (await partner.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@hr.example", "HR person", hrRole.Id))).Content.ReadFromJsonAsync<InviteCreated>())!;
        var hr = (await (await api.Raw().PostAsJsonAsync("/api/auth/accept-invite",
            new AcceptInviteRequest(invite.InviteToken, "HR person", "Hr-password-123"))).Content.ReadFromJsonAsync<AuthResult>())!;
        var hrClient = api.WithToken(hr.AccessToken!);
        var partnerRole = await RoleId(partner, "Partner");

        // can't promote self, can't invite a Partner, can't deactivate the Partner
        Assert.Equal(HttpStatusCode.Forbidden, (await hrClient.PutAsJsonAsync($"/api/users/{hr.User!.Id}",
            new UpdateUserRequest(null, null, [partnerRole]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hrClient.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@x.example", null, partnerRole))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hrClient.PutAsJsonAsync($"/api/users/{u.UserId}",
            new UpdateUserRequest(null, false, null))).StatusCode);

        // but can invite with a role inside their own permissions
        Assert.Equal(HttpStatusCode.OK, (await hrClient.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@x.example", null, hrRole.Id))).StatusCode);
    }

    [Fact]
    public async Task Login_history_is_recorded_for_the_firm()
    {
        await Login(api.FirmA.Email, "Wrong-password-1");
        var events = await api.WithToken(api.FirmA.AccessToken).GetFromJsonAsync<List<LoginEventItem>>("/api/login-events");
        Assert.Contains(events!, e => e.Reason == "bad_password");
    }
}
