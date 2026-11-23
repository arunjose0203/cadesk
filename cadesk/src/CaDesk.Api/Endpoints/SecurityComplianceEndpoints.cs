using CaDesk.Api.Auth;
using CaDesk.Application.SecurityCompliance;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class SecurityComplianceEndpoints
{
    public static IEndpointRouteBuilder MapSecurityComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/security-compliance").WithTags("SecurityCompliance");

        // Current Assessment & Summary
        g.MapGet("/assessment", async ([FromQuery] string? period, SecurityComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAssessmentSummaryAsync(period, ct)))
            .RequirePermission(Perms.SettingsManage);

        // Control Matrix & Evidences
        g.MapGet("/controls", async ([FromQuery] string? period, [FromQuery] string? category, SecurityComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetControlEvidencesAsync(period, category, ct)))
            .RequirePermission(Perms.SettingsManage);

        // Run Continuous Telemetry Audit Scan
        g.MapPost("/telemetry/evaluate", async ([FromQuery] string? period, SecurityComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.RunContinuousTelemetryAuditAsync(period, ct)))
            .RequirePermission(Perms.SettingsManage);

        // Update Evidence & Remarks
        g.MapPost("/controls/{code}", async (string code, [FromBody] UpdateControlEvidenceRequest req, SecurityComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateControlEvidenceAsync(code, req, ct)))
            .RequirePermission(Perms.SettingsManage);

        // Partner Attestation & Certification
        g.MapPost("/certify", async ([FromBody] CertifySecurityAssessmentRequest req, SecurityComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.CertifyAssessmentAsync(req, ct)))
            .RequirePermission(Perms.SettingsManage);

        // Export Standalone HTML Security Whitepaper & Trust Center Dossier
        g.MapGet("/whitepaper", async ([FromQuery] string? period, SecurityComplianceService svc, CancellationToken ct) =>
        {
            var html = await svc.GenerateSecurityWhitepaperHtmlAsync(period, ct);
            return Results.Content(html, "text/html; charset=utf-8");
        }).RequirePermission(Perms.SettingsManage);

        // Public / RFP Trust Center Overview
        g.MapGet("/trust-center/overview", async (SecurityComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTrustCenterOverviewAsync(ct)))
            .AllowAnonymous();

        return app;
    }
}
