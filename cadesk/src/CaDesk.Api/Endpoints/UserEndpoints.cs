using CaDesk.Api.Auth;
using CaDesk.Api.Users;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Users and roles");

        // users
        api.MapGet("/users", async (UserAdminService s, CancellationToken ct) => Results.Ok(await s.ListUsersAsync(ct)))
            .RequirePermission(Perms.UsersRead);
        api.MapPut("/users/{id:guid}", async (Guid id, UpdateUserRequest r, UserAdminService s, CancellationToken ct) =>
                await s.UpdateUserAsync(id, r, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.UsersManage);

        // roles + permission catalogue
        api.MapGet("/roles", async (UserAdminService s, CancellationToken ct) => Results.Ok(await s.ListRolesAsync(ct)))
            .RequirePermission(Perms.UsersRead);
        api.MapGet("/permissions", async (UserAdminService s, CancellationToken ct) => Results.Ok(await s.ListPermissionsAsync(ct)))
            .RequirePermission(Perms.RolesManage);
        api.MapPost("/roles", async (SaveRoleRequest r, UserAdminService s, CancellationToken ct) =>
            {
                var role = await s.CreateRoleAsync(r, ct);
                return Results.Created($"/api/roles/{role.Id}", role);
            })
            .RequirePermission(Perms.RolesManage);
        api.MapPut("/roles/{id:guid}", async (Guid id, SaveRoleRequest r, UserAdminService s, CancellationToken ct) =>
                await s.UpdateRoleAsync(id, r, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.RolesManage);
        api.MapDelete("/roles/{id:guid}", async (Guid id, UserAdminService s, CancellationToken ct) =>
                await s.DeleteRoleAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.RolesManage);

        // invitations
        api.MapGet("/invitations", async (UserAdminService s, CancellationToken ct) => Results.Ok(await s.ListInvitationsAsync(ct)))
            .RequirePermission(Perms.UsersManage);
        api.MapPost("/invitations", async (InviteRequest r, UserAdminService s, CancellationToken ct) =>
                Results.Ok(await s.InviteAsync(r, ct)))
            .RequirePermission(Perms.UsersManage);
        api.MapDelete("/invitations/{id:guid}", async (Guid id, UserAdminService s, CancellationToken ct) =>
                await s.RevokeInvitationAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.UsersManage);

        // login history
        api.MapGet("/login-events", async (int? take, UserAdminService s, CancellationToken ct) =>
                Results.Ok(await s.ListLoginEventsAsync(take ?? 100, ct)))
            .RequirePermission(Perms.AuditRead);

        return app;
    }
}
