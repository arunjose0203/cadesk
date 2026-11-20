using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.EInvoicing;
using CaDesk.Domain.EInvoicing;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 24: Automated E-Invoicing & E-Way Bill Integration Desk.
/// Tests IRP JSON generation, SHA-256 IRN hash generation, signed QR code synthesis,
/// E-Way Bill Part A & Part B vehicle updates, Rule 138(10) validity, and multi-tenant firm isolation.
/// </summary>
public class EInvoicingTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Logistics Tax Advisory")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Mahindra Logistics Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task EInvoice_And_EWayBill_Lifecycle_Generates_Valid_Irn_And_PartB()
    {
        var (http, _) = await NewFirmAsync("Ernst & Young E-Invoicing Desk");
        var client = await CreateClientAsync(http, "Adani Ports & SEZ Ltd");

        // 1. Generate E-Invoice
        var einvReq = new GenerateEInvoiceRequest(
            ClientId: client.Id,
            InvoiceId: null,
            DocType: EInvoiceDocType.INV,
            DocNumber: "INV/2024/099",
            DocDate: DateOnly.FromDateTime(DateTime.UtcNow),
            FinancialYear: "2024-25",
            SupplierGstin: "27AAACB1234F1Z5",
            SupplierTradeName: "Adani Logistics Hub",
            RecipientGstin: "29BBBCB5678G1Z2",
            RecipientLegalName: "Bangalore Tech Park Private Ltd",
            SupplyType: EInvoiceSupplyType.B2B,
            TotalTaxableValue: 500000m,
            CgstAmount: 45000m,
            SgstAmount: 45000m,
            IgstAmount: 0m,
            CessAmount: 0m,
            MainHsnCode: "998311",
            ItemCount: 2
        );

        var einvRes = await http.PostAsJsonAsync("/api/einvoicing/invoices", einvReq);
        einvRes.EnsureSuccessStatusCode();
        var einv = await einvRes.Content.ReadFromJsonAsync<EInvoiceDto>();
        Assert.NotNull(einv);
        Assert.Equal(EInvoiceStatus.Generated, einv.Status);
        Assert.NotNull(einv.Irn);
        Assert.Equal(64, einv.Irn.Length);
        Assert.NotNull(einv.SignedQrCode);
        Assert.NotNull(einv.AckNo);
        Assert.Equal(590000m, einv.TotalInvoiceValue);

        // 2. Generate E-Way Bill (Part A & Part B)
        var ewbReq = new GenerateEWayBillRequest(
            ClientId: client.Id,
            EInvoiceId: einv.Id,
            SupplyType: EwbSupplyType.Outward,
            SubSupplyType: EwbSubSupplyType.Supply,
            DocType: "INV",
            DocNumber: "INV/2024/099",
            DocDate: DateOnly.FromDateTime(DateTime.UtcNow),
            FromGstin: "27AAACB1234F1Z5",
            FromTradeName: "Adani Logistics Hub",
            FromPincode: "400001",
            FromStateCode: 27,
            ToGstin: "29BBBCB5678G1Z2",
            ToTradeName: "Bangalore Tech Park Private Ltd",
            ToPincode: "560001",
            ToStateCode: 29,
            TotalValue: 590000m,
            HsnCode: "998311",
            DistanceKm: 980,
            TransportMode: EwbTransportMode.Road,
            VehicleType: EwbVehicleType.Regular,
            VehicleNumber: "MH12AB1234",
            TransporterId: "27TRANS12345678",
            TransporterName: "VRL Logistics"
        );

        var ewbRes = await http.PostAsJsonAsync("/api/einvoicing/ewaybills", ewbReq);
        ewbRes.EnsureSuccessStatusCode();
        var ewb = await ewbRes.Content.ReadFromJsonAsync<EWayBillDto>();
        Assert.NotNull(ewb);
        Assert.Equal(EwbStatus.Active, ewb.Status);
        Assert.Equal("MH12AB1234", ewb.VehicleNumber);
        Assert.True(ewb.RemainingHours > 24);

        // 3. Update Part B (Vehicle update during transit)
        var updatePartBRes = await http.PutAsJsonAsync($"/api/einvoicing/ewaybills/{ewb.Id}/part-b", new UpdateEWayBillPartBRequest(
            VehicleNumber: "KA01XY9999",
            TransporterId: "27TRANS12345678",
            TransporterName: "VRL Logistics",
            Reason: "Vehicle Breakdown"
        ));
        updatePartBRes.EnsureSuccessStatusCode();
        var updatedEwb = await updatePartBRes.Content.ReadFromJsonAsync<EWayBillDto>();
        Assert.NotNull(updatedEwb);
        Assert.Equal("KA01XY9999", updatedEwb.VehicleNumber);

        // 4. Cancel E-Invoice
        var cancelRes = await http.PostAsJsonAsync($"/api/einvoicing/invoices/{einv.Id}/cancel", new CancelEInvoiceRequest(
            Reason: "Data Entry Mistake",
            Remarks: "Corrected recipient billing details"
        ));
        cancelRes.EnsureSuccessStatusCode();
        var cancelled = await cancelRes.Content.ReadFromJsonAsync<EInvoiceDto>();
        Assert.NotNull(cancelled);
        Assert.Equal(EInvoiceStatus.Cancelled, cancelled.Status);

        // 5. Check Summary
        var summary = await http.GetFromJsonAsync<EInvoicingSummaryDto>($"/api/einvoicing/summary?clientId={client.Id}");
        Assert.NotNull(summary);
        Assert.True(summary.TotalEInvoices >= 1);
        Assert.True(summary.ActiveEWayBills >= 1);
    }

    [Fact]
    public async Task Two_Lock_Multi_Tenancy_Protects_Firm_EInvoices()
    {
        var (firmA, _) = await NewFirmAsync("Firm A E-Way Desk");
        var (firmB, _) = await NewFirmAsync("Firm B E-Way Desk");

        var clientA = await CreateClientAsync(firmA, "Client Firm A");

        var einvReq = new GenerateEInvoiceRequest(
            ClientId: clientA.Id,
            InvoiceId: null,
            DocType: EInvoiceDocType.INV,
            DocNumber: "DOC/2024/001",
            DocDate: DateOnly.FromDateTime(DateTime.UtcNow),
            FinancialYear: "2024-25",
            SupplierGstin: "27XYZAB1234F1Z9",
            SupplierTradeName: "Firm A Supplier",
            RecipientGstin: "27ABCDE5678G1Z1",
            RecipientLegalName: "Firm A Buyer",
            SupplyType: EInvoiceSupplyType.B2B,
            TotalTaxableValue: 100000m,
            CgstAmount: 9000m,
            SgstAmount: 9000m,
            IgstAmount: 0m,
            CessAmount: 0m,
            MainHsnCode: "847130",
            ItemCount: 1
        );

        var resA = await firmA.PostAsJsonAsync("/api/einvoicing/invoices", einvReq);
        resA.EnsureSuccessStatusCode();
        var einvA = (await resA.Content.ReadFromJsonAsync<EInvoiceDto>())!;

        // Firm B tries to get Firm A's e-invoice directly -> Must be 404
        var resB = await firmB.GetAsync($"/api/einvoicing/invoices/{einvA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resB.StatusCode);
    }
}
