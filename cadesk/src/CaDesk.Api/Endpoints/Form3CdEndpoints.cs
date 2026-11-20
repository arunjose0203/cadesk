using System;
using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.TaxAudit;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class Form3CdEndpoints
{
    public static IEndpointRouteBuilder MapForm3CdEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/taxaudit").WithTags("Form 3CD Tax Audit Statement & Disclosures Desk");

        // KPI Summary
        g.MapGet("/summary", async (Form3CdService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.TaxAuditRead);

        // Standard 44 Clause Templates
        g.MapGet("/clauses-template", (Form3CdService svc) =>
        {
            var templates = svc.GetStandardClausesTemplate();
            return Results.Ok(templates);
        }).RequirePermission(Perms.TaxAuditRead);

        // Applicability Evaluator
        g.MapPost("/evaluate-applicability", (EvaluateApplicabilityRequest req, Form3CdService svc) =>
        {
            var eval = svc.EvaluateApplicability(req);
            return Results.Ok(eval);
        }).RequirePermission(Perms.TaxAuditRead);

        // List assessments
        g.MapGet("/assessments", async (
            Guid? clientId,
            string? fy,
            string? status,
            Form3CdService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListAssessmentsAsync(clientId, fy, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TaxAuditRead);

        // Get assessment by ID
        g.MapGet("/assessments/{id:guid}", async (Guid id, Form3CdService svc, CancellationToken ct) =>
        {
            var assessment = await svc.GetAssessmentByIdAsync(id, ct);
            return Results.Ok(assessment);
        }).RequirePermission(Perms.TaxAuditRead);

        // Create new assessment
        g.MapPost("/assessments", async (
            SaveForm3CdAssessmentRequest req,
            Form3CdService svc,
            CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateAssessmentAsync(null, req, ct);
            return Results.Created($"/api/taxaudit/assessments/{created.Id}", created);
        }).RequirePermission(Perms.TaxAuditManage);

        // Update assessment particulars
        g.MapPut("/assessments/{id:guid}", async (
            Guid id,
            SaveForm3CdAssessmentRequest req,
            Form3CdService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateAssessmentAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.TaxAuditManage);

        // Update clause response
        g.MapPut("/assessments/{assessmentId:guid}/clauses/{clauseId:guid}", async (
            Guid assessmentId,
            Guid clauseId,
            UpdateForm3CdClauseRequest req,
            Form3CdService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.UpdateClauseResponseAsync(assessmentId, clauseId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.TaxAuditManage);

        // Generate report draft text
        g.MapPost("/assessments/{id:guid}/generate-report", async (
            Guid id,
            Form3CdService svc,
            CancellationToken ct) =>
        {
            var reportText = await svc.GenerateReportTextAsync(id, ct);
            return Results.Ok(new { ReportText = reportText });
        }).RequirePermission(Perms.TaxAuditManage);

        // Finalize assessment
        g.MapPost("/assessments/{id:guid}/finalize", async (
            Guid id,
            FinalizeForm3CdRequest? req,
            Form3CdService svc,
            CancellationToken ct) =>
        {
            var finalized = await svc.FinalizeAssessmentAsync(id, req?.AuditorObservations, ct);
            return Results.Ok(finalized);
        }).RequirePermission(Perms.TaxAuditFinalize);

        return app;
    }
}

public record FinalizeForm3CdRequest(string? AuditorObservations);
