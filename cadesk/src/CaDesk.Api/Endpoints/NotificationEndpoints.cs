using CaDesk.Api.Auth;
using CaDesk.Application.Notifications;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/notifications").WithTags("Reminders").RequirePermission(Perms.SettingsManage);

        g.MapGet("/", async (string? status, string? channel, Guid? clientId, DateTime? before, int? take, NotificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(status, channel, clientId, before, take ?? 50, ct)));
        g.MapPost("/{id:guid}/retry", async (Guid id, NotificationService svc, CancellationToken ct) =>
            await svc.RetryAsync(id, ct) ? Results.NoContent() : Results.NotFound());
        g.MapPost("/{id:guid}/cancel", async (Guid id, NotificationService svc, CancellationToken ct) =>
            await svc.CancelAsync(id, ct) ? Results.NoContent() : Results.NotFound());
        g.MapPost("/test-email", async (NotificationService svc, NotificationDispatcher send, CancellationToken ct) =>
        {
            var id = await svc.QueueTestEmailAsync(ct);
            await send.DispatchAsync(20, ct);
            return Results.Ok(new { id });
        });

        // queue and send now, instead of waiting for the 5-minute job
        app.MapPost("/api/reminders/run", async (NotificationService svc, NotificationDispatcher send, CancellationToken ct) =>
        {
            var plan = await svc.PlanAsync(ct);
            var (sent, failed) = await send.DispatchAsync(500, ct);
            return Results.Ok(new RunResult(plan.Queued, sent, failed, plan.Queued - sent - failed));
        }).WithTags("Reminders").RequirePermission(Perms.SettingsManage);

        // a client's reminder history (anyone who can see the client)
        app.MapGet("/api/clients/{clientId:guid}/notifications", async (Guid clientId, NotificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(null, null, clientId, null, 50, ct, clientFacingOnly: true))).WithTags("Reminders").RequirePermission(Perms.ClientsRead);

        // ---- message templates
        var t = app.MapGroup("/api/notification-templates").WithTags("Reminders").RequirePermission(Perms.SettingsManage);
        t.MapGet("/", async (NotificationService svc, CancellationToken ct) => Results.Ok(await svc.TemplatesAsync(ct)));
        t.MapPut("/{key}/{channel}", async (string key, string channel, SaveMessageTemplateRequest r, NotificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.SaveTemplateAsync(key, channel, r, ct)));
        t.MapDelete("/{key}/{channel}", async (string key, string channel, NotificationService svc, CancellationToken ct) =>
            await svc.ResetTemplateAsync(key, channel, ct) ? Results.NoContent() : Results.NotFound());
        t.MapPost("/{key}/preview", async (string key, PreviewRequest r, NotificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.PreviewAsync(key, r, ct)));

        return app;
    }
}
