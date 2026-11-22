using CaDesk.Domain.Common;
using CaDesk.Domain.ForeignRemittance;
using CaDesk.Domain.Presumptive;
using CaDesk.Domain.TaxAudit;
using CaDesk.Domain.TransferPricing;
using Xunit;

namespace CaDesk.UnitTests;

/// <summary>
/// Income-tax Act, 2025 changeover: tax year 2026-27 onward cites the 2025 Act and the Income-tax Rules, 2026;
/// earlier years keep the 1961 Act. Expected numbers are from the CBDT mapping utilities on incometaxindia.gov.in.
/// </summary>
public class IncomeTaxLawUnitTests
{
    [Theory]
    [InlineData("2025-26", false)]
    [InlineData("2026-27", true)]
    [InlineData("2027-28", true)]
    public void Act_2025_governs_tax_year_2026_27_onward(string fy, bool act2025)
    {
        Assert.Equal(act2025, IncomeTaxLaw.IsAct2025(fy));
        Assert.Equal(act2025 ? "Income-tax Act, 2025" : "Income-tax Act, 1961", IncomeTaxLaw.ActName(fy));
    }

    [Theory]
    [InlineData("44AB", "section 63")]
    [InlineData("44AD", "section 58(2) (Table: Sl. No. 1)")]
    [InlineData("92E", "section 172")]
    [InlineData("234B", "section 424")]
    [InlineData("87A", "section 156")]
    [InlineData("40A(3)", "section 36(4)")]
    [InlineData("40(a)(ia)", "section 35(b)(i)")]
    [InlineData("43B(h)", "section 37(2)(g)")]
    public void Sections_follow_the_official_mapping(string old, string expected2026)
    {
        Assert.Equal(expected2026, IncomeTaxLaw.Sec(old, "2026-27"));
        Assert.Equal($"section {old}", IncomeTaxLaw.Sec(old, "2025-26"));
    }

    [Fact]
    public void An_unmapped_reference_is_cited_as_the_1961_provision_not_guessed()
    {
        Assert.False(IncomeTaxLaw.IsMapped("36(1)(va)"));
        Assert.Equal("section 36(1)(va) of the Income-tax Act, 1961", IncomeTaxLaw.Sec("36(1)(va)", "2026-27"));
        Assert.Equal("Sec 36(1)(va) [1961 Act]", IncomeTaxLaw.SecShort("36(1)(va)", "2026-27"));
    }

    [Theory]
    [InlineData("3CD", "26")]
    [InlineData("3CEB", "48")]
    [InlineData("15CA", "145")]
    [InlineData("15CB", "146")]
    [InlineData("26Q", "140")]
    public void Forms_follow_the_Rules_2026_mapping(string old, string expected)
    {
        Assert.Equal($"Form No. {expected}", IncomeTaxLaw.Form(old, "2026-27"));
        Assert.Equal($"Form No. {old}", IncomeTaxLaw.Form(old, "2025-26"));
    }

    [Fact]
    public void Year_label_uses_tax_year_under_the_2025_act()
    {
        Assert.Equal("Tax Year 2026-27", IncomeTaxLaw.YearLabel("2026-27"));
        Assert.Equal("FY 2025-26 (AY 2026-27)", IncomeTaxLaw.YearLabel("2025-26"));
    }

    [Fact]
    public void Tax_audit_report_for_tax_year_2026_27_is_form_26_under_section_63()
    {
        var a = new Form3CdAssessment
        {
            FinancialYear = "2026-27", AssessmentYear = "", AuditType = Form3CdAuditType.Form3CA,
            Section44AbClause = Form3CdSection44AbClause.Clause_a_BusinessTurnover, AssesseeName = "Test Co", Pan = "AABCT1234F",
        };
        var report = Form3CdEngine.GenerateTaxAuditReportDraft(a, []);

        Assert.Contains("FORM NO. 26", report);
        Assert.Contains("SECTION 63 OF THE INCOME-TAX ACT, 2025", report);
        Assert.Contains("Tax Year           : 2026-27", report);
        Assert.Contains("Sec 36(4) Cash payments", report);
        Assert.Contains("Sec 37(2)(g) MSME", report);
        Assert.Contains("map them to the clauses of Form No. 26", report);
        Assert.DoesNotContain("3CA", report.Split('\n')[0]);
        Assert.DoesNotContain("Assessment Year", report);

        // the same assessment for FY 2025-26 stays on the 1961 Act
        a.FinancialYear = "2025-26";
        var old = Form3CdEngine.GenerateTaxAuditReportDraft(a, []);
        Assert.StartsWith("FORM NO. 3CA - AUDIT REPORT UNDER SECTION 44AB OF THE INCOME-TAX ACT, 1961", old);
        Assert.Contains("Sec 40A(3) Cash payments", old);
    }

    [Fact]
    public void Applicability_rationale_cites_section_63_for_tax_year_2026_27()
    {
        var eval = Form3CdEngine.EvaluateApplicability("Company", true, 5_00_00_000m, 50_00_000m, 4_00_00_000m, 40_00_000m,
            isAuditedUnderOtherLaw: true, financialYear: "2026-27");

        Assert.True(eval.IsMandatory);
        Assert.Contains("section 63(1) (Table: Sl. No. 1(a)/(b))", eval.Rationale);
    }

    [Fact]
    public void Presumptive_lockout_reason_cites_section_58_for_tax_year_2026_27()
    {
        var reason = PresumptiveTaxEngine.Evaluate44ABAudit("44AD", 50_00_000m, 2_00_00_000m, true, 3_00_000m, 3_00_000m,
            isLockoutActive: true, financialYear: "2026-27").Reason;

        Assert.Contains("section 58(7)", reason);
        Assert.Contains("section 58(8)", reason);
    }

    [Fact]
    public void Remittance_certificate_for_tax_year_2026_27_is_form_146()
    {
        var a = new ForeignRemittanceAssessment { FinancialYear = "2026-27", RemitterName = "R", BeneficiaryName = "B", BeneficiaryCountry = "US" };
        var cert = ForeignRemittanceEngine.GenerateForm15CbCertificate(a);

        Assert.Contains("FORM NO. 146", cert);
        Assert.Contains("TAXABILITY IN INDIA UNDER THE INCOME-TAX ACT, 2025", cert);
        Assert.Contains("section 393 of the Income-tax Act, 2025", cert);
        Assert.DoesNotContain("15CB", cert);
    }

    [Fact]
    public void Transfer_pricing_report_for_tax_year_2026_27_is_form_48_under_section_172()
    {
        var study = new TransferPricingStudy { FinancialYear = "2026-27", AssessmentYear = "" };
        var report = TransferPricingEngine.GenerateForm3CEB(study, "Test Co", "AABCT1234F", "Mumbai");

        Assert.Contains("FORM NO. 48", report);
        Assert.Contains("UNDER SECTION 172", report);
        Assert.Contains("sections 162 to 172 of the Income-tax Act, 2025", report);
        Assert.DoesNotContain("3CEB", report.Split("Note:")[0]);
    }
}
