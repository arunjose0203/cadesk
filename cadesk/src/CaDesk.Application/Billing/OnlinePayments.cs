using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Billing;

// Sprint 69: clients pay invoices online through the firm's own Razorpay account.
// The firm's key secret and webhook secret live in the encrypted secret store; the
// payment link is for the invoice's balance due; Razorpay's webhook records the payment.

public sealed record RazorpayCredentials(string KeyId, string KeySecret);

public sealed record PaymentLinkRequest(
    long AmountPaise, string ReferenceId, string Description,
    string? CustomerName, string? CustomerEmail, string? CustomerContact,
    IReadOnlyDictionary<string, string> Notes);

public sealed record PaymentLinkCreated(string Id, string ShortUrl, string Status);

public sealed record PaymentLinkDto(Guid InvoiceId, string InvoiceNumber, string Url, string Status, decimal Amount);

/// <summary>A payment_link.* webhook, reduced to what CaDesk needs.</summary>
public sealed record RazorpayWebhookEvent(
    string Event, string PaymentLinkId, string? PaymentId, long AmountPaise, string? Method, DateTimeOffset? PaidAt);

public enum WebhookOutcome { Recorded, Duplicate, Ignored, BadSignature, NotConfigured, UnknownFirm }

/// <summary>Calls Razorpay. The HTTP implementation lives in the API project.</summary>
public interface IRazorpayClient
{
    Task<PaymentLinkCreated> CreatePaymentLinkAsync(RazorpayCredentials credentials, PaymentLinkRequest request, CancellationToken ct);
}

/// <summary>Pure rules, unit-tested.</summary>
public static class RazorpayRules
{
    /// <summary>Razorpay takes whole paise; INR payment links need at least 100 paise.</summary>
    public const long MinimumPaise = 100;

    public static long ToPaise(decimal rupees) => (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);

    /// <summary>
    /// X-Razorpay-Signature is the hex HMAC-SHA256 of the raw request body, keyed with the webhook secret.
    /// Compared in constant time.
    /// </summary>
    public static bool SignatureOk(string rawBody, string? signature, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrEmpty(webhookSecret)) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), Encoding.UTF8.GetBytes(rawBody));
        byte[] given;
        try { given = Convert.FromHexString(signature.Trim()); }
        catch (FormatException) { return false; }
        return given.Length == expected.Length && CryptographicOperations.FixedTimeEquals(given, expected);
    }

    /// <summary>
    /// reference_id must be unique per payment link and at most 40 characters. A new link for the same
    /// invoice (after a part payment, or once one expires) gets a new suffix.
    /// </summary>
    public static string ReferenceId(Guid invoiceId, DateTimeOffset now) =>
        $"{invoiceId:N}"[..24] + "-" + now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    /// <summary>Razorpay wants 8-14 characters with the country code; a 10-digit Indian mobile gets +91.</summary>
    public static string? Contact(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return "+91" + digits;
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal)) return "+" + digits;
        if (digits.Length == 11 && digits.StartsWith('0')) return "+91" + digits[1..];
        return digits.Length is >= 8 and <= 13 ? "+" + digits : null;
    }

    public static string PaymentLinkJson(PaymentLinkRequest r)
    {
        var body = new JsonObject
        {
            ["amount"] = r.AmountPaise,
            ["currency"] = "INR",
            ["accept_partial"] = false,
            ["reference_id"] = r.ReferenceId,
            ["description"] = r.Description.Length > 2048 ? r.Description[..2048] : r.Description,
            // The firm shares the link itself (WhatsApp, e-mail), so Razorpay sends nothing.
            ["notify"] = new JsonObject { ["sms"] = false, ["email"] = false },
            ["reminder_enable"] = false,
        };
        var customer = new JsonObject();
        if (!string.IsNullOrWhiteSpace(r.CustomerName)) customer["name"] = r.CustomerName;
        if (!string.IsNullOrWhiteSpace(r.CustomerEmail)) customer["email"] = r.CustomerEmail;
        if (!string.IsNullOrWhiteSpace(r.CustomerContact)) customer["contact"] = r.CustomerContact;
        if (customer.Count > 0) body["customer"] = customer;
        if (r.Notes.Count > 0)
        {
            var notes = new JsonObject();
            foreach (var (k, v) in r.Notes.Take(15)) notes[k] = v.Length > 255 ? v[..255] : v;
            body["notes"] = notes;
        }
        return body.ToJsonString();
    }

    public static PaymentLinkCreated? ParseCreated(string json)
    {
        try
        {
            var o = JsonNode.Parse(json)?.AsObject();
            var id = o?["id"]?.GetValue<string>();
            var url = o?["short_url"]?.GetValue<string>();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url)) return null;
            return new PaymentLinkCreated(id, url, o?["status"]?.GetValue<string>() ?? "created");
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>Razorpay errors look like {"error":{"code":"BAD_REQUEST_ERROR","description":"..."}}.</summary>
    public static string ErrorMessage(string json)
    {
        try
        {
            var d = JsonNode.Parse(json)?["error"]?["description"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(d)) return d.Length > 300 ? d[..300] : d;
        }
        catch (JsonException) { }
        catch (InvalidOperationException) { }
        return "no reason given";
    }

    public static RazorpayWebhookEvent? ParseWebhook(string json)
    {
        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            var ev = root?["event"]?.GetValue<string>();
            var link = root?["payload"]?["payment_link"]?["entity"];
            var linkId = link?["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(ev) || string.IsNullOrEmpty(linkId)) return null;
            var pay = root?["payload"]?["payment"]?["entity"];
            long amount = pay?["amount"]?.GetValue<long>() ?? link?["amount_paid"]?.GetValue<long>() ?? 0;
            long? created = pay?["created_at"]?.GetValue<long>();
            return new RazorpayWebhookEvent(ev, linkId, pay?["id"]?.GetValue<string>(), amount,
                pay?["method"]?.GetValue<string>(), created is { } c ? DateTimeOffset.FromUnixTimeSeconds(c) : null);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (FormatException) { return null; }
    }

    /// <summary>Payments are dated in India time.</summary>
    public static DateOnly IndiaDate(DateTimeOffset t) => DateOnly.FromDateTime(t.ToOffset(TimeSpan.FromHours(5.5)).DateTime);
}

