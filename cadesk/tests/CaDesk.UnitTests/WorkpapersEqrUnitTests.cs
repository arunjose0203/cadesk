using CaDesk.Application.Audit;
using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.UnitTests;

public class WorkpapersEqrUnitTests
{
    [Fact]
    public void ArchivalDeadline_CalculatesExact60DaysUnderSqc1()
    {
        var auditReportDate = new DateOnly(2025, 9, 30);
        var deadline = AuditQualityEngine.CalculateArchivalDeadline(auditReportDate);

        // Sep 30 + 60 days = Nov 29, 2025
        Assert.Equal(new DateOnly(2025, 11, 29), deadline);
        Assert.Equal(60, deadline.DayNumber - auditReportDate.DayNumber);
    }

    [Fact]
    public void ArchivalStatus_EvaluatesLifecycleStatesCorrectly()
    {
        var reportDate = new DateOnly(2025, 9, 30);
        var deadline = new DateOnly(2025, 11, 29);

        // 1. Already archived
        var statusArchived = AuditQualityEngine.GetArchivalStatus(true, reportDate, deadline, new DateOnly(2025, 10, 15));
        Assert.Contains("Archived & Locked", statusArchived);

        // 2. Report not yet issued
        var statusPending = AuditQualityEngine.GetArchivalStatus(false, null, null, new DateOnly(2025, 10, 15));
        Assert.Contains("Report not yet issued", statusPending);

        // 3. Overdue (> 60 days)
        var statusOverdue = AuditQualityEngine.GetArchivalStatus(false, reportDate, deadline, new DateOnly(2025, 12, 5));
        Assert.Contains("OVERDUE for Archival", statusOverdue);

        // 4. Due soon (<= 15 days)
        var statusDueSoon = AuditQualityEngine.GetArchivalStatus(false, reportDate, deadline, new DateOnly(2025, 11, 20));
        Assert.Contains("Due in 9 days", statusDueSoon);
    }

    [Fact]
    public void CanSignOffAudit_BlocksWhenOpenReviewNotesExist()
    {
        var (allowed, reason) = AuditQualityEngine.CanSignOffAudit(
            openReviewNotesCount: 3,
            eqrRequired: false,
            eqrStatus: EqrReviewStatus.NotApplicable
        );

        Assert.False(allowed);
        Assert.Contains("3 review note(s) remain open", reason);
    }

    [Fact]
    public void CanSignOffAudit_BlocksWhenEqrClearanceNotGranted()
    {
        var (allowed, reason) = AuditQualityEngine.CanSignOffAudit(
            openReviewNotesCount: 0,
            eqrRequired: true,
            eqrStatus: EqrReviewStatus.InProgress
        );

        Assert.False(allowed);
        Assert.Contains("(EQR) hot review clearance is mandatory", reason);
    }

    [Fact]
    public void CanSignOffAudit_AllowsWhenAllReviewNotesClearedAndEqrGranted()
    {
        var (allowed, reason) = AuditQualityEngine.CanSignOffAudit(
            openReviewNotesCount: 0,
            eqrRequired: true,
            eqrStatus: EqrReviewStatus.ClearanceGranted
        );

        Assert.True(allowed);
        Assert.Contains("Eligible for Partner Sign-off", reason);
    }

    [Fact]
    public void StandardPreIssuanceChecklist_ContainsMandatoryStandardsOnAuditing()
    {
        var checklist = AuditQualityEngine.GetStandardPreIssuanceChecklist();

        Assert.Equal(10, checklist.Count);
        Assert.Contains(checklist, c => c.Code == "SA_210");
        Assert.Contains(checklist, c => c.Code == "SA_315");
        Assert.Contains(checklist, c => c.Code == "SA_320");
        Assert.Contains(checklist, c => c.Code == "SA_240");
        Assert.Contains(checklist, c => c.Code == "SA_505");
        Assert.Contains(checklist, c => c.Code == "SA_501");
        Assert.Contains(checklist, c => c.Code == "SA_570");
        Assert.Contains(checklist, c => c.Code == "SA_580");
        Assert.Contains(checklist, c => c.Code == "CARO_2020");
        Assert.Contains(checklist, c => c.Code == "TB_AGREEMENT");
    }

    [Fact]
    public void MaterialityCalculation_ComputesSA320TolerancesCorrectly()
    {
        // 1% of Revenue 50,000,000 = 500,000 overall, 375,000 perf (75%), 25,000 trivial (5%)
        var (overall, perf, trivial) = AuditService.CalculateMateriality(BenchmarkType.Revenue, 50_000_000m);

        Assert.Equal(500_000m, overall);
        Assert.Equal(375_000m, perf);
        Assert.Equal(25_000m, trivial);

        // 5% of PBT 10,000,000 = 500,000 overall
        var (pbtOverall, pbtPerf, pbtTrivial) = AuditService.CalculateMateriality(BenchmarkType.ProfitBeforeTax, 10_000_000m);

        Assert.Equal(500_000m, pbtOverall);
        Assert.Equal(375_000m, pbtPerf);
        Assert.Equal(25_000m, pbtTrivial);
    }
}
