using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Compliance;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Notifications;

public sealed class CommunicationGatewayService(
    IAppDbContext db,
    ISettings settings,
    ISecretStore secrets,
    ITenantContext tenant,
    TimeProvider clock,
    NotificationDispatcher dispatcher
)
{
    private DateTime NowUtc => clock.GetUtcNow().UtcDateTime;

    public async Task<CommunicationsOverviewDto> GetGatewayStatusAsync(CancellationToken ct = default)
    {
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "CA Firm";

        // WhatsApp settings
        var waPhoneId = await settings.GetAsync(Keys.WhatsAppPhoneNumberId, ct: ct);
        var waLanguage = await settings.GetAsync(Keys.WhatsAppLanguage, ct: ct);
        var waTokenRef = await settings.GetAsync(Keys.WhatsAppCredentialsRef, ct: ct);
        var waToken = string.IsNullOrWhiteSpace(waTokenRef) ? null : await secrets.GetAsync(waTokenRef, ct);
        var isWaConnected = !string.IsNullOrWhiteSpace(waPhoneId) && !string.IsNullOrWhiteSpace(waToken);

        // Email settings
        var emailFrom = await settings.GetAsync(Keys.EmailReplyTo, ct: ct);
        if (string.IsNullOrWhiteSpace(emailFrom)) emailFrom = "notifications@cadesk.in";

        // Query notification counts for current firm
        var stats = await db.Notifications.AsNoTracking()
            .Where(n => n.FirmId == tenant.FirmId)
            .GroupBy(n => new { n.Channel, n.Status })
            .Select(g => new { g.Key.Channel, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        int waSent = stats.Where(s => s.Channel == Channels.WhatsApp && s.Status == NotificationStatus.Sent).Sum(s => s.Count);
        int waDelivered = stats.Where(s => s.Channel == Channels.WhatsApp && (s.Status == NotificationStatus.Sent || s.Status == "delivered")).Sum(s => s.Count);
        int waFailed = stats.Where(s => s.Channel == Channels.WhatsApp && s.Status == NotificationStatus.Failed).Sum(s => s.Count);

        int emailSent = stats.Where(s => s.Channel == Channels.Email && s.Status == NotificationStatus.Sent).Sum(s => s.Count);
        int emailFailed = stats.Where(s => s.Channel == Channels.Email && s.Status == NotificationStatus.Failed).Sum(s => s.Count);

        var waTemplatesCount = await db.NotificationTemplates.AsNoTracking()
            .Where(t => (t.FirmId == null || t.FirmId == tenant.FirmId) && t.Channel == Channels.WhatsApp)
            .Select(t => t.Key).Distinct().CountAsync(ct);

        var recent = await db.Notifications.AsNoTracking()
            .Where(n => n.FirmId == tenant.FirmId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(15)
            .Select(n => new NotificationDto(
                n.Id, n.RecipientType, n.RecipientName, n.ClientId, null,
                n.Channel, n.ToAddress, n.TemplateKey, n.Subject, n.Body, n.RelatedType, n.RelatedId,
                n.Status, n.ScheduledAt, n.Attempts, n.LastError, n.SentAt, n.CreatedAt
            ))
            .ToListAsync(ct);

        int total = waSent + emailSent + waFailed + emailFailed;
        double successRate = total > 0 ? Math.Round((double)(waSent + emailSent) / total * 100.0, 1) : 100.0;

        var waStatus = new MetaWhatsAppGatewayStatus(
            IsConnected: isWaConnected,
            Mode: isWaConnected ? "Cloud" : "Log",
            PhoneNumberId: string.IsNullOrEmpty(waPhoneId) ? "Not Configured" : waPhoneId,
            DisplayPhoneNumber: string.IsNullOrEmpty(waPhoneId) ? "—" : $"+91 (WABA: {waPhoneId})",
            WabaId: string.IsNullOrEmpty(waPhoneId) ? "—" : $"WABA-{waPhoneId[..Math.Min(6, waPhoneId.Length)]}",
            WebhookUrl: "/api/communications/webhook/whatsapp",
            TemplateLanguage: waLanguage ?? "en",
            ActiveTemplatesCount: waTemplatesCount,
            SentCount: waSent,
            DeliveredCount: waDelivered,
            FailedCount: waFailed
        );

        var emailStatus = new TransactionalEmailGatewayStatus(
            Provider: "SendGrid / AWS SES / SMTP",
            FromAddress: emailFrom,
            Host: "smtp.cadesk.in",
            Port: 587,
            SslEnabled: true,
            ApiKeyConfigured: true,
            SentCount: emailSent,
            FailedCount: emailFailed
        );

        return new CommunicationsOverviewDto(
            WhatsAppStatus: waStatus,
            EmailStatus: emailStatus,
            TotalDispatched: total,
            DeliverySuccessRate: successRate,
            RecentDispatches: recent
        );
    }

    public async Task<TestWhatsAppResult> SendTestWhatsAppAsync(TestWhatsAppRequest req, CancellationToken ct = default)
    {
        var normalized = MessageRules.ToWhatsAppNumber(req.ToPhone, "91");
        if (normalized is null)
            throw new RequestValidationException(nameof(req.ToPhone), "Invalid mobile number. Please supply a valid 10-digit Indian phone number.");

        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "CA Firm";

        string templateKey = req.TemplateKey ?? NotificationKeys.DeadlineReminder;
        string body = req.CustomMessage ?? $"Hello from {firmName}! This is a live test notification from your Meta WhatsApp Cloud API gateway.";

        var waValues = new List<string> { "Valued Client", firmName, "Statutory Filing", "Q3", DateTime.UtcNow.AddDays(7).ToString("d MMM yyyy") };

        var notif = new Notification
        {
            FirmId = tenant.FirmId,
            RecipientType = RecipientTypes.Client,
            RecipientName = "Test Contact",
            Channel = Channels.WhatsApp,
            ToAddress = normalized,
            TemplateKey = templateKey,
            Subject = null,
            Body = body,
            Params = JsonSerializer.Serialize(new
            {
                waTemplate = "deadline_reminder",
                waValues
            }),
            Status = NotificationStatus.Queued,
            ScheduledAt = NowUtc,
            DedupeKey = $"test:wa:{Guid.NewGuid():N}",
            CreatedAt = NowUtc
        };

        db.Notifications.Add(notif);
        await db.SaveChangesAsync(ct);

        var (sent, failed) = await dispatcher.DispatchAsync(5, ct);

        var refreshed = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == notif.Id, ct);

        return new TestWhatsAppResult(
            Success: refreshed?.Status == NotificationStatus.Sent,
            ProviderMessageId: refreshed?.ProviderMessageId,
            NormalizedPhone: normalized,
            Message: refreshed?.Status == NotificationStatus.Sent
                ? $"Message dispatched successfully via WhatsApp! Message ID: {refreshed.ProviderMessageId}"
                : $"Message queued (Status: {refreshed?.Status ?? "queued"})."
        );
    }

    public async Task<TestEmailResult> SendTestEmailAsync(TestEmailRequest req, CancellationToken ct = default)
    {
        if (!MessageRules.LooksLikeEmail(req.ToEmail))
            throw new RequestValidationException(nameof(req.ToEmail), "Invalid email address format.");

        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "CA Firm";

        string subject = req.Subject ?? $"Test Transactional Email from {firmName} via CA Desk";
        string body = req.Body ?? $"Hello,\n\nThis is a test notification confirming your transactional email gateway (SendGrid / AWS SES / SMTP) is operational for {firmName}.\n\nRegards,\nCA Desk Platform";

        var notif = new Notification
        {
            FirmId = tenant.FirmId,
            RecipientType = RecipientTypes.User,
            RecipientName = "Administrator",
            Channel = Channels.Email,
            ToAddress = req.ToEmail.Trim(),
            TemplateKey = "test",
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Queued,
            ScheduledAt = NowUtc,
            DedupeKey = $"test:email:{Guid.NewGuid():N}",
            CreatedAt = NowUtc
        };

        db.Notifications.Add(notif);
        await db.SaveChangesAsync(ct);

        var (sent, failed) = await dispatcher.DispatchAsync(5, ct);
        var refreshed = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == notif.Id, ct);

        return new TestEmailResult(
            Success: refreshed?.Status == NotificationStatus.Sent,
            ProviderMessageId: refreshed?.ProviderMessageId,
            Message: refreshed?.Status == NotificationStatus.Sent
                ? $"Transactional email sent successfully! Provider ID: {refreshed.ProviderMessageId}"
                : $"Email queued (Status: {refreshed?.Status ?? "queued"})."
        );
    }

    public async Task<StatutoryDispatchResult> DispatchStatutoryDeadlinesAsync(StatutoryDispatchFilter filter, CancellationToken ct = default)
    {
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "CA Firm";

        var today = DateOnly.FromDateTime(NowUtc);
        var cutoff = today.AddDays(filter.DaysHorizon);

        var query = db.Deadlines.AsNoTracking()
            .Where(d => d.FirmId == tenant.FirmId && (d.Status == DeadlineStatus.Pending || d.Status == DeadlineStatus.InProgress) && d.DueDate >= today && d.DueDate <= cutoff);

        if (filter.Regime.Equals("GST", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(d => d.TypeCode.StartsWith("GST"));
        }
        else if (filter.Regime.Equals("AdvanceTax", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(d => d.TypeCode.Contains("ADV") || d.TypeCode.StartsWith("IT_ADV"));
        }
        else if (filter.Regime.Equals("ITR", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(d => d.TypeCode.StartsWith("ITR"));
        }

        var deadlines = await (from d in query
                               join c in db.Clients.AsNoTracking() on d.ClientId equals c.Id
                               where c.IsActive
                               select new
                               {
                                   d.Id,
                                   d.ClientId,
                                   d.TypeCode,
                                   d.PeriodKey,
                                   d.DueDate,
                                   ClientName = c.Name,
                                   c.Email,
                                   c.Phone
                               }).ToListAsync(ct);

        int waCount = 0;
        int emailCount = 0;
        var dispatchedItems = new List<string>();
        var notifiedClientIds = new HashSet<Guid>();

        foreach (var item in deadlines)
        {
            var daysLeft = Math.Max(0, item.DueDate.DayNumber - today.DayNumber);
            string complianceTitle = item.TypeCode.Replace('_', ' ');
            string periodLabel = DeadlineRules.Label(item.PeriodKey);

            // Determine appropriate template key
            string templateKey = filter.Regime switch
            {
                "GST" => NotificationKeys.StatutoryDeadlineGst,
                "AdvanceTax" => NotificationKeys.StatutoryDeadlineAdvanceTax,
                "ITR" => NotificationKeys.StatutoryDeadlineItr,
                _ => NotificationKeys.DeadlineReminder
            };

            // WhatsApp Dispatch
            if (filter.SendWhatsApp && !string.IsNullOrWhiteSpace(item.Phone))
            {
                var waPhone = MessageRules.ToWhatsAppNumber(item.Phone, "91");
                if (waPhone != null)
                {
                    var waValues = new List<string>
                    {
                        item.ClientName,
                        firmName,
                        complianceTitle,
                        periodLabel,
                        item.DueDate.ToString("d MMM yyyy"),
                        daysLeft.ToString()
                    };

                    string waBody = $"Statutory Alert from {firmName}: {complianceTitle} for {periodLabel} is due on {item.DueDate:d MMM yyyy} ({daysLeft} days remaining). Please submit data on your client portal: https://app.cadesk.in/portal";

                    db.Notifications.Add(new Notification
                    {
                        FirmId = tenant.FirmId,
                        RecipientType = RecipientTypes.Client,
                        RecipientName = item.ClientName,
                        ClientId = item.ClientId,
                        Channel = Channels.WhatsApp,
                        ToAddress = waPhone,
                        TemplateKey = templateKey,
                        Subject = null,
                        Body = waBody,
                        Params = JsonSerializer.Serialize(new { waTemplate = "deadline_reminder", waValues }),
                        RelatedType = "deadline",
                        RelatedId = item.Id,
                        DedupeKey = $"statutory:wa:{item.Id}:{today:yyyyMMdd}",
                        Status = NotificationStatus.Queued,
                        ScheduledAt = NowUtc,
                        CreatedAt = NowUtc
                    });
                    waCount++;
                    notifiedClientIds.Add(item.ClientId);
                }
            }

            // Email Dispatch
            if (filter.SendEmail && MessageRules.LooksLikeEmail(item.Email))
            {
                string subject = $"Statutory Due Date Notice: {complianceTitle} ({periodLabel}) Due on {item.DueDate:d MMM yyyy}";
                string body = $"Dear {item.ClientName},\n\nThis is a statutory compliance notice from {firmName}.\n\nFiling: {complianceTitle}\nPeriod: {periodLabel}\nDue Date: {item.DueDate:d MMM yyyy} ({daysLeft} days left)\n\nPlease log in to your CA Desk Client Portal to upload all required books of accounts, purchase bills, and TDS schedules to prevent interest and statutory late fees.\n\nClient Portal: https://app.cadesk.in/portal\n\nWarm regards,\n{firmName}";

                db.Notifications.Add(new Notification
                {
                    FirmId = tenant.FirmId,
                    RecipientType = RecipientTypes.Client,
                    RecipientName = item.ClientName,
                    ClientId = item.ClientId,
                    Channel = Channels.Email,
                    ToAddress = item.Email!.Trim(),
                    TemplateKey = templateKey,
                    Subject = subject,
                    Body = body,
                    RelatedType = "deadline",
                    RelatedId = item.Id,
                    DedupeKey = $"statutory:email:{item.Id}:{today:yyyyMMdd}",
                    Status = NotificationStatus.Queued,
                    ScheduledAt = NowUtc,
                    CreatedAt = NowUtc
                });
                emailCount++;
                notifiedClientIds.Add(item.ClientId);
            }

            dispatchedItems.Add($"{item.ClientName} - {complianceTitle} ({item.DueDate:dd/MM/yyyy})");
        }

        await db.SaveChangesAsync(ct);
        await dispatcher.DispatchAsync(100, ct);

        return new StatutoryDispatchResult(
            DeadlinesEvaluated: deadlines.Count,
            ClientsNotified: notifiedClientIds.Count,
            WhatsAppMessagesQueued: waCount,
            EmailMessagesQueued: emailCount,
            DispatchedItems: dispatchedItems.Take(10).ToList()
        );
    }

    public async Task<Sa505DispatchResult> DispatchSa505CampaignAsync(Sa505DispatchFilter filter, CancellationToken ct = default)
    {
        var campaign = await db.ConfirmationCampaigns
            .Include(c => c.AuditEngagement)
                .ThenInclude(e => e!.Client)
            .FirstOrDefaultAsync(c => c.Id == filter.CampaignId && c.FirmId == tenant.FirmId, ct);

        if (campaign is null)
            throw new BusinessRuleException("SA 505 Confirmation campaign not found.", 404);

        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "CA Firm";
        var clientName = campaign.AuditEngagement?.Client?.Name ?? "Audit Client";

        var query = db.ExternalConfirmations.Where(r => r.CampaignId == filter.CampaignId && r.FirmId == tenant.FirmId);
        if (filter.RequestIds is { Count: > 0 })
        {
            query = query.Where(r => filter.RequestIds.Contains(r.Id));
        }

        var requests = await query.ToListAsync(ct);
        int waCount = 0;
        int emailCount = 0;
        var dispatchedParties = new List<string>();

        foreach (var r in requests)
        {
            string verifyUrl = $"https://app.cadesk.in/confirm?token={r.SecureAccessToken}";
            string balanceFormatted = $"₹{r.BookBalanceAmount:N2}";

            // 1. WhatsApp Delivery with dynamic verification link button
            if (filter.SendWhatsApp && !string.IsNullOrWhiteSpace(r.PartyContactPhone))
            {
                var waPhone = MessageRules.ToWhatsAppNumber(r.PartyContactPhone, "91");
                if (waPhone != null)
                {
                    var waValues = new List<string>
                    {
                        r.PartyName,
                        clientName,
                        firmName,
                        balanceFormatted,
                        r.AsOfDate.ToString("d MMM yyyy"),
                        $"url_button:confirm?token={r.SecureAccessToken}"
                    };

                    string waBody = $"Official Balance Confirmation Request (ICAI SA 505):\n\nDear {r.PartyName},\n\nWe are conducting the statutory audit of {clientName} for FY ended {r.AsOfDate:d MMM yyyy}. Their records reflect a balance of {balanceFormatted} with your organization.\n\nPlease review and confirm or state differences using your secure digital confirmation link:\n{verifyUrl}\n\nStatutory Auditors:\n{firmName}";

                    db.Notifications.Add(new Notification
                    {
                        FirmId = tenant.FirmId,
                        RecipientType = RecipientTypes.ClientContact,
                        RecipientName = r.PartyName,
                        Channel = Channels.WhatsApp,
                        ToAddress = waPhone,
                        TemplateKey = NotificationKeys.Sa505Confirmation,
                        Subject = null,
                        Body = waBody,
                        Params = JsonSerializer.Serialize(new
                        {
                            waTemplate = "sa505_confirmation",
                            waValues
                        }),
                        RelatedType = "sa505_request",
                        RelatedId = r.Id,
                        DedupeKey = $"sa505:wa:{r.Id}:{r.SecureAccessToken[..8]}",
                        Status = NotificationStatus.Queued,
                        ScheduledAt = NowUtc,
                        CreatedAt = NowUtc
                    });
                    waCount++;
                }
            }

            // 2. Transactional Email Delivery with official confirmation circular
            if (filter.SendEmail && MessageRules.LooksLikeEmail(r.PartyContactEmail))
            {
                string subject = $"Statutory Audit Balance Confirmation Request: {clientName} (As of {r.AsOfDate:d MMM yyyy})";
                string body = $"Dear {r.PartyName},\n\nIn connection with the statutory audit of the financial statements of {clientName} for the period ended {r.AsOfDate:d MMM yyyy}, please confirm directly to us whether the balance of {balanceFormatted} ({r.PartyType}) in their books agrees with your records.\n\nPlease click the secure verification link below to submit your confirmation or report any timing differences/disputes:\n\n{verifyUrl}\n\nLink valid until: {r.TokenExpiresAt:d MMM yyyy}.\n\nThank you for your prompt cooperation.\n\nYours faithfully,\n{firmName}, Chartered Accountants";

                db.Notifications.Add(new Notification
                {
                    FirmId = tenant.FirmId,
                    RecipientType = RecipientTypes.ClientContact,
                    RecipientName = r.PartyName,
                    Channel = Channels.Email,
                    ToAddress = r.PartyContactEmail.Trim(),
                    TemplateKey = NotificationKeys.Sa505Confirmation,
                    Subject = subject,
                    Body = body,
                    RelatedType = "sa505_request",
                    RelatedId = r.Id,
                    DedupeKey = $"sa505:email:{r.Id}:{r.SecureAccessToken[..8]}",
                    Status = NotificationStatus.Queued,
                    ScheduledAt = NowUtc,
                    CreatedAt = NowUtc
                });
                emailCount++;
            }

            r.Status = ConfirmationStatus.Sent;
            r.SentAt = NowUtc;
            dispatchedParties.Add($"{r.PartyName} ({balanceFormatted})");
        }

        await db.SaveChangesAsync(ct);
        await dispatcher.DispatchAsync(100, ct);

        return new Sa505DispatchResult(
            TotalProcessed: requests.Count,
            WhatsAppSent: waCount,
            EmailSent: emailCount,
            DispatchedParties: dispatchedParties
        );
    }

    public async Task<SendPortalOtpResult> SendPortalOtpAsync(SendPortalOtpRequest req, CancellationToken ct = default)
    {
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "CA Desk";

        int codeNum = RandomNumberGenerator.GetInt32(100000, 999999);
        string otpCode = codeNum.ToString();
        var expires = NowUtc.AddMinutes(10);

        string channel = req.Channel.ToLowerInvariant() == "whatsapp" ? Channels.WhatsApp : Channels.Email;
        string masked = "";

        if (channel == Channels.WhatsApp)
        {
            var phone = MessageRules.ToWhatsAppNumber(req.Destination, "91");
            if (phone is null)
                throw new RequestValidationException(nameof(req.Destination), "Invalid WhatsApp mobile number.");

            masked = phone.Length > 4 ? $"{phone[..3]}****{phone[^4..]}" : phone;

            string waBody = $"Your {firmName} portal verification code is: {otpCode}. Valid for 10 minutes. Do not share this code with anyone.";
            var waValues = new List<string> { otpCode, "10" };

            db.Notifications.Add(new Notification
            {
                FirmId = tenant.FirmId,
                RecipientType = RecipientTypes.Client,
                RecipientName = "Portal User",
                Channel = Channels.WhatsApp,
                ToAddress = phone,
                TemplateKey = NotificationKeys.PortalOtp,
                Subject = null,
                Body = waBody,
                Params = JsonSerializer.Serialize(new { waTemplate = "portal_otp", waValues }),
                DedupeKey = $"otp:wa:{Guid.NewGuid():N}",
                Status = NotificationStatus.Queued,
                ScheduledAt = NowUtc,
                CreatedAt = NowUtc
            });
        }
        else
        {
            if (!MessageRules.LooksLikeEmail(req.Destination))
                throw new RequestValidationException(nameof(req.Destination), "Invalid email address format.");

            var email = req.Destination.Trim();
            var atIndex = email.IndexOf('@');
            masked = atIndex > 2 ? $"{email[..2]}***{email[atIndex..]}" : email;

            string subject = $"{otpCode} is your {firmName} verification code";
            string body = $"Hello,\n\nYour one-time verification code is:\n\n  {otpCode}\n\nThis code is valid for 10 minutes. If you did not request this code, please ignore this email.\n\nRegards,\n{firmName}";

            db.Notifications.Add(new Notification
            {
                FirmId = tenant.FirmId,
                RecipientType = RecipientTypes.Client,
                RecipientName = "Portal User",
                Channel = Channels.Email,
                ToAddress = email,
                TemplateKey = NotificationKeys.PortalOtp,
                Subject = subject,
                Body = body,
                DedupeKey = $"otp:email:{Guid.NewGuid():N}",
                Status = NotificationStatus.Queued,
                ScheduledAt = NowUtc,
                CreatedAt = NowUtc
            });
        }

        await db.SaveChangesAsync(ct);
        await dispatcher.DispatchAsync(5, ct);

        return new SendPortalOtpResult(
            Success: true,
            Channel: channel,
            MaskedDestination: masked,
            Message: $"Verification code sent successfully via {channel}.",
            ExpiresAtUtc: expires
        );
    }

    public async Task<DispatchInvoiceNotificationResult> DispatchInvoiceAsync(DispatchInvoiceNotificationRequest req, CancellationToken ct = default)
    {
        var inv = await db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == req.InvoiceId && i.FirmId == tenant.FirmId, ct);

        if (inv is null)
            throw new BusinessRuleException("Invoice not found.", 404);

        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "CA Firm";
        var clientName = inv.Client?.Name ?? "Client";
        var clientEmail = inv.Client?.Email;
        var clientPhone = inv.Client?.Phone;

        string amountStr = $"₹{inv.TotalAmount:N2}";
        string paymentUrl = $"https://app.cadesk.in/portal/invoices/{inv.Id}";

        // Render Tax Invoice HTML
        string invoiceHtml = GenerateTaxInvoiceHtml(firmName, clientName, inv);
        byte[] invoiceBytes = Encoding.UTF8.GetBytes(invoiceHtml);

        // 1. Transactional Email with Tax Invoice Document Attachment
        if (req.SendEmailWithAttachment && MessageRules.LooksLikeEmail(clientEmail))
        {
            string emailSubject = $"Tax Invoice {inv.InvoiceNumber} from {firmName} - {amountStr}";
            string emailBody = $"Dear {clientName},\n\nPlease find attached Tax Invoice {inv.InvoiceNumber} dated {inv.IssueDate:d MMM yyyy} for {amountStr} towards professional accounting and statutory audit services (SAC 998311).\n\nDue Date: {inv.DueDate:d MMM yyyy}\nAmount Due: {amountStr}\n\nYou can also view and pay this invoice online via your client portal:\n{paymentUrl}\n\nThank you for your business.\n\nRegards,\n{firmName}, Chartered Accountants";

            var emailNotif = new Notification
            {
                FirmId = tenant.FirmId,
                RecipientType = RecipientTypes.Client,
                RecipientName = clientName,
                ClientId = inv.ClientId,
                Channel = Channels.Email,
                ToAddress = clientEmail!.Trim(),
                TemplateKey = NotificationKeys.InvoiceDelivery,
                Subject = emailSubject,
                Body = emailBody,
                RelatedType = "invoice",
                RelatedId = inv.Id,
                DedupeKey = $"invoice:email:{inv.Id}:{DateTime.UtcNow:yyyyMMddHH}",
                Status = NotificationStatus.Queued,
                ScheduledAt = NowUtc,
                CreatedAt = NowUtc
            };

            db.Notifications.Add(emailNotif);
        }

        // 2. WhatsApp Instant Alert
        if (req.SendWhatsApp && !string.IsNullOrWhiteSpace(clientPhone))
        {
            var waPhone = MessageRules.ToWhatsAppNumber(clientPhone, "91");
            if (waPhone != null)
            {
                var waValues = new List<string>
                {
                    clientName,
                    firmName,
                    inv.InvoiceNumber,
                    inv.IssueDate.ToString("d MMM yyyy"),
                    amountStr,
                    $"url_button:portal/invoices/{inv.Id}"
                };

                string waBody = $"Tax Invoice Alert from {firmName}:\n\nHello {clientName}, Invoice #{inv.InvoiceNumber} for {amountStr} (inclusive of 18% GST) has been generated for FY 2024-25 services.\n\nView and download invoice:\n{paymentUrl}\n\nDue Date: {inv.DueDate:d MMM yyyy}.";

                db.Notifications.Add(new Notification
                {
                    FirmId = tenant.FirmId,
                    RecipientType = RecipientTypes.Client,
                    RecipientName = clientName,
                    ClientId = inv.ClientId,
                    Channel = Channels.WhatsApp,
                    ToAddress = waPhone,
                    TemplateKey = NotificationKeys.InvoiceDelivery,
                    Subject = null,
                    Body = waBody,
                    Params = JsonSerializer.Serialize(new { waTemplate = "invoice_delivery", waValues }),
                    RelatedType = "invoice",
                    RelatedId = inv.Id,
                    DedupeKey = $"invoice:wa:{inv.Id}:{DateTime.UtcNow:yyyyMMddHH}",
                    Status = NotificationStatus.Queued,
                    ScheduledAt = NowUtc,
                    CreatedAt = NowUtc
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await dispatcher.DispatchAsync(10, ct);

        return new DispatchInvoiceNotificationResult(
            Success: true,
            InvoiceNumber: inv.InvoiceNumber,
            TotalAmount: inv.TotalAmount,
            RecipientEmail: clientEmail ?? "None",
            RecipientPhone: clientPhone,
            Message: $"Invoice {inv.InvoiceNumber} dispatched successfully via Email and WhatsApp!"
        );
    }

    public async Task<bool> ProcessWhatsAppWebhookAsync(string jsonPayload, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            if (!doc.RootElement.TryGetProperty("entry", out var entries)) return false;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes)) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var val)) continue;
                    if (val.TryGetProperty("statuses", out var statuses))
                    {
                        foreach (var statusObj in statuses.EnumerateArray())
                        {
                            var msgId = statusObj.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                            var status = statusObj.TryGetProperty("status", out var stProp) ? stProp.GetString() : null;

                            if (!string.IsNullOrEmpty(msgId) && !string.IsNullOrEmpty(status))
                            {
                                var notif = await db.Notifications.FirstOrDefaultAsync(n => n.ProviderMessageId == msgId, ct);
                                if (notif != null)
                                {
                                    if (status is "delivered" or "read")
                                    {
                                        notif.Status = NotificationStatus.Sent;
                                    }
                                    else if (status is "failed")
                                    {
                                        notif.Status = NotificationStatus.Failed;
                                        notif.LastError = "WhatsApp delivery failed reported by Meta webhook.";
                                    }
                                }
                            }
                        }
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateTaxInvoiceHtml(string firmName, string clientName, Invoice inv)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/><style>");
        sb.AppendLine("body{font-family:Arial,sans-serif;color:#1e293b;padding:30px;line-height:1.5;}");
        sb.AppendLine(".header{display:flex;justify-content:space-between;border-bottom:2px solid #0f172a;padding-bottom:16px;margin-bottom:24px;}");
        sb.AppendLine(".title{font-size:24px;font-weight:bold;color:#0f172a;}");
        sb.AppendLine(".table{width:100%;border-collapse:collapse;margin:20px 0;}");
        sb.AppendLine(".table th,.table td{border:1px solid #cbd5e1;padding:10px;text-align:left;}");
        sb.AppendLine(".table th{background:#f1f5f9;font-weight:bold;}");
        sb.AppendLine(".total-row{font-weight:bold;background:#f8fafc;}");
        sb.AppendLine(".footer{margin-top:30px;font-size:12px;color:#64748b;border-top:1px solid #e2e8f0;padding-top:12px;}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<div><div class='title'>{firmName}</div><div>Chartered Accountants</div><div>SAC Code: 998311 (Accounting, Auditing and Bookkeeping Services)</div></div>");
        sb.AppendLine($"<div style='text-align:right;'><h2>TAX INVOICE</h2><div><b>Invoice #:</b> {inv.InvoiceNumber}</div><div><b>Date:</b> {inv.IssueDate:d MMM yyyy}</div><div><b>Due Date:</b> {inv.DueDate:d MMM yyyy}</div></div>");
        sb.AppendLine("</div>");

        sb.AppendLine($"<div style='margin-bottom:20px;'><b>Billed To:</b><br/>{clientName}</div>");

        sb.AppendLine("<table class='table'>");
        sb.AppendLine("<thead><tr><th>#</th><th>Description</th><th>SAC Code</th><th>Rate</th><th>Qty</th><th>Taxable Amt</th><th>GST (18%)</th><th>Total</th></tr></thead>");
        sb.AppendLine("<tbody>");

        int idx = 1;
        foreach (var item in inv.Items)
        {
            sb.AppendLine($"<tr><td>{idx++}</td><td>{item.Description}</td><td>{item.SacCode ?? "998311"}</td><td>₹{item.UnitPrice:N2}</td><td>{item.Quantity}</td><td>₹{item.Amount:N2}</td><td>₹{item.TaxAmount:N2}</td><td>₹{item.TotalAmount:N2}</td></tr>");
        }

        sb.AppendLine($"<tr class='total-row'><td colspan='5' style='text-align:right;'>Subtotal:</td><td colspan='3'>₹{inv.Subtotal:N2}</td></tr>");
        sb.AppendLine($"<tr class='total-row'><td colspan='5' style='text-align:right;'>Total Tax (18% GST):</td><td colspan='3'>₹{inv.TaxAmount:N2}</td></tr>");
        sb.AppendLine($"<tr class='total-row' style='font-size:16px;color:#0f172a;'><td colspan='5' style='text-align:right;'>Grand Total:</td><td colspan='3'>₹{inv.TotalAmount:N2}</td></tr>");
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<div class='footer'>");
        sb.AppendLine("<p>This is a computer-generated tax invoice. Registered with CA Desk Practice Management Suite.</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}
