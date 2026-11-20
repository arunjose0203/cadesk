using CaDesk.Api.Auth;
using CaDesk.Application.FixedAssets;
using CaDesk.Domain.FixedAssets;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class FixedAssetEndpoints
{
    public static IEndpointRouteBuilder MapFixedAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/fixed-assets").WithTags("Fixed Asset Register & Dual Depreciation Engine");

        // Summary KPI
        g.MapGet("/summary", async (Guid? clientId, string? financialYear, IFixedAssetService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(clientId, financialYear, ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.AssetsRead);

        // Assets Register
        g.MapGet("/", async (Guid? clientId, AssetClass? assetClass, AssetStatus? status, IFixedAssetService svc, CancellationToken ct) =>
        {
            var list = await svc.GetAssetsAsync(clientId, assetClass, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AssetsRead);

        g.MapGet("/{id:guid}", async (Guid id, IFixedAssetService svc, CancellationToken ct) =>
        {
            var item = await svc.GetAssetByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(Perms.AssetsRead);

        g.MapPost("/", async (SaveFixedAssetRequest req, IFixedAssetService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAssetAsync(req, ct);
            return Results.Created($"/api/fixed-assets/{created.Id}", created);
        }).RequirePermission(Perms.AssetsManage);

        g.MapPut("/{id:guid}", async (Guid id, SaveFixedAssetRequest req, IFixedAssetService svc, CancellationToken ct) =>
        {
            var updated = await svc.UpdateAssetAsync(id, req, ct);
            return Results.Ok(updated);
        }).RequirePermission(Perms.AssetsManage);

        g.MapPost("/{id:guid}/dispose", async (Guid id, DisposeAssetRequest req, IFixedAssetService svc, CancellationToken ct) =>
        {
            var disposed = await svc.DisposeAssetAsync(id, req, ct);
            return Results.Ok(disposed);
        }).RequirePermission(Perms.AssetsManage);

        g.MapDelete("/{id:guid}", async (Guid id, IFixedAssetService svc, CancellationToken ct) =>
        {
            await svc.DeleteAssetAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Perms.AssetsManage);

        // Dual Depreciation Engine
        g.MapPost("/depreciation", async (RunDualDepreciationRequest req, IFixedAssetService svc, CancellationToken ct) =>
        {
            var entries = await svc.RunDepreciationAsync(req, ct);
            return Results.Ok(entries);
        }).RequirePermission(Perms.AssetsDepreciate);

        g.MapGet("/depreciation", async (Guid? assetId, Guid? clientId, string? financialYear, IFixedAssetService svc, CancellationToken ct) =>
        {
            var entries = await svc.GetDepreciationEntriesAsync(assetId, clientId, financialYear, ct);
            return Results.Ok(entries);
        }).RequirePermission(Perms.AssetsRead);

        // Physical Verifications
        g.MapPost("/{id:guid}/verifications", async (Guid id, RecordPhysicalVerificationRequest req, IFixedAssetService svc, CancellationToken ct) =>
        {
            var verification = await svc.RecordVerificationAsync(id, req, ct);
            return Results.Created($"/api/fixed-assets/{id}/verifications/{verification.Id}", verification);
        }).RequirePermission(Perms.AssetsManage);

        g.MapGet("/verifications", async (Guid? assetId, Guid? clientId, IFixedAssetService svc, CancellationToken ct) =>
        {
            var list = await svc.GetVerificationsAsync(assetId, clientId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.AssetsRead);

        return app;
    }
}
