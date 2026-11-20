using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.ForeignRemittance;
using Xunit;

namespace CaDesk.IntegrationTests;

public class ForeignRemittanceTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "PwC International Tax Advisory LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "CloudScale Networks India Pvt Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task ForeignRemittance_FullLifecycle_Checklist_15CB_UDIN_15CA_Filing_And_FirmIsolation()
    {
        var (httpA, _) = await NewFirmAsync("Ernst & Young International Tax Services");
        var clientA = await CreateClientAsync(httpA, "InfraTech Cloud Solutions Pvt Ltd");

        // 1. Create Foreign Remittance Assessment
        var createReq = new CreateForeignRemittanceRequest(
            ClientId: clientA.Id,
            FinancialYear: "2024-25",
            RemittanceReference: "REM-2024-001",
            RemitterPan: "AAACI9999M",
            RemitterTan: "BLRI99999P",
            RemitterName: "InfraTech Cloud Solutions Pvt Ltd",
            RemitterStatus: "Company",
            RemitterAddress: "Prestige Tech Park, Outer Ring Road, Bangalore, Karnataka 560103",
            RemitterEmail: "accounts@infratech.in",
            RemitterPhone: "+91-80-49999999",
            BeneficiaryName: "Amazon Web Services Inc",
            BeneficiaryCountry: "United States",
            BeneficiaryCountryCode: "US",
            BeneficiaryAddress: "410 Terry Avenue North, Seattle, WA 98109-5210",
            BeneficiaryTaxId: "EIN-91-1646860",
            BeneficiaryEntityType: "Company",
            RemittanceCurrency: "USD",
            ForeignAmount: 50000m,
            ExchangeRate: 85.00m, // INR 4,250,000 > 500,000 => Form 15CA Part C + Form 15CB
            ProposedRemittanceDate: DateTime.UtcNow.AddDays(5),
            RbiPurposeCode: "P0802",
            PurposeDescription: "Software hosting, infrastructure as a service (IaaS)",
            NatureOfRemittance: "FeesForTechnicalServices",
            ContractDescription: "AWS Enterprise Cloud Services Agreement",
            InvoiceNumber: "INV-AWS-2024-7788",
            InvoiceDate: DateTime.UtcNow.AddDays(-10),
            IsChargeableToTax: true,
            ChargeabilityReason: "Fees for Technical Services under Section 9(1)(vii)",
            DomesticSection: "Section 195 read with Section 115A",
            DomesticTaxRate: 20.00m,
            SurchargeRate: 0.00m,
            CessRate: 4.00m,
            TreatyBenefitClaimed: true,
            TreatyCountry: "United States",
            TreatyArticle: "Article 12 - Royalties and Fees for Included Services",
            TreatyTaxRate: 15.00m,
            HasTrc: true,
            TrcPeriodFrom: new DateTime(2024, 4, 1),
            TrcPeriodTo: new DateTime(2025, 3, 31),
            HasForm10F: true,
            NoPeDeclaration: true,
            BeneficialOwnershipConfirmed: true,
            MliPptPassed: true,
            TaxBorneByRemitter: false,
            AoOrderSection: null,
            AoOrderNumber: null,
            AoOrderDate: null,
            AoCertificateRate: null
        );

        var createRes = await httpA.PostAsJsonAsync("/api/foreign-remittance/assessments", createReq);
        createRes.EnsureSuccessStatusCode();
        var assessment = (await createRes.Content.ReadFromJsonAsync<ForeignRemittanceAssessmentDto>())!;
        Assert.NotNull(assessment);
        Assert.Equal("REM-2024-001", assessment.RemittanceReference);
        Assert.Equal(4250000m, assessment.InrEquivalent);
        Assert.Equal("PartC", assessment.Form15CaPart);
        Assert.True(assessment.RequiresForm15Cb);
        Assert.Equal(15.00m, assessment.FinalWithholdingRate);
        Assert.Equal(637500m, assessment.TdsAmountInr); // 4,250,000 * 15%
        Assert.Equal(7500m, assessment.TdsAmountFc);
        Assert.Equal(42500m, assessment.NetRemittanceFc);
        Assert.NotEmpty(assessment.Checklist);

        // 2. Verify and Update Checklist Items
        var trcItem = Assert.Single(assessment.Checklist, x => x.ItemCode == "TRC");
        var updateChecklistRes = await httpA.PutAsJsonAsync($"/api/foreign-remittance/assessments/{assessment.Id}/checklist/{trcItem.Id}",
            new UpdateChecklistItemRequest("Verified", "TRC issued by IRS Form 6166 verified."));
        updateChecklistRes.EnsureSuccessStatusCode();

        // 3. Certify Form 15CB with CA UDIN
        var certifyReq = new CertifyRemittanceRequest(
            CaMembershipNumber: "214589",
            CaName: "CA Ananya Sharma",
            CaFirmName: "Sharma & Associates LLP",
            CaFrn: "123456N",
            Udin: "24214589BK99887766" // 18 chars
        );

        var certifyRes = await httpA.PostAsJsonAsync($"/api/foreign-remittance/assessments/{assessment.Id}/certify", certifyReq);
        certifyRes.EnsureSuccessStatusCode();
        var certified = (await certifyRes.Content.ReadFromJsonAsync<ForeignRemittanceAssessmentDto>())!;
        Assert.Equal("Certified", certified.Status);
        Assert.Equal("24214589BK99887766", certified.Udin);
        Assert.Contains("FORM NO. 15CB", certified.Generated15CbCertificateText);
        Assert.Contains("CA Ananya Sharma", certified.Generated15CbCertificateText);
        Assert.Contains("<Form15CA", certified.Generated15CaXmlPayload);

        // 4. File on Income Tax Portal
        var fileReq = new FileRemittanceRequest("ACK15CA20240019284");
        var fileRes = await httpA.PostAsJsonAsync($"/api/foreign-remittance/assessments/{assessment.Id}/file", fileReq);
        fileRes.EnsureSuccessStatusCode();
        var filed = (await fileRes.Content.ReadFromJsonAsync<ForeignRemittanceAssessmentDto>())!;
        Assert.Equal("Filed", filed.Status);
        Assert.Equal("ACK15CA20240019284", filed.AcknowledgementNumber);

        // 5. Check Summary API
        var summaryRes = await httpA.GetFromJsonAsync<ForeignRemittanceSummaryDto>("/api/foreign-remittance/summary?fy=2024-25");
        Assert.NotNull(summaryRes);
        Assert.Equal(1, summaryRes.TotalAssessments);
        Assert.Equal(4250000m, summaryRes.TotalRemittanceInr);
        Assert.Equal(637500m, summaryRes.TotalTdsInr);
        Assert.Equal(1, summaryRes.FiledCount);
        Assert.Equal(1, summaryRes.PartCCount);

        // 6. Two-Lock Firm Isolation: Firm B cannot access Firm A's assessment
        var (httpB, _) = await NewFirmAsync("KPMG Corporate Tax Group");
        var firmBGetRes = await httpB.GetAsync($"/api/foreign-remittance/assessments/{assessment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, firmBGetRes.StatusCode);
    }
}
