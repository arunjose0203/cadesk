using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Api.Endpoints;
using CaDesk.Application.Clients;
using CaDesk.Application.TaxAudit;
using CaDesk.Domain.TaxAudit;
using Xunit;

namespace CaDesk.IntegrationTests;

public class Form3CdTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "PwC Tax & Advisory LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Godrej Precision Systems Pvt Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Form3Cd_Applicability_Assessment_ClauseDisallowances_ReportGeneration_Finalize_And_TenantIsolation()
    {
        var (httpA, _) = await NewFirmAsync("Deloitte Tax Audit Practice");
        var clientA = await CreateClientAsync(httpA, "ElectroMech Industrial Equipment Pvt Ltd");

        // 1. Evaluate Applicability endpoint
        var evalReq = new EvaluateApplicabilityRequest
        {
            EntityType = "Company",
            IsBusiness = true,
            TurnoverOrGrossReceipts = 35_000_000m, // ₹3.5 Cr
            CashReceipts = 4_000_000m,             // ₹40 Lakhs (> 5% -> ₹1 Cr threshold applies)
            TotalPayments = 28_000_000m,
            CashPayments = 1_000_000m,
            IsAuditedUnderOtherLaw = true
        };

        var evalRes = await httpA.PostAsJsonAsync("/api/taxaudit/evaluate-applicability", evalReq);
        evalRes.EnsureSuccessStatusCode();
        var eval = await evalRes.Content.ReadFromJsonAsync<Form3CdApplicabilityEvaluation>();
        Assert.NotNull(eval);
        Assert.True(eval.IsMandatory);
        Assert.Equal(Form3CdAuditType.Form3CA, eval.RecommendedAuditType);
        Assert.Equal(10_000_000m, eval.StatutoryTurnoverThreshold);

        // 2. Create Assessment
        var createReq = new SaveForm3CdAssessmentRequest
        {
            ClientId = clientA.Id,
            FinancialYear = "2024-25",
            AssessmentYear = "2025-26",
            AuditType = Form3CdAuditType.Form3CA,
            TotalTurnoverOrGrossReceipts = 35_000_000m,
            TotalCashReceipts = 4_000_000m,
            TotalPayments = 28_000_000m,
            TotalCashPayments = 1_000_000m,
            AssesseeName = "ElectroMech Industrial Equipment Pvt Ltd",
            Pan = "AAACE1234F",
            Address = "Plot 42, MIDC Industrial Area, Pune, Maharashtra",
            Gstin = "27AAACE1234F1Z5",
            Status = "Company",
            MethodOfAccounting = "Mercantile",
            OptedSpecialTaxRegime = "115BAA",
            IsAuditedUnderOtherLaw = true,
            IsBusiness = true
        };

        var createRes = await httpA.PostAsJsonAsync("/api/taxaudit/assessments", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<Form3CdAssessmentDto>();
        Assert.NotNull(created);
        Assert.True(created.IsTaxAuditMandatory);
        Assert.Equal(Form3CdAuditType.Form3CA, created.AuditType);
        Assert.NotEmpty(created.ClauseResponses);
        Assert.True(created.ClauseResponses.Count >= 18);

        // 3. Update Clause 26/43B(h) (MSME Overdue) with Disallowance
        var clause43Bh = created.ClauseResponses.First(c => c.ClauseNumber == "Clause 26/43B(h)");
        var update43BhReq = new UpdateForm3CdClauseRequest
        {
            Status = Form3CdClauseStatus.Disallowed,
            ReportedAmount = 120_000m, // ₹1,20,000
            AuditorRemarks = "Payments to Micro Enterprise supplier delayed beyond 45 days agreement limit, unpaid as on 31st March.",
            HasDiscrepancy = true,
            WorkpaperRef = "WP-MSME-01"
        };

        var res43Bh = await httpA.PutAsJsonAsync($"/api/taxaudit/assessments/{created.Id}/clauses/{clause43Bh.Id}", update43BhReq);
        res43Bh.EnsureSuccessStatusCode();

        // 4. Update Clause 21(b) (Sec 40(a)(ia) TDS Defaults) with Disallowance
        var clause40a = created.ClauseResponses.First(c => c.ClauseNumber == "Clause 21(b)");
        var update40aReq = new UpdateForm3CdClauseRequest
        {
            Status = Form3CdClauseStatus.Disallowed,
            ReportedAmount = 60_000m, // ₹60,000
            AuditorRemarks = "30% disallowance u/s 40(a)(ia) for non-deduction of TDS on subcontract payments of ₹2,00,000.",
            HasDiscrepancy = true,
            WorkpaperRef = "WP-TDS-07"
        };

        var res40a = await httpA.PutAsJsonAsync($"/api/taxaudit/assessments/{created.Id}/clauses/{clause40a.Id}", update40aReq);
        res40a.EnsureSuccessStatusCode();
        var afterClauses = await res40a.Content.ReadFromJsonAsync<Form3CdAssessmentDto>();
        Assert.NotNull(afterClauses);
        Assert.Equal(120_000m, afterClauses.TotalDisallowanceSec43Bh);
        Assert.Equal(60_000m, afterClauses.TotalDisallowanceSec40a);
        Assert.Equal(180_000m, afterClauses.TotalNetIncomeAdjustment);
        Assert.Equal(2, afterClauses.QualificationsCount);

        // 5. Generate Report Text Draft
        var reportRes = await httpA.PostAsync($"/api/taxaudit/assessments/{created.Id}/generate-report", null);
        reportRes.EnsureSuccessStatusCode();

        // 6. Finalize Assessment
        var finalizeRes = await httpA.PostAsJsonAsync($"/api/taxaudit/assessments/{created.Id}/finalize", new FinalizeForm3CdRequest(
            "Tax Audit particulars finalized. Net additions to taxable business profit: ₹1,80,000."
        ));
        finalizeRes.EnsureSuccessStatusCode();
        var finalized = await finalizeRes.Content.ReadFromJsonAsync<Form3CdAssessmentDto>();
        Assert.NotNull(finalized);
        Assert.Equal(Form3CdStatus.Finalized, finalized.AssessmentStatus);
        Assert.NotNull(finalized.FinalizedAt);
        Assert.Contains("FORM NO. 3CA", finalized.GeneratedReportText);
        Assert.Contains("Clause 26/43B(h)", finalized.GeneratedReportText);

        // 7. Verify Summary endpoint
        var sumRes = await httpA.GetAsync("/api/taxaudit/summary");
        sumRes.EnsureSuccessStatusCode();
        var summary = await sumRes.Content.ReadFromJsonAsync<Form3CdSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalAssessments);
        Assert.Equal(1, summary.MandatoryCount);
        Assert.Equal(1, summary.FinalizedCount);
        Assert.Equal(180_000m, summary.TotalNetDisallowances);

        // 8. Multi-Tenant Isolation: Firm B cannot access Firm A's assessment
        var (httpB, _) = await NewFirmAsync("EY Tax Practice LLP");
        var resFirmB = await httpB.GetAsync($"/api/taxaudit/assessments/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resFirmB.StatusCode);
    }
}
