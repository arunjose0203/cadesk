using CaDesk.Api.Auth;
using CaDesk.Api.Configuration;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Configuration;
using CaDesk.Domain.Users;
using CaDesk.Infrastructure.Configuration;

namespace CaDesk.Api.Endpoints;

public sealed record SaveSecretRequest(string Value);

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- firm and client settings: partners / settings.manage
        var g = app.MapGroup("/api/settings").WithTags("Settings").RequirePermission(Perms.SettingsManage);

        g.MapGet("/", async (string? area, SettingsService s, CancellationToken ct) =>
            Results.Ok(await s.ListForFirmAsync(area, ct)));

        g.MapGet("/{key}/explain", async (string key, Guid? clientId, Guid? userId, SettingsService s, HttpContext ctx, CancellationToken ct) =>
        {
            if (userId is not null && userId != ctx.User.UserId())
                throw new BusinessRuleException("You can only see your own preferences.", 403);
            return Results.Ok(await s.ExplainAsync(key, clientId, userId, ct));
        });

        g.MapPut("/{key}", async (string key, SaveSettingRequest r, SettingsService s, SecretStore secrets, HttpContext ctx, CancellationToken ct) =>
        {
            if (string.Equals(r.Scope, "user", StringComparison.OrdinalIgnoreCase) && r.ScopeId != ctx.User.UserId())
                throw new BusinessRuleException("Users set their own preferences under /api/me/settings.", 403);
            await s.SetAsync(key, r, ctx.User.UserId(), secrets, ct);
            return Results.NoContent();
        });

        g.MapDelete("/{key}", async (string key, string? scope, Guid? scopeId, string? reason, SettingsService s,
            HttpContext ctx, CancellationToken ct) =>
        {
            if (string.Equals(scope, "user", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Users reset their own preferences under /api/me/settings.", 403);
            return await s.ResetAsync(key, scope, scopeId, ctx.User.UserId(), reason, ct) ? Results.NoContent() : Results.NotFound();
        });

        g.MapGet("/changes", async (string? key, int? take, SettingsService s, CancellationToken ct) =>
            Results.Ok(await s.ChangesAsync(key, take ?? 100, ct)));

        g.MapPost("/changes/{id:guid}/revert", async (Guid id, SettingsService s, SecretStore secrets, HttpContext ctx, CancellationToken ct) =>
            await s.RevertAsync(id, ctx.User.UserId(), secrets, ct) ? Results.NoContent() : Results.NotFound());

        // ---- secrets: write-only
        g.MapGet("/secrets", async (SecretStore secrets, CancellationToken ct) => Results.Ok(await secrets.ListAsync(ct)));

        g.MapPut("/secrets/{name}", async (string name, SaveSecretRequest r, SecretStore secrets, CancellationToken ct) =>
        {
            try { await secrets.SetAsync(name, r.Value, ct); }
            catch (ArgumentException ex) { throw new RequestValidationException(ex.ParamName ?? "value", ex.Message.Split(" (")[0]); }
            return Results.NoContent();
        });

        g.MapDelete("/secrets/{name}", async (string name, SecretStore secrets, CancellationToken ct) =>
            await secrets.DeleteAsync(name, ct) ? Results.NoContent() : Results.NotFound());

        // ---- personal preferences: any signed-in user, only their own user-level values
        var me = app.MapGroup("/api/me/settings").WithTags("Settings").RequireAuthorization(Policies.FirmMember);

        me.MapGet("/", async (SettingsService s, HttpContext ctx, CancellationToken ct) =>
        {
            var userId = ctx.User.UserId();
            var items = (await s.ListForFirmAsync(null, ct)).Where(i => i.UserOverrideAllowed).ToList();
            var result = new List<object>();
            foreach (var i in items)
            {
                var r = await s.ResolveAsync(i.Key, null, userId, ct);
                result.Add(new { i.Key, i.Label, i.Schema, Value = r.Value, Source = SettingsService.SourceName(r.Source) });
            }
            return Results.Ok(result);
        });

        me.MapPut("/{key}", async (string key, SaveSettingRequest r, SettingsService s, SecretStore secrets, HttpContext ctx, CancellationToken ct) =>
        {
            var userId = ctx.User.UserId();
            await s.SetAsync(key, r with { Scope = "user", ScopeId = userId, LockForLowerLevels = null }, userId, secrets, ct);
            return Results.NoContent();
        });

        me.MapDelete("/{key}", async (string key, SettingsService s, HttpContext ctx, CancellationToken ct) =>
        {
            var userId = ctx.User.UserId();
            return await s.ResetAsync(key, "user", userId, userId, null, ct) ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
