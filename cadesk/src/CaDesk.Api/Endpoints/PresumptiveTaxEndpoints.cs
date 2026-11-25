using System;
using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Presumptive;
using CaDesk.Application.Tax;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CaDesk.Api.Endpoints;

public static class PresumptiveTaxEndpoints
{
    public static IEndpointRouteBuilder MapPresumptiveTaxEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/presumptive").WithTags("Presumptive Taxation & Minimal Financials (44AD/ADA/AE)");

        // KPI Summary
        g.MapGet("/summary", async (PresumptiveTaxService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.PresumptiveRead);

        // List computations
        g.MapGet("/computations", async (
            Guid? clientId,
            string? fy,
            string? section,
            string? status,
            PresumptiveTaxService svc,
            CancellationToken ct) =>
        {
            var list = await svc.ListComputationsAsync(clientId, fy, section, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.PresumptiveRead);

        // Get computation by ID
        g.MapGet("/computations/{id:guid}", async (Guid id, PresumptiveTaxService svc, CancellationToken ct) =>
        {
            var comp = await svc.GetComputationByIdAsync(id, ct);
            return Results.Ok(comp);
        }).RequirePermission(Perms.PresumptiveRead);

        // Create computation
        g.MapPost("/computations", async (
            SavePresumptiveComputationRequest req,
            ClaimsPrincipal user,
            PresumptiveTaxService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var created = await svc.CreateComputationAsync(req, userId, ct);
            return Results.Created($"/api/presumptive/computations/{created.Id}", created);
        }).RequirePermission(Perms.PresumptiveManage);

        // Update computation
        g.MapPut("/computations/{id:guid}", async (
            Guid id,
            SavePresumptiveComputationRequest req,
            ClaimsPrincipal user,
            PresumptiveTaxService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var updated = await svc.UpdateComputationAsync(id, req, userId, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.PresumptiveManage);

        // Recalculate computation
        g.MapPost("/computations/{id:guid}/recalculate", async (Guid id, PresumptiveTaxService svc, CancellationToken ct) =>
        {
            var computed = await svc.RecalculateAsync(id, ct);
            return Results.Ok(computed);
        }).RequirePermission(Perms.PresumptiveManage);

        // Add 44AE Vehicle
        g.MapPost("/computations/{id:guid}/vehicles", async (
            Guid id,
            SaveVehicleRequest req,
            PresumptiveTaxService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.AddVehicleAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.PresumptiveManage);

        // Delete 44AE Vehicle
        g.MapDelete("/computations/{id:guid}/vehicles/{vehicleId:guid}", async (
            Guid id,
            Guid vehicleId,
            PresumptiveTaxService svc,
            CancellationToken ct) =>
        {
            var updated = await svc.RemoveVehicleAsync(id, vehicleId, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.PresumptiveManage);

        // Approve computation
        g.MapPost("/computations/{id:guid}/approve", async (
            Guid id,
            ClaimsPrincipal user,
            PresumptiveTaxService svc,
            CancellationToken ct) =>
        {
            var userId = user.RequiredUserId();
            var approved = await svc.ApproveAsync(id, userId, ct);
            return Results.Ok(approved);
        }).RequirePermission(Perms.PresumptiveApprove);

        // Get Minimal Financials Statement (Sec 44AA / ITR-4 Particulars)
        g.MapGet("/computations/{id:guid}/statement", async (Guid id, PresumptiveTaxService svc, CancellationToken ct) =>
        {
            var stmt = await svc.GetMinimalFinancialsStatementAsync(id, ct);
            return Results.Ok(stmt);
        }).RequirePermission(Perms.PresumptiveRead);

        // Export ITR-4 (Sugam) Official Schema JSON (Sprint 67)
        g.MapPost("/computations/{id:guid}/export-itr4", async (Guid id, ExportItrJsonRequest? req, PresumptiveTaxService svc, CancellationToken ct) =>
        {
            var result = await svc.ExportItr4JsonAsync(id, req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.PresumptiveRead);

        return app;
    }
}
