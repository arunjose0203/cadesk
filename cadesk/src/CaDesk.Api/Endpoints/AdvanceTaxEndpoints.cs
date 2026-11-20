using CaDesk.Api.Auth;
using CaDesk.Application.AdvanceTax;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class AdvanceTaxEndpoints
{
    public static IEndpointRouteBuilder MapAdvanceTaxEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/advance-tax").WithTags("Advance Tax Planner & Section 234A/B/C Interest Engine");

        // Summary KPI
        g.MapGet("/summary", async (Guid? clientId, string? financialYear, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.AdvanceTaxRead);

        // Plans
        g.MapGet("/plans", async (Guid? clientId, string? financialYear, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var list = await svc.GetPlansAsync(clientId, financialYear, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AdvanceTaxRead);

        g.MapGet("/plans/{id:guid}", async (Guid id, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var item = await svc.GetPlanByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.AdvanceTaxRead);

        g.MapPost("/plans", async (SaveAdvanceTaxPlanRequest req, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var created = await svc.CreatePlanAsync(req, ct);
            return Results.Created($"/api/advance-tax/plans/{created.Id}", created);
        }).RequirePermission(Perms.AdvanceTaxManage);

        g.MapPut("/plans/{id:guid}", async (Guid id, SaveAdvanceTaxPlanRequest req, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdatePlanAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AdvanceTaxManage);

        g.MapDelete("/plans/{id:guid}", async (Guid id, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            await svc.DeletePlanAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.AdvanceTaxManage);

        // Installments
        g.MapGet("/plans/{id:guid}/installments", async (Guid id, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var installments = await svc.GetInstallmentsAsync(id, ct);
            return Results.Ok(installments);
        }).RequirePermission(Perms.AdvanceTaxRead);

        g.MapPost("/installments/{installmentId:guid}/pay", async (Guid installmentId, RecordInstallmentPaymentRequest req, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var updated = await svc.RecordPaymentAsync(installmentId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AdvanceTaxManage);

        // Statutory Interest Engine (Sections 234A, 234B, 234C)
        g.MapPost("/plans/{id:guid}/calculate-interest", async (Guid id, CalculateStatutoryInterestRequest req, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var result = await svc.CalculateInterestAsync(id, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.AdvanceTaxCalculate);

        g.MapGet("/plans/{id:guid}/interest-computations", async (Guid id, IAdvanceTaxService svc, CancellationToken ct) =>
        {
            var list = await svc.GetInterestComputationsAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AdvanceTaxRead);

        return app;
    }
}
