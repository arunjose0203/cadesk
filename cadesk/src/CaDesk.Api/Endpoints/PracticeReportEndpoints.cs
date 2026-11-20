using CaDesk.Api.Auth;
using CaDesk.Application.Reports;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class PracticeReportEndpoints
{
    public static IEndpointRouteBuilder MapPracticeReportEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports").WithTags("Practice Performance MIS & Executive Reporting");

        // Executive KPI Dashboard Summary
        g.MapGet("/executive-summary", async (string? financialYear, IPracticeReportService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetExecutiveSummaryAsync(financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.ReportsRead);

        // Client Profitability Analysis
        g.MapGet("/client-profitability", async (string? financialYear, IPracticeReportService svc, CancellationToken ct) =>
        {
            var list = await svc.GetClientProfitabilityAsync(financialYear, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.ReportsRead);

        // Staff Utilization & Billable Hours
        g.MapGet("/staff-utilization", async (DateOnly? from, DateOnly? to, IPracticeReportService svc, CancellationToken ct) =>
        {
            var list = await svc.GetStaffUtilizationAsync(from, to, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.ReportsRead);

        // Receivables Aging (0-30, 31-60, 61-90, >90)
        g.MapGet("/receivables-aging", async (IPracticeReportService svc, CancellationToken ct) =>
        {
            var aging = await svc.GetReceivablesAgingAsync(ct);
            return Results.Ok(aging);
        }).RequirePermission(Perms.ReportsRead);

        // Statutory Compliance Filing SLAs
        g.MapGet("/compliance-sla", async (string? financialYear, IPracticeReportService svc, CancellationToken ct) =>
        {
            var sla = await svc.GetComplianceSlaReportAsync(financialYear, ct);
            return Results.Ok(sla);
        }).RequirePermission(Perms.ReportsRead);

        // Revenue Breakdown by Practice / SAC
        g.MapGet("/revenue-breakdown", async (string? financialYear, IPracticeReportService svc, CancellationToken ct) =>
        {
            var breakdown = await svc.GetRevenueBreakdownAsync(financialYear, ct);
            return Results.Ok(breakdown);
        }).RequirePermission(Perms.ReportsRead);

        // Practice Targets
        g.MapGet("/targets/{fy}", async (string fy, IPracticeReportService svc, CancellationToken ct) =>
        {
            var target = await svc.GetPracticeTargetAsync(fy, ct);
            return target is null ? Results.NotFound() : Results.Ok(target);
        }).RequirePermission(Perms.ReportsRead);

        g.MapPost("/targets", async (SetPracticeTargetRequest req, IPracticeReportService svc, CancellationToken ct) =>
        {
            var target = await svc.SetPracticeTargetAsync(req, ct);
            return Results.Ok(target);
        }).RequirePermission(Perms.ReportsManage);

        return app;
    }
}
