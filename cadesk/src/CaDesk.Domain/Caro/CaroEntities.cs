using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Caro;

public static class CaroCompanyType
{
    public const string Private = "Private";
    public const string Public = "Public";
    public const string Section8 = "Section8";
    public const string SmallCompany = "SmallCompany";
    public const string OnePersonCompany = "OPC";
    public const string Banking = "Banking";
    public const string Insurance = "Insurance";
    public const string Nbfc = "NBFC";

    public static readonly string[] All = [Private, Public, Section8, SmallCompany, OnePersonCompany, Banking, Insurance, Nbfc];
}

public static class CaroAssessmentStatus
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Completed = "completed";
    public const string Finalized = "finalized";

    public static readonly string[] All = [Draft, InReview, Completed, Finalized];
}

public static class CaroClauseStatus
{
    public const string Compliant = "Compliant";
    public const string Qualified = "Qualified";
    public const string Adverse = "Adverse";
    public const string NotApplicable = "NotApplicable";

    public static readonly string[] All = [Compliant, Qualified, Adverse, NotApplicable];
}

public static class CaroClauseCategory
{
    public const string PpeAndIntangibles = "PPE & Intangibles";
    public const string InventoryAndWorkingCapital = "Inventory & Working Capital";
    public const string LoansInvestmentsGuarantees = "Loans, Investments & Guarantees";
    public const string RelatedPartyAndDeposits = "Related Parties & Deposits";
    public const string StatutoryDues = "Statutory Dues";
    public const string FinancialHealthAndBorrowings = "Borrowings & Going Concern";
    public const string FraudAndInternalAudit = "Fraud & Internal Controls";
    public const string RegulatoryCompliance = "Regulatory & Group Reporting";
}

