using System;
using System.Collections.Generic;
using CaDesk.Domain.InternalAudit;
using Xunit;

namespace CaDesk.UnitTests;

public class InternalAuditUnitTests
{
    [Fact]
    public void EvaluateResidualRisk_EffectiveDesignAndOperating_MitigatesToLow()
    {
        var control = new RcmControlItem
        {
            InherentRiskRating = "High",
            WalkthroughPerformed = true,
            DesignEffectiveness = "Effective",
            TestingMethod = "Inspection",
            SampleSize = 30,
            ExceptionsCount = 0,
            OperatingEffectiveness = "Effective"
        };

        InternalAuditEngine.EvaluateResidualRisk(control);

        Assert.Equal("Low", control.ResidualRiskRating);
        Assert.False(control.HasFinding);
    }

    [Fact]
    public void EvaluateResidualRisk_DeficientDesign_ResultsInHighResidualRiskAndFinding()
    {
        var control = new RcmControlItem
        {
            InherentRiskRating = "High",
            WalkthroughPerformed = true,
            DesignEffectiveness = "Deficient",
            TestingMethod = "Inquiry",
            SampleSize = 10,
            ExceptionsCount = 0,
            OperatingEffectiveness = "Effective"
        };

        InternalAuditEngine.EvaluateResidualRisk(control);

        Assert.Equal("High", control.ResidualRiskRating);
        Assert.True(control.HasFinding);
    }

    [Fact]
    public void EvaluateResidualRisk_ExceptionsFound_ResultsInHighResidualRiskAndFinding()
    {
        var control = new RcmControlItem
        {
            InherentRiskRating = "Medium",
            WalkthroughPerformed = true,
            DesignEffectiveness = "Effective",
            TestingMethod = "Reperformance",
            SampleSize = 25,
            ExceptionsCount = 3,
            OperatingEffectiveness = "Ineffective"
        };

        InternalAuditEngine.EvaluateResidualRisk(control);

        Assert.Equal("High", control.ResidualRiskRating);
        Assert.True(control.HasFinding);
    }

    [Fact]
    public void CalculateHealthScore_MixedTestedControls_CalculatesAccuratePercentage()
    {
        var controls = new List<RcmControlItem>
        {
            new() { OperatingEffectiveness = "Effective" },
            new() { OperatingEffectiveness = "Effective" },
            new() { OperatingEffectiveness = "Ineffective" },
            new() { OperatingEffectiveness = "NotTested" } // Not counted in tested denominator
        };

        var score = InternalAuditEngine.CalculateHealthScore(controls);

        // 2 effective out of 3 tested = 66.67%
        Assert.Equal(66.67m, score);
    }

    [Fact]
    public void CalculateHealthScore_AllEffective_Returns100()
    {
        var controls = new List<RcmControlItem>
        {
            new() { OperatingEffectiveness = "Effective" },
            new() { OperatingEffectiveness = "Effective" }
        };

        var score = InternalAuditEngine.CalculateHealthScore(controls);

        Assert.Equal(100.0m, score);
    }

    [Fact]
    public void CalculateHealthScore_EmptyOrUntested_Returns100()
    {
        var emptyControls = new List<RcmControlItem>();
        Assert.Equal(100.0m, InternalAuditEngine.CalculateHealthScore(emptyControls));

        var untestedControls = new List<RcmControlItem>
        {
            new() { OperatingEffectiveness = "NotTested" },
            new() { OperatingEffectiveness = "NotTested" }
        };
        Assert.Equal(100.0m, InternalAuditEngine.CalculateHealthScore(untestedControls));
    }

    [Fact]
    public void GenerateStandardRcmTemplate_GeneratesAllSixCoreCycles()
    {
        var firmId = Guid.NewGuid();
        var engId = Guid.NewGuid();

        var template = InternalAuditEngine.GenerateStandardRcmTemplate(firmId, engId);

        Assert.Equal(6, template.Count);
        Assert.Contains(template, c => c.ProcessCycle == "P2P" && c.ControlCode == "P2P-CTL-01");
        Assert.Contains(template, c => c.ProcessCycle == "O2C" && c.ControlCode == "O2C-CTL-01");
        Assert.Contains(template, c => c.ProcessCycle == "H2R" && c.ControlCode == "H2R-CTL-01");
        Assert.Contains(template, c => c.ProcessCycle == "R2R" && c.ControlCode == "R2R-CTL-01");
        Assert.Contains(template, c => c.ProcessCycle == "Treasury" && c.ControlCode == "TR-CTL-01");
        Assert.Contains(template, c => c.ProcessCycle == "ITGC" && c.ControlCode == "ITGC-CTL-01");
    }

    [Fact]
    public void GenerateExecutiveReport_IncludesStatutoryReferencesAndClientDetails()
    {
        var eng = new InternalAuditEngagement
        {
            Id = Guid.NewGuid(),
            FinancialYear = "2024-25",
            AuditPeriod = "Q3 FY 2024-25",
            Title = "Risk Based Internal Audit & IFC Assessment",
            EngagementLeadName = "CA Ramesh Sharma, FCA, DISA",
            HealthScorePercentage = 85.5m,
            Controls = new List<RcmControlItem>
            {
                new() { ControlCode = "P2P-001", ProcessCycle = "P2P", OperatingEffectiveness = "Effective", ResidualRiskRating = "Low" },
                new() { ControlCode = "O2C-001", ProcessCycle = "O2C", OperatingEffectiveness = "Ineffective", ResidualRiskRating = "High" }
            },
            Findings = new List<InternalAuditFinding>
            {
                new()
                {
                    FindingReference = "IA-FIND-001",
                    Title = "Missing Credit Limit Check on Manual Billing",
                    ProcessCycle = "O2C",
                    Severity = "High",
                    IfcClassification = "SignificantDeficiency",
                    EstimatedRiskAmount = 450000m,
                    AuditorRecommendation = "Enforce automated system block for overdue balances",
                    ImplementationStatus = "Open"
                }
            }
        };

        var report = InternalAuditEngine.GenerateExecutiveReport(eng, "Tata BlueScope Steel Limited");

        Assert.Contains("Tata BlueScope Steel Limited", report);
        Assert.Contains("Section 138", report);
        Assert.Contains("143(3)(i)", report);
        Assert.Contains("Standards on Internal Audit (SIA)", report);
        Assert.Contains("85.50%", report);
        Assert.Contains("IA-FIND-001", report);
        Assert.Contains("SignificantDeficiency", report);
    }
}
