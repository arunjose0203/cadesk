using System;
using System.Collections.Generic;
using System.Linq;
using CaDesk.Domain.SecretarialAudit;
using Xunit;

namespace CaDesk.UnitTests;

public class SecretarialAuditUnitTests
{
    [Fact]
    public void EvaluateApplicability_ListedEntity_IsAlwaysMandatory()
    {
        // Listed company with 0 capital and 0 turnover
        var (isApplicable, reason, thresholds) = SecretarialAuditEngine.EvaluateApplicability(
            SecretarialAuditConstants.CompanyTypeListedPublic,
            paidUpCapital: 1_00_000m,
            turnover: 5_00_000m,
            outstandingBorrowings: 0m);

        Assert.True(isApplicable);
        Assert.Contains("Listed Entity", reason);
        Assert.Single(thresholds);
    }

    [Fact]
    public void EvaluateApplicability_UnlistedPublic_CapitalAbove50Cr_IsMandatory()
    {
        // Unlisted Public with 60 Cr paid-up capital
        var (isApplicable, reason, thresholds) = SecretarialAuditEngine.EvaluateApplicability(
            SecretarialAuditConstants.CompanyTypeUnlistedPublic,
            paidUpCapital: 60_00_00_000m,
            turnover: 100_00_00_000m,
            outstandingBorrowings: 20_00_00_000m);

        Assert.True(isApplicable);
        Assert.Contains("Paid-up share capital", reason);
        Assert.Single(thresholds);
    }

    [Fact]
    public void EvaluateApplicability_UnlistedPublic_TurnoverAbove250Cr_IsMandatory()
    {
        // Unlisted Public with 300 Cr turnover
        var (isApplicable, reason, thresholds) = SecretarialAuditEngine.EvaluateApplicability(
            SecretarialAuditConstants.CompanyTypeUnlistedPublic,
            paidUpCapital: 20_00_00_000m,
            turnover: 300_00_00_000m,
            outstandingBorrowings: 10_00_00_000m);

        Assert.True(isApplicable);
        Assert.Contains("Annual Turnover", reason);
        Assert.Single(thresholds);
    }

    [Fact]
    public void EvaluateApplicability_PrivateCompany_BorrowingsAbove100Cr_IsMandatory()
    {
        // Private company with 120 Cr bank borrowings (Rule 9(1)(c))
        var (isApplicable, reason, thresholds) = SecretarialAuditEngine.EvaluateApplicability(
            SecretarialAuditConstants.CompanyTypePrivate,
            paidUpCapital: 10_00_00_000m,
            turnover: 80_00_00_000m,
            outstandingBorrowings: 120_00_00_000m);

        Assert.True(isApplicable);
        Assert.Contains("Outstanding loans/borrowings", reason);
        Assert.Single(thresholds);
    }

    [Fact]
    public void EvaluateApplicability_PrivateCompany_BelowAllThresholds_IsExempt()
    {
        // Private company below 100 Cr borrowings
        var (isApplicable, reason, thresholds) = SecretarialAuditEngine.EvaluateApplicability(
            SecretarialAuditConstants.CompanyTypePrivate,
            paidUpCapital: 10_00_00_000m,
            turnover: 80_00_00_000m,
            outstandingBorrowings: 45_00_00_000m);

        Assert.False(isApplicable);
        Assert.Contains("Exempt", reason);
        Assert.Empty(thresholds);
    }

    [Fact]
    public void EvaluateMeetingCompliance_BoardMeeting_120DayGapBreached_FlagsNonCompliance()
    {
        var meeting = new BoardMeetingAuditRecord
        {
            MeetingType = SecretarialAuditConstants.MeetingBoard,
            PreviousMeetingDate = new DateTime(2024, 4, 1),
            MeetingDate = new DateTime(2024, 8, 15), // 136 days gap > 120 days
            NoticeDispatchDate = new DateTime(2024, 8, 5), // 10 days notice >= 7
            TotalMembersCount = 6,
            AttendedMembersCount = 4
        };

        SecretarialAuditEngine.EvaluateMeetingCompliance(meeting);

        Assert.Equal(136, meeting.GapFromPreviousMeetingDays);
        Assert.False(meeting.IsGapCompliant);
        Assert.True(meeting.IsNoticeCompliant);
        Assert.True(meeting.IsQuorumCompliant); // 4 >= 2
    }

