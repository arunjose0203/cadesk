using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Subscriptions;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/subscriptions").WithTags("Subscriptions");

        // 1. Get current firm subscription status, limits, trial countdown
        g.MapGet("/status", async (SubscriptionService svc, CancellationToken ct) =>
        {
            var status = await svc.GetSubscriptionStatusAsync(ct);
            return Results.Ok(status);
        });

        // 2. Get available pricing plans and features
        g.MapGet("/plans", (SubscriptionService svc) =>
        {
            var plans = svc.GetAvailablePlans();
            return Results.Ok(plans);
        });

        // 3. Upgrade / change plan tier (simulates Razorpay order & issues GST tax invoice)
        g.MapPost("/upgrade", async (UpgradePlanRequest req, SubscriptionService svc, CancellationToken ct) =>
        {
            var result = await svc.UpgradePlanAsync(req, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.SettingsManage);

        // 4. Get invoice history
        g.MapGet("/invoices", async (SubscriptionService svc, CancellationToken ct) =>
        {
            var invoices = await svc.GetSubscriptionInvoicesAsync(ct);
            return Results.Ok(invoices);
        }).RequirePermission(Perms.SettingsManage);

        // 5. 1-Click Demo Firm Seeder (pre-loads Apex Global, Bharat Steel, Vanguard Diagnostics)
        g.MapPost("/seed-demo", async (ClaimsPrincipal user, SubscriptionService svc, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await svc.SeedDemoDataAsync(userId, ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.SettingsManage);

        // 6. Purge Demo Data
        g.MapPost("/purge-demo", async (SubscriptionService svc, CancellationToken ct) =>
        {
            var result = await svc.PurgeDemoDataAsync(ct);
            return Results.Ok(result);
        }).RequirePermission(Perms.SettingsManage);

        return app;
    }
}
