using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.AntiProfiteering;
using Xunit;

namespace CaDesk.IntegrationTests;

public class AntiProfiteeringTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F", ["GSTIN"] = "27AAACB1234F1Z9" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Deloitte Indirect Tax Advisory LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Godrej Consumer Products Limited")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task AntiProfiteering_FullLifecycle_Notifications_Assessments_Skus_Refunds_And_Isolation()
    {
        var (httpA, _) = await NewFirmAsync("BDO GST & Indirect Tax Advisory");
        var clientA = await CreateClientAsync(httpA, "Britannia Industries Limited");

        // 1. List Notifications (triggers baseline seeding)
        var notifs = await httpA.GetFromJsonAsync<List<GstRateNotificationDto>>("/api/anti-profiteering/notifications");
        Assert.NotNull(notifs);
        Assert.True(notifs.Count >= 5);

        var restaurantNotif = notifs.Find(x => x.HsnOrSacCode == "9963");
        Assert.NotNull(restaurantNotif);

        // 2. Create Assessment linked to notification
        var createAssessmentReq = new SaveAssessmentRequest(
            ClientId: clientA.Id,
            AssessmentReference: "APA-2024-BRIT-01",
            Title: "Section 171 Anti-Profiteering Audit - Confectionery & Biscuits",
            FinancialYear: "2024-25",
            AuditPeriodFrom: new DateTime(2024, 7, 1),
            AuditPeriodTo: new DateTime(2024, 9, 30),
            GstNotificationId: restaurantNotif.Id,
            NotificationReference: restaurantNotif.NotificationNumber,
            RateCutEffectiveDate: restaurantNotif.EffectiveDate,
            LeadAuditorName: "CA Sneha Mehra, Partner"
        );

        var assessRes = await httpA.PostAsJsonAsync("/api/anti-profiteering/assessments", createAssessmentReq);
        assessRes.EnsureSuccessStatusCode();
        var assessment = (await assessRes.Content.ReadFromJsonAsync<AntiProfiteeringAssessmentDto>())!;
        Assert.NotNull(assessment);
        Assert.Equal("APA-2024-BRIT-01", assessment.AssessmentReference);
        Assert.Equal("Draft", assessment.AuditStatus);

        // 3. Add SKU 1: Fully Compliant (Base maintained, selling price reduced commensurate with tax cut)
        var sku1Req = new SaveSkuItemRequest(
            ProductCode: "SKU-GOOD-DAY-100",
            HsnOrSacCode: "1905",
            ProductName: "Good Day Butter Cookies 100g",
            UnitOfMeasurement: "PACK",
            PreCutBasePrice: 25.00m,
            OldGstRate: 18.00m,
            NewGstRate: 5.00m,
            PostCutActualBasePrice: 25.00m,
            QuantitySoldPostCut: 10000m,
            HasRawMaterialCostJustification: false,
            JustificationRemarks: "Price reduced from Rs 29.50 to Rs 26.25 immediately upon notification."
        );

        var sku1Res = await httpA.PostAsJsonAsync($"/api/anti-profiteering/assessments/{assessment.Id}/skus", sku1Req);
        sku1Res.EnsureSuccessStatusCode();
        var sku1 = (await sku1Res.Content.ReadFromJsonAsync<SkuProfiteeringItemDto>())!;
        Assert.Equal("Compliant", sku1.ComplianceStatus);
        Assert.Equal(0m, sku1.ProfiteeringPerUnit);
        Assert.Equal(0m, sku1.TotalProfiteeredAmount);

        // 4. Add SKU 2: Profiteered (Base price hiked, zero benefit passed to consumer)
        var sku2Req = new SaveSkuItemRequest(
            ProductCode: "SKU-TREAT-200",
            HsnOrSacCode: "1905",
            ProductName: "Treat Chocolate Cream Biscuits 200g",
            UnitOfMeasurement: "PACK",
            PreCutBasePrice: 40.00m,
            OldGstRate: 18.00m, // PreCutSelling = 47.20
            NewGstRate: 5.00m,  // Commensurate required = 40 * 1.05 = 42.00
            PostCutActualBasePrice: 44.95m, // Hiked base so selling price stays 47.20!
            QuantitySoldPostCut: 5000m,
            HasRawMaterialCostJustification: false,
            JustificationRemarks: "No input cost increase documented."
        );

        var sku2Res = await httpA.PostAsJsonAsync($"/api/anti-profiteering/assessments/{assessment.Id}/skus", sku2Req);
        sku2Res.EnsureSuccessStatusCode();
        var sku2 = (await sku2Res.Content.ReadFromJsonAsync<SkuProfiteeringItemDto>())!;
        Assert.Equal("Profiteered", sku2.ComplianceStatus);
        Assert.True(sku2.ProfiteeringPerUnit > 5.0m);
        Assert.True(sku2.TotalProfiteeredAmount > 25000m);

        // 5. Add Customer Refund Record (Credit note passed to institutional distributor)
        var refundReq = new SaveRefundRecordRequest(
            RecipientType: "IdentifiedCustomer",
            CustomerNameOrEntity: "D-Mart Avenue Supermarts Ltd",
            CustomerGstin: "27AABCA1234F1Z1",
            InvoiceReference: "CN-2024-0091",
            RefundAmount: 15000m,
            RefundMode: "CreditNote",
            RefundOrDepositDate: DateTime.UtcNow.AddDays(-10),
            ChallanOrCinNumber: "",
            InterestAmountPaid: 0m,
            Notes: "Commercial credit note issued to pass on differential tax benefit."
        );

        var refundRes = await httpA.PostAsJsonAsync($"/api/anti-profiteering/assessments/{assessment.Id}/refunds", refundReq);
        refundRes.EnsureSuccessStatusCode();
        var refund = (await refundRes.Content.ReadFromJsonAsync<ProfiteeringRefundRecordDto>())!;
        Assert.Equal(15000m, refund.RefundAmount);

        // 6. Verify Summary API
        var summary = await httpA.GetFromJsonAsync<AntiProfiteeringSummaryDto>($"/api/anti-profiteering/summary?clientId={clientA.Id}");
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalAssessments);
        Assert.Equal(2, summary.TotalSkusAudited);
        Assert.Equal(1, summary.TotalProfiteeredSkus);
        Assert.Equal(15000m, summary.TotalRefundedToCustomers);

        // 7. Certify Assessment with ICAI UDIN
        var certifyReq = new CertifyAssessmentRequest(
            LeadAuditorName: "CA Sneha Mehra, FCA, Partner",
            Udin: "24078512AAAAAP7766"
        );

        var certRes = await httpA.PostAsJsonAsync($"/api/anti-profiteering/assessments/{assessment.Id}/certify", certifyReq);
        certRes.EnsureSuccessStatusCode();
        var certified = (await certRes.Content.ReadFromJsonAsync<AntiProfiteeringAssessmentDto>())!;
        Assert.Equal("Certified", certified.AuditStatus);
        Assert.Equal("24078512AAAAAP7766", certified.Udin);
        Assert.NotNull(certified.StatutoryReportText);
        Assert.Contains("Section 171", certified.StatutoryReportText);
        Assert.Contains("Rule 122-137", certified.StatutoryReportText);
        Assert.Contains("BRITANNIA INDUSTRIES LIMITED", certified.StatutoryReportText.ToUpperInvariant());

        // 8. Two-Lock Isolation Check: Firm B cannot access Firm A's assessment, SKUs, or refunds
        var (httpB, _) = await NewFirmAsync("Grant Thornton Indirect Tax");

        var bGetAssess = await httpB.GetAsync($"/api/anti-profiteering/assessments/{assessment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bGetAssess.StatusCode);

        var bSkus = await httpB.GetFromJsonAsync<List<SkuProfiteeringItemDto>>($"/api/anti-profiteering/assessments/{assessment.Id}/skus");
        Assert.Empty(bSkus!);

        var bRefunds = await httpB.GetFromJsonAsync<List<ProfiteeringRefundRecordDto>>($"/api/anti-profiteering/assessments/{assessment.Id}/refunds");
        Assert.Empty(bRefunds!);
    }
}
