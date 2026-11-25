using System;
using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Schedule3;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class Schedule3Endpoints
{
    public static IEndpointRouteBuilder MapSchedule3Endpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/schedule3").RequirePlanDesk("schedule3").WithTags("Schedule III 11 Analytical Ratios & Disclosures");

        // KPI Summary
        g.MapGet("/summary", async (Schedule3Service svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.Schedule3Read);

        // List assessments
        g.MapGet("/assessments", async (
            Guid? clientId,
            string? fy,
            string? status,
            Schedule3Service svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListAssessmentsAsync(clientId, fy, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.Schedule3Read);

        // Get assessment by ID
        g.MapGet("/assessments/{id:guid}", async (Guid id, Schedule3Service svc, CancellationToken ct) =>
        {
            var assessment = await svc.GetAssessmentByIdAsync(id, ct);
            return Results.Ok(assessment);
        }).RequirePermission(Perms.Schedule3Read);

        // Create new assessment
        g.MapPost("/assessments", async (
            SaveSchedule3AssessmentRequest req,
            Schedule3Service svc,
            CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateAssessmentAsync(null, req, ct);
            return Results.Created($"/api/schedule3/assessments/{created.Id}", created);
        }).RequirePermission(Perms.Schedule3Manage);

        // Update assessment particulars & metrics
        g.MapPut("/assessments/{id:guid}", async (
            Guid id,
            SaveSchedule3AssessmentRequest req,
            Schedule3Service svc,
            CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateAssessmentAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.Schedule3Manage);

        // Update ratio explanation
        g.MapPut("/assessments/{assessmentId:guid}/ratios/{ratioId:guid}", async (
            Guid assessmentId,
            Guid ratioId,
            UpdateRatioExplanationRequest req,
            Schedule3Service svc,
            CancellationToken ct) =>
        {
            var updated = await svc.UpdateRatioExplanationAsync(assessmentId, ratioId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.Schedule3Manage);

        // Update ageing item
        g.MapPut("/assessments/{assessmentId:guid}/ageing/{ageingId:guid}", async (
            Guid assessmentId,
            Guid ageingId,
            UpdateAgeingItemRequest req,
            Schedule3Service svc,
            CancellationToken ct) =>
        {
            var updated = await svc.UpdateAgeingItemAsync(assessmentId, ageingId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.Schedule3Manage);

        // Generate disclosure note draft
        g.MapPost("/assessments/{id:guid}/generate-note", async (
            Guid id,
            Schedule3Service svc,
            CancellationToken ct) =>
        {
            var noteText = await svc.GenerateDisclosureNoteAsync(id, ct);
            return Results.Ok(new { DisclosureNoteText = noteText });
        }).RequirePermission(Perms.Schedule3Manage);

        // Finalize assessment
        g.MapPost("/assessments/{id:guid}/finalize", async (
            Guid id,
            FinalizeSchedule3Request? req,
            Schedule3Service svc,
            CancellationToken ct) =>
        {
            var finalized = await svc.FinalizeAssessmentAsync(id, req?.AuditorRemarks, ct);
            return Results.Ok(finalized);
        }).RequirePermission(Perms.Schedule3Finalize);

        return app;
    }
}

public record FinalizeSchedule3Request(string? AuditorRemarks);
