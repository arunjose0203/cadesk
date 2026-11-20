using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Portal;

public static class PortalRole
{
    public const string ClientAdmin = "client_admin";
    public const string ClientViewer = "client_viewer";
    public static readonly string[] All = [ClientAdmin, ClientViewer];
}

public static class RequestCategory
{
    public const string TaxFiling = "tax_filing";
    public const string NoticeReply = "notice_reply";
    public const string Certification = "certification";
    public const string Advisory = "advisory";
    public const string BillingQuery = "billing_query";
    public const string General = "general";
    public static readonly string[] All = [TaxFiling, NoticeReply, Certification, Advisory, BillingQuery, General];
}

public static class RequestPriority
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Urgent = "urgent";
    public static readonly string[] All = [Low, Medium, High, Urgent];
}

public static class RequestStatus
{
    public const string Submitted = "submitted";
    public const string InProgress = "in_progress";
    public const string Resolved = "resolved";
    public const string Closed = "closed";
    public static readonly string[] All = [Submitted, InProgress, Resolved, Closed];
}

public class ClientPortalUser : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = PortalRole.ClientAdmin;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public User? CreatedByUser { get; set; }
}

public class ServiceRequest : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? PortalUserId { get; set; }
    public string RequestNumber { get; set; } = "";
    public string Category { get; set; } = RequestCategory.General;
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = RequestPriority.Medium;
    public string Status { get; set; } = RequestStatus.Submitted;
    public Guid? AssignedUserId { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public ClientPortalUser? PortalUser { get; set; }
    public User? AssignedUser { get; set; }
    public List<ServiceRequestMessage> Messages { get; set; } = [];
}

public class ServiceRequestMessage : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid RequestId { get; set; }
    public string SenderType { get; set; } = "client"; // client | staff
    public string SenderName { get; set; } = "";
    public Guid? SenderUserId { get; set; }
    public string Message { get; set; } = "";
    public Guid? AttachmentDocId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ServiceRequest? Request { get; set; }
}
