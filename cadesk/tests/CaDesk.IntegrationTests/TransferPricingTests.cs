using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.TransferPricing;
using Xunit;

namespace CaDesk.IntegrationTests;

public class TransferPricingTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AABCT1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "PwC International Tax & Transfer Pricing LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Cognizant Technology Solutions India Pvt Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task TransferPricing_FullLifecycle_Benchmark_Txs_Certification_And_TwoLock_Isolation()
    {
        var (httpA, _) = await NewFirmAsync("EY Transfer Pricing Practice");
        var clientA = await CreateClientAsync(httpA, "Infosys BPM Limited");

        // 1. Calculate Benchmark API
        var benchmarkReq = new BenchmarkCalculationRequest(
            ComparableMargins: new List<decimal> { 12m, 14m, 15m, 16m, 18m, 20m, 22m },
            ToleranceBandPercentage: 3.0m);
        var benchRes = await httpA.PostAsJsonAsync("/api/transfer-pricing/calculate-benchmark", benchmarkReq);
        benchRes.EnsureSuccessStatusCode();
        var benchResult = (await benchRes.Content.ReadFromJsonAsync<BenchmarkCalculationResult>())!;
        Assert.True(benchResult.UseRange);
        Assert.Equal(7, benchResult.DataCount);

        // 2. Create Transfer Pricing Study
        var createStudyReq = new SaveStudyRequest(
            ClientId: clientA.Id,
            StudyReference: "TP-2024-INFY-01",
            Title: "Transfer Pricing Documentation & Form 3CEB Study FY 2024-25",
            FinancialYear: "2024-25",
            AssessmentYear: "2025-26",
            AuditPeriodStart: new DateTime(2024, 4, 1),
            AuditPeriodEnd: new DateTime(2025, 3, 31),
            Rule10DDocumentationMaintained: true,
            Rule10DDocumentationPath: "/docs/tp/2024-25/LocalFile_Infosys.pdf",
            AccountantName: "CA Amit Kapur",
            AccountantFirmName: "EY & Associates LLP",
            MembershipNumber: "098765",
            FirmRegistrationNumber: "101234W");

        var studyRes = await httpA.PostAsJsonAsync("/api/transfer-pricing/studies", createStudyReq);
        studyRes.EnsureSuccessStatusCode();
        var study = (await studyRes.Content.ReadFromJsonAsync<TransferPricingStudyDto>())!;
        Assert.NotEqual(Guid.Empty, study.Id);

        // 3. Add Associated Enterprise
        var createAeReq = new SaveEnterpriseRequest(
            EnterpriseName: "Infosys Technologies Americas Inc",
            CountryOfResidence: "USA",
            CountryCode: "US",
            TaxIdentificationNumber: "EIN-123456789",
            RegisteredAddress: "Plano, Texas, United States",
            RelationshipSection: "Sec 92A(2)(a) - Holding > 26% Voting Power",
            OwnershipPercentage: 100m,
            BriefActivityDescription: "Marketing, client relationship and contract front-ending entity.");
        var aeRes = await httpA.PostAsJsonAsync($"/api/transfer-pricing/studies/{study.Id}/enterprises", createAeReq);
        aeRes.EnsureSuccessStatusCode();
        var ae = (await aeRes.Content.ReadFromJsonAsync<AssociatedEnterpriseDto>())!;
        Assert.NotEqual(Guid.Empty, ae.Id);

        // 4. Add Comparables
        var compReq = new SaveComparableRequest(
            TransactionId: null,
            ComparableCompanyName: "Wipro HR Services India Ltd",
            CinOrIdentifier: "U72200KA2000PLC027000",
            FinancialYear: "2024-25",
            OperatingRevenue: 500_000_000m,
            OperatingCost: 420_000_000m,
            OperatingProfit: 80_000_000m,
            Weight: 1.0m,
            IsAccepted: true,
            RejectionReason: null);
        var compRes = await httpA.PostAsJsonAsync($"/api/transfer-pricing/studies/{study.Id}/comparables", compReq);
        compRes.EnsureSuccessStatusCode();
        var comp = (await compRes.Content.ReadFromJsonAsync<ComparableBenchmarkDto>())!;
        Assert.True(comp.CalculatedMarginPercentage > 0);

        // 5. Add Transaction (Clause 13 ITeS / Software services)
        var createTxReq = new SaveTransactionRequest(
            AssociatedEnterpriseId: ae.Id,
            TransactionCategory: "InternationalTransaction",
            ClauseNumber: 13,
            DescriptionOfTransaction: "Provision of IT-enabled Back Office Support Services",
            CurrencyCode: "USD",
            ForeignCurrencyAmount: 2_000_000m,
            ExchangeRate: 83.50m,
            MostAppropriateMethod: "TNMM",
            ProfitLevelIndicator: "OP_TC",
            TestedPartyCostBase: 140_000_000m,
            TestedPartyMargin: 19.04m, // Matches comparable margin -> At Arm's Length
            ToleranceBandPercentage: 3.0m,
            MethodSelectionRationale: "TNMM is most appropriate given routine captive services FAR profile.",
            DocumentationReference: "Local File Section 6");
        var txRes = await httpA.PostAsJsonAsync($"/api/transfer-pricing/studies/{study.Id}/transactions", createTxReq);
        txRes.EnsureSuccessStatusCode();
        var tx = (await txRes.Content.ReadFromJsonAsync<TransferPricingTransactionDto>())!;
        Assert.Equal(167_000_000m, tx.InrValue);
        Assert.True(tx.IsWithinArmLength);

        // 6. Certify Form 3CEB with 18-character UDIN
        var certifyReq = new CertifyStudyRequest(
            Udin: "25098765B000123456",
            CertifiedBy: "CA Amit Kapur, Partner");
        var certifyRes = await httpA.PostAsJsonAsync($"/api/transfer-pricing/studies/{study.Id}/certify", certifyReq);
        certifyRes.EnsureSuccessStatusCode();
        var certifiedStudy = (await certifyRes.Content.ReadFromJsonAsync<TransferPricingStudyDto>())!;
        Assert.Equal("Certified", certifiedStudy.StudyStatus);
        Assert.Equal("25098765B000123456", certifiedStudy.Udin);
        Assert.Contains("FORM NO. 3CEB", certifiedStudy.Form3CebReportText);

        // 7. Multi-Tenancy Two-Lock Verification: Firm B cannot access Firm A's TP study
        var (httpB, _) = await NewFirmAsync("Deloitte Transfer Pricing Team");
        var firmBList = await httpB.GetFromJsonAsync<List<TransferPricingStudyDto>>("/api/transfer-pricing/studies");
        Assert.NotNull(firmBList);
        Assert.DoesNotContain(firmBList, x => x.Id == study.Id);

        var firmBGetRes = await httpB.GetAsync($"/api/transfer-pricing/studies/{study.Id}");
        Assert.Equal(HttpStatusCode.NotFound, firmBGetRes.StatusCode);
    }
}
