using System.Text.Json;

namespace CaDesk.Application.Configuration;

/// <summary>A typed handle on a catalogue key, so callers can't mistype names or types.</summary>
public sealed record ConfigKey<T>(string Name);

/// <summary>Keys used in code. The catalogue (config_definitions) holds the defaults and rules.</summary>
public static class Keys
{
    public static readonly ConfigKey<string> TimeZone = new("firm.time_zone");
    public static readonly ConfigKey<int> FiscalYearStartMonth = new("firm.fiscal_year_start_month");
    public static readonly ConfigKey<int[]> WorkingDays = new("firm.working_days");
    public static readonly ConfigKey<string> DateFormat = new("firm.date_format");
    public static readonly ConfigKey<string> Language = new("ui.language");

    public static readonly ConfigKey<string> ClientCodeFormat = new("client.code_format");
    public static readonly ConfigKey<string[]> ClientEntityTypes = new("client.entity_types");
    public static readonly ConfigKey<string[]> ClientRequiredFields = new("client.required_fields");

    public static readonly ConfigKey<string[]> DocAllowedTypes = new("doc.allowed_types");
    public static readonly ConfigKey<int> DocMaxSizeMb = new("doc.max_size_mb");
    public static readonly ConfigKey<int> DocRetentionYears = new("doc.retention_years");
    public static readonly ConfigKey<bool> DocClientUploadEnabled = new("doc.client_upload_enabled");
    public static readonly ConfigKey<int> DocUploadLinkDays = new("doc.upload_link_days");
    public static readonly ConfigKey<int> DocTrashDays = new("doc.trash_days");

    public static readonly ConfigKey<bool> AiEnabled = new("ai.enabled");
    public static readonly ConfigKey<bool> AiAutoExtract = new("ai.auto_extract");
    public static readonly ConfigKey<int> AiMonthlyPageQuota = new("ai.monthly_page_quota");
    public static readonly ConfigKey<bool> AiReviewRequired = new("ai.review_required");

    public static readonly ConfigKey<int> ComplianceGenerateAheadDays = new("compliance.generate_ahead_days");
    public static readonly ConfigKey<int> ComplianceInternalOffsetDays = new("compliance.internal_due_offset_days");

    public static readonly ConfigKey<int[]> ReminderDays = new("reminder.schedule_days");
    public static readonly ConfigKey<string[]> ReminderChannels = new("reminder.channels");
    public static readonly ConfigKey<string> ReminderQuietStart = new("reminder.quiet_start");
    public static readonly ConfigKey<string> ReminderQuietEnd = new("reminder.quiet_end");
    public static readonly ConfigKey<int> ReminderEscalateAfterDays = new("reminder.escalate_after_days");
    public static readonly ConfigKey<bool> ReminderClientEnabled = new("reminder.client_enabled");
    public static readonly ConfigKey<string> StaffDigestTime = new("reminder.staff_digest_time");
    public static readonly ConfigKey<bool> StaffDailyDigest = new("notify.staff_daily_digest");
    public static readonly ConfigKey<string> EmailFromName = new("email.from_name");
    public static readonly ConfigKey<string> EmailReplyTo = new("email.reply_to");
    public static readonly ConfigKey<string> WhatsAppCredentialsRef = new("whatsapp.credentials_ref");
    public static readonly ConfigKey<string> WhatsAppPhoneNumberId = new("whatsapp.phone_number_id");
    public static readonly ConfigKey<string> WhatsAppLanguage = new("whatsapp.language");
    public static readonly ConfigKey<string> RazorpayKeyId = new("payments.razorpay.key_id");
    public static readonly ConfigKey<string> RazorpayCredentialsRef = new("payments.razorpay.credentials_ref");
    public static readonly ConfigKey<string> RazorpayWebhookSecretRef = new("payments.razorpay.webhook_secret_ref");

    public static readonly ConfigKey<string> TaskAutoAssignRule = new("task.auto_assign_rule");
    public static readonly ConfigKey<bool> TaskReviewRequired = new("task.review_required");
    public static readonly ConfigKey<int> TaskCreateAheadDays = new("task.create_ahead_days");

    public static readonly ConfigKey<string> TaxDefaultRegime = new("tax.default_regime");
    public static readonly ConfigKey<bool> TaxOverrideNeedsPartner = new("tax.override_needs_partner");

    public static readonly ConfigKey<string> InvoiceNumberFormat = new("billing.invoice_number_format");
    public static readonly ConfigKey<int> PaymentTermsDays = new("billing.payment_terms_days");
    public static readonly ConfigKey<decimal> FeeTaxRate = new("billing.fee_tax_rate");

    public static readonly ConfigKey<int> SessionTimeoutMinutes = new("security.session_timeout_min");
    public static readonly ConfigKey<int> PasswordMinLength = new("security.password_min_length");
    public static readonly ConfigKey<string[]> MfaRequiredRoles = new("security.mfa_required_roles");
    public static readonly ConfigKey<string> DataRegion = new("security.data_region");
}

/// <summary>Read settings for the current firm (optionally for a client and/or user).</summary>
public interface ISettings
{
    Task<T> GetAsync<T>(ConfigKey<T> key, Guid? clientId = null, Guid? userId = null, CancellationToken ct = default);
    Task<ResolvedValue> ResolveAsync(string key, Guid? clientId = null, Guid? userId = null, CancellationToken ct = default);
}

/// <summary>Encrypted secrets per firm. Values can be written and used by the server, never read back by users.</summary>
public interface ISecretStore
{
    Task SetAsync(string name, string plaintext, CancellationToken ct = default);
    Task<string?> GetAsync(string name, CancellationToken ct = default);
    Task<bool> DeleteAsync(string name, CancellationToken ct = default);
    Task<bool> ExistsAsync(string name, CancellationToken ct = default);
}

public static class SettingsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
