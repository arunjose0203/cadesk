using System;
using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.ForeignRemittance;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class ForeignRemittanceEndpoints
{
    public static IEndpointRouteBuilder MapForeignRemittanceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/foreign-remittance").RequirePlanDesk("foreignremittance").WithTags("Foreign Remittance & Form 15CA / 15CB Certification (Rule 37BB / Section 195 / DTAA)");

        // 1. Summary
        g.MapGet("/summary", async (string? fy, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(fy, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        // 2. List assessments
        g.MapGet("/assessments", async (
            Guid? clientId,
            string? fy,
            string? status,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var list = await svc.GetAssessmentsAsync(clientId, fy, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        // 3. Get assessment by ID
        g.MapGet("/assessments/{id:guid}", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var assessment = await svc.GetAssessmentByIdAsync(id, ct);
            return assessment is null ? Results.NotFound() : Results.Ok(assessment);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        // 4. Create assessment
        g.MapPost("/assessments", async (
            CreateForeignRemittanceRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var created = await svc.CreateAssessmentAsync(req, ct);
            return Results.Created($"/api/foreign-remittance/assessments/{created.Id}", created);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        // 5. Update assessment
        g.MapPut("/assessments/{id:guid}", async (
            Guid id,
            UpdateForeignRemittanceRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.UpdateAssessmentAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        // 6. Delete assessment
        g.MapDelete("/assessments/{id:guid}", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteAssessmentAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.ForeignRemittanceManage);

        // 7. Update checklist item
        g.MapPut("/assessments/{id:guid}/checklist/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            UpdateChecklistItemRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var item = await svc.UpdateChecklistItemAsync(id, itemId, req, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        // 8. Re-generate statutory artifacts
        g.MapPost("/assessments/{id:guid}/generate-artifacts", async (
            Guid id,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var assessment = await svc.GenerateArtifactsAsync(id, ct);
            return Results.Ok(assessment);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        // 9. Certify Form 15CB (requires foreignremittance.certify)
        g.MapPost("/assessments/{id:guid}/certify", async (
            Guid id,
            CertifyRemittanceRequest req,
            ClaimsPrincipal user,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            Guid? userId = user.UserId();
            var result = await svc.CertifyRemittanceAsync(id, userId, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.ForeignRemittanceCertify);

        // 10. Record filing on income tax portal
        g.MapPost("/assessments/{id:guid}/file", async (
            Guid id,
            FileRemittanceRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var result = await svc.FileRemittanceAsync(id, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        // =====================================================================
        // SPRINT 62: RBI FEMA FIRMS (FC-GPR / FC-TRS) ENDPOINTS
        // =====================================================================

        g.MapGet("/firms/summary", async (ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetFirmsSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        g.MapGet("/firms/filings", async (
            Guid? clientId,
            CaDesk.Domain.ForeignRemittance.FirmsFilingType? type,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var list = await svc.GetFirmsFilingsAsync(clientId, type, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        g.MapGet("/firms/filings/{id:guid}", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var filing = await svc.GetFirmsFilingByIdAsync(id, ct);
            return Results.Ok(filing);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        g.MapPost("/firms/filings", async (
            SaveFemaFirmsFilingRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var saved = await svc.SaveFirmsFilingAsync(req, ct);
            return Results.Ok(saved);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        g.MapPost("/firms/filings/{id:guid}/certify", async (
            Guid id,
            CertifyFemaFirmsRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var certified = await svc.CertifyFirmsFilingAsync(id, req, ct);
            return Results.Ok(certified);
        }).RequirePermission(Perms.ForeignRemittanceCertify);

        g.MapDelete("/firms/filings/{id:guid}", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            await svc.DeleteFirmsFilingAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.ForeignRemittanceManage);

        g.MapGet("/firms/filings/{id:guid}/certificate-html", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var html = await svc.GetFirmsComplianceCertificateHtmlAsync(id, ct);
            return Results.Content(html, "text/html");
        }).RequirePermission(Perms.ForeignRemittanceRead);

        // =====================================================================
        // SPRINT 62: RBI ANNUAL FLA RETURN ENDPOINTS
        // =====================================================================

        g.MapGet("/fla/summary", async (string? fy, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetFlaSummaryAsync(fy, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        g.MapGet("/fla/returns", async (
            Guid? clientId,
            string? fy,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var list = await svc.GetFlaReturnsAsync(clientId, fy, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        g.MapGet("/fla/returns/{id:guid}", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var item = await svc.GetFlaReturnByIdAsync(id, ct);
            return Results.Ok(item);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        g.MapPost("/fla/returns", async (
            SaveRbiAnnualFlaReturnRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var saved = await svc.SaveFlaReturnAsync(req, ct);
            return Results.Ok(saved);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        g.MapPost("/fla/returns/{id:guid}/certify", async (
            Guid id,
            CertifyRbiFlaRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var certified = await svc.CertifyFlaReturnAsync(id, req, ct);
            return Results.Ok(certified);
        }).RequirePermission(Perms.ForeignRemittanceCertify);

        g.MapPost("/fla/returns/{id:guid}/file", async (
            Guid id,
            FileRbiFlaRequest req,
            ForeignRemittanceService svc,
            CancellationToken ct) =>
        {
            var filed = await svc.FileFlaReturnAsync(id, req, ct);
            return Results.Ok(filed);
        }).RequirePermission(Perms.ForeignRemittanceManage);

        g.MapDelete("/fla/returns/{id:guid}", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            await svc.DeleteFlaReturnAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.ForeignRemittanceManage);

        g.MapGet("/fla/returns/{id:guid}/report-html", async (Guid id, ForeignRemittanceService svc, CancellationToken ct) =>
        {
            var html = await svc.GetFlaReturnHtmlAsync(id, ct);
            return Results.Content(html, "text/html");
        }).RequirePermission(Perms.ForeignRemittanceRead);

        // =====================================================================
        // SPRINT 62: DTAA MFN CLAUSE & PE RISK EVALUATION ENDPOINTS
        // =====================================================================

        g.MapPost("/evaluate-mfn", (DtaaMfnEvaluationRequest req, ForeignRemittanceService svc) =>
        {
            var res = svc.EvaluateDtaaMfn(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        g.MapPost("/evaluate-pe", (PeExposureEvaluationRequest req, ForeignRemittanceService svc) =>
        {
            var res = svc.EvaluatePeRisk(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.ForeignRemittanceRead);

        return app;
    }
}
