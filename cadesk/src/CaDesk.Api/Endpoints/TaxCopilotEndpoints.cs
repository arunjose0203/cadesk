using CaDesk.Api.Auth;
using CaDesk.Application.TaxNotices;
using CaDesk.Domain.TaxNotices;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class TaxCopilotEndpoints
{
    public static IEndpointRouteBuilder MapTaxCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tax-copilot").WithTags("CA Copilot: Statutory Citation & Notice Defense Drafter");

        // 1. Notice Text / OCR Ingestion & Categorization
        g.MapPost("/parse", async (ParseNoticeRequest req, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var result = await svc.ParseNoticeAsync(req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.NoticesRead);

        // 2. Judicial Precedents & Citations Search
        g.MapGet("/precedents", async (TaxAuthority? authority, PrecedentCategory? category, string? search, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var list = await svc.GetPrecedentsAsync(authority, category, search, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.NoticesRead);

        // 3. Add Custom Firm Precedent
        g.MapPost("/precedents", async (CreatePrecedentRequest req, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateCustomPrecedentAsync(req, ct);
            return Results.Created($"/api/tax-copilot/precedents/{created.Id}", created);
        }).RequirePermission(Perms.NoticesManage);

        // 4. Create Defense Draft for Notice
        g.MapPost("/drafts", async (CreateDefenseDraftRequest req, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var draft = await svc.CreateDefenseDraftAsync(req, ct);
            return Results.Created($"/api/tax-copilot/drafts/{draft.Id}", draft);
        }).RequirePermission(Perms.NoticesManage);

        // 5. Get Defense Draft Details
        g.MapGet("/drafts/{id:guid}", async (Guid id, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var draft = await svc.GetDefenseDraftByIdAsync(id, ct);
            return draft is null ? Results.NotFound() : Results.Ok(draft);
        }).RequirePermission(Perms.NoticesRead);

        // 6. Get Defense Drafts for a specific Notice
        g.MapGet("/notices/{noticeId:guid}/drafts", async (Guid noticeId, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var list = await svc.GetDefenseDraftsForNoticeAsync(noticeId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.NoticesRead);

        // 7. Update Defense Draft
        g.MapPut("/drafts/{id:guid}", async (Guid id, UpdateDefenseDraftRequest req, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateDefenseDraftAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.NoticesManage);

        // 8. Partner Approval & UDIN Attestation
        g.MapPost("/drafts/{id:guid}/approve", async (Guid id, ApproveDefenseDraftRequest req, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var approved = await svc.ApproveDefenseDraftAsync(id, req, ct);
            return Results.Ok(approved);
        }).RequirePermission(Perms.NoticesApprove);

        // 9. Export Standalone Print-Ready HTML Submission
        g.MapGet("/drafts/{id:guid}/export-html", async (Guid id, ITaxCopilotService svc, CancellationToken ct) =>
        {
            var html = await svc.ExportDefenseDraftHtmlAsync(id, ct);
            return Results.Content(html, "text/html; charset=utf-8");
        }).RequirePermission(Perms.NoticesRead);

        return app;
    }
}
