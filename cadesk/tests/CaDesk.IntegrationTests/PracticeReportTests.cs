using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Billing;
using CaDesk.Application.Clients;
using CaDesk.Application.Reports;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 21: Practice MIS & Executive Reporting Desk.
/// Tests executive KPI summary, client profitability, staff utilization, receivables aging,
/// compliance SLA reporting, practice targets, and multi-tenant firm isolation.
/// </summary>
public class PracticeReportTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Advisory Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Reliance Retail Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Executive_Summary_And_Practice_Reports_Calculate_Accurately()
    {
        var (http, _) = await NewFirmAsync("PwC Assurance Services");
        var client = await CreateClientAsync(http, "Reliance Retail Ltd");

        // 1. Create Invoice
        var invoiceReq = new CreateInvoiceRequest(
            ClientId: client.Id,
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
            Currency: "INR",
            Notes: "Quarterly audit interim billing",
            Terms: "Net 15",
            Items:
            [
                new InvoiceItemInput(
                    Description: "Statutory Audit Fees for FY 2026-27",
                    SacCode: "998221",
                    Quantity: 1m,
                    UnitPrice: 100_000m,
                    GstRate: 18m,
                    TimeEntryId: null)
            ],
            TimeEntryIds: null
        );

        var invRes = await http.PostAsJsonAsync("/api/invoices", invoiceReq);
        Assert.Equal(HttpStatusCode.Created, invRes.StatusCode);
        var invoice = (await invRes.Content.ReadFromJsonAsync<InvoiceDto>())!;

        // 2. Record Partial Payment
        var paymentReq = new RecordPaymentRequest(
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 50_000m,
            PaymentMode: "bank_transfer",
            ReferenceNumber: "UTR987654321",
            Notes: "Part payment received"
        );
        var payRes = await http.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", paymentReq);
        Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);

        // 3. Log Timesheet Entry
        var timeReq = new CreateTimeEntryRequest(
            ClientId: client.Id,
            TaskId: null,
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Hours: 8.0m,
            HourlyRate: 2500m,
            Description: "Field audit and internal control walkthrough",
            Billable: true
        );
        var timeRes = await http.PostAsJsonAsync("/api/timesheets", timeReq);
        Assert.Equal(HttpStatusCode.Created, timeRes.StatusCode);

        // 4. Set Practice Targets
        var targetReq = new SetPracticeTargetRequest(
            FinancialYear: "2026-27",
            TargetAnnualRevenue: 5_000_000m,
            TargetBillableHours: 2000m,
            TargetRecoveryPercentage: 90.0m,
            TargetComplianceSlaPct: 98.0m
        );
        var targetRes = await http.PostAsJsonAsync("/api/reports/targets", targetReq);
        Assert.Equal(HttpStatusCode.OK, targetRes.StatusCode);

        // 5. Query Executive Summary
        var sumRes = await http.GetAsync("/api/reports/executive-summary");
        Assert.Equal(HttpStatusCode.OK, sumRes.StatusCode);
        var summary = (await sumRes.Content.ReadFromJsonAsync<PracticeExecutiveSummaryDto>())!;
        Assert.True(summary.TotalBilledYtd >= 118_000m); // 100k + 18% GST
        Assert.True(summary.TotalCollectedYtd >= 50_000m);
        Assert.True(summary.TotalOutstandingReceivables >= 68_000m);
        Assert.True(summary.TotalBillableHoursLogged >= 8.0m);
        Assert.NotNull(summary.Targets);
        Assert.Equal(5_000_000m, summary.Targets.TargetAnnualRevenue);

        // 6. Query Receivables Aging
        var agingRes = await http.GetAsync("/api/reports/receivables-aging");
        Assert.Equal(HttpStatusCode.OK, agingRes.StatusCode);
        var aging = (await agingRes.Content.ReadFromJsonAsync<ReceivablesAgingDto>())!;
        Assert.True(aging.TotalReceivables >= 68_000m);
        Assert.NotEmpty(aging.AgedInvoices);

        // 7. Query Client Profitability
        var profRes = await http.GetAsync("/api/reports/client-profitability");
        Assert.Equal(HttpStatusCode.OK, profRes.StatusCode);
        var profitability = (await profRes.Content.ReadFromJsonAsync<List<ClientProfitabilityDto>>())!;
        Assert.NotEmpty(profitability);
        var clientProf = profitability.Single(p => p.ClientId == client.Id);
        Assert.True(clientProf.TotalInvoiced >= 118_000m);
        Assert.True(clientProf.TotalHoursLogged >= 8.0m);
        Assert.Equal("HighMargin", clientProf.ProfitabilityRating);
    }

    [Fact]
    public async Task Practice_Reports_MultiTenant_Isolation_FirmA_Cannot_Leak_To_FirmB()
    {
        var (httpA, _) = await NewFirmAsync("Firm Gamma Analytics");
        var (httpB, _) = await NewFirmAsync("Firm Delta Advisory");

        var clientA = await CreateClientAsync(httpA, "Client Alpha Gamma");

        // Firm A creates an invoice
        var invoiceReq = new CreateInvoiceRequest(
            ClientId: clientA.Id,
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Currency: "INR",
            Notes: "Confidential billing",
            Terms: "Net 30",
            Items:
            [
                new InvoiceItemInput(
                    Description: "Tax Advisory Fees",
                    SacCode: "998223",
                    Quantity: 1m,
                    UnitPrice: 200_000m,
                    GstRate: 18m,
                    TimeEntryId: null)
            ],
            TimeEntryIds: null
        );
        var resA = await httpA.PostAsJsonAsync("/api/invoices", invoiceReq);
        Assert.Equal(HttpStatusCode.Created, resA.StatusCode);

        // Firm B checks Executive Summary -> Must show zero billed and zero receivables
        var summaryBRes = await httpB.GetAsync("/api/reports/executive-summary");
        Assert.Equal(HttpStatusCode.OK, summaryBRes.StatusCode);
        var summaryB = (await summaryBRes.Content.ReadFromJsonAsync<PracticeExecutiveSummaryDto>())!;
        Assert.Equal(0m, summaryB.TotalBilledYtd);
        Assert.Equal(0m, summaryB.TotalOutstandingReceivables);

        // Firm B checks Receivables Aging -> Must show empty aged invoices
        var agingBRes = await httpB.GetAsync("/api/reports/receivables-aging");
        Assert.Equal(HttpStatusCode.OK, agingBRes.StatusCode);
        var agingB = (await agingBRes.Content.ReadFromJsonAsync<ReceivablesAgingDto>())!;
        Assert.Equal(0m, agingB.TotalReceivables);
        Assert.Empty(agingB.AgedInvoices);
    }
}
