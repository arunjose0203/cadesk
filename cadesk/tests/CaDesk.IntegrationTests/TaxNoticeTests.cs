using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.TaxNotices;
using CaDesk.Domain.TaxNotices;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 22: Tax Notice & Faceless Assessment Management Desk.
/// Tests statutory notice registration, DIN verification, hearing appointment scheduling,
/// written submission draft & partner approval lifecycle, demand appeal countdowns, and multi-tenant firm isolation.
/// </summary>
public class TaxNoticeTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Assessment Advisory Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Bharat Petroleum Corp")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Notice_Lifecycle_Hearing_Approval_And_Demand_Workflow_Succeeds()
    {
        var (http, _) = await NewFirmAsync("KPMG Tax Advisory");
        var client = await CreateClientAsync(http, "Tata Motors Ltd");

        // 1. Create Tax Notice
        var noticeReq = new CreateTaxNoticeRequest(
            ClientId: client.Id,
            Authority: TaxAuthority.IncomeTax,
            Category: NoticeCategory.Inquiry142_1,
            Section: "Sec 142(1)",
            NoticeNumber: "ITBA/AST/S/142(1)/2024-25/998877",
            Din: "ITBA/COM/F/17/2024-25/1068834928(1)",
            AssessmentYear: "2024-25",
            FinancialYear: "2023-24",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            DemandAmount: 450000m,
            RefundDisallowed: 0m,
            Subject: "Inquiry regarding large cash transactions in Schedule BP",
            Description: "Explain source of cash deposits exceeding Rs 10 Lakhs during FY 2023-24",
            AssignedToUserId: null,
            OfficerName: "Shri R. K. Sharma",
            OfficerJurisdiction: "NFAC Delhi Assessment Unit 3"
        );

        var createRes = await http.PostAsJsonAsync("/api/tax-notices", noticeReq);
        createRes.EnsureSuccessStatusCode();
        var notice = await createRes.Content.ReadFromJsonAsync<TaxNoticeDto>();
        Assert.NotNull(notice);
        Assert.Equal("Sec 142(1)", notice.Section);
        Assert.Equal(DinStatus.Valid, notice.DinStatus);
        Assert.Equal(450000m, notice.DemandAmount);

        // 2. Schedule Video Conference Hearing
        var hearingReq = new CreateHearingRequest(
            HearingDate: DateTimeOffset.UtcNow.AddDays(5),
            HearingType: HearingType.VideoConference,
            OfficerName: "Shri R. K. Sharma",
            OfficerDesignation: "Additional CIT (NFAC)",
            MeetingLink: "https://webex.incometax.gov.in/m/nfac-hearing-998877",
            SummaryNotes: "Preliminary VC on books of account verification"
        );
        var hearingRes = await http.PostAsJsonAsync($"/api/tax-notices/{notice.Id}/hearings", hearingReq);
        hearingRes.EnsureSuccessStatusCode();
        var hearing = await hearingRes.Content.ReadFromJsonAsync<NoticeHearingDto>();
        Assert.NotNull(hearing);
        Assert.Equal(HearingType.VideoConference, hearing.HearingType);

        // 3. Draft Written Submission
        var draftReq = new DraftResponseRequest(
            SubmissionTitle: "Written Submission in response to Sec 142(1)",
            SummaryOfSubmissions: "Furnished bank statements, cash ledger, and VAT/GST monthly returns matching cash deposits.",
            LegalGrounds: "Cash sales deposited into bank in ordinary course of business, duly recorded in audited books.",
            AttachmentNames: "Cash_Ledger.pdf, Bank_Statements_FY24.pdf"
        );
        var responseRes = await http.PostAsJsonAsync($"/api/tax-notices/{notice.Id}/responses", draftReq);
        responseRes.EnsureSuccessStatusCode();
        var draft = await responseRes.Content.ReadFromJsonAsync<NoticeResponseDto>();
        Assert.NotNull(draft);
        Assert.Equal(ResponseStatus.Draft, draft.Status);

        // 4. Partner Maker-Checker Approval
        var approveRes = await http.PostAsync($"/api/tax-notices/responses/{draft.Id}/approve", null);
        approveRes.EnsureSuccessStatusCode();
        var approved = await approveRes.Content.ReadFromJsonAsync<NoticeResponseDto>();
        Assert.NotNull(approved);
        Assert.Equal(ResponseStatus.PartnerApproved, approved.Status);

        // 5. Submit to E-Filing Portal with Acknowledgement
        var submitReq = new RecordSubmissionRequest(
            PortalAckNumber: "ACK-ITBA-2024-9988776655",
            SubmittedAt: DateTimeOffset.UtcNow
        );
        var submitRes = await http.PostAsJsonAsync($"/api/tax-notices/responses/{draft.Id}/submit", submitReq);
        submitRes.EnsureSuccessStatusCode();
        var submitted = await submitRes.Content.ReadFromJsonAsync<NoticeResponseDto>();
        Assert.NotNull(submitted);
        Assert.Equal(ResponseStatus.UploadedToPortal, submitted.Status);
        Assert.Equal("ACK-ITBA-2024-9988776655", submitted.PortalAckNumber);

        // 6. Check Demand Register and Record Partial Payment
        var demandsRes = await http.GetFromJsonAsync<List<TaxDemandDto>>($"/api/tax-notices/demands?clientId={client.Id}");
        Assert.NotNull(demandsRes);
        var demand = Assert.Single(demandsRes);
        Assert.Equal(450000m, demand.OriginalDemand);
        Assert.Equal(450000m, demand.OutstandingAmount);

        // Record 150,000 payment
        var payRes = await http.PostAsJsonAsync($"/api/tax-notices/demands/{demand.Id}/payment", new RecordDemandPaymentRequest(
            Amount: 150000m,
            IsRefundAdjustment: false,
            Remarks: "Challan 280 BSR 0210045"
        ));
        payRes.EnsureSuccessStatusCode();
        var paidDemand = await payRes.Content.ReadFromJsonAsync<TaxDemandDto>();
        Assert.NotNull(paidDemand);
        Assert.Equal(300000m, paidDemand.OutstandingAmount);

        // 7. Verify Dashboard Summary
        var summary = await http.GetFromJsonAsync<NoticeDashboardSummaryDto>("/api/tax-notices/summary");
        Assert.NotNull(summary);
        Assert.True(summary.TotalActiveNotices >= 1);
        Assert.True(summary.IncomeTaxNotices >= 1);
        Assert.True(summary.TotalDemandAmount >= 450000m);
    }

    [Fact]
    public async Task Two_Lock_Multi_Tenancy_Protects_Firm_Notices_From_Cross_Access()
    {
        var (firmA, _) = await NewFirmAsync("Firm A Advocates");
        var (firmB, _) = await NewFirmAsync("Firm B Tax Associates");

        var clientA = await CreateClientAsync(firmA, "Client of Firm A");

        // Firm A creates a notice
        var noticeReq = new CreateTaxNoticeRequest(
            ClientId: clientA.Id,
            Authority: TaxAuthority.Gst,
            Category: NoticeCategory.GstDrc01,
            Section: "DRC-01",
            NoticeNumber: "ZA2709240019999",
            Din: "20240927001999999999",
            AssessmentYear: null,
            FinancialYear: "2024-25",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            DemandAmount: 85000m,
            RefundDisallowed: 0m,
            Subject: "Discrepancy in GSTR-1 vs GSTR-3B tax liability",
            Description: null,
            AssignedToUserId: null,
            OfficerName: "Superintendent Range 2",
            OfficerJurisdiction: "Mumbai South GST"
        );

        var resA = await firmA.PostAsJsonAsync("/api/tax-notices", noticeReq);
        resA.EnsureSuccessStatusCode();
        var noticeA = (await resA.Content.ReadFromJsonAsync<TaxNoticeDto>())!;

        // Firm B tries to get Firm A's notice directly -> Must be 404
        var resBDirect = await firmB.GetAsync($"/api/tax-notices/{noticeA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resBDirect.StatusCode);

        // Firm B list notices -> Must not contain noticeA
        var listB = await firmB.GetFromJsonAsync<List<TaxNoticeDto>>("/api/tax-notices");
        Assert.NotNull(listB);
        Assert.DoesNotContain(listB, n => n.Id == noticeA.Id);
    }
}
