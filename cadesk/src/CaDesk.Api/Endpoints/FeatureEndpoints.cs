using CaDesk.Api.Auth;
using CaDesk.Application.Features;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class FeatureEndpoints
{
    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/features")
            .WithTags("Feature Flags")
            .RequireAuthorization(Policies.FirmMember);

        g.MapGet("", async (IFeatureFlagService featureService, CancellationToken ct) =>
        {
            var features = await featureService.GetFirmFeaturesAsync(ct);
            return Results.Ok(features);
        });

        g.MapGet("/{key}", async (string key, IFeatureFlagService featureService, CancellationToken ct) =>
        {
            var isEnabled = await featureService.IsEnabledAsync(key, ct);
            return Results.Ok(new { key, isEnabled });
        });

        g.MapPost("/{key}", async (string key, [FromBody] SetFeatureRequest req, IFeatureFlagService featureService, CancellationToken ct) =>
        {
            await featureService.SetFeatureAsync(key, req.Enabled, ct);
            return Results.Ok(new { key, isEnabled = req.Enabled });
        }).RequirePermission(Perms.SettingsManage);
    }
}

public sealed record SetFeatureRequest(bool Enabled);
