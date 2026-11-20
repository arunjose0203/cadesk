using System;
using System.Collections.Generic;
using CaDesk.Domain.Caro;
using Xunit;

namespace CaDesk.UnitTests;

public class CaroUnitTests
{
    [Fact]
    public void EvaluateApplicability_PrivateCompany_ExemptWhenAllThreeConditionsSatisfied()
    {
        // Capital ₹80L, Borrowings ₹40L, Revenue ₹6 Cr (all under thresholds)
        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            companyType: CaroCompanyType.Private,
            isHoldingOrSubsidiaryOfPublic: false,
            paidUpCapitalAndReserves: 8_000_000m,
            totalBorrowings: 4_000_000m,
            totalRevenue: 60_000_000m
        );

        Assert.False(isApplicable);
        Assert.NotNull(reason);
        Assert.Contains("Paragraph 1(2)(iii)", reason);
        Assert.Contains("all 3 threshold exemptions", reason);
    }

    [Fact]
    public void EvaluateApplicability_PrivateCompany_ApplicableWhenCapitalBreached()
    {
        // Capital ₹1.2 Cr (> ₹1 Cr), Borrowings ₹10L, Revenue ₹2 Cr
        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            companyType: CaroCompanyType.Private,
            isHoldingOrSubsidiaryOfPublic: false,
            paidUpCapitalAndReserves: 12_000_000m,
            totalBorrowings: 1_000_000m,
            totalRevenue: 20_000_000m
        );

        Assert.True(isApplicable);
        Assert.NotNull(reason);
        Assert.Contains("Paid-up capital & reserves", reason);
        Assert.Contains("exceeds ₹1 Crore", reason);
    }

    [Fact]
    public void EvaluateApplicability_PrivateCompany_ApplicableWhenBorrowingsBreached()
    {
        // Capital ₹50L, Borrowings ₹1.5 Cr (> ₹1 Cr), Revenue ₹4 Cr
        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            companyType: CaroCompanyType.Private,
            isHoldingOrSubsidiaryOfPublic: false,
            paidUpCapitalAndReserves: 5_000_000m,
            totalBorrowings: 15_000_000m,
            totalRevenue: 40_000_000m
        );

        Assert.True(isApplicable);
        Assert.NotNull(reason);
        Assert.Contains("Total borrowings", reason);
        Assert.Contains("exceeds ₹1 Crore", reason);
    }

    [Fact]
    public void EvaluateApplicability_PrivateCompany_ApplicableWhenRevenueBreached()
    {
        // Capital ₹50L, Borrowings ₹20L, Revenue ₹12 Cr (> ₹10 Cr)
        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            companyType: CaroCompanyType.Private,
            isHoldingOrSubsidiaryOfPublic: false,
            paidUpCapitalAndReserves: 5_000_000m,
            totalBorrowings: 2_000_000m,
            totalRevenue: 120_000_000m
        );

        Assert.True(isApplicable);
        Assert.NotNull(reason);
        Assert.Contains("Total revenue", reason);
        Assert.Contains("exceeds ₹10 Crores", reason);
    }

    [Fact]
    public void EvaluateApplicability_PrivateCompany_ApplicableWhenHoldingOrSubsidiaryOfPublic()
    {
        // Below all 3 thresholds, but is subsidiary of a public company
        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            companyType: CaroCompanyType.Private,
            isHoldingOrSubsidiaryOfPublic: true,
            paidUpCapitalAndReserves: 1_000_000m,
            totalBorrowings: 500_000m,
            totalRevenue: 5_000_000m
        );

        Assert.True(isApplicable);
        Assert.NotNull(reason);
        Assert.Contains("deemed public company under Section 2(71)", reason);
    }

    [Theory]
    [InlineData(CaroCompanyType.Section8, "Section 8")]
    [InlineData(CaroCompanyType.OnePersonCompany, "One Person Company")]
    [InlineData(CaroCompanyType.SmallCompany, "Small Company")]
    [InlineData(CaroCompanyType.Banking, "Banking company")]
    [InlineData(CaroCompanyType.Insurance, "Insurance company")]
    public void EvaluateApplicability_ExemptCategories_CorrectlyFlagged(string companyType, string expectedCitation)
    {
        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            companyType: companyType,
            isHoldingOrSubsidiaryOfPublic: false,
            paidUpCapitalAndReserves: 50_000_000m, // High figures should not matter for exempt types
            totalBorrowings: 50_000_000m,
            totalRevenue: 500_000_000m
        );

        Assert.False(isApplicable);
        Assert.NotNull(reason);
        Assert.Contains(expectedCitation, reason);
    }

    [Fact]
    public void GetStandard21ClauseTemplates_ContainsAllMandatoryClauses()
    {
        var templates = CaroEngine.GetStandard21ClauseTemplates();
        Assert.True(templates.Count >= 21);

        Assert.Contains(templates, t => t.ClauseNumber == "i(a)(A)");
        Assert.Contains(templates, t => t.ClauseNumber == "ii(b)");
        Assert.Contains(templates, t => t.ClauseNumber == "vii(a)");
        Assert.Contains(templates, t => t.ClauseNumber == "ix(b)");
        Assert.Contains(templates, t => t.ClauseNumber == "xix");
        Assert.Contains(templates, t => t.ClauseNumber == "xxi");
    }

    [Fact]
    public void GenerateAnnexureDraft_FormatsQualificationsAndAdverseRemarks()
    {
        var assessment = new CaroAssessment
        {
            Id = Guid.NewGuid(),
            FinancialYear = "2024-25",
            IsCaroApplicable = true
        };

        var clauses = new List<CaroClauseResponse>
        {
            new()
            {
                ClauseNumber = "i(a)(A)",
                ClauseTitle = "Proper Records of PPE",
                Status = CaroClauseStatus.Compliant,
                AuditorRemark = "Company maintained proper records of PPE."
            },
            new()
            {
                ClauseNumber = "ii(b)",
                ClauseTitle = "Quarterly Bank Returns Agreement",
                Status = CaroClauseStatus.Qualified,
                AuditorRemark = "Statements filed with State Bank of India showed variance of ₹25 Lakhs against books.",
                HasDiscrepancy = true,
                DiscrepancyAmount = 2_500_000m
            },
            new()
            {
                ClauseNumber = "ix(b)",
                ClauseTitle = "Willful Defaulter Declaration",
                Status = CaroClauseStatus.Adverse,
                AuditorRemark = "The company has been declared a willful defaulter by Union Bank of India."
            }
        };

        var draft = CaroEngine.GenerateAnnexureDraft(assessment, clauses);

        Assert.Contains("ANNEXURE 'A' TO THE INDEPENDENT AUDITOR'S REPORT", draft);
        Assert.Contains("QUALIFICATION:", draft);
        Assert.Contains("variance of ₹25 Lakhs", draft);
        Assert.Contains($"Discrepancy amount quantified: ₹{2_500_000m:N2}", draft);
        Assert.Contains("ADVERSE REMARK:", draft);
        Assert.Contains("declared a willful defaulter", draft);
    }
}
