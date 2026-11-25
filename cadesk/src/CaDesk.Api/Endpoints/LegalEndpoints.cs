using CaDesk.Api.Auth;
using CaDesk.Application.Legal;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

/// <summary>Sprint 71: terms, privacy notice and data processing terms, and who accepted which version.</summary>
public static class LegalEndpoints
{
    public static IEndpointRouteBuilder MapLegalEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/legal").WithTags("Legal");

        // public: the current versions, for the sign-up page
        g.MapGet("/documents", () => Results.Ok(LegalService.Published())).AllowAnonymous();

        g.MapGet("/status", async (HttpContext http, LegalService svc, CancellationToken ct) =>
                Results.Ok(await svc.StatusAsync(ManagesFirm(http), ct)))
            .RequireAuthorization(Policies.FirmMember);

        g.MapPost("/accept", async (AcceptLegalRequest r, HttpContext http, LegalService svc, CancellationToken ct) =>
                Results.Ok(await svc.AcceptAsync(r, ManagesFirm(http), http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString(), ct)))
            .RequireAuthorization(Policies.FirmMember);

        return app;
    }

    private static bool ManagesFirm(HttpContext http) =>
        http.User.HasClaim(AppClaims.Permission, Perms.SettingsManage);
}
