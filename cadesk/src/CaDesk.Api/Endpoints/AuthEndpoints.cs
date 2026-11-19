using CaDesk.Api.Auth;

namespace CaDesk.Api.Endpoints;

public static class AuthEndpoints
{
    public const string RefreshCookie = "cadesk_rt";
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        // ---- anonymous, rate limited
        var open = g.MapGroup("").AllowAnonymous().RequireRateLimiting(RateLimitPolicy);

        open.MapPost("/signup", async (SignUpRequest r, AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Respond(ctx, await auth.SignUpAsync(r, Info(ctx), ct)));

        open.MapPost("/login", async (LoginRequest r, AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Respond(ctx, await auth.LoginAsync(r, Info(ctx), ct)));

        open.MapPost("/mfa/verify", async (MfaVerifyRequest r, AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Respond(ctx, await auth.VerifyMfaAsync(r, Info(ctx), ct)));

        open.MapPost("/refresh", async (AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Respond(ctx, await auth.RefreshAsync(ctx.Request.Cookies[RefreshCookie], Info(ctx), ct)));

        open.MapPost("/accept-invite", async (AcceptInviteRequest r, AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Respond(ctx, await auth.AcceptInvitationAsync(r, Info(ctx), ct)));

        open.MapPost("/logout", async (AuthService auth, HttpContext ctx, CancellationToken ct) =>
        {
            await auth.LogoutAsync(ctx.Request.Cookies[RefreshCookie], ct);
            ctx.Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
            return Results.NoContent();
        });

        // ---- signed in
        var me = g.MapGroup("").RequireAuthorization(Policies.FirmMember);

        me.MapGet("/me", async (AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Results.Ok(await auth.GetProfileAsync(ctx.User.UserId()!.Value, ct)));

        me.MapPost("/password", async (ChangePasswordRequest r, AuthService auth, HttpContext ctx, CancellationToken ct) =>
                Respond(ctx, await auth.ChangePasswordAsync(ctx.User.UserId()!.Value, r, Info(ctx), ct)))
            .RequireRateLimiting(RateLimitPolicy);

        me.MapPost("/mfa/setup", async (AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Results.Ok(await auth.BeginMfaSetupAsync(ctx.User.UserId()!.Value, ct)));

        me.MapPost("/mfa/enable", async (MfaCodeRequest r, AuthService auth, HttpContext ctx, CancellationToken ct) =>
            Results.Ok(await auth.EnableMfaAsync(ctx.User.UserId()!.Value, r.Code, ct)));

        me.MapPost("/mfa/disable", async (MfaCodeRequest r, AuthService auth, HttpContext ctx, CancellationToken ct) =>
        {
            await auth.DisableMfaAsync(ctx.User.UserId()!.Value, r.Code, ct);
            return Results.NoContent();
        }).RequireRateLimiting(RateLimitPolicy);

        return app;
    }

    private static RequestInfo Info(HttpContext ctx) =>
        new(ctx.Connection.RemoteIpAddress, ctx.Request.Headers.UserAgent.ToString());

    /// <summary>Refresh token goes in an HttpOnly cookie scoped to /api/auth; access token in the body.</summary>
    private static IResult Respond(HttpContext ctx, AuthResult result)
    {
        if (result.RefreshToken is { } rt)
        {
            var o = RefreshCookieOptions();
            o.Expires = result.RefreshTokenExpires;
            ctx.Response.Cookies.Append(RefreshCookie, rt, o);
        }
        return Results.Ok(result);
    }

    private static CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        IsEssential = true,
    };
}
