using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Audit;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class StatutoryAuditEndpoints
{
    public static IEndpointRouteBuilder MapStatutoryAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/audit").WithTags("Statutory Audit & Working Papers");

        // KPI Summary
        g.MapGet("/summary", async (AuditService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.AuditRead);

        // List engagements
        g.MapGet("/engagements", async (Guid? clientId, string? financialYear, string? status, AuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListEngagementsAsync(clientId, financialYear, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AuditRead);

        // Get single engagement
        g.MapGet("/engagements/{id:guid}", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var eng = await svc.GetEngagementByIdAsync(id, ct);
            return Results.Ok(eng);
        }).RequirePermission(Perms.AuditRead);

        // Create engagement
        g.MapPost("/engagements", async (CreateAuditEngagementRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eng = await svc.CreateEngagementAsync(req, userId, ct);
            return Results.Created($"/api/audit/engagements/{eng.Id}", eng);
        }).RequirePermission(Perms.AuditManage);

        // Update materiality
        g.MapPut("/engagements/{id:guid}/materiality", async (Guid id, UpdateMaterialityRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eng = await svc.UpdateMaterialityAsync(id, req, userId, ct);
            return Results.Ok(eng);
        }).RequirePermission(Perms.AuditManage);

        // Update engagement status & audit opinion
        g.MapPut("/engagements/{id:guid}/status", async (Guid id, UpdateEngagementStatusRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eng = await svc.UpdateStatusAsync(id, req, userId, ct);
            return Results.Ok(eng);
        }).RequirePermission(Perms.AuditReview);

        // Delete engagement
        g.MapDelete("/engagements/{id:guid}", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            return await svc.DeleteEngagementAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.AuditManage);

        // List working papers
        g.MapGet("/engagements/{id:guid}/working-papers", async (Guid id, string? area, string? status, AuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListWorkingPapersAsync(id, area, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AuditRead);

        // Get working paper
        g.MapGet("/working-papers/{id:guid}", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var wp = await svc.GetWorkingPaperByIdAsync(id, ct);
            return Results.Ok(wp);
        }).RequirePermission(Perms.AuditRead);

        // Create working paper
        g.MapPost("/engagements/{id:guid}/working-papers", async (Guid id, SaveWorkingPaperRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var wp = await svc.CreateWorkingPaperAsync(id, req, userId, ct);
            return Results.Created($"/api/audit/working-papers/{wp.Id}", wp);
        }).RequirePermission(Perms.AuditManage);

        // Update working paper
        g.MapPut("/working-papers/{id:guid}", async (Guid id, SaveWorkingPaperRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var wp = await svc.UpdateWorkingPaperAsync(id, req, userId, ct);
            return Results.Ok(wp);
        }).RequirePermission(Perms.AuditManage);

        // Review working paper
        g.MapPut("/working-papers/{id:guid}/review", async (Guid id, ReviewWorkingPaperRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var wp = await svc.ReviewWorkingPaperAsync(id, req, userId, ct);
            return Results.Ok(wp);
        }).RequirePermission(Perms.AuditReview);

        // Delete working paper
        g.MapDelete("/working-papers/{id:guid}", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            return await svc.DeleteWorkingPaperAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.AuditManage);

        // Sign off audit report (SA 700 / SA 220)
        g.MapPost("/engagements/{id:guid}/signoff", async (Guid id, SignOffAuditReportRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eng = await svc.SignOffAuditAsync(id, req, userId, ct);
            return Results.Ok(eng);
        }).RequirePermission(Perms.AuditReview);

        // Lock & archive engagement file (SQC 1 Para 74 60-day limit)
        g.MapPost("/engagements/{id:guid}/archive", async (Guid id, ArchiveEngagementRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eng = await svc.ArchiveEngagementAsync(id, req, userId, ct);
            return Results.Ok(eng);
        }).RequirePermission(Perms.AuditArchive);

        // EQR Reviews (SA 220)
        g.MapGet("/engagements/{id:guid}/eqr", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var eqr = await svc.GetEqrReviewAsync(id, ct);
            return eqr is not null ? Results.Ok(eqr) : Results.NotFound();
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/engagements/{id:guid}/eqr/assign", async (Guid id, CreateEqrReviewRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eqr = await svc.AssignEqrReviewAsync(id, req, userId, ct);
            return Results.Created($"/api/audit/eqr/{eqr.Id}", eqr);
        }).RequirePermission(Perms.AuditManage);

        g.MapPut("/eqr/{id:guid}", async (Guid id, UpdateEqrReviewRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eqr = await svc.UpdateEqrReviewAsync(id, req, userId, ct);
            return Results.Ok(eqr);
        }).RequirePermission(Perms.EqrReview);

        g.MapPost("/eqr/{id:guid}/clear", async (Guid id, ClearEqrReviewRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var eqr = await svc.ClearEqrReviewAsync(id, req, userId, ct);
            return Results.Ok(eqr);
        }).RequirePermission(Perms.EqrReview);

        // Review Notes & Query Resolution Workflow
        g.MapGet("/engagements/{id:guid}/review-notes", async (Guid id, Guid? workingPaperId, string? status, string? priority, AuditService svc, CancellationToken ct) =>
        {
            var list = await svc.ListReviewNotesAsync(id, workingPaperId, status, priority, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/engagements/{id:guid}/review-notes", async (Guid id, CreateReviewNoteRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var note = await svc.AddReviewNoteAsync(id, req, userId, ct);
            return Results.Created($"/api/audit/review-notes/{note.Id}", note);
        }).RequirePermission(Perms.WorkpapersManage);

        g.MapPost("/review-notes/{id:guid}/respond", async (Guid id, RespondReviewNoteRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var note = await svc.RespondReviewNoteAsync(id, req, userId, ct);
            return Results.Ok(note);
        }).RequirePermission(Perms.WorkpapersManage);

        g.MapPost("/review-notes/{id:guid}/clear", async (Guid id, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var note = await svc.ClearReviewNoteAsync(id, userId, ct);
            return Results.Ok(note);
        }).RequirePermission(Perms.AuditReview);

        g.MapPost("/review-notes/{id:guid}/reopen", async (Guid id, ReopenReviewNoteRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var note = await svc.ReopenReviewNoteAsync(id, req, userId, ct);
            return Results.Ok(note);
        }).RequirePermission(Perms.AuditReview);

        // Standard SA pre-issuance checklist template
        g.MapGet("/checklist-template", () =>
        {
            return Results.Ok(AuditService.GetChecklistTemplate());
        }).RequirePermission(Perms.AuditRead);

        return app;
    }
}
