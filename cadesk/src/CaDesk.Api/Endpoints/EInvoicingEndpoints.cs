using CaDesk.Api.Auth;
using CaDesk.Application.EInvoicing;
using CaDesk.Domain.EInvoicing;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class EInvoicingEndpoints
{
    public static IEndpointRouteBuilder MapEInvoicingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/einvoicing")
            .WithTags("Automated E-Invoicing & E-Way Bill Integration Desk")
            .RequireFeature(CaDesk.Domain.Features.FeatureKeys.EInvoicing)
            .RequirePlanDesk("einvoicing");

        // Summary
        g.MapGet("/summary", async (Guid? clientId, IEInvoicingService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.EInvoiceRead);

        // E-Invoices
        g.MapGet("/invoices", async (Guid? clientId, EInvoiceStatus? status, IEInvoicingService svc, CancellationToken ct) =>
        {
            var list = await svc.GetEInvoicesAsync(clientId, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.EInvoiceRead);

        g.MapGet("/invoices/{id:guid}", async (Guid id, IEInvoicingService svc, CancellationToken ct) =>
        {
            var item = await svc.GetEInvoiceByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.EInvoiceRead);

        g.MapPost("/invoices", async (GenerateEInvoiceRequest req, IEInvoicingService svc, CancellationToken ct) =>
        {
            var created = await svc.GenerateEInvoiceAsync(req, ct);
            return Results.Created($"/api/einvoicing/invoices/{created.Id}", created);
        }).RequirePermission(Perms.EInvoiceGenerate);

        g.MapPost("/invoices/{id:guid}/cancel", async (Guid id, CancelEInvoiceRequest req, IEInvoicingService svc, CancellationToken ct) =>
        {
            var cancelled = await svc.CancelEInvoiceAsync(id, req, ct);
            return Results.Ok(cancelled);
        }).RequirePermission(Perms.EInvoiceCancel);

        // E-Way Bills
        g.MapGet("/ewaybills", async (Guid? clientId, EwbStatus? status, IEInvoicingService svc, CancellationToken ct) =>
        {
            var list = await svc.GetEWayBillsAsync(clientId, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.EInvoiceRead);

        g.MapGet("/ewaybills/{id:guid}", async (Guid id, IEInvoicingService svc, CancellationToken ct) =>
        {
            var item = await svc.GetEWayBillByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.EInvoiceRead);

        g.MapPost("/ewaybills", async (GenerateEWayBillRequest req, IEInvoicingService svc, CancellationToken ct) =>
        {
            var created = await svc.GenerateEWayBillAsync(req, ct);
            return Results.Created($"/api/einvoicing/ewaybills/{created.Id}", created);
        }).RequirePermission(Perms.EInvoiceGenerate);

        g.MapPut("/ewaybills/{id:guid}/part-b", async (Guid id, UpdateEWayBillPartBRequest req, IEInvoicingService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateEWayBillPartBAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.EInvoiceGenerate);

        g.MapPost("/ewaybills/{id:guid}/cancel", async (Guid id, CancelEWayBillRequest req, IEInvoicingService svc, CancellationToken ct) =>
        {
            var cancelled = await svc.CancelEWayBillAsync(id, req, ct);
            return Results.Ok(cancelled);
        }).RequirePermission(Perms.EInvoiceCancel);

        return app;
    }
}
