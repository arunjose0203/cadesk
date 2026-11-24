using CaDesk.Application.Features;

namespace CaDesk.Api.Auth;

public static class FeatureAuthorizationExtensions
{
    public static RouteGroupBuilder RequireFeature(this RouteGroupBuilder builder, string featureKey)
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var featureFlags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
            if (!await featureFlags.IsEnabledAsync(featureKey, context.HttpContext.RequestAborted))
            {
                return Results.NotFound(new { error = $"Feature module '{featureKey}' is not enabled for this firm." });
            }

            return await next(context);
        });

        return builder;
    }
}
