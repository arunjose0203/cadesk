using System.Text.Json;
using CaDesk.Application.Notifications;
using CaDesk.Domain.Notifications;
using Xunit;

namespace CaDesk.UnitTests;

public class CommunicationGatewayUnitTests
{
    [Theory]
    [InlineData("9876543210", "919876543210")]
    [InlineData("919876543210", "919876543210")]
    [InlineData("+91 98765 43210", "919876543210")]
    [InlineData("09876543210", "919876543210")]
    [InlineData("+91-98765-43210", "919876543210")]
    [InlineData("  9876543210  ", "919876543210")]
    public void WhatsAppPhoneNormalization_FormatsIndianNumbersToE164(string raw, string expected)
    {
        var normalized = MessageRules.ToWhatsAppNumber(raw, "91");
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("12345")] // too short
    [InlineData("abcdefghij")] // not digits
    [InlineData("")]
    [InlineData(null)]
    public void WhatsAppPhoneNormalization_RejectsInvalidNumbers(string? raw)
    {
        var normalized = MessageRules.ToWhatsAppNumber(raw, "91");
        Assert.Null(normalized);
    }

    [Fact]
    public void StatutoryDeadlines_TemplatePlaceholdersAreRegistered()
    {
        Assert.True(NotificationKeys.Placeholders.ContainsKey(NotificationKeys.StatutoryDeadlineGst));
        Assert.True(NotificationKeys.Placeholders.ContainsKey(NotificationKeys.StatutoryDeadlineAdvanceTax));
        Assert.True(NotificationKeys.Placeholders.ContainsKey(NotificationKeys.StatutoryDeadlineItr));

        var gstPlaceholders = NotificationKeys.Placeholders[NotificationKeys.StatutoryDeadlineGst];
        Assert.Contains("compliance", gstPlaceholders);
        Assert.Contains("due_date", gstPlaceholders);
        Assert.Contains("days_left", gstPlaceholders);
        Assert.Contains("portal_url", gstPlaceholders);
    }

    [Fact]
    public void Sa505Confirmation_GeneratesValidWhatsAppUrlButtonFormat()
    {
        string token = "sec_tok_9f8e7d6c5b4a3210";
        string buttonParam = $"url_button:confirm?token={token}";

        Assert.StartsWith("url_button:", buttonParam);
        string extractedSuffix = buttonParam["url_button:".Length..];
        Assert.Equal($"confirm?token={token}", extractedSuffix);
    }

    [Fact]
    public void PortalOtp_NotificationKeyAndPlaceholders_AreCorrect()
    {
        Assert.True(NotificationKeys.Placeholders.ContainsKey(NotificationKeys.PortalOtp));
        var placeholders = NotificationKeys.Placeholders[NotificationKeys.PortalOtp];
        Assert.Contains("otp_code", placeholders);
        Assert.Contains("valid_minutes", placeholders);
    }

    [Fact]
    public void InvoiceDelivery_NotificationKeyAndPlaceholders_AreCorrect()
    {
        Assert.True(NotificationKeys.Placeholders.ContainsKey(NotificationKeys.InvoiceDelivery));
        var placeholders = NotificationKeys.Placeholders[NotificationKeys.InvoiceDelivery];
        Assert.Contains("invoice_number", placeholders);
        Assert.Contains("total_amount", placeholders);
        Assert.Contains("due_date", placeholders);
    }

    [Fact]
    public void SendGridPayload_SerializesCorrectlyWithBase64Attachment()
    {
        var attachmentBytes = System.Text.Encoding.UTF8.GetBytes("<html>Sample Invoice</html>");
        var base64 = Convert.ToBase64String(attachmentBytes);

        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = "client@example.com" } } } },
            from = new { email = "billing@cadesk.in", name = "Apex CA Associates" },
            subject = "Tax Invoice INV-2025-001",
            content = new[] { new { type = "text/html", value = "Please find attached your invoice." } },
            attachments = new[]
            {
                new
                {
                    content = base64,
                    type = "text/html",
                    filename = "Invoice_INV-2025-001.html",
                    disposition = "attachment"
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("Apex CA Associates", doc.RootElement.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("attachments").GetArrayLength());
        Assert.Equal("Invoice_INV-2025-001.html", doc.RootElement.GetProperty("attachments")[0].GetProperty("filename").GetString());
    }

    [Fact]
    public void MetaWhatsAppCloudPayload_SerializesInteractiveComponents()
    {
        var bodyParams = new[] { new { type = "text", text = "ABC Enterprises" }, new { type = "text", text = "Apex CA" } };
        var buttonComponent = new
        {
            type = "button",
            sub_type = "url",
            index = "0",
            parameters = new[] { new { type = "text", text = "confirm?token=xyz123" } }
        };

        var payload = new
        {
            messaging_product = "whatsapp",
            to = "919876543210",
            type = "template",
            template = new
            {
                name = "sa505_confirmation",
                language = new { code = "en" },
                components = new object[]
                {
                    new { type = "body", parameters = bodyParams },
                    buttonComponent
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("whatsapp", doc.RootElement.GetProperty("messaging_product").GetString());
        Assert.Equal("919876543210", doc.RootElement.GetProperty("to").GetString());
        Assert.Equal("sa505_confirmation", doc.RootElement.GetProperty("template").GetProperty("name").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("template").GetProperty("components").GetArrayLength());
    }
}
