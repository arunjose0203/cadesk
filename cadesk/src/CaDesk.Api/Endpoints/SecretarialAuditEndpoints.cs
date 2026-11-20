using System;
using System.Threading;
using CaDesk.Api.Auth;
using CaDesk.Application.SecretarialAudit;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class SecretarialAuditEndpoints
{
    public static IEndpointRouteBuilder MapSecretarialAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/secretarial-audit").WithTags("Secretarial Audit & Form MR-3 Compliance Engine (Companies Act Sec 204 / Rule 9)");

        // 1. Summary & Applicability
        g.MapGet("/summary", async (Guid? clientId, string? financialYear, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.SecretarialAuditRead);

        g.MapPost("/check-applicability", (ApplicabilityCheckRequest req, SecretarialAuditService svc) =>
        {
            var result = svc.CheckApplicability(req);
            return Results.Ok(result);
        }).RequirePermission(Perms.SecretarialAuditRead);

        // 2. Engagements CRUD & Certification
        g.MapGet("/engagements", async (Guid? clientId, string? financialYear, string? status, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListEngagementsAsync(clientId, financialYear, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.SecretarialAuditRead);

        g.MapGet("/engagements/{id:guid}", async (Guid id, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var item = await svc.GetEngagementByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.SecretarialAuditRead);

        g.MapPost("/engagements", async (SaveEngagementRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateEngagementAsync(null, req, ct);
            return Results.Created($"/api/secretarial-audit/engagements/{created.Id}", created);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapPut("/engagements/{id:guid}", async (Guid id, SaveEngagementRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateEngagementAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapDelete("/engagements/{id:guid}", async (Guid id, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteEngagementAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapPost("/engagements/{id:guid}/certify", async (Guid id, CertifyEngagementRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var certified = await svc.CertifyEngagementAsync(id, req, ct);
            return Results.Ok(certified);
        }).RequirePermission(Perms.SecretarialAuditCertify);

        g.MapPost("/engagements/{id:guid}/generate-mr3", async (Guid id, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var report = await svc.GenerateMR3ReportAsync(id, ct);
            return Results.Ok(new { ReportText = report });
        }).RequirePermission(Perms.SecretarialAuditRead);

        // 3. Checklist Items
        g.MapGet("/engagements/{id:guid}/checklist", async (Guid id, string? category, string? status, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListChecklistItemsAsync(id, category, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.SecretarialAuditRead);

        g.MapPost("/engagements/{id:guid}/checklist", async (Guid id, SaveChecklistItemRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateChecklistItemAsync(id, null, req, ct);
            return Results.Created($"/api/secretarial-audit/engagements/{id}/checklist/{created.Id}", created);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapPut("/engagements/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, SaveChecklistItemRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateChecklistItemAsync(id, itemId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapDelete("/engagements/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteChecklistItemAsync(id, itemId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapPost("/engagements/{id:guid}/checklist/batch", async (Guid id, BatchUpdateChecklistRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var count = await svc.BatchUpdateChecklistAsync(id, req, ct);
            return Results.Ok(new { UpdatedCount = count });
        }).RequirePermission(Perms.SecretarialAuditManage);

        // 4. MCA Form Filings
        g.MapGet("/engagements/{id:guid}/filings", async (Guid id, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListMcaFilingsAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.SecretarialAuditRead);

        g.MapPost("/engagements/{id:guid}/filings", async (Guid id, SaveMcaFilingRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateMcaFilingAsync(id, null, req, ct);
            return Results.Created($"/api/secretarial-audit/engagements/{id}/filings/{created.Id}", created);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapPut("/engagements/{id:guid}/filings/{filingId:guid}", async (Guid id, Guid filingId, SaveMcaFilingRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateMcaFilingAsync(id, filingId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapDelete("/engagements/{id:guid}/filings/{filingId:guid}", async (Guid id, Guid filingId, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteMcaFilingAsync(id, filingId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.SecretarialAuditManage);

        // 5. Board & Committee Meetings
        g.MapGet("/engagements/{id:guid}/meetings", async (Guid id, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListMeetingAuditsAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.SecretarialAuditRead);

        g.MapPost("/engagements/{id:guid}/meetings", async (Guid id, SaveBoardMeetingAuditRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateMeetingAuditAsync(id, null, req, ct);
            return Results.Created($"/api/secretarial-audit/engagements/{id}/meetings/{created.Id}", created);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapPut("/engagements/{id:guid}/meetings/{meetingId:guid}", async (Guid id, Guid meetingId, SaveBoardMeetingAuditRequest req, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateMeetingAuditAsync(id, meetingId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.SecretarialAuditManage);

        g.MapDelete("/engagements/{id:guid}/meetings/{meetingId:guid}", async (Guid id, Guid meetingId, SecretarialAuditService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteMeetingAuditAsync(id, meetingId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.SecretarialAuditManage);

        return app;
    }
}
