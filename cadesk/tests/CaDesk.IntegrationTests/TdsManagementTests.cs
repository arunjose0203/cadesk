using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Tds;
using CaDesk.Domain.Tds;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 17: TDS Computation, Lower Deduction Certificates & Form 26Q/27Q Preparation.
/// Tests statutory section rates, 206AA missing PAN penalty rate, LDC threshold utilization,
/// Challan allocation workflow, and firm-level tenant isolation.
/// </summary>
public class TdsManagementTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "TDS Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Godrej Consumer Products")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Tds_DeductionChallanAndLdcLifecycle_Succeeds()
    {
        var (http, _) = await NewFirmAsync("PwC Tax Advisors");
        var client = await CreateClientAsync(http, "Infosys BPO Ltd");

        // 1. Create a Lower Deduction Certificate (Section 197) for Contractor at 0.5%
        var ldcReq = new CreateLdcRequest(
            ClientId: client.Id,
            DeducteePan: "ABCDE1234F",
            DeducteeName: "FastTrack Logistics",
            Section: TdsSection.Sec194CCompany,
            CertificateNumber: "LDC/2425/00012",
            FinancialYear: "2024-25",
            ValidFrom: new DateOnly(2024, 4, 1),
            ValidTo: new DateOnly(2025, 3, 31),
            SpecifiedRate: 0.5m,
            ThresholdAmount: 5_000_000m
        );
        var ldcRes = await http.PostAsJsonAsync("/api/tds/ldc", ldcReq);
        Assert.Equal(HttpStatusCode.Created, ldcRes.StatusCode);
        var ldc = (await ldcRes.Content.ReadFromJsonAsync<TdsLowerDeductionCertificateDto>())!;
        Assert.Equal(0.5m, ldc.SpecifiedRate);
        Assert.Equal(5_000_000m, ldc.RemainingAmount);

        // 2. Record Deduction with LDC applied: ₹200,000 at 0.5% = ₹1,000
        var dedReq1 = new RecordTdsDeductionRequest(
            ClientId: client.Id,
            FormType: TdsFormType.Form26Q,
            Quarter: "Q1",
            FinancialYear: "2024-25",
            DeducteePan: "ABCDE1234F",
            DeducteeName: "FastTrack Logistics",
            Section: TdsSection.Sec194CCompany,
            PaymentDate: new DateOnly(2024, 4, 15),
            AmountPaid: 200_000m,
            DeductionDate: new DateOnly(2024, 4, 15),
            CertificateId: ldc.Id,
            ForceHigherRate206Ab: false
        );
        var dedRes1 = await http.PostAsJsonAsync("/api/tds/deductions", dedReq1);
        Assert.Equal(HttpStatusCode.Created, dedRes1.StatusCode);
        var ded1 = (await dedRes1.Content.ReadFromJsonAsync<TdsDeductionDto>())!;
        Assert.Equal(0.5m, ded1.TdsRate);
        Assert.Equal(1000m, ded1.TdsAmount);

        // 3. Record Deduction with Invalid PAN -> Triggers Section 206AA 20% Rate
        var dedReq2 = new RecordTdsDeductionRequest(
            ClientId: client.Id,
            FormType: TdsFormType.Form26Q,
            Quarter: "Q1",
            FinancialYear: "2024-25",
            DeducteePan: "INVALIDPAN",
            DeducteeName: "Local Vendor",
            Section: TdsSection.Sec194CCompany,
            PaymentDate: new DateOnly(2024, 4, 20),
            AmountPaid: 100_000m,
            DeductionDate: new DateOnly(2024, 4, 20),
            CertificateId: null,
            ForceHigherRate206Ab: false
        );
        var dedRes2 = await http.PostAsJsonAsync("/api/tds/deductions", dedReq2);
        Assert.Equal(HttpStatusCode.Created, dedRes2.StatusCode);
        var ded2 = (await dedRes2.Content.ReadFromJsonAsync<TdsDeductionDto>())!;
        Assert.Equal(20.0m, ded2.TdsRate);
        Assert.Equal(20000m, ded2.TdsAmount);
        Assert.Equal(HigherRateReason.NoPan206Aa, ded2.HigherRateReason);

        // 4. Create ITNS 281 Challan for April (Total TDS: 1,000 + 20,000 = 21,000)
        var challanReq = new CreateTdsChallanRequest(
            ClientId: client.Id,
            Quarter: "Q1",
            FinancialYear: "2024-25",
            BsrCode: "0210045",
            ChallanDate: new DateOnly(2024, 5, 6),
            ChallanSerialNumber: "00123",
            SectionCode: "94C",
            MinorHead: TdsMinorHead.Regular200,
            TdsAmount: 21_000m,
            Surcharge: 0m,
            Cess: 0m,
            Interest: 0m,
            LateFee234E: 0m,
            Notes: "April 2024 TDS on Contractors Section 194C"
        );
        var chalRes = await http.PostAsJsonAsync("/api/tds/challans", challanReq);
        Assert.Equal(HttpStatusCode.Created, chalRes.StatusCode);
        var challan = (await chalRes.Content.ReadFromJsonAsync<TdsChallanDto>())!;
        Assert.Equal(21_000m, challan.TotalChallanAmount);
        Assert.Equal(0m, challan.AllocatedAmount);

        // 5. Map deductions to Challan
        var mapReq = new MapDeductionsToChallanRequest(
            ChallanId: challan.Id,
            DeductionIds: [ded1.Id, ded2.Id]
        );
        var mapRes = await http.PostAsJsonAsync("/api/tds/challans/map", mapReq);
        Assert.Equal(HttpStatusCode.OK, mapRes.StatusCode);
        var updatedChallan = (await mapRes.Content.ReadFromJsonAsync<TdsChallanDto>())!;
        Assert.Equal(21_000m, updatedChallan.AllocatedAmount);
        Assert.Equal(0m, updatedChallan.UnallocatedBalance);

        // 6. Summary KPI
        var summary = await http.GetFromJsonAsync<TdsSummaryDto>("/api/tds/summary");
        Assert.NotNull(summary);
        Assert.True(summary.TotalDeductionsCount >= 2);
        Assert.True(summary.TotalDeposited >= 21_000m);
    }

    [Fact]
    public async Task Tds_TenantIsolation_CrossFirmAccessBlocked()
    {
        var (firmA, _) = await NewFirmAsync("Firm A TDS");
        var (firmB, _) = await NewFirmAsync("Firm B TDS");
        var clientA = await CreateClientAsync(firmA, "Client Alpha TDS");

        var challanReq = new CreateTdsChallanRequest(
            ClientId: clientA.Id,
            Quarter: "Q1",
            FinancialYear: "2024-25",
            BsrCode: "0510001",
            ChallanDate: new DateOnly(2024, 5, 5),
            ChallanSerialNumber: "00001",
            SectionCode: "94J",
            MinorHead: TdsMinorHead.Regular200,
            TdsAmount: 50_000m,
            Surcharge: 0m,
            Cess: 0m,
            Interest: 0m,
            LateFee234E: 0m,
            Notes: null
        );

        var resA = await firmA.PostAsJsonAsync("/api/tds/challans", challanReq);
        var challanA = (await resA.Content.ReadFromJsonAsync<TdsChallanDto>())!;

        // Firm B tries to list Firm A's challans
        var listB = await firmB.GetFromJsonAsync<List<TdsChallanDto>>($"/api/tds/challans?clientId={clientA.Id}");
        Assert.True(listB == null || listB.Count == 0);

        // Firm B tries to list Firm A's deductions
        var dedsB = await firmB.GetFromJsonAsync<List<TdsDeductionDto>>($"/api/tds/deductions?clientId={clientA.Id}");
        Assert.True(dedsB == null || dedsB.Count == 0);
    }
}
