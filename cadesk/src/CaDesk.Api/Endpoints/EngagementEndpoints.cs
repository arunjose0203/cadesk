using CaDesk.Api.Auth;
using CaDesk.Application.Engagements;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class EngagementEndpoints
{
    public static IEndpointRouteBuilder MapEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        var gEng = app.MapGroup("/api/engagements").WithTags("Digital Engagement Letters (ICAI SA 210)");
        var gKyc = app.MapGroup("/api/kyc").WithTags("Client AML / PMLA KYC & UBO Compliance");

        // KPI Summary
        gEng.MapGet("/summary", async (IEngagementService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.EngagementRead);

        // Engagement Letters CRUD & Lifecycle
        gEng.MapGet("/", async (Guid? clientId, string? type, string? status, IEngagementService svc, CancellationToken ct) =>
        {
            var list = await svc.ListLettersAsync(clientId, type, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.EngagementRead);

        gEng.MapGet("/{id:guid}", async (Guid id, IEngagementService svc, CancellationToken ct) =>
        {
            var letter = await svc.GetLetterByIdAsync(id, ct);
            return letter is null ? Results.NotFound() : Results.Ok(letter);
        }).RequirePermission(Perms.EngagementRead);

        gEng.MapPost("/", async (CreateEngagementLetterRequest req, IEngagementService svc, CancellationToken ct) =>
        {
            var letter = await svc.CreateLetterAsync(req, ct);
            return Results.Created($"/api/engagements/{letter.Id}", letter);
        }).RequirePermission(Perms.EngagementManage);

        gEng.MapPost("/{id:guid}/send", async (Guid id, IEngagementService svc, CancellationToken ct) =>
        {
            var letter = await svc.SendLetterAsync(id, ct);
            return Results.Ok(letter);
        }).RequirePermission(Perms.EngagementManage);

        gEng.MapPost("/{id:guid}/accept", async (Guid id, AcceptEngagementLetterRequest req, IEngagementService svc, CancellationToken ct) =>
        {
            var letter = await svc.AcceptLetterAsync(id, req, ct);
            return Results.Ok(letter);
        }).RequirePermission(Perms.EngagementManage);

        gEng.MapPost("/template", (GenerateTemplateRequest req, IEngagementService svc) =>
        {
            var template = svc.GenerateTemplate(req);
            return Results.Ok(template);
        }).RequirePermission(Perms.EngagementRead);

        // Client KYC & Beneficial Ownership (UBO)
        gKyc.MapGet("/", async (Guid? clientId, string? riskRating, IEngagementService svc, CancellationToken ct) =>
        {
            var list = await svc.ListKycRecordsAsync(clientId, riskRating, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.KycRead);

        gKyc.MapGet("/{clientId:guid}", async (Guid clientId, IEngagementService svc, CancellationToken ct) =>
        {
            var record = await svc.GetKycRecordByClientIdAsync(clientId, ct);
            return record is null ? Results.NotFound() : Results.Ok(record);
        }).RequirePermission(Perms.KycRead);

        gKyc.MapPost("/", async (SaveClientKycRequest req, IEngagementService svc, CancellationToken ct) =>
        {
            var record = await svc.SaveKycRecordAsync(req, ct);
            return Results.Ok(record);
        }).RequirePermission(Perms.KycManage);

        return app;
    }
}
