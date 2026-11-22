using CaDesk.Api.Auth;
using CaDesk.Application.TrialBalance;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class TrialBalanceEndpoints
{
    public static void MapTrialBalanceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/trial-balance")
            .RequireAuthorization(Policies.FirmMember)
            .WithTags("Trial Balance & Multi-Desk Synchronizer");

        // Upload and parse Tally XML / CSV
        g.MapPost("/upload", async (
            [FromBody] UploadTrialBalanceRequest req,
            TrialBalanceService service,
            CancellationToken ct) =>
        {
            var result = await service.UploadTrialBalanceAsync(req, ct);
            return Results.Created($"/api/trial-balance/imports/{result.Id}", result);
        }).RequirePermission(Perms.TrialBalanceManage);

        // List imports
        g.MapGet("/imports", async (
            [FromQuery] Guid? clientId,
            [FromQuery] string? financialYear,
            TrialBalanceService service,
            CancellationToken ct) =>
        {
            var list = await service.GetImportsAsync(clientId, financialYear, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.TrialBalanceRead);

        // Get single import with all ledgers
        g.MapGet("/imports/{id:guid}", async (
            Guid id,
            TrialBalanceService service,
            CancellationToken ct) =>
        {
            var item = await service.GetImportByIdAsync(id, ct);
            return item is not null ? Results.Ok(item) : Results.NotFound();
        }).RequirePermission(Perms.TrialBalanceRead);

        // Update single ledger mapping (with option to save as default rule for client)
        g.MapPut("/imports/{id:guid}/ledgers/{ledgerId:guid}", async (
            Guid id,
            Guid ledgerId,
            [FromBody] UpdateLedgerMappingRequest req,
            TrialBalanceService service,
            CancellationToken ct) =>
        {
            var updated = await service.UpdateLedgerMappingAsync(id, ledgerId, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.TrialBalanceManage);

        // Sync Trial Balance down to Audit, Schedule III, Form 3CD, CARO 2020, and Fixed Assets
        g.MapPost("/imports/{id:guid}/sync", async (
            Guid id,
            [FromBody] SyncTrialBalanceRequest req,
            TrialBalanceService service,
            CancellationToken ct) =>
        {
            var syncResult = await service.SyncToDesksAsync(id, req, ct);
            return Results.Ok(syncResult);
        }).RequirePermission(Perms.TrialBalanceSync);

        // Get saved client-level mapping rules
        g.MapGet("/clients/{clientId:guid}/rules", async (
            Guid clientId,
            TrialBalanceService service,
            CancellationToken ct) =>
        {
            var rules = await service.GetClientRulesAsync(clientId, ct);
            return Results.Ok(rules);
        }).RequirePermission(Perms.TrialBalanceRead);
    }
}
