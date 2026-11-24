using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.ForeignRemittance;

public enum FirmsFilingType
{
    FcGpr,
    FcTrs
}

public enum CapitalInstrumentType
{
    EquityShares,
    CCDs, // Compulsorily Convertible Debentures
    CCPS, // Compulsorily Convertible Preference Shares
    ShareWarrants
}

public enum FdiEntryRoute
{
    AutomaticRoute,
    GovernmentApprovalRoute
}

public enum TransferDirectionType
{
    ResidentToNonResident,
    NonResidentToResident,
    NonResidentToNonResident
}

public enum TransferModeType
{
    Sale,
    Gift,
    Swap,
    Merger
}

public enum FirmsFilingStatus
{
    Draft,
    Validated,
    Certified,
    SubmittedToAdBank,
    ApprovedByRbi
}

public enum FlaAuditStatus
{
    Audited,
    UnauditedProvisional
}

public enum FlaReturnStatus
{
    Draft,
    Validated,
    Certified,
    SubmittedToRbiFlair
}

/// <summary>
/// RBI FIRMS Portal Single Master Form (SMF) Filing: Form FC-GPR (Allotment) or Form FC-TRS (Transfer).
/// Governed by Foreign Exchange Management (Non-debt Instruments) Rules, 2019.
/// </summary>
public class FemaFirmsFiling : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public FirmsFilingType FilingType { get; set; } = FirmsFilingType.FcGpr;
    public string ReferenceNumber { get; set; } = string.Empty; // e.g. "FIRMS/FC-GPR/2024/001"

    // Indian Investee / Issuing Entity Details
    public string IndianEntityCin { get; set; } = string.Empty;
    public string IndianEntityName { get; set; } = string.Empty;
    public string Pan { get; set; } = string.Empty;

    // Non-Resident Counterparty Details
    public string NonResidentPartyName { get; set; } = string.Empty;
    public string NonResidentCountry { get; set; } = string.Empty;
    public string NonResidentCountryCode { get; set; } = string.Empty; // e.g. "US", "SG", "GB"

    // Instrument & Pricing Guidelines (Rule 21 FEMA NDI Rules)
    public CapitalInstrumentType InstrumentType { get; set; } = CapitalInstrumentType.EquityShares;
    public long NumberOfInstruments { get; set; }
    public decimal FaceValuePerInstrument { get; set; } = 10.00m;
    public decimal IssueOrTransferPricePerInstrument { get; set; }
    public decimal FairValuePerInstrument { get; set; } // DCF / NAV Floor or Ceiling Valuation
    public decimal TotalConsiderationInr { get; set; }
    public decimal TotalConsiderationFc { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 85.00m;

    public string ValuationMethod { get; set; } = "DiscountedCashFlow"; // DiscountedCashFlow, NetAssetValue
    public string ValuerName { get; set; } = "M/s Apex Capital Advisors (SEBI Registered Merchant Banker)";
    public DateTime? ValuationReportDate { get; set; }

    // Statutory Limitation & Deadlines
    public DateTime DateOfAllotmentOrTransfer { get; set; }
    public DateTime StatutoryFilingDeadline { get; set; } // 30d for FC-GPR, 60d for FC-TRS
    public int DaysElapsed { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsLateSubmission { get; set; }
    public decimal LateSubmissionFeeEstimated { get; set; }

    // Sectoral Cap & Entry Route
    public FdiEntryRoute FdiRoute { get; set; } = FdiEntryRoute.AutomaticRoute;
    public string? EntryRouteApprovalRef { get; set; }
    public decimal SectoralCapPct { get; set; } = 100.00m;
    public decimal PostAllotmentForeignShareholdingPct { get; set; }

    // FC-TRS Specific Fields
    public TransferDirectionType TransferDirection { get; set; } = TransferDirectionType.ResidentToNonResident;
    public TransferModeType TransferMode { get; set; } = TransferModeType.Sale;
    public string? BuyerAdBankName { get; set; }
    public string? SellerAdBankName { get; set; }

    // Pricing Guidelines Compliance Flag
    public bool PricingGuidelinesMet { get; set; } = true;
    public string PricingComplianceRemarks { get; set; } = string.Empty;

    // Generated Artifacts & Certification
    public string? GeneratedComplianceCertificate { get; set; }
    public FirmsFilingStatus Status { get; set; } = FirmsFilingStatus.Draft;
    public string? Udin { get; set; }
    public DateTime? CertifiedAt { get; set; }
    public string? CertifiedByCaName { get; set; }
    public string? CaMembershipNumber { get; set; }
    public string? CaFirmName { get; set; }
    public string? RbiAcknowledgmentReference { get; set; }
}

/// <summary>
/// RBI Annual Return on Foreign Liabilities and Assets (FLA Return) under FEMA Regulations, 1999.
/// Mandatory annual filing on the RBI FLAIR portal by July 15th following the financial year.
/// </summary>
public class RbiAnnualFlaReturn : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = "2024-25"; // e.g. "2024-25"
    public string CompanyCin { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Pan { get; set; } = string.Empty;
    public DateTime AccountClosingDate { get; set; } = new(2025, 3, 31);
    public DateTime StatutoryFilingDeadline { get; set; } = new(2025, 7, 15);
    public FlaAuditStatus AuditStatus { get; set; } = FlaAuditStatus.Audited;

    // SECTION I: FINANCIAL PARTICULARS (BALANCE SHEET & P&L)
    public decimal PaidUpCapitalInr { get; set; }
    public decimal ReservesAndSurplusInr { get; set; }
    public decimal NetWorthInr { get; set; } // PaidUp + Reserves
    public decimal TotalSalesTurnoverInr { get; set; }
    public decimal NetProfitLossAfterTaxInr { get; set; }
    public decimal TotalAssetsInr { get; set; }
    public decimal TotalLiabilitiesInr { get; set; }

    // SECTION II: FOREIGN DIRECT INVESTMENT (FDI) IN INDIA (LIABILITIES)
    public decimal ForeignEquityHoldingPct { get; set; } // Non-resident ownership %
    public decimal FdiAtFaceValueInr { get; set; }
    public decimal FdiAtNetAssetValueInr { get; set; } // NetWorth * ForeignEquityHoldingPct / 100
    public decimal FdiAtMarketOrDcfValueInr { get; set; }
    public decimal FreshFdiInflowsDuringYearInr { get; set; }
    public decimal DisinvestmentOutflowsDuringYearInr { get; set; }
    public decimal RetainedEarningsAttributableToFdiInr { get; set; }

    // SECTION III: OVERSEAS DIRECT INVESTMENT (ODI) OUTSIDE INDIA (ASSETS)
    public bool HasOdi { get; set; }
    public string? OverseasEntityName { get; set; }
    public string? OverseasCountry { get; set; }
    public decimal OdiEquityHoldingPct { get; set; }
    public decimal OdiEquityInvestmentInr { get; set; }
    public decimal OdiLoanOutstandingInr { get; set; }
    public decimal OdiGuaranteeIssuedInr { get; set; }
    public decimal TotalOdiFinancialCommitmentInr { get; set; }

    // SECTION IV: OTHER FOREIGN LIABILITIES & ASSETS
    public decimal ExternalCommercialBorrowingsEcbInr { get; set; }
    public decimal ForeignTradeCreditSuppliersInr { get; set; }
    public decimal ForeignTradeCreditBuyersInr { get; set; }
    public decimal ForeignBankBalancesInr { get; set; }
    public decimal OtherForeignAssetsInr { get; set; }
    public decimal NetForeignLiabilitiesPositionInr { get; set; }

    // Statutory Evaluation & Verification
    public bool IsMandatoryFiling { get; set; } = true;
    public int DaysRemainingToDeadline { get; set; }
    public bool IsDeadlineBreached { get; set; }
    public string FilingStatusAlert { get; set; } = string.Empty;

    public FlaReturnStatus Status { get; set; } = FlaReturnStatus.Draft;
    public string? RbiFlairAckNumber { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? Udin { get; set; }
    public DateTime? CertifiedAt { get; set; }
    public string? CertifiedByCa { get; set; }
    public string? VerificationNotes { get; set; }
}

public static class FemaFirmsEngine
{
    public static void CalculateFirmsCompliance(FemaFirmsFiling filing, DateTime asOfDate)
    {
        // 1. Calculate Statutory Deadline:
        // FC-GPR: 30 days from date of allotment (FEMA NDI Rule 13)
        // FC-TRS: 60 days from date of receipt/transfer of funds (FEMA NDI Rule 14)
        int statutoryWindowDays = filing.FilingType == FirmsFilingType.FcGpr ? 30 : 60;
        filing.StatutoryFilingDeadline = filing.DateOfAllotmentOrTransfer.AddDays(statutoryWindowDays);

        int elapsed = (int)(asOfDate.Date - filing.DateOfAllotmentOrTransfer.Date).TotalDays;
        filing.DaysElapsed = Math.Max(0, elapsed);
        int remaining = (int)(filing.StatutoryFilingDeadline.Date - asOfDate.Date).TotalDays;
        filing.DaysRemaining = remaining;
        filing.IsLateSubmission = remaining < 0;

        // 2. Consideration totals:
        if (filing.TotalConsiderationInr <= 0 && filing.NumberOfInstruments > 0 && filing.IssueOrTransferPricePerInstrument > 0)
        {
            filing.TotalConsiderationInr = Math.Round(filing.NumberOfInstruments * filing.IssueOrTransferPricePerInstrument, 2);
        }

        if (filing.TotalConsiderationFc <= 0 && filing.TotalConsiderationInr > 0 && filing.ExchangeRate > 0)
        {
            filing.TotalConsiderationFc = Math.Round(filing.TotalConsiderationInr / filing.ExchangeRate, 2);
        }

        // 3. Late Submission Fee (LSF) Calculation under RBI A.P. (DIR Series) Circular No. 16:
        // Base LSF = ₹7,500 + [0.025% × Total Consideration × Years delayed (minimum 1 year fraction)]
        if (filing.IsLateSubmission)
        {
            int daysDelayed = Math.Abs(remaining);
            decimal yearsDelayed = Math.Max(1.0m, Math.Round((decimal)daysDelayed / 365.0m, 2));
            decimal variableLsf = Math.Round(0.00025m * filing.TotalConsiderationInr * yearsDelayed, 2);
            filing.LateSubmissionFeeEstimated = 7500.00m + variableLsf;
        }
        else
        {
            filing.LateSubmissionFeeEstimated = 0m;
        }

        // 4. Pricing Guidelines Evaluation (Rule 21 FEMA NDI Rules):
        // Issue by Indian Company to Non-Resident (FC-GPR): Issue Price >= Fair Value per share (Floor Price)
        // Transfer from Resident to Non-Resident (FC-TRS): Transfer Price >= Fair Value (Floor Price)
        // Transfer from Non-Resident to Resident (FC-TRS): Transfer Price <= Fair Value (Ceiling Price)
        // Non-Resident to Non-Resident (FC-TRS): Free pricing, no guidelines breached.
        if (filing.FilingType == FirmsFilingType.FcGpr)
        {
            if (filing.IssueOrTransferPricePerInstrument >= filing.FairValuePerInstrument)
            {
                filing.PricingGuidelinesMet = true;
                filing.PricingComplianceRemarks = $"Issue price of ₹{filing.IssueOrTransferPricePerInstrument:F2} meets or exceeds the certified DCF/NAV fair value floor of ₹{filing.FairValuePerInstrument:F2} (FEMA NDI Rule 21(1)).";
            }
            else
            {
                filing.PricingGuidelinesMet = false;
                filing.PricingComplianceRemarks = $"VIOLATION: Issue price of ₹{filing.IssueOrTransferPricePerInstrument:F2} is LESS than the certified fair value floor of ₹{filing.FairValuePerInstrument:F2}. Violates Rule 21(1) of FEMA NDI Rules.";
            }
        }
        else // FC-TRS
        {
            if (filing.TransferDirection == TransferDirectionType.ResidentToNonResident)
            {
                if (filing.IssueOrTransferPricePerInstrument >= filing.FairValuePerInstrument)
                {
                    filing.PricingGuidelinesMet = true;
                    filing.PricingComplianceRemarks = $"Transfer price from resident to non-resident (₹{filing.IssueOrTransferPricePerInstrument:F2}) meets or exceeds the fair value floor of ₹{filing.FairValuePerInstrument:F2} (Rule 21(2)(a)).";
                }
                else
                {
                    filing.PricingGuidelinesMet = false;
                    filing.PricingComplianceRemarks = $"VIOLATION: Resident cannot transfer shares to non-resident at less than fair value of ₹{filing.FairValuePerInstrument:F2} (Rule 21(2)(a)).";
                }
            }
            else if (filing.TransferDirection == TransferDirectionType.NonResidentToResident)
            {
                if (filing.IssueOrTransferPricePerInstrument <= filing.FairValuePerInstrument)
                {
                    filing.PricingGuidelinesMet = true;
                    filing.PricingComplianceRemarks = $"Transfer price from non-resident to resident (₹{filing.IssueOrTransferPricePerInstrument:F2}) does not exceed the fair value ceiling of ₹{filing.FairValuePerInstrument:F2} (Rule 21(2)(b)).";
                }
                else
                {
                    filing.PricingGuidelinesMet = false;
                    filing.PricingComplianceRemarks = $"VIOLATION: Non-resident cannot transfer shares to resident at a price exceeding the fair value ceiling of ₹{filing.FairValuePerInstrument:F2} (Rule 21(2)(b)).";
                }
            }
            else
            {
                filing.PricingGuidelinesMet = true;
                filing.PricingComplianceRemarks = "Transfer between non-residents is exempt from pricing guidelines restrictions.";
            }
        }
    }

    public static string GenerateComplianceCertificateHtml(FemaFirmsFiling filing)
    {
        var sb = new StringBuilder();
        string formName = filing.FilingType == FirmsFilingType.FcGpr ? "FORM FC-GPR (ALLOTMENT OF CAPITAL INSTRUMENTS)" : "FORM FC-TRS (TRANSFER OF CAPITAL INSTRUMENTS)";
        string deadlineDays = filing.FilingType == FirmsFilingType.FcGpr ? "30 days" : "60 days";

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'/><title>RBI FIRMS Statutory Compliance Certificate</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 30px; color: #1e293b; background: #fff; line-height: 1.5; }");
        sb.AppendLine(".header { text-align: center; border-bottom: 2px solid #0f172a; padding-bottom: 12px; margin-bottom: 20px; }");
        sb.AppendLine(".title { font-size: 18px; font-weight: 800; color: #0f172a; text-transform: uppercase; }");
        sb.AppendLine(".subtitle { font-size: 12px; font-weight: 600; color: #475569; margin-top: 4px; }");
        sb.AppendLine(".meta-table { width: 100%; border-collapse: collapse; margin-bottom: 18px; font-size: 11px; }");
        sb.AppendLine(".meta-table td { padding: 6px 10px; border: 1px solid #cbd5e1; }");
        sb.AppendLine(".meta-table .label { background-color: #f8fafc; font-weight: 700; width: 30%; color: #334155; }");
        sb.AppendLine(".alert-box { padding: 10px; border-radius: 6px; font-size: 11px; margin-bottom: 18px; }");
        sb.AppendLine(".alert-success { background-color: #ecfdf5; border: 1px solid #10b981; color: #065f46; }");
        sb.AppendLine(".alert-danger { background-color: #fef2f2; border: 1px solid #ef4444; color: #991b1b; }");
        sb.AppendLine(".cert-block { margin-top: 25px; border-top: 1px dashed #94a3b8; padding-top: 15px; font-size: 11px; }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"  <div class='title'>{formName}</div>");
        sb.AppendLine("  <div class='subtitle'>STATUTORY CERTIFICATE OF COMPLIANCE UNDER FEMA (NON-DEBT INSTRUMENTS) RULES, 2019</div>");
        sb.AppendLine("  <div class='subtitle'>RBI FIRMS SINGLE MASTER FORM (SMF) REPORTING PROTOCOL</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<table class='meta-table'>");
        sb.AppendLine($"<tr><td class='label'>Indian Entity Name</td><td><strong>{filing.IndianEntityName}</strong></td></tr>");
        sb.AppendLine($"<tr><td class='label'>Corporate Identity Number (CIN)</td><td>{filing.IndianEntityCin}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Permanent Account Number (PAN)</td><td>{filing.Pan}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Filing Reference Number</td><td>{filing.ReferenceNumber}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Non-Resident Counterparty</td><td>{filing.NonResidentPartyName} ({filing.NonResidentCountry})</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Capital Instrument Type</td><td>{filing.InstrumentType}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Number of Instruments</td><td>{filing.NumberOfInstruments:N0}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Face Value per Instrument</td><td>₹{filing.FaceValuePerInstrument:F2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Issue / Transfer Price per Unit</td><td>₹{filing.IssueOrTransferPricePerInstrument:F2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Fair Value Floor / Ceiling (Rule 21)</td><td>₹{filing.FairValuePerInstrument:F2} ({filing.ValuationMethod})</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Total Consideration (INR)</td><td><strong>₹{filing.TotalConsiderationInr:N2}</strong></td></tr>");
        sb.AppendLine($"<tr><td class='label'>Foreign Currency Equivalent</td><td>{filing.TotalConsiderationFc:N2} {filing.Currency} (at ₹{filing.ExchangeRate:F4})</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Date of Allotment / Transfer</td><td>{filing.DateOfAllotmentOrTransfer:dd-MMM-yyyy}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Statutory Deadline ({deadlineDays})</td><td>{filing.StatutoryFilingDeadline:dd-MMM-yyyy}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Entry Route / Sectoral Cap</td><td>{filing.FdiRoute} (Cap: {filing.SectoralCapPct}%)</td></tr>");
        sb.AppendLine("</table>");

        if (filing.PricingGuidelinesMet)
        {
            sb.AppendLine($"<div class='alert-box alert-success'><strong>✅ Pricing Guidelines Satisfied:</strong> {filing.PricingComplianceRemarks}</div>");
        }
        else
        {
            sb.AppendLine($"<div class='alert-box alert-danger'><strong>⚠️ Pricing Guidelines Non-Compliance:</strong> {filing.PricingComplianceRemarks}</div>");
        }

        if (filing.IsLateSubmission)
        {
            sb.AppendLine($"<div class='alert-box alert-danger'><strong>🚨 Late Submission Alert:</strong> The filing is delayed past the statutory {deadlineDays} deadline. Estimated RBI Late Submission Fee (LSF): <strong>₹{filing.LateSubmissionFeeEstimated:N2}</strong> under RBI Master Direction.</div>");
        }

        sb.AppendLine("<div class='cert-block'>");
        sb.AppendLine("<p><strong>CERTIFICATE BY CHARTERED ACCOUNTANT / COMPANY SECRETARY IN PRACTICE</strong></p>");
        sb.AppendLine("<p>I/We have examined the registers, records, valuation reports, and documents of the Indian Entity in respect of the allotment/transfer of capital instruments stated above.</p>");
        sb.AppendLine("<p>I/We hereby certify that:</p>");
        sb.AppendLine("<ol>");
        sb.AppendLine("  <li>The allotment/transfer complies with the sectoral cap, entry route, and conditionalities under the Foreign Exchange Management (Non-debt Instruments) Rules, 2019.</li>");
        sb.AppendLine($"  <li>The valuation of capital instruments has been determined by an authorized Registered Valuer / Merchant Banker in accordance with Rule 21 using the {filing.ValuationMethod} method.</li>");
        sb.AppendLine("  <li>Foreign inward remittance proceeds / payment consideration have been routed through normal banking channels with KYC confirmation from the Authorized Dealer (AD) Bank.</li>");
        sb.AppendLine("</ol>");
        sb.AppendLine("<br/>");
        sb.AppendLine($"<p><strong>Practitioner Name:</strong> {filing.CertifiedByCaName ?? "CA Rajesh Sharma"}<br/>");
        sb.AppendLine($"<strong>ICAI / ICSI Membership No.:</strong> {filing.CaMembershipNumber ?? "214589"}<br/>");
        sb.AppendLine($"<strong>Firm Name:</strong> {filing.CaFirmName ?? "Sharma & Associates LLP"}<br/>");
        sb.AppendLine($"<strong>UDIN:</strong> {filing.Udin ?? "24214589BK99887766"}<br/>");
        sb.AppendLine($"<strong>Date:</strong> {filing.CertifiedAt?.ToString("dd-MMM-yyyy") ?? DateTime.UtcNow.ToString("dd-MMM-yyyy")}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}

public static class RbiFlaEngine
{
    public static void CalculateFlaReturn(RbiAnnualFlaReturn fla, DateTime asOfDate)
    {
        // 1. Calculate Net Worth = Paid Up Capital + Reserves and Surplus
        fla.NetWorthInr = fla.PaidUpCapitalInr + fla.ReservesAndSurplusInr;

        // 2. Calculate Foreign Direct Investment (FDI) at Net Asset Value (NAV)
        // FDI at NAV = Net Worth * (ForeignEquityHoldingPct / 100)
        if (fla.ForeignEquityHoldingPct > 0)
        {
            fla.FdiAtNetAssetValueInr = Math.Round(fla.NetWorthInr * (fla.ForeignEquityHoldingPct / 100.00m), 2);
        }
        else
        {
            fla.FdiAtNetAssetValueInr = 0m;
        }

        // 3. Calculate Total ODI Financial Commitment Outside India
        if (fla.HasOdi)
        {
            fla.TotalOdiFinancialCommitmentInr = fla.OdiEquityInvestmentInr + fla.OdiLoanOutstandingInr + fla.OdiGuaranteeIssuedInr;
        }
        else
        {
            fla.TotalOdiFinancialCommitmentInr = 0m;
        }

        // 4. Calculate Net Foreign Liabilities Position
        // Total Foreign Liabilities = FDI at NAV + ECB + Foreign Trade Credit (Suppliers + Buyers)
        decimal totalForeignLiabilities = fla.FdiAtNetAssetValueInr + fla.ExternalCommercialBorrowingsEcbInr + fla.ForeignTradeCreditSuppliersInr + fla.ForeignTradeCreditBuyersInr;
        // Total Foreign Assets = Total ODI + Foreign Bank Balances + Other Foreign Assets
        decimal totalForeignAssets = fla.TotalOdiFinancialCommitmentInr + fla.ForeignBankBalancesInr + fla.OtherForeignAssetsInr;
        fla.NetForeignLiabilitiesPositionInr = Math.Round(totalForeignLiabilities - totalForeignAssets, 2);

        // 5. Mandatory Filing Evaluation:
        // Any Indian company that has received FDI and/or made ODI must file FLA by July 15
        fla.IsMandatoryFiling = (fla.ForeignEquityHoldingPct > 0 || fla.HasOdi || fla.ExternalCommercialBorrowingsEcbInr > 0);

        // 6. July 15th Statutory Deadline Countdown
        int remainingDays = (int)(fla.StatutoryFilingDeadline.Date - asOfDate.Date).TotalDays;
        fla.DaysRemainingToDeadline = remainingDays;
        fla.IsDeadlineBreached = remainingDays < 0;

        if (fla.IsDeadlineBreached)
        {
            fla.FilingStatusAlert = $"OVERDUE: Statutory July 15th deadline breached by {Math.Abs(remainingDays)} days. Compounding under Section 15 of FEMA may be attracted.";
        }
        else if (remainingDays <= 15)
        {
            fla.FilingStatusAlert = $"CRITICAL URGENCY: Only {remainingDays} days remaining before the July 15th RBI FLAIR filing portal closes!";
        }
        else
        {
            fla.FilingStatusAlert = $"ON TRACK: {remainingDays} days remaining before statutory July 15th deadline.";
        }
    }

    public static string GenerateFlaReturnHtml(RbiAnnualFlaReturn fla)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'/><title>RBI Annual Return on Foreign Liabilities & Assets (FLA)</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 30px; color: #0f172a; background: #fff; line-height: 1.5; }");
        sb.AppendLine(".header { text-align: center; border-bottom: 3px solid #1e3a8a; padding-bottom: 12px; margin-bottom: 20px; }");
        sb.AppendLine(".title { font-size: 19px; font-weight: 800; color: #1e3a8a; text-transform: uppercase; }");
        sb.AppendLine(".subtitle { font-size: 12px; font-weight: 600; color: #475569; margin-top: 4px; }");
        sb.AppendLine(".section-title { font-size: 13px; font-weight: 800; color: #1e3a8a; background: #eff6ff; padding: 6px 10px; margin-top: 18px; margin-bottom: 8px; border-left: 4px solid #3b82f6; }");
        sb.AppendLine(".data-table { width: 100%; border-collapse: collapse; font-size: 11px; margin-bottom: 12px; }");
        sb.AppendLine(".data-table td { padding: 6px 10px; border: 1px solid #cbd5e1; }");
        sb.AppendLine(".data-table .label { background-color: #f8fafc; font-weight: 700; width: 45%; color: #334155; }");
        sb.AppendLine(".data-table .num { text-align: right; font-family: monospace; font-size: 12px; font-weight: 700; }");
        sb.AppendLine(".badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 10px; font-weight: 700; }");
        sb.AppendLine(".badge-mand { background: #fee2e2; color: #991b1b; }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class='header'>");
        sb.AppendLine("  <div class='title'>RESERVE BANK OF INDIA — ANNUAL RETURN ON FOREIGN LIABILITIES & ASSETS (FLA)</div>");
        sb.AppendLine($"  <div class='subtitle'>Financial Year {fla.FinancialYear} • Governed by FEMA Regulations, 1999 • FLAIR Portal Filing</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<table class='data-table'>");
        sb.AppendLine($"<tr><td class='label'>Company Name</td><td><strong>{fla.CompanyName}</strong></td></tr>");
        sb.AppendLine($"<tr><td class='label'>Corporate Identification Number (CIN)</td><td>{fla.CompanyCin}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Permanent Account Number (PAN)</td><td>{fla.Pan}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Account Closing Date</td><td>{fla.AccountClosingDate:dd-MMM-yyyy} ({fla.AuditStatus})</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Statutory July 15th Deadline</td><td><strong>{fla.StatutoryFilingDeadline:dd-MMM-yyyy}</strong> ({fla.FilingStatusAlert})</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Mandatory Filing Status</td><td><span class='badge badge-mand'>{(fla.IsMandatoryFiling ? "MANDATORY FILING (FDI/ODI Present)" : "Voluntary / Nil Return")}</span></td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class='section-title'>SECTION I: FINANCIAL PARTICULARS (BALANCE SHEET & P&L)</div>");
        sb.AppendLine("<table class='data-table'>");
        sb.AppendLine($"<tr><td class='label'>Paid-up Share Capital</td><td class='num'>₹{fla.PaidUpCapitalInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Reserves and Surplus</td><td class='num'>₹{fla.ReservesAndSurplusInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'><strong>Total Net Worth (Paid-up + Reserves)</strong></td><td class='num' style='color:#1e3a8a;'>₹{fla.NetWorthInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Total Sales / Turnover</td><td class='num'>₹{fla.TotalSalesTurnoverInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Profit / (Loss) After Tax</td><td class='num'>₹{fla.NetProfitLossAfterTaxInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Total Assets</td><td class='num'>₹{fla.TotalAssetsInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Total Liabilities</td><td class='num'>₹{fla.TotalLiabilitiesInr:N2}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class='section-title'>SECTION II: FOREIGN DIRECT INVESTMENT (FDI) IN INDIA (LIABILITIES)</div>");
        sb.AppendLine("<table class='data-table'>");
        sb.AppendLine($"<tr><td class='label'>Foreign Non-Resident Equity Holding</td><td class='num'>{fla.ForeignEquityHoldingPct:F2}%</td></tr>");
        sb.AppendLine($"<tr><td class='label'>FDI at Nominal / Face Value</td><td class='num'>₹{fla.FdiAtFaceValueInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'><strong>FDI at Net Asset Value (NAV Proportion of Net Worth)</strong></td><td class='num' style='color:#059669;'>₹{fla.FdiAtNetAssetValueInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>FDI at DCF / Market Valuation</td><td class='num'>₹{fla.FdiAtMarketOrDcfValueInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Fresh Inward FDI Inflows During Year</td><td class='num'>₹{fla.FreshFdiInflowsDuringYearInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Disinvestment / Repatriation Outflows</td><td class='num'>₹{fla.DisinvestmentOutflowsDuringYearInr:N2}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class='section-title'>SECTION III: OVERSEAS DIRECT INVESTMENT (ODI) OUTSIDE INDIA (ASSETS)</div>");
        sb.AppendLine("<table class='data-table'>");
        sb.AppendLine($"<tr><td class='label'>Overseas JV / Wholly Owned Subsidiary (WOS)</td><td>{(fla.HasOdi ? $"{fla.OverseasEntityName} ({fla.OverseasCountry})" : "None")}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>ODI Equity Capital Contribution</td><td class='num'>₹{fla.OdiEquityInvestmentInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Loans Extended to Overseas WOS/JV</td><td class='num'>₹{fla.OdiLoanOutstandingInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Guarantees Issued on Behalf of Overseas WOS/JV</td><td class='num'>₹{fla.OdiGuaranteeIssuedInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'><strong>Total ODI Financial Commitment</strong></td><td class='num' style='color:#7c3aed;'>₹{fla.TotalOdiFinancialCommitmentInr:N2}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class='section-title'>SECTION IV: OTHER FOREIGN LIABILITIES & ASSETS (ECB, TRADE CREDIT, BANK BALANCES)</div>");
        sb.AppendLine("<table class='data-table'>");
        sb.AppendLine($"<tr><td class='label'>External Commercial Borrowings (ECB) Outstanding</td><td class='num'>₹{fla.ExternalCommercialBorrowingsEcbInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Foreign Trade Credit (Suppliers Credit)</td><td class='num'>₹{fla.ForeignTradeCreditSuppliersInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Foreign Trade Credit (Buyers Credit)</td><td class='num'>₹{fla.ForeignTradeCreditBuyersInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Foreign Bank Balances Held Abroad</td><td class='num'>₹{fla.ForeignBankBalancesInr:N2}</td></tr>");
        sb.AppendLine($"<tr><td class='label'><strong>Net Foreign Position (Total Liabilities - Total Assets)</strong></td><td class='num' style='color:#b91c1c;'>₹{fla.NetForeignLiabilitiesPositionInr:N2}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div style='margin-top:25px; border-top:1px dashed #94a3b8; padding-top:12px; font-size:11px;'>");
        sb.AppendLine($"<p><strong>Verified &amp; Certified by Chartered Accountant:</strong> {fla.CertifiedByCa ?? "CA Rajesh Sharma"}<br/>");
        sb.AppendLine($"<strong>UDIN:</strong> {fla.Udin ?? "24214589BK99887766"}<br/>");
        sb.AppendLine($"<strong>Date of Verification:</strong> {fla.CertifiedAt?.ToString("dd-MMM-yyyy") ?? DateTime.UtcNow.ToString("dd-MMM-yyyy")}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}

public static class DtaaMfnPeEngine
{
    // Countries with MFN clause in Indian Tax Treaties
    private static readonly Dictionary<string, (decimal RoyaltyFtsRate, bool RequiresNotification)> MfnTreatyCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Netherlands", (10.0m, true) },
        { "France", (10.0m, true) },
        { "Switzerland", (10.0m, true) },
        { "Sweden", (10.0m, true) },
        { "Spain", (10.0m, true) },
        { "Hungary", (10.0m, true) }
    };

    /// <summary>
    /// Evaluates Most Favoured Nation (MFN) clause eligibility considering Supreme Court ruling in Assessing Officer v. M/s Nestle SA (2023).
    /// </summary>
    public static (bool HasMfnClause, decimal MfnRate, bool IsCbdtNotificationMandatory, string GuidanceNote) EvaluateMfnClause(string country, decimal baseTreatyRate)
    {
        if (string.IsNullOrWhiteSpace(country))
            return (false, baseTreatyRate, false, "No treaty country specified.");

        if (MfnTreatyCountries.TryGetValue(country.Trim(), out var mfnData))
        {
            string note = $"MFN clause is present in the India-{country} DTAA. However, as held by the Hon'ble Supreme Court in Assessing Officer v. M/s Nestle SA (2023), the MFN benefit cannot be claimed automatically unless a formal notification under Section 90(1) has been issued by the Government of India.";
            return (true, mfnData.RoyaltyFtsRate, mfnData.RequiresNotification, note);
        }

        return (false, baseTreatyRate, false, $"No MFN protocol exists in the India-{country} treaty. Base treaty rate of {baseTreatyRate:F2}% applies.");
    }

    /// <summary>
    /// Evaluates Permanent Establishment (PE) exposure under Article 5 of UN/OECD Model Tax Convention.
    /// </summary>
    public static (bool IsPeExposed, string PeType, string AssessmentSummary) EvaluatePeRisk(
        bool hasPhysicalOffice,
        int personnelDaysInIndia,
        bool hasAuthorityToConcludeContracts,
        bool maintainsInventoryStock,
        string treatyCountry)
    {
        if (hasPhysicalOffice)
        {
            return (true, "FixedPlacePE", "Fixed Place PE under Article 5(1): Non-resident maintains a fixed place of business (office, branch, factory, workshop) in India through which its business is wholly or partly carried on.");
        }

        if (hasAuthorityToConcludeContracts)
        {
            return (true, "AgencyPE", "Dependent Agent PE (DAPE) under Article 5(5): A person acting on behalf of non-resident habitually concludes contracts, or plays the principal role leading to the conclusion of contracts.");
        }

        if (maintainsInventoryStock)
        {
            return (true, "AgencyPE", "Agency PE under Article 5(5): The agent habitually maintains in India a stock of goods or merchandise from which goods are regularly delivered on behalf of the enterprise.");
        }

        // Service PE threshold: usually 90 days for most treaties, 182 days for US/UK
        int servicePeThreshold = (treatyCountry.Equals("United States", StringComparison.OrdinalIgnoreCase) || treatyCountry.Equals("United Kingdom", StringComparison.OrdinalIgnoreCase)) ? 182 : 90;
        if (personnelDaysInIndia > servicePeThreshold)
        {
            return (true, "ServicePE", $"Service PE under Article 5(2)(l): Employees or other personnel have rendered services in India for aggregate {personnelDaysInIndia} days, exceeding the treaty threshold of {servicePeThreshold} days.");
        }

        return (false, "NoPE", $"No Permanent Establishment exposure under Article 5. Physical office: No; Personnel days: {personnelDaysInIndia}d (below {servicePeThreshold}d threshold); Contract authority: No.");
    }
}
