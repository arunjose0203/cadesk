using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Office;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class OfficeEndpoints
{
    public static IEndpointRouteBuilder MapOfficeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/office").WithTags("Office Essentials");

        // =====================================================================
        // 1. Attendance
        // =====================================================================

        g.MapPost("/attendance/check-in", async (
            [FromBody] AttendanceCheckInRequest req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var record = await svc.CheckInAsync(userId, req, ct);
            return Results.Ok(record);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapPost("/attendance/check-out", async (
            [FromBody] AttendanceCheckOutRequest req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var record = await svc.CheckOutAsync(userId, req, ct);
            return Results.Ok(record);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/attendance/today", async (
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var record = await svc.GetTodayAsync(userId, ct);
            return Results.Ok(record);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/attendance/register", async (
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] Guid? userId,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var m = month ?? now.Month;
            var y = year ?? now.Year;
            var targetUserId = userId ?? user.RequiredUserId();

            var summary = await svc.GetMonthlyRegisterAsync(m, y, targetUserId, ct);
            return Results.Ok(summary);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/attendance/export", async (
            [FromQuery] int? month,
            [FromQuery] int? year,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var m = month ?? now.Month;
            var y = year ?? now.Year;

            var csv = await svc.ExportAttendanceCsvAsync(m, y, ct);
            return Results.Content(csv, "text/csv");
        }).RequirePermission(Perms.AttendanceManage);

        // =====================================================================
        // 2. Leave Management
        // =====================================================================

        g.MapGet("/leave/balances", async (
            [FromQuery] Guid? userId,
            [FromQuery] int? year,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var targetUserId = userId ?? user.RequiredUserId();
            var balances = await svc.GetBalancesAsync(targetUserId, year, ct);
            return Results.Ok(balances);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapPost("/leave/apply", async (
            [FromBody] ApplyLeaveRequest req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var result = await svc.ApplyLeaveAsync(userId, req, ct);
            return Results.Created($"/api/office/leave/requests/{result.Id}", result);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/leave/requests", async (
            [FromQuery] Guid? userId,
            [FromQuery] string? status,
            [FromQuery] int? year,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListRequestsAsync(userId, status, year, ct);
            return Results.Ok(list);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapPost("/leave/requests/{id:guid}/approve", async (
            Guid id,
            [FromBody] LeaveApprovalRequest? req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var approverId = user.RequiredUserId();
            var updated = await svc.ApproveLeaveAsync(id, approverId, req?.Remarks, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.LeaveApprove);

        g.MapPost("/leave/requests/{id:guid}/reject", async (
            Guid id,
            [FromBody] LeaveRejectionRequest req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var approverId = user.RequiredUserId();
            var updated = await svc.RejectLeaveAsync(id, approverId, req.Reason, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.LeaveApprove);

        g.MapGet("/leave/export", async (
            [FromQuery] int? year,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var y = year ?? DateTime.UtcNow.Year;
            var csv = await svc.ExportLeaveRegisterCsvAsync(y, ct);
            return Results.Content(csv, "text/csv");
        }).RequirePermission(Perms.LeaveApprove);

        // =====================================================================
        // 3. Office Expenses & Client Reimbursables
        // =====================================================================

        g.MapPost("/expenses", async (
            [FromBody] SubmitExpenseRequest req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var created = await svc.SubmitExpenseAsync(userId, req, ct);
            return Results.Created($"/api/office/expenses/{created.Id}", created);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/expenses", async (
            [FromQuery] Guid? userId,
            [FromQuery] Guid? clientId,
            [FromQuery] string? status,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListExpensesAsync(userId, clientId, status, from, to, ct);
            return Results.Ok(list);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapPost("/expenses/{id:guid}/approve", async (
            Guid id,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var approverId = user.RequiredUserId();
            var approved = await svc.ApproveExpenseAsync(id, approverId, ct);
            return Results.Ok(approved);
        }).RequirePermission(Perms.ExpenseApprove);

        g.MapPost("/expenses/{id:guid}/reject", async (
            Guid id,
            [FromBody] ExpenseRejectionRequest req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var approverId = user.RequiredUserId();
            var rejected = await svc.RejectExpenseAsync(id, approverId, req.Reason, ct);
            return Results.Ok(rejected);
        }).RequirePermission(Perms.ExpenseApprove);

        g.MapGet("/expenses/unbilled/{clientId:guid}", async (
            Guid clientId,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var unbilled = await svc.GetUnbilledClientExpensesAsync(clientId, ct);
            return Results.Ok(unbilled);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/expenses/report", async (
            [FromQuery] int? month,
            [FromQuery] int? year,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var m = month ?? now.Month;
            var y = year ?? now.Year;
            var report = await svc.GetMonthlyExpenseReportAsync(m, y, ct);
            return Results.Ok(report);
        }).RequirePermission(Perms.ExpenseApprove);

        // =====================================================================
        // 4. Inward / Outward Registers
        // =====================================================================

        g.MapPost("/registers", async (
            [FromBody] CreateRegisterEntryRequest req,
            ClaimsPrincipal user,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var created = await svc.CreateRegisterEntryAsync(userId, req, ct);
            return Results.Created($"/api/office/registers/{created.Id}", created);
        }).RequirePermission(Perms.RegisterManage);

        g.MapGet("/registers", async (
            [AsParameters] RegisterListQuery query,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListRegisterEntriesAsync(query, ct);
            return Results.Ok(list);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/registers/printable-html", async (
            [FromQuery] string? entryType,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            OfficeService svc,
            CancellationToken ct) =>
        {
            var html = await svc.GeneratePrintableRegisterHtmlAsync(entryType, from, to, ct);
            return Results.Content(html, "text/html");
        }).RequireAuthorization(Policies.FirmMember);

        return app;
    }
}
