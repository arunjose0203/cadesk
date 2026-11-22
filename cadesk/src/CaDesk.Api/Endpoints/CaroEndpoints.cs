using System;
using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Caro;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class CaroEndpoints
{
    public static IEndpointRouteBuilder MapCaroEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/caro").WithTags("CARO 2020 Automated Audit Checklist & Disclosures Generator");

        // KPI Summary
        g.MapGet("/summary", async (CaroService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.CaroRead);

        // Standard 21 Clause Templates
        g.MapGet("/clauses-template", (CaroService svc) =>
        {
            var templates = svc.GetClauseTemplates();
            return Results.Ok(templates);
        }).RequirePermission(Perms.CaroRead);

        // List assessments
        g.MapGet("/assessments", async (
            Guid? clientId,
            string? fy,
            string? status,
            CaroService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListAssessmentsAsync(clientId, fy, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.CaroRead);

        // Get assessment by ID
        g.MapGet("/assessments/{id:guid}", async (Guid id, CaroService svc, CancellationToken ct) =>
        {
            var assessment = await svc.GetAssessmentByIdAsync(id, ct);
            return Results.Ok(assessment);
        }).RequirePermission(Perms.CaroRead);

        // Create assessment
        g.MapPost("/assessments", async (
            SaveCaroAssessmentRequest req,
            ClaimsPrincipal user,
            CaroService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var created = await svc.CreateAssessmentAsync(req, userId, ct);
            return Results.Created($"/api/caro/assessments/{created.Id}", created);
        }).RequirePermission(Perms.CaroManage);

        // Update assessment
        g.MapPut("/assessments/{id:guid}", async (
            Guid id,
            SaveCaroAssessmentRequest req,
            ClaimsPrincipal user,
            CaroService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var updated = await svc.UpdateAssessmentAsync(id, req, userId, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.CaroManage);

        // Update individual clause response
        g.MapPut("/assessments/{id:guid}/clauses/{clauseId:guid}", async (
            Guid id,
            Guid clauseId,
            UpdateClauseResponseRequest req,
            ClaimsPrincipal user,
            CaroService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var updated = await svc.UpdateClauseResponseAsync(id, clauseId, req, userId, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.CaroManage);

        // Re-generate draft annexure
        g.MapPost("/assessments/{id:guid}/generate-annexure", async (Guid id, CaroService svc, CancellationToken ct) =>
        {
            var text = await svc.GenerateAnnexureAsync(id, ct);
            return Results.Ok(new { annexureText = text });
        }).RequirePermission(Perms.CaroManage);

        // Finalize assessment
        g.MapPost("/assessments/{id:guid}/finalize", async (
            Guid id,
            FinalizeCaroRequest? req,
            ClaimsPrincipal user,
            CaroService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var finalized = await svc.FinalizeAssessmentAsync(id, req?.Conclusion, userId, ct);
            return Results.Ok(finalized);
        }).RequirePermission(Perms.CaroFinalize);

        return app;
    }
}

public class FinalizeCaroRequest
{
    public string? Conclusion { get; set; }
}
