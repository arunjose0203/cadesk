using System.Security.Claims;
using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Portal;
using CaDesk.Domain.Portal;
using CaDesk.Domain.Users;
using CaDesk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Endpoints;

public static class ClientPortalEndpoints
{
    private sealed record PortalLoginCandidate(
        Guid Id,
        Guid FirmId,
        Guid ClientId,
        string Email,
        string Name,
        string? Phone,
        string PasswordHash,
        string Role,
        bool IsActive,
        string ClientName
    );

    public static IEndpointRouteBuilder MapClientPortalEndpoints(this IEndpointRouteBuilder app)
    {
        // ---------------------------------------------------------------- External Portal Auth (Public / Client)
        var portalAuth = app.MapGroup("/api/portal/auth").WithTags("Client Portal Auth").AllowAnonymous();

        portalAuth.MapPost("/login", async (PortalLoginRequest req, ClientPortalService portalSvc, TokenIssuer tokens, TenantContext tenant, IAppDbContext db, CancellationToken ct) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var candidates = await db.Database.SqlQuery<PortalLoginCandidate>($"""
                select id as "Id", firm_id as "FirmId", client_id as "ClientId", email as "Email",
                       name as "Name", phone as "Phone", password_hash as "PasswordHash",
                       role as "Role", is_active as "IsActive", client_name as "ClientName"
                from auth_find_portal_user({email})
                """).ToListAsync(ct);

            var user = candidates.FirstOrDefault();
            if (user is null || !ClientPortalService.VerifyPassword(req.Password, user.PasswordHash))
                throw new BusinessRuleException("Invalid client portal credentials.", 401);

            if (!user.IsActive)
                throw new BusinessRuleException("This client portal account is deactivated. Please contact your CA firm.", 403);

            // Set tenant so RLS and query filters are activated
            tenant.Set(user.FirmId, user.Id, null);

            var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == user.ClientId, ct);
            var clientSummary = new ClientSummaryDto(
                user.ClientId,
                client?.Code ?? "",
                user.ClientName,
                client?.EntityType ?? "Company",
                client?.TaxIds ?? new()
            );

            var accessToken = tokens.Issue(
                user.Id,
                user.FirmId,
                [user.Role, "client_portal"],
                ["portal.access", "clients.read"],
                user.Name
            );

            var userDto = new PortalUserDto(
                user.Id,
                user.ClientId,
                user.ClientName,
                user.Email,
                user.Name,
                user.Phone,
                user.Role,
                user.IsActive,
                DateTime.UtcNow,
                DateTime.UtcNow
            );

            return Results.Ok(new PortalAuthResult(
                accessToken,
                tokens.AccessTokenSeconds,
                userDto,
                clientSummary
            ));
        });

        // ---------------------------------------------------------------- Client Portal Data (External Client Users)
        var portal = app.MapGroup("/api/portal").WithTags("Client Portal");

        portal.MapGet("/auth/me", async (ClaimsPrincipal cp, IAppDbContext db, CancellationToken ct) =>
        {
            var portalUserId = GetUserId(cp);
            var user = await db.ClientPortalUsers
                .Include(x => x.Client)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == portalUserId, ct);

            if (user is null) return Results.NotFound();

            var client = user.Client!;
            var clientSummary = new ClientSummaryDto(client.Id, client.Code, client.Name, client.EntityType, client.TaxIds);
            var userDto = new PortalUserDto(user.Id, user.ClientId, client.Name, user.Email, user.Name, user.Phone, user.Role, user.IsActive, user.LastLoginAt, user.CreatedAt);

            return Results.Ok(new { user = userDto, client = clientSummary });
        });

        portal.MapGet("/dashboard", async (ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var dashboard = await svc.GetPortalDashboardAsync(clientId, ct);
            return Results.Ok(dashboard);
        });

        portal.MapGet("/filings", async (ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var filings = await svc.GetPortalFilingsAsync(clientId, ct);
            return Results.Ok(filings);
        });

        portal.MapGet("/invoices", async (ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var invoices = await svc.GetPortalInvoicesAsync(clientId, ct);
            return Results.Ok(invoices);
        });

        portal.MapGet("/dsc", async (ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var dscs = await svc.GetPortalDscsAsync(clientId, ct);
            return Results.Ok(dscs);
        });

        portal.MapGet("/requests", async (ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var requests = await svc.ListRequestsAsync(clientId: clientId, ct: ct);
            return Results.Ok(requests);
        });

        portal.MapGet("/requests/{id:guid}", async (Guid id, ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var req = await svc.GetRequestByIdAsync(id, ct);
            if (req.ClientId != clientId) return Results.NotFound();
            return Results.Ok(req);
        });

        portal.MapPost("/requests", async (CreateServiceRequestInput input, ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var portalUserId = GetUserId(cp);
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var name = cp.FindFirstValue("name") ?? "Client Contact";

            var created = await svc.CreateRequestAsync(clientId, portalUserId, input, name, ct);
            return Results.Created($"/api/portal/requests/{created.Id}", created);
        });

        portal.MapPost("/requests/{id:guid}/messages", async (Guid id, AddRequestMessageInput input, ClaimsPrincipal cp, ClientPortalService svc, IAppDbContext db, CancellationToken ct) =>
        {
            var portalUserId = GetUserId(cp);
            var clientId = await ResolveClientIdAsync(cp, db, ct);
            var name = cp.FindFirstValue("name") ?? "Client Contact";

            var existing = await svc.GetRequestByIdAsync(id, ct);
            if (existing.ClientId != clientId) return Results.NotFound();

            var updated = await svc.AddMessageAsync(id, "client", name, portalUserId, input, ct);
            return Results.Ok(updated);
        });

        // ---------------------------------------------------------------- CA Firm Staff Service Desk Endpoints
        var staffPortal = app.MapGroup("/api/portal/users").WithTags("Staff Portal Management");

        staffPortal.MapGet("/", async (Guid? clientId, ClientPortalService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPortalUsersAsync(clientId, ct))).RequirePermission(Perms.PortalRead);

        staffPortal.MapPost("/", async (CreatePortalUserRequest req, ClaimsPrincipal user, ClientPortalService svc, CancellationToken ct) =>
        {
            var staffId = GetUserId(user);
            var created = await svc.CreatePortalUserAsync(req, staffId, ct);
            return Results.Created($"/api/portal/users/{created.Id}", created);
        }).RequirePermission(Perms.PortalManage);

        staffPortal.MapPut("/{id:guid}/status", async (Guid id, bool isActive, ClientPortalService svc, CancellationToken ct) =>
            Results.Ok(await svc.SetPortalUserActiveAsync(id, isActive, ct))).RequirePermission(Perms.PortalManage);

        staffPortal.MapDelete("/{id:guid}", async (Guid id, ClientPortalService svc, CancellationToken ct) =>
            await svc.DeletePortalUserAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.PortalManage);

        // Service Desk for Staff
        var desk = app.MapGroup("/api/service-requests").WithTags("Staff Service Desk");

        desk.MapGet("/", async (Guid? clientId, string? status, string? category, int? skip, int? take, ClientPortalService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRequestsAsync(clientId, status, category, skip ?? 0, take ?? 50, ct))).RequirePermission(Perms.TasksRead);

        desk.MapGet("/{id:guid}", async (Guid id, ClientPortalService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRequestByIdAsync(id, ct))).RequirePermission(Perms.TasksRead);

        desk.MapPut("/{id:guid}/status", async (Guid id, UpdateServiceRequestStatusInput req, ClaimsPrincipal user, ClientPortalService svc, CancellationToken ct) =>
        {
            var staffId = GetUserId(user);
            return Results.Ok(await svc.UpdateRequestStatusAsync(id, req, staffId, ct));
        }).RequirePermission(Perms.ServiceRequestsManage);

        desk.MapPost("/{id:guid}/messages", async (Guid id, AddRequestMessageInput req, ClaimsPrincipal user, ClientPortalService svc, CancellationToken ct) =>
        {
            var staffId = GetUserId(user);
            var name = user.FindFirstValue("name") ?? "Firm Staff";
            return Results.Ok(await svc.AddMessageAsync(id, "staff", name, staffId, req, ct));
        }).RequirePermission(Perms.ServiceRequestsManage);

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal cp) =>
        Guid.Parse(cp.FindFirstValue("sub") ?? cp.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static async Task<Guid> ResolveClientIdAsync(ClaimsPrincipal cp, IAppDbContext db, CancellationToken ct)
    {
        var userId = GetUserId(cp);
        var user = await db.ClientPortalUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) throw new BusinessRuleException("Client portal identity not recognized.", 403);
        return user.ClientId;
    }
}