    [Fact]
    public void EvaluateMeetingCompliance_NoticeAndQuorum_EvaluatedCorrectly()
    {
        var meeting = new BoardMeetingAuditRecord
        {
            MeetingType = SecretarialAuditConstants.MeetingBoard,
            PreviousMeetingDate = new DateTime(2024, 4, 1),
            MeetingDate = new DateTime(2024, 6, 15), // 75 days <= 120 days
            NoticeDispatchDate = new DateTime(2024, 6, 12), // 3 days notice < 7 days
            TotalMembersCount = 9,
            AttendedMembersCount = 2 // 1/3rd of 9 = 3; attended 2 < 3
        };

        SecretarialAuditEngine.EvaluateMeetingCompliance(meeting);

        Assert.True(meeting.IsGapCompliant);
        Assert.False(meeting.IsNoticeCompliant);
        Assert.Equal(3, meeting.QuorumRequiredCount);
        Assert.False(meeting.IsQuorumCompliant);
    }

    [Fact]
    public void EvaluateMcaFiling_DelayedSubmission_CalculatesDelayDays()
    {
        var filing = new McaFormFilingAudit
        {
            FormName = "AOC-4",
            Description = "Filing of Financial Statements with ROC",
            EventDate = new DateTime(2024, 9, 30), // AGM Date
            StatutoryDaysAllowed = 30, // 30 days -> Due 30 Oct 2024
            ActualFilingDate = new DateTime(2024, 11, 15), // Filed 16 days late
            Srn = "F12345678"
        };

        SecretarialAuditEngine.EvaluateMcaFiling(filing);

        Assert.Equal(new DateTime(2024, 10, 30), filing.StatutoryDueDate);
        Assert.Equal(16, filing.DelayDays);
        Assert.Equal(SecretarialAuditConstants.FilingDelayed, filing.FilingStatus);
    }

    [Fact]
    public void SeedBaselineChecklist_SeedsComprehensiveClauses()
    {
        var engId = Guid.NewGuid();
        var firmId = Guid.NewGuid();

        var unlistedChecklist = SecretarialAuditEngine.SeedBaselineChecklist(engId, firmId, isListed: false);
        var listedChecklist = SecretarialAuditEngine.SeedBaselineChecklist(engId, firmId, isListed: true);

        Assert.True(unlistedChecklist.Count >= 14);
        Assert.True(listedChecklist.Count >= unlistedChecklist.Count + 4);

        // Verify specific key clauses exist
        Assert.Contains(unlistedChecklist, c => c.ClauseCode == "SEC-149");
        Assert.Contains(unlistedChecklist, c => c.ClauseCode == "SEC-173");
        Assert.Contains(unlistedChecklist, c => c.ClauseCode == "SEC-177");
        Assert.Contains(unlistedChecklist, c => c.ClauseCode == "FEMA-FCGPR");
        Assert.Contains(unlistedChecklist, c => c.ClauseCode == "DEP-RULE-9A-9B");

        // Verify SEBI clauses exist for listed
        Assert.Contains(listedChecklist, c => c.ClauseCode == "SEBI-LODR-17");
        Assert.Contains(listedChecklist, c => c.ClauseCode == "SEBI-PIT-3-5");
    }

