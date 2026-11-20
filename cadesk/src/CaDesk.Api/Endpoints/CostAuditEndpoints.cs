using System;
using System.Threading;
using CaDesk.Api.Auth;
using CaDesk.Application.CostAudit;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class CostAuditEndpoints
{
    public static IEndpointRouteBuilder MapCostAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cost-audit").WithTags("Cost Audit & Form CRA-1 to CRA-4 Desk (Companies Act Sec 148 / Cost Records Rules 2014)");

        // 1. Summary & Calculations
        g.MapGet("/summary", async (Guid? clientId, string? financialYear, CostAuditService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.CostAuditRead);

        g.MapPost("/applicability-check", (ApplicabilityCheckRequest req, CostAuditService svc) =>
        {
            var res = svc.CheckApplicability(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.CostAuditRead);

        g.MapPost("/variance-calculate", (MaterialVarianceRequest req, CostAuditService svc) =>
        {
            var res = svc.CalculateMaterialVariance(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.CostAuditRead);

        // 2. Engagements CRUD
        g.MapGet("/engagements", async (Guid? clientId, string? financialYear, string? auditStatus, CostAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.GetEngagementsAsync(clientId, financialYear, auditStatus, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.CostAuditRead);

        g.MapGet("/engagements/{id:guid}", async (Guid id, CostAuditService svc, CancellationToken ct) =>
        {
            var engagement = await svc.GetEngagementByIdAsync(id, ct);
            return engagement is null ? Results.NotFound() : Results.Ok(engagement);
        }).RequirePermission(Perms.CostAuditRead);

        g.MapPost("/engagements", async (CreateCostAuditEngagementRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateEngagementAsync(req, ct);
            return Results.Created($"/api/cost-audit/engagements/{created.Id}", created);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapPut("/engagements/{id:guid}", async (Guid id, UpdateCostAuditEngagementRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateEngagementAsync(id, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapDelete("/engagements/{id:guid}", async (Guid id, CostAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteEngagementAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.CostAuditManage);

        // 3. Form CRA-1 Cost Records Heads
        g.MapPost("/engagements/{id:guid}/seed-heads", async (Guid id, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.SeedStandardHeadsAsync(id, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapPut("/engagements/{id:guid}/heads/{headId:guid}", async (Guid id, Guid headId, UpdateRecordHeadRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateRecordHeadAsync(id, headId, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        // 4. Products & Cost Statements (Part B/C)
        g.MapPost("/engagements/{id:guid}/products", async (Guid id, CreateProductCostStatementRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.AddProductCostStatementAsync(id, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapPut("/engagements/{id:guid}/products/{productId:guid}", async (Guid id, Guid productId, UpdateProductCostStatementRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateProductCostStatementAsync(id, productId, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapDelete("/engagements/{id:guid}/products/{productId:guid}", async (Guid id, Guid productId, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.DeleteProductCostStatementAsync(id, productId, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        // 5. P&L Profit Reconciliation Items (Part D)
        g.MapPost("/engagements/{id:guid}/reconciliation", async (Guid id, CreateReconciliationItemRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.AddReconciliationItemAsync(id, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapPut("/engagements/{id:guid}/reconciliation/{itemId:guid}", async (Guid id, Guid itemId, UpdateReconciliationItemRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateReconciliationItemAsync(id, itemId, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapDelete("/engagements/{id:guid}/reconciliation/{itemId:guid}", async (Guid id, Guid itemId, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.DeleteReconciliationItemAsync(id, itemId, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        // 6. Form CRA-2 & CRA-4 Filings Tracking
        g.MapPost("/engagements/{id:guid}/cra2-filing", async (Guid id, RecordCra2FilingRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.RecordCra2FilingAsync(id, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapPost("/engagements/{id:guid}/cra4-filing", async (Guid id, RecordCra4FilingRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.RecordCra4FilingAsync(id, req, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        // 7. Form CRA-3 Report Generation & UDIN Certification
        g.MapPost("/engagements/{id:guid}/generate-cra3", async (Guid id, CostAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.GenerateCra3ReportAsync(id, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequirePermission(Perms.CostAuditManage);

        g.MapPost("/engagements/{id:guid}/certify", async (Guid id, CertifyEngagementRequest req, CostAuditService svc, CancellationToken ct) =>
        {
            try
            {
                var certified = await svc.CertifyEngagementAsync(id, req, ct);
                return certified is null ? Results.NotFound() : Results.Ok(certified);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.CostAuditCertify);

        return app;
    }
}
