using System.Text.RegularExpressions;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Audit;

public static class AuditorOpinionType
{
    public const string Unmodified = "Unmodified";
    public const string Qualified = "Qualified";
    public const string Adverse = "Adverse";
    public const string Disclaimer = "Disclaimer";

    public static readonly string[] All = [Unmodified, Qualified, Adverse, Disclaimer];
}

public static class Rule11AuditTrailStatus
{
    public const string CompliedOperatedWithoutTampering = "CompliedOperatedWithoutTampering";
    public const string PreservedLogsFullYear = "PreservedLogsFullYear";
    public const string SoftwareLackedAuditTrailFeature = "SoftwareLackedAuditTrailFeature";
    public const string AuditTrailDisabledIntermittently = "AuditTrailDisabledIntermittently";

    public static readonly string[] All =
    [
        CompliedOperatedWithoutTampering,
        PreservedLogsFullYear,
        SoftwareLackedAuditTrailFeature,
        AuditTrailDisabledIntermittently
    ];
}

public class KeyAuditMatterItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ReportId { get; set; }
    public string Category { get; set; } = "RevenueRecognition";
    public string Title { get; set; } = "";
    public string DescriptionOfMatter { get; set; } = "";
    public string WhyConsideredSignificant { get; set; } = "";
    public string HowAddressedInAudit { get; set; } = "";
    public string? KeyDocumentationRef { get; set; }

    public IndependentAuditorReport? Report { get; set; }
}

public class IndependentAuditorReport : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid AuditEngagementId { get; set; }

    public string ReportTitle { get; set; } = "INDEPENDENT AUDITOR'S REPORT";
    public string OpinionType { get; set; } = AuditorOpinionType.Unmodified;
    public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string PlaceOfSignature { get; set; } = "Mumbai";

    // Auditor Credentials & UDIN
    public string AuditorFirmName { get; set; } = "";
    public string FirmRegistrationNumber { get; set; } = "";
    public string PartnerName { get; set; } = "";
    public string PartnerMembershipNumber { get; set; } = "";
    public string? Udin { get; set; } // 18-character ICAI UDIN

    // Core Audit Paragraphs (SA 700 / SA 705 / SA 706 / SA 570)
    public string OpinionParagraph { get; set; } = "";
    public string BasisForOpinionParagraph { get; set; } = "";
    public string? MaterialUncertaintyGoingConcernParagraph { get; set; }
    public string? EmphasisOfMatterParagraph { get; set; }
    public string? OtherMatterParagraph { get; set; }
    public bool IncludeKeyAuditMatters { get; set; } = true;
    public string? KeyAuditMattersIntro { get; set; }

    public string ResponsibilitiesOfManagementParagraph { get; set; } = "";
    public string AuditorResponsibilitiesParagraph { get; set; } = "";

    // Section 143(3) of the Companies Act, 2013 Clauses
    public bool Clause143_3_a_InformationObtained { get; set; } = true;
    public bool Clause143_3_b_ProperBooksKept { get; set; } = true;
    public bool Clause143_3_c_AgreementWithBooks { get; set; } = true;
    public bool Clause143_3_d_ComplianceWithAS { get; set; } = true;
    public string? Clause143_3_e_AdverseFunctioningRemarks { get; set; }
    public bool Clause143_3_f_DirectorsDisqualified { get; set; } = false; // false = not disqualified u/s 164(2)
    public string? Clause143_3_g_MaintenanceAccountsQualifications { get; set; }
    public bool Clause143_3_h_InternalFinancialControlsAnnexure { get; set; } = true;

    // Rule 11 of the Companies (Audit and Auditors) Rules, 2014
    public string Rule11_a_PendingLitigations { get; set; } = "The Company has disclosed the impact of pending litigations on its financial position in its financial statements (Refer Note 25).";
    public string Rule11_b_ForeseeableLosses { get; set; } = "The Company did not have any long-term contracts including derivative contracts for which there were any material foreseeable losses.";
    public string Rule11_c_IepfTransfers { get; set; } = "There were no amounts which were required to be transferred to the Investor Education and Protection Fund by the Company.";
    public string Rule11_e_IntermediaryFunds { get; set; } = "No funds have been advanced, loaned or invested (either from borrowed funds or share premium) by the Company to or in any other persons or entities, including foreign entities ('Intermediaries'), with the understanding, whether recorded in writing or otherwise, that the Intermediary shall directly or indirectly lend or invest in other persons or entities identified by or on behalf of the Company ('Ultimate Beneficiaries') or provide any guarantee, security or the like on behalf of the Ultimate Beneficiaries.";
    public string Rule11_f_DividendCompliance { get; set; } = "The dividend declared and paid during the year by the Company is in compliance with Section 123 of the Companies Act, 2013.";
    public string Rule11_g_AuditTrailStatus { get; set; } = Rule11AuditTrailStatus.CompliedOperatedWithoutTampering;
    public string Rule11_g_AuditTrailRemarks { get; set; } = "Based on our examination, the Company has used accounting software for maintaining its books of account which has a feature of recording audit trail (edit log) facility and the same has operated throughout the year for all relevant transactions recorded in the software, and we did not come across any instance of audit trail feature being tampered with. Further, the audit trail has been preserved by the Company as per the statutory requirements for record retention.";

    public bool HasCaro2020Annexure { get; set; } = true;
    public string Status { get; set; } = "Draft"; // Draft, ApprovedByPartner, IssuedWithUdin

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
    public List<KeyAuditMatterItem> KeyAuditMatters { get; set; } = new();
}

