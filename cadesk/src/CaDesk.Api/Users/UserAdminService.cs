using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Users;
using CaDesk.Infrastructure.Audit;
using CaDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Users;

public sealed record UserListItem(Guid Id, string Name, string Email, bool IsActive, DateTime? LastLoginAt,
    IReadOnlyList<RoleRef> Roles, bool MfaEnabled);
public sealed record RoleRef(Guid Id, string Name);
public sealed record UpdateUserRequest(string? Name, bool? IsActive, List<Guid>? RoleIds);

public sealed record RoleItem(Guid Id, string Name, bool IsSystem, IReadOnlyList<string> Permissions, int UserCount);
public sealed record SaveRoleRequest(string Name, List<string> Permissions);

public sealed record InviteRequest(string Email, string? Name, Guid RoleId);
public sealed record InviteCreated(Guid Id, string Email, DateTime ExpiresAt, string InviteToken);
public sealed record InvitationItem(Guid Id, string Email, string? Name, string RoleName, DateTime ExpiresAt, DateTime CreatedAt);

public sealed class LoginEventItem
{
    public DateTime At { get; set; }
    public string? Email { get; set; }
    public bool Succeeded { get; set; }
    public string? Reason { get; set; }
    public string? Ip { get; set; }
}

/// <summary>
/// Users, roles and invitations inside the current firm. All queries are tenant-filtered.
/// No escalation: a caller can only grant, assign or invite with permissions they hold themselves,
/// and can't change a user who has permissions they don't have (e.g. a Partner).
/// </summary>
public sealed class UserAdminService(AppDbContext db, ITenantContext tenant, TimeProvider clock, IHttpContextAccessor http, AuditWriter audit)
{
    private const string PartnerRole = "Partner";
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private HashSet<string> CallerPermissions =>
        http.HttpContext?.User.FindAll(AppClaims.Permission).Select(c => c.Value).ToHashSet() ?? [];

    private void EnsureCallerHolds(IEnumerable<string> permissions, string what)
    {
        var mine = CallerPermissions;
        var missing = permissions.Where(p => !mine.Contains(p)).Distinct().ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException($"You can't {what}: it includes permissions you don't have ({string.Join(", ", missing)}).", 403);
    }

    private async Task<List<string>> PermissionsOfRolesAsync(IEnumerable<Guid> roleIds, CancellationToken ct)
    {
        var ids = roleIds.ToList();
        return await db.RolePermissions.Where(p => ids.Contains(p.RoleId)).Select(p => p.PermissionCode).ToListAsync(ct);
    }

    // ------------------------------------------------------------ users

