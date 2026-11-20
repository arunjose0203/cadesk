using CaDesk.Api.Auth;
using CaDesk.Application.Mca;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

public static class McaComplianceEndpoints
{
    public static IEndpointRouteBuilder MapMcaComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/mca").WithTags("ROC / MCA Corporate Governance & Secretarial Compliance");

        // Summary KPI
        g.MapGet("/summary", async (IMcaComplianceService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetSummaryAsync(ct);
            return Results.Ok(summary);
        }).RequirePermission(Perms.McaRead);

        // Companies
        g.MapGet("/companies", async (Guid? clientId, string? status, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var list = await svc.ListCompaniesAsync(clientId, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.McaRead);

        g.MapGet("/companies/{id:guid}", async (Guid id, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var company = await svc.GetCompanyByIdAsync(id, ct);
            return company is null ? Results.NotFound() : Results.Ok(company);
        }).RequirePermission(Perms.McaRead);

        g.MapPost("/companies", async (CreateCompanyRequest req, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateCompanyAsync(req, ct);
            return Results.Created($"/api/mca/companies/{created.Id}", created);
        }).RequirePermission(Perms.McaManage);

        // Directors & DIN
        g.MapGet("/directors", async (Guid? companyId, string? kycStatus, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var list = await svc.ListDirectorsAsync(companyId, kycStatus, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.McaRead);

        g.MapPost("/directors", async (CreateDirectorRequest req, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateDirectorAsync(req, ct);
            return Results.Created($"/api/mca/directors/{created.Id}", created);
        }).RequirePermission(Perms.McaManage);

        // Statutory Filings
        g.MapGet("/filings", async (Guid? companyId, string? financialYear, string? formType, string? status, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var list = await svc.ListFilingsAsync(companyId, financialYear, formType, status, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.McaRead);

        g.MapPost("/filings", async (CreateFilingRequest req, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateFilingAsync(req, ct);
            return Results.Created($"/api/mca/filings/{created.Id}", created);
        }).RequirePermission(Perms.McaManage);

        // Board & Committee Resolutions
        g.MapGet("/resolutions", async (Guid? companyId, string? meetingType, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var list = await svc.ListResolutionsAsync(companyId, meetingType, ct);
            return Results.Ok(list);
        }).RequirePermission(Perms.McaRead);

        g.MapPost("/resolutions", async (CreateResolutionRequest req, IMcaComplianceService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateResolutionAsync(req, ct);
            return Results.Created($"/api/mca/resolutions/{created.Id}", created);
        }).RequirePermission(Perms.McaManage);

        // Section 403 Late Fee Calculator Tool
        g.MapPost("/late-fee/calculate", (CalculateMcaLateFeeRequest req, IMcaComplianceService svc) =>
        {
            var result = svc.CalculateLateFee(req);
            return Results.Ok(result);
        }).RequirePermission(Perms.McaRead);

        return app;
    }
}
