using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.TaxAudit;

public static class Form3CdAuditType
{
    public const string Form3CA = "Form3CA"; // Where accounts are audited under any other law (e.g., Companies Act)
    public const string Form3CB = "Form3CB"; // Where accounts are audited solely under Section 44AB (e.g., Proprietorship, Partnership)

    public static readonly string[] All = [Form3CA, Form3CB];
}

public static class Form3CdSection44AbClause
{
    public const string Clause_a_BusinessTurnover = "44AB(a) - Business Turnover > ₹1 Cr (or ₹10 Cr if cash <= 5%)";
    public const string Clause_b_ProfessionReceipts = "44AB(b) - Professional Receipts > ₹50 L (or ₹75 L if cash <= 5%)";
    public const string Clause_c_PresumptiveBusiness = "44AB(c) - Section 44AE / 44BB / 44BBB lower profit declared";
    public const string Clause_d_PresumptiveProfession = "44AB(d) - Section 44ADA lower profit declared";
    public const string Clause_e_Presumptive44AdLockout = "44AB(e) - Section 44AD(4) 5-year opt-out lockout";

    public static readonly string[] All = [
        Clause_a_BusinessTurnover,
        Clause_b_ProfessionReceipts,
        Clause_c_PresumptiveBusiness,
        Clause_d_PresumptiveProfession,
        Clause_e_Presumptive44AdLockout
    ];
}

public static class Form3CdStatus
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Completed = "completed";
    public const string Finalized = "finalized";

    public static readonly string[] All = [Draft, InReview, Completed, Finalized];
}

public static class Form3CdClauseStatus
{
    public const string Clean = "Clean";
    public const string Disallowed = "Disallowed";
    public const string Qualified = "Qualified";
    public const string NotApplicable = "NotApplicable";

    public static readonly string[] All = [Clean, Disallowed, Qualified, NotApplicable];
}

public static class Form3CdClauseCategory
{
    public const string PartAGeneral = "Part A - General Assessee Details";
    public const string AccountingAndIcds = "Accounting Methods & ICDS";
    public const string DepreciationAndDeductions = "Depreciation & Capital Deductions";
    public const string DisallowancesAndAdditions = "Disallowances & Additions (Sec 40, 40A, 36)";
    public const string StatutoryDuesAnd43B = "Statutory Dues & Sec 43B / 43B(h)";
    public const string LoansAndCash269 = "Loans, Deposits & Cash (Sec 269SS/ST/T)";
    public const string TdsCompliance = "TDS / TCS Compliance (Chapter XVII-B)";
    public const string RatiosAndGst = "Analytical Ratios & GST Expenditure";
    public const string OtherDisclosures = "Other Statutory Disclosures";
}

