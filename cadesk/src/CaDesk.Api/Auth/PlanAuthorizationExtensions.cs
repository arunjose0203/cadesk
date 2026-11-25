using CaDesk.Application.Subscriptions;
using CaDesk.Domain.Subscriptions;

namespace CaDesk.Api.Auth;

public static class PlanAuthorizationExtensions
{
    /// <summary>
    /// Closes a desk's API to firms whose plan does not include it (Sprint 71). Anonymous calls in the group
    /// (public portals that carry their own token) are left alone.
    /// </summary>
    public static RouteGroupBuilder RequirePlanDesk(this RouteGroupBuilder builder, string desk)
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var plans = context.HttpContext.RequestServices.GetRequiredService<PlanAccessService>();
                if (!await plans.AllowsDeskAsync(desk, context.HttpContext.RequestAborted))
                {
                    var needs = SubscriptionEngine.GetPlan(PlanDesks.RequiredTier(desk));
                    return Results.Json(new { error = $"This desk is in the {needs.DisplayName} plan. Upgrade to use it.", desk, requiredPlan = needs.TierCode },
                        statusCode: StatusCodes.Status403Forbidden);
                }
            }
            return await next(context);
        });
        return builder;
    }
}
