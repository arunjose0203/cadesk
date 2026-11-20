using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Gst;
using CaDesk.Domain.Gst;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 16: GST 2A / 2B Purchase Register Reconciliation Engine.
/// Tests invoice normalization matching, tolerance thresholds, discrepancy classification,
/// batch queries, and firm-level tenant isolation.
/// </summary>
public class GstReconciliationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "GST Advisory Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Bharat Manufacturing Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Gst_ReconcileBatchLifecycle_MatchesAccuratelyAndSegregatesTaxAtRisk()
    {
        var (http, userId) = await NewFirmAsync("KPMG Tax Practice");
        var client = await CreateClientAsync(http, "Mahindra Logistics Ltd");

        var prInvoices = new List<PurchaseRegisterInvoice>
        {
            new("27AABCS1429B1ZB", "Siemens India", "SIEM/24-25/001", new DateOnly(2024, 4, 5), "regular", 200000, 18000, 18000, 0, 0),
            new("29AAACH7409R1ZX", "HCL Tech", "INV-8889", new DateOnly(2024, 4, 18), "regular", 150000, 0, 0, 27000, 0)
        };

        var gstr2BInvoices = new List<Gstr2BInvoice>
        {
            // Matched Siemens invoice with different numbering formatting
            new("27AABCS1429B1ZB", "Siemens India Ltd", "SIEM-24-25-1", new DateOnly(2024, 4, 5), "regular", 200000, 18000, 18000, 0, 0, true, null),
            // Unbooked purchase from Tata Steel in 2B
            new("27AAACT2727Q1ZW", "Tata Steel Ltd", "TS/9000", new DateOnly(2024, 4, 28), "regular", 500000, 45000, 45000, 0, 0, true, null)
        };

        var req = new RunReconciliationRequest(
            ClientId: client.Id,
            ReturnPeriod: "042024",
            FinancialYear: "2024-25",
            ToleranceAmount: 1.00m,
            PurchaseInvoices: prInvoices,
            Gstr2BInvoices: gstr2BInvoices,
            Notes: "April 2024 monthly GSTR-2B ITC reconciliation"
        );

        // 1. Run reconciliation
        var res = await http.PostAsJsonAsync("/api/gst/reconcile", req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var batch = (await res.Content.ReadFromJsonAsync<GstReconciliationBatchDto>())!;

        Assert.Equal(client.Id, batch.ClientId);
        Assert.Equal(2, batch.PurchaseRegisterCount);
        Assert.Equal(2, batch.Gstr2BCount);
        Assert.Equal(1, batch.MatchedCount);         // Siemens matched
        Assert.Equal(1, batch.MissingIn2BCount);     // HCL missing in 2B
        Assert.Equal(1, batch.MissingInBooksCount);  // Tata Steel unbooked in books

        // 2. Query batch items
        var allItems = await http.GetFromJsonAsync<List<GstReconciliationItemDto>>($"/api/gst/batches/{batch.Id}/items");
        Assert.NotNull(allItems);
        Assert.Equal(3, allItems.Count); // 1 matched, 1 missing in 2B, 1 missing in books

        // Filter by missing in 2B
        var missing2BItems = await http.GetFromJsonAsync<List<GstReconciliationItemDto>>(
            $"/api/gst/batches/{batch.Id}/items?matchStatus={GstMatchStatus.MissingIn2B}");
        Assert.NotNull(missing2BItems);
        Assert.Single(missing2BItems);
        Assert.Equal("INV-8889", missing2BItems[0].InvoiceNumber);

        // 3. Summary
        var summary = await http.GetFromJsonAsync<GstReconciliationSummaryDto>("/api/gst/summary");
        Assert.NotNull(summary);
        Assert.True(summary.TotalBatches >= 1);
        Assert.True(summary.TotalItcAtRisk >= 27000); // HCL tax at risk
    }

    [Fact]
    public async Task Gst_TenantIsolation_CrossFirmAccessBlocked()
    {
        var (firmA, _) = await NewFirmAsync("Firm A GST");
        var (firmB, _) = await NewFirmAsync("Firm B GST");
        var clientA = await CreateClientAsync(firmA, "Client Alpha GST");

        var req = new RunReconciliationRequest(
            ClientId: clientA.Id,
            ReturnPeriod: "042024",
            FinancialYear: "2024-25",
            ToleranceAmount: 1.00m,
            PurchaseInvoices: [new("27AABCS1429B1ZB", "Vendor", "INV-1", null, "regular", 10000, 900, 900, 0, 0)],
            Gstr2BInvoices: [],
            Notes: null
        );

        var resA = await firmA.PostAsJsonAsync("/api/gst/reconcile", req);
        var batchA = (await resA.Content.ReadFromJsonAsync<GstReconciliationBatchDto>())!;

        // Firm B tries to get Firm A's reconciliation batch
        var getRes = await firmB.GetAsync($"/api/gst/batches/{batchA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Firm B tries to query items of Firm A's batch
        var getItemsRes = await firmB.GetAsync($"/api/gst/batches/{batchA.Id}/items");
        var itemsB = await getItemsRes.Content.ReadFromJsonAsync<List<GstReconciliationItemDto>>();
        Assert.True(itemsB == null || itemsB.Count == 0);

        // Firm B tries to delete Firm A's batch
        var delRes = await firmB.DeleteAsync($"/api/gst/batches/{batchA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }
}
