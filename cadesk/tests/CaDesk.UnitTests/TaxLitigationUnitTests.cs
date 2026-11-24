using CaDesk.Domain.Clients;
using CaDesk.Domain.TaxLitigation;
using Xunit;

namespace CaDesk.UnitTests;

public class TaxLitigationUnitTests
{
    [Fact]
    public void TaxAppealStatutoryCalculator_CalculateLimitation_CitAppeals_30Days()
    {
        var serviceDate = new DateOnly(2026, 9, 1);
        var asOfDate = new DateOnly(2026, 9, 15);

        var result = TaxAppealStatutoryCalculator.CalculateLimitation(AppealForum.CitAppeals, serviceDate, asOfDate);

        Assert.Equal(new DateOnly(2026, 10, 1), result.DeadlineDate);
        Assert.Equal(30, result.AllowedDays);
        Assert.Equal(16, result.DaysRemaining);
        Assert.False(result.IsExpired);
        Assert.False(result.DelayCondonationRequired);
        Assert.Equal(0, result.DaysOfDelay);
    }

    [Fact]
    public void TaxAppealStatutoryCalculator_CalculateLimitation_Itat_60Days_WithinLimit()
    {
        var serviceDate = new DateOnly(2026, 8, 1);
        var asOfDate = new DateOnly(2026, 9, 10);

        var result = TaxAppealStatutoryCalculator.CalculateLimitation(AppealForum.Itat, serviceDate, asOfDate);

        Assert.Equal(new DateOnly(2026, 9, 30), result.DeadlineDate);
        Assert.Equal(60, result.AllowedDays);
        Assert.Equal(20, result.DaysRemaining);
        Assert.False(result.IsExpired);
        Assert.False(result.DelayCondonationRequired);
    }

    [Fact]
    public void TaxAppealStatutoryCalculator_CalculateLimitation_Itat_60Days_DelayedRequiresCondonation()
    {
        // 60 days from Aug 1, 2026 is Sep 30, 2026. As of Oct 15, 2026, it is 15 days delayed.
        var serviceDate = new DateOnly(2026, 8, 1);
        var asOfDate = new DateOnly(2026, 10, 15);

        var result = TaxAppealStatutoryCalculator.CalculateLimitation(AppealForum.Itat, serviceDate, asOfDate);

        Assert.Equal(new DateOnly(2026, 9, 30), result.DeadlineDate);
        Assert.True(result.IsExpired);
        Assert.True(result.DelayCondonationRequired);
        Assert.Equal(15, result.DaysOfDelay);
        Assert.True(result.DaysRemaining < 0);
    }

    [Fact]
    public void TaxAppealStatutoryCalculator_CalculateLimitation_HighCourt_120Days()
    {
        var serviceDate = new DateOnly(2026, 5, 1);
        var asOfDate = new DateOnly(2026, 6, 1);

        var result = TaxAppealStatutoryCalculator.CalculateLimitation(AppealForum.HighCourt, serviceDate, asOfDate);

        Assert.Equal(120, result.AllowedDays);
        Assert.False(result.IsExpired);
        Assert.True(result.DaysRemaining > 0);
    }

    [Fact]
    public void TaxAppealStatutoryCalculator_CalculateStatutoryFilingFee_CitAppeals_Tiers()
    {
        // Tier 1: Income <= 1L -> ₹250
        decimal fee1 = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.CitAppeals, 80000m);
        Assert.Equal(250m, fee1);

