using CaDesk.Api.Auth;
using CaDesk.Application.UaeTax;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class UaeCorporateTaxEndpoints
{
    public static IEndpointRouteBuilder MapUaeCorporateTaxEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/uae-tax").WithTags("UAE Corporate Tax & EmaraTax Desk");

        // 1. Tax Profiles
        g.MapGet("/profiles", async (Guid? clientId, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var list = await svc.GetProfilesAsync(clientId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.UaeCorporateTaxRead);

        g.MapGet("/profiles/{id:guid}", async (Guid id, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var profile = await svc.GetProfileByIdAsync(id, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        }).RequirePermission(Perms.UaeCorporateTaxRead);

        g.MapPost("/profiles", async (CreateOrUpdateTaxProfileRequest req, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateOrUpdateProfileAsync(req, ct);
            return Results.Ok(created);
        }).RequirePermission(Perms.UaeCorporateTaxManage);

        // 2. Real-Time Statutory Tax Calculation Engine
        g.MapPost("/calculate", (CalculateUaeTaxRequest req, IUaeCorporateTaxService svc) =>
        {
            var comp = svc.CalculateTax(req);
            return Results.Ok(comp);
        }).RequirePermission(Perms.UaeCorporateTaxRead);

        // 3. Tax Returns (Form CT01)
        g.MapPost("/returns", async (SaveUaeCorporateTaxReturnRequest req, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var ret = await svc.SaveReturnAsync(req, ct);
            return Results.Created($"/api/uae-tax/returns/{ret.Id}", ret);
        }).RequirePermission(Perms.UaeCorporateTaxManage);

        g.MapGet("/returns/{id:guid}", async (Guid id, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var ret = await svc.GetReturnByIdAsync(id, ct);
            return ret is null ? Results.NotFound() : Results.Ok(ret);
        }).RequirePermission(Perms.UaeCorporateTaxRead);

        g.MapGet("/profiles/{profileId:guid}/returns", async (Guid profileId, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var list = await svc.GetReturnsForProfileAsync(profileId, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.UaeCorporateTaxRead);

        // 4. Partner Sign-Off & EmaraTax Attestation
        g.MapPost("/returns/{id:guid}/approve", async (Guid id, ApproveUaeReturnRequest req, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var approved = await svc.ApproveReturnAsync(id, req, ct);
            return Results.Ok(approved);
        }).RequirePermission(Perms.UaeCorporateTaxFile);

        // 5. 1-Click EmaraTax XML & JSON Submission Package
        g.MapGet("/returns/{id:guid}/export-emaratax", async (Guid id, IUaeCorporateTaxService svc, CancellationToken ct) =>
        {
            var pkg = await svc.ExportEmaraTaxPackageAsync(id, ct);
            return Results.Ok(pkg);
        }).RequirePermission(Perms.UaeCorporateTaxRead);

        return app;
    }
}