public class Form3CdAssessment : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? AuditEngagementId { get; set; }
    public string FinancialYear { get; set; } = ""; // e.g. "2024-25"
    public string AssessmentYear { get; set; } = ""; // e.g. "2025-26"
    public string AuditType { get; set; } = Form3CdAuditType.Form3CA;
    public string Section44AbClause { get; set; } = Form3CdSection44AbClause.Clause_a_BusinessTurnover;

    // Turnover & Cash Receipts / Payments
    public decimal TotalTurnoverOrGrossReceipts { get; set; }
    public decimal TotalCashReceipts { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal TotalCashPayments { get; set; }
    public decimal CashReceiptPercentage { get; set; }
    public decimal CashPaymentPercentage { get; set; }
    public bool IsTaxAuditMandatory { get; set; } = true;
    public string? ApplicabilityReason { get; set; }

    // Part A Particulars
    public string AssesseeName { get; set; } = "";
    public string Pan { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Gstin { get; set; }
    public string Status { get; set; } = "Company"; // Company, Firm, LLP, Individual, HUF, AOP, BOI
    public string MethodOfAccounting { get; set; } = "Mercantile"; // Mercantile, Cash
    public string OptedSpecialTaxRegime { get; set; } = "None"; // None, 115BAA, 115BAB, 115BAC, 115BAD, 115BAE

    // Disallowance & Adjustment Totals (Quantified particulars)
    public decimal TotalDisallowanceSec40a { get; set; } // Sec 40(a)(i) 100% + 40(a)(ia) 30%
    public decimal TotalDisallowanceSec40A3 { get; set; } // Cash payments > ₹10,000 u/s 40A(3)
    public decimal TotalDisallowanceSec43B { get; set; } // Unpaid statutory dues / interest
    public decimal TotalDisallowanceSec43Bh { get; set; } // MSME payments delayed beyond 15/45 days
    public decimal TotalDisallowanceSec361va { get; set; } // Employee PF/ESI late deposits
    public decimal TotalSec269Violations { get; set; } // 269SS/ST/T cash violations
    public decimal TotalIcdsNetAdjustment { get; set; } // Net ICDS adjustments
    public decimal TotalNetIncomeAdjustment { get; set; } // Sum of additions to taxable profit

    // Status & Audit Workflow
    public string AssessmentStatus { get; set; } = Form3CdStatus.Draft;
    public int QualificationsCount { get; set; }
    public int DiscrepanciesCount { get; set; }
    public string? AuditorObservations { get; set; } // Paragraph 3 / observation remarks
    public string? GeneratedReportText { get; set; } // Full Form 3CA / 3CB report text

    public Guid? FinalizedByUserId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
    public User? FinalizedByUser { get; set; }
    public List<Form3CdClauseResponse> ClauseResponses { get; set; } = new();
}

public class Form3CdClauseResponse : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid AssessmentId { get; set; }
    public string ClauseNumber { get; set; } = ""; // e.g. "Clause 13", "Clause 21(a)", "Clause 26/43B", "Clause 31"
    public string ClauseTitle { get; set; } = "";
    public string Category { get; set; } = Form3CdClauseCategory.PartAGeneral;
    public string Status { get; set; } = Form3CdClauseStatus.Clean; // Clean, Disallowed, Qualified, NotApplicable
    public decimal ReportedAmount { get; set; }
    public string AuditorRemarks { get; set; } = "";
    public bool HasDiscrepancy { get; set; }
    public string? ParticularsJson { get; set; } // Structured line items (e.g., MSME bills, 40(a)(ia) entries)
    public string? WorkpaperRef { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Form3CdAssessment? Assessment { get; set; }
    public User? VerifiedByUser { get; set; }
}

public record Form3CdApplicabilityEvaluation(
    bool IsMandatory,
    string RecommendedAuditType,
    string ApplicableClause,
    decimal CashReceiptPercent,
    decimal CashPaymentPercent,
    string Rationale,
    decimal StatutoryTurnoverThreshold
);

public class MsmeOverdueItem
{
    public string SupplierName { get; set; } = "";
    public string? SupplierGstin { get; set; }
    public string EnterpriseType { get; set; } = "Micro"; // Micro, Small
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public decimal InvoiceAmount { get; set; }
    public bool HasWrittenAgreement { get; set; }
    public int AllowedDaysLimit => HasWrittenAgreement ? 45 : 15;
    public DateTime StatutoryDueDate => InvoiceDate.AddDays(AllowedDaysLimit);
    public DateTime? ActualPaymentDate { get; set; }
    public bool IsDelayedBeyondLimit => ActualPaymentDate.HasValue
        ? ActualPaymentDate.Value.Date > StatutoryDueDate.Date
        : DateTime.UtcNow.Date > StatutoryDueDate.Date;
    public decimal DisallowedAmount => IsDelayedBeyondLimit ? InvoiceAmount : 0m;
}

public static class Form3CdEngine
{
    private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");

    public static Form3CdApplicabilityEvaluation EvaluateApplicability(
        string entityType, // "Company", "Partnership", "LLP", "Individual", "HUF"
        bool isBusiness, // true = business, false = profession
        decimal turnoverOrGrossReceipts,
        decimal cashReceipts,
        decimal totalPayments,
        decimal cashPayments,
        bool isAuditedUnderOtherLaw, // true for Companies, etc.
        bool declaresLowerPresumptiveProfit = false,
        bool is44AdLockout = false,
        bool optsPresumptive44Ada = false,
        string? financialYear = null)
    {
        // cite the law of the year being audited (Income-tax Act, 2025 from tax year 2026-27)
        string S(string ref1961) => IncomeTaxLaw.Sec(ref1961, financialYear);

        var cashReceiptsPct = turnoverOrGrossReceipts > 0
            ? Math.Round((cashReceipts / turnoverOrGrossReceipts) * 100m, 2)
            : 0m;

        var cashPaymentsPct = totalPayments > 0
            ? Math.Round((cashPayments / totalPayments) * 100m, 2)
            : 0m;

        string auditType = (entityType == "Company" || isAuditedUnderOtherLaw)
            ? Form3CdAuditType.Form3CA
            : Form3CdAuditType.Form3CB;

        // 1. Presumptive 44AD(4) lockout check
        if (is44AdLockout)
        {
            return new Form3CdApplicabilityEvaluation(
                IsMandatory: true,
                RecommendedAuditType: auditType,
                ApplicableClause: Form3CdSection44AbClause.Clause_e_Presumptive44AdLockout,
                CashReceiptPercent: cashReceiptsPct,
                CashPaymentPercent: cashPaymentsPct,
                Rationale: $"Mandatory tax audit under {S("44AB(e)")}: Assessee has opted out of presumptive taxation within the 5-year lockout period under {S("44AD(4)")}.",
                StatutoryTurnoverThreshold: 0m
            );
        }

        // 2. Presumptive lower profit declared check
        if (declaresLowerPresumptiveProfit)
        {
            string clause = isBusiness
                ? Form3CdSection44AbClause.Clause_c_PresumptiveBusiness
                : Form3CdSection44AbClause.Clause_d_PresumptiveProfession;

            return new Form3CdApplicabilityEvaluation(
                IsMandatory: true,
                RecommendedAuditType: auditType,
                ApplicableClause: clause,
                CashReceiptPercent: cashReceiptsPct,
                CashPaymentPercent: cashPaymentsPct,
                Rationale: $"Mandatory tax audit under {S(isBusiness ? "44AB(c)" : "44AB(d)")}: Assessee declared net profit lower than statutory deemed presumptive rates.",
                StatutoryTurnoverThreshold: 0m
            );
        }

        // 3. Professional gross receipts check
        if (!isBusiness)
        {
            // Audit above Rs 50 lakh: 44AB(b) / s.63(1) (Table: Sl. No. 1(c)). Rs 75 lakh is the 44ADA presumptive ceiling
            // (cash receipts <= 5%), not an audit threshold: it only spares a professional who declares at least the
            // deemed 50% under 44ADA, which the caller states with optsPresumptive44Ada.
            const decimal threshold = 50_00_000m;
            bool within44AdaCeiling = cashReceiptsPct <= 5.0m ? turnoverOrGrossReceipts <= 75_00_000m : turnoverOrGrossReceipts <= 50_00_000m;

            if (turnoverOrGrossReceipts > threshold && optsPresumptive44Ada && within44AdaCeiling)
            {
                return new Form3CdApplicabilityEvaluation(
                    IsMandatory: false,
                    RecommendedAuditType: auditType,
                    ApplicableClause: Form3CdSection44AbClause.Clause_b_ProfessionReceipts,
                    CashReceiptPercent: cashReceiptsPct,
                    CashPaymentPercent: cashPaymentsPct,
                    Rationale: $"Tax audit not mandatory: professional gross receipts ({turnoverOrGrossReceipts.ToString("C2", IndianCulture)}) are within the {S("44ADA")} presumptive ceiling of ₹75 lakh (cash receipts <= 5%) and income is declared at not less than the deemed 50%.",
                    StatutoryTurnoverThreshold: 75_00_000m
                );
            }

            if (turnoverOrGrossReceipts > threshold)
            {
                return new Form3CdApplicabilityEvaluation(
                    IsMandatory: true,
                    RecommendedAuditType: auditType,
                    ApplicableClause: Form3CdSection44AbClause.Clause_b_ProfessionReceipts,
                    CashReceiptPercent: cashReceiptsPct,
                    CashPaymentPercent: cashPaymentsPct,
                    Rationale: $"Mandatory tax audit under {S("44AB(b)")}: Professional gross receipts ({turnoverOrGrossReceipts.ToString("C2", IndianCulture)}) exceed ₹50 lakh." +
                        (within44AdaCeiling ? $" (Not required if income is offered under {S("44ADA")} at not less than 50% of receipts.)" : ""),
                    StatutoryTurnoverThreshold: threshold
                );
            }

            return new Form3CdApplicabilityEvaluation(
                IsMandatory: false,
                RecommendedAuditType: auditType,
                ApplicableClause: Form3CdSection44AbClause.Clause_b_ProfessionReceipts,
                CashReceiptPercent: cashReceiptsPct,
                CashPaymentPercent: cashPaymentsPct,
                Rationale: $"Tax audit not mandatory under {S("44AB(b)")}: Professional gross receipts ({turnoverOrGrossReceipts.ToString("C2", IndianCulture)}) within statutory threshold ({threshold.ToString("C2", IndianCulture)}).",
                StatutoryTurnoverThreshold: threshold
            );
        }

        // 4. Business turnover check
        // Standard limit ₹1 Crore; Enhanced limit ₹10 Crores if cash receipts <= 5% AND cash payments <= 5%
        bool isEligibleFor10Crores = (cashReceiptsPct <= 5.0m && cashPaymentsPct <= 5.0m);
        decimal businessThreshold = isEligibleFor10Crores ? 10_00_00_000m : 1_00_00_000m;

        if (turnoverOrGrossReceipts > businessThreshold)
        {
            string ruleDesc = isEligibleFor10Crores
                ? "enhanced threshold of ₹10 Crores (aggregate cash receipts and cash payments both <= 5%)"
                : "standard threshold of ₹1 Crore (aggregate cash receipts or payments exceed 5%)";

            return new Form3CdApplicabilityEvaluation(
                IsMandatory: true,
                RecommendedAuditType: auditType,
                ApplicableClause: Form3CdSection44AbClause.Clause_a_BusinessTurnover,
                CashReceiptPercent: cashReceiptsPct,
                CashPaymentPercent: cashPaymentsPct,
                Rationale: $"Mandatory tax audit under {S("44AB(a)")}: Business turnover ({turnoverOrGrossReceipts.ToString("C2", IndianCulture)}) exceeds statutory {ruleDesc}.",
                StatutoryTurnoverThreshold: businessThreshold
            );
        }

        string exemptReason = isEligibleFor10Crores
            ? $"Business turnover ({turnoverOrGrossReceipts.ToString("C2", IndianCulture)}) is within the ₹10 Crore threshold under {S("44AB(a)")} (cash receipts: {cashReceiptsPct}%, cash payments: {cashPaymentsPct}%)."
            : $"Business turnover ({turnoverOrGrossReceipts.ToString("C2", IndianCulture)}) is within the standard ₹1 Crore threshold under {S("44AB(a)")}.";

        return new Form3CdApplicabilityEvaluation(
            IsMandatory: false,
            RecommendedAuditType: auditType,
            ApplicableClause: Form3CdSection44AbClause.Clause_a_BusinessTurnover,
            CashReceiptPercent: cashReceiptsPct,
            CashPaymentPercent: cashPaymentsPct,
            Rationale: $"Tax audit not mandatory: {exemptReason}",
            StatutoryTurnoverThreshold: businessThreshold
        );
    }

    public static decimal CalculateSection40aDisallowance(decimal residentPaymentWithoutTds, decimal nonResidentPaymentWithoutTds)
    {
        // Sec 40(a)(ia): 30% disallowance for resident TDS defaults
        // Sec 40(a)(i): 100% disallowance for non-resident TDS defaults
        var residentDisallowance = Math.Round(residentPaymentWithoutTds * 0.30m, 2);
        var nonResidentDisallowance = Math.Round(nonResidentPaymentWithoutTds * 1.00m, 2);
        return residentDisallowance + nonResidentDisallowance;
    }

    public static void ComputeAssessmentTotals(Form3CdAssessment assessment, IEnumerable<Form3CdClauseResponse> clauses)
    {
        decimal total40a = 0m;
        decimal total40A3 = 0m;
        decimal total43B = 0m;
        decimal total43Bh = 0m;
        decimal total361va = 0m;
        decimal totalSec269 = 0m;
        decimal totalIcds = 0m;
        int qualifications = 0;
        int discrepancies = 0;

        foreach (var clause in clauses)
        {
            if (clause.Status == Form3CdClauseStatus.Qualified || clause.Status == Form3CdClauseStatus.Disallowed)
            {
                qualifications++;
            }

            if (clause.HasDiscrepancy)
            {
                discrepancies++;
            }

            switch (clause.ClauseNumber)
            {
                case "Clause 13": // ICDS
                    totalIcds += clause.ReportedAmount;
                    break;
                case "Clause 20(b)": // Sec 36(1)(va) PF/ESI employee share late
                    total361va += clause.ReportedAmount;
                    break;
                case "Clause 21(a)": // Sec 37 offenses/penalties
                case "Clause 21(b)": // Sec 40(a)(i) and 40(a)(ia)
                    total40a += clause.ReportedAmount;
                    break;
                case "Clause 21(d)": // Sec 40A(3) cash > 10,000
                    total40A3 += clause.ReportedAmount;
                    break;
                case "Clause 26": // Standard 43B statutory dues
                    total43B += clause.ReportedAmount;
                    break;
                case "Clause 26/43B(h)": // MSME 45-day overdue
                    total43Bh += clause.ReportedAmount;
                    break;
                case "Clause 31": // Sec 269SS/ST/T
                    totalSec269 += clause.ReportedAmount;
                    break;
            }
        }

        assessment.TotalDisallowanceSec40a = total40a;
        assessment.TotalDisallowanceSec40A3 = total40A3;
        assessment.TotalDisallowanceSec43B = total43B;
        assessment.TotalDisallowanceSec43Bh = total43Bh;
        assessment.TotalDisallowanceSec361va = total361va;
        assessment.TotalSec269Violations = totalSec269;
        assessment.TotalIcdsNetAdjustment = totalIcds;

        // Sum of all additions to income (excluding 269 penalties which are separate penalty provisions)
        assessment.TotalNetIncomeAdjustment = total40a + total40A3 + total43B + total43Bh + total361va + totalIcds;
        assessment.QualificationsCount = qualifications;
        assessment.DiscrepanciesCount = discrepancies;
    }

    public static string GenerateTaxAuditReportDraft(Form3CdAssessment assessment, IEnumerable<Form3CdClauseResponse> clauses)
    {
        var sb = new StringBuilder();
        var fy = assessment.FinancialYear;
        bool act2025 = IncomeTaxLaw.IsAct2025(fy);
        string S(string ref1961) => IncomeTaxLaw.SecShort(ref1961, fy);
        string statement = IncomeTaxLaw.Form("3CD", fy); // Form No. 26 from tax year 2026-27
        string reportTitle = act2025
            // Rules 2026 merge 3CA / 3CB / 3CD into one Form No. 26 under section 63
            ? $"{statement.ToUpperInvariant()} - AUDIT REPORT AND STATEMENT OF PARTICULARS UNDER SECTION 63 OF THE {IncomeTaxLaw.Act2025.ToUpperInvariant()}"
            : assessment.AuditType == Form3CdAuditType.Form3CA
                ? "FORM NO. 3CA - AUDIT REPORT UNDER SECTION 44AB OF THE INCOME-TAX ACT, 1961"
                : "FORM NO. 3CB - AUDIT REPORT UNDER SECTION 44AB OF THE INCOME-TAX ACT, 1961";

        sb.AppendLine(reportTitle);
        sb.AppendLine(new string('=', reportTitle.Length));
        sb.AppendLine();

        sb.AppendLine($"Name of Assessee   : {assessment.AssesseeName}");
        sb.AppendLine($"PAN                : {assessment.Pan}");
        sb.AppendLine($"Address            : {assessment.Address}");
        sb.AppendLine($"Status             : {assessment.Status}");
        sb.AppendLine(act2025
            ? $"Tax Year           : {fy}"
            : $"Financial Year     : {fy} (Assessment Year: {assessment.AssessmentYear})");
        sb.AppendLine(act2025
            ? $"Audit Form         : {statement} (tax audit trigger: {IncomeTaxLaw.SecWithEarlier(TriggerRef(assessment.Section44AbClause), fy)})"
            : $"Audit Form         : {assessment.AuditType} (Section: {assessment.Section44AbClause})");
        sb.AppendLine($"Method of Account  : {assessment.MethodOfAccounting}");
        sb.AppendLine($"Special Tax Regime : {assessment.OptedSpecialTaxRegime}");
        sb.AppendLine();

        if (assessment.AuditType == Form3CdAuditType.Form3CA)
        {
            sb.AppendLine("1. We report that the statutory audit of the accounts of the assessee was conducted by us/statutory auditors under the Companies Act, 2013, and a copy of the audit report along with the audited financial statements is annexed hereto.");
            sb.AppendLine($"2. We report that the statement of particulars required to be furnished under {IncomeTaxLaw.Sec("44AB", fy)} is given in {statement} annexed herewith.");
            sb.AppendLine($"3. In our opinion and to the best of our information and according to explanations given to us, the particulars given in the said {statement} and the Annexures thereto are true and correct, subject to the following observations / qualifications:");
        }
        else
        {
            sb.AppendLine($"1. We have examined the balance sheet as at 31st March of the relevant {(act2025 ? "tax year" : "previous year")}, and the profit and loss account / income and expenditure account for the year ended on that date, of the assessee.");
            sb.AppendLine("2. We certify that the balance sheet and the profit and loss account are in agreement with the books of account maintained at the head office and branches.");
            sb.AppendLine($"3. In our opinion and to the best of our information and according to explanations given to us, the said accounts give a true and fair view, and the particulars given in {statement} are true and correct, subject to the following observations / qualifications:");
        }

        sb.AppendLine();

        // Observation paragraphs based on modified clauses
        var modifiedClauses = clauses
            .Where(c => c.Status == Form3CdClauseStatus.Qualified || c.Status == Form3CdClauseStatus.Disallowed)
            .OrderBy(c => c.ClauseNumber)
            .ToList();

        if (modifiedClauses.Count == 0)
        {
            sb.AppendLine($"   * None. All particulars furnished in {statement} have been verified against the books of account, statutory returns, and vouchers and are found clean and compliant.");
        }
        else
        {
            int index = 1;
            foreach (var mc in modifiedClauses)
            {
                sb.AppendLine($"   ({index++}) {mc.ClauseNumber} [{mc.ClauseTitle}]:");
                sb.AppendLine($"       Status: {mc.Status.ToUpper()} | Reported Amount: {mc.ReportedAmount.ToString("C2", IndianCulture)}");
                sb.AppendLine($"       Auditor Finding: {mc.AuditorRemarks}");
                if (!string.IsNullOrWhiteSpace(mc.WorkpaperRef))
                {
                    sb.AppendLine($"       Audit Reference: {mc.WorkpaperRef}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"STATEMENT OF QUANTIFIED DISALLOWANCES / ADDITIONS TO TAXABLE PROFIT ({statement.ToUpperInvariant()}):");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine(Line($"1. {S("40(a)(i)")} & {S("40(a)(ia)")} TDS defaults disallowance", assessment.TotalDisallowanceSec40a));
        sb.AppendLine(Line($"2. {S("40A(3)")} Cash payments exceeding ₹10,000", assessment.TotalDisallowanceSec40A3));
        sb.AppendLine(Line($"3. {S("43B")} Unpaid statutory dues / interest", assessment.TotalDisallowanceSec43B));
        sb.AppendLine(Line($"4. {S("43B(h)")} MSME overdue payments (> 15/45 days)", assessment.TotalDisallowanceSec43Bh));
        sb.AppendLine(Line($"5. {S("36(1)(va)")} Employee PF/ESI late deposits", assessment.TotalDisallowanceSec361va));
        sb.AppendLine($"6. Net ICDS I-X adjustments to business profit         : {assessment.TotalIcdsNetAdjustment.ToString("C2", IndianCulture)}");
        sb.AppendLine($"TOTAL ADDITIONS TO TAXABLE BUSINESS PROFIT             : {assessment.TotalNetIncomeAdjustment.ToString("C2", IndianCulture)}");
        sb.AppendLine();
        sb.AppendLine($"{S("269SS")} / {S("269ST")} / {S("269T")} cash loans / repayments violations reported: {assessment.TotalSec269Violations.ToString("C2", IndianCulture)}");
        if (act2025)
        {
            sb.AppendLine();
            sb.AppendLine($"Note: clause references above follow the Form No. 3CD layout; map them to the clauses of {statement} before filing.");
        }
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("For & on behalf of:");
        sb.AppendLine("Chartered Accountants");
        sb.AppendLine("ICAI Firm Registration Number: 000000N");
        sb.AppendLine();
        sb.AppendLine("Partner / Proprietor");
        sb.AppendLine("Membership No.: 000000");
        sb.AppendLine("UDIN: [Generated upon final sign-off]");
        sb.AppendLine($"Place: India | Date: {DateTime.UtcNow:dd-MM-yyyy}");

        return sb.ToString();
    }

    private static string Line(string label, decimal amount) =>
        $"{label.PadRight(55)}: {amount.ToString("C2", IndianCulture)}";

    /// <summary>"44AB(a) - Business ..." -> "44AB(a)", the 1961 trigger stored on the assessment.</summary>
    private static string TriggerRef(string? section44AbClause) =>
        string.IsNullOrWhiteSpace(section44AbClause) ? "44AB" : section44AbClause.Split(' ')[0];

    public static List<Form3CdClauseTemplate> GetStandardClausesTemplate()
    {
        return new List<Form3CdClauseTemplate>
        {
            // Part A
            new("Clause 1-8a", "Part A - General Assessee & Audit Details", Form3CdClauseCategory.PartAGeneral, "Name, address, PAN, GSTIN, status, previous year, 44AB applicable clause, and special tax regime."),
            
            // Part B
            new("Clause 13", "Method of Accounting & ICDS Adjustments", Form3CdClauseCategory.AccountingAndIcds, "Method of accounting employed (Mercantile/Cash), deviations, and net profit adjustments under ICDS I to X."),
            new("Clause 14", "Method of Valuation of Closing Stock", Form3CdClauseCategory.AccountingAndIcds, "Valuation of inventory and deviation from Section 145A (inclusive method of taxes/GST)."),
            new("Clause 16", "Amounts Not Credited to Profit & Loss", Form3CdClauseCategory.AccountingAndIcds, "Capital receipts, export incentives (duty drawback), refunds of customs/excise/GST, and other receipts not credited to P&L."),
            new("Clause 17", "Transfer of Land/Building for Inadequate Consideration", Form3CdClauseCategory.DepreciationAndDeductions, "Transfer of land or building for consideration less than stamp duty value u/s 43CA or 50C."),
            new("Clause 18", "Depreciation Allowable under Income Tax Act, 1961", Form3CdClauseCategory.DepreciationAndDeductions, "Depreciation schedule by block of assets, 180-day rule for additions, deletions, and additional depreciation u/s 32(1)(iia)."),
            new("Clause 19", "Deductions Admissible under Chapter IV (Sec 32AC, 35, etc.)", Form3CdClauseCategory.DepreciationAndDeductions, "Admissible deductions u/s 32AC, 33AB, 35(1), 35(2AA), 35D, 35E."),
            new("Clause 20(b)", "Employee PF / ESI Superannuation Late Deposits (Sec 36(1)(va))", Form3CdClauseCategory.DisallowancesAndAdditions, "Sums received from employees as contribution to PF/ESI not credited to employee account on or before statutory due date under the PF/ESI Act."),
            new("Clause 21(a)", "Expenditure of Personal Nature / Penalties / Offenses", Form3CdClauseCategory.DisallowancesAndAdditions, "Expenditure for personal purposes, fines/penalties for infraction of law, compounding fees, and souvenirs u/s 37."),
            new("Clause 21(b)", "Amounts Inadmissible u/s 40(a) (TDS Defaults)", Form3CdClauseCategory.DisallowancesAndAdditions, "Disallowance u/s 40(a)(i) (100% on non-resident payments) and Sec 40(a)(ia) (30% on resident payments without TDS)."),
            new("Clause 21(d)", "Disallowance for Cash Payments Exceeding ₹10,000 (Sec 40A(3))", Form3CdClauseCategory.DisallowancesAndAdditions, "Cash payments in excess of ₹10,000 per person per day (or ₹35,000 for goods carriages) not falling under Rule 6DD."),
            new("Clause 23", "Payments to Specified Persons / Related Parties (Sec 40A(2)(b))", Form3CdClauseCategory.DisallowancesAndAdditions, "Particulars of payments made to persons specified u/s 40A(2)(b) and whether consideration is excessive or unreasonable."),
            new("Clause 26", "Liability for Statutory Dues under Section 43B", Form3CdClauseCategory.StatutoryDuesAnd43B, "Liability for taxes, duties, cess, employee bonuses, leave encashment, and interest to banks/FIs paid on/before 139(1) vs unpaid."),
            new("Clause 26/43B(h)", "Disallowance for Delayed Payments to MSMEs (Sec 43B(h))", Form3CdClauseCategory.StatutoryDuesAnd43B, "Payments due to Micro and Small Enterprises beyond the 15-day / 45-day statutory limit under Section 15 of MSMED Act, 2006."),
            new("Clause 29B", "Gifts & Inadequate Consideration Received (Sec 56(2)(x))", Form3CdClauseCategory.DisallowancesAndAdditions, "Any sum of money or property received without consideration or for inadequate consideration exceeding ₹50,000."),
            new("Clause 31", "Acceptance & Repayment of Loans/Deposits (Sec 269SS/ST/T)", Form3CdClauseCategory.LoansAndCash269, "Particulars of loans/deposits accepted > ₹20,000 (269SS), cash receipts > ₹2,00,000 (269ST), and loan repayments > ₹20,000 (269T) in cash."),
            new("Clause 34", "TDS / TCS Compliance & Quarterly Returns (Chapter XVII-B)", Form3CdClauseCategory.TdsCompliance, "Compliance with deduction/collection, timely filing of Form 24Q, 26Q, 27Q, 27EQ, and interest payable u/s 201(1A) / 206C(7)."),
            new("Clause 40", "Accounting Ratios (Current Year vs Preceding Year)", Form3CdClauseCategory.RatiosAndGst, "Gross profit/turnover, Net profit/turnover, Stock-in-trade/turnover, and Material consumed/finished goods produced."),
            new("Clause 44", "Break-up of Total Expenditure in Respect of GST", Form3CdClauseCategory.RatiosAndGst, "Total expenditure relating to entities registered under GST (exempt, composition, registered) vs entities not registered under GST.")
        };
    }
}

public record Form3CdClauseTemplate(
    string ClauseNumber,
    string ClauseTitle,
    string Category,
    string Description
);