        // Tier 2: Income 1L to 2L -> ₹500
        decimal fee2 = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.CitAppeals, 180000m);
        Assert.Equal(500m, fee2);

        // Tier 3: Income > 2L -> ₹1,000
        decimal fee3 = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.CitAppeals, 2500000m);
        Assert.Equal(1000m, fee3);

        // Other matter -> ₹250
        decimal feeOther = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.CitAppeals, 0m, isOtherMatter: true);
        Assert.Equal(250m, feeOther);
    }

    [Fact]
    public void TaxAppealStatutoryCalculator_CalculateStatutoryFilingFee_Itat_TiersAndCap()
    {
        // Tier 1: Income <= 1L -> ₹500
        decimal fee1 = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.Itat, 90000m);
        Assert.Equal(500m, fee1);

        // Tier 2: Income 1L to 2L -> ₹1,500
        decimal fee2 = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.Itat, 195000m);
        Assert.Equal(1500m, fee2);

        // Tier 3: Income > 2L (e.g. ₹5,00,000 -> 1% is ₹5,000)
        decimal fee3 = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.Itat, 500000m);
        Assert.Equal(5000m, fee3);

        // Tier 3 with Max Cap: Income ₹2,00,00,000 (1% is ₹2,00,000, capped at ₹10,000)
        decimal feeMaxCap = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.Itat, 20000000m);
        Assert.Equal(10000m, feeMaxCap);

        // Other matter -> ₹500
        decimal feeOther = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(AppealForum.Itat, 0m, isOtherMatter: true);
        Assert.Equal(500m, feeOther);
    }

    [Fact]
    public void ItatRule18PaperBookEngine_RepaginateDocuments_ConsecutivePages()
    {
        var docs = new List<PaperBookDocument>
        {
            new()
            {
                ItemNumber = 1,
                DescriptionOfDocument = "Assessment Order u/s 143(3)",
                PageNumberFrom = 1,
                PageNumberTo = 14 // 14 pages
            },
            new()
            {
                ItemNumber = 2,
                DescriptionOfDocument = "Written Submissions before AO",
                PageNumberFrom = 1,
                PageNumberTo = 10 // 10 pages
            },
            new()
            {
                ItemNumber = 3,
                DescriptionOfDocument = "Bank Statements & Third Party Confirmations",
                PageNumberFrom = 1,
                PageNumberTo = 25 // 25 pages
            }
        };

        ItatRule18PaperBookEngine.RepaginateDocuments(docs);

        // Doc 1: Pages 1 to 14
        Assert.Equal(1, docs[0].PageNumberFrom);
        Assert.Equal(14, docs[0].PageNumberTo);
        Assert.Equal(14, docs[0].TotalPages);

        // Doc 2: Pages 15 to 24 (10 pages)
        Assert.Equal(15, docs[1].PageNumberFrom);
        Assert.Equal(24, docs[1].PageNumberTo);
        Assert.Equal(10, docs[1].TotalPages);

        // Doc 3: Pages 25 to 49 (25 pages)
        Assert.Equal(25, docs[2].PageNumberFrom);
        Assert.Equal(49, docs[2].PageNumberTo);
        Assert.Equal(25, docs[2].TotalPages);
    }

    [Fact]
    public void ItatRule18PaperBookEngine_GenerateRule18Certificate_ContainsMandatoryDeclarations()
    {
        string cert = ItatRule18PaperBookEngine.GenerateRule18Certificate(
            appellantName: "Apex Global Infotech Ltd",
            pan: "AABCA1234F",
            appealNumber: "ITA No. 1045/DEL/2026",
            assessmentYear: "2024-25",
            totalPages: 125,
            partnerName: "CA Rajesh Sharma",
            membershipNo: "094582",
            firmName: "Sharma & Associates LLP"
        );

        Assert.Contains("CERTIFICATE UNDER RULE 18(6) OF THE INCOME TAX APPELLATE TRIBUNAL RULES, 1963", cert);
        Assert.Contains("Apex Global Infotech Ltd (PAN: AABCA1234F)", cert);
        Assert.Contains("ITA No. 1045/DEL/2026", cert);
        Assert.Contains("Page Nos. 1 to 125", cert);
        Assert.Contains("true, correct, and complete copies", cert);
        Assert.Contains("ICAI Membership No.: 094582", cert);
        Assert.Contains("Sharma & Associates LLP", cert);
    }

    [Fact]
    public void ItatRule18PaperBookEngine_CompileAppealDossierHtml_ContainsForm36AndIndex()
    {
        var appeal = new TaxAppeal
        {
            Id = Guid.NewGuid(),
            Forum = AppealForum.Itat,
            AppealNumber = "ITA No. 892/MUM/2026",
            AssessmentYear = "2024-25",
            FinancialYear = "2023-24",
            AssessingOfficerWardOrCircle = "Circle 5(1), Mumbai",
            ImpugnedOrderSection = "Sec 143(3) r.w.s. 144B",
            ImpugnedOrderNumber = "ITBA/AST/S/143(3)/2025-26/99812",
            ImpugnedOrderDate = new DateOnly(2026, 3, 25),
            DateOfServiceOfOrder = new DateOnly(2026, 3, 28),
            StatutoryFilingDeadline = new DateOnly(2026, 5, 27),
            AssessedTotalIncome = 4500000m,
            DisputedTaxDemand = 1250000m,
            FilingFeeAmount = 10000m,
            StatementOfFacts = "The Assessee is engaged in software exports and claimed exemption under Section 10AA.",
            ReliefSought = "It is prayed that the addition of ₹45,00,000 be deleted in full.",
            Client = new Client
            {
                Name = "Zenith Tech Systems Pvt Ltd",
                TaxIds = new Dictionary<string, string> { { "PAN", "AAACZ9876K" } }
            }
        };

        var grounds = new List<AppealGroundItem>
        {
            new()
            {
                GroundNumber = 1,
                Title = "Denial of Section 10AA Exemption",
                GroundText = "The learned AO erred in denying deduction under Section 10AA without appreciating the approval of the SEZ Development Commissioner.",
                StatutorySection = "Sec 10AA",
                DisputedAdditionAmount = 4500000m
            }
        };

        var docs = new List<PaperBookDocument>
        {
            new()
            {
                ItemNumber = 1,
                DocumentDate = new DateOnly(2026, 3, 25),
                DescriptionOfDocument = "Impugned Assessment Order",
                PageNumberFrom = 1,
                PageNumberTo = 18
            }
        };

        var citations = new List<AppealJudicialCitation>
        {
            new()
            {
                CaseTitle = "CIT v. Yokogawa India Ltd",
                Citation = "[2017] 391 ITR 274 (SC)",
                CourtOrTribunal = "Supreme Court",
                JudgmentYear = 2017,
                StatutorySection = "Sec 10AA",
                KeyRatioProposition = "Deduction under Section 10A/10AA is to be allowed from the profits of the eligible unit at the stage of computing gross total income."
            }
        };

        string html = ItatRule18PaperBookEngine.CompileAppealDossierHtml(appeal, grounds, docs, citations);

        Assert.Contains("FORM NO. 36", html);
        Assert.Contains("Zenith Tech Systems Pvt Ltd", html);
        Assert.Contains("AAACZ9876K", html);
        Assert.Contains("Denial of Section 10AA Exemption", html);
        Assert.Contains("PAPER BOOK INDEX UNDER ITAT RULE 18", html);
        Assert.Contains("CIT v. Yokogawa India Ltd", html);
        Assert.Contains("CERTIFICATE UNDER RULE 18(6)", html);
    }
}
