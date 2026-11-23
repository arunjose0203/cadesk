using CaDesk.Domain.TaxNotices;
using Xunit;

namespace CaDesk.UnitTests;

public class TaxCopilotUnitTests
{
    [Fact]
    public void NoticeOcrParser_ParsesSection148ANoticeCorrectly()
    {
        var sampleNotice = @"
            GOVERNMENT OF INDIA
            MINISTRY OF FINANCE
            INCOME TAX DEPARTMENT
            OFFICE OF THE NATIONAL FACELESS ASSESSMENT CENTRE, DELHI
            
            Notice under clause (b) of Section 148A of the Income-tax Act, 1961
            DIN: ITBA/AST/S/148A/2023-24/1058291029(1)
            Date of Issue: 15/03/2024
            
            To:
            PAN: ABCDE1234F
            M/S BHARAT INFRASTRUCTURES LTD
            
            Assessment Year: 2021-22
            Financial Year: 2020-21
            
            Sub: Show Cause Notice under Section 148A(b) - Proposed addition of Rs. 75,00,000/-
            
            Sir/Madam,
            Information has been flagged in Insight Portal that you have made high-value cash deposits of Rs. 75,00,000 during FY 2020-21.
            You are hereby required to show cause on or before 30/03/2024 why notice u/s 148 should not be issued.
        ";

        var result = NoticeOcrParser.Parse(sampleNotice);

        Assert.Equal(TaxAuthority.IncomeTax, result.Authority);
        Assert.Equal(NoticeCategory.Reassessment148_148A, result.Category);
        Assert.Equal("Section 148A(b)", result.Section);
        Assert.Equal("ITBA/AST/S/148A/2023-24/1058291029(1)", result.Din);
        Assert.Equal(DinStatus.Valid, result.DinStatus);
        Assert.Equal("ABCDE1234F", result.PanOrGstin);
        Assert.Equal("2021-22", result.AssessmentYear);
        Assert.Equal("2020-21", result.FinancialYear);
        Assert.Equal(7500000m, result.DemandAmount);
        Assert.Equal(new DateOnly(2024, 3, 15), result.IssueDate);
        Assert.Equal(new DateOnly(2024, 3, 30), result.DueDate);
        Assert.Contains(result.RecommendedPreliminaryObjections, o => o.Contains("149") || o.Contains("Sanction"));
        Assert.Contains(result.RecommendedGrounds, g => g.Contains("escapement") || g.Contains("disclosed"));
        Assert.True(result.ConfidenceScore >= 80);
    }

    [Fact]
    public void NoticeOcrParser_ParsesGstDrc01CNoticeCorrectly()
    {
        var sampleGstNotice = @"
            FORM GST DRC-01C
            [See Rule 88D]
            Reference No: GST/DRC01C/27AAAAA0000A1Z5/2023-24/098
            Date: 10/11/2023
            
            GSTIN: 27AAAAA0000A1Z5
            Legal Name: VANGUARD TRADING CORP
            Financial Year: 2022-23
            
            Intimation of difference in input tax credit available in Form GSTR-2B and that availed in Form GSTR-3B
            Part A (System Generated)
            It is observed that the ITC availed in Form GSTR-3B exceeds the ITC available in Form GSTR-2B by Rs. 8,45,000.
            You are directed to pay the excess ITC along with interest or furnish reasons within 7 days.
            Due Date: 17/11/2023
        ";

        var result = NoticeOcrParser.Parse(sampleGstNotice);

        Assert.Equal(TaxAuthority.Gst, result.Authority);
        Assert.Equal(NoticeCategory.GstDrc01A, result.Category);
        Assert.Equal("Form DRC-01C (Rule 88D)", result.Section);
        Assert.Equal("27AAAAA0000A1Z5", result.PanOrGstin);
        Assert.Equal(845000m, result.DemandAmount);
        Assert.Equal(new DateOnly(2023, 11, 10), result.IssueDate);
        Assert.Equal(new DateOnly(2023, 11, 17), result.DueDate);
        Assert.Contains(result.RecommendedPreliminaryObjections, o => o.Contains("Rule 88D"));
        Assert.Contains(result.RecommendedGrounds, g => g.Contains("Timing") || g.Contains("Suncraft"));
    }

    [Fact]
    public void NoticeOcrParser_ParsesSec143_1IntimationCorrectly()
    {
        var sampleIntimation = @"
            CENTRALIZED PROCESSING CENTRE, BENGALURU
            INTIMATION UNDER SECTION 143(1) OF THE INCOME TAX ACT, 1961
            DIN: 20230987654321
            PAN: AABCP9999K
            Assessment Year: 2023-24
            Date: 05/12/2023
            
            Adjustment made under Section 143(1)(a):
            TDS credit claimed in return differs from Form 26AS.
            Disallowance of Rs. 1,25,000 under Chapter VI-A.
            Demand payable: Rs. 1,48,500.
            Please submit rectification or payment within 30 days.
        ";

        var result = NoticeOcrParser.Parse(sampleIntimation);

        Assert.Equal(TaxAuthority.IncomeTax, result.Authority);
        Assert.Equal(NoticeCategory.Intimation143_1, result.Category);
        Assert.Equal("Section 143(1)", result.Section);
        Assert.Equal("20230987654321", result.Din);
        Assert.Equal("AABCP9999K", result.PanOrGstin);
        Assert.Equal("2023-24", result.AssessmentYear);
        Assert.Equal("2022-23", result.FinancialYear);
        Assert.Contains(result.RecommendedGrounds, g => g.Contains("199") || g.Contains("37BA"));
    }

