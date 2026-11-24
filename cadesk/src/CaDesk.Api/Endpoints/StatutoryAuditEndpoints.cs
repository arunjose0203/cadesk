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
        }).RequirePermission(Perms.AuditSignoff);

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

        // SA 230 Lead Schedules (A to Q) & Trial Balance Ingestion
        g.MapGet("/engagements/{id:guid}/lead-schedules", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var list = await svc.GetLeadSchedulesAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/engagements/{id:guid}/lead-schedules/generate", async (Guid id, GenerateLeadSchedulesRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var list = await svc.GenerateLeadSchedulesFromTrialBalanceAsync(id, req.TrialBalanceImportId, userId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AuditManage);

        g.MapPut("/lead-schedules/{id:guid}", async (Guid id, UpdateLeadScheduleRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var item = await svc.UpdateLeadScheduleAsync(id, req, userId, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.AuditManage);

        // SA 230 / SQC 1 Digital Audit Dossier & NFRA Defense Vault
        g.MapGet("/engagements/{id:guid}/dossier", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var dossier = await svc.GetDossierAsync(id, ct);
            return dossier is not null ? Results.Ok(dossier) : Results.NotFound();
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/engagements/{id:guid}/dossier/compile", async (Guid id, CompileDossierRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var dossier = await svc.CompileDossierAsync(id, req, userId, ct);
            return Results.Ok(dossier);
        }).RequirePermission(Perms.AuditManage);

        g.MapPost("/dossier/{id:guid}/seal", async (Guid id, SealDossierRequest req, ClaimsPrincipal user, AuditService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var dossier = await svc.SealDossierAsync(id, req, userId, ct);
            return Results.Ok(dossier);
        }).RequirePermission(Perms.AuditReview);

        g.MapGet("/engagements/{id:guid}/dossier/export-html", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var html = await svc.ExportDossierHtmlAsync(id, ct);
            return Results.Ok(html);
        }).RequirePermission(Perms.AuditRead);

        // =========================================================================
        // Sprint 45: SA 505 Automated External Confirmations Endpoints
        // =========================================================================

        g.MapGet("/engagements/{id:guid}/confirmation-campaigns", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var campaigns = await svc.GetConfirmationCampaignsAsync(id, ct);
            return Results.Ok(campaigns);
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/engagements/{id:guid}/confirmation-campaigns", async (
            Guid id,
            CreateConfirmationCampaignRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var campaign = await svc.CreateConfirmationCampaignAsync(id, req, userId, ct);
            return Results.Created($"/api/audit/confirmation-campaigns/{campaign.Id}", campaign);
        }).RequirePermission(Perms.AuditManage);

        g.MapGet("/confirmation-campaigns/{campaignId:guid}/requests", async (Guid campaignId, AuditService svc, CancellationToken ct) =>
        {
            var requests = await svc.GetConfirmationRequestsAsync(campaignId, ct);
            return Results.Ok(requests);
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/confirmation-campaigns/{campaignId:guid}/requests", async (
            Guid campaignId,
            CreateConfirmationRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var item = await svc.CreateConfirmationRequestAsync(campaignId, req, userId, ct);
            return Results.Created($"/api/audit/confirmations/{item.Id}", item);
        }).RequirePermission(Perms.AuditManage);

        g.MapPost("/confirmation-campaigns/{campaignId:guid}/auto-populate", async (
            Guid campaignId,
            AutoPopulateConfirmationsRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var items = await svc.AutoPopulateConfirmationsFromTrialBalanceAsync(campaignId, req, userId, ct);
            return Results.Ok(items);
        }).RequirePermission(Perms.AuditManage);

        g.MapPost("/confirmation-campaigns/{campaignId:guid}/send", async (
            Guid campaignId,
            List<Guid>? requestIds,
            AuditService svc,
            CancellationToken ct) =>
        {
            var count = await svc.SendConfirmationRequestsAsync(campaignId, requestIds, ct);
            return Results.Ok(new { sentCount = count });
        }).RequirePermission(Perms.AuditManage);

        g.MapPut("/confirmations/{requestId:guid}/evaluate", async (
            Guid requestId,
            UpdateConfirmationAuditorEvaluationRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var item = await svc.UpdateConfirmationAuditorEvaluationAsync(requestId, req, userId, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.AuditManage);

        g.MapGet("/confirmation-campaigns/{campaignId:guid}/export-summary", async (Guid campaignId, AuditService svc, CancellationToken ct) =>
        {
            var summary = await svc.ExportConfirmationSummaryHtmlAsync(campaignId, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.AuditRead);

        g.MapGet("/confirmations/{requestId:guid}/letter-html", async (Guid requestId, AuditService svc, CancellationToken ct) =>
        {
            var html = await svc.ExportConfirmationLetterHtmlAsync(requestId, ct);
            return Results.Content(html, "text/html");
        }).RequirePermission(Perms.AuditRead);

        // Anonymous Public Confirmation Portal Endpoints
        app.MapGet("/api/audit/confirmations/public/{token}", async (string token, AuditService svc, CancellationToken ct) =>
        {
            var details = await svc.GetPublicConfirmationByTokenAsync(token, ct);
            return Results.Ok(details);
        }).AllowAnonymous();

        app.MapPost("/api/audit/confirmations/public/{token}/respond", async (
            string token,
            SubmitConfirmationResponseRequest req,
            AuditService svc,
            CancellationToken ct) =>
        {
            var res = await svc.SubmitPublicConfirmationResponseAsync(token, req, ct);
            return Results.Ok(res);
        }).AllowAnonymous();

        // =========================================================================
        // Sprint 46: 1-Click ICAI Independent Auditor's Report & Notes Endpoints
        // =========================================================================

        g.MapGet("/engagements/{id:guid}/auditor-report", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var report = await svc.GetAuditorReportAsync(id, ct);
            return report == null ? Results.NotFound() : Results.Ok(report);
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/engagements/{id:guid}/auditor-report/generate", async (
            Guid id,
            GenerateAuditorReportRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var report = await svc.GenerateAuditorReportAsync(id, req, userId, ct);
            return Results.Ok(report);
        }).RequirePermission(Perms.AuditManage);

        g.MapPut("/auditor-reports/{id:guid}", async (
            Guid id,
            UpdateAuditorReportRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var report = await svc.UpdateAuditorReportAsync(id, req, userId, ct);
            return Results.Ok(report);
        }).RequirePermission(Perms.AuditManage);

        g.MapPost("/auditor-reports/{id:guid}/issue-udin", async (
            Guid id,
            IssueReportWithUdinRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var report = await svc.IssueReportWithUdinAsync(id, req, userId, ct);
            return Results.Ok(report);
        }).RequirePermission(Perms.AuditReview);

        g.MapGet("/engagements/{id:guid}/auditor-report/export-html", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var export = await svc.ExportAuditorReportHtmlAsync(id, ct);
            return Results.Ok(export);
        }).RequirePermission(Perms.AuditRead);

        g.MapGet("/engagements/{id:guid}/notes-to-accounts", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var notes = await svc.GetNotesToAccountsAsync(id, ct);
            return notes == null ? Results.NotFound() : Results.Ok(notes);
        }).RequirePermission(Perms.AuditRead);

        g.MapPost("/engagements/{id:guid}/notes-to-accounts/generate", async (
            Guid id,
            GenerateNotesToAccountsRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notes = await svc.GenerateNotesToAccountsAsync(id, req, userId, ct);
            return Results.Ok(notes);
        }).RequirePermission(Perms.AuditManage);

        g.MapPut("/notes-to-accounts/{id:guid}", async (
            Guid id,
            UpdateNotesToAccountsRequest req,
            ClaimsPrincipal user,
            AuditService svc,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notes = await svc.UpdateNotesToAccountsAsync(id, req, userId, ct);
            return Results.Ok(notes);
        }).RequirePermission(Perms.AuditManage);

        g.MapGet("/engagements/{id:guid}/notes-to-accounts/export-html", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            var export = await svc.ExportNotesToAccountsHtmlAsync(id, ct);
            return Results.Ok(export);
        }).RequirePermission(Perms.AuditRead);

        return app;
    }
}
