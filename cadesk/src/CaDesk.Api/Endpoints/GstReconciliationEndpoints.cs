using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Gst;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class GstReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapGstReconciliationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/gst").WithTags("GST 2A/2B Reconciliation");

        // KPI Summary
        g.MapGet("/summary", async (GstReconciliationService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.GstRead);

        // List Batches
        g.MapGet("/batches", async (Guid? clientId, string? financialYear, string? returnPeriod, GstReconciliationService svc, CancellationToken ct) =>
        {
            var list = await svc.ListBatchesAsync(clientId, financialYear, returnPeriod, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.GstRead);

        // Get single batch
        g.MapGet("/batches/{id:guid}", async (Guid id, GstReconciliationService svc, CancellationToken ct) =>
        {
            var batch = await svc.GetBatchByIdAsync(id, ct);
            return Results.Ok(batch);
        }).RequirePermission(Perms.GstRead);

        // Run reconciliation
        g.MapPost("/reconcile", async (RunReconciliationRequest req, ClaimsPrincipal user, GstReconciliationService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var batch = await svc.RunReconciliationAsync(req, userId, ct);
            return Results.Created($"/api/gst/batches/{batch.Id}", batch);
        }).RequirePermission(Perms.GstReconcile);

        // List items in batch
        g.MapGet("/batches/{id:guid}/items", async (Guid id, string? matchStatus, GstReconciliationService svc, CancellationToken ct) =>
        {
            var items = await svc.ListItemsAsync(id, matchStatus, ct);
            return Results.Ok(items);
        }).RequirePermission(Perms.GstRead);

        // Delete batch
        g.MapDelete("/batches/{id:guid}", async (Guid id, GstReconciliationService svc, CancellationToken ct) =>
        {
            return await svc.DeleteBatchAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.GstReconcile);

        return app;
    }
}
