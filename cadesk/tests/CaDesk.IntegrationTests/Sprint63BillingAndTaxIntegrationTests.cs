using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Billing;
using CaDesk.Application.Clients;
using CaDesk.Application.Tax;
using CaDesk.Domain.Billing;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 63: Correctness Integration Tests.
/// Validates:
/// 1. B2B GST tax invoice generation conforming to Section 31 and Rule 46 (Intra-State vs Inter-State)
/// 2. Printable HTML tax invoice generation
/// 3. Invoice email dispatch and status progression
/// 4. Two-lock multi-tenancy isolation on invoices and HTML views
/// 5. Income tax computation portal parity
/// </summary>
public class Sprint63BillingAndTaxIntegrationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> KeralaPan = new() { ["PAN"] = "AAACB1234F", ["GSTIN"] = "32AAACB1234F1ZI" };
    private static readonly Dictionary<string, string> KarnatakaPan = new() { ["PAN"] = "BBBCB5678G", ["GSTIN"] = "29BBBCB5678G1ZB" };

    private async Task<ClientDto> CreateClientAsync(HttpClient http, string name, Dictionary<string, string> taxIds)
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, $"{name} Private Limited", null, "Company", taxIds, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Create_IntraState_Invoice_Computes_Cgst_And_Sgst_Conforming_To_Rule46()
    {
        var auth = await api.SignUpAsync("Ernakulam Tax Associates");
        var http = api.WithToken(auth.AccessToken);

        // Client in Kerala (GSTIN starts with 32)
        var client = await CreateClientAsync(http, "Kochi Marine Hub", KeralaPan);

        var req = new CreateInvoiceRequest(
            ClientId: client.Id,
            IssueDate: new DateOnly(2026, 12, 5),
            DueDate: new DateOnly(2026, 12, 20),
            Currency: "INR",
            Notes: "Statutory Tax Audit Fees FY 2025-26",
            Terms: "Due within 15 days",
            Items:
            [
                new InvoiceItemInput(
                    Description: "Tax Audit u/s 44AB of Income Tax Act",
                    SacCode: GstInvoiceEngine.DefaultSacTaxation,
                    Quantity: 1,
                    UnitPrice: 50000m,
                    GstRate: 18m
                )
            ]
        );

        var res = await http.PostAsJsonAsync("/api/invoices", req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var inv = (await res.Content.ReadFromJsonAsync<InvoiceDto>())!;
        Assert.NotNull(inv.InvoiceNumber);
        Assert.True(inv.InvoiceNumber.Length <= 16, $"Invoice number '{inv.InvoiceNumber}' exceeds statutory 16-character limit!");
        Assert.Equal(50000m, inv.Subtotal);

        // Intra-State: 18% splits to 9% CGST (4500) + 9% SGST (4500), 0 IGST
        Assert.False(inv.IsInterState);
        Assert.Equal(4500m, inv.CgstAmount);
        Assert.Equal(4500m, inv.SgstAmount);
        Assert.Equal(0m, inv.IgstAmount);
        Assert.Equal(9000m, inv.TaxAmount);
        Assert.Equal(59000m, inv.TotalAmount);
        Assert.Equal("32", inv.SupplierStateCode);
        Assert.Equal("32", inv.RecipientStateCode);

        // Fetch Rule 46 HTML
        var htmlRes = await http.GetAsync($"/api/invoices/{inv.Id}/html");
        Assert.Equal(HttpStatusCode.OK, htmlRes.StatusCode);
        var html = await htmlRes.Content.ReadAsStringAsync();
        Assert.Contains("TAX INVOICE", html);
        Assert.Contains("Rule 46", html);
        Assert.Contains(inv.InvoiceNumber, html);
        Assert.Contains("Rupees Fifty-Nine Thousand Only", html);

        // Send email dispatch
        var emailRes = await http.PostAsync($"/api/invoices/{inv.Id}/send-email", null);
        Assert.Equal(HttpStatusCode.OK, emailRes.StatusCode);
        var sentInv = (await emailRes.Content.ReadFromJsonAsync<InvoiceDto>())!;
        Assert.Equal(InvoiceStatus.Sent, sentInv.Status);
    }

    [Fact]
    public async Task Create_InterState_Invoice_Computes_Igst_Only()
    {
        var auth = await api.SignUpAsync("Calicut Audit Partners");
        var http = api.WithToken(auth.AccessToken);

        // Client in Karnataka (GSTIN starts with 29)
        var client = await CreateClientAsync(http, "Bangalore Cloud Systems", KarnatakaPan);

        var req = new CreateInvoiceRequest(
            ClientId: client.Id,
            IssueDate: new DateOnly(2026, 12, 5),
            DueDate: new DateOnly(2026, 12, 20),
            Currency: "INR",
            Notes: "Accounting Retainer",
            Terms: "Net 30",
            Items:
            [
                new InvoiceItemInput(
                    Description: "IFRS Financial Statements Compilation",
                    SacCode: GstInvoiceEngine.DefaultSacAccounting,
                    Quantity: 1,
                    UnitPrice: 80000m,
                    GstRate: 18m
                )
            ]
        );

        var res = await http.PostAsJsonAsync("/api/invoices", req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var inv = (await res.Content.ReadFromJsonAsync<InvoiceDto>())!;

        // Inter-State (Kerala to Karnataka): 18% IGST (14400), 0 CGST, 0 SGST
        Assert.True(inv.IsInterState);
        Assert.Equal(0m, inv.CgstAmount);
        Assert.Equal(0m, inv.SgstAmount);
        Assert.Equal(14400m, inv.IgstAmount);
        Assert.Equal(14400m, inv.TaxAmount);
        Assert.Equal(94400m, inv.TotalAmount);
        Assert.Equal("29", inv.RecipientStateCode);
        Assert.Contains("Karnataka", inv.PlaceOfSupply);
    }

    [Fact]
    public async Task Multi_Tenancy_Protects_Invoices_And_Printable_Html()
    {
        var authA = await api.SignUpAsync("Firm A Invoicing Desk");
        var httpA = api.WithToken(authA.AccessToken);

        var authB = await api.SignUpAsync("Firm B Invoicing Desk");
        var httpB = api.WithToken(authB.AccessToken);

        var clientA = await CreateClientAsync(httpA, "Firm A Client", KeralaPan);

        var resA = await httpA.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(
            ClientId: clientA.Id,
            IssueDate: new DateOnly(2026, 12, 1),
            DueDate: new DateOnly(2026, 12, 15),
            Currency: "INR",
            Notes: null,
            Terms: null,
            Items: [new InvoiceItemInput("Advisory", "998233", 1, 10000m, 18m)]
        ));
        resA.EnsureSuccessStatusCode();
        var invA = (await resA.Content.ReadFromJsonAsync<InvoiceDto>())!;

        // Firm B tries to get Firm A's invoice -> 404
        var resB = await httpB.GetAsync($"/api/invoices/{invA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resB.StatusCode);

        // Firm B tries to get Firm A's invoice HTML -> 404
        var htmlB = await httpB.GetAsync($"/api/invoices/{invA.Id}/html");
        Assert.Equal(HttpStatusCode.NotFound, htmlB.StatusCode);
    }

    [Fact]
    public async Task Tax_Computation_Comparison_Validates_Portal_Parity()
    {
        var auth = await api.SignUpAsync("Kottayam Tax Associates");
        var http = api.WithToken(auth.AccessToken);

        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: "2025-26",
            FinancialYear: "2024-25",
            GrossSalary: 795000m // Salaried ₹7.95L -> Taxable ₹7.20L with 87A marginal relief
        );

        var res = await http.PostAsJsonAsync("/api/tax/comparison", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var comparison = (await res.Content.ReadFromJsonAsync<RegimeComparisonDto>())!;
        Assert.Equal(720000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(2000m, comparison.NewRegime.Rebate87A);
        Assert.Equal(20800m, comparison.NewRegime.TotalTaxLiability);
        Assert.Equal("new", comparison.RecommendedRegime);
    }
}
