using System;
using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.BranchAudit;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class BranchAuditEndpoints
{
    public static IEndpointRouteBuilder MapBranchAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/branchaudit").RequirePlanDesk("branchaudit").WithTags("Multi-Firm Collaborative Audit & Branch Audit Consolidation (Sec 143(8) / SA 600)");

        // 1. KPI Summary
        g.MapGet("/summary", async (BranchAuditService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.BranchAuditRead);

        // 2. List engagements
        g.MapGet("/engagements", async (
            Guid? clientId,
            string? fy,
            string? status,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListEngagementsAsync(clientId, fy, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.BranchAuditRead);

        // 3. Get engagement by ID
        g.MapGet("/engagements/{id:guid}", async (Guid id, BranchAuditService svc, CancellationToken ct) =>
        {
            var engagement = await svc.GetEngagementByIdAsync(id, ct);
            return Results.Ok(engagement);
        }).RequirePermission(Perms.BranchAuditRead);

        // 4. Create engagement
        g.MapPost("/engagements", async (
            SaveBranchEngagementRequest req,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateEngagementAsync(null, req, ct);
            return Results.Created($"/api/branchaudit/engagements/{created.Id}", created);
        }).RequirePermission(Perms.BranchAuditManage);

        // 5. Update engagement
        g.MapPut("/engagements/{id:guid}", async (
            Guid id,
            SaveBranchEngagementRequest req,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateEngagementAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.BranchAuditManage);

        // 6. Delete engagement
        g.MapDelete("/engagements/{id:guid}", async (
            Guid id,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            await svc.DeleteEngagementAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.BranchAuditManage);

        // 7. Add branch office
        g.MapPost("/engagements/{id:guid}/branches", async (
            Guid id,
            SaveBranchOfficeRequest req,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var created = await svc.AddOrUpdateBranchOfficeAsync(id, null, req, ct);
            return Results.Created($"/api/branchaudit/engagements/{id}/branches/{created.Id}", created);
        }).RequirePermission(Perms.BranchAuditManage);

        // 8. Update branch office
        g.MapPut("/engagements/{id:guid}/branches/{branchId:guid}", async (
            Guid id,
            Guid branchId,
            SaveBranchOfficeRequest req,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.AddOrUpdateBranchOfficeAsync(id, branchId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.BranchAuditManage);

        // 9. Delete branch office
        g.MapDelete("/engagements/{id:guid}/branches/{branchId:guid}", async (
            Guid id,
            Guid branchId,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            await svc.DeleteBranchOfficeAsync(id, branchId, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.BranchAuditManage);

        // 10. Generate branch access token for external auditor
        g.MapPost("/engagements/{id:guid}/branches/{branchId:guid}/generate-token", async (
            Guid id,
            Guid branchId,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var res = await svc.GenerateBranchAccessTokenAsync(id, branchId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.BranchAuditManage);

        // 11. External Branch Auditor Portal - Fetch Branch info
        var portal = app.MapGroup("/api/branchaudit/portal").WithTags("External Branch Auditor Portal").AllowAnonymous();

        portal.MapGet("/branch", async (
            string token,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var office = await svc.GetBranchPortalInfoByTokenAsync(token, ct);
            return Results.Ok(office);
        });

        // 12. External Branch Auditor Portal - Submit Returns
        portal.MapPost("/submit", async (
            SubmitBranchReturnsRequest req,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var submitted = await svc.SubmitBranchReturnsExternalAsync(req, ct);
            return Results.Ok(submitted);
        });

        // 13. Review Branch Office (Accept/Reject)
        g.MapPost("/engagements/{id:guid}/branches/{branchId:guid}/review", async (
            Guid id,
            Guid branchId,
            ReviewBranchOfficeRequest req,
            ClaimsPrincipal user,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var reviewed = await svc.ReviewBranchOfficeAsync(id, branchId, req, userId, ct);
            return Results.Ok(reviewed);
        }).RequirePermission(Perms.BranchAuditManage);

        // 14. Consolidate Engagement
        g.MapPost("/engagements/{id:guid}/consolidate", async (
            Guid id,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var consolidated = await svc.ConsolidateEngagementAsync(id, ct);
            return Results.Ok(consolidated);
        }).RequirePermission(Perms.BranchAuditConsolidate);

        // 15. Finalize Engagement
        g.MapPost("/engagements/{id:guid}/finalize", async (
            Guid id,
            ClaimsPrincipal user,
            BranchAuditService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var finalized = await svc.FinalizeEngagementAsync(id, userId, ct);
            return Results.Ok(finalized);
        }).RequirePermission(Perms.BranchAuditConsolidate);

        return app;
    }
}
