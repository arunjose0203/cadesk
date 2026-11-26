using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Tax;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class TaxComputationEndpoints
{
    public static IEndpointRouteBuilder MapTaxComputationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tax").WithTags("Tax Computation Engine");

        // Preview regime comparison without persisting
        g.MapPost("/preview", (CalculateTaxRequest req, TaxComputationService svc) =>
        {
            var result = svc.PreviewComparisonAsync(req);
            return Results.Ok(result.Result);
        }).RequirePermission(Perms.ComputationsRun);

        g.MapPost("/comparison", (CalculateTaxRequest req, TaxComputationService svc) =>
        {
            var result = svc.PreviewComparisonAsync(req);
            return Results.Ok(result.Result);
        }).RequirePermission(Perms.ComputationsRun);

        // List computations
        g.MapGet("/computations", async (Guid? clientId, string? assessmentYear, int? skip, int? take, TaxComputationService svc, CancellationToken ct) =>
        {
            var list = await svc.ListComputationsAsync(clientId, assessmentYear, skip ?? 0, take ?? 50, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.ComputationsRun);

        // Get single computation
        g.MapGet("/computations/{id:guid}", async (Guid id, TaxComputationService svc, CancellationToken ct) =>
        {
            var comp = await svc.GetByIdAsync(id, ct);
            return Results.Ok(comp);
        }).RequirePermission(Perms.ComputationsRun);

        // Create & save computation
        g.MapPost("/computations", async (CalculateTaxRequest req, ClaimsPrincipal user, TaxComputationService svc, CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var comp = await svc.SaveComputationAsync(req, userId, ct);
            return Results.Created($"/api/tax/computations/{comp.Id}", comp);
        }).RequirePermission(Perms.ComputationsRun);

        // Import AIS / 26AS JSON and auto-generate dual-regime tax computation
        g.MapPost("/computations/import-ais", async (ImportAisToComputationRequest req, ClaimsPrincipal user, TaxComputationService svc, CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var comp = await svc.ImportAisToComputationAsync(req.ClientId, req.AisJson, userId, ct);
            return Results.Created($"/api/tax/computations/{comp.Id}", comp);
        }).RequirePermission(Perms.ComputationsRun);

        // Update computation status (e.g. approve, mark filed)
        g.MapPut("/computations/{id:guid}/status", async (Guid id, string status, ClaimsPrincipal user, TaxComputationService svc, CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var updated = await svc.UpdateStatusAsync(id, status, userId, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.ComputationsApprove);

        // Export ITR-1 (Sahaj) Official Schema JSON (Sprint 67)
        g.MapPost("/computations/{id:guid}/export-itr1", async (Guid id, ExportItrJsonRequest? req, TaxComputationService svc, CancellationToken ct) =>
        {
            var result = await svc.ExportItr1JsonAsync(id, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.ComputationsRun);

        // Business Tax (ITR-5 Firm/LLP & ITR-6 Company) Prefill, Preview, Save & Official Schema Exports
        g.MapPost("/business/prefill", async (BusinessPrefillRequest req, TaxComputationService svc, CancellationToken ct) =>
        {
            var prefill = await svc.PrefillBusinessAsync(req.ClientId, req.FinancialYear, req.AssessmentYear, req.EntityType, ct);
            return Results.Ok(prefill);
        }).RequirePermission(Perms.ComputationsRun);

        g.MapPost("/business/preview", async (CalculateBusinessTaxRequest req, TaxComputationService svc) =>
        {
            var result = await svc.PreviewBusinessAsync(req);
            return Results.Ok(result);
        }).RequirePermission(Perms.ComputationsRun);

        g.MapPost("/business/computations", async (CalculateBusinessTaxRequest req, ClaimsPrincipal user, TaxComputationService svc, CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var saved = await svc.SaveBusinessComputationAsync(req, userId, ct);
            return Results.Created($"/api/tax/computations/{saved.Id}", saved);
        }).RequirePermission(Perms.ComputationsRun);

        g.MapPost("/computations/{id:guid}/export-itr5", async (Guid id, ExportItrJsonRequest? req, TaxComputationService svc, CancellationToken ct) =>
        {
            var result = await svc.ExportItr5JsonAsync(id, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.ComputationsRun);

        g.MapPost("/computations/{id:guid}/export-itr6", async (Guid id, ExportItrJsonRequest? req, TaxComputationService svc, CancellationToken ct) =>
        {
            var result = await svc.ExportItr6JsonAsync(id, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.ComputationsRun);

        // Delete computation
        g.MapDelete("/computations/{id:guid}", async (Guid id, TaxComputationService svc, CancellationToken ct) =>
        {
            return await svc.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.ComputationsRun);

        return app;
    }
}
