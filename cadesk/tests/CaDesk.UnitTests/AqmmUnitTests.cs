using CaDesk.Domain.Aqmm;
using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.UnitTests;

public class AqmmUnitTests
{
    [Fact]
    public void StandardQuestions_TotalPoints_EqualsExactly600Points()
    {
        var totalPoints = AqmmEngine.StandardQuestions.Sum(q => q.MaxPoints);
        Assert.Equal(600, totalPoints);
        Assert.Equal(AqmmEngine.MaxTotal, totalPoints);
    }

    [Fact]
    public void StandardQuestions_SectionTotals_MatchStatutoryWeightages()
    {
        var secA = AqmmEngine.StandardQuestions.Where(q => q.Section == AqmmSection.PracticeManagement).Sum(q => q.MaxPoints);
        var secB = AqmmEngine.StandardQuestions.Where(q => q.Section == AqmmSection.HumanResources).Sum(q => q.MaxPoints);
        var secC = AqmmEngine.StandardQuestions.Where(q => q.Section == AqmmSection.AuditExecution).Sum(q => q.MaxPoints);

        Assert.Equal(190, secA);
        Assert.Equal(AqmmEngine.MaxSectionA, secA);

        Assert.Equal(110, secB);
        Assert.Equal(AqmmEngine.MaxSectionB, secB);

        Assert.Equal(300, secC);
        Assert.Equal(AqmmEngine.MaxSectionC, secC);
    }

    [Fact]
    public void StandardQuestions_HasExpected21Criteria()
    {
        Assert.Equal(21, AqmmEngine.StandardQuestions.Count);
        Assert.Equal(7, AqmmEngine.StandardQuestions.Count(q => q.Section == AqmmSection.PracticeManagement));
        Assert.Equal(5, AqmmEngine.StandardQuestions.Count(q => q.Section == AqmmSection.HumanResources));
        Assert.Equal(9, AqmmEngine.StandardQuestions.Count(q => q.Section == AqmmSection.AuditExecution));
    }

    [Theory]
    [InlineData(600, AqmmMaturityLevel.Level3)]
    [InlineData(510, AqmmMaturityLevel.Level3)]
    [InlineData(509, AqmmMaturityLevel.Level2)]
    [InlineData(450, AqmmMaturityLevel.Level2)]
    [InlineData(449, AqmmMaturityLevel.Level1)]
    [InlineData(300, AqmmMaturityLevel.Level1)]
    [InlineData(0, AqmmMaturityLevel.Level1)]
    public void CalculateMaturityLevel_AccuratelyClassifiesFirmLevel(int score, string expectedLevel)
    {
        var level = AqmmEngine.CalculateMaturityLevel(score);
        Assert.Equal(expectedLevel, level);
    }

    [Theory]
    [InlineData(420, 30, 90)]
    [InlineData(450, 0, 60)]
    [InlineData(480, 0, 30)]
    [InlineData(510, 0, 0)]
    [InlineData(550, 0, 0)]
    [InlineData(0, 450, 510)]
    public void CalculateGaps_ComputesCorrectPointsDeficitToLevel2And3(int score, int expectedL2Gap, int expectedL3Gap)
    {
        var (l2Gap, l3Gap) = AqmmEngine.CalculateGaps(score);
        Assert.Equal(expectedL2Gap, l2Gap);
        Assert.Equal(expectedL3Gap, l3Gap);
    }

    [Fact]
    public void StandardQuestions_TopCriterionMatchesMaxPointsForEveryQuestion()
    {
        foreach (var q in AqmmEngine.StandardQuestions)
        {
            Assert.NotEmpty(q.Criteria);
            var maxCriterion = q.Criteria.Max(c => c.Points);
            Assert.Equal(q.MaxPoints, maxCriterion);
        }
    }

    [Fact]
    public void StandardQuestions_AutoEvaluationKeys_CoverCoreAuditMoats()
    {
        var keys = AqmmEngine.StandardQuestions
            .Where(q => !string.IsNullOrEmpty(q.AutoEvaluationKey))
            .Select(q => q.AutoEvaluationKey)
            .ToList();

        Assert.Contains("KYC_COMPLETION", keys);
        Assert.Contains("IT_SECURITY", keys);
        Assert.Contains("SQC1_FRAMEWORK", keys);
        Assert.Contains("SA230_DOSSIER", keys);
        Assert.Contains("AUTOMATED_AUDIT_TOOLS", keys);
        Assert.Contains("SA505_CONFIRMATIONS", keys);
        Assert.Contains("SA700_REPORTING", keys);
    }

    [Theory]
    [InlineData("25123456AAAA123456", true)]
    [InlineData("25123456aaaa123456", true)]
    [InlineData("12345678901234567", false)] // 17 chars
    [InlineData("1234567890123456789", false)] // 19 chars
    [InlineData("", false)]
    public void AqmmCertification_Enforces18CharacterUdin(string udin, bool expected)
    {
        var isValid = AuditorReportEngine.IsValidUdin(udin);
        Assert.Equal(expected, isValid);
    }
}