    public async Task<IReadOnlyList<UserListItem>> ListUsersAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Name).ToListAsync(ct);
        var mfa = await db.UserMfa.Where(m => m.EnabledAt != null).Select(m => m.UserId).ToListAsync(ct);
        return users.Select(u => new UserListItem(u.Id, u.Name, u.Email, u.IsActive, u.LastLoginAt,
            u.UserRoles.Where(r => r.Role != null).Select(r => new RoleRef(r.RoleId, r.Role!.Name)).ToList(),
            mfa.Contains(u.Id))).ToList();
    }

    public async Task<bool> UpdateUserAsync(Guid id, UpdateUserRequest r, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return false;

        if (id != tenant.UserId)
            EnsureCallerHolds(await PermissionsOfRolesAsync(user.UserRoles.Select(ur => ur.RoleId), ct), "change this user");

        if (r.Name is { } name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new RequestValidationException("name", "Name is required.");
            user.Name = name.Trim();
        }

        if (r.IsActive is false && id == tenant.UserId)
            throw new BusinessRuleException("You can't deactivate your own account.");
        if (r.IsActive is { } active) user.IsActive = active;

        if (r.RoleIds is { } roleIds)
        {
            if (roleIds.Count == 0) throw new RequestValidationException("roleIds", "Give the user at least one role.");
            var roles = await db.Roles.Where(x => roleIds.Contains(x.Id)).ToListAsync(ct); // other firms' roles are invisible
            if (roles.Count != roleIds.Distinct().Count()) throw new RequestValidationException("roleIds", "Unknown role.");
            EnsureCallerHolds(await PermissionsOfRolesAsync(roleIds, ct), "assign this role");

            var wanted = roles.Select(x => x.Id).ToHashSet();
            db.UserRoles.RemoveRange(user.UserRoles.Where(ur => !wanted.Contains(ur.RoleId)));
            foreach (var roleId in wanted.Where(w => user.UserRoles.All(ur => ur.RoleId != w)))
                db.UserRoles.Add(new UserRole { UserId = id, RoleId = roleId });
        }

        if (user.IsActive == false || r.RoleIds is not null)
            await EnsureAnotherActivePartnerAsync(user, r.RoleIds, ct);

        await db.SaveChangesAsync(ct);

        if (!user.IsActive) // sign the user out everywhere
            await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, Now).SetProperty(x => x.RevokedReason, "deactivated"), ct);
        return true;
    }

    /// <summary>A firm must always keep at least one active Partner, or nobody could manage it.</summary>
    private async Task EnsureAnotherActivePartnerAsync(User changed, List<Guid>? newRoleIds, CancellationToken ct)
    {
        var partner = await db.Roles.FirstOrDefaultAsync(x => x.Name == PartnerRole && x.IsSystem, ct);
        if (partner is null) return;

        var stillPartner = changed.IsActive &&
            (newRoleIds is null ? changed.UserRoles.Any(ur => ur.RoleId == partner.Id) : newRoleIds.Contains(partner.Id));
        if (stillPartner) return;

        var others = await db.UserRoles.AnyAsync(ur => ur.RoleId == partner.Id && ur.UserId != changed.Id &&
            db.Users.Any(u => u.Id == ur.UserId && u.IsActive), ct);
        if (!others) throw new BusinessRuleException("The firm needs at least one active Partner.");
    }

    // ------------------------------------------------------------ roles

    public async Task<IReadOnlyList<RoleItem>> ListRolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().Include(x => x.Permissions).OrderBy(x => x.Name).ToListAsync(ct);
        var counts = await db.UserRoles.GroupBy(x => x.RoleId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return roles.Select(x => new RoleItem(x.Id, x.Name, x.IsSystem,
            x.Permissions.Select(p => p.PermissionCode).OrderBy(p => p).ToList(),
            counts.GetValueOrDefault(x.Id))).ToList();
    }

    public Task<List<Permission>> ListPermissionsAsync(CancellationToken ct) =>
        db.Permissions.AsNoTracking().OrderBy(p => p.Area).ThenBy(p => p.Code).ToListAsync(ct);

    public async Task<RoleItem> CreateRoleAsync(SaveRoleRequest r, CancellationToken ct)
    {
        await ValidateRoleAsync(r, null, ct);
        EnsureCallerHolds(r.Permissions, "create this role");
        var role = new Role { Name = r.Name.Trim() };
        db.Roles.Add(role);
        foreach (var p in r.Permissions.Distinct()) db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = p });
        await db.SaveChangesAsync(ct);
        return new RoleItem(role.Id, role.Name, false, r.Permissions.Distinct().OrderBy(p => p).ToList(), 0);
    }

    public async Task<bool> UpdateRoleAsync(Guid id, SaveRoleRequest r, CancellationToken ct)
    {
        var role = await db.Roles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (role is null) return false;
        if (role.IsSystem) throw new BusinessRuleException("Built-in roles can't be changed. Create a new role instead.");
        await ValidateRoleAsync(r, id, ct);
        EnsureCallerHolds(r.Permissions.Concat(role.Permissions.Select(p => p.PermissionCode)), "change this role");

        role.Name = r.Name.Trim();
        var wanted = r.Permissions.ToHashSet();
        db.RolePermissions.RemoveRange(role.Permissions.Where(p => !wanted.Contains(p.PermissionCode)));
        foreach (var code in wanted.Where(w => role.Permissions.All(p => p.PermissionCode != w)))
            db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionCode = code });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteRoleAsync(Guid id, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (role is null) return false;
        if (role.IsSystem) throw new BusinessRuleException("Built-in roles can't be deleted.");
        if (await db.UserRoles.AnyAsync(x => x.RoleId == id, ct))
            throw new BusinessRuleException("Move the users to another role first.");
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValidateRoleAsync(SaveRoleRequest r, Guid? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 60)
            throw new RequestValidationException("name", "Role name is required (max 60 characters).");
        if (await db.Roles.AnyAsync(x => x.Name == r.Name.Trim() && x.Id != id, ct))
            throw new RequestValidationException("name", "A role with this name already exists.");
        var known = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        if (r.Permissions is null) throw new RequestValidationException("permissions", "Choose the role's permissions.");
        var unknown = r.Permissions.Except(known).ToList();
        if (unknown.Count > 0) throw new RequestValidationException("permissions", $"Unknown permission: {string.Join(", ", unknown)}");
    }

    // ------------------------------------------------------------ invitations

    public async Task<InviteCreated> InviteAsync(InviteRequest r, CancellationToken ct)
    {
        var email = (r.Email ?? "").Trim();
        if (!AuthService.LooksLikeEmail(email)) throw new RequestValidationException("email", "Enter a valid email address.");
        if (!await db.Roles.AnyAsync(x => x.Id == r.RoleId, ct)) throw new RequestValidationException("roleId", "Unknown role.");
        EnsureCallerHolds(await PermissionsOfRolesAsync([r.RoleId], ct), "invite with this role");
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.FirmId == tenant.FirmId && u.Email == email, ct))
            throw new BusinessRuleException("This email is already used by a user of the firm (or a removed user).");

        var raw = SecureTokens.New();
        var inv = new Invitation
        {
            Email = email,
            Name = r.Name?.Trim(),
            RoleId = r.RoleId,
            TokenHash = SecureTokens.Hash(raw),
            ExpiresAt = Now.Add(InvitationLifetime),
            InvitedBy = tenant.UserId ?? throw new BusinessRuleException("Sign in again.", 401),
        };
        db.Invitations.Add(inv);
        await db.SaveChangesAsync(ct);
        // Email delivery arrives with notifications (Sprint 8); until then the partner shares the link.
        return new InviteCreated(inv.Id, inv.Email, inv.ExpiresAt, raw);
    }

    public async Task<IReadOnlyList<InvitationItem>> ListInvitationsAsync(CancellationToken ct)
    {
        var now = Now;
        var rows = await db.Invitations.AsNoTracking()
            .Where(i => i.AcceptedAt == null && i.ExpiresAt > now)
            .Join(db.Roles, i => i.RoleId, r => r.Id, (i, r) => new { i, RoleName = r.Name })
            .OrderByDescending(x => x.i.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(x => new InvitationItem(x.i.Id, x.i.Email, x.i.Name, x.RoleName, x.i.ExpiresAt, x.i.CreatedAt)).ToList();
    }

    public async Task<bool> RevokeInvitationAsync(Guid id, CancellationToken ct)
    {
        var inv = await db.Invitations.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.AcceptedAt == null, ct);
        if (inv is null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);   // delete and its audit row together
        var deleted = await db.Invitations.Where(i => i.Id == id && i.AcceptedAt == null).ExecuteDeleteAsync(ct);
        if (deleted == 0) return false;
        await audit.RecordAsync("invitations", id.ToString(), AuditAction.Revoke,
            new { inv.Email, inv.RoleId, inv.ExpiresAt }, null, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    // ------------------------------------------------------------ login history

    public async Task<IReadOnlyList<LoginEventItem>> ListLoginEventsAsync(int take, CancellationToken ct) =>
        await db.Database.SqlQuery<LoginEventItem>($"""
            select at as "At", email::text as "Email", succeeded as "Succeeded", reason as "Reason", host(ip) as "Ip"
            from login_events
            order by at desc
            limit {Math.Clamp(take, 1, 500)}
            """).ToListAsync(ct);
}
