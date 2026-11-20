using CaDesk.Domain.Common;

namespace CaDesk.Domain.Users;

/// <summary>A member of a firm's staff. Logs in with email + password (+ TOTP if enabled).</summary>
public class User : TenantEntity, ISoftDelete
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string? Language { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
}

/// <summary>A named set of permissions, defined per firm. System roles come from the setup template.</summary>
public class Role : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string Name { get; set; } = "";
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RolePermission> Permissions { get; set; } = new();
}

public class RolePermission : ITenantOwned
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = "";
    public Guid FirmId { get; set; }
}

public class UserRole : ITenantOwned
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid FirmId { get; set; }
    public Role? Role { get; set; }
}

/// <summary>Global catalogue; permissions are fixed in code, roles are firm data.</summary>
public class Permission
{
    public string Code { get; set; } = "";
    public string Area { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>One per issued refresh token. Only the SHA-256 hash is stored.</summary>
public class RefreshToken : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedBy { get; set; }
    public System.Net.IPAddress? CreatedIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}

public class UserMfa : ITenantOwned
{
    public Guid UserId { get; set; }
    public Guid FirmId { get; set; }
    public string SecretProtected { get; set; } = "";
    public DateTime? EnabledAt { get; set; }
    public List<string> RecoveryCodesHash { get; set; } = new();
    public long LastUsedStep { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Invitation : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public Guid RoleId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid InvitedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class Perms
{
    public const string ClientsRead = "clients.read";
    public const string ClientsWrite = "clients.write";
    public const string ClientsDelete = "clients.delete";
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string SettingsManage = "settings.manage";
    public const string AuditRead = "audit.read";
    public const string ComplianceRead = "compliance.read";
    public const string ComplianceManage = "compliance.manage";
    public const string TasksRead = "tasks.read";
    public const string TasksManage = "tasks.manage";
    public const string TasksReview = "tasks.review";
    public const string DocumentsRead = "documents.read";
    public const string DocumentsUpload = "documents.upload";
    public const string DocumentsDelete = "documents.delete";
    public const string BillingRead = "billing.read";
    public const string BillingManage = "billing.manage";
    public const string TimesheetsRead = "timesheets.read";
    public const string TimesheetsLog = "timesheets.log";
    public const string TimesheetsManage = "timesheets.manage";
    public const string DscRead = "dsc.read";
    public const string DscManage = "dsc.manage";
    public const string PortalRead = "portal.read";
    public const string PortalManage = "portal.manage";
    public const string ServiceRequestsManage = "service_requests.manage";
    public const string ComputationsRun = "computations.run";
    public const string ComputationsApprove = "computations.approve";
    public const string AuditManage = "audit.manage";
    public const string AuditReview = "audit.review";
    public const string GstRead = "gst.read";
    public const string GstReconcile = "gst.reconcile";
    public const string TdsRead = "tds.read";
    public const string TdsManage = "tds.manage";
    public const string McaRead = "mca.read";
    public const string McaManage = "mca.manage";
}
