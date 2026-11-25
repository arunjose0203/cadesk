using CaDesk.Api.Auth;
using CaDesk.Application.Ibc;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class IbcCirpEndpoints
{
    public static IEndpointRouteBuilder MapIbcCirpEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ibc-cirp").RequirePlanDesk("ibc-cirp")
            .WithTags("IBC 2016 Insolvency & CIRP Desk")
            .RequireFeature(CaDesk.Domain.Features.FeatureKeys.IbcCirp);

        // 1. CIRP Case Master Registry
        g.MapGet("/cases", async ([FromQuery] Guid? clientId, IIbcCirpService svc, CancellationToken ct) =>
        {
            var res = await svc.GetCasesAsync(clientId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.IbcCirpRead);

        g.MapGet("/cases/{id:guid}", async (Guid id, IIbcCirpService svc, CancellationToken ct) =>
        {
            var res = await svc.GetCaseByIdAsync(id, ct);
            return res is null ? Results.NotFound() : Results.Ok(res);
        }).RequirePermission(Perms.IbcCirpRead);

        g.MapPost("/cases", async ([FromBody] SaveCirpCaseRequest req, IIbcCirpService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveCaseAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.IbcCirpManage);

        g.MapDelete("/cases/{id:guid}", async (Guid id, IIbcCirpService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteCaseAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.IbcCirpManage);

        // 2. Committee of Creditors (CoC) Members
        g.MapPost("/coc-members", async ([FromBody] SaveCocMemberRequest req, IIbcCirpService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveCocMemberAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.IbcCirpManage);

        g.MapDelete("/coc-members/{id:guid}", async (Guid id, IIbcCirpService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteCocMemberAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.IbcCirpManage);

        // 3. CoC Resolutions & Voting Thresholds
        g.MapPost("/resolutions", async ([FromBody] SaveCocResolutionRequest req, IIbcCirpService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveResolutionAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.IbcCirpManage);

        g.MapDelete("/resolutions/{id:guid}", async (Guid id, IIbcCirpService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteResolutionAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.IbcCirpManage);

        // 4. PUFE Forensic Avoidance Transactions
        g.MapPost("/pufe", async ([FromBody] SavePufeTransactionRequest req, IIbcCirpService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SavePufeTransactionAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.IbcCirpManage);

        g.MapDelete("/pufe/{id:guid}", async (Guid id, IIbcCirpService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeletePufeTransactionAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.IbcCirpManage);

        // 5. Asset Valuations (Regulation 35 Fair & Liquidation Value)
        g.MapPost("/valuations", async ([FromBody] SaveAssetValuationRequest req, IIbcCirpService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveValuationAsync(req, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.IbcCirpManage);

        g.MapDelete("/valuations/{id:guid}", async (Guid id, IIbcCirpService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteValuationAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.IbcCirpManage);

        // 6. Regulation 36 Information Memorandum (IM) HTML Export
        g.MapGet("/cases/{id:guid}/im-dossier", async (Guid id, IIbcCirpService svc, CancellationToken ct) =>
        {
            try
            {
                var html = await svc.GenerateInformationMemorandumHtmlAsync(id, ct);
                return Results.Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.IbcCirpRead);

        // 7. Interactive CIRP Timeline Calculator
        g.MapPost("/calculate-timeline", ([FromBody] CalculateCirpTimelineRequest req, IIbcCirpService svc) =>
        {
            var res = svc.CalculateTimelines(req);
            return Results.Ok(res);
        }).RequirePermission(Perms.IbcCirpRead);

        return app;
    }
}
