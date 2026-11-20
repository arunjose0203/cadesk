using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Dsc;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class DscEndpoints
{
    public static IEndpointRouteBuilder MapDscEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/dsc").WithTags("DSC");

        g.MapGet("/", async (Guid? clientId, string? status, string? location, int? expiringWithinDays, int? skip, int? take, DscService svc, CancellationToken ct) =>
        {
            var list = await svc.GetListAsync(clientId, status, location, expiringWithinDays, skip ?? 0, take ?? 50, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.DscRead);

        g.MapGet("/summary", async (DscService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.DscRead);

        g.MapGet("/{id:guid}", async (Guid id, DscService svc, CancellationToken ct) =>
        {
            var dsc = await svc.GetByIdAsync(id, ct);
            return Results.Ok(dsc);
        }).RequirePermission(Perms.DscRead);

        g.MapPost("/", async (CreateDscRequest req, ClaimsPrincipal user, DscService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var dsc = await svc.CreateAsync(req, userId, ct);
            return Results.Created($"/api/dsc/{dsc.Id}", dsc);
        }).RequirePermission(Perms.DscManage);

        g.MapPut("/{id:guid}", async (Guid id, UpdateDscRequest req, ClaimsPrincipal user, DscService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var dsc = await svc.UpdateAsync(id, req, userId, ct);
            return Results.Ok(dsc);
        }).RequirePermission(Perms.DscManage);

        g.MapPost("/{id:guid}/custody", async (Guid id, TransferCustodyRequest req, ClaimsPrincipal user, DscService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var dsc = await svc.TransferCustodyAsync(id, req, userId, ct);
            return Results.Ok(dsc);
        }).RequirePermission(Perms.DscManage);

        g.MapPost("/{id:guid}/transfer", async (Guid id, TransferCustodyRequest req, ClaimsPrincipal user, DscService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var dsc = await svc.TransferCustodyAsync(id, req, userId, ct);
            return Results.Ok(dsc);
        }).RequirePermission(Perms.DscManage);

        g.MapDelete("/{id:guid}", async (Guid id, DscService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.DscManage);

        return app;
    }
}
