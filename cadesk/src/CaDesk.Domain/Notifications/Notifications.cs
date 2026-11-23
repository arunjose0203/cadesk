using CaDesk.Domain.Common;

namespace CaDesk.Domain.Notifications;

/// <summary>Message text for one key and channel: platform default (FirmId null) or the firm's own version.</summary>
public class NotificationTemplate : Entity
{
    public Guid? FirmId { get; set; }
    public string Key { get; set; } = "";
    public string Channel { get; set; } = Channels.Email;
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    /// <summary>WhatsApp: the approved template's name and which placeholder fills {{1}}, {{2}} ...</summary>
    public string? WaTemplate { get; set; }
    public List<string>? WaParams { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One message in the outbox. Rendered when queued, so the log shows exactly what was sent.</summary>
public class Notification : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string RecipientType { get; set; } = "";
    public Guid? RecipientId { get; set; }
    public string? RecipientName { get; set; }
    public Guid? ClientId { get; set; }
    public string Channel { get; set; } = Channels.Email;
    public string ToAddress { get; set; } = "";
    public string TemplateKey { get; set; } = "";
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    /// <summary>JSON. WhatsApp: {"waTemplate":"...","waValues":["...",...]}.</summary>
    public string Params { get; set; } = "{}";
    public string? RelatedType { get; set; }
    public Guid? RelatedId { get; set; }
    public string DedupeKey { get; set; } = "";
    public string Status { get; set; } = NotificationStatus.Queued;
    public DateTime ScheduledAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? LastError { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class Channels
{
    public const string Email = "email";
    public const string WhatsApp = "whatsapp";
    public const string Sms = "sms";
}

public static class NotificationStatus
{
    public const string Queued = "queued";
    public const string Sending = "sending";
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string Cancelled = "cancelled";
}

public static class RecipientTypes
{
    public const string ClientContact = "client_contact";
    public const string Client = "client";
    public const string User = "user";
}

public static class NotificationKeys
{
    public const string DeadlineReminder = "client.deadline_reminder";
    public const string DeadlineOverdue = "client.deadline_overdue";
    public const string DailyDigest = "staff.daily_digest";
    public const string TaskSentBack = "staff.task_sent_back";
    public const string ReviewRequested = "staff.review_requested";
    public const string Escalation = "staff.escalation";

    // Sprint 51: Real-Time Automation & Communication Moat
    public const string StatutoryDeadlineGst = "client.statutory_gst";
    public const string StatutoryDeadlineAdvanceTax = "client.statutory_advancetax";
    public const string StatutoryDeadlineItr = "client.statutory_itr";
    public const string Sa505Confirmation = "client.sa505_confirmation";
    public const string PortalQueryAlert = "client.portal_query";
    public const string PortalOtp = "client.portal_otp";
    public const string InvoiceDelivery = "client.invoice_delivery";

    /// <summary>Placeholders each message may use; anything else is refused when a firm edits a template.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Placeholders = new Dictionary<string, string[]>
    {
        [DeadlineReminder] = ["firm", "client", "contact", "compliance", "period", "due_date", "days_left"],
        [DeadlineOverdue] = ["firm", "client", "contact", "compliance", "period", "due_date", "days_overdue"],
        [DailyDigest] = ["firm", "user", "overdue_count", "today_count", "week_count", "review_count", "task_list"],
        [TaskSentBack] = ["firm", "user", "task", "client", "comment"],
        [ReviewRequested] = ["firm", "user", "task", "client"],
        [Escalation] = ["firm", "user", "client", "compliance", "period", "due_date", "days_overdue"],
        [StatutoryDeadlineGst] = ["firm", "client", "contact", "compliance", "period", "due_date", "days_left", "portal_url"],
        [StatutoryDeadlineAdvanceTax] = ["firm", "client", "contact", "installment", "fy", "due_date", "days_left", "estimated_tax", "portal_url"],
        [StatutoryDeadlineItr] = ["firm", "client", "contact", "ay", "due_date", "days_left", "portal_url"],
        [Sa505Confirmation] = ["firm", "client", "party_name", "as_of_date", "balance", "confirmation_url", "token_expires"],
        [PortalQueryAlert] = ["firm", "client", "user", "ticket_number", "subject", "status", "portal_url"],
        [PortalOtp] = ["firm", "otp_code", "valid_minutes"],
        [InvoiceDelivery] = ["firm", "client", "contact", "invoice_number", "invoice_date", "total_amount", "due_date", "payment_url"],
    };
}

public sealed record NotificationAttachment(string FileName, string ContentType, byte[] Data);