public class CaroAssessment : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid? EngagementId { get; set; }
    public AuditEngagement? Engagement { get; set; }

    public string FinancialYear { get; set; } = "2024-25";
    public string CompanyType { get; set; } = CaroCompanyType.Private;
    public bool IsHoldingOrSubsidiaryOfPublic { get; set; }
    public decimal PaidUpCapitalAndReserves { get; set; }
    public decimal TotalBorrowings { get; set; }
    public decimal TotalRevenue { get; set; }
    public bool IsCaroApplicable { get; set; } = true;
    public string? ExemptionReason { get; set; }
    public string Status { get; set; } = CaroAssessmentStatus.Draft;
    public int QualificationsCount { get; set; }
    public int AdverseRemarksCount { get; set; }
    public string? GeneratedAnnexureText { get; set; }
    public string? AuditorConclusion { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public User? FinalizedByUser { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<CaroClauseResponse> ClauseResponses { get; set; } = [];
}

public class CaroClauseResponse : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid AssessmentId { get; set; }
    public CaroAssessment? Assessment { get; set; }

    public string ClauseNumber { get; set; } = "";
    public string ClauseTitle { get; set; } = "";
    public string Category { get; set; } = CaroClauseCategory.PpeAndIntangibles;
    public string Status { get; set; } = CaroClauseStatus.Compliant;
    public string AuditorRemark { get; set; } = "";
    public bool HasDiscrepancy { get; set; }
    public decimal? DiscrepancyAmount { get; set; }
    public string? SupportingWpsRef { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public User? VerifiedByUser { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public static class CaroEngine
{
    public const decimal PrivateCompanyCapitalLimit = 10_000_000m;    // ₹1 Crore
    public const decimal PrivateCompanyBorrowingsLimit = 10_000_000m; // ₹1 Crore
    public const decimal PrivateCompanyRevenueLimit = 100_000_000m;   // ₹10 Crores

    public static (bool IsApplicable, string? Reason) EvaluateApplicability(
        string companyType,
        bool isHoldingOrSubsidiaryOfPublic,
        decimal paidUpCapitalAndReserves,
        decimal totalBorrowings,
        decimal totalRevenue)
    {
        // 1. Unconditional Exemptions under paragraph 1(2)
        if (companyType == CaroCompanyType.Banking)
            return (false, "Exempt under Paragraph 1(2)(i) of CARO 2020: Banking company as defined in Section 5(c) of Banking Regulation Act, 1949.");

        if (companyType == CaroCompanyType.Insurance)
            return (false, "Exempt under Paragraph 1(2)(ii) of CARO 2020: Insurance company as defined under Insurance Act, 1938.");

        if (companyType == CaroCompanyType.Section8)
            return (false, "Exempt under Paragraph 1(2)(iii) of CARO 2020: Company licensed to operate under Section 8 of Companies Act, 2013 (charitable objects).");

        if (companyType == CaroCompanyType.OnePersonCompany)
            return (false, "Exempt under Paragraph 1(2)(iv) of CARO 2020: One Person Company (OPC) as defined in Section 2(62) of Companies Act, 2013.");

        if (companyType == CaroCompanyType.SmallCompany)
            return (false, "Exempt under Paragraph 1(2)(iv) of CARO 2020: Small Company as defined in Section 2(85) of Companies Act, 2013.");

        // 2. Private Limited Company 3-Pillar Test
        if (companyType == CaroCompanyType.Private)
        {
            if (isHoldingOrSubsidiaryOfPublic)
            {
                return (true, "Applicable: Private company is a holding or subsidiary of a public company (deemed public company under Section 2(71)).");
            }

            var capitalBreached = paidUpCapitalAndReserves > PrivateCompanyCapitalLimit;
            var borrowingsBreached = totalBorrowings > PrivateCompanyBorrowingsLimit;
            var revenueBreached = totalRevenue > PrivateCompanyRevenueLimit;

            if (capitalBreached || borrowingsBreached || revenueBreached)
            {
                var reasons = new List<string>();
                if (capitalBreached) reasons.Add($"Paid-up capital & reserves ({paidUpCapitalAndReserves:N0}) exceeds ₹1 Crore");
                if (borrowingsBreached) reasons.Add($"Total borrowings ({totalBorrowings:N0}) exceeds ₹1 Crore");
                if (revenueBreached) reasons.Add($"Total revenue ({totalRevenue:N0}) exceeds ₹10 Crores");

                return (true, $"CARO 2020 Applicable: {string.Join("; ", reasons)}.");
            }

            return (false, $"Exempt under Paragraph 1(2)(iii) of CARO 2020: Private company satisfies all 3 threshold exemptions — Capital (₹{paidUpCapitalAndReserves:N0} <= ₹1 Cr), Borrowings (₹{totalBorrowings:N0} <= ₹1 Cr) and Revenue (₹{totalRevenue:N0} <= ₹10 Cr).");
        }

        // Public and other companies
        return (true, $"CARO 2020 Applicable for {companyType} company under Section 143(11) of Companies Act, 2013.");
    }

    public static List<(string ClauseNumber, string Title, string Category, string StandardRemark)> GetStandard21ClauseTemplates()
    {
        return
        [
            ("i(a)(A)", "Proper Records of Property, Plant and Equipment", CaroClauseCategory.PpeAndIntangibles,
                "The Company has maintained proper records showing full particulars, including quantitative details and situation of Property, Plant and Equipment."),
            ("i(a)(B)", "Proper Records of Intangible Assets", CaroClauseCategory.PpeAndIntangibles,
                "The Company has maintained proper records showing full particulars of intangible assets."),
            ("i(b)", "Physical Verification of Property, Plant and Equipment", CaroClauseCategory.PpeAndIntangibles,
                "Property, Plant and Equipment have been physically verified by management at reasonable intervals and no material discrepancies were noticed on such verification."),
            ("i(c)", "Title Deeds of Immovable Properties held in Company Name", CaroClauseCategory.PpeAndIntangibles,
                "The title deeds of all immovable properties (other than properties where company is lessee) disclosed in the financial statements are held in the name of the company."),
            ("i(d)", "Revaluation of PPE or Intangible Assets (> 10%)", CaroClauseCategory.PpeAndIntangibles,
                "The Company has not revalued its Property, Plant and Equipment (including Right of Use assets) or intangible assets during the year."),
            ("i(e)", "Proceedings for Benami Property", CaroClauseCategory.PpeAndIntangibles,
                "No proceedings have been initiated or are pending against the Company for holding any benami property under the Benami Transactions (Prohibition) Act, 1988."),

            ("ii(a)", "Physical Verification of Inventory & Discrepancies (>= 10%)", CaroClauseCategory.InventoryAndWorkingCapital,
                "The management has conducted physical verification of inventory at reasonable intervals. In our opinion the coverage and procedure of such verification is appropriate and no discrepancies of 10% or more in aggregate for each class of inventory were noticed."),
            ("ii(b)", "Quarterly Returns / Statements filed with Banks (Working Capital > ₹5 Cr)", CaroClauseCategory.InventoryAndWorkingCapital,
                "The quarterly returns or statements filed by the Company with banks or financial institutions for sanctioned working capital limits exceeding ₹5 Crores are in agreement with books of account."),

            ("iii", "Investments, Guarantees, Securities, Loans & Advances", CaroClauseCategory.LoansInvestmentsGuarantees,
                "During the year the Company has not made investments in, provided any guarantee or security or granted any loans or advances in the nature of loans, secured or unsecured, to companies, firms, Limited Liability Partnerships or any other parties."),
            ("iv", "Loans to Directors & Investments (Section 185 and 186)", CaroClauseCategory.LoansInvestmentsGuarantees,
                "In our opinion and according to the information and explanations given to us, the Company has complied with provisions of Sections 185 and 186 of the Act in respect of loans, investments, guarantees and security."),
            ("v", "Deposits Accepted by the Company (Sections 73 to 76)", CaroClauseCategory.RelatedPartyAndDeposits,
                "The Company has not accepted any deposits or amounts deemed to be deposits within the meaning of Sections 73 to 76 of the Act and the rules framed thereunder."),
            ("vi", "Maintenance of Cost Records under Section 148(1)", CaroClauseCategory.RegulatoryCompliance,
                "The Central Government has not specified the maintenance of cost records under Section 148(1) of the Act for the products/activities of the Company."),

            ("vii(a)", "Undisputed Statutory Dues (GST, PF, ESI, Income Tax)", CaroClauseCategory.StatutoryDues,
                "The Company is regular in depositing undisputed statutory dues including Goods and Services Tax, provident fund, employees' state insurance, income-tax, sales-tax, service tax, duty of customs, duty of excise, value added tax, cess and other statutory dues. No undisputed amounts were in arrears as at balance sheet date for a period of more than six months from the date they became payable."),
            ("vii(b)", "Disputed Statutory Dues Not Deposited", CaroClauseCategory.StatutoryDues,
                "According to the information and explanations given to us, there are no statutory dues which have not been deposited on account of any dispute."),

            ("viii", "Unrecorded Transactions Disclosed in Tax Assessments", CaroClauseCategory.RegulatoryCompliance,
                "There were no transactions surrendered or disclosed as income during the year in the tax assessments under the Income Tax Act, 1961."),

            ("ix(a)", "Default in Repayment of Loans or Borrowings", CaroClauseCategory.FinancialHealthAndBorrowings,
                "The Company has not defaulted in repayment of loans or other borrowings or in the payment of interest thereon to any lender."),
            ("ix(b)", "Declared Willful Defaulter by Lenders", CaroClauseCategory.FinancialHealthAndBorrowings,
                "The Company has not been declared a willful defaulter by any bank or financial institution or other lender."),
            ("ix(c)", "Application of Term Loans for Sanctioned Purpose", CaroClauseCategory.FinancialHealthAndBorrowings,
                "In our opinion and according to information given to us, term loans were applied for the purpose for which the loans were obtained."),
            ("ix(d)", "Short-Term Funds Used for Long-Term Purposes", CaroClauseCategory.FinancialHealthAndBorrowings,
                "On an overall examination of financial statements, funds raised on short-term basis have not been utilized for long-term purposes."),
            ("ix(e)", "Funds Taken to Meet Obligations of Subsidiaries/JVs", CaroClauseCategory.FinancialHealthAndBorrowings,
                "The Company has not taken any funds from any entity or person on account of or to meet the obligations of its subsidiaries, associates or joint ventures."),
            ("ix(f)", "Loans Raised on Pledge of Securities in Subsidiaries/JVs", CaroClauseCategory.FinancialHealthAndBorrowings,
                "The Company has not raised any loans during the year on the pledge of securities held in its subsidiaries, joint ventures or associate companies."),

            ("x(a)", "Application of Moneys from IPO / FPO", CaroClauseCategory.RegulatoryCompliance,
                "The Company has not raised any moneys by way of initial public offer or further public offer (including debt instruments) during the year."),
            ("x(b)", "Preferential Allotment / Private Placement (Section 42 & 62)", CaroClauseCategory.RegulatoryCompliance,
                "The Company has not made any preferential allotment or private placement of shares or convertible debentures during the year."),

            ("xi(a)", "Fraud Noticed or Reported during the Year", CaroClauseCategory.FraudAndInternalAudit,
                "No fraud by the Company or any fraud on the Company has been noticed or reported during the year."),
            ("xi(b)", "Report under Section 143(12) in Form ADT-4", CaroClauseCategory.FraudAndInternalAudit,
                "No report under sub-section (12) of Section 143 of the Act has been filed by the auditors in Form ADT-4 with the Central Government."),
            ("xi(c)", "Whistle-Blower Complaints Received", CaroClauseCategory.FraudAndInternalAudit,
                "According to explanations given to us, no whistle-blower complaints were received during the year by the Company."),

            ("xii", "Compliance with Nidhi Company Regulations", CaroClauseCategory.RegulatoryCompliance,
                "The Company is not a Nidhi Company as defined under Section 406 of the Act. Accordingly, Clause 3(xii) is not applicable."),
            ("xiii", "Related Party Transactions (Section 177 and 188)", CaroClauseCategory.RelatedPartyAndDeposits,
                "In our opinion all transactions with the related parties are in compliance with Sections 177 and 188 of the Act and details have been disclosed in the financial statements as required by applicable accounting standards."),
            ("xiv", "Internal Audit System Commensurate with Size", CaroClauseCategory.FraudAndInternalAudit,
                "The Company has an internal audit system commensurate with the size and nature of its business, and internal audit reports were duly considered."),
            ("xv", "Non-Cash Transactions with Directors (Section 192)", CaroClauseCategory.RelatedPartyAndDeposits,
                "The Company has not entered into any non-cash transactions with directors or persons connected with him."),
            ("xvi", "Registration under Section 45-IA of RBI Act (NBFC / CIC)", CaroClauseCategory.RegulatoryCompliance,
                "The Company is not required to be registered under Section 45-IA of the Reserve Bank of India Act, 1934."),
            ("xvii", "Cash Losses Incurred in Current and Preceding FY", CaroClauseCategory.FinancialHealthAndBorrowings,
                "The Company has not incurred cash losses in the financial year and in the immediately preceding financial year."),
            ("xviii", "Resignation of Statutory Auditors", CaroClauseCategory.FraudAndInternalAudit,
                "There has been no resignation of statutory auditors during the year."),
            ("xix", "Material Uncertainty on Going Concern / Liability Payment within 1 Year", CaroClauseCategory.FinancialHealthAndBorrowings,
                "On the basis of financial ratios, ageing and expected realization of financial assets, payment of financial liabilities and other information accompanying the financial statements, in our opinion, no material uncertainty exists as on report date regarding company's capability to meet its liabilities existing at balance sheet date within a period of one year."),
            ("xx", "Corporate Social Responsibility Compliance (Section 135)", CaroClauseCategory.RegulatoryCompliance,
                "The Company has complied with Section 135 of the Act in respect of unspent Corporate Social Responsibility amounts."),
            ("xxi", "Qualifications in CARO Reports of Group Entities", CaroClauseCategory.RegulatoryCompliance,
                "There are no qualifications or adverse remarks by the respective auditors in the CARO reports of companies included in consolidated financial statements.")
        ];
    }

    public static string GenerateAnnexureDraft(CaroAssessment assessment, List<CaroClauseResponse> clauses)
    {
        var sb = new StringBuilder();
        var clientName = assessment.Client?.Name ?? "The Company";

        sb.AppendLine($"ANNEXURE 'A' TO THE INDEPENDENT AUDITOR'S REPORT");
        sb.AppendLine($"Referred to in Paragraph 1 under 'Report on Other Legal and Regulatory Requirements' of our report of even date to the members of {clientName} on the financial statements for the year ended 31st March, {assessment.FinancialYear.Split('-').LastOrDefault() ?? "2025"}:");
        sb.AppendLine();

        if (!assessment.IsCaroApplicable)
        {
            sb.AppendLine($"The Companies (Auditor's Report) Order, 2020 ('the Order') is not applicable to the Company for the year under audit for the following reason:");
            sb.AppendLine($"{assessment.ExemptionReason}");
            sb.AppendLine();
            sb.AppendLine("Accordingly, no statement on matters specified in paragraphs 3 and 4 of the Order is required.");
            return sb.ToString();
        }

        sb.AppendLine("To the best of our information and according to explanations given to us and condition of books of account examined by us in the normal course of audit, we report that:");
        sb.AppendLine();

        foreach (var c in clauses.OrderBy(x => x.ClauseNumber))
        {
            sb.Append($"({c.ClauseNumber}) ");
            if (c.Status == CaroClauseStatus.NotApplicable)
            {
                sb.AppendLine($"In our opinion, Clause 3({c.ClauseNumber}) of the Order regarding '{c.ClauseTitle}' is not applicable to the Company.");
            }
            else if (c.Status == CaroClauseStatus.Qualified || c.Status == CaroClauseStatus.Adverse)
            {
                var prefix = c.Status == CaroClauseStatus.Adverse ? "ADVERSE REMARK: " : "QUALIFICATION: ";
                sb.Append(prefix);
                sb.Append(c.AuditorRemark);
                if (c.HasDiscrepancy && c.DiscrepancyAmount.HasValue && c.DiscrepancyAmount.Value > 0)
                {
                    sb.Append($" Discrepancy amount quantified: ₹{c.DiscrepancyAmount.Value:N2}.");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(string.IsNullOrWhiteSpace(c.AuditorRemark) ? "Complied with in all material respects." : c.AuditorRemark);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
