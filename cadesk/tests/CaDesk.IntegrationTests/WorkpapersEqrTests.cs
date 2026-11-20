using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Audit;
using CaDesk.Application.Clients;
using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.IntegrationTests;

public class WorkpapersEqrTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "PwC Audit Practice LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Hindustan Unilever Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Audit_EqrAndWorkpapers_Lifecycle_ReviewNotes_HotReviewClearance_Signoff_And_60DayArchivalLock()
    {
        var (http, userId) = await NewFirmAsync("EY Statutory Audit Desk");
        var client = await CreateClientAsync(http, "Larsen & Toubro Ltd");

        // 1. Create audit engagement with EQR Required
        var createEngReq = new CreateAuditEngagementRequest(
            ClientId: client.Id,
            FinancialYear: "2024-25",
            AuditType: AuditType.Statutory,
            Title: "Statutory Audit of Larsen & Toubro FY 2024-25",
            LeadPartnerId: userId,
            ManagerId: userId,
            PlannedStartDate: new DateOnly(2025, 4, 1),
            PlannedEndDate: new DateOnly(2025, 9, 30),
            BenchmarkType: BenchmarkType.Revenue,
            BenchmarkAmount: 50_000_000m,
            EqrRequired: true,
            EqrPartnerId: userId,
            Notes: "High risk listed entity requiring SA 220 hot review."
        );

        var engRes = await http.PostAsJsonAsync("/api/audit/engagements", createEngReq);
        Assert.Equal(HttpStatusCode.Created, engRes.StatusCode);
        var eng = await engRes.Content.ReadFromJsonAsync<AuditEngagementDto>();
        Assert.NotNull(eng);
        Assert.True(eng.EqrRequired);
        Assert.Equal(EqrReviewStatus.Assigned, eng.EqrStatus);

        // 2. Add an audit review note to test gate-check
        var noteReq = new CreateReviewNoteRequest(
            WorkingPaperId: null,
            RaisedByRole: ReviewNoteRole.Manager,
            Category: ReviewNoteCategory.DocumentationGap,
            Priority: ReviewNotePriority.High,
            Query: "Please attach SA 505 external confirmation reconciliation for Axis Bank account."
        );
        var noteRes = await http.PostAsJsonAsync($"/api/audit/engagements/{eng.Id}/review-notes", noteReq);
        Assert.Equal(HttpStatusCode.Created, noteRes.StatusCode);
        var note = await noteRes.Content.ReadFromJsonAsync<AuditReviewNoteDto>();
        Assert.NotNull(note);
        Assert.Equal(ReviewNoteStatus.Open, note.Status);

        // 3. Attempt sign-off -> should be blocked by gate-check
        var signoffReq = new SignOffAuditReportRequest(
            AuditReportDate: new DateOnly(2025, 9, 30),
            AuditOpinion: AuditOpinion.Unmodified,
            Notes: "Unmodified true and fair audit report."
        );
        var blockedSignoffRes = await http.PostAsJsonAsync($"/api/audit/engagements/{eng.Id}/signoff", signoffReq);
        Assert.Equal(HttpStatusCode.InternalServerError, blockedSignoffRes.StatusCode); // throws InvalidOperationException

        // 4. Respond to review note
        var respondRes = await http.PostAsJsonAsync($"/api/audit/review-notes/{note.Id}/respond", new RespondReviewNoteRequest(
            "Axis bank certificate obtained and reconciled with ledger. No variance."
        ));
        Assert.Equal(HttpStatusCode.OK, respondRes.StatusCode);

        // 5. Clear review note
        var clearNoteRes = await http.PostAsJsonAsync($"/api/audit/review-notes/{note.Id}/clear", new { });
        Assert.Equal(HttpStatusCode.OK, clearNoteRes.StatusCode);

        // 6. Fetch and Clear EQR Review
        var eqrRes = await http.GetAsync($"/api/audit/engagements/{eng.Id}/eqr");
        Assert.Equal(HttpStatusCode.OK, eqrRes.StatusCode);
        var eqr = await eqrRes.Content.ReadFromJsonAsync<AuditEqrReviewDto>();
        Assert.NotNull(eqr);

        // Verify checklist items
        var verifiedChecklist = eqr.PreIssuanceChecklist.Select(c => new PreIssuanceChecklistItemDto(
            c.Code, c.Description, c.Required, true, "Verified satisfactorily", DateTime.UtcNow
        )).ToList();

        var clearEqrReq = new ClearEqrReviewRequest(
            EqrConclusionRemarks: "Concurred with unmodified opinion. Significant risks and materiality evaluated under SA 220.",
            PreIssuanceChecklist: verifiedChecklist
        );

        var clearEqrRes = await http.PostAsJsonAsync($"/api/audit/eqr/{eqr.Id}/clear", clearEqrReq);
        Assert.Equal(HttpStatusCode.OK, clearEqrRes.StatusCode);
        var clearedEqr = await clearEqrRes.Content.ReadFromJsonAsync<AuditEqrReviewDto>();
        Assert.NotNull(clearedEqr);
        Assert.Equal(EqrReviewStatus.ClearanceGranted, clearedEqr.ReviewStatus);

        // 7. Sign off audit report successfully
        var allowedSignoffRes = await http.PostAsJsonAsync($"/api/audit/engagements/{eng.Id}/signoff", signoffReq);
        Assert.Equal(HttpStatusCode.OK, allowedSignoffRes.StatusCode);
        var signedOffEng = await allowedSignoffRes.Content.ReadFromJsonAsync<AuditEngagementDto>();
        Assert.NotNull(signedOffEng);
        Assert.Equal(EngagementStatus.PartnerSignoff, signedOffEng.Status);
        Assert.Equal(new DateOnly(2025, 11, 29), signedOffEng.ArchivalDeadline); // exact 60 days!

        // 8. Seal and lock engagement file under ICAI SQC 1
        var archiveRes = await http.PostAsJsonAsync($"/api/audit/engagements/{eng.Id}/archive", new ArchiveEngagementRequest(
            "Assembled and archived within 60 days as per ICAI SQC 1."
        ));
        Assert.Equal(HttpStatusCode.OK, archiveRes.StatusCode);
        var archivedEng = await archiveRes.Content.ReadFromJsonAsync<AuditEngagementDto>();
        Assert.NotNull(archivedEng);
        Assert.True(archivedEng.IsArchived);
        Assert.Contains("Archived & Locked", archivedEng.ArchivalStatus);

        // 9. Verify immutable lock: attempts to create/edit working paper must fail
        var wpReq = new SaveWorkingPaperRequest(
            RefCode: "WP-TEST-01",
            Area: "Revenue",
            Title: "Post-Archival Edit Attempt",
            Assertion: AuditAssertion.Existence,
            Objective: "Testing lock",
            ProceduresPerformed: "None",
            SamplingMethod: SamplingMethod.Haphazard,
            PopulationValue: 0m,
            SampleValue: 0m,
            ItemsTestedCount: 0,
            ExceptionsNoted: null,
            Conclusion: AuditConclusion.Satisfactory
        );
        var lockedWpRes = await http.PostAsJsonAsync($"/api/audit/engagements/{eng.Id}/working-papers", wpReq);
        Assert.Equal(HttpStatusCode.InternalServerError, lockedWpRes.StatusCode); // Blocked by SQC 1 archival lock
    }

    [Fact]
    public async Task Audit_MultiTenantIsolation_FirmBCannotAccessFirmAEngagements()
    {
        var (httpA, userIdA) = await NewFirmAsync("Firm A Auditors");
        var clientA = await CreateClientAsync(httpA, "Client Alpha Private Ltd");

        var createReq = new CreateAuditEngagementRequest(
            ClientId: clientA.Id,
            FinancialYear: "2024-25",
            AuditType: AuditType.Statutory,
            Title: "Firm A Audit",
            LeadPartnerId: userIdA,
            ManagerId: userIdA,
            PlannedStartDate: new DateOnly(2025, 4, 1),
            PlannedEndDate: new DateOnly(2025, 9, 30),
            BenchmarkType: BenchmarkType.Revenue,
            BenchmarkAmount: 10_000_000m,
            EqrRequired: false,
            EqrPartnerId: null,
            Notes: null
        );

        var resA = await httpA.PostAsJsonAsync("/api/audit/engagements", createReq);
        Assert.Equal(HttpStatusCode.Created, resA.StatusCode);
        var engA = await resA.Content.ReadFromJsonAsync<AuditEngagementDto>();
        Assert.NotNull(engA);

        // Firm B signs up and attempts to read Firm A's engagement
        var (httpB, _) = await NewFirmAsync("Firm B Auditors");

        var getRes = await httpB.GetAsync($"/api/audit/engagements/{engA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }
}
