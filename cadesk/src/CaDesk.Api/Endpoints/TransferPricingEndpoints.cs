using System;
using System.Threading;
using CaDesk.Api.Auth;
using CaDesk.Application.TransferPricing;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class TransferPricingEndpoints
{
    public static IEndpointRouteBuilder MapTransferPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/transfer-pricing").WithTags("Transfer Pricing Study & Form 3CEB Certification Desk (Income Tax Act Sec 92E / Rule 10D & 10E)");

        // 1. Summary & Benchmark Calculator
        g.MapGet("/summary", async (Guid? clientId, string? financialYear, TransferPricingService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.TransferPricingRead);

        g.MapPost("/calculate-benchmark", (BenchmarkCalculationRequest req, TransferPricingService svc) =>
        {
            var result = svc.CalculateBenchmark(req);
            return Results.Ok(result);
        }).RequirePermission(Perms.TransferPricingRead);

        // 2. Studies CRUD & Certification
        g.MapGet("/studies", async (Guid? clientId, string? financialYear, string? status, TransferPricingService svc, CancellationToken ct) =>
        {
            var list = await svc.ListStudiesAsync(clientId, financialYear, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TransferPricingRead);

        g.MapGet("/studies/{id:guid}", async (Guid id, TransferPricingService svc, CancellationToken ct) =>
        {
            var item = await svc.GetStudyByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.TransferPricingRead);

        g.MapPost("/studies", async (SaveStudyRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateStudyAsync(null, req, ct);
            return Results.Created($"/api/transfer-pricing/studies/{created.Id}", created);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapPut("/studies/{id:guid}", async (Guid id, SaveStudyRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateStudyAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapDelete("/studies/{id:guid}", async (Guid id, TransferPricingService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteStudyAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapPost("/studies/{id:guid}/certify", async (Guid id, CertifyStudyRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var certified = await svc.CertifyStudyAsync(id, req, ct);
            return Results.Ok(certified);
        }).RequirePermission(Perms.TransferPricingCertify);

        g.MapPost("/studies/{id:guid}/generate-3ceb", async (Guid id, TransferPricingService svc, CancellationToken ct) =>
        {
            var report = await svc.GenerateForm3CEBReportAsync(id, ct);
            return Results.Ok(new { ReportText = report });
        }).RequirePermission(Perms.TransferPricingRead);

        // 3. Associated Enterprises
        g.MapGet("/studies/{id:guid}/enterprises", async (Guid id, TransferPricingService svc, CancellationToken ct) =>
        {
            var list = await svc.ListEnterprisesAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TransferPricingRead);

        g.MapPost("/studies/{id:guid}/enterprises", async (Guid id, SaveEnterpriseRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateEnterpriseAsync(id, null, req, ct);
            return Results.Created($"/api/transfer-pricing/studies/{id}/enterprises/{created.Id}", created);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapPut("/studies/{id:guid}/enterprises/{aeId:guid}", async (Guid id, Guid aeId, SaveEnterpriseRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateEnterpriseAsync(id, aeId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapDelete("/studies/{id:guid}/enterprises/{aeId:guid}", async (Guid id, Guid aeId, TransferPricingService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteEnterpriseAsync(id, aeId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.TransferPricingManage);

        // 4. Transactions
        g.MapGet("/studies/{id:guid}/transactions", async (Guid id, TransferPricingService svc, CancellationToken ct) =>
        {
            var list = await svc.ListTransactionsAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TransferPricingRead);

        g.MapPost("/studies/{id:guid}/transactions", async (Guid id, SaveTransactionRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateTransactionAsync(id, null, req, ct);
            return Results.Created($"/api/transfer-pricing/studies/{id}/transactions/{created.Id}", created);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapPut("/studies/{id:guid}/transactions/{txId:guid}", async (Guid id, Guid txId, SaveTransactionRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateTransactionAsync(id, txId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapDelete("/studies/{id:guid}/transactions/{txId:guid}", async (Guid id, Guid txId, TransferPricingService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteTransactionAsync(id, txId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.TransferPricingManage);

        // 5. Comparable Benchmarks
        g.MapGet("/studies/{id:guid}/comparables", async (Guid id, TransferPricingService svc, CancellationToken ct) =>
        {
            var list = await svc.ListComparablesAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TransferPricingRead);

        g.MapPost("/studies/{id:guid}/comparables", async (Guid id, SaveComparableRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateComparableAsync(id, null, req, ct);
            return Results.Created($"/api/transfer-pricing/studies/{id}/comparables/{created.Id}", created);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapPut("/studies/{id:guid}/comparables/{compId:guid}", async (Guid id, Guid compId, SaveComparableRequest req, TransferPricingService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateComparableAsync(id, compId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.TransferPricingManage);

        g.MapDelete("/studies/{id:guid}/comparables/{compId:guid}", async (Guid id, Guid compId, TransferPricingService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteComparableAsync(id, compId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.TransferPricingManage);

        return app;
    }
}
