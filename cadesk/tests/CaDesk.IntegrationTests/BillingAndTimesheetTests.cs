using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Billing;
using CaDesk.Application.Clients;
using CaDesk.Domain.Billing;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 11: Billing, Invoicing & Timesheets.
/// Tests timesheet logging, invoice generation with GST, payment tracking, and strict tenant isolation.
/// </summary>
public class BillingAndTimesheetTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Billing Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Apex Traders")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Timesheet_LogAndQuerySummary_Works()
    {
        var (http, userId) = await NewFirmAsync("Timesheet Firm");
        var client = await CreateClientAsync(http, "Star Retail");

        // Log timesheet: 4.5 hours
        var logRes = await http.PostAsJsonAsync("/api/timesheets", new CreateTimeEntryRequest(
            client.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), 4.5m, 1200m, "Q2 GST Audit", true));
        Assert.Equal(HttpStatusCode.Created, logRes.StatusCode);
        var entry = (await logRes.Content.ReadFromJsonAsync<TimeEntryDto>())!;

        Assert.Equal(4.5m, entry.Hours);
        Assert.Equal(1200m, entry.HourlyRate);
        Assert.False(entry.IsBilled);

        // Check summary
        var summary = await http.GetFromJsonAsync<TimesheetSummaryDto>("/api/timesheets/summary");
        Assert.NotNull(summary);
        Assert.Equal(4.5m, summary.TotalHours);
        Assert.Equal(4.5m, summary.BillableHours);
        Assert.Equal(5400m, summary.EstimatedUnbilledValue);
    }

    [Fact]
    public async Task Invoice_CreateFromTimesheet_AndPaymentLifecycle()
    {
        var (http, userId) = await NewFirmAsync("Invoice Flow Firm");
        var client = await CreateClientAsync(http, "Delta Corp");

        // 1. Log a billable timesheet entry
        var logRes = await http.PostAsJsonAsync("/api/timesheets", new CreateTimeEntryRequest(
            client.Id, null, DateOnly.FromDateTime(DateTime.UtcNow), 5.0m, 2000m, "Corporate Tax Filing", true));
        var timeEntry = (await logRes.Content.ReadFromJsonAsync<TimeEntryDto>())!;

        // 2. Create invoice including the time entry
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var createInvRes = await http.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(
            client.Id,
            today,
            today.AddDays(15),
            "INR",
            "Payment due within 15 days",
            "Net 15",
            new List<InvoiceItemInput>
            {
                new("Advisory Consultation", "9982", 1, 5000m, 18.00m, null)
            },
            new List<Guid> { timeEntry.Id }
        ));

        Assert.Equal(HttpStatusCode.Created, createInvRes.StatusCode);
        var invoice = (await createInvRes.Content.ReadFromJsonAsync<InvoiceDto>())!;

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(2, invoice.Items.Count); // 1 manual + 1 from timesheet

        // Subtotal: 5000 + (5 * 2000 = 10000) = 15000
        // GST (18%): 2700
        // Total: 17700
        Assert.Equal(15000m, invoice.Subtotal);
        Assert.Equal(2700m, invoice.TaxAmount);
        Assert.Equal(17700m, invoice.TotalAmount);
        Assert.Equal(17700m, invoice.BalanceDue);

        // Verify timesheet is now marked as billed
        var timesheets = await http.GetFromJsonAsync<List<TimeEntryDto>>($"/api/timesheets?clientId={client.Id}");
        Assert.NotNull(timesheets);
        var updatedEntry = timesheets.First(x => x.Id == timeEntry.Id);
        Assert.True(updatedEntry.IsBilled);

        // 3. Update status to 'sent'
        var statusRes = await http.PutAsJsonAsync($"/api/invoices/{invoice.Id}/status", new InvoiceStatusUpdateRequest(InvoiceStatus.Sent));
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);

        // 4. Record partial payment (7700)
        var payRes1 = await http.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new RecordPaymentRequest(
            today, 7700m, PaymentMode.Upi, "UPI-REF-001", "Advance payment"));
        Assert.Equal(HttpStatusCode.Created, payRes1.StatusCode);

        var invAfterPay1 = await http.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoice.Id}");
        Assert.NotNull(invAfterPay1);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invAfterPay1.Status);
        Assert.Equal(7700m, invAfterPay1.PaidAmount);
        Assert.Equal(10000m, invAfterPay1.BalanceDue);

        // 5. Record remaining payment (10000)
        var payRes2 = await http.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new RecordPaymentRequest(
            today, 10000m, PaymentMode.BankTransfer, "NEFT-REF-002", "Final clearance"));
        Assert.Equal(HttpStatusCode.Created, payRes2.StatusCode);

        var invAfterPay2 = await http.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoice.Id}");
        Assert.NotNull(invAfterPay2);
        Assert.Equal(InvoiceStatus.Paid, invAfterPay2.Status);
        Assert.Equal(17700m, invAfterPay2.PaidAmount);
        Assert.Equal(0m, invAfterPay2.BalanceDue);
    }

    [Fact]
    public async Task Billing_TenantIsolation_CrossFirmAccessBlocked()
    {
        var (firmA, _) = await NewFirmAsync("Firm A Accounting");
        var (firmB, _) = await NewFirmAsync("Firm B Advisory");

        var clientA = await CreateClientAsync(firmA, "Client Alpha");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invRes = await firmA.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(
            clientA.Id, today, today.AddDays(15), "INR", null, null,
            new List<InvoiceItemInput> { new("Audit fee", "9982", 1, 20000m, 18m, null) },
            null
        ));
        var invoiceA = (await invRes.Content.ReadFromJsonAsync<InvoiceDto>())!;

        // Firm B attempts to read Firm A's invoice
        var getRes = await firmB.GetAsync($"/api/invoices/{invoiceA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Firm B attempts to pay Firm A's invoice
        var payRes = await firmB.PostAsJsonAsync($"/api/invoices/{invoiceA.Id}/payments",
            new RecordPaymentRequest(today, 1000m, PaymentMode.Cash, null, null));
        Assert.Equal(HttpStatusCode.NotFound, payRes.StatusCode);

        // Firm B list invoices does NOT show Firm A's invoice
        var listB = await firmB.GetFromJsonAsync<List<InvoiceDto>>("/api/invoices");
        Assert.NotNull(listB);
        Assert.DoesNotContain(listB, x => x.Id == invoiceA.Id);
    }
}
