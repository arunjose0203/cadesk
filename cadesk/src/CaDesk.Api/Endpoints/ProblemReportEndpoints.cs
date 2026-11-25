using CaDesk.Api.Auth;
using CaDesk.Application.Support;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

/// <summary>Sprint 72: crash reports and "Report a problem".</summary>
public static class ProblemReportEndpoints
{
    public const string RateLimitPolicy = "problem-reports";

    public static IEndpointRouteBuilder MapProblemReportEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/problem-reports").WithTags("Support");

        g.MapPost("", async (ProblemReportRequest r, HttpContext http, ProblemReportService svc, CancellationToken ct) =>
                Results.Ok(await svc.CreateAsync(r, http.Request.Headers.UserAgent.ToString(), ct)))
            .RequireAuthorization(Policies.FirmMember)
            .RequireRateLimiting(RateLimitPolicy);

        g.MapGet("", async (string? status, int? take, ProblemReportService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(status, take ?? 50, ct)))
            .RequirePermission(Perms.SettingsManage);

        g.MapPost("/{id:guid}/status", async (Guid id, SetProblemStatusRequest r, ProblemReportService svc, CancellationToken ct) =>
                await svc.SetStatusAsync(id, r.Status, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.SettingsManage);

        return app;
    }
}

public sealed record SetProblemStatusRequest(string Status);
