using CaDesk.Api.Auth;
using CaDesk.Application.BankFeeds;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class BankFeedEndpoints
{
    public static IEndpointRouteBuilder MapBankFeedEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/bank").RequirePlanDesk("banking").WithTags("Bank Feeds & Automated BRS Auto-Matcher");

        // Bank Accounts CRUD
        g.MapGet("/accounts", async (Guid? clientId, IBankFeedService svc, CancellationToken ct) =>
        {
            var list = await svc.GetAccountsAsync(clientId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.BankRead);

        g.MapGet("/accounts/{id:guid}", async (Guid id, IBankFeedService svc, CancellationToken ct) =>
        {
            var acc = await svc.GetAccountByIdAsync(id, ct);
            return Results.Ok(acc);
        }).RequirePermission(Perms.BankRead);

        g.MapPost("/accounts", async (CreateBankAccountRequest req, IBankFeedService svc, CancellationToken ct) =>
        {
            var acc = await svc.CreateAccountAsync(req, ct);
            return Results.Created($"/api/bank/accounts/{acc.Id}", acc);
        }).RequirePermission(Perms.BankManage);

        g.MapPut("/accounts/{id:guid}", async (Guid id, UpdateBankAccountRequest req, IBankFeedService svc, CancellationToken ct) =>
        {
            var acc = await svc.UpdateAccountAsync(id, req, ct);
            return Results.Ok(acc);
        }).RequirePermission(Perms.BankManage);

        g.MapDelete("/accounts/{id:guid}", async (Guid id, IBankFeedService svc, CancellationToken ct) =>
        {
            await svc.DeleteAccountAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.BankManage);

        // Bank Statements
        g.MapGet("/accounts/{id:guid}/statements", async (Guid id, IBankFeedService svc, CancellationToken ct) =>
        {
            var list = await svc.GetStatementsAsync(id, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.BankRead);

        g.MapPost("/accounts/{id:guid}/statements", async (Guid id, UploadStatementRequest req, IBankFeedService svc, CancellationToken ct) =>
        {
            var stmt = await svc.UploadStatementAsync(id, req, ct);
            return Results.Created($"/api/bank/accounts/{id}/statements/{stmt.Id}", stmt);
        }).RequirePermission(Perms.BankManage);

        // Transactions & Matching
        g.MapGet("/accounts/{id:guid}/transactions", async (
            Guid id,
            Guid? statementId,
            string? matchStatus,
            DateOnly? fromDate,
            DateOnly? toDate,
            IBankFeedService svc,
            CancellationToken ct) =>
        {
            var list = await svc.GetTransactionsAsync(id, statementId, matchStatus, fromDate, toDate, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.BankRead);

        g.MapPost("/transactions/{id:guid}/match", async (Guid id, MatchTransactionRequest req, IBankFeedService svc, CancellationToken ct) =>
        {
            var tx = await svc.MatchTransactionAsync(id, req, ct);
            return Results.Ok(tx);
        }).RequirePermission(Perms.BankReconcile);

        // BRS Reconciliation Summary
        g.MapGet("/accounts/{id:guid}/brs", async (Guid id, DateOnly? asOfDate, IBankFeedService svc, CancellationToken ct) =>
        {
            var brs = await svc.GetBrsSummaryAsync(id, asOfDate, ct);
            return Results.Ok(brs);
        }).RequirePermission(Perms.BankRead);

        // Categorization & Reconciliation Rules
        g.MapGet("/rules", async (IBankFeedService svc, CancellationToken ct) =>
        {
            var rules = await svc.GetRulesAsync(ct);
            return Results.Ok(rules);
        }).RequirePermission(Perms.BankRead);

        g.MapPost("/rules", async (CreateBankReconciliationRuleRequest req, IBankFeedService svc, CancellationToken ct) =>
        {
            var rule = await svc.CreateRuleAsync(req, ct);
            return Results.Created($"/api/bank/rules/{rule.Id}", rule);
        }).RequirePermission(Perms.BankManage);

        g.MapDelete("/rules/{id:guid}", async (Guid id, IBankFeedService svc, CancellationToken ct) =>
        {
            await svc.DeleteRuleAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.BankManage);

        // Forensic Fraud Detection & Statutory Scanners (Sec 40A(3), Sec 269ST, Round-Tripping)
        g.MapPost("/accounts/{id:guid}/forensic-scan", async (Guid id, RunForensicScanRequest req, IBankFeedService svc, CancellationToken ct) =>
        {
            var summary = await svc.RunForensicScanAsync(id, req, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.BankReconcile);

        g.MapGet("/accounts/{id:guid}/forensic-report", async (Guid id, IBankFeedService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetForensicReportAsync(id, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.BankRead);

        g.MapPut("/anomalies/{id:guid}/status", async (Guid id, UpdateAnomalyStatusRequest req, IBankFeedService svc, CancellationToken ct) =>
        {
            var anomaly = await svc.UpdateAnomalyStatusAsync(id, req, ct);
            return Results.Ok(anomaly);
        }).RequirePermission(Perms.BankReconcile);

        g.MapPost("/clients/{clientId:guid}/reconcile-tb", async (Guid clientId, Guid? trialBalanceImportId, IBankFeedService svc, CancellationToken ct) =>
        {
            var report = await svc.ReconcileWithTrialBalanceAsync(clientId, trialBalanceImportId, ct);
            return Results.Ok(report);
        }).RequirePermission(Perms.BankRead);

        g.MapGet("/accounts/{id:guid}/forensic-report/export-html", async (Guid id, IBankFeedService svc, CancellationToken ct) =>
        {
            var report = await svc.ExportForensicReportHtmlAsync(id, ct);
            return Results.Ok(report);
        }).RequirePermission(Perms.BankRead);

        return app;
    }
}
