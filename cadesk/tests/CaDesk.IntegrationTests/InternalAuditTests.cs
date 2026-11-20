using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.InternalAudit;
using Xunit;

namespace CaDesk.IntegrationTests;

public class InternalAuditTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "PwC Risk Advisory LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Godrej Agrovet Limited")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task InternalAudit_FullLifecycle_Engagements_Rcm_Testing_Findings_Report_And_Isolation()
    {
        var (httpA, _) = await NewFirmAsync("EY Risk Consulting LLP");
        var clientA = await CreateClientAsync(httpA, "Mahindra Logistics Limited");

        // 1. Create Engagement with standard RCM template populated
        var createEngReq = new SaveInternalAuditEngagementRequest(
            ClientId: clientA.Id,
            FinancialYear: "2024-25",
            AuditPeriod: "Half-Yearly (H1 FY 2024-25)",
            Title: "Comprehensive Internal Audit & IFC-FR Testing",
            EngagementLeadName: "CA Anita Desai, Partner",
            AuditScopeDescription: "Testing operating effectiveness of internal financial controls for P2P, O2C, H2R, R2R, Treasury & ITGC under Companies Act Sec 138 & 143(3)(i).",
            Status: "InProgress"
        );

        var engRes = await httpA.PostAsJsonAsync("/api/internal-audit/engagements?populateStandardRcm=true", createEngReq);
        engRes.EnsureSuccessStatusCode();
        var eng = (await engRes.Content.ReadFromJsonAsync<InternalAuditEngagementDto>())!;
        Assert.NotNull(eng);
        Assert.Equal("Comprehensive Internal Audit & IFC-FR Testing", eng.Title);
        Assert.Equal(6, eng.TotalControls);

        // 2. List RCM Controls
        var controlsRes = await httpA.GetFromJsonAsync<List<RcmControlItemDto>>($"/api/internal-audit/engagements/{eng.Id}/controls");
        Assert.NotNull(controlsRes);
        Assert.Equal(6, controlsRes.Count);

        var p2pControl = controlsRes.Find(x => x.ProcessCycle == "P2P");
        Assert.NotNull(p2pControl);
        Assert.Equal("P2P-CTL-01", p2pControl.ControlCode);
        Assert.Equal("OperatingEffectively", p2pControl.OperatingEffectiveness);

        // 3. Update P2P Control with Walkthrough and Test of Operating Effectiveness (Exceptions Found)
        var updateControlReq = new SaveRcmControlItemRequest(
            ControlCode: p2pControl.ControlCode,
            ProcessCycle: p2pControl.ProcessCycle,
            SubProcess: p2pControl.SubProcess,
            RiskDescription: p2pControl.RiskDescription,
            RiskCategory: p2pControl.RiskCategory,
            InherentRiskRating: p2pControl.InherentRiskRating,
            ControlActivity: p2pControl.ControlActivity,
            ControlNature: p2pControl.ControlNature,
            ControlFrequency: p2pControl.ControlFrequency,
            ControlType: p2pControl.ControlType,
            ControlOwner: "Accounts Payable Manager",
            Assertions: "Accuracy, Completeness",
            WalkthroughPerformed: true,
            WalkthroughDate: DateTime.UtcNow.AddDays(-15),
            WalkthroughReference: "WP-P2P-WT-01",
            DesignEffectiveness: "Effective",
            DesignNotes: "Three-way match logic configured in SAP ECC 6.0",
            TestingMethod: "Reperformance",
            SampleSize: 25,
            ExceptionsCount: 2, // 2 exceptions!
            OperatingEffectiveness: "Ineffective",
            TestingNotes: "Identified two vendor invoices processed without approved purchase orders.",
            TestedAt: DateTime.UtcNow.AddDays(-2),
            TestedBy: "CA Anita Desai"
        );

        var updateControlRes = await httpA.PutAsJsonAsync($"/api/internal-audit/engagements/{eng.Id}/controls/{p2pControl.Id}", updateControlReq);
        updateControlRes.EnsureSuccessStatusCode();
        var updatedControl = (await updateControlRes.Content.ReadFromJsonAsync<RcmControlItemDto>())!;
        Assert.Equal("High", updatedControl.ResidualRiskRating);
        Assert.True(updatedControl.HasFinding);

        // 4. Update O2C Control with Operating Effectiveness: Effective (0 exceptions)
        var o2cControl = controlsRes.Find(x => x.ProcessCycle == "O2C");
        Assert.NotNull(o2cControl);
        var updateO2cReq = new SaveRcmControlItemRequest(
            ControlCode: o2cControl.ControlCode,
            ProcessCycle: o2cControl.ProcessCycle,
            SubProcess: o2cControl.SubProcess,
            RiskDescription: o2cControl.RiskDescription,
            RiskCategory: o2cControl.RiskCategory,
            InherentRiskRating: o2cControl.InherentRiskRating,
            ControlActivity: o2cControl.ControlActivity,
            ControlNature: o2cControl.ControlNature,
            ControlFrequency: o2cControl.ControlFrequency,
            ControlType: o2cControl.ControlType,
            ControlOwner: "Credit Control Head",
            Assertions: "Valuation, Existence",
            WalkthroughPerformed: true,
            WalkthroughDate: DateTime.UtcNow.AddDays(-20),
            WalkthroughReference: "WP-O2C-WT-01",
            DesignEffectiveness: "Effective",
            DesignNotes: "Credit limit controls active.",
            TestingMethod: "Inspection",
            SampleSize: 30,
            ExceptionsCount: 0,
            OperatingEffectiveness: "Effective",
            TestingNotes: "Tested 30 customer sales orders; all adhered to approved credit matrix.",
            TestedAt: DateTime.UtcNow.AddDays(-5),
            TestedBy: "Rahul Verma (Senior Auditor)"
        );

        var updateO2cRes = await httpA.PutAsJsonAsync($"/api/internal-audit/engagements/{eng.Id}/controls/{o2cControl.Id}", updateO2cReq);
        updateO2cRes.EnsureSuccessStatusCode();
        var updatedO2c = (await updateO2cRes.Content.ReadFromJsonAsync<RcmControlItemDto>())!;
        Assert.Equal("Low", updatedO2c.ResidualRiskRating);
        Assert.False(updatedO2c.HasFinding);

        // 5. Log Internal Audit Finding
        var logFindingReq = new SaveInternalAuditFindingRequest(
            ControlItemId: p2pControl.Id,
            ControlCode: "P2P-001",
            FindingReference: "IA-FIND-001",
            Title: "Invoices Approved Without Valid Purchase Order (Bypass of 3-Way Match)",
            ProcessCycle: "P2P",
            Severity: "High",
            DetailedObservation: "During testing of 25 procurement transactions, 2 invoices totaling Rs. 3,85,000 were released for payment without system-linked PO or goods receipt confirmation.",
            RootCause: "Emergency spare procurement circumvented standard SAP workflow by manual finance override.",
            EstimatedRiskAmount: 385000m,
            IfcClassification: "SignificantDeficiency",
            AuditorRecommendation: "Disable manual financial document posting in SAP without mandatory purchase order reference. Mandate retrospectively regularized PO approvals.",
            ManagementActionPlan: "SAP system posting rules will be locked to disallow non-PO invoice entry. Training to be imparted to plant maintenance team by Q3.",
            ActionOwner: "Vikram Singhania (CFO)",
            TargetCompletionDate: DateTime.UtcNow.AddMonths(1),
            ImplementationStatus: "Open"
        );

        var findingRes = await httpA.PostAsJsonAsync($"/api/internal-audit/engagements/{eng.Id}/findings", logFindingReq);
        findingRes.EnsureSuccessStatusCode();
        var finding = (await findingRes.Content.ReadFromJsonAsync<InternalAuditFindingDto>())!;
        Assert.NotNull(finding);
        Assert.Equal("IA-FIND-001", finding.FindingReference);
        Assert.Equal("SignificantDeficiency", finding.IfcClassification);
        Assert.Equal(385000m, finding.EstimatedRiskAmount);

        // 6. Update Finding Status
        var updateStatusReq = new UpdateFindingStatusRequest(
            ImplementationStatus: "InProgress",
            ManagementActionPlan: "System config change in progress in SAP sandbox environment.",
            TargetCompletionDate: DateTime.UtcNow.AddDays(45)
        );

        var statusRes = await httpA.PatchAsync($"/api/internal-audit/engagements/{eng.Id}/findings/{finding.Id}/status", JsonContent.Create(updateStatusReq));
        statusRes.EnsureSuccessStatusCode();
        var updatedFinding = (await statusRes.Content.ReadFromJsonAsync<InternalAuditFindingDto>())!;
        Assert.Equal("InProgress", updatedFinding.ImplementationStatus);

        // 7. Verify Summary Stats
        var summary = await httpA.GetFromJsonAsync<InternalAuditSummaryDto>($"/api/internal-audit/summary?clientId={clientA.Id}");
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalEngagements);
        Assert.Equal(6, summary.TotalControlsTested); // All 6 template controls
        // 5 effective out of 6 tested = 83.33%
        Assert.Equal(83.33m, summary.OverallHealthScore);
        Assert.Equal(1, summary.TotalDeficiencies);
        Assert.Equal(1, summary.OpenActionsCount);
        Assert.Equal(385000m, summary.TotalFinancialExposure);

        // 8. Generate Executive Report
        var reportRes = await httpA.PostAsync($"/api/internal-audit/engagements/{eng.Id}/generate-report", null);
        reportRes.EnsureSuccessStatusCode();
        var reportEng = (await reportRes.Content.ReadFromJsonAsync<InternalAuditEngagementDto>())!;
        Assert.Equal("Reporting", reportEng.Status);
        Assert.NotNull(reportEng.GeneratedReportText);
        Assert.Contains("Mahindra Logistics Limited", reportEng.GeneratedReportText);
        Assert.Contains("Section 138", reportEng.GeneratedReportText);
        Assert.Contains("143(3)(i)", reportEng.GeneratedReportText);
        Assert.Contains("83.33%", reportEng.GeneratedReportText);

        // 9. Two-Lock Isolation Check: Firm B cannot access Firm A's data
        var (httpB, _) = await NewFirmAsync("KPMG Assurance Services LLP");

        var bGetEng = await httpB.GetAsync($"/api/internal-audit/engagements/{eng.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bGetEng.StatusCode);

        var bControls = await httpB.GetFromJsonAsync<List<RcmControlItemDto>>($"/api/internal-audit/engagements/{eng.Id}/controls");
        Assert.Empty(bControls!);

        var bFindings = await httpB.GetFromJsonAsync<List<InternalAuditFindingDto>>($"/api/internal-audit/engagements/{eng.Id}/findings");
        Assert.Empty(bFindings!);
    }
}
