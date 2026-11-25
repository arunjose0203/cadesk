using System;
using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.AuditCommittee;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class AuditCommitteeEndpoints
{
    public static IEndpointRouteBuilder MapAuditCommitteeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auditcommittee").RequirePlanDesk("auditcommittee").WithTags("Audit Committee & Board Governance Desk (ICAI SA 260 / SA 265 / Companies Act Sec 177)");

        // 1. Summary
        g.MapGet("/summary", async (AuditCommitteeService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.AuditCommitteeRead);

        // 2. List engagements
        g.MapGet("/engagements", async (
            Guid? clientId,
            string? fy,
            string? status,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListEngagementsAsync(clientId, fy, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AuditCommitteeRead);

        // 3. Get engagement by ID
        g.MapGet("/engagements/{id:guid}", async (Guid id, AuditCommitteeService svc, CancellationToken ct) =>
        {
            var engagement = await svc.GetEngagementByIdAsync(id, ct);
            return Results.Ok(engagement);
        }).RequirePermission(Perms.AuditCommitteeRead);

        // 4. Create engagement
        g.MapPost("/engagements", async (
            SaveCommitteeEngagementRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateEngagementAsync(null, req, ct);
            return Results.Created($"/api/auditcommittee/engagements/{created.Id}", created);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 5. Update engagement
        g.MapPut("/engagements/{id:guid}", async (
            Guid id,
            SaveCommitteeEngagementRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateEngagementAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 6. Delete engagement
        g.MapDelete("/engagements/{id:guid}", async (
            Guid id,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            await svc.DeleteEngagementAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 7. Add deficiency (SA 265)
        g.MapPost("/engagements/{id:guid}/deficiencies", async (
            Guid id,
            SaveDeficiencyRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var created = await svc.AddOrUpdateDeficiencyAsync(id, null, req, ct);
            return Results.Created($"/api/auditcommittee/engagements/{id}/deficiencies/{created.Id}", created);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 8. Update deficiency
        g.MapPut("/engagements/{id:guid}/deficiencies/{deficiencyId:guid}", async (
            Guid id,
            Guid deficiencyId,
            SaveDeficiencyRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.AddOrUpdateDeficiencyAsync(id, deficiencyId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 9. Delete deficiency
        g.MapDelete("/engagements/{id:guid}/deficiencies/{deficiencyId:guid}", async (
            Guid id,
            Guid deficiencyId,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            await svc.DeleteDeficiencyAsync(id, deficiencyId, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 10. Add significant audit matter (SA 260)
        g.MapPost("/engagements/{id:guid}/matters", async (
            Guid id,
            SaveSignificantMatterRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var created = await svc.AddOrUpdateSignificantMatterAsync(id, null, req, ct);
            return Results.Created($"/api/auditcommittee/engagements/{id}/matters/{created.Id}", created);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 11. Update significant audit matter
        g.MapPut("/engagements/{id:guid}/matters/{matterId:guid}", async (
            Guid id,
            Guid matterId,
            SaveSignificantMatterRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.AddOrUpdateSignificantMatterAsync(id, matterId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 12. Delete significant audit matter
        g.MapDelete("/engagements/{id:guid}/matters/{matterId:guid}", async (
            Guid id,
            Guid matterId,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            await svc.DeleteSignificantMatterAsync(id, matterId, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 13. Add uncorrected misstatement (SA 450)
        g.MapPost("/engagements/{id:guid}/misstatements", async (
            Guid id,
            SaveUncorrectedMisstatementRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var created = await svc.AddOrUpdateMisstatementAsync(id, null, req, ct);
            return Results.Created($"/api/auditcommittee/engagements/{id}/misstatements/{created.Id}", created);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 14. Update uncorrected misstatement
        g.MapPut("/engagements/{id:guid}/misstatements/{misstatementId:guid}", async (
            Guid id,
            Guid misstatementId,
            SaveUncorrectedMisstatementRequest req,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.AddOrUpdateMisstatementAsync(id, misstatementId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 15. Delete uncorrected misstatement
        g.MapDelete("/engagements/{id:guid}/misstatements/{misstatementId:guid}", async (
            Guid id,
            Guid misstatementId,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            await svc.DeleteMisstatementAsync(id, misstatementId, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 16. Generate Governance Pack & SA 265 Letter
        g.MapPost("/engagements/{id:guid}/generate-pack", async (
            Guid id,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var generated = await svc.GenerateGovernancePackAsync(id, ct);
            return Results.Ok(generated);
        }).RequirePermission(Perms.AuditCommitteeManage);

        // 17. Formally Sign Off
        g.MapPost("/engagements/{id:guid}/signoff", async (
            Guid id,
            SignOffCommitteeEngagementRequest req,
            ClaimsPrincipal user,
            AuditCommitteeService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var signedOff = await svc.SignOffEngagementAsync(id, req, userId, ct);
            return Results.Ok(signedOff);
        }).RequirePermission(Perms.AuditCommitteeSignOff);

        return app;
    }
}
