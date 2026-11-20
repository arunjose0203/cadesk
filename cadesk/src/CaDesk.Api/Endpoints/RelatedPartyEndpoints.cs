using System;
using CaDesk.Api.Auth;
using CaDesk.Application.RelatedParty;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class RelatedPartyEndpoints
{
    public static IEndpointRouteBuilder MapRelatedPartyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/related-party").WithTags("Related Party Transactions & Section 188 / AS 18 / Ind AS 24 Desk");

        // 1. Summary
        g.MapGet("/summary", async (Guid? clientId, string? fy, RelatedPartyService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, fy, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.RelatedPartyRead);

        // 2. Entities (Related Parties Master)
        g.MapGet("/entities", async (Guid? clientId, RelatedPartyService svc, CancellationToken ct) =>
        {
            var list = await svc.ListEntitiesAsync(clientId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.RelatedPartyRead);

        g.MapGet("/entities/{id:guid}", async (Guid id, RelatedPartyService svc, CancellationToken ct) =>
        {
            var entity = await svc.GetEntityByIdAsync(id, ct);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        }).RequirePermission(Perms.RelatedPartyRead);

        g.MapPost("/entities", async (SaveRelatedPartyEntityRequest req, RelatedPartyService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateEntityAsync(null, req, ct);
            return Results.Created($"/api/related-party/entities/{created.Id}", created);
        }).RequirePermission(Perms.RelatedPartyManage);

        g.MapPut("/entities/{id:guid}", async (Guid id, SaveRelatedPartyEntityRequest req, RelatedPartyService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateEntityAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.RelatedPartyManage);

        g.MapDelete("/entities/{id:guid}", async (Guid id, RelatedPartyService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteEntityAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.RelatedPartyManage);

        // 3. Transactions
        g.MapGet("/transactions", async (
            Guid? clientId,
            string? fy,
            Guid? relatedPartyId,
            string? status,
            RelatedPartyService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListTransactionsAsync(clientId, fy, relatedPartyId, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.RelatedPartyRead);

        g.MapGet("/transactions/{id:guid}", async (Guid id, RelatedPartyService svc, CancellationToken ct) =>
        {
            var tx = await svc.GetTransactionByIdAsync(id, ct);
            return tx is null ? Results.NotFound() : Results.Ok(tx);
        }).RequirePermission(Perms.RelatedPartyRead);

        g.MapPost("/transactions", async (SaveRelatedPartyTransactionRequest req, RelatedPartyService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateTransactionAsync(null, req, ct);
            return Results.Created($"/api/related-party/transactions/{created.Id}", created);
        }).RequirePermission(Perms.RelatedPartyManage);

        g.MapPut("/transactions/{id:guid}", async (Guid id, SaveRelatedPartyTransactionRequest req, RelatedPartyService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateTransactionAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.RelatedPartyManage);

        g.MapDelete("/transactions/{id:guid}", async (Guid id, RelatedPartyService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteTransactionAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.RelatedPartyManage);

        g.MapPost("/transactions/{id:guid}/approve", async (Guid id, ApproveTransactionRequest req, RelatedPartyService svc, CancellationToken ct) =>
        {
            var result = await svc.ApproveTransactionAsync(id, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.RelatedPartyApprove);

        // 4. Omnibus Approvals
        g.MapGet("/omnibus", async (Guid? clientId, string? fy, RelatedPartyService svc, CancellationToken ct) =>
        {
            var list = await svc.ListOmnibusApprovalsAsync(clientId, fy, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.RelatedPartyRead);

        g.MapPost("/omnibus", async (SaveOmnibusApprovalRequest req, RelatedPartyService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateOmnibusApprovalAsync(null, req, ct);
            return Results.Created($"/api/related-party/omnibus/{created.Id}", created);
        }).RequirePermission(Perms.RelatedPartyManage);

        g.MapPut("/omnibus/{id:guid}", async (Guid id, SaveOmnibusApprovalRequest req, RelatedPartyService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateOmnibusApprovalAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.RelatedPartyManage);

        g.MapDelete("/omnibus/{id:guid}", async (Guid id, RelatedPartyService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteOmnibusApprovalAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.RelatedPartyManage);

        // 5. Reports (Form MBP-4 Register & AS 18 Disclosure Note)
        g.MapGet("/reports", async (Guid clientId, string fy, RelatedPartyService svc, CancellationToken ct) =>
        {
            var reports = await svc.GenerateReportsAsync(clientId, fy, ct);
            return Results.Ok(reports);
        }).RequirePermission(Perms.RelatedPartyRead);

        return app;
    }
}
