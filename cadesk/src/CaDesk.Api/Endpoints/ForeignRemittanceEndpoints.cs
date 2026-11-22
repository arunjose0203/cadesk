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
        var g = app.MapGroup("/api/foreign-remittance").WithTags("Foreign Remittance & Form 15CA / 15CB Certification (Rule 37BB / Section 195 / DTAA)");

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

        return app;
    }
}
