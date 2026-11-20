using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Billing;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class TimesheetEndpoints
{
    public static IEndpointRouteBuilder MapTimesheetEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/timesheets").WithTags("Timesheets");

        g.MapGet("/", async (Guid? userId, Guid? clientId, Guid? taskId, DateOnly? from, DateOnly? to, bool? unbilledOnly,
                int? skip, int? take, ClaimsPrincipal user, TimesheetService svc, CancellationToken ct) =>
        {
            var hasManage = user.FindAll("perm").Any(c => c.Value is Perms.TimesheetsManage or Perms.BillingManage);
            var currentUserId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // If user doesn't have timesheet management permission, restrict to their own entries
            var filterUserId = hasManage ? userId : currentUserId;

            var entries = await svc.GetTimeEntriesAsync(filterUserId, clientId, taskId, from, to, unbilledOnly, skip ?? 0, take ?? 50, ct);
            return Results.Ok(entries);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapGet("/summary", async (Guid? userId, Guid? clientId, DateOnly? from, DateOnly? to,
                ClaimsPrincipal user, TimesheetService svc, CancellationToken ct) =>
        {
            var hasManage = user.FindAll("perm").Any(c => c.Value is Perms.TimesheetsManage or Perms.BillingManage);
            var currentUserId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var filterUserId = hasManage ? userId : currentUserId;

            var summary = await svc.GetSummaryAsync(filterUserId, clientId, from, to, ct);
            return Results.Ok(summary);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapPost("/", async (CreateTimeEntryRequest req, ClaimsPrincipal user, TimesheetService svc, CancellationToken ct) =>
        {
            var currentUserId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var entry = await svc.CreateAsync(req, currentUserId, ct);
            return Results.Created($"/api/timesheets/{entry.Id}", entry);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapPut("/{id:guid}", async (Guid id, UpdateTimeEntryRequest req, ClaimsPrincipal user, TimesheetService svc, CancellationToken ct) =>
        {
            var currentUserId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var canManage = user.FindAll("perm").Any(c => c.Value is Perms.TimesheetsManage or Perms.BillingManage);
            var entry = await svc.UpdateAsync(id, req, currentUserId, canManage, ct);
            return Results.Ok(entry);
        }).RequireAuthorization(Policies.FirmMember);

        g.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, TimesheetService svc, CancellationToken ct) =>
        {
            var currentUserId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var canManage = user.FindAll("perm").Any(c => c.Value is Perms.TimesheetsManage or Perms.BillingManage);
            await svc.DeleteAsync(id, currentUserId, canManage, ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.FirmMember);

        return app;
    }
}
