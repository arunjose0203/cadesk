using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Api.Endpoints;
using CaDesk.Application.AuditCommittee;
using CaDesk.Application.Clients;
using CaDesk.Domain.AuditCommittee;
using Xunit;

namespace CaDesk.IntegrationTests;

public class AuditCommitteeTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "KPMG Audit Governance Practice LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Tata Advanced Systems Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task AuditCommittee_FullLifecycle_Deficiencies_Matters_Misstatements_SignOff_And_FirmIsolation()
    {
        var (httpA, _) = await NewFirmAsync("Deloitte Statutory Governance LLP");
        var clientA = await CreateClientAsync(httpA, "Infosys Automotive Ltd");

        // 1. Create Audit Committee Engagement
        var createEngReq = new SaveCommitteeEngagementRequest(
            ClientId: clientA.Id,
            AuditEngagementId: null,
            FinancialYear: "2024-25",
            Title: "Statutory Audit Committee Review Meeting FY 2024-25",
            MeetingType: CommitteeMeetingType.PreIssuance,
            MeetingDate: DateTime.UtcNow.AddDays(7),
            MeetingLocation: "Board Room, Corporate Office, Bangalore",
            CommitteeChairName: "Dr. K. S. Rao (Independent Director)",
            LeadPartnerName: "CA S. Venkatraman",
            MeetingMinutes: null
        );

        var engRes = await httpA.PostAsJsonAsync("/api/auditcommittee/engagements", createEngReq);
        engRes.EnsureSuccessStatusCode();
        var engagement = (await engRes.Content.ReadFromJsonAsync<AuditCommitteeEngagementDto>())!;
        Assert.NotNull(engagement);
        Assert.Equal(AuditCommitteeEngagementStatus.Draft, engagement.Status);
        Assert.Equal("Dr. K. S. Rao (Independent Director)", engagement.CommitteeChairName);

        // 2. Add SA 265 Internal Control Deficiency
        var addDefReq = new SaveDeficiencyRequest(
            DeficiencyCode: "ICD-01",
            Title: "Inadequate segregation of duties in master data changes",
            ProcessArea: "Financial Close & Consolidation",
            DeficiencyType: DeficiencyType.DesignDeficiency,
            Severity: DeficiencySeverity.SignificantDeficiency,
            Description: "Same personnel authorized to initiate vendor master additions and execute wire transfers.",
            PotentialFinancialEffect: "Risk of unauthorized disbursements.",
            EstimatedRiskAmount: 5_000_000m, // ₹50 Lakhs
            AuditorRecommendation: "Segregate master creation rights from disbursement approval in ERP roles.",
            ManagementResponse: "Dual authorization matrix approved by CFO; rollout underway.",
            RemediationTargetDate: DateTime.UtcNow.AddMonths(1),
            Status: DeficiencyStatus.Identified
        );

        var defRes = await httpA.PostAsJsonAsync($"/api/auditcommittee/engagements/{engagement.Id}/deficiencies", addDefReq);
        defRes.EnsureSuccessStatusCode();
        var def = (await defRes.Content.ReadFromJsonAsync<InternalControlDeficiencyDto>())!;
        Assert.NotNull(def);
        Assert.Equal("ICD-01", def.DeficiencyCode);
        Assert.Equal(DeficiencySeverity.SignificantDeficiency, def.Severity);

        // 3. Add SA 260 Significant Audit Matter
        var addMatterReq = new SaveSignificantMatterRequest(
            MatterCode: "SAM-01",
            Subject: "Revenue Recognition for Complex Long-Term Software Licences (Ind AS 115)",
            Category: AuditMatterCategory.KeyAuditMatter,
            Details: "Evaluation of multi-element contracts with unbundled ongoing maintenance.",
            AuditorAssessment: "Allocation of transaction price verified against standalone selling prices.",
            AuditCommitteeFeedback: "Committee concurred with revenue deferral treatment."
        );

        var matterRes = await httpA.PostAsJsonAsync($"/api/auditcommittee/engagements/{engagement.Id}/matters", addMatterReq);
        matterRes.EnsureSuccessStatusCode();
        var matter = (await matterRes.Content.ReadFromJsonAsync<SignificantAuditMatterDto>())!;
        Assert.NotNull(matter);
        Assert.Equal("SAM-01", matter.MatterCode);

        // 4. Add SA 450 Uncorrected Misstatement
        var addMisstatementReq = new SaveUncorrectedMisstatementRequest(
            AccountDescription: "Prepaid insurance amortization timing difference",
            MisstatementAmount: 350_000m, // ₹3.5 Lakhs
            Nature: MisstatementNature.Factual,
            IsMaterial: false,
            ManagementJustificationForNonAdjustment: "Immaterial timing item; will normalize in next financial period."
        );

        var misRes = await httpA.PostAsJsonAsync($"/api/auditcommittee/engagements/{engagement.Id}/misstatements", addMisstatementReq);
        misRes.EnsureSuccessStatusCode();
        var mis = (await misRes.Content.ReadFromJsonAsync<UncorrectedMisstatementDto>())!;
        Assert.NotNull(mis);
        Assert.Equal(350_000m, mis.MisstatementAmount);
        Assert.False(mis.IsMaterial);

        // 5. Generate Governance Pack & SA 265 Letter
        var packRes = await httpA.PostAsync($"/api/auditcommittee/engagements/{engagement.Id}/generate-pack", null);
        packRes.EnsureSuccessStatusCode();
        var packedEng = (await packRes.Content.ReadFromJsonAsync<AuditCommitteeEngagementDto>())!;
        Assert.Equal(AuditCommitteeEngagementStatus.PackGenerated, packedEng.Status);
        Assert.NotNull(packedEng.GeneratedSa265LetterText);
        Assert.Contains("COMMUNICATION OF SIGNIFICANT DEFICIENCIES IN INTERNAL CONTROL", packedEng.GeneratedSa265LetterText);
        Assert.Contains("[ICD-01] Inadequate segregation of duties", packedEng.GeneratedSa265LetterText);
        Assert.NotNull(packedEng.GeneratedGovernancePackText);
        Assert.Contains("STATEMENT OF AUDITOR INDEPENDENCE", packedEng.GeneratedGovernancePackText);
        Assert.Contains("SAM-01", packedEng.GeneratedGovernancePackText);

        // 6. Sign Off Engagement
        var signOffReq = new SignOffCommitteeEngagementRequest(
            Minutes: "Audit Committee reviewed SA 260 presentation, uncorrected misstatements, and SA 265 letter. Committee recommended financial statements for Board approval."
        );
        var signOffRes = await httpA.PostAsJsonAsync($"/api/auditcommittee/engagements/{engagement.Id}/signoff", signOffReq);
        signOffRes.EnsureSuccessStatusCode();
        var signedEng = (await signOffRes.Content.ReadFromJsonAsync<AuditCommitteeEngagementDto>())!;
        Assert.Equal(AuditCommitteeEngagementStatus.SignedOff, signedEng.Status);
        Assert.NotNull(signedEng.SignedOffAt);
        Assert.Equal(1, signedEng.TotalDeficienciesCount);
        Assert.Equal(1, signedEng.SignificantDeficienciesCount);
        Assert.Equal(1, signedEng.SignificantMattersCount);
        Assert.Equal(1, signedEng.UncorrectedMisstatementsCount);

        // 7. Check Summary
        var sumRes = await httpA.GetAsync("/api/auditcommittee/summary");
        sumRes.EnsureSuccessStatusCode();
        var summary = (await sumRes.Content.ReadFromJsonAsync<AuditCommitteeSummaryDto>())!;
        Assert.True(summary.TotalEngagements >= 1);
        Assert.True(summary.SignedOffCount >= 1);
        Assert.True(summary.TotalSignificantDeficiencies >= 1);

        // 8. Multi-Tenant Isolation: Firm B cannot view Firm A's engagement
        var (httpB, _) = await NewFirmAsync("BSR Audit practice");
        var crossFirmRes = await httpB.GetAsync($"/api/auditcommittee/engagements/{engagement.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossFirmRes.StatusCode);
    }
}