    [Fact]
    public void RecalculateSummary_CalculatesComplianceScoreAccurately()
    {
        var eng = new SecretarialAuditEngagement
        {
            ChecklistItems = new List<SecretarialChecklistItem>
            {
                new() { ComplianceStatus = SecretarialAuditConstants.ComplianceComplied },
                new() { ComplianceStatus = SecretarialAuditConstants.ComplianceComplied },
                new() { ComplianceStatus = SecretarialAuditConstants.ComplianceComplied },
                new() { ComplianceStatus = SecretarialAuditConstants.ComplianceNotComplied, IsQualification = true },
                new() { ComplianceStatus = SecretarialAuditConstants.ComplianceNotApplicable }
            },
            McaFilings = new List<McaFormFilingAudit>
            {
                new() { FilingStatus = SecretarialAuditConstants.FilingOnTime },
                new() { FilingStatus = SecretarialAuditConstants.FilingDelayed }
            },
            MeetingAudits = new List<BoardMeetingAuditRecord>
            {
                new() { IsGapCompliant = true, IsNoticeCompliant = true, IsQuorumCompliant = true },
                new() { IsGapCompliant = false, IsNoticeCompliant = true, IsQuorumCompliant = true }
            }
        };

        SecretarialAuditEngine.RecalculateSummary(eng);

        // 3 complied out of 4 applicable = 75%
        Assert.Equal(5, eng.TotalChecklistCount);
        Assert.Equal(3, eng.CompliedCount);
        Assert.Equal(1, eng.NotCompliedCount);
        Assert.Equal(1, eng.NotApplicableCount);
        Assert.Equal(1, eng.QualificationsCount);
        Assert.Equal(75.00m, eng.OverallComplianceScore);

        Assert.Equal(2, eng.TotalFilingsAudited);
        Assert.Equal(1, eng.FilingsDelayedCount);
        Assert.Equal(2, eng.TotalMeetingsAudited);
        Assert.Equal(1, eng.MeetingDeficienciesCount);
    }

    [Fact]
    public void GenerateFormMR3_FormatsOfficialReportWithQualifications()
    {
        var eng = new SecretarialAuditEngagement
        {
            EngagementReference = "SEC-2024-001",
            Cin = "U74999DL2018PTC123456",
            FinancialYear = "2024-25",
            CompanyType = SecretarialAuditConstants.CompanyTypeUnlistedPublic,
            AuditorName = "CS Rajiv Sharma",
            AuditorFirmName = "R. Sharma & Associates",
            MembershipType = "FCS",
            MembershipNumber = "8899",
            CertificateOfPracticeNumber = "12345",
            PeerReviewCertificateNumber = "PR-9988",
            Udin = "F008899B0001234567",
            ChecklistItems = new List<SecretarialChecklistItem>
            {
                new()
                {
                    StatuteCategory = SecretarialAuditConstants.StatuteCompaniesAct,
                    ClauseCode = "SEC-188",
                    SubjectArea = "Related Party Transactions",
                    StatutoryRequirement = "Prior approval of Audit Committee under Section 188",
                    ComplianceStatus = SecretarialAuditConstants.ComplianceNotComplied,
                    IsQualification = true,
                    DeviationRisk = SecretarialAuditConstants.RiskHigh,
                    AuditorRemarks = "Two transactions entered without omnibus Audit Committee approval."
                }
            }
        };

        SecretarialAuditEngine.RecalculateSummary(eng);
        var report = SecretarialAuditEngine.GenerateFormMR3(eng, "Acme Enterprises Limited", "Connaught Place, New Delhi");

        Assert.Contains("FORM NO. MR-3", report);
        Assert.Contains("SECRETARIAL AUDIT REPORT", report);
        Assert.Contains("Acme Enterprises Limited", report);
        Assert.Contains("U74999DL2018PTC123456", report);
        Assert.Contains("SEC-188", report);
        Assert.Contains("Two transactions entered without omnibus Audit Committee approval.", report);
        Assert.Contains("F008899B0001234567", report);
        Assert.Contains("CS Rajiv Sharma", report);
    }
}
