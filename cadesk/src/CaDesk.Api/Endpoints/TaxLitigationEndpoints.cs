using CaDesk.Api.Auth;
using CaDesk.Application.TaxLitigation;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class TaxLitigationEndpoints
{
    public static IEndpointRouteBuilder MapTaxLitigationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tax-litigation")
            .WithTags("Tax Appeals & Paper Book Desk")
            .RequireFeature(CaDesk.Domain.Features.FeatureKeys.TaxLitigation);

        // 1. Tax Appeals Master Registry
        g.MapGet("/appeals", async ([FromQuery] Guid? clientId, ITaxLitigationService svc, CancellationToken ct) =>
        {
            var res = await svc.GetAppealsAsync(clientId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.TaxLitigationRead);

        g.MapGet("/appeals/{id:guid}", async (Guid id, ITaxLitigationService svc, CancellationToken ct) =>
        {
            var res = await svc.GetAppealByIdAsync(id, ct);
            return res is null ? Results.NotFound() : Results.Ok(res);
        }).RequirePermission(Perms.TaxLitigationRead);

        g.MapPost("/appeals", async ([FromBody] CreateOrUpdateTaxAppealRequest req, ITaxLitigationService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveAppealAsync(req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.TaxLitigationManage);

        g.MapDelete("/appeals/{id:guid}", async (Guid id, ITaxLitigationService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteAppealAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.TaxLitigationManage);

        // 2. Grounds of Appeal
        g.MapPost("/grounds", async ([FromBody] SaveAppealGroundRequest req, ITaxLitigationService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveGroundAsync(req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.TaxLitigationManage);

        g.MapDelete("/grounds/{id:guid}", async (Guid id, ITaxLitigationService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteGroundAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.TaxLitigationManage);

        // 3. ITAT Rule 18 Paper Book Documents
        g.MapPost("/paper-book", async ([FromBody] SavePaperBookDocRequest req, ITaxLitigationService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SavePaperBookDocumentAsync(req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.TaxLitigationManage);

        g.MapDelete("/paper-book/{id:guid}", async (Guid id, ITaxLitigationService svc, CancellationToken ct) =>
        {
            var res = await svc.DeletePaperBookDocumentAsync(id, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.TaxLitigationManage);

        // 4. Judicial Authority Landmark Citations
        g.MapPost("/citations", async ([FromBody] AddJudicialCitationRequest req, ITaxLitigationService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.AddCitationAsync(req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.TaxLitigationManage);

        g.MapDelete("/citations/{id:guid}", async (Guid id, ITaxLitigationService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteCitationAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.TaxLitigationManage);

        // 5. Certification & Dossier Compilation
        g.MapPost("/appeals/{id:guid}/certify", async (Guid id, [FromBody] CertifyAppealRequest req, ITaxLitigationService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.CertifyAppealAsync(id, req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.TaxLitigationCertify);

        g.MapGet("/appeals/{id:guid}/dossier", async (Guid id, ITaxLitigationService svc, CancellationToken ct) =>
        {
            try
            {
                var html = await svc.GenerateAppealDossierHtmlAsync(id, ct);
                return Results.Content(html, "text/html");
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.TaxLitigationRead);

        // 6. Fee Calculator
        g.MapPost("/filing-fee/calculate", ([FromBody] CalculateFilingFeeRequest req, ITaxLitigationService svc) =>
        {
            var res = svc.CalculateFilingFee(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.TaxLitigationRead);

        return app;
    }
}
