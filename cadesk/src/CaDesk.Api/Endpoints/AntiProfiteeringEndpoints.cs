using System;
using CaDesk.Api.Auth;
using CaDesk.Application.AntiProfiteering;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class AntiProfiteeringEndpoints
{
    public static IEndpointRouteBuilder MapAntiProfiteeringEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/anti-profiteering").RequirePlanDesk("antiprofiteering").WithTags("GST Anti-Profiteering & Rate Notification Engine (Section 171 / Rule 122)");

        // 1. Summary
        g.MapGet("/summary", async (Guid? clientId, string? financialYear, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.AntiProfiteeringRead);

        // 2. Rate Notifications Master
        g.MapGet("/notifications", async (bool? isActive, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var list = await svc.ListNotificationsAsync(isActive, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AntiProfiteeringRead);

        g.MapGet("/notifications/{id:guid}", async (Guid id, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var item = await svc.GetNotificationByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.AntiProfiteeringRead);

        g.MapPost("/notifications", async (SaveRateNotificationRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateNotificationAsync(null, req, ct);
            return Results.Created($"/api/anti-profiteering/notifications/{created.Id}", created);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapPut("/notifications/{id:guid}", async (Guid id, SaveRateNotificationRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateNotificationAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapDelete("/notifications/{id:guid}", async (Guid id, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteNotificationAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.AntiProfiteeringManage);

        // 3. Anti-Profiteering Assessments
        g.MapGet("/assessments", async (Guid? clientId, string? financialYear, string? status, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var list = await svc.ListAssessmentsAsync(clientId, financialYear, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AntiProfiteeringRead);

        g.MapGet("/assessments/{id:guid}", async (Guid id, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var item = await svc.GetAssessmentByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.AntiProfiteeringRead);

        g.MapPost("/assessments", async (SaveAssessmentRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateAssessmentAsync(null, req, ct);
            return Results.Created($"/api/anti-profiteering/assessments/{created.Id}", created);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapPut("/assessments/{id:guid}", async (Guid id, SaveAssessmentRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateAssessmentAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapDelete("/assessments/{id:guid}", async (Guid id, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteAssessmentAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapPost("/assessments/{id:guid}/certify", async (Guid id, CertifyAssessmentRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var certified = await svc.CertifyAssessmentAsync(id, req, ct);
            return Results.Ok(certified);
        }).RequirePermission(Perms.AntiProfiteeringCertify);

        // 4. SKU Profiteering Items
        g.MapGet("/assessments/{id:guid}/skus", async (Guid id, string? complianceStatus, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var list = await svc.ListSkuItemsAsync(id, complianceStatus, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AntiProfiteeringRead);

        g.MapPost("/assessments/{id:guid}/skus", async (Guid id, SaveSkuItemRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateSkuItemAsync(id, null, req, ct);
            return Results.Created($"/api/anti-profiteering/assessments/{id}/skus/{created.Id}", created);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapPut("/assessments/{id:guid}/skus/{skuId:guid}", async (Guid id, Guid skuId, SaveSkuItemRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateSkuItemAsync(id, skuId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapDelete("/assessments/{id:guid}/skus/{skuId:guid}", async (Guid id, Guid skuId, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteSkuItemAsync(id, skuId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.AntiProfiteeringManage);

        // 5. Customer Refunds & Consumer Welfare Fund Records
        g.MapGet("/assessments/{id:guid}/refunds", async (Guid id, string? recipientType, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var list = await svc.ListRefundRecordsAsync(id, recipientType, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AntiProfiteeringRead);

        g.MapPost("/assessments/{id:guid}/refunds", async (Guid id, SaveRefundRecordRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateRefundRecordAsync(id, null, req, ct);
            return Results.Created($"/api/anti-profiteering/assessments/{id}/refunds/{created.Id}", created);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapPut("/assessments/{id:guid}/refunds/{refundId:guid}", async (Guid id, Guid refundId, SaveRefundRecordRequest req, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var updated = await svc.CreateOrUpdateRefundRecordAsync(id, refundId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AntiProfiteeringManage);

        g.MapDelete("/assessments/{id:guid}/refunds/{refundId:guid}", async (Guid id, Guid refundId, AntiProfiteeringService svc, CancellationToken ct) =>
        {
            var deleted = await svc.DeleteRefundRecordAsync(id, refundId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequirePermission(Perms.AntiProfiteeringManage);

        return app;
    }
}
