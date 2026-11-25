using System;
using CaDesk.Api.Auth;
using CaDesk.Application.InternalAudit;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class InternalAuditEndpoints
{
    public static IEndpointRouteBuilder MapInternalAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/internal-audit").RequirePlanDesk("internalaudit").WithTags("Internal Audit Risk & Control Matrix (RCM) & IFC Testing Desk");

        // 1. Summary
        g.MapGet("/summary", async (Guid? clientId, string? financialYear, InternalAuditService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.InternalAuditRead);

        // 2. Engagements
        g.MapGet("/engagements", async (Guid? clientId, string? financialYear, string? status, InternalAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListEngagementsAsync(clientId, financialYear, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.InternalAuditRead);

        g.MapGet("/engagements/{id:guid}", async (Guid id, InternalAuditService svc, CancellationToken ct) =>
        {
            var eng = await svc.GetEngagementByIdAsync(id, ct);
            return eng is null ? Results.NotFound() : Results.Ok(eng);
        }).RequirePermission(Perms.InternalAuditRead);

        g.MapPost("/engagements", async (SaveInternalAuditEngagementRequest req, bool populateStandardRcm, InternalAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateEngagementAsync(null, req, populateStandardRcm, ct);
            return Results.Created($"/api/internal-audit/engagements/{created.Id}", created);
        }).RequirePermission(Perms.InternalAuditManage);

        g.MapPut("/engagements/{id:guid}", async (Guid id, SaveInternalAuditEngagementRequest req, InternalAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateEngagementAsync(id, req, false, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.InternalAuditManage);

        g.MapDelete("/engagements/{id:guid}", async (Guid id, InternalAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteEngagementAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.InternalAuditManage);

        // 3. RCM Controls
        g.MapGet("/engagements/{id:guid}/controls", async (Guid id, string? processCycle, InternalAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListControlsAsync(id, processCycle, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.InternalAuditRead);

        g.MapPost("/engagements/{id:guid}/controls", async (Guid id, SaveRcmControlItemRequest req, InternalAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateControlAsync(id, null, req, ct);
            return Results.Created($"/api/internal-audit/engagements/{id}/controls/{created.Id}", created);
        }).RequirePermission(Perms.InternalAuditManage);

        g.MapPut("/engagements/{id:guid}/controls/{controlId:guid}", async (Guid id, Guid controlId, SaveRcmControlItemRequest req, InternalAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateControlAsync(id, controlId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.InternalAuditManage);

        g.MapDelete("/engagements/{id:guid}/controls/{controlId:guid}", async (Guid id, Guid controlId, InternalAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteControlAsync(id, controlId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.InternalAuditManage);

        // 4. Findings & Remediation Tracker
        g.MapGet("/engagements/{id:guid}/findings", async (Guid id, string? severity, string? status, InternalAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListFindingsAsync(id, severity, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.InternalAuditRead);

        g.MapPost("/engagements/{id:guid}/findings", async (Guid id, SaveInternalAuditFindingRequest req, InternalAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateFindingAsync(id, null, req, ct);
            return Results.Created($"/api/internal-audit/engagements/{id}/findings/{created.Id}", created);
        }).RequirePermission(Perms.InternalAuditManage);

        g.MapPut("/engagements/{id:guid}/findings/{findingId:guid}", async (Guid id, Guid findingId, SaveInternalAuditFindingRequest req, InternalAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateFindingAsync(id, findingId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.InternalAuditManage);

        g.MapPatch("/engagements/{id:guid}/findings/{findingId:guid}/status", async (Guid id, Guid findingId, UpdateFindingStatusRequest req, InternalAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateFindingStatusAsync(id, findingId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.InternalAuditManage);

        g.MapDelete("/engagements/{id:guid}/findings/{findingId:guid}", async (Guid id, Guid findingId, InternalAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteFindingAsync(id, findingId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.InternalAuditManage);

        // 5. Executive Report Generation
        g.MapPost("/engagements/{id:guid}/generate-report", async (Guid id, InternalAuditService svc, CancellationToken ct) =>
        {
            var reported = await svc.GenerateReportAsync(id, ct);
            return Results.Ok(reported);
        }).RequirePermission(Perms.InternalAuditReport);

        return app;
    }
}
