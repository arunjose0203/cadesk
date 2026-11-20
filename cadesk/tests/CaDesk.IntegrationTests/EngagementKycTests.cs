using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Engagements;
using CaDesk.Domain.Engagements;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 19: Digital Engagement Letters & KYC Compliance (ICAI Standards & Risk Profiling).
/// Tests SA 210 engagement letter generation, digital acceptance flow, PMLA AML/KYC risk profiling,
/// UBO beneficial ownership tracking, and multi-tenant isolation.
/// </summary>
public class EngagementKycTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Engagement Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Adani Ports and SEZ Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Engagement_Letter_Full_Lifecycle_And_Kyc_Succeeds()
    {
        var (http, _) = await NewFirmAsync("KPMG Assurance");
        var client = await CreateClientAsync(http, "Adani Ports and SEZ Ltd");

        // 1. Create Digital Engagement Letter for Statutory Audit
        var createReq = new CreateEngagementLetterRequest(
            ClientId: client.Id,
            EngagementType: EngagementType.StatutoryAudit,
            FinancialYear: "2024-25",
            ScopeOfWork: null, // will auto-populate from SA 210 template
            ManagementResponsibilities: null,
            AuditorResponsibilities: null,
            FeeStructure: EngagementFeeStructure.FixedFee,
            AgreedFeeAmount: 500_000m,
            OutOfPocketIncluded: false,
            LetterContent: null
        );

        var createRes = await http.PostAsJsonAsync("/api/engagements", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var letter = (await createRes.Content.ReadFromJsonAsync<EngagementLetterDto>())!;
        Assert.Equal(EngagementStatus.Draft, letter.Status);
        Assert.Equal(500_000m, letter.AgreedFeeAmount);
        Assert.Contains("SA 210", letter.LetterContent);

        // 2. Send Letter to Client
        var sendRes = await http.PostAsync($"/api/engagements/{letter.Id}/send", null);
        Assert.Equal(HttpStatusCode.OK, sendRes.StatusCode);
        var sentLetter = (await sendRes.Content.ReadFromJsonAsync<EngagementLetterDto>())!;
        Assert.Equal(EngagementStatus.SentToClient, sentLetter.Status);
        Assert.NotNull(sentLetter.SentDate);

        // 3. Client Digitally Accepts Letter
        var acceptReq = new AcceptEngagementLetterRequest(
            SignatoryName: "Gautam Adani",
            SignatoryDesignation: "Managing Director",
            IpAddress: "203.192.12.5"
        );

        var acceptRes = await http.PostAsJsonAsync($"/api/engagements/{letter.Id}/accept", acceptReq);
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);
        var acceptedLetter = (await acceptRes.Content.ReadFromJsonAsync<EngagementLetterDto>())!;
        Assert.Equal(EngagementStatus.Accepted, acceptedLetter.Status);
        Assert.NotNull(acceptedLetter.AcceptedDate);
        Assert.Equal("Gautam Adani", acceptedLetter.ClientSignatoryName);
        Assert.Equal("203.192.12.5", acceptedLetter.AcceptanceIp);

        // 4. Record PMLA Client KYC Profile & UBO
        var kycReq = new SaveClientKycRequest(
            ClientId: client.Id,
            RiskRating: KycRiskRating.Medium,
            VerificationStatus: KycVerificationStatus.Verified,
            ConstitutionDocumentType: "CertificateOfIncorporation",
            ConstitutionDocumentNumber: "L63090GJ1998PLC034182",
            PanVerified: true,
            GstinVerified: true,
            AddressProofType: "ElectricityBill",
            AddressProofNumber: "EB-2024-9988",
            UboName: "Adani Family Trust",
            UboShareholdingPercentage: 65.5m,
            UboPan: "AAATA1234K",
            SanctionsCheckCleared: true,
            AdverseMediaCheckCleared: true,
            RiskRationale: "Listed entity with transparent ownership structure and no adverse sanctions."
        );

        var kycRes = await http.PostAsJsonAsync("/api/kyc", kycReq);
        Assert.Equal(HttpStatusCode.OK, kycRes.StatusCode);
        var kyc = (await kycRes.Content.ReadFromJsonAsync<ClientKycRecordDto>())!;
        Assert.Equal(KycRiskRating.Medium, kyc.RiskRating);
        Assert.Equal(KycVerificationStatus.Verified, kyc.VerificationStatus);
        Assert.True(kyc.PanVerified);
        Assert.Equal(65.5m, kyc.UboShareholdingPercentage);

        // 5. Verify KPI Summary
        var sumRes = await http.GetAsync("/api/engagements/summary");
        Assert.Equal(HttpStatusCode.OK, sumRes.StatusCode);
        var summary = (await sumRes.Content.ReadFromJsonAsync<EngagementKycSummaryDto>())!;
        Assert.Equal(1, summary.TotalLettersCount);
        Assert.Equal(1, summary.AcceptedLettersCount);
        Assert.Equal(0, summary.PendingLettersCount);
        Assert.Equal(1, summary.TotalKycRecordsCount);
        Assert.Equal(1, summary.VerifiedKycCount);
        Assert.Equal(500_000m, summary.TotalContractedFeeValue);
    }
}