public sealed class OnlinePaymentService(
    IAppDbContext db, ISettings settings, ISecretStore secrets, IRazorpayClient razorpay,
    ITenantContext tenant, ITenantSetter setTenant, TimeProvider clock)
{
    private const string NotSetUp =
        "Online payments are not set up. A partner can add the Razorpay key ID and secrets under Settings, in the Payments section.";

    /// <summary>The current link for the invoice's balance, or a new one when there is none or the balance changed.</summary>
    public async Task<PaymentLinkDto> GetOrCreateLinkAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Client).FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new BusinessRuleException("Invoice not found.", 404);
        if (invoice.Status == InvoiceStatus.Draft) throw new BusinessRuleException("Send the invoice before asking for payment.", 409);
        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid) throw new BusinessRuleException("This invoice has nothing left to pay.", 409);

        var balance = invoice.TotalAmount - invoice.PaidAmount;
        if (RazorpayRules.ToPaise(balance) < RazorpayRules.MinimumPaise)
            throw new BusinessRuleException("The balance is below Re 1, the smallest amount Razorpay accepts.", 409);

        if (invoice.PaymentLinkId is not null && invoice.PaymentLinkUrl is not null
            && invoice.PaymentLinkStatus is "created" or "partially_paid" && invoice.PaymentLinkAmount == balance)
            return new PaymentLinkDto(invoice.Id, invoice.InvoiceNumber, invoice.PaymentLinkUrl, invoice.PaymentLinkStatus!, balance);

        var credentials = await CredentialsAsync(ct) ?? throw new BusinessRuleException(NotSetUp, 409);
        var request = new PaymentLinkRequest(
            RazorpayRules.ToPaise(balance),
            RazorpayRules.ReferenceId(invoice.Id, clock.GetUtcNow()),
            $"Invoice {invoice.InvoiceNumber}",
            invoice.Client?.Name, invoice.Client?.Email, RazorpayRules.Contact(invoice.Client?.Phone),
            new Dictionary<string, string> { ["invoice_id"] = invoice.Id.ToString(), ["invoice_number"] = invoice.InvoiceNumber });
        var created = await razorpay.CreatePaymentLinkAsync(credentials, request, ct);

        invoice.PaymentLinkId = created.Id;
        invoice.PaymentLinkUrl = created.ShortUrl;
        invoice.PaymentLinkStatus = created.Status;
        invoice.PaymentLinkAmount = balance;
        await db.SaveChangesAsync(ct);
        return new PaymentLinkDto(invoice.Id, invoice.InvoiceNumber, created.ShortUrl, created.Status, balance);
    }

    /// <summary>True when the key id and key secret are both present.</summary>
    public async Task<bool> IsSetUpAsync(CancellationToken ct) => await CredentialsAsync(ct) is not null;

    private async Task<RazorpayCredentials?> CredentialsAsync(CancellationToken ct)
    {
        var keyId = await settings.GetAsync(Keys.RazorpayKeyId, ct: ct);
        var secretName = await settings.GetAsync(Keys.RazorpayCredentialsRef, ct: ct);
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(secretName)) return null;
        var secret = await secrets.GetAsync(secretName, ct);
        return string.IsNullOrEmpty(secret) ? null : new RazorpayCredentials(keyId, secret);
    }

    /// <summary>
    /// Razorpay's webhook, sent to /api/webhooks/razorpay/{firmId}. Works inside that firm (both isolation
    /// locks), checks the signature with the firm's webhook secret, and records a payment_link.paid event once.
    /// </summary>
    public async Task<WebhookOutcome> HandleWebhookAsync(Guid firmId, string rawBody, string? signature, CancellationToken ct)
    {
        setTenant.Set(firmId, null, "payment-gateway");
        if (!await db.Firms.AsNoTracking().AnyAsync(ct)) return WebhookOutcome.UnknownFirm;

        var secretName = await settings.GetAsync(Keys.RazorpayWebhookSecretRef, ct: ct);
        if (string.IsNullOrWhiteSpace(secretName)) return WebhookOutcome.NotConfigured;
        var secret = await secrets.GetAsync(secretName, ct);
        if (string.IsNullOrEmpty(secret)) return WebhookOutcome.NotConfigured;
        if (!RazorpayRules.SignatureOk(rawBody, signature, secret)) return WebhookOutcome.BadSignature;

        var ev = RazorpayRules.ParseWebhook(rawBody);
        if (ev is null) return WebhookOutcome.Ignored;
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.PaymentLinkId == ev.PaymentLinkId, ct);
        if (invoice is null) return WebhookOutcome.Ignored;

        if (ev.Event is "payment_link.expired" or "payment_link.cancelled")
        {
            invoice.PaymentLinkStatus = ev.Event == "payment_link.expired" ? "expired" : "cancelled";
            await db.SaveChangesAsync(ct);
            return WebhookOutcome.Recorded;
        }
        if (ev.Event is not ("payment_link.paid" or "payment_link.partially_paid") || string.IsNullOrEmpty(ev.PaymentId))
            return WebhookOutcome.Ignored;
        if (await db.Payments.AnyAsync(p => p.GatewayPaymentId == ev.PaymentId, ct)) return WebhookOutcome.Duplicate;

        var amount = Math.Min(ev.AmountPaise / 100m, invoice.TotalAmount - invoice.PaidAmount);
        invoice.PaymentLinkStatus = ev.Event == "payment_link.paid" ? "paid" : "partially_paid";
        if (amount > 0 && invoice.Status != InvoiceStatus.Cancelled)
        {
            db.Payments.Add(new Payment
            {
                FirmId = tenant.FirmId,
                InvoiceId = invoice.Id,
                ClientId = invoice.ClientId,
                PaymentDate = RazorpayRules.IndiaDate(ev.PaidAt ?? clock.GetUtcNow()),
                Amount = Math.Round(amount, 2),
                PaymentMode = PaymentMode.Online,
                ReferenceNumber = ev.PaymentId,
                GatewayPaymentId = ev.PaymentId,
                Notes = ev.Method is null ? "Paid online (Razorpay)" : $"Paid online (Razorpay, {ev.Method})",
            });
            invoice.PaidAmount += Math.Round(amount, 2);
            invoice.Status = invoice.PaidAmount >= invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // the same event arrived twice at once and the unique index kept one; anything else is a real failure
            if (await db.Payments.AsNoTracking().AnyAsync(p => p.GatewayPaymentId == ev.PaymentId, CancellationToken.None))
                return WebhookOutcome.Duplicate;
            throw;
        }
        return WebhookOutcome.Recorded;
    }
}
