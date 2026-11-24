using CaDesk.Api.Auth;
using CaDesk.Application.EsgCsr;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class EsgCsrEndpoints
{
    public static IEndpointRouteBuilder MapEsgCsrEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/esg-csr").WithTags("SEBI BRSR Core & Sec 135 CSR Desk");

        // 1. CSR Assessments
        g.MapGet("/assessments", async ([FromQuery] Guid? clientId, IEsgCsrService svc, CancellationToken ct) =>
        {
            var res = await svc.GetCsrAssessmentsAsync(clientId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.EsgCsrRead);

        g.MapGet("/assessments/{id:guid}", async (Guid id, IEsgCsrService svc, CancellationToken ct) =>
        {
            var res = await svc.GetCsrAssessmentByIdAsync(id, ct);
            return res is null ? Results.NotFound() : Results.Ok(res);
        }).RequirePermission(Perms.EsgCsrRead);

        g.MapPost("/assessments", async ([FromBody] SaveCsrAssessmentRequest req, IEsgCsrService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveCsrAssessmentAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.EsgCsrManage);

        g.MapDelete("/assessments/{id:guid}", async (Guid id, IEsgCsrService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteCsrAssessmentAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.EsgCsrManage);

        // 2. CSR Projects
        g.MapPost("/projects", async ([FromBody] SaveCsrProjectRequest req, IEsgCsrService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveCsrProjectAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.EsgCsrManage);

        g.MapDelete("/projects/{id:guid}", async (Guid id, IEsgCsrService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteCsrProjectAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.EsgCsrManage);

        // 3. SEBI BRSR Core Reports
        g.MapGet("/brsr", async ([FromQuery] Guid? clientId, IEsgCsrService svc, CancellationToken ct) =>
        {
            var res = await svc.GetBrsrReportsAsync(clientId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.EsgCsrRead);

        g.MapGet("/brsr/{id:guid}", async (Guid id, IEsgCsrService svc, CancellationToken ct) =>
        {
            var res = await svc.GetBrsrReportByIdAsync(id, ct);
            return res is null ? Results.NotFound() : Results.Ok(res);
        }).RequirePermission(Perms.EsgCsrRead);

        g.MapGet("/brsr/client/{clientId:guid}/{financialYear}", async (Guid clientId, string financialYear, IEsgCsrService svc, CancellationToken ct) =>
        {
            var res = await svc.GetBrsrReportByClientAndYearAsync(clientId, financialYear, ct);
            return res is null ? Results.NotFound() : Results.Ok(res);
        }).RequirePermission(Perms.EsgCsrRead);

        g.MapPost("/brsr", async ([FromBody] SaveBrsrReportRequest req, IEsgCsrService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveBrsrReportAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.EsgCsrManage);

        g.MapDelete("/brsr/{id:guid}", async (Guid id, IEsgCsrService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteBrsrReportAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.EsgCsrManage);

        // 4. Certification & Sign-off
        g.MapPost("/assessments/{id:guid}/certify", async (Guid id, [FromBody] CertifyCsrRequest req, IEsgCsrService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.CertifyCsrAsync(id, req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.EsgCsrCertify);

        g.MapPost("/brsr/{id:guid}/certify", async (Guid id, [FromBody] CertifyBrsrRequest req, IEsgCsrService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.CertifyBrsrAsync(id, req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.EsgCsrCertify);

        // 5. HTML Assurance Dossier Generation
        g.MapGet("/brsr/{id:guid}/dossier", async (Guid id, IEsgCsrService svc, CancellationToken ct) =>
        {
            try
            {
                var html = await svc.GenerateAssuranceDossierHtmlAsync(id, ct);
                return Results.Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.EsgCsrRead);

        // 6. Interactive Obligation Calculator
        g.MapPost("/calculate-obligation", ([FromBody] CalculateCsrObligationRequest req, IEsgCsrService svc) =>
        {
            var res = svc.CalculateCsrObligation(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.EsgCsrRead);

        return app;
    }
}
