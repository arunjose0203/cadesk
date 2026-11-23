using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.PartnerCompanion;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class PartnerCompanionEndpoints
{
    public static IEndpointRouteBuilder MapPartnerCompanionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/partner-companion").WithTags("PartnerCompanion");

        // Action Center Pulse (Practice pulse metrics, pending audits, timesheets, invoices, forensic alerts, deadlines)
        g.MapGet("/pulse", async (ClaimsPrincipal user, PartnerCompanionService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await svc.GetActionCenterPulseAsync(userId, ct);
            return Results.Ok(result);
        }).RequireAuthorization(Policies.FirmMember);

        // 1-Tap Audit Sign-off
        g.MapPost("/audit/{id:guid}/quick-sign", async (Guid id, [FromBody] QuickSignAuditRequest req, ClaimsPrincipal user, PartnerCompanionService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var success = await svc.QuickSignAuditAsync(id, userId, req, ct);
            return success ? Results.Ok(new { success = true, message = "Audit sign-off recorded successfully." }) : Results.NotFound();
        }).RequireAuthorization(Policies.FirmMember);

        // Quick Approve Timesheets
        g.MapPost("/timesheet/quick-approve", async ([FromBody] QuickApproveTimesheetsRequest req, ClaimsPrincipal user, PartnerCompanionService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            int count = await svc.QuickApproveTimesheetsAsync(req.TimeEntryIds, userId, ct);
            return Results.Ok(new { success = true, approvedCount = count });
        }).RequireAuthorization(Policies.FirmMember);

        // Quick Approve Draft Invoice
        g.MapPost("/invoice/{id:guid}/quick-approve", async (Guid id, ClaimsPrincipal user, PartnerCompanionService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var success = await svc.QuickApproveInvoiceAsync(id, userId, ct);
            return success ? Results.Ok(new { success = true, message = "Invoice approved and marked as Sent." }) : Results.NotFound();
        }).RequireAuthorization(Policies.FirmMember);

        // Quick Triage Bank Forensic Alert
        g.MapPost("/forensic-alert/{id:guid}/triage", async (Guid id, [FromBody] TriageForensicAlertRequest req, ClaimsPrincipal user, PartnerCompanionService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var success = await svc.TriageForensicAlertAsync(id, req, userId, ct);
            return success ? Results.Ok(new { success = true, message = "Forensic alert triage status updated." }) : Results.NotFound();
        }).RequireAuthorization(Policies.FirmMember);

        // Setup Quick PIN
        g.MapPost("/quick-pin/setup", async ([FromBody] SetupQuickPinRequest req, ClaimsPrincipal user, PartnerCompanionService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            try
            {
                var success = await svc.SetupQuickPinAsync(userId, req, ct);
                return Results.Ok(new { success, message = "Quick PIN configured successfully." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(Policies.FirmMember);

        // Verify Quick PIN
        g.MapPost("/quick-pin/verify", async ([FromBody] VerifyQuickPinRequest req, ClaimsPrincipal user, PartnerCompanionService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await svc.VerifyQuickPinAsync(userId, req, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireAuthorization(Policies.FirmMember);

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
