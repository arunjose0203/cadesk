namespace CaDesk.Api.Auth;

/// <summary>
/// When the firm requires two-step sign-in for a user's role and the user hasn't set it up,
/// their token carries mfa_setup=required. Until they set it up (and refresh), only the
/// endpoints needed to do that are allowed; everything else gets 403 with a clear reason.
/// </summary>
public sealed class MfaSetupGate(RequestDelegate next)
{
    private static readonly string[] Allowed =
    [
        "/api/auth/",          // me, mfa/setup, mfa/enable, refresh, logout, password
        "/api/me/settings",
        "/api/health",
    ];

    public async Task InvokeAsync(HttpContext ctx)
    {
        var restricted = ctx.User.FindFirst(AppClaims.MfaSetup)?.Value == "required";
        var path = ctx.Request.Path.Value ?? "";
        if (restricted && !Allowed.Any(a => path.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
        {
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    title: "Set up two-step sign-in first",
                    detail: "Your firm requires two-step sign-in for your role. Open My account to set it up.",
                    extensions: new Dictionary<string, object?> { ["code"] = "mfa_setup_required" })
                .ExecuteAsync(ctx);
            return;
        }
        await next(ctx);
    }
}
