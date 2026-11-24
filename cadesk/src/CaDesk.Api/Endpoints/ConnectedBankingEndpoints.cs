using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.ConnectedBanking;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class ConnectedBankingEndpoints
{
    public static IEndpointRouteBuilder MapConnectedBankingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/connected-banking")
            .WithTags("ConnectedBanking")
            .RequireFeature(CaDesk.Domain.Features.FeatureKeys.ConnectedBanking);

        // 1. List connected bank API feeds
        g.MapGet("/accounts", async ([FromQuery] Guid? clientId, ConnectedBankingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetConnectedAccountsAsync(clientId, ct)))
            .RequireAuthorization(Policies.FirmMember);

        // 2. Connect bank credentials (ICICI, HDFC, Axis, Open)
        g.MapPost("/accounts/connect", async ([FromBody] ConnectBankRequest req, ConnectedBankingService svc, CancellationToken ct) =>
        {
            try
            {
                var result = await svc.ConnectBankAccountAsync(req, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(Policies.FirmMember);

        // 3. Trigger 1-Click live feed synchronization
        g.MapPost("/accounts/{id:guid}/sync", async (Guid id, ConnectedBankingService svc, CancellationToken ct) =>
        {
            try
            {
                var result = await svc.SyncLiveBankFeedAsync(id, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(Policies.FirmMember);

        // 4. List Payout Batches
        g.MapGet("/payouts", async ([FromQuery] Guid? clientId, [FromQuery] string? status, ConnectedBankingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPayoutBatchesAsync(clientId, status, ct)))
            .RequireAuthorization(Policies.FirmMember);

        // 5. Create Payout Batch (Maker)
        g.MapPost("/payouts", async ([FromBody] CreatePayoutBatchRequest req, ClaimsPrincipal user, ConnectedBankingService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            try
            {
                var batch = await svc.CreatePayoutBatchAsync(req, userId, ct);
                return Results.Ok(batch);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.BankingPayoutCreate);

        // 6. Submit Batch for Partner Review (Maker)
        g.MapPost("/payouts/{id:guid}/submit", async (Guid id, ClaimsPrincipal user, ConnectedBankingService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            try
            {
                var success = await svc.SubmitBatchForApprovalAsync(id, userId, ct);
                return success ? Results.Ok(new { success = true, message = "Batch submitted for Partner authorization." }) : Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.BankingPayoutCreate);

        // 7. Authorize Payout Batch (Partner Checker)
        g.MapPost("/payouts/{id:guid}/authorize", async (Guid id, [FromBody] AuthorizePayoutBatchRequest req, ClaimsPrincipal user, ConnectedBankingService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            try
            {
                var batch = await svc.AuthorizePayoutBatchAsync(id, userId, req, ct);
                return Results.Ok(batch);
            }
            catch (InvalidOperationException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.BankingPayoutApprove);

        // 8. Reject Payout Batch (Partner Checker)
        g.MapPost("/payouts/{id:guid}/reject", async (Guid id, [FromBody] RejectPayoutBatchRequest req, ClaimsPrincipal user, ConnectedBankingService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            try
            {
                var success = await svc.RejectPayoutBatchAsync(id, userId, req, ct);
                return success ? Results.Ok(new { success = true, message = "Batch rejected and returned to maker." }) : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.BankingPayoutApprove);

        // 9. 1-Click Statutory Tax Challan Payout Generation
        g.MapPost("/payouts/tax-challan", async ([FromBody] GenerateTaxChallanPayoutRequest req, ClaimsPrincipal user, ConnectedBankingService svc, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            try
            {
                var batch = await svc.GenerateTaxChallanPayoutAsync(req, userId, ct);
                return Results.Ok(batch);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(Policies.FirmMember);

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
