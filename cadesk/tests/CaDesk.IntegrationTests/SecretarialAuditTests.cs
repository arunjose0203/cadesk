using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.SecretarialAudit;
using Xunit;

namespace CaDesk.IntegrationTests;

public class SecretarialAuditTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F", ["CIN"] = "L17110MH1973PLC019786" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "KPMG Corporate Governance Advisors")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Reliance Industries Limited")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task SecretarialAudit_FullLifecycle_And_TwoLock_Isolation()
    {
        var (httpA, _) = await NewFirmAsync("Singhi & Co Company Secretaries");
        var clientA = await CreateClientAsync(httpA, "Tata Steel Limited");

        // 1. Applicability Check Endpoint
        var checkReq = new ApplicabilityCheckRequest(
            CompanyType: "ListedPublic",
            PaidUpShareCapital: 120_00_00_000m,
            Turnover: 1500_00_00_000m,
            OutstandingBorrowings: 500_00_00_000m);
        var checkRes = await httpA.PostAsJsonAsync("/api/secretarial-audit/check-applicability", checkReq);
        checkRes.EnsureSuccessStatusCode();
        var checkResult = (await checkRes.Content.ReadFromJsonAsync<ApplicabilityCheckResult>())!;
        Assert.True(checkResult.IsApplicable);

        // 2. Create Engagement (Unlisted Public with 60 Cr capital)
        var createReq = new SaveEngagementRequest(
            ClientId: clientA.Id,
            EngagementReference: "SEC-2024-TATA-01",
            Title: "Statutory Secretarial Audit FY 2024-25",
            FinancialYear: "2024-25",
            AuditPeriodStart: new DateTime(2024, 4, 1),
            AuditPeriodEnd: new DateTime(2025, 3, 31),
            Cin: "L27100MH1907PLC000260",
            CompanyType: "UnlistedPublic",
            PaidUpShareCapital: 60_00_00_000m,
            Turnover: 150_00_00_000m,
            OutstandingBorrowings: 80_00_00_000m,
            AuditorName: "CS Alok Singhi",
            AuditorFirmName: "Singhi & Co",
            MembershipType: "FCS",
            MembershipNumber: "7766",
            CertificateOfPracticeNumber: "11223",
            PeerReviewCertificateNumber: "PR-2024-88");

        var createRes = await httpA.PostAsJsonAsync("/api/secretarial-audit/engagements", createReq);
        createRes.EnsureSuccessStatusCode();
        var eng = (await createRes.Content.ReadFromJsonAsync<SecretarialAuditEngagementDto>())!;
        Assert.NotEqual(Guid.Empty, eng.Id);
        Assert.True(eng.IsApplicable);
        Assert.True(eng.TotalChecklistCount >= 14); // Baseline seeded

        // 3. Fetch Seeded Checklist
        var checklist = await httpA.GetFromJsonAsync<List<SecretarialChecklistItemDto>>($"/api/secretarial-audit/engagements/{eng.Id}/checklist");
        Assert.NotNull(checklist);
        Assert.NotEmpty(checklist);

        // 4. Batch Update Checklist Item
        var firstItem = checklist[0];
        var batchReq = new BatchUpdateChecklistRequest(new List<BatchChecklistUpdateItem>
        {
            new(firstItem.Id, "Complied", "Verified with ROC records and board registers.", false, "None")
        });
        var batchRes = await httpA.PostAsJsonAsync($"/api/secretarial-audit/engagements/{eng.Id}/checklist/batch", batchReq);
        batchRes.EnsureSuccessStatusCode();

        // 5. Add MCA Form Filing
        var filingReq = new SaveMcaFilingRequest(
            FormName: "AOC-4",
            Description: "Financial Statements Filing",
            EventDate: new DateTime(2024, 9, 30),
            StatutoryDaysAllowed: 30,
            ActualFilingDate: new DateTime(2024, 10, 25), // On time
            Srn: "F99887766",
            AdditionalFeePaid: 0m,
            AuditorRemarks: "Filed within statutory 30 days.");
        var filingRes = await httpA.PostAsJsonAsync($"/api/secretarial-audit/engagements/{eng.Id}/filings", filingReq);
        filingRes.EnsureSuccessStatusCode();
        var filing = (await filingRes.Content.ReadFromJsonAsync<McaFormFilingAuditDto>())!;
        Assert.Equal("FiledOnTime", filing.FilingStatus);

        // 6. Add Board Meeting Record
        var meetingReq = new SaveBoardMeetingAuditRequest(
            MeetingType: "BoardMeeting",
            MeetingDate: new DateTime(2024, 6, 20),
            PreviousMeetingDate: new DateTime(2024, 4, 10), // 71 days <= 120
            NoticeDispatchDate: new DateTime(2024, 6, 10), // 10 days >= 7
            TotalMembersCount: 8,
            AttendedMembersCount: 6,
            IndependentDirectorsAttended: 3,
            SecretarialStandardComplied: true,
            AuditorRemarks: "Proper quorum present, notice compliant.");
        var meetingRes = await httpA.PostAsJsonAsync($"/api/secretarial-audit/engagements/{eng.Id}/meetings", meetingReq);
        meetingRes.EnsureSuccessStatusCode();
        var meeting = (await meetingRes.Content.ReadFromJsonAsync<BoardMeetingAuditRecordDto>())!;
        Assert.True(meeting.IsGapCompliant);
        Assert.True(meeting.IsNoticeCompliant);
        Assert.True(meeting.IsQuorumCompliant);

        // 7. Certify Engagement with UDIN
        var certifyReq = new CertifyEngagementRequest(
            Udin: "F007766B0009876543",
            CertifiedBy: "CS Alok Singhi, FCS");
        var certifyRes = await httpA.PostAsJsonAsync($"/api/secretarial-audit/engagements/{eng.Id}/certify", certifyReq);
        certifyRes.EnsureSuccessStatusCode();
        var certifiedEng = (await certifyRes.Content.ReadFromJsonAsync<SecretarialAuditEngagementDto>())!;
        Assert.Equal("Certified", certifiedEng.AuditStatus);
        Assert.Equal("F007766B0009876543", certifiedEng.Udin);
        Assert.Contains("FORM NO. MR-3", certifiedEng.FormMR3ReportText);

        // 8. Multi-Tenancy Two-Lock Verification: Firm B cannot see Firm A's engagement
        var (httpB, _) = await NewFirmAsync("BDO Company Secretarial Services");
        var firmBList = await httpB.GetFromJsonAsync<List<SecretarialAuditEngagementDto>>("/api/secretarial-audit/engagements");
        Assert.NotNull(firmBList);
        Assert.DoesNotContain(firmBList, x => x.Id == eng.Id);

        var firmBGetRes = await httpB.GetAsync($"/api/secretarial-audit/engagements/{eng.Id}");
        Assert.Equal(HttpStatusCode.NotFound, firmBGetRes.StatusCode);
    }
}