    [Fact]
    public void NoticeOcrParser_ParsesSec270APenaltyNoticeCorrectly()
    {
        var samplePenalty = @"
            OFFICE OF THE ASSISTANT COMMISSIONER OF INCOME TAX, CIRCLE 1(1), MUMBAI
            Notice for penalty under Section 270A of the Income-tax Act, 1961
            DIN: ITBA/PNL/S/270A/2024-25/00112233
            PAN: ABCDE5555M
            Assessment Year: 2022-23
            Date: 12/01/2025
            
            Whereas in the course of assessment proceedings, it appears that you have under-reported your income.
            Proposed penalty of Rs. 15,00,000 is initiated.
            Show cause why penalty under section 270A should not be imposed.
        ";

        var result = NoticeOcrParser.Parse(samplePenalty);

        Assert.Equal(TaxAuthority.IncomeTax, result.Authority);
        Assert.Equal(NoticeCategory.PenaltyNotice270A, result.Category);
        Assert.Equal("Section 270A", result.Section);
        Assert.Equal(1500000m, result.DemandAmount);
        Assert.Contains(result.RecommendedGrounds, g => g.Contains("Price Waterhouse") || g.Contains("Bona Fide"));
    }

    [Fact]
    public void NoticeOcrParser_HandlesEmptyTextGracefully()
    {
        var result = NoticeOcrParser.Parse("");

        Assert.Equal(TaxAuthority.IncomeTax, result.Authority);
        Assert.Equal(NoticeCategory.GeneralNotice, result.Category);
        Assert.Equal(0, result.DemandAmount);
        Assert.True(result.ConfidenceScore <= 20);
    }

    [Fact]
    public void TaxDefenseDraft_JsonSerialization_WorksCorrectly()
    {
        var draft = new TaxDefenseDraft();

        var objections = new List<string> { "Objection 1: Limitation bar", "Objection 2: Sanction defect" };
        draft.SetPreliminaryObjections(objections);
        Assert.Equal(2, draft.GetPreliminaryObjections().Count);
        Assert.Equal("Objection 1: Limitation bar", draft.GetPreliminaryObjections()[0]);

        var facts = new List<FactualItem>
        {
            new() { ItemNo = 1, EventDate = "01/04/2023", Description = "Incorporation of Company", AnnexureRef = "Annexure A-1" }
        };
        draft.SetFactualMatrix(facts);
        Assert.Single(draft.GetFactualMatrix());
        Assert.Equal("Incorporation of Company", draft.GetFactualMatrix()[0].Description);

        var grounds = new List<MeritGroundItem>
        {
            new() { GroundNo = 1, Title = "Genuineness of Transactions", Argument = "Backed by banking channels", RelevantSection = "Sec 68" }
        };
        draft.SetMeritGrounds(grounds);
        Assert.Single(draft.GetMeritGrounds());
        Assert.Equal("Genuineness of Transactions", draft.GetMeritGrounds()[0].Title);

        var citations = new List<CitedPrecedentItem>
        {
            new() { CaseTitle = "CIT v. Lovely Exports", Citation = "[2008] 216 CTR 195 (SC)", CourtBench = "Supreme Court", RelevantHolding = "Share application money" }
        };
        draft.SetCitedPrecedents(citations);
        Assert.Single(draft.GetCitedPrecedents());
        Assert.Equal("CIT v. Lovely Exports", draft.GetCitedPrecedents()[0].CaseTitle);

        var annexures = new List<AnnexureItem>
        {
            new() { AnnexureNo = "A-1", Title = "Certificate of Incorporation", DocumentType = "Govt Certificate", PageCount = 2 }
        };
        draft.SetAnnexureIndex(annexures);
        Assert.Single(draft.GetAnnexureIndex());
        Assert.Equal("A-1", draft.GetAnnexureIndex()[0].AnnexureNo);
    }

    [Theory]
    [InlineData("24104523ABCD123456", true)]
    [InlineData("25012345PQRS987654", true)]
    [InlineData("12345", false)] // Too short
    [InlineData("24104523ABCD123456789", false)] // Too long
    [InlineData("24104523_ABCD12345", false)] // Invalid char
    public void PartnerUdinValidation_ValidatesCorrectly(string udin, bool expectedValid)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^[0-9]{2}[0-9]{6}[A-Z0-9]{4}[A-Z0-9]{6}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var isValid = regex.IsMatch(udin);
        Assert.Equal(expectedValid, isValid);
    }
}
