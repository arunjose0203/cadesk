using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Gstr9;
using CaDesk.Domain.Gstr9;
using Xunit;

namespace CaDesk.IntegrationTests;

public class Gstr9Tests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardTaxIds = new()
    {
        ["PAN"] = "AAACB1234F",
        ["GSTIN"] = "27AAACB1234F1Z5"
    };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "GST Practice LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Reliance Retail Logistics Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardTaxIds, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Gstr9_Lifecycle_Compile_ReconcileGstr9c_AddDiscrepancy_Certify_And_RecordDrc03()
    {
        var (http, _) = await NewFirmAsync("KPMG GST Desk");
        var client = await CreateClientAsync(http, "Tata Consumer Products Ltd");

        // 1. Compile GSTR-9 return with Turnover = 6.5 Cr (> 5 Cr => Mandatory GSTR-9 and 9C)
        var compileReq = new CompileGstr9ReturnRequest(
            ClientId: client.Id,
            FinancialYear: "2023-24",
            AggregateTurnover: 65_000_000m,
            OutwardTaxableSupplies: 60_000_000m,
            OutwardExemptedSupplies: 5_000_000m,
            RcmInwardSupplies: 200_000m,
            ItcAvailed3b: 4_500_000m,
            ItcCategorizedB2b: 4_400_000m,
            ItcReversed: 200_000m,
            ItcAsPer2a: 4_600_000m,
            ItcSubsequentFy: 100_000m,
            TotalTaxPayable: 10_800_000m,
            TaxPaidCash: 6_500_000m,
            TaxPaidItc: 4_300_000m,
            PreviousFyAmendmentsTax: 0m,
            Notes: "FY 2023-24 GSTR-9 Annual Return Compilation"
        );

        var compileRes = await http.PostAsJsonAsync("/api/gstr9/returns/compile", compileReq);
        Assert.Equal(HttpStatusCode.Created, compileRes.StatusCode);
        var ret = await compileRes.Content.ReadFromJsonAsync<Gstr9ReturnDto>();
        Assert.NotNull(ret);
        Assert.True(ret.IsGstr9Mandatory);
        Assert.True(ret.IsGstr9cMandatory);
        Assert.Equal(64_800_000m, ret.TotalTurnover); // (60M + 5M) - 200k = 64.8M
        Assert.Equal(4_300_000m, ret.NetItcAvailed);   // 4.5M - 200k = 4.3M
        Assert.Equal(100_000m, ret.ItcReconciledVariance); // 4.6M - (4.4M + 100k) = 100k

        // 2. Fetch GSTR-9C Reconciliation
        var gstr9cRes = await http.GetAsync($"/api/gstr9/returns/{ret.Id}/gstr9c");
        Assert.Equal(HttpStatusCode.OK, gstr9cRes.StatusCode);
        var gstr9c = await gstr9cRes.Content.ReadFromJsonAsync<Gstr9cReconciliationDto>();
        Assert.NotNull(gstr9c);
        Assert.Equal(Gstr9cCertificationStatus.Draft, gstr9c.CertificationStatus);

        // 3. Reconcile GSTR-9C with Audited Financials
        var reconcileReq = new ReconcileGstr9cRequest(
            AuditedGrossTurnover: 65_000_000m,
            UnbilledRevenueStart: 500_000m,
            UnadjustedAdvancesEnd: 300_000m,
            DeemedSupplies: 0m,
            CreditNotesAfterFy: 200_000m,
            TradeDiscountsDisallowed: 100_000m,
            UnbilledRevenueEnd: 400_000m,
            UnadjustedAdvancesStart: 200_000m,
            OtherTurnoverAdjustments: 0m,
            ExemptedNilTurnover: 5_000_000m,
            ZeroRatedWopTurnover: 0m,
            RcmOutwardTurnover: 0m,
            AdditionalTaxPayable: 50_000m,
            AdditionalInterestPayable: 9_000m,
            AdditionalPenaltyPayable: 0m,
            BookItcTotal: 4_300_000m,
            AuditorRemarks: "Annual Reconciliation Completed. Shortfall identified in unbilled revenue."
        );

        var reconcileRes = await http.PostAsJsonAsync($"/api/gstr9/returns/{ret.Id}/gstr9c/reconcile", reconcileReq);
        Assert.Equal(HttpStatusCode.OK, reconcileRes.StatusCode);
        var reconciled9c = await reconcileRes.Content.ReadFromJsonAsync<Gstr9cReconciliationDto>();
        Assert.NotNull(reconciled9c);
        Assert.Equal(Gstr9cCertificationStatus.UnderReview, reconciled9c.CertificationStatus);
        Assert.Equal(59_000m, reconciled9c.TotalAdditionalPayable); // 50k + 9k

        // 4. Add Discrepancy
        var discReq = new AddDiscrepancyRequest(
            Category: DiscrepancyCategory.UnbilledRevenue,
            TableRef: "Table5",
            Description: "Timing mismatch in unbilled revenue recognition for Q4 services",
            DiscrepancyAmount: 500_000m,
            TaxImpact: 50_000m,
            AuditorRecommendation: "Pay through DRC-03 in cash"
        );
        var discRes = await http.PostAsJsonAsync($"/api/gstr9/returns/{ret.Id}/gstr9c/discrepancies", discReq);
        Assert.Equal(HttpStatusCode.Created, discRes.StatusCode);
        var disc = await discRes.Content.ReadFromJsonAsync<Gstr9cDiscrepancyDto>();
        Assert.NotNull(disc);

        // 5. Resolve Discrepancy
        var resolveRes = await http.PostAsJsonAsync($"/api/gstr9/discrepancies/{disc.Id}/resolve", new ResolveDiscrepancyRequest("Acknowledged; DRC-03 tax payment initiated."));
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        // 6. Certify GSTR-9C
        var certifyReq = new CertifyGstr9cRequest(
            AuditorName: "CA Ramesh Kumar",
            AuditorMembershipNo: "098765",
            AuditorFirmRegNo: "102030W",
            Udin: "24098765BK12345678",
            AuditorRemarks: "Certified based on audited financial statements and books of account."
        );
        var certifyRes = await http.PostAsJsonAsync($"/api/gstr9/returns/{ret.Id}/gstr9c/certify", certifyReq);
        Assert.Equal(HttpStatusCode.OK, certifyRes.StatusCode);
        var certified9c = await certifyRes.Content.ReadFromJsonAsync<Gstr9cReconciliationDto>();
        Assert.NotNull(certified9c);
        Assert.Equal(Gstr9cCertificationStatus.Certified, certified9c.CertificationStatus);
        Assert.Equal("24098765BK12345678", certified9c.Udin);

        // 7. Record DRC-03 Payment
        var drcReq = new RecordDrc03PaymentRequest(
            Drc03Arn: "AA270324123456C",
            Drc03Date: new DateOnly(2024, 11, 20),
            Drc03PaidAmount: 59_000m,
            Drc03PaymentMode: "CashLedger"
        );
        var drcRes = await http.PostAsJsonAsync($"/api/gstr9/returns/{ret.Id}/gstr9c/drc03", drcReq);
        Assert.Equal(HttpStatusCode.OK, drcRes.StatusCode);
        var updated9c = await drcRes.Content.ReadFromJsonAsync<Gstr9cReconciliationDto>();
        Assert.NotNull(updated9c);
        Assert.True(updated9c.IsDrc03Completed);

        // 8. File GSTR-9 Return
        var fileReq = new FileGstr9ReturnRequest(
            Arn: "AA2712249876543",
            Notes: "Successfully filed on GST portal with DRC-03 attachment"
        );
        var fileRes = await http.PostAsJsonAsync($"/api/gstr9/returns/{ret.Id}/file", fileReq);
        Assert.Equal(HttpStatusCode.OK, fileRes.StatusCode);
        var filedRet = await fileRes.Content.ReadFromJsonAsync<Gstr9ReturnDto>();
        Assert.NotNull(filedRet);
        Assert.Equal(Gstr9FilingStatus.Filed, filedRet.Status);
        Assert.Equal("AA2712249876543", filedRet.Arn);
    }

    [Fact]
    public async Task Gstr9_MultiTenantIsolation_FirmBCannotAccessFirmAReturns()
    {
        var (httpA, _) = await NewFirmAsync("Firm A GST Advisors");
        var clientA = await CreateClientAsync(httpA, "Client Alpha Pvt Ltd");

        var compileReq = new CompileGstr9ReturnRequest(
            ClientId: clientA.Id,
            FinancialYear: "2023-24",
            AggregateTurnover: 30_000_000m,
            OutwardTaxableSupplies: 28_000_000m,
            OutwardExemptedSupplies: 2_000_000m,
            RcmInwardSupplies: 0m,
            ItcAvailed3b: 2_000_000m,
            ItcCategorizedB2b: 2_000_000m,
            ItcReversed: 0m,
            ItcAsPer2a: 2_000_000m,
            ItcSubsequentFy: 0m,
            TotalTaxPayable: 5_000_000m,
            TaxPaidCash: 3_000_000m,
            TaxPaidItc: 2_000_000m,
            PreviousFyAmendmentsTax: 0m,
            Notes: "Firm A GSTR-9"
        );

        var createRes = await httpA.PostAsJsonAsync("/api/gstr9/returns/compile", compileReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var retA = await createRes.Content.ReadFromJsonAsync<Gstr9ReturnDto>();
        Assert.NotNull(retA);

        // Firm B signs up and tries to access Firm A's return
        var (httpB, _) = await NewFirmAsync("Firm B GST Advisors");

        var getRes = await httpB.GetAsync($"/api/gstr9/returns/{retA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }
}
