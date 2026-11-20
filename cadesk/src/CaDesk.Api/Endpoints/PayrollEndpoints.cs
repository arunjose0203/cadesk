using CaDesk.Api.Auth;
using CaDesk.Application.Payroll;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/payroll").WithTags("Payroll, Salary TDS & Statutory PF / ESIC Desk");

        // Summary
        g.MapGet("/summary", async (Guid? clientId, IPayrollService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetPayrollSummaryAsync(clientId, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.PayrollRead);

        // Employee Master
        g.MapGet("/employees", async (Guid? clientId, bool? activeOnly, IPayrollService svc, CancellationToken ct) =>
        {
            var list = await svc.GetEmployeesAsync(clientId, activeOnly, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.PayrollRead);

        g.MapGet("/employees/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct) =>
        {
            var emp = await svc.GetEmployeeByIdAsync(id, ct);
            return emp is null ? Results.NotFound() : Results.Ok(emp);
        }).RequirePermission(Perms.PayrollRead);

        g.MapPost("/employees", async (SavePayrollEmployeeRequest req, IPayrollService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateEmployeeAsync(req, ct);
            return Results.Created($"/api/payroll/employees/{created.Id}", created);
        }).RequirePermission(Perms.PayrollManage);

        g.MapPut("/employees/{id:guid}", async (Guid id, SavePayrollEmployeeRequest req, IPayrollService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateEmployeeAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.PayrollManage);

        g.MapDelete("/employees/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct) =>
        {
            await svc.DeleteEmployeeAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.PayrollManage);

        // Monthly Payroll Runs
        g.MapGet("/runs", async (Guid? clientId, int? year, IPayrollService svc, CancellationToken ct) =>
        {
            var runs = await svc.GetPayrollRunsAsync(clientId, year, ct);
            return Results.Ok(runs);
        }).RequirePermission(Perms.PayrollRead);

        g.MapGet("/runs/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct) =>
        {
            var run = await svc.GetPayrollRunByIdAsync(id, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        }).RequirePermission(Perms.PayrollRead);

        g.MapPost("/runs", async (ProcessPayrollRunRequest req, IPayrollService svc, CancellationToken ct) =>
        {
            var processed = await svc.ProcessPayrollRunAsync(req, ct);
            return Results.Created($"/api/payroll/runs/{processed.Id}", processed);
        }).RequirePermission(Perms.PayrollProcess);

        g.MapPut("/runs/{id:guid}/status", async (Guid id, UpdatePayrollRunStatusRequest req, IPayrollService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdatePayrollRunStatusAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.PayrollProcess);

        // Payslips & ECR Export
        g.MapGet("/runs/{id:guid}/payslips", async (Guid id, IPayrollService svc, CancellationToken ct) =>
        {
            var payslips = await svc.GetPayslipsAsync(id, ct);
            return Results.Ok(payslips);
        }).RequirePermission(Perms.PayrollRead);

        g.MapGet("/runs/{id:guid}/ecr", async (Guid id, IPayrollService svc, CancellationToken ct) =>
        {
            var ecr = await svc.ExportEpfoEcrAsync(id, ct);
            return Results.Ok(ecr);
        }).RequirePermission(Perms.PayrollRead);

        return app;
    }
}
