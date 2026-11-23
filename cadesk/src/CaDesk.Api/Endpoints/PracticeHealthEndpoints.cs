using CaDesk.Api.Auth;
using CaDesk.Application.PracticeHealth;
using CaDesk.Domain.PracticeHealth;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class PracticeHealthEndpoints
{
    public static IEndpointRouteBuilder MapPracticeHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/practice-health").WithTags("Practice Health, Succession & Valuation Desk");

        // 1. Live Practice Telemetry
        g.MapGet("/telemetry", async (IPracticeHealthService svc, CancellationToken ct) =>
        {
            var tele = await svc.GetFirmTelemetryAsync(ct);
            return Results.Ok(tele);
        }).RequirePermission(Perms.PracticeHealthRead);

        // 2. Firm Valuation Calculator
        g.MapPost("/valuation/calculate", (CalculateValuationRequest req, IPracticeHealthService svc) =>
        {
            var res = svc.CalculateValuation(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.PracticeHealthRead);

        g.MapPost("/valuation", async (SaveValuationRequest req, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var res = await svc.SaveValuationAsync(req, ct);
            return Results.Created($"/api/practice-health/valuation/{res.Id}", res);
        }).RequirePermission(Perms.PracticeHealthValuation);

        g.MapGet("/valuation", async (IPracticeHealthService svc, CancellationToken ct) =>
        {
            var list = await svc.GetValuationsAsync(ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.PracticeHealthRead);

        g.MapGet("/valuation/{id:guid}", async (Guid id, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var item = await svc.GetValuationByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.PracticeHealthRead);

        g.MapPost("/valuation/{id:guid}/approve", async (Guid id, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var item = await svc.ApproveValuationAsync(id, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.PracticeHealthValuation);

        // 3. Partner Equity & Cap Table
        g.MapGet("/equity", async (IPracticeHealthService svc, CancellationToken ct) =>
        {
            var list = await svc.GetPartnerEquitiesAsync(ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.PracticeHealthRead);

        g.MapPost("/equity", async (SavePartnerEquityRequest req, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var item = await svc.SavePartnerEquityAsync(req, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.PracticeHealthManage);

        g.MapDelete("/equity/{id:guid}", async (Guid id, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeletePartnerEquityAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.PracticeHealthManage);

        // 4. Partner Retirement Plans & Buyout Amortization
        g.MapGet("/retirement-plans", async (IPracticeHealthService svc, CancellationToken ct) =>
        {
            var list = await svc.GetRetirementPlansAsync(ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.PracticeHealthRead);

        g.MapPost("/retirement-plans", async (CreateRetirementPlanRequest req, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var item = await svc.CreateRetirementPlanAsync(req, ct);
            return Results.Created($"/api/practice-health/retirement-plans/{item.Id}", item);
        }).RequirePermission(Perms.PracticeHealthManage);

        g.MapPatch("/retirement-plans/{id:guid}/status", async (Guid id, RetirementPlanStatus status, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var item = await svc.UpdateRetirementPlanStatusAsync(id, status, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.PracticeHealthManage);

        // 5. Client Succession Transitions
        g.MapGet("/transitions", async (IPracticeHealthService svc, CancellationToken ct) =>
        {
            var list = await svc.GetClientTransitionsAsync(ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.PracticeHealthRead);

        g.MapPost("/transitions", async (SaveClientTransitionRequest req, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var item = await svc.SaveClientTransitionAsync(req, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.PracticeHealthManage);

        g.MapDelete("/transitions/{id:guid}", async (Guid id, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteClientTransitionAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.PracticeHealthManage);

        // 6. Peer Benchmarking Telemetry
        g.MapPost("/benchmarks/generate", async (GenerateBenchmarkRequest req, IPracticeHealthService svc, CancellationToken ct) =>
        {
            var item = await svc.GenerateBenchmarkAsync(req, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.PracticeHealthRead);

        g.MapGet("/benchmarks", async (IPracticeHealthService svc, CancellationToken ct) =>
        {
            var list = await svc.GetBenchmarksAsync(ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.PracticeHealthRead);

        return app;
    }
}
