using CaDesk.Application.Engagements;
using CaDesk.Domain.Engagements;
using Xunit;

namespace CaDesk.UnitTests;

public class EngagementUnitTests
{
    [Fact]
    public void CalculateNextKycReviewDueDate_HighRisk_Returns_1_Year()
    {
        var reviewDate = new DateOnly(2024, 6, 1);
        var nextDue = EngagementTemplateGenerator.CalculateNextKycReviewDueDate(KycRiskRating.High, reviewDate);

        Assert.Equal(new DateOnly(2025, 6, 1), nextDue);
    }

    [Fact]
    public void CalculateNextKycReviewDueDate_Pep_Returns_1_Year()
    {
        var reviewDate = new DateOnly(2024, 6, 1);
        var nextDue = EngagementTemplateGenerator.CalculateNextKycReviewDueDate(KycRiskRating.PoliticallyExposedPerson, reviewDate);

        Assert.Equal(new DateOnly(2025, 6, 1), nextDue);
    }

    [Fact]
    public void CalculateNextKycReviewDueDate_MediumRisk_Returns_2_Years()
    {
        var reviewDate = new DateOnly(2024, 6, 1);
        var nextDue = EngagementTemplateGenerator.CalculateNextKycReviewDueDate(KycRiskRating.Medium, reviewDate);

        Assert.Equal(new DateOnly(2026, 6, 1), nextDue);
    }

    [Fact]
    public void CalculateNextKycReviewDueDate_LowRisk_Returns_3_Years()
    {
        var reviewDate = new DateOnly(2024, 6, 1);
        var nextDue = EngagementTemplateGenerator.CalculateNextKycReviewDueDate(KycRiskRating.Low, reviewDate);

        Assert.Equal(new DateOnly(2027, 6, 1), nextDue);
    }

    [Theory]
    [InlineData(EngagementType.StatutoryAudit, "Section 139", "SA 210")]
    [InlineData(EngagementType.TaxAudit, "Section 44AB", "Form 3CD")]
    [InlineData(EngagementType.GstAudit, "GSTR-9C", "Section 44")]
    [InlineData(EngagementType.DirectTaxFiling, "ITR", "Chapter VI-A")]
    [InlineData(EngagementType.InternalAudit, "internal controls", "ERP")]
    public void GenerateTemplate_Generates_Standard_Clauses(string engagementType, string expectedTerm1, string expectedTerm2)
    {
        var req = new GenerateTemplateRequest(
            EngagementType: engagementType,
            ClientName: "Reliance Retail Limited",
            FinancialYear: "2024-25",
            FeeStructure: EngagementFeeStructure.FixedFee,
            AgreedFeeAmount: 250_000m
        );

        var template = EngagementTemplateGenerator.GenerateTemplate(req);

        Assert.Contains(expectedTerm1, template.FullLetterContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedTerm2, template.FullLetterContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reliance Retail Limited", template.FullLetterContent);
        Assert.Contains("250,000.00", template.FullLetterContent);
    }
}
