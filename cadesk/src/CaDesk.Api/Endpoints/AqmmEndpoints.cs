using CaDesk.Application.Aqmm;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class AqmmEndpoints
{
    public static IEndpointRouteBuilder MapAqmmEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/aqmm")
            .RequireAuthorization(Policies.FirmMember);

        // Get current firm's AQMM assessment summary
        group.MapGet("/current", async (AqmmService service, string? fy, CancellationToken ct) =>
        {
            var summary = await service.GetOrCreateAssessmentAsync(fy ?? "2024-25", ct);
            return Results.Ok(summary);
        });

        // Get all 21 questions with awarded points and criteria
        group.MapGet("/questions", async (AqmmService service, string? fy, CancellationToken ct) =>
        {
            var questions = await service.GetQuestionsAsync(fy ?? "2024-25", ct);
            return Results.Ok(questions);
        });

        // Update a single question's response (points, remarks, evidence)
        group.MapPost("/responses", async (AqmmService service, UpdateAqmmResponseRequest req, string? fy, CancellationToken ct) =>
        {
            var summary = await service.UpdateResponseAsync(fy ?? "2024-25", req, ct);
            return Results.Ok(summary);
        });

        // Auto-evaluate points using CA Desk live telemetry
        group.MapPost("/auto-score", async (AqmmService service, string? fy, CancellationToken ct) =>
        {
            var result = await service.AutoScoreFromSystemDataAsync(fy ?? "2024-25", ct);
            return Results.Ok(result);
        });

        // Get Gap Analysis and recommendations for Level 2 & 3
        group.MapGet("/gap-analysis", async (AqmmService service, string? fy, CancellationToken ct) =>
        {
            var gap = await service.GetGapAnalysisAsync(fy ?? "2024-25", ct);
            return Results.Ok(gap);
        });

        // Certify assessment with Lead Partner details and 18-digit UDIN
        group.MapPost("/certify", async (AqmmService service, CertifyAqmmAssessmentRequest req, string? fy, CancellationToken ct) =>
        {
            var summary = await service.CertifyAssessmentAsync(fy ?? "2024-25", req, ct);
            return Results.Ok(summary);
        });

        // Export official HTML Peer Review Dossier & Scorecard
        group.MapGet("/dossier", async (AqmmService service, string? fy, CancellationToken ct) =>
        {
            var html = await service.GenerateDossierHtmlAsync(fy ?? "2024-25", ct);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        return app;
    }
}
