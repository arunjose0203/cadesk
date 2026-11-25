using CaDesk.Api.Auth;
using CaDesk.Application.Gstr9;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class Gstr9Endpoints
{
    public static void MapGstr9Endpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/gstr9").RequirePlanDesk("gstr9")
            .RequireAuthorization(Policies.FirmMember);

        g.MapGet("/returns", async (
            [FromQuery] Guid? clientId,
            [FromQuery] string? financialYear,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var list = await service.GetReturnsAsync(clientId, financialYear, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.Gstr9Read);

        g.MapGet("/returns/{id:guid}", async (
            Guid id,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var item = await service.GetReturnByIdAsync(id, ct);
            return item is not null ? Results.Ok(item) : Results.NotFound();
        }).RequirePermission(Perms.Gstr9Read);

        g.MapPost("/returns/compile", async (
            [FromBody] CompileGstr9ReturnRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var created = await service.CompileReturnAsync(req, ct);
            return Results.Created($"/api/gstr9/returns/{created.Id}", created);
        }).RequirePermission(Perms.Gstr9Manage);

        g.MapPut("/returns/{id:guid}", async (
            Guid id,
            [FromBody] UpdateGstr9ReturnRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var updated = await service.UpdateReturnAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.Gstr9Manage);

        g.MapPost("/returns/{id:guid}/file", async (
            Guid id,
            [FromBody] FileGstr9ReturnRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var filed = await service.FileReturnAsync(id, req, ct);
            return Results.Ok(filed);
        }).RequirePermission(Perms.Gstr9Manage);

        g.MapGet("/returns/{id:guid}/gstr9c", async (
            Guid id,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var rec = await service.GetGstr9cAsync(id, ct);
            return rec is not null ? Results.Ok(rec) : Results.NotFound();
        }).RequirePermission(Perms.Gstr9Read);

        g.MapPost("/returns/{id:guid}/gstr9c/reconcile", async (
            Guid id,
            [FromBody] ReconcileGstr9cRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var rec = await service.ReconcileGstr9cAsync(id, req, ct);
            return Results.Ok(rec);
        }).RequirePermission(Perms.Gstr9Manage);

        g.MapPost("/returns/{id:guid}/gstr9c/discrepancies", async (
            Guid id,
            [FromBody] AddDiscrepancyRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var item = await service.AddDiscrepancyAsync(id, req, ct);
            return Results.Created($"/api/gstr9/discrepancies/{item.Id}", item);
        }).RequirePermission(Perms.Gstr9Manage);

        g.MapPost("/discrepancies/{id:guid}/resolve", async (
            Guid id,
            [FromBody] ResolveDiscrepancyRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var item = await service.ResolveDiscrepancyAsync(id, req, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.Gstr9Manage);

        g.MapPost("/returns/{id:guid}/gstr9c/certify", async (
            Guid id,
            [FromBody] CertifyGstr9cRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var rec = await service.CertifyGstr9cAsync(id, req, ct);
            return Results.Ok(rec);
        }).RequirePermission(Perms.Gstr9Certify);

        g.MapPost("/returns/{id:guid}/gstr9c/drc03", async (
            Guid id,
            [FromBody] RecordDrc03PaymentRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var rec = await service.RecordDrc03PaymentAsync(id, req, ct);
            return Results.Ok(rec);
        }).RequirePermission(Perms.Gstr9Manage);

        g.MapGet("/summary", async (
            [FromQuery] string? financialYear,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var summary = await service.GetSummaryAsync(financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.Gstr9Read);

        // Import GSTR-1 and/or GSTR-2B JSON to populate or update GSTR-9 return
        g.MapPost("/import-gst-json", async (
            [FromBody] ImportGstJsonToReturnRequest req,
            IGstr9Service service,
            CancellationToken ct) =>
        {
            var clientId = req.ClientId ?? Guid.Empty;
            var fy = req.FinancialYear ?? string.Empty;

            if (req.Gstr9Id.HasValue && req.Gstr9Id.Value != Guid.Empty && (clientId == Guid.Empty || string.IsNullOrEmpty(fy)))
            {
                var existing = await service.GetReturnByIdAsync(req.Gstr9Id.Value, ct);
                if (existing != null)
                {
                    clientId = existing.ClientId;
                    fy = existing.FinancialYear;
                }
            }

            var ret = await service.ImportGstJsonToReturnAsync(clientId, fy, req.Gstr1Json, req.Gstr2BJson, ct);
            return Results.Ok(ret);
        }).RequirePermission(Perms.Gstr9Manage);
    }
}
