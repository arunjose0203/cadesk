using System;
using System.Collections.Generic;
using System.Globalization;
using CaDesk.Domain.TaxAudit;
using Xunit;

namespace CaDesk.UnitTests;

public class Form3CdUnitTests
{
    private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");

    [Fact]
    public void EvaluateApplicability_Business_Under1Cr_NotMandatory()
    {
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: "Company",
            isBusiness: true,
            turnoverOrGrossReceipts: 80_00_000m,
            cashReceipts: 20_00_000m,
            totalPayments: 60_00_000m,
            cashPayments: 15_00_000m,
            isAuditedUnderOtherLaw: true
        );

        Assert.False(eval.IsMandatory);
        Assert.Equal(Form3CdAuditType.Form3CA, eval.RecommendedAuditType);
        Assert.Equal(1_00_00_000m, eval.StatutoryTurnoverThreshold);
        Assert.Contains("within the standard ₹1 Crore threshold", eval.Rationale);
    }

    [Fact]
    public void EvaluateApplicability_Business_Between1CrAnd10Cr_HighCash_MandatoryUnder44ABa()
    {
        // Turnover ₹4 Cr, Cash receipts ₹50 L (12.5% > 5%) -> standard ₹1 Cr applies -> Mandatory
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: "Company",
            isBusiness: true,
            turnoverOrGrossReceipts: 4_00_00_000m,
            cashReceipts: 50_00_000m,
            totalPayments: 3_00_00_000m,
            cashPayments: 5_00_000m,
            isAuditedUnderOtherLaw: true
        );

        Assert.True(eval.IsMandatory);
        Assert.Equal(Form3CdAuditType.Form3CA, eval.RecommendedAuditType);
        Assert.Equal(Form3CdSection44AbClause.Clause_a_BusinessTurnover, eval.ApplicableClause);
        Assert.Equal(1_00_00_000m, eval.StatutoryTurnoverThreshold);
        Assert.Contains("standard threshold of ₹1 Crore", eval.Rationale);
    }

    [Fact]
    public void EvaluateApplicability_Business_Between1CrAnd10Cr_LowCash_ExemptUnderProviso()
    {
        // Turnover ₹6 Cr, Cash receipts ₹10 L (1.67% <= 5%), Payments ₹5 Cr, Cash payments ₹10 L (2% <= 5%) -> ₹10 Cr applies -> Exempt
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: "Partnership",
            isBusiness: true,
            turnoverOrGrossReceipts: 6_00_00_000m,
            cashReceipts: 10_00_000m,
            totalPayments: 5_00_00_000m,
            cashPayments: 10_00_000m,
            isAuditedUnderOtherLaw: false
        );

        Assert.False(eval.IsMandatory);
        Assert.Equal(Form3CdAuditType.Form3CB, eval.RecommendedAuditType);
        Assert.Equal(10_00_00_000m, eval.StatutoryTurnoverThreshold);
        Assert.Contains("within the ₹10 Crore threshold", eval.Rationale);
    }

    [Fact]
    public void EvaluateApplicability_Business_Exceeding10Cr_EvenWithLowCash_Mandatory()
    {
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: "Company",
            isBusiness: true,
            turnoverOrGrossReceipts: 12_50_00_000m,
            cashReceipts: 0m,
            totalPayments: 10_00_00_000m,
            cashPayments: 0m,
            isAuditedUnderOtherLaw: true
        );

        Assert.True(eval.IsMandatory);
        Assert.Equal(10_00_00_000m, eval.StatutoryTurnoverThreshold);
        Assert.Contains("enhanced threshold of ₹10 Crores", eval.Rationale);
    }

    [Fact]
    public void EvaluateApplicability_Profession_Under50Lakhs_NotMandatory()
    {
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: "Individual",
            isBusiness: false,
            turnoverOrGrossReceipts: 42_00_000m,
            cashReceipts: 5_00_000m,
            totalPayments: 20_00_000m,
            cashPayments: 2_00_000m,
            isAuditedUnderOtherLaw: false
        );

        Assert.False(eval.IsMandatory);
        Assert.Equal(Form3CdAuditType.Form3CB, eval.RecommendedAuditType);
        Assert.Equal(50_00_000m, eval.StatutoryTurnoverThreshold);
    }

    [Fact]
    public void EvaluateApplicability_Profession_Between50LAnd75L_LowCash_MandatoryUnlessOffered44Ada()
    {
        // 44AB(b) / s.63(1) Sl. No. 1(c): audit above ₹50 lakh. ₹75 lakh is the 44ADA presumptive ceiling (cash <= 5%),
        // which spares only a professional who offers at least the deemed 50% under 44ADA.
        Form3CdApplicabilityEvaluation Eval(bool opts44Ada) => Form3CdEngine.EvaluateApplicability(
            entityType: "Individual",
            isBusiness: false,
            turnoverOrGrossReceipts: 65_00_000m,
            cashReceipts: 2_00_000m,
            totalPayments: 30_00_000m,
            cashPayments: 1_00_000m,
            isAuditedUnderOtherLaw: false,
            optsPresumptive44Ada: opts44Ada,
            financialYear: "2025-26"
        );

        var regular = Eval(opts44Ada: false);
        Assert.True(regular.IsMandatory);
        Assert.Equal(50_00_000m, regular.StatutoryTurnoverThreshold);
        Assert.Contains("section 44ADA", regular.Rationale);

        var presumptive = Eval(opts44Ada: true);
        Assert.False(presumptive.IsMandatory);
        Assert.Equal(75_00_000m, presumptive.StatutoryTurnoverThreshold);
    }

    [Fact]
    public void EvaluateApplicability_Profession_Between50LAnd75L_HighCash_Mandatory()
    {
        // Professional receipts ₹65 Lakhs, Cash receipts ₹10 Lakhs (15.38% > 5%) -> standard ₹50 L applies -> Mandatory
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: "LLP",
            isBusiness: false,
            turnoverOrGrossReceipts: 65_00_000m,
            cashReceipts: 10_00_000m,
            totalPayments: 30_00_000m,
            cashPayments: 1_00_000m,
            isAuditedUnderOtherLaw: false
        );

        Assert.True(eval.IsMandatory);
        Assert.Equal(50_00_000m, eval.StatutoryTurnoverThreshold);
        Assert.Contains("exceed ₹50 lakh", eval.Rationale);
        Assert.DoesNotContain("44ADA", eval.Rationale); // cash above 5%: 44ADA's ₹75 lakh ceiling is not available
    }

    [Fact]
    public void EvaluateApplicability_Presumptive44AdLockout_MandatoryUnder44ABe()
    {
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: "Individual",
            isBusiness: true,
            turnoverOrGrossReceipts: 85_00_000m,
            cashReceipts: 5_00_000m,
            totalPayments: 50_00_000m,
            cashPayments: 2_00_000m,
            isAuditedUnderOtherLaw: false,
            is44AdLockout: true,
            financialYear: "2025-26"
        );

        Assert.True(eval.IsMandatory);
        Assert.Equal(Form3CdSection44AbClause.Clause_e_Presumptive44AdLockout, eval.ApplicableClause);
        Assert.Contains("section 44AB(e)", eval.Rationale);
    }

    [Fact]
    public void CalculateSection40aDisallowance_Computes30PercentResidentAnd100PercentNonResident()
    {
        decimal residentDefault = 1_00_000m;    // 30% = ₹30,000
        decimal nonResidentDefault = 50_000m;   // 100% = ₹50,000

        var disallowance = Form3CdEngine.CalculateSection40aDisallowance(residentDefault, nonResidentDefault);

        Assert.Equal(80_000m, disallowance);
    }

    [Fact]
    public void Section43Bh_MsmeOverduePayment_DisallowedBeyondStatutoryLimit()
    {
        var invoiceDate = new DateTime(2024, 11, 1);
        var itemWithAgreement = new MsmeOverdueItem
        {
            SupplierName = "Shree Micro Tech Tools",
            EnterpriseType = "Micro",
            InvoiceNumber = "INV-042",
            InvoiceDate = invoiceDate,
            InvoiceAmount = 75_000m,
            HasWrittenAgreement = true, // 45 days
            ActualPaymentDate = invoiceDate.AddDays(55) // Paid on day 55 > 45 days
        };

        Assert.Equal(45, itemWithAgreement.AllowedDaysLimit);
        Assert.True(itemWithAgreement.IsDelayedBeyondLimit);
        Assert.Equal(75_000m, itemWithAgreement.DisallowedAmount);

        var itemWithoutAgreement = new MsmeOverdueItem
        {
            SupplierName = "Apex Small Parts",
            EnterpriseType = "Small",
            InvoiceNumber = "INV-099",
            InvoiceDate = invoiceDate,
            InvoiceAmount = 40_000m,
            HasWrittenAgreement = false, // 15 days
            ActualPaymentDate = invoiceDate.AddDays(10) // Paid on day 10 <= 15 days
        };

        Assert.Equal(15, itemWithoutAgreement.AllowedDaysLimit);
        Assert.False(itemWithoutAgreement.IsDelayedBeyondLimit);
        Assert.Equal(0m, itemWithoutAgreement.DisallowedAmount);
    }

    [Fact]
    public void ComputeAssessmentTotals_And_GenerateReportDraft_CorrectlyAggregatesDisallowances()
    {
        var assessment = new Form3CdAssessment
        {
            AssesseeName = "Alpha Automations Pvt Ltd",
            Pan = "AAACA9876C",
            Address = "Nariman Point, Mumbai",
            Status = "Company",
            FinancialYear = "2024-25",
            AssessmentYear = "2025-26",
            AuditType = Form3CdAuditType.Form3CA,
            Section44AbClause = Form3CdSection44AbClause.Clause_a_BusinessTurnover
        };

        var clauses = new List<Form3CdClauseResponse>
        {
            new()
            {
                ClauseNumber = "Clause 21(b)",
                ClauseTitle = "Amounts Inadmissible u/s 40(a)",
                Status = Form3CdClauseStatus.Disallowed,
                ReportedAmount = 60_000m,
                AuditorRemarks = "TDS not deducted on professional fees of ₹2,00,000 paid to resident (30% disallowance)."
            },
            new()
            {
                ClauseNumber = "Clause 21(d)",
                ClauseTitle = "Cash Payments Exceeding ₹10,000 (Sec 40A(3))",
                Status = Form3CdClauseStatus.Disallowed,
                ReportedAmount = 25_000m,
                AuditorRemarks = "Cash payment for office repairs made in excess of ₹10,000."
            },
            new()
            {
                ClauseNumber = "Clause 26/43B(h)",
                ClauseTitle = "Delayed Payments to MSMEs (Sec 43B(h))",
                Status = Form3CdClauseStatus.Disallowed,
                ReportedAmount = 1_50_000m,
                AuditorRemarks = "Payments to 2 Micro suppliers delayed beyond 45 days."
            },
            new()
            {
                ClauseNumber = "Clause 31",
                ClauseTitle = "Loans/Deposits in Cash (Sec 269SS/ST/T)",
                Status = Form3CdClauseStatus.Qualified,
                ReportedAmount = 50_000m,
                AuditorRemarks = "Cash loan accepted from director in violation of Sec 269SS."
            }
        };

        Form3CdEngine.ComputeAssessmentTotals(assessment, clauses);

        Assert.Equal(60_000m, assessment.TotalDisallowanceSec40a);
        Assert.Equal(25_000m, assessment.TotalDisallowanceSec40A3);
        Assert.Equal(1_50_000m, assessment.TotalDisallowanceSec43Bh);
        Assert.Equal(50_000m, assessment.TotalSec269Violations);
        // Net additions: 60k + 25k + 150k = 235k
        Assert.Equal(2_35_000m, assessment.TotalNetIncomeAdjustment);
        Assert.Equal(4, assessment.QualificationsCount);

        var reportText = Form3CdEngine.GenerateTaxAuditReportDraft(assessment, clauses);

        Assert.Contains("FORM NO. 3CA", reportText);
        Assert.Contains("Alpha Automations Pvt Ltd", reportText);
        Assert.Contains("Clause 21(b)", reportText);
        Assert.Contains("Clause 26/43B(h)", reportText);
        Assert.Contains("TOTAL ADDITIONS TO TAXABLE BUSINESS PROFIT", reportText);
        Assert.Contains(2_35_000m.ToString("C2", IndianCulture), reportText);
    }
}
