using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Tds;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class TdsEndpoints
{
    public static IEndpointRouteBuilder MapTdsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tds").WithTags("TDS & Form 26Q/27Q Management");

        // KPI Summary
        g.MapGet("/summary", async (TdsService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.TdsRead);

        // Lower Deduction Certificates (LDC)
        g.MapGet("/ldc", async (Guid? clientId, string? financialYear, TdsService svc, CancellationToken ct) =>
        {
            var list = await svc.ListLdcsAsync(clientId, financialYear, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TdsRead);

        g.MapPost("/ldc", async (CreateLdcRequest req, ClaimsPrincipal user, TdsService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ldc = await svc.CreateLdcAsync(req, userId, ct);
            return Results.Created($"/api/tds/ldc/{ldc.Id}", ldc);
        }).RequirePermission(Perms.TdsManage);

        // Challans (ITNS 281)
        g.MapGet("/challans", async (Guid? clientId, string? financialYear, string? quarter, TdsService svc, CancellationToken ct) =>
        {
            var list = await svc.ListChallansAsync(clientId, financialYear, quarter, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TdsRead);

        g.MapPost("/challans", async (CreateTdsChallanRequest req, ClaimsPrincipal user, TdsService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var challan = await svc.CreateChallanAsync(req, userId, ct);
            return Results.Created($"/api/tds/challans/{challan.Id}", challan);
        }).RequirePermission(Perms.TdsManage);

        // Deductions Register
        g.MapGet("/deductions", async (Guid? clientId, string? financialYear, string? quarter, string? formType, string? bookingStatus, TdsService svc, CancellationToken ct) =>
        {
            var list = await svc.ListDeductionsAsync(clientId, financialYear, quarter, formType, bookingStatus, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TdsRead);

        g.MapPost("/deductions", async (RecordTdsDeductionRequest req, ClaimsPrincipal user, TdsService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var deduction = await svc.RecordDeductionAsync(req, userId, ct);
            return Results.Created($"/api/tds/deductions/{deduction.Id}", deduction);
        }).RequirePermission(Perms.TdsManage);

        // Map Deductions to Challan
        g.MapPost("/challans/map", async (MapDeductionsToChallanRequest req, ClaimsPrincipal user, TdsService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var updatedChallan = await svc.MapDeductionsToChallanAsync(req, userId, ct);
            return Results.Ok(updatedChallan);
        }).RequirePermission(Perms.TdsManage);

        // Calculate Interest u/s 201(1A)
        g.MapPost("/interest/calculate", (CalculateTdsInterestRequest req) =>
        {
            var result = TdsTaxCalculator.CalculateInterest(req.DeductionDate, req.PaymentDate, req.DepositDate, req.TdsAmount);
            return Results.Ok(result);
        }).RequirePermission(Perms.TdsRead);

        // Sprint 68: Deductor Profiles (BH/FH metadata), NSDL RPU .txt Export/Validate, FVU .err Parser, Form 16/16A Import
        g.MapGet("/profiles/{clientId:guid}", async (Guid clientId, TdsService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDeductorProfileAsync(clientId, ct)))
            .RequirePermission(Perms.TdsRead);

        g.MapPut("/profiles/{clientId:guid}", async (Guid clientId, SaveTdsDeductorProfileRequest req, ClaimsPrincipal user, TdsService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(await svc.SaveDeductorProfileAsync(clientId, req, userId, ct));
        }).RequirePermission(Perms.TdsManage);

        g.MapPost("/returns/export", async (ExportTdsReturnRequest req, TdsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ExportReturnAsync(req, ct)))
            .RequirePermission(Perms.TdsRead);

        g.MapPost("/returns/parse-err", (ParseFvuErrRequest req) =>
            Results.Ok(NsdlRpuBuilder.ParseFvuErrFile(req.ErrContent)))
            .RequirePermission(Perms.TdsRead);

        g.MapPost("/import/form16", async (ImportForm16Request req, ClaimsPrincipal user, TdsService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await svc.ImportForm16Async(req, userId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.TdsManage);

        return app;
    }
}
