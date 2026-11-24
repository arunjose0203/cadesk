using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Billing;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/invoices").WithTags("Invoices");

        g.MapGet("/", async (string? status, Guid? clientId, DateOnly? from, DateOnly? to, int? skip, int? take, BillingService svc, CancellationToken ct) =>
        {
            var invoices = await svc.GetInvoicesAsync(status, clientId, from, to, skip ?? 0, take ?? 50, ct);
            return Results.Ok(invoices);
        }).RequirePermission(Perms.BillingRead);

        g.MapGet("/summary", async (BillingService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.BillingRead);

        g.MapGet("/{id:guid}", async (Guid id, BillingService svc, CancellationToken ct) =>
        {
            var invoice = await svc.GetInvoiceByIdAsync(id, ct);
            return Results.Ok(invoice);
        }).RequirePermission(Perms.BillingRead);

        g.MapGet("/{id:guid}/html", async (Guid id, BillingService svc, CancellationToken ct) =>
        {
            var html = await svc.GetInvoiceHtmlAsync(id, ct);
            return Results.Content(html, "text/html");
        }).RequirePermission(Perms.BillingRead);

        g.MapPost("/{id:guid}/send-email", async (Guid id, BillingService svc, CancellationToken ct) =>
        {
            var invoice = await svc.SendInvoiceEmailAsync(id, ct);
            return Results.Ok(invoice);
        }).RequirePermission(Perms.BillingManage);

        g.MapPost("/", async (CreateInvoiceRequest req, ClaimsPrincipal user, BillingService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var invoice = await svc.CreateInvoiceAsync(req, userId, ct);
            return Results.Created($"/api/invoices/{invoice.Id}", invoice);
        }).RequirePermission(Perms.BillingManage);

        g.MapPut("/{id:guid}", async (Guid id, UpdateInvoiceRequest req, ClaimsPrincipal user, BillingService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var invoice = await svc.UpdateInvoiceAsync(id, req, userId, ct);
            return Results.Ok(invoice);
        }).RequirePermission(Perms.BillingManage);

        g.MapPut("/{id:guid}/status", async (Guid id, InvoiceStatusUpdateRequest req, BillingService svc, CancellationToken ct) =>
        {
            var invoice = await svc.UpdateStatusAsync(id, req.Status, ct);
            return Results.Ok(invoice);
        }).RequirePermission(Perms.BillingManage);

        g.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest req, ClaimsPrincipal user, BillingService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var payment = await svc.RecordPaymentAsync(id, req, userId, ct);
            return Results.Created($"/api/invoices/{id}/payments/{payment.Id}", payment);
        }).RequirePermission(Perms.BillingManage);

        g.MapDelete("/{id:guid}", async (Guid id, BillingService svc, CancellationToken ct) =>
        {
            await svc.DeleteInvoiceAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.BillingManage);

        return app;
    }
}
