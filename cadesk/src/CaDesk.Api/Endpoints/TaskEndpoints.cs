using CaDesk.Api.Auth;
using CaDesk.Application.Tasks;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public sealed record VersionRequest(uint? Version);
public sealed record CancelTaskRequest(string? Reason, uint? Version);
public sealed record ChecklistItemRequest(bool? Done, string? Text);
public sealed record CommentRequest(string Body);

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tasks").WithTags("Tasks");
        static IResult Found(TaskDetail? t) => t is null ? Results.NotFound() : Results.Ok(t);

        // assignee: me | unassigned | all | <user id>; status: active (default) | overdue | all | open | in_progress | in_review ...
        g.MapGet("/", async (string? assignee, bool? toReview, Guid? clientId, string? status, DateOnly? dueTo, string? search,
                int? skip, int? take, TaskService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(new TaskQuery(assignee, toReview ?? false, clientId, status, dueTo, search, skip ?? 0, take ?? 100), ct)))
            .RequirePermission(Perms.TasksRead);

        g.MapGet("/workload", async (TaskService svc, CancellationToken ct) => Results.Ok(await svc.WorkloadAsync(ct)))
            .RequirePermission(Perms.TasksRead);

        g.MapGet("/{id:guid}", async (Guid id, TaskService svc, CancellationToken ct) => Found(await svc.GetAsync(id, ct)))
            .RequirePermission(Perms.TasksRead);

        g.MapPost("/", async (SaveTaskRequest r, TaskService svc, CancellationToken ct) =>
        {
            var t = await svc.CreateAsync(r, ct);
            return Results.Created($"/api/tasks/{t.Task.Id}", t);
        }).RequirePermission(Perms.TasksManage);

        g.MapPut("/{id:guid}", async (Guid id, SaveTaskRequest r, TaskService svc, CancellationToken ct) => Found(await svc.UpdateAsync(id, r, ct)))
            .RequirePermission(Perms.TasksManage);

        // ---- flow
        g.MapPost("/{id:guid}/start", async (Guid id, VersionRequest? r, TaskService svc, CancellationToken ct) =>
            Found(await svc.StartAsync(id, r?.Version, ct))).RequirePermission(Perms.TasksManage);
        g.MapPost("/{id:guid}/complete", async (Guid id, CompleteTaskRequest r, TaskService svc, CancellationToken ct) =>
            Found(await svc.CompleteAsync(id, r, ct))).RequirePermission(Perms.TasksManage);
        g.MapPost("/{id:guid}/review", async (Guid id, ReviewTaskRequest r, TaskService svc, CancellationToken ct) =>
            Found(await svc.ReviewAsync(id, r, ct))).RequirePermission(Perms.TasksReview);
        g.MapPost("/{id:guid}/reopen", async (Guid id, VersionRequest? r, TaskService svc, CancellationToken ct) =>
            Found(await svc.ReopenAsync(id, r?.Version, ct))).RequirePermission(Perms.TasksManage);
        g.MapPost("/{id:guid}/cancel", async (Guid id, CancelTaskRequest r, TaskService svc, CancellationToken ct) =>
            Found(await svc.CancelAsync(id, r.Reason, r.Version, ct))).RequirePermission(Perms.TasksManage);

        // ---- checklist and comments
        g.MapPost("/{id:guid}/checklist", async (Guid id, ChecklistItemRequest r, TaskService svc, CancellationToken ct) =>
            Found(await svc.AddItemAsync(id, r.Text ?? "", ct))).RequirePermission(Perms.TasksManage);
        g.MapPut("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, ChecklistItemRequest r, TaskService svc, CancellationToken ct) =>
            Found(await svc.SetItemAsync(id, itemId, r.Done ?? false, ct))).RequirePermission(Perms.TasksManage);
        g.MapDelete("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, TaskService svc, CancellationToken ct) =>
            Found(await svc.RemoveItemAsync(id, itemId, ct))).RequirePermission(Perms.TasksManage);
        g.MapPost("/{id:guid}/comments", async (Guid id, CommentRequest r, TaskService svc, CancellationToken ct) =>
            Found(await svc.AddCommentAsync(id, r.Body, ct))).RequirePermission(Perms.TasksRead);

        // ---- checklist templates (the firm's own version per compliance type)
        var t = app.MapGroup("/api/task-templates").WithTags("Tasks");
        t.MapGet("/", async (TaskService svc, CancellationToken ct) => Results.Ok(await svc.TemplatesAsync(ct)))
            .RequirePermission(Perms.TasksRead);
        t.MapPut("/{typeCode}", async (string typeCode, SaveTemplateRequest r, TaskService svc, CancellationToken ct) =>
            Results.Ok(await svc.SaveTemplateAsync(typeCode, r, ct))).RequirePermission(Perms.SettingsManage);
        t.MapDelete("/{typeCode}", async (string typeCode, TaskService svc, CancellationToken ct) =>
            await svc.ResetTemplateAsync(typeCode, ct) ? Results.NoContent() : Results.NotFound()).RequirePermission(Perms.SettingsManage);

        return app;
    }
}
