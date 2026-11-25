using CaDesk.Api.Auth;
using CaDesk.Application.LedgerSync;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class LedgerSyncEndpoints
{
    public static IEndpointRouteBuilder MapLedgerSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ledger-sync").RequirePlanDesk("ledger-sync")
            .WithTags("Ledger Sync & Auto-Reconciliation")
            .RequireFeature(CaDesk.Domain.Features.FeatureKeys.LedgerSync);

        // 1. Agent Configurations (Firm management)
        g.MapGet("/agents", async ([FromQuery] Guid? clientId, ILedgerSyncService svc, CancellationToken ct) =>
        {
            var res = await svc.GetAgentConfigsAsync(clientId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.LedgerSyncRead);

        g.MapGet("/agents/{id:guid}", async (Guid id, ILedgerSyncService svc, CancellationToken ct) =>
        {
            var res = await svc.GetAgentConfigByIdAsync(id, ct);
            return res is null ? Results.NotFound() : Results.Ok(res);
        }).RequirePermission(Perms.LedgerSyncRead);

        g.MapPost("/agents", async ([FromBody] CreateOrUpdateAgentConfigRequest req, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.SaveAgentConfigAsync(req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.LedgerSyncManage);

        g.MapDelete("/agents/{id:guid}", async (Guid id, ILedgerSyncService svc, CancellationToken ct) =>
        {
            var ok = await svc.DeleteAgentConfigAsync(id, ct);
            return ok ? Results.Ok(new { success = true }) : Results.NotFound();
        }).RequirePermission(Perms.LedgerSyncManage);

        // 2. Agent Heartbeat & Diagnostics (Called by local sync agent)
        g.MapPost("/agents/heartbeat", async ([FromBody] AgentHeartbeatRequest req, ILedgerSyncService svc, CancellationToken ct) =>
        {
            var ok = await svc.RecordAgentHeartbeatAsync(req, ct);
            return ok ? Results.Ok(new { success = true }) : Results.Unauthorized();
        }).AllowAnonymous();

        // 3. Inbound Voucher Sync (Called by local sync agent)
        g.MapPost("/vouchers/batch-sync", async ([FromBody] BatchSyncVouchersRequest req, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.BatchSyncVouchersAsync(req, ct);
                return Results.Ok(res);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).AllowAnonymous();

        g.MapGet("/vouchers", async ([FromQuery] Guid? clientId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, ILedgerSyncService svc, CancellationToken ct) =>
        {
            var res = await svc.GetVouchersAsync(clientId, fromDate, toDate, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.LedgerSyncRead);

        // 4. 2-Way Outgoing Vouchers (Audit Adjustments)
        g.MapPost("/outgoing-vouchers", async ([FromBody] CreateOutgoingVoucherRequest req, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.CreateOutgoingVoucherAsync(req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.LedgerSyncManage);

        g.MapPost("/outgoing-vouchers/{id:guid}/authorize", async (Guid id, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.AuthorizeOutgoingVoucherAsync(id, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequirePermission(Perms.LedgerSyncPost);

        g.MapGet("/outgoing-vouchers", async ([FromQuery] Guid? clientId, ILedgerSyncService svc, CancellationToken ct) =>
        {
            var res = await svc.GetOutgoingVouchersAsync(clientId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.LedgerSyncRead);

        // Agent polling for queued outgoing vouchers
        g.MapGet("/outgoing-vouchers/pending-queue", async ([FromQuery] string agentToken, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.GetPendingOutgoingQueueForAgentAsync(agentToken, ct);
                return Results.Ok(res);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).AllowAnonymous();

        // Agent reports posted voucher status
        g.MapPost("/outgoing-vouchers/report-status", async ([FromBody] ReportOutgoingVoucherStatusRequest req, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var ok = await svc.ReportOutgoingVoucherStatusAsync(req, ct);
                return ok ? Results.Ok(new { success = true }) : Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).AllowAnonymous();

        // 5. AI Bank Feed Auto-Reconciliation Engine
        g.MapPost("/reconciliation/auto-match", async ([FromBody] RunAutoReconciliationRequest req, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.RunAutoReconciliationAsync(req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.LedgerSyncManage);

        g.MapGet("/reconciliation/matches", async ([FromQuery] Guid? clientId, [FromQuery] Guid? bankAccountId, ILedgerSyncService svc, CancellationToken ct) =>
        {
            var res = await svc.GetReconciliationMatchesAsync(clientId, bankAccountId, ct);
            return Results.Ok(res);
        }).RequirePermission(Perms.LedgerSyncRead);

        g.MapPost("/reconciliation/matches/{id:guid}/action", async (Guid id, [FromBody] ReconcileMatchActionRequest req, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.ProcessReconciliationActionAsync(id, req, ct);
                return Results.Ok(res);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.LedgerSyncManage);

        g.MapGet("/reconciliation/summary", async ([FromQuery] Guid clientId, [FromQuery] Guid bankAccountId, ILedgerSyncService svc, CancellationToken ct) =>
        {
            try
            {
                var res = await svc.GetReconciliationSummaryAsync(clientId, bankAccountId, ct);
                return Results.Ok(res);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequirePermission(Perms.LedgerSyncRead);

        return app;
    }
}
