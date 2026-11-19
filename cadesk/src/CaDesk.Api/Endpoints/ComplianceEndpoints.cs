using CaDesk.Api.Auth;
using CaDesk.Application.Compliance;
using CaDesk.Application.Tasks;
using CaDesk.Domain.Users;
using CaDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Endpoints;

public sealed record AddHolidayRequest(DateOnly Date, string Name);

public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/deadlines").WithTags("Compliance");

        g.MapGet("/", async (DateOnly? from, DateOnly? to, Guid? clientId, string? type, string? status, Guid? assignedUserId,
                int? skip, int? take, DeadlineService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(new DeadlineQuery(from, to, clientId, type, status, assignedUserId, skip ?? 0, take ?? 100), ct)))
            .RequirePermission(Perms.ComplianceRead);

        g.MapPut("/{id:guid}", async (Guid id, UpdateDeadlineRequest r, DeadlineService svc, CancellationToken ct) =>
            await svc.UpdateAsync(id, r, ct) is { } d ? Results.Ok(d) : Results.NotFound())
            .RequirePermission(Perms.ComplianceManage);

        // make the task for a deadline now (normally made task.create_ahead_days before it is due)
        g.MapPost("/{id:guid}/task", async (Guid id, TaskService tasks, CancellationToken ct) =>
            await tasks.EnsureTaskForDeadlineAsync(id, ct) is { } taskId ? Results.Ok(new { taskId }) : Results.NotFound())
            .RequirePermission(Perms.TasksManage);

        // re-run generation now (it also runs daily and after client changes)
        g.MapPost("/generate", async (DeadlineService svc, CancellationToken ct) => Results.Ok(await svc.GenerateAsync(null, ct)))
            .RequirePermission(Perms.ComplianceManage);

        // ---- which compliances apply to a client
        var c = app.MapGroup("/api/clients/{clientId:guid}/compliances").WithTags("Compliance");
        c.MapGet("/", async (Guid clientId, DeadlineService svc, CancellationToken ct) =>
            await svc.ClientCompliancesAsync(clientId, ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequirePermission(Perms.ComplianceRead);
        c.MapPut("/{code}", async (Guid clientId, string code, DeadlineService.SaveClientComplianceRequest r, DeadlineService svc, CancellationToken ct) =>
            await svc.SaveClientComplianceAsync(clientId, code, r, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.ComplianceManage);
        c.MapDelete("/{code}", async (Guid clientId, string code, DeadlineService svc, CancellationToken ct) =>
            await svc.ResetClientComplianceAsync(clientId, code, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.ComplianceManage);

        // ---- rules (platform data) and holidays
        app.MapGet("/api/compliance-types", async (string? country, AppDbContext db, CancellationToken ct) =>
        {
            var q = db.ComplianceTypes.AsNoTracking().Where(t => t.IsActive);
            if (!string.IsNullOrWhiteSpace(country)) q = q.Where(t => t.CountryCode == country.Trim().ToUpper());
            var rows = await q.OrderBy(t => t.CountryCode).ThenBy(t => t.SortOrder).ToListAsync(ct);
            return Results.Ok(rows.Select(t => new
            {
                t.Code, CountryCode = t.CountryCode.Trim(), t.Name, t.Authority, t.Description, t.Frequencies, t.DefaultFrequency,
                t.ShiftOnHoliday, t.AutoApply, AppliesTo = Applicability.From(t.AppliesWhen).Describe(),
            }));
        }).WithTags("Compliance").RequirePermission(Perms.ComplianceRead);

        var h = app.MapGroup("/api/holidays").WithTags("Compliance");
        h.MapGet("/", async (int? year, DeadlineService svc, TimeProvider clock, CancellationToken ct) =>
            Results.Ok(await svc.HolidaysAsync(year ?? clock.GetUtcNow().Year, ct)))
            .RequirePermission(Perms.ComplianceRead);
        h.MapPost("/", async (AddHolidayRequest r, DeadlineService svc, CancellationToken ct) =>
        {
            var added = await svc.AddFirmHolidayAsync(r.Date, r.Name, ct);
            await svc.GenerateAsync(null, ct);                        // internal targets move off the new closed day
            return Results.Created($"/api/holidays/{added.Id}", added);
        }).RequirePermission(Perms.SettingsManage);
        h.MapDelete("/{id:guid}", async (Guid id, DeadlineService svc, CancellationToken ct) =>
        {
            if (!await svc.DeleteFirmHolidayAsync(id, ct)) return Results.NotFound();
            await svc.GenerateAsync(null, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.SettingsManage);

        return app;
    }
}
