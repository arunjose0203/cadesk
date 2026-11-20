using System;
using System.Collections.Generic;
using CaDesk.Domain.AuditCommittee;
using CaDesk.Domain.Clients;
using Xunit;

namespace CaDesk.UnitTests;

public class AuditCommitteeUnitTests
{
    [Fact]
    public void GenerateSa265DeficiencyLetter_FormatsSignificantAndOtherDeficienciesCorrectly()
    {
        var engagement = new AuditCommitteeEngagement
        {
            FinancialYear = "2024-25",
            Title = "Audit Committee Pre-Issuance Governance Review",
            CommitteeChairName = "Mr. R. V. Ramanathan (Independent Director)",
            LeadPartnerName = "CA Vikram Malhotra",
            Client = new Client { Name = "Bharat Heavy Infrastructure Ltd" }
        };

        var deficiencies = new List<InternalControlDeficiency>
        {
            new()
            {
                DeficiencyCode = "ICD-01",
                Title = "Lack of formal three-way matching for high-value procurement",
                ProcessArea = "Procurement & Payables",
                DeficiencyType = DeficiencyType.OperatingDeficiency,
                Severity = DeficiencySeverity.SignificantDeficiency,
                Description = "Purchase orders above ₹50 Lakhs processed without automated goods receipt note (GRN) verification in ERP.",
                PotentialFinancialEffect = "Risk of duplicate vendor payments or unrecorded liabilities.",
                EstimatedRiskAmount = 12_500_000m, // ₹1.25 Cr
                AuditorRecommendation = "Enforce automated 3-way match hard stop in SAP prior to payment voucher creation.",
                ManagementResponse = "IT team deployed custom validation workflow effective April 1, 2025.",
                RemediationTargetDate = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                DeficiencyCode = "ICD-02",
                Title = "Periodic review of privileged ERP user access logs not documented",
                ProcessArea = "IT General Controls (ITGC)",
                DeficiencyType = DeficiencyType.DesignDeficiency,
                Severity = DeficiencySeverity.OtherDeficiency,
                AuditorRecommendation = "Establish monthly supervisory review of SAP BASIS super-user activities.",
                ManagementResponse = "CISO mandated quarterly review sign-offs."
            }
        };

        var letter = AuditCommitteeEngine.GenerateSa265DeficiencyLetter(engagement, deficiencies);

        Assert.Contains("COMMUNICATION OF SIGNIFICANT DEFICIENCIES IN INTERNAL CONTROL", letter);
        Assert.Contains("Standard on Auditing 265 & Companies Act, 2013 Section 177", letter);
        Assert.Contains("not for the purpose of expressing an opinion on the effectiveness of the Company's internal financial controls", letter);
        Assert.Contains("SIGNIFICANT DEFICIENCIES IN INTERNAL CONTROL (SA 265)", letter);
        Assert.Contains("[ICD-01] Lack of formal three-way matching", letter);
        Assert.Contains("Procurement & Payables", letter);
        Assert.Contains("Deficiency in Operating Effectiveness", letter);
        Assert.Contains("Enforce automated 3-way match hard stop", letter);
        Assert.Contains("OTHER DEFICIENCIES COMMUNICATED FOR OPERATIONAL IMPROVEMENT", letter);
        Assert.Contains("[ICD-02] Periodic review of privileged ERP user access", letter);
        Assert.Contains("CA Vikram Malhotra", letter);
    }

    [Fact]
    public void GenerateSa260GovernancePack_ContainsIndependenceAffirmationsAndMisstatements()
    {
        var engagement = new AuditCommitteeEngagement
        {
            FinancialYear = "2024-25",
            Title = "Statutory Audit FY 2024-25 Audit Committee Pack",
            MeetingType = CommitteeMeetingType.PreIssuance,
            CommitteeChairName = "Mrs. Sunita Kulkarni",
            LeadPartnerName = "CA Anita Sharma",
            Client = new Client { Name = "Zeta Precision Technologies Ltd" },
            Status = AuditCommitteeEngagementStatus.Presented
        };

        var deficiencies = new List<InternalControlDeficiency>
        {
            new()
            {
                DeficiencyCode = "ICD-01",
                Title = "Revenue cutoff discrepancy at period-end",
                Severity = DeficiencySeverity.SignificantDeficiency
            }
        };

        var matters = new List<SignificantAuditMatter>
        {
            new()
            {
                MatterCode = "SAM-01",
                Subject = "Valuation of Intangible Assets under Development (Ind AS 38)",
                Category = AuditMatterCategory.KeyAuditMatter,
                Details = "Capitalization of ₹18 Crores in EV battery management software.",
                AuditorAssessment = "Technical feasibility report verified with independent domain expert."
            }
        };

        var misstatements = new List<UncorrectedMisstatement>
        {
            new()
            {
                AccountDescription = "Understated provision for doubtful debts",
                MisstatementAmount = 1_500_000m, // ₹15 Lakhs
                Nature = MisstatementNature.Judgmental,
                IsMaterial = false,
                ManagementJustificationForNonAdjustment = "Disputed customer accounts undergoing legal settlement."
            }
        };

        var pack = AuditCommitteeEngine.GenerateSa260GovernancePack(engagement, deficiencies, matters, misstatements);

        Assert.Contains("AUDIT COMMITTEE GOVERNANCE PACK & STATUTORY PRESENTATION", pack);
        Assert.Contains("SA 260 (Revised) & Companies Act, 2013 Section 177", pack);
        Assert.Contains("STATEMENT OF AUDITOR INDEPENDENCE", pack);
        Assert.Contains("Section 144 of the Companies Act, 2013", pack);
        Assert.Contains("[SAM-01] Valuation of Intangible Assets under Development", pack);
        Assert.Contains("KeyAuditMatter", pack);
        Assert.Contains("SCHEDULE OF UNCORRECTED MISSTATEMENTS", pack);
        Assert.Contains("Understated provision for doubtful debts", pack);
        Assert.Contains("Judgmental", pack);
        Assert.Contains("INTERNAL CONTROL DEFICIENCY HIGHLIGHTS (SA 265)", pack);
        Assert.Contains("Significant Deficiencies : 1", pack);
        Assert.Contains("UNMODIFIED audit opinion", pack);
        Assert.Contains("CA Anita Sharma", pack);
    }
}
