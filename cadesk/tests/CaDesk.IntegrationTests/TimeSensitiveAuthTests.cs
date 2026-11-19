using System.Net;
using System.Net.Http.Json;
using CaDesk.Api.Auth;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Tests that move the server clock (MFA codes, token reuse, lockout expiry).
/// Own class = own ApiFactory, so the clock changes don't affect other tests.
/// </summary>
public class TimeSensitiveAuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory api;

    public TimeSensitiveAuthTests(ApiFactory api)
    {
        this.api = api;
        api.Clock.Offset = TimeSpan.Zero; // xunit creates the class per test: each test starts at real time
    }

    private string CodeNow(string base32Secret) =>
        Totp.Code(Base32.Decode(base32Secret), Totp.CurrentStep(api.Clock.GetUtcNow()));

    private void NextTotpStep() => api.Clock.Offset += TimeSpan.FromSeconds(Totp.StepSeconds);

    [Fact]
    public async Task Mfa_set_up_login_replay_protection_and_recovery_codes()
    {
        var u = await api.SignUpAsync("MFA Co");
        var c = api.WithToken(u.AccessToken);

        var setup = (await (await c.PostAsync("/api/auth/mfa/setup", null)).Content.ReadFromJsonAsync<MfaSetupResult>())!;
        Assert.StartsWith("otpauth://totp/", setup.OtpAuthUri);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync("/api/auth/mfa/enable", new MfaCodeRequest("000000"))).StatusCode);
        var enableCode = CodeNow(setup.Secret);
        var enable = await c.PostAsJsonAsync("/api/auth/mfa/enable", new MfaCodeRequest(enableCode));
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var recovery = (await enable.Content.ReadFromJsonAsync<MfaEnabledResult>())!.RecoveryCodes;
        Assert.Equal(8, recovery.Count);

        // password step now asks for the code
        var login = await api.Raw().PostAsJsonAsync("/api/auth/login", new LoginRequest(u.Email, ApiFactory.Password, null));
        var pending = (await login.Content.ReadFromJsonAsync<AuthResult>())!;
        Assert.Equal(AuthStatus.MfaRequired, pending.Status);
        Assert.Null(pending.AccessToken);

        // the code used to enable can't be replayed in the same 30-second step
        var replay = await api.Raw().PostAsJsonAsync("/api/auth/mfa/verify", new MfaVerifyRequest(pending.MfaToken!, enableCode));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        NextTotpStep();
        var verified = await api.Raw().PostAsJsonAsync("/api/auth/mfa/verify", new MfaVerifyRequest(pending.MfaToken!, CodeNow(setup.Secret)));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.NotNull((await verified.Content.ReadFromJsonAsync<AuthResult>())!.AccessToken);

        // a recovery code works once
        var login2 = (await (await api.Raw().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(u.Email, ApiFactory.Password, null))).Content.ReadFromJsonAsync<AuthResult>())!;
        Assert.Equal(HttpStatusCode.OK, (await api.Raw().PostAsJsonAsync("/api/auth/mfa/verify",
            new MfaVerifyRequest(login2.MfaToken!, recovery[0].ToUpperInvariant()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Raw().PostAsJsonAsync("/api/auth/mfa/verify",
            new MfaVerifyRequest(login2.MfaToken!, recovery[0]))).StatusCode);
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_ends_the_whole_session()
    {
        var u = await api.SignUpAsync("Reuse Co");

        var first = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", u.RefreshToken));
        var current = TestData.RefreshCookie(first)!;

        // within a few seconds: treated as two tabs refreshing at once, nothing revoked
        var quick = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", u.RefreshToken));
        Assert.Equal(HttpStatusCode.Conflict, quick.StatusCode);

        // later: the old token is being replayed -> the whole family is revoked
        api.Clock.Offset += TimeSpan.FromMinutes(2);
        var replay = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", u.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var stillValid = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", current));
        Assert.Equal(HttpStatusCode.Unauthorized, stillValid.StatusCode);
    }

    [Fact]
    public async Task Lockout_ends_after_fifteen_minutes()
    {
        var u = await api.SignUpAsync("Unlock Co");
        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
            await api.Raw().PostAsJsonAsync("/api/auth/login", new LoginRequest(u.Email, "Wrong-password-1", null));

        var locked = await api.Raw().PostAsJsonAsync("/api/auth/login", new LoginRequest(u.Email, ApiFactory.Password, null));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        api.Clock.Offset += AuthService.LockoutDuration + TimeSpan.FromSeconds(5);
        var ok = await api.Raw().PostAsJsonAsync("/api/auth/login", new LoginRequest(u.Email, ApiFactory.Password, null));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Inactive_sessions_are_signed_out_after_the_firm_timeout()
    {
        var u = await api.SignUpAsync("Idle Co");
        await api.WithToken(u.AccessToken).PutAsJsonAsync("/api/settings/security.session_timeout_min",
            new CaDesk.Infrastructure.Configuration.SaveSettingRequest(
                System.Text.Json.JsonDocument.Parse("30").RootElement.Clone(), null, null, null, null));

        // active within the window: fine (and the refresh resets the clock)
        api.Clock.Offset += TimeSpan.FromMinutes(25);
        var ok = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", u.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var next = TestData.RefreshCookie(ok)!;

        // idle for longer than 30 minutes: signed out
        api.Clock.Offset += TimeSpan.FromMinutes(31);
        var idle = await api.Raw().SendAsync(TestData.WithRefreshCookie("/api/auth/refresh", next));
        Assert.Equal(HttpStatusCode.Unauthorized, idle.StatusCode);
        Assert.Contains("inactivity", await idle.Content.ReadAsStringAsync());
    }
}