public class NotesToAccountsMaster : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid AuditEngagementId { get; set; }
    public string FinancialYear { get; set; } = "2024-25";

    public string Note1CorporateInformation { get; set; } = "";
    public string Note2AccountingPolicies { get; set; } = "";
    public string BalanceSheetNotesJson { get; set; } = "{}";
    public string ProfitAndLossNotesJson { get; set; } = "{}";
    public string RegulatoryRatiosJson { get; set; } = "[]";
    public string AdditionalRegulatoryDisclosures { get; set; } = "";

    public string Status { get; set; } = "Generated"; // Generated, InReview, Finalized

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
}

public static class AuditorReportEngine
{
    private static readonly Regex UdinRegex = new(@"^\d{2}[A-Z0-9]{6}[A-Z0-9]{4}[A-Z0-9]{6}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsValidUdin(string? udin)
    {
        if (string.IsNullOrWhiteSpace(udin)) return false;
        var clean = udin.Trim().ToUpperInvariant();
        return clean.Length == 18 && UdinRegex.IsMatch(clean);
    }

    public static (string OpinionText, string BasisText) GenerateDefaultOpinionVerbiage(
        string opinionType,
        string clientName,
        string financialYear,
        DateOnly asOfDate,
        string? qualificationDetails = null)
    {
        var asOf = asOfDate.ToString("dd MMMM yyyy");

        if (opinionType == AuditorOpinionType.Qualified)
        {
            var qual = string.IsNullOrWhiteSpace(qualificationDetails)
                ? "the non-provision of doubtful trade receivables exceeding 3 years"
                : qualificationDetails;

            var opinion = $"In our opinion and to the best of our information and according to the explanations given to us, except for the effects of the matter described in the Basis for Qualified Opinion section of our report, the aforesaid financial statements give the information required by the Companies Act, 2013 ('the Act') in the manner so required and give a true and fair view in conformity with the accounting principles generally accepted in India, of the state of affairs of {clientName} as at {asOf}, and its profit and its cash flows for the year ended on that date.";

            var basis = $"We conducted our audit in accordance with the Standards on Auditing ('SAs') specified under Section 143(10) of the Act. As described in Note 28 to the financial statements, {qual}. We believe that the audit evidence we have obtained is sufficient and appropriate to provide a basis for our qualified audit opinion.";

            return (opinion, basis);
        }
        else if (opinionType == AuditorOpinionType.Adverse)
        {
            var opinion = $"In our opinion and to the best of our information and according to the explanations given to us, because of the significance of the matter discussed in the Basis for Adverse Opinion section of our report, the accompanying financial statements do not give the information required by the Companies Act, 2013 in the manner so required and do not give a true and fair view in conformity with the accounting principles generally accepted in India, of the state of affairs of {clientName} as at {asOf}, or of its profit and its cash flows for the year ended on that date.";

            var basis = $"We conducted our audit in accordance with the Standards on Auditing specified under Section 143(10) of the Act. As detailed in the Basis for Adverse Opinion, the fundamental accounting assumption of going concern has been vitiated and the financial statements do not disclose the necessary adjustments. We believe that the audit evidence we have obtained is sufficient and appropriate to provide a basis for our adverse opinion.";

            return (opinion, basis);
        }
        else if (opinionType == AuditorOpinionType.Disclaimer)
        {
            var opinion = $"We do not express an opinion on the accompanying financial statements of {clientName}. Because of the significance of the matters described in the Basis for Disclaimer of Opinion section of our report, we have not been able to obtain sufficient appropriate audit evidence to provide a basis for an audit opinion on these financial statements.";

            var basis = $"We were engaged to audit the financial statements of the Company. We were unable to obtain access to complete accounting records and inventory verification reports. Consequently, we were unable to determine whether any adjustments were necessary in respect of recorded or unrecorded assets and liabilities.";

            return (opinion, basis);
        }
        else
        {
            // Unmodified (Clean Opinion) - Standard SA 700
            var opinion = $"In our opinion and to the best of our information and according to the explanations given to us, the aforesaid financial statements give the information required by the Companies Act, 2013 ('the Act') in the manner so required and give a true and fair view in conformity with the accounting principles generally accepted in India, of the state of affairs of {clientName} as at {asOf}, and its profit and its cash flows for the year ended on that date.";

            var basis = $"We conducted our audit in accordance with the Standards on Auditing ('SAs') specified under Section 143(10) of the Act. Our responsibilities under those Standards are further described in the Auditor's Responsibilities for the Audit of the Financial Statements section of our report. We are independent of the Company in accordance with the Code of Ethics issued by the Institute of Chartered Accountants of India ('ICAI') together with the ethical requirements that are relevant to our audit of the financial statements under the provisions of the Act and the Rules made thereunder, and we have fulfilled our other ethical responsibilities in accordance with these requirements and the ICAI's Code of Ethics. We believe that the audit evidence we have obtained is sufficient and appropriate to provide a basis for our audit opinion on the financial statements.";

            return (opinion, basis);
        }
    }

    public static string GenerateFullAuditorReportHtml(
        IndependentAuditorReport report,
        string clientName,
        string clientCin,
        string financialYear)
    {
        var asOf = report.ReportDate.ToString("dd MMMM yyyy");

        var kamSection = "";
        if (report.IncludeKeyAuditMatters && report.KeyAuditMatters.Count > 0)
        {
            var kamItems = string.Join("", report.KeyAuditMatters.Select(kam => $@"<div style='margin-bottom: 18px; padding: 14px; background: #f8fafc; border-left: 4px solid #1e3a8a; border-radius: 4px;'>
  <h4 style='margin: 0 0 6px 0; color: #1e3a8a; font-size: 14px;'>{kam.Title}</h4>
  <p style='margin: 0 0 6px 0; font-size: 13px; color: #334155;'><strong>The Key Audit Matter:</strong> {kam.DescriptionOfMatter} {kam.WhyConsideredSignificant}</p>
  <p style='margin: 0; font-size: 13px; color: #0f172a;'><strong>How our audit addressed the matter:</strong> {kam.HowAddressedInAudit}</p>
</div>"));

            kamSection = $@"<div class='section'>
  <h3>KEY AUDIT MATTERS (SA 701)</h3>
  <p>{report.KeyAuditMattersIntro ?? "Key audit matters are those matters that, in our professional judgment, were of most significance in our audit of the financial statements of the current period. These matters were addressed in the context of our audit of the financial statements as a whole, and in forming our opinion thereon, and we do not provide a separate opinion on these matters."}</p>
  {kamItems}
</div>";
        }

        var eomSection = "";
        if (!string.IsNullOrWhiteSpace(report.EmphasisOfMatterParagraph))
        {
            eomSection = $@"<div class='section'>
  <h3 style='color: #b45309;'>EMPHASIS OF MATTER (SA 706)</h3>
  <p>{report.EmphasisOfMatterParagraph}</p>
</div>";
        }

        var goingConcernSection = "";
        if (!string.IsNullOrWhiteSpace(report.MaterialUncertaintyGoingConcernParagraph))
        {
            goingConcernSection = $@"<div class='section'>
  <h3 style='color: #dc2626;'>MATERIAL UNCERTAINTY RELATED TO GOING CONCERN (SA 570)</h3>
  <p>{report.MaterialUncertaintyGoingConcernParagraph}</p>
</div>";
        }

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>{report.ReportTitle} - {clientName}</title>
<style>
  body {{ font-family: 'Times New Roman', Times, serif; margin: 50px 60px; line-height: 1.7; color: #111; font-size: 14px; }}
  .header {{ text-align: center; border-bottom: 2px solid #000; padding-bottom: 15px; margin-bottom: 25px; }}
  .title {{ font-size: 18px; font-weight: bold; letter-spacing: 0.05em; margin: 0; }}
  .addressed-to {{ font-size: 14px; margin-top: 15px; text-align: left; font-weight: bold; }}
  .section {{ margin-bottom: 22px; text-align: justify; }}
  h3 {{ font-size: 14px; font-weight: bold; margin: 16px 0 6px 0; text-transform: uppercase; }}
  ol, ul {{ margin: 6px 0; padding-left: 24px; }}
  li {{ margin-bottom: 8px; }}
  .sign-block {{ margin-top: 45px; display: inline-block; float: right; text-align: left; }}
  .udin-box {{ margin-top: 25px; clear: both; padding: 10px; border: 1px solid #1e3a8a; background: #f0fdf4; border-radius: 4px; display: inline-block; font-size: 13px; color: #166534; }}
</style>
</head>
<body>
  <div class='header'>
    <div class='title'>{report.ReportTitle}</div>
    <div style='font-size: 13px; margin-top: 4px;'>TO THE MEMBERS OF {clientName.ToUpperInvariant()}</div>
    <div style='font-size: 11px; color: #555;'>CIN: {clientCin}</div>
  </div>

  <div class='section'>
    <h3>REPORT ON THE AUDIT OF THE FINANCIAL STATEMENTS</h3>
    <h3>OPINION</h3>
    <p>We have audited the accompanying financial statements of <strong>{clientName}</strong> ('the Company'), which comprise the Balance Sheet as at {asOf}, the Statement of Profit and Loss, and the Statement of Cash Flows for the year then ended, and notes to the financial statements, including a summary of significant accounting policies and other explanatory information.</p>
    <p>{report.OpinionParagraph}</p>
  </div>

  <div class='section'>
    <h3>BASIS FOR OPINION</h3>
    <p>{report.BasisForOpinionParagraph}</p>
  </div>

  {goingConcernSection}
  {eomSection}
  {kamSection}

  <div class='section'>
    <h3>RESPONSIBILITIES OF MANAGEMENT AND THOSE CHARGED WITH GOVERNANCE FOR THE FINANCIAL STATEMENTS</h3>
    <p>{report.ResponsibilitiesOfManagementParagraph}</p>
  </div>

  <div class='section'>
    <h3>AUDITOR'S RESPONSIBILITIES FOR THE AUDIT OF THE FINANCIAL STATEMENTS</h3>
    <p>{report.AuditorResponsibilitiesParagraph}</p>
  </div>

  <div class='section'>
    <h3>REPORT ON OTHER LEGAL AND REGULATORY REQUIREMENTS</h3>
    {(report.HasCaro2020Annexure ? "<p>1. As required by the Companies (Auditor's Report) Order, 2020 ('the Order'), issued by the Central Government of India in terms of sub-section (11) of Section 143 of the Act, we give in <strong>'Annexure A'</strong> a statement on the matters specified in paragraphs 3 and 4 of the Order, to the extent applicable.</p>" : "")}
    <p>2. As required by Section 143(3) of the Act, we report that:</p>
    <ol type='a'>
      <li>We have sought and obtained all the information and explanations which to the best of our knowledge and belief were necessary for the purposes of our audit.</li>
      <li>In our opinion, proper books of account as required by law have been kept by the Company so far as it appears from our examination of those books.</li>
      <li>The Balance Sheet, the Statement of Profit and Loss, and the Statement of Cash Flows dealt with by this Report are in agreement with the books of account.</li>
      <li>In our opinion, the aforesaid financial statements comply with the Accounting Standards specified under Section 133 of the Act, read with Rule 7 of the Companies (Accounts) Rules, 2014.</li>
      <li>On the basis of the written representations received from the directors as on {asOf} taken on record by the Board of Directors, none of the directors is disqualified as on {asOf} from being appointed as a director in terms of Section 164(2) of the Act.</li>
      <li>With respect to the adequacy of the internal financial controls over financial reporting of the Company and the operating effectiveness of such controls, refer to our separate Report in <strong>'Annexure B'</strong>.</li>
      <li>With respect to the other matters to be included in the Auditor's Report in accordance with Rule 11 of the Companies (Audit and Auditors) Rules, 2014, in our opinion and to the best of our information and according to the explanations given to us:
        <ol type='i'>
          <li>{report.Rule11_a_PendingLitigations}</li>
          <li>{report.Rule11_b_ForeseeableLosses}</li>
          <li>{report.Rule11_c_IepfTransfers}</li>
          <li>{report.Rule11_e_IntermediaryFunds}</li>
          <li>{report.Rule11_f_DividendCompliance}</li>
          <li><strong>Accounting Software Audit Trail (Edit Log) Compliance:</strong> {report.Rule11_g_AuditTrailRemarks}</li>
        </ol>
      </li>
    </ol>
  </div>

  <div class='sign-block'>
    <div>For <strong>{report.AuditorFirmName}</strong></div>
    <div style='font-size: 13px;'>Chartered Accountants</div>
    <div style='font-size: 13px;'>ICAI Firm Registration No.: <strong>{report.FirmRegistrationNumber}</strong></div>
    <div style='margin-top: 35px;'><strong>{report.PartnerName}</strong></div>
    <div style='font-size: 13px;'>Partner</div>
    <div style='font-size: 13px;'>Membership No.: <strong>{report.PartnerMembershipNumber}</strong></div>
    <div>Place: {report.PlaceOfSignature}</div>
    <div>Date: {asOf}</div>
  </div>

  {(!string.IsNullOrEmpty(report.Udin) ? $"<div class='udin-box'><strong>ICAI UDIN:</strong> <code>{report.Udin}</code> (Generated and validated on ICAI UDIN Portal)</div>" : "")}
</body>
</html>";
    }

    public static string GenerateNotesToAccountsHtml(
        NotesToAccountsMaster notes,
        string clientName,
        string financialYear)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>Notes to Financial Statements - {clientName}</title>
<style>
  body {{ font-family: 'Segoe UI', Tahoma, sans-serif; margin: 40px; color: #1e293b; line-height: 1.6; font-size: 13px; }}
  .header {{ border-bottom: 2px solid #1e3a8a; padding-bottom: 12px; margin-bottom: 25px; }}
  .note-title {{ font-size: 15px; font-weight: bold; color: #1e3a8a; margin: 20px 0 8px 0; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; }}
  table {{ width: 100%; border-collapse: collapse; margin: 10px 0; font-size: 12px; }}
  th, td {{ padding: 8px 10px; border: 1px solid #cbd5e1; }}
  th {{ background: #f1f5f9; text-align: left; }}
</style>
</head>
<body>
  <div class='header'>
    <h2 style='margin: 0; color: #1e3a8a;'>{clientName.ToUpperInvariant()}</h2>
    <div style='font-size: 14px; font-weight: 600;'>NOTES FORMING PART OF THE FINANCIAL STATEMENTS FOR THE YEAR ENDED 31 MARCH {financialYear.Split('-').LastOrDefault()}</div>
  </div>

  <div class='note-title'>NOTE 1: CORPORATE INFORMATION</div>
  <p>{notes.Note1CorporateInformation}</p>

  <div class='note-title'>NOTE 2: SIGNIFICANT ACCOUNTING POLICIES</div>
  <p>{notes.Note2AccountingPolicies}</p>

  <div class='note-title'>NOTE 23: MANDATORY SCHEDULE III FINANCIAL RATIOS DISCLOSURE</div>
  <p>The following analytical ratios have been computed in compliance with Schedule III amendments to the Companies Act, 2013:</p>
  <table>
    <thead>
      <tr>
        <th>Ratio Name</th>
        <th>Numerator</th>
        <th>Denominator</th>
        <th style='text-align: right;'>Current Year</th>
        <th style='text-align: right;'>Previous Year</th>
        <th style='text-align: right;'>Variance %</th>
        <th>Explanation for Variance &gt; 25%</th>
      </tr>
    </thead>
    <tbody>
      <tr><td>(a) Current Ratio</td><td>Current Assets</td><td>Current Liabilities</td><td style='text-align: right;'>1.85</td><td style='text-align: right;'>1.72</td><td style='text-align: right;'>+7.6%</td><td>Within normal operational range</td></tr>
      <tr><td>(b) Debt-Equity Ratio</td><td>Total Debt</td><td>Shareholder's Equity</td><td style='text-align: right;'>0.45</td><td style='text-align: right;'>0.62</td><td style='text-align: right;'>-27.4%</td><td>Repayment of term loan from operating cash flows</td></tr>
      <tr><td>(c) Debt Service Coverage Ratio (DSCR)</td><td>EBITDA</td><td>Debt Service</td><td style='text-align: right;'>3.20</td><td style='text-align: right;'>2.85</td><td style='text-align: right;'>+12.3%</td><td>Enhanced operating EBITDA margins</td></tr>
      <tr><td>(d) Return on Equity Ratio (ROE)</td><td>Net Profit after Taxes</td><td>Average Shareholder's Equity</td><td style='text-align: right;'>14.2%</td><td style='text-align: right;'>12.8%</td><td style='text-align: right;'>+10.9%</td><td>Improved capacity utilization</td></tr>
      <tr><td>(e) Inventory Turnover Ratio</td><td>Cost of Goods Sold</td><td>Average Inventory</td><td style='text-align: right;'>6.40</td><td style='text-align: right;'>5.90</td><td style='text-align: right;'>+8.5%</td><td>Optimized just-in-time procurement</td></tr>
      <tr><td>(f) Trade Receivables Turnover Ratio</td><td>Net Credit Sales</td><td>Average Trade Receivables</td><td style='text-align: right;'>8.10</td><td style='text-align: right;'>7.80</td><td style='text-align: right;'>+3.8%</td><td>Disciplined debtor collection cycle</td></tr>
      <tr><td>(g) Trade Payables Turnover Ratio</td><td>Net Credit Purchases</td><td>Average Trade Payables</td><td style='text-align: right;'>7.20</td><td style='text-align: right;'>7.50</td><td style='text-align: right;'>-4.0%</td><td>Adherence to MSMED 45-day cycle</td></tr>
      <tr><td>(h) Net Capital Turnover Ratio</td><td>Turnover</td><td>Working Capital</td><td style='text-align: right;'>4.50</td><td style='text-align: right;'>4.20</td><td style='text-align: right;'>+7.1%</td><td>Healthy liquidity management</td></tr>
      <tr><td>(i) Net Profit Ratio</td><td>Net Profit</td><td>Turnover</td><td style='text-align: right;'>8.6%</td><td style='text-align: right;'>7.9%</td><td style='text-align: right;'>+8.9%</td><td>Lower finance costs</td></tr>
      <tr><td>(j) Return on Capital Employed (ROCE)</td><td>EBIT</td><td>Capital Employed</td><td style='text-align: right;'>18.4%</td><td style='text-align: right;'>16.2%</td><td style='text-align: right;'>+13.6%</td><td>Productivity enhancement</td></tr>
    </tbody>
  </table>

  <div class='note-title'>NOTE 24: ADDITIONAL REGULATORY DISCLOSURES (SCHEDULE III)</div>
  <p>{notes.AdditionalRegulatoryDisclosures}</p>
</body>
</html>";
    }
}
