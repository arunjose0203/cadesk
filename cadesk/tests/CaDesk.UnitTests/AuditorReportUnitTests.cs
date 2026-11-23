using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.UnitTests;

public class AuditorReportUnitTests
{
    [Theory]
    [InlineData("25123456AAAA123456", true)]
    [InlineData("24999999ABCD123456", true)]
    [InlineData("25123456aaaa123456", true)]
    [InlineData("25123456AAAA12345", false)] // 17 chars
    [InlineData("25123456AAAA1234567", false)] // 19 chars
    [InlineData("A5123456AAAA123456", false)] // non-digit start
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AuditorReportEngine_IsValidUdin_ValidatesCorrect18CharacterStructure(string? udin, bool expected)
    {
        var isValid = AuditorReportEngine.IsValidUdin(udin);
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public void AuditorReportEngine_GenerateDefaultOpinionVerbiage_ProducesStandardUnmodifiedOpinion()
    {
        var (opinion, basis) = AuditorReportEngine.GenerateDefaultOpinionVerbiage(
            AuditorOpinionType.Unmodified,
            "Apex Global Technologies Ltd",
            "2024-25",
            new DateOnly(2025, 3, 31));

        Assert.Contains("Apex Global Technologies Ltd", opinion);
        Assert.Contains("give a true and fair view in conformity with the accounting principles generally accepted in India", opinion);
        Assert.Contains("Standards on Auditing ('SAs') specified under Section 143(10)", basis);
        Assert.Contains("ICAI's Code of Ethics", basis);
    }

    [Fact]
    public void AuditorReportEngine_GenerateDefaultOpinionVerbiage_ProducesQualifiedOpinionWithExceptForClause()
    {
        var (opinion, basis) = AuditorReportEngine.GenerateDefaultOpinionVerbiage(
            AuditorOpinionType.Qualified,
            "Apex Global Technologies Ltd",
            "2024-25",
            new DateOnly(2025, 3, 31),
            "non-provisioning of ₹45,00,000 doubtful debts");

        Assert.Contains("except for the effects of the matter described in the Basis for Qualified Opinion", opinion);
        Assert.Contains("non-provisioning of ₹45,00,000 doubtful debts", basis);
        Assert.Contains("sufficient and appropriate to provide a basis for our qualified audit opinion", basis);
    }

    [Fact]
    public void AuditorReportEngine_GenerateDefaultOpinionVerbiage_ProducesAdverseOpinionWhenFundamentalAssumptionsVitiated()
    {
        var (opinion, basis) = AuditorReportEngine.GenerateDefaultOpinionVerbiage(
            AuditorOpinionType.Adverse,
            "Apex Global Technologies Ltd",
            "2024-25",
            new DateOnly(2025, 3, 31));

        Assert.Contains("do not give a true and fair view", opinion);
        Assert.Contains("fundamental accounting assumption of going concern has been vitiated", basis);
        Assert.Contains("provide a basis for our adverse opinion", basis);
    }

    [Fact]
    public void AuditorReportEngine_GenerateDefaultOpinionVerbiage_ProducesDisclaimerWhenEvidenceUnavailable()
    {
        var (opinion, basis) = AuditorReportEngine.GenerateDefaultOpinionVerbiage(
            AuditorOpinionType.Disclaimer,
            "Apex Global Technologies Ltd",
            "2024-25",
            new DateOnly(2025, 3, 31));

        Assert.Contains("We do not express an opinion", opinion);
        Assert.Contains("have not been able to obtain sufficient appropriate audit evidence", opinion);
        Assert.Contains("unable to obtain access to complete accounting records", basis);
    }

    [Fact]
    public void AuditorReportEngine_GenerateFullAuditorReportHtml_RendersKamRule11gAndUdin()
    {
        var report = new IndependentAuditorReport
        {
            ReportTitle = "INDEPENDENT AUDITOR'S REPORT",
            OpinionType = AuditorOpinionType.Unmodified,
            ReportDate = new DateOnly(2025, 7, 20),
            PlaceOfSignature = "Mumbai",
            AuditorFirmName = "Sharma & Varma, Chartered Accountants",
            FirmRegistrationNumber = "109823W",
            PartnerName = "CA R. Sharma",
            PartnerMembershipNumber = "045678",
            Udin = "25045678AAAA998877",
            OpinionParagraph = "In our opinion, the accompanying financial statements give a true and fair view.",
            BasisForOpinionParagraph = "We conducted our audit in accordance with SAs under Section 143(10).",
            ResponsibilitiesOfManagementParagraph = "The Company's Board of Directors is responsible for the preparation of these financial statements.",
            AuditorResponsibilitiesParagraph = "Our objectives are to obtain reasonable assurance about whether the financial statements are free from material misstatement.",
            Rule11_g_AuditTrailStatus = Rule11AuditTrailStatus.CompliedOperatedWithoutTampering,
            Rule11_g_AuditTrailRemarks = "The Company has used an accounting software which has a feature of recording audit trail (edit log) facility and the same has operated throughout the year without being tampered.",
            IncludeKeyAuditMatters = true
        };

        report.KeyAuditMatters.Add(new KeyAuditMatterItem
        {
            Category = "Revenue",
            Title = "Revenue Recognition from Multi-Element Software Contracts",
            DescriptionOfMatter = "The Company recognises revenue under Ind AS 115 from complex software contracts.",
            WhyConsideredSignificant = "Due to the judgment required in allocating transaction prices.",
            HowAddressedInAudit = "Tested internal controls over sales contracts and sampled milestone acceptances.",
            KeyDocumentationRef = "WP-KAM-REV-01"
        });

        var html = AuditorReportEngine.GenerateFullAuditorReportHtml(report, "Apex Global Technologies Ltd", "U74999MH2018PTC305123", "2024-25");

        Assert.NotNull(html);
        Assert.Contains("INDEPENDENT AUDITOR'S REPORT", html);
        Assert.Contains("Apex Global Technologies Ltd", html);
        Assert.Contains("Revenue Recognition from Multi-Element Software Contracts", html);
        Assert.Contains("Accounting Software Audit Trail (Edit Log) Compliance", html);
        Assert.Contains("The Company has used an accounting software which has a feature of recording audit trail", html);
        Assert.Contains("25045678AAAA998877", html);
    }

    [Fact]
    public void AuditorReportEngine_GenerateNotesToAccountsHtml_RendersCorporatePoliciesAnd11Ratios()
    {
        var notes = new NotesToAccountsMaster
        {
            FinancialYear = "2024-25",
            Note1CorporateInformation = "Apex Global Technologies Ltd is a company domiciled in India.",
            Note2AccountingPolicies = "Financial statements are prepared under historical cost convention.",
            AdditionalRegulatoryDisclosures = "No proceedings have been initiated against the Company for holding Benami property."
        };

        var html = AuditorReportEngine.GenerateNotesToAccountsHtml(notes, "Apex Global Technologies Ltd", "2024-25");

        Assert.NotNull(html);
        Assert.Contains("NOTE 1: CORPORATE INFORMATION", html);
        Assert.Contains("Apex Global Technologies Ltd is a company domiciled in India", html);
        Assert.Contains("NOTE 2: SIGNIFICANT ACCOUNTING POLICIES", html);
        Assert.Contains("NOTE 23: MANDATORY SCHEDULE III FINANCIAL RATIOS DISCLOSURE", html);
        Assert.Contains("(a) Current Ratio", html);
        Assert.Contains("(b) Debt-Equity Ratio", html);
        Assert.Contains("-27.4%", html); // variance explanation
        Assert.Contains("Repayment of term loan", html);
        Assert.Contains("NOTE 24: ADDITIONAL REGULATORY DISCLOSURES (SCHEDULE III)", html);
    }
}
