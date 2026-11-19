using CaDesk.Api.Audit;
using CaDesk.Api.Auth;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit-logs", async (string? entity, string? entityId, Guid? actorId, string? action,
                DateTime? from, DateTime? to, DateTime? before, int? take, AuditQueryService s, CancellationToken ct) =>
                Results.Ok(await s.ListAsync(new AuditQuery(entity, entityId, actorId, action, from, to, before, take), ct)))
            .WithTags("Audit")
            .RequirePermission(Perms.AuditRead);
        return app;
    }
}
