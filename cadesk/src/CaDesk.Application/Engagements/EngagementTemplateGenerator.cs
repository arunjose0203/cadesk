using CaDesk.Domain.Engagements;

namespace CaDesk.Application.Engagements;

public static class EngagementTemplateGenerator
{
    public static DateOnly CalculateNextKycReviewDueDate(string riskRating, DateOnly reviewDate)
    {
        return riskRating switch
        {
            KycRiskRating.High or KycRiskRating.PoliticallyExposedPerson => reviewDate.AddYears(1),
            KycRiskRating.Medium => reviewDate.AddYears(2),
            _ => reviewDate.AddYears(3), // Low Risk: 36 months
        };
    }

    public static GeneratedEngagementTemplate GenerateTemplate(GenerateTemplateRequest req)
    {
        var type = req.EngagementType ?? EngagementType.StatutoryAudit;
        var clientName = string.IsNullOrWhiteSpace(req.ClientName) ? "The Client" : req.ClientName.Trim();
        var fy = string.IsNullOrWhiteSpace(req.FinancialYear) ? "2024-25" : req.FinancialYear.Trim();
        var fee = req.AgreedFeeAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        var feeStructure = req.FeeStructure ?? EngagementFeeStructure.FixedFee;

        string scope;
        string mgmt;
        string auditor;

        switch (type)
        {
            case EngagementType.TaxAudit:
                scope = $"Audit of statement of particulars in Form 3CD and issuance of audit report in Form 3CA / 3CB pursuant to Section 44AB of the Income Tax Act, 1961 for Financial Year {fy}.";
                mgmt = "Maintenance of comprehensive books of account, vouchers, depreciation schedules, quantitative registers, and timely furnishing of Form 3CD particulars along with true and correct financial statements.";
                auditor = "Conducting examination in accordance with ICAI Guidance Note on Tax Audit u/s 44AB, expressing an opinion on true and fair view of accounts, and verifying statutory tax compliance.";
                break;

            case EngagementType.GstAudit:
                scope = $"Reconciliation of annual GST returns (GSTR-9) and compilation of reconciliation statement in GSTR-9C under Section 44 of the CGST/SGST Act, 2017 for Financial Year {fy}.";
                mgmt = "Reconciling outward supplies, inward supplies, eligible ITC availed with GSTR-2B, and maintaining state-wise audited trial balances.";
                auditor = "Testing reconciliation between audited financial statements and annual GST returns, verifying reverse charge liabilities, and reporting un-reconciled discrepancies.";
                break;

            case EngagementType.DirectTaxFiling:
                scope = $"Preparation, computational review, and electronic filing of Corporate / Individual Income Tax Return (ITR-6 / ITR-7 / ITR-5) for Assessment Year corresponding to FY {fy}.";
                mgmt = "Full disclosure of all sources of income, capital gains, foreign assets, AIS/TIS items, and timely approval of draft computation before filing.";
                auditor = "Computing taxable income under applicable tax regime, claiming statutory deductions under Chapter VI-A, and transmitting electronic ITR verification.";
                break;

            case EngagementType.InternalAudit:
                scope = $"Risk-based internal audit of internal financial controls, standard operating procedures (SOPs), procurement-to-pay, and order-to-cash cycles for FY {fy}.";
                mgmt = "Facilitating unhindered access to Enterprise Resource Planning (ERP) databases, operating personnel, and implementing agreed corrective audit recommendations.";
                auditor = "Evaluating operating effectiveness of internal controls, identifying fraud risk red flags, and submitting quarterly actionable executive governance reports.";
                break;

            default: // Statutory Audit
                scope = $"Audit of standalone and consolidated financial statements of {clientName} under Section 139 of the Companies Act, 2013 and applicable Indian Accounting Standards (Ind AS / AS) for FY {fy}.";
                mgmt = "Preparation of financial statements ensuring true and fair view, selection and application of accounting policies, safeguarding of assets, and maintenance of adequate internal controls.";
                auditor = "Conducting audit in accordance with ICAI Standards on Auditing (SA 200 to SA 705) to obtain reasonable assurance whether financial statements are free of material misstatement whether due to fraud or error.";
                break;
        }

        var fullContent = $"""
# ENGAGEMENT LETTER FOR {type.ToUpperInvariant()}
**Financial Year:** {fy}  
**Addressed to:** The Board of Directors / Management, {clientName}  
**Issued by:** Chartered Accountants (Registered with ICAI)

Dear Sirs / Mesdames,

We are writing to confirm our understanding of the terms and objectives of our engagement and the nature and limitations of the services we will provide in accordance with **ICAI Standard on Auditing SA 210 - Agreeing the Terms of Audit Engagements** and **SQC 1**.

### 1. Scope of the Engagement
{scope}

### 2. Responsibilities of Management & Those Charged with Governance
{mgmt}

Management acknowledges and understands that it has responsibility:
- For the preparation and fair presentation of the financial statements in accordance with the applicable financial reporting framework;
- For such internal control as management determines is necessary to enable the preparation of financial statements that are free from material misstatement;
- To provide us with unrestricted access to all information, records, documentation, and personnel.

### 3. Responsibilities of the Auditor
{auditor}

Because of the inherent limitations of an audit, together with the inherent limitations of internal control, there is an unavoidable risk that some material misstatements may not be detected, even though the audit is properly planned and performed in accordance with SAs.

### 4. Fee Arrangement & Commercial Terms
- **Structure:** {feeStructure}
- **Agreed Fee:** ₹{fee} (exclusive of applicable Goods & Services Tax)
- **Out of Pocket Expenses:** Reimbursement of actual conveyance, traveling, and government portal statutory fees shall be charged separately.

### 5. Dispute Resolution & Governing Law
This engagement shall be governed by and construed in accordance with the laws of India, under the professional disciplinary jurisdiction of the Institute of Chartered Accountants of India (ICAI).

Please sign and return the attached copy of this letter to indicate your acknowledgment of, and agreement with, the arrangements for our audit of the financial statements.

Yours faithfully,  
**For & on behalf of Chartered Accountants**  
(Partner / Authorized Signatory)  

---
### Acknowledgment and Acceptance by Client
I / We confirm our agreement to the terms stated above on behalf of **{clientName}**.

*Digital Acceptance Tracking active via CA Desk Client Portal.*
""";

        return new GeneratedEngagementTemplate(
            EngagementType: type,
            DefaultScope: scope,
            DefaultManagementResponsibilities: mgmt,
            DefaultAuditorResponsibilities: auditor,
            FullLetterContent: fullContent
        );
    }
}
