using CaDesk.Api.Auth;
using CaDesk.Application.TaxNotices;
using CaDesk.Domain.TaxNotices;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class TaxNoticeEndpoints
{
    public static IEndpointRouteBuilder MapTaxNoticeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tax-notices").WithTags("Tax Notice & Faceless Assessment Desk");

        // Dashboard Summary
        g.MapGet("/summary", async (ITaxNoticeService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetDashboardSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.NoticesRead);

        // Notices List
        g.MapGet("", async (TaxAuthority? authority, NoticeStatus? status, Guid? clientId, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var list = await svc.GetNoticesAsync(authority, status, clientId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.NoticesRead);

        // Notice Details
        g.MapGet("/{id:guid}", async (Guid id, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var notice = await svc.GetNoticeByIdAsync(id, ct);
            return notice is null ? Results.NotFound() : Results.Ok(notice);
        }).RequirePermission(Perms.NoticesRead);

        // Create Notice
        g.MapPost("", async (CreateTaxNoticeRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateNoticeAsync(req, ct);
            return Results.Created($"/api/tax-notices/{created.Id}", created);
        }).RequirePermission(Perms.NoticesManage);

        // Update Notice
        g.MapPut("/{id:guid}", async (Guid id, UpdateTaxNoticeRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateNoticeAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.NoticesManage);

        // Verify DIN
        g.MapPost("/{id:guid}/verify-din", async (Guid id, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var verified = await svc.VerifyDinAsync(id, ct);
            return Results.Ok(verified);
        }).RequirePermission(Perms.NoticesManage);

        // Hearings
        g.MapGet("/hearings", async (Guid? noticeId, HearingStatus? status, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var list = await svc.GetHearingsAsync(noticeId, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.NoticesRead);

        g.MapPost("/{id:guid}/hearings", async (Guid id, CreateHearingRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var hearing = await svc.AddHearingAsync(id, req, ct);
            return Results.Created($"/api/tax-notices/hearings/{hearing.Id}", hearing);
        }).RequirePermission(Perms.NoticesManage);

        g.MapPut("/hearings/{hearingId:guid}/status", async (Guid hearingId, UpdateHearingStatusRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateHearingStatusAsync(hearingId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.NoticesManage);

        // Submissions & Responses
        g.MapGet("/{id:guid}/responses", async (Guid id, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var responses = await svc.GetResponsesAsync(id, ct);
            return Results.Ok(responses);
        }).RequirePermission(Perms.NoticesRead);

        g.MapPost("/{id:guid}/responses", async (Guid id, DraftResponseRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var response = await svc.DraftResponseAsync(id, req, ct);
            return Results.Created($"/api/tax-notices/responses/{response.Id}", response);
        }).RequirePermission(Perms.NoticesManage);

        g.MapPost("/responses/{responseId:guid}/approve", async (Guid responseId, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var approved = await svc.ApproveResponseAsync(responseId, ct);
            return Results.Ok(approved);
        }).RequirePermission(Perms.NoticesApprove);

        g.MapPost("/responses/{responseId:guid}/submit", async (Guid responseId, RecordSubmissionRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var submitted = await svc.RecordSubmissionAsync(responseId, req, ct);
            return Results.Ok(submitted);
        }).RequirePermission(Perms.NoticesManage);

        // Tax Demands & Appeals Ledger
        g.MapGet("/demands", async (Guid? clientId, bool? overdueOnly, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var demands = await svc.GetDemandsAsync(clientId, overdueOnly, ct);
            return Results.Ok(demands);
        }).RequirePermission(Perms.NoticesRead);

        g.MapPost("/demands", async (CreateTaxDemandRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var demand = await svc.CreateDemandAsync(req, ct);
            return Results.Created($"/api/tax-notices/demands/{demand.Id}", demand);
        }).RequirePermission(Perms.NoticesManage);

        g.MapPost("/demands/{demandId:guid}/payment", async (Guid demandId, RecordDemandPaymentRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var paid = await svc.RecordDemandPaymentAsync(demandId, req, ct);
            return Results.Ok(paid);
        }).RequirePermission(Perms.NoticesManage);

        g.MapPut("/demands/{demandId:guid}/appeal", async (Guid demandId, UpdateAppealRequest req, ITaxNoticeService svc, CancellationToken ct) =>
        {
            var appealed = await svc.UpdateAppealStatusAsync(demandId, req, ct);
            return Results.Ok(appealed);
        }).RequirePermission(Perms.NoticesManage);

        return app;
    }
}
