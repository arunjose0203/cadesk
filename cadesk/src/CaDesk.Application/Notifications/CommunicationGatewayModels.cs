using CaDesk.Domain.Notifications;

namespace CaDesk.Application.Notifications;

public sealed record MetaWhatsAppGatewayStatus(
    bool IsConnected,
    string Mode, // "Cloud" or "Log"
    string PhoneNumberId,
    string DisplayPhoneNumber,
    string WabaId,
    string WebhookUrl,
    string TemplateLanguage,
    int ActiveTemplatesCount,
    int SentCount,
    int DeliveredCount,
    int FailedCount
);

public sealed record TransactionalEmailGatewayStatus(
    string Provider, // "SendGrid", "Ses", "Smtp", "Log"
    string FromAddress,
    string Host,
    int Port,
    bool SslEnabled,
    bool ApiKeyConfigured,
    int SentCount,
    int FailedCount
);

public sealed record CommunicationsOverviewDto(
    MetaWhatsAppGatewayStatus WhatsAppStatus,
    TransactionalEmailGatewayStatus EmailStatus,
    int TotalDispatched,
    double DeliverySuccessRate,
    IReadOnlyList<NotificationDto> RecentDispatches
);

public sealed record TestWhatsAppRequest(
    string ToPhone,
    string? TemplateKey,
    string? CustomMessage
);

public sealed record TestWhatsAppResult(
    bool Success,
    string? ProviderMessageId,
    string NormalizedPhone,
    string Message
);

public sealed record TestEmailRequest(
    string ToEmail,
    string? Subject,
    string? Body
);

public sealed record TestEmailResult(
    bool Success,
    string? ProviderMessageId,
    string Message
);

public sealed record StatutoryDispatchFilter(
    string Regime, // "GST", "AdvanceTax", "ITR", "All"
    int DaysHorizon = 30,
    bool SendWhatsApp = true,
    bool SendEmail = true
);

public sealed record StatutoryDispatchResult(
    int DeadlinesEvaluated,
    int ClientsNotified,
    int WhatsAppMessagesQueued,
    int EmailMessagesQueued,
    IReadOnlyList<string> DispatchedItems
);

public sealed record Sa505DispatchFilter(
    Guid CampaignId,
    List<Guid>? RequestIds,
    bool SendWhatsApp = true,
    bool SendEmail = true
);

public sealed record Sa505DispatchResult(
    int TotalProcessed,
    int WhatsAppSent,
    int EmailSent,
    IReadOnlyList<string> DispatchedParties
);

public sealed record SendPortalOtpRequest(
    string Destination, // Phone or Email
    string Channel // "whatsapp" or "email"
);

public sealed record SendPortalOtpResult(
    bool Success,
    string Channel,
    string MaskedDestination,
    string Message,
    DateTime ExpiresAtUtc
);

public sealed record DispatchInvoiceNotificationRequest(
    Guid InvoiceId,
    bool SendEmailWithAttachment = true,
    bool SendWhatsApp = true
);

public sealed record DispatchInvoiceNotificationResult(
    bool Success,
    string InvoiceNumber,
    decimal TotalAmount,
    string RecipientEmail,
    string? RecipientPhone,
    string Message
);
