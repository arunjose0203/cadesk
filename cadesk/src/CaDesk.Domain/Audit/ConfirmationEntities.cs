using System.Security.Cryptography;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Audit;

public static class ConfirmationType
{
    public const string PositiveStatedBalance = "PositiveStatedBalance";
    public const string PositiveBlankBalance = "PositiveBlankBalance";
    public const string Negative = "Negative";

    public static readonly string[] All = [PositiveStatedBalance, PositiveBlankBalance, Negative];
}

public static class ConfirmationPartyType
{
    public const string TradeDebtor = "TradeDebtor";
    public const string TradeCreditor = "TradeCreditor";
    public const string Bank = "Bank";
    public const string LoanGiven = "LoanGiven";
    public const string Borrowing = "Borrowing";
    public const string SecurityDeposit = "SecurityDeposit";
    public const string LegalCounsel = "LegalCounsel";

    public static readonly string[] All =
    [
        TradeDebtor,
        TradeCreditor,
        Bank,
        LoanGiven,
        Borrowing,
        SecurityDeposit,
        LegalCounsel
    ];
}

public static class ConfirmationStatus
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string ConfirmedAgreed = "ConfirmedAgreed";
    public const string Disputed = "Disputed";
    public const string ManagementRefusal = "ManagementRefusal";
    public const string NonResponseAlternativeDone = "NonResponseAlternativeDone";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Draft,
        Sent,
        ConfirmedAgreed,
        Disputed,
        ManagementRefusal,
        NonResponseAlternativeDone,
        Cancelled
    ];
}

public static class ConfirmationDisputeReason
{
    public const string None = "None";
    public const string GoodsInTransit = "GoodsInTransit";
    public const string ChequeInTransit = "ChequeInTransit";
    public const string RateDiscountDispute = "RateDiscountDispute";
    public const string TdsMismatch = "TdsMismatch";
    public const string UnadjustedDebitCreditNote = "UnadjustedDebitCreditNote";
    public const string OtherVariance = "OtherVariance";

    public static readonly string[] All =
    [
        None,
        GoodsInTransit,
        ChequeInTransit,
        RateDiscountDispute,
        TdsMismatch,
        UnadjustedDebitCreditNote,
        OtherVariance
    ];
}

public static class ConfirmationAuditorConclusion
{
    public const string PendingVerification = "PendingVerification";
    public const string ReconciledSatisfactory = "ReconciledSatisfactory";
    public const string TimingDifferenceAcceptable = "TimingDifferenceAcceptable";
    public const string InvestigationRequired = "InvestigationRequired";
    public const string ProposedAdjustmentEntry = "ProposedAdjustmentEntry";

    public static readonly string[] All =
    [
        PendingVerification,
        ReconciledSatisfactory,
        TimingDifferenceAcceptable,
        InvestigationRequired,
        ProposedAdjustmentEntry
    ];
}

public class ConfirmationCampaign : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid AuditEngagementId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly AsOfDate { get; set; }
    public DateOnly CutoffDate { get; set; }
    public string Status { get; set; } = "Active"; // Active, Closed, Draft

    public int TotalRequestsCount { get; set; }
    public int ConfirmedAgreedCount { get; set; }
    public int DisputedCount { get; set; }
    public int PendingCount { get; set; }
    public decimal TotalBookBalanceAmount { get; set; }
    public decimal TotalConfirmedAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
    public List<ExternalConfirmationRequest> Requests { get; set; } = new();
}

public class ExternalConfirmationRequest : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid AuditEngagementId { get; set; }

    public string LeadScheduleRef { get; set; } = "B";
    public string PartyType { get; set; } = ConfirmationPartyType.TradeDebtor;
    public string PartyName { get; set; } = "";
    public string PartyContactEmail { get; set; } = "";
    public string? PartyContactPhone { get; set; }
    public string? PartyPanGstin { get; set; }

    public string ConfirmationType { get; set; } = Domain.Audit.ConfirmationType.PositiveStatedBalance;
    public decimal BookBalanceAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public DateOnly AsOfDate { get; set; }

    // Digital Verification Link & Token
    public string SecureAccessToken { get; set; } = ConfirmationDeskEngine.GenerateAccessToken();
    public DateTime TokenExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    public string Status { get; set; } = ConfirmationStatus.Draft;
    public DateTime? SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    // Third-party response data
    public decimal? ConfirmedBalanceAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string DisputeReason { get; set; } = ConfirmationDisputeReason.None;
    public string? ThirdPartyRemarks { get; set; }
    public string? ThirdPartySignatoryName { get; set; }
    public string? ThirdPartyDesignation { get; set; }
    public Guid? AttachmentDocId { get; set; }

    // Auditor evaluation & Alternative procedures (SA 505 para 12)
    public string? AlternativeProceduresPerformed { get; set; }
    public string AuditorConclusion { get; set; } = ConfirmationAuditorConclusion.PendingVerification;
    public decimal? ProposedAdjustmentAmount { get; set; }
    public string? AuditorNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public ConfirmationCampaign? Campaign { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
}

public static class ConfirmationDeskEngine
{
    public static string GenerateAccessToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static decimal CalculateVariance(decimal bookBalance, decimal? confirmedBalance)
    {
        if (!confirmedBalance.HasValue) return 0m;
        return bookBalance - confirmedBalance.Value;
    }

    public static string EvaluateConfirmationResponse(decimal bookBalance, decimal confirmedBalance, string? disputeReason)
    {
        var diff = Math.Abs(bookBalance - confirmedBalance);
        if (diff <= 1.0m) // ₹1 tolerance for rounding off
        {
            return ConfirmationStatus.ConfirmedAgreed;
        }

        return ConfirmationStatus.Disputed;
    }

    public static string GenerateConfirmationLetterHtml(
        ExternalConfirmationRequest req,
        string clientName,
        string auditorFirmName)
    {
        var asOf = req.AsOfDate.ToString("dd-MMM-yyyy");
        var balanceDisplay = req.ConfirmationType == ConfirmationType.PositiveBlankBalance
            ? "[PLEASE INSERT YOUR STATED BALANCE AS PER YOUR RECORDS]"
            : $"₹{req.BookBalanceAmount:N2}";

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>External Balance Confirmation - {req.PartyName}</title>
<style>
  body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; color: #222; line-height: 1.6; }}
  .header {{ border-bottom: 2px solid #1e3a8a; padding-bottom: 15px; margin-bottom: 25px; }}
  .title {{ font-size: 20px; font-weight: bold; color: #1e3a8a; margin: 0; }}
  .subtitle {{ font-size: 13px; color: #64748b; margin-top: 4px; }}
  .box {{ background: #f8fafc; border: 1px solid #cbd5e1; border-radius: 8px; padding: 20px; margin: 20px 0; }}
  .meta-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 12px; font-size: 14px; }}
  .btn {{ display: inline-block; background: #2563eb; color: #fff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; margin-top: 20px; }}
  .footer {{ font-size: 12px; color: #64748b; margin-top: 40px; border-top: 1px solid #e2e8f0; padding-top: 15px; }}
</style>
</head>
<body>
  <div class='header'>
    <div class='title'>STANDARD ON AUDITING (SA) 505 EXTERNAL CONFIRMATION REQUEST</div>
    <div class='subtitle'>Auditor Direct Circularization: {auditorFirmName} on behalf of {clientName}</div>
  </div>

  <p>To,<br/>
  <strong>{req.PartyName}</strong><br/>
  {(!string.IsNullOrEmpty(req.PartyContactEmail) ? $"Email: {req.PartyContactEmail}<br/>" : "")}
  {(!string.IsNullOrEmpty(req.PartyPanGstin) ? $"GSTIN / PAN: {req.PartyPanGstin}<br/>" : "")}
  </p>

  <p>Dear Sir / Madam,</p>

  <p>In connection with the statutory audit of the financial statements of <strong>{clientName}</strong> for the period ended <strong>{asOf}</strong>, please confirm directly to our independent statutory auditors, <strong>{auditorFirmName}</strong>, the correctness of the balance outstanding with your organization as of {asOf}.</p>

  <div class='box'>
    <div class='meta-grid'>
      <div><strong>Client Entity:</strong> {clientName}</div>
      <div><strong>Confirmation As Of Date:</strong> {asOf}</div>
      <div><strong>Category:</strong> {req.PartyType}</div>
      <div><strong>Balance as per Client's Records:</strong> <span style='font-size: 16px; color: #1e3a8a; font-weight: bold;'>{balanceDisplay}</span></div>
    </div>
  </div>

  <p>Please note that this is <strong>not a demand for payment</strong>, but a standard statutory audit confirmation required under the Standards on Auditing issued by the Institute of Chartered Accountants of India (ICAI).</p>

  <p>You may submit your response online using our secure confirmation link below:</p>

  <a class='btn' href='/confirm/{req.SecureAccessToken}'>Verify &amp; Submit Balance Confirmation</a>

  <div class='footer'>
    <p>Issued by {auditorFirmName} | Confidential &amp; Privileged Statutory Audit Communication under ICAI SA 505.<br/>
    Access Token: <code>{req.SecureAccessToken}</code> (Expires on {req.TokenExpiresAt:dd-MMM-yyyy})</p>
  </div>
</body>
</html>";
    }

    public static string GenerateSa505SummaryHtml(
        ConfirmationCampaign campaign,
        List<ExternalConfirmationRequest> requests,
        string clientName,
        string auditorFirmName)
    {
        var rows = string.Join("", requests.Select((r, idx) =>
        {
            var statusColor = r.Status switch
            {
                ConfirmationStatus.ConfirmedAgreed => "#15803d",
                ConfirmationStatus.Disputed => "#b91c1c",
                ConfirmationStatus.Sent => "#2563eb",
                ConfirmationStatus.NonResponseAlternativeDone => "#b45309",
                _ => "#475569"
            };

            var varianceDisplay = r.VarianceAmount.HasValue && r.VarianceAmount.Value != 0
                ? $"₹{r.VarianceAmount.Value:N2}"
                : "₹0.00";

            return $@"<tr>
  <td style='padding: 8px 10px; border: 1px solid #cbd5e1; text-align: center;'>{idx + 1}</td>
  <td style='padding: 8px 10px; border: 1px solid #cbd5e1;'><strong>{r.PartyName}</strong><br/><span style='font-size: 11px; color: #64748b;'>{r.PartyType} ({r.LeadScheduleRef})</span></td>
  <td style='padding: 8px 10px; border: 1px solid #cbd5e1; text-align: right; font-weight: bold;'>₹{r.BookBalanceAmount:N2}</td>
  <td style='padding: 8px 10px; border: 1px solid #cbd5e1; text-align: right;'>{(r.ConfirmedBalanceAmount.HasValue ? $"₹{r.ConfirmedBalanceAmount.Value:N2}" : "—")}</td>
  <td style='padding: 8px 10px; border: 1px solid #cbd5e1; text-align: right; color: {(r.VarianceAmount.GetValueOrDefault() != 0 ? "#b91c1c" : "#15803d")};'>{varianceDisplay}</td>
  <td style='padding: 8px 10px; border: 1px solid #cbd5e1; color: {statusColor}; font-weight: bold;'>{r.Status}</td>
  <td style='padding: 8px 10px; border: 1px solid #cbd5e1; font-size: 12px;'>
    <div>{r.DisputeReason}</div>
    {(!string.IsNullOrEmpty(r.AlternativeProceduresPerformed) ? $"<div style='font-size: 11px; color: #b45309; margin-top: 2px;'><strong>Alt Proc:</strong> {r.AlternativeProceduresPerformed}</div>" : "")}
    {(!string.IsNullOrEmpty(r.AuditorConclusion) && r.AuditorConclusion != ConfirmationAuditorConclusion.PendingVerification ? $"<div style='font-size: 11px; color: #1e3a8a; margin-top: 2px;'><strong>Conclusion:</strong> {r.AuditorConclusion}</div>" : "")}
  </td>
</tr>";
        }));

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>SA 505 External Confirmation Summary - {campaign.Title}</title>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 30px; color: #1e293b; line-height: 1.5; }}
  .header {{ border-bottom: 3px solid #1e3a8a; padding-bottom: 12px; margin-bottom: 20px; }}
  .title {{ font-size: 22px; font-weight: 800; color: #1e3a8a; margin: 0; }}
  .kpi-grid {{ display: grid; grid-template-columns: repeat(5, 1fr); gap: 12px; margin-bottom: 24px; }}
  .kpi-card {{ background: #f8fafc; border: 1px solid #cbd5e1; border-radius: 6px; padding: 12px; }}
  .kpi-title {{ font-size: 11px; text-transform: uppercase; color: #64748b; font-weight: 600; }}
  .kpi-val {{ font-size: 18px; font-weight: 800; margin-top: 4px; }}
  table {{ width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 15px; }}
  th {{ background: #1e3a8a; color: #fff; padding: 10px; border: 1px solid #1e3a8a; text-align: left; }}
</style>
</head>
<body>
  <div class='header'>
    <div class='title'>ICAI SA 505 EXTERNAL CONFIRMATIONS AUDIT SUMMARY WORKPAPER</div>
    <div style='color: #64748b; font-size: 13px; margin-top: 4px;'>
      Client: <strong>{clientName}</strong> | Auditor: <strong>{auditorFirmName}</strong> | As of: <strong>{campaign.AsOfDate:dd-MMM-yyyy}</strong> | Campaign: <strong>{campaign.Title}</strong>
    </div>
  </div>

  <div class='kpi-grid'>
    <div class='kpi-card'>
      <div class='kpi-title'>Total Circularized</div>
      <div class='kpi-val'>{campaign.TotalRequestsCount} Parties</div>
    </div>
    <div class='kpi-card'>
      <div class='kpi-title'>Agreed / Confirmed</div>
      <div class='kpi-val' style='color: #15803d;'>{campaign.ConfirmedAgreedCount}</div>
    </div>
    <div class='kpi-card'>
      <div class='kpi-title'>Disputed / Exceptions</div>
      <div class='kpi-val' style='color: #b91c1c;'>{campaign.DisputedCount}</div>
    </div>
    <div class='kpi-card'>
      <div class='kpi-title'>Pending Responses</div>
      <div class='kpi-val' style='color: #2563eb;'>{campaign.PendingCount}</div>
    </div>
    <div class='kpi-card'>
      <div class='kpi-title'>Total Book Balance</div>
      <div class='kpi-val'>₹{campaign.TotalBookBalanceAmount:N2}</div>
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th style='width: 40px; text-align: center;'>#</th>
        <th>Confirming Party Name &amp; Schedule</th>
        <th style='text-align: right;'>Balance as per Books</th>
        <th style='text-align: right;'>Confirmed Balance</th>
        <th style='text-align: right;'>Variance Amount</th>
        <th>Status</th>
        <th>Dispute Reason / Alternative Audit Procedures</th>
      </tr>
    </thead>
    <tbody>
      {rows}
    </tbody>
  </table>

  <div style='margin-top: 30px; padding: 15px; background: #f1f5f9; border-radius: 6px; font-size: 12px; color: #475569;'>
    <strong>Auditor Statement under SA 505 (Paragraph 12–16):</strong><br/>
    For all non-responses, alternative audit procedures including examination of subsequent collections/payments, dispatch documents, and sales contracts were executed. Disputed balances have been investigated for timing differences (goods-in-transit, cheques-in-transit, TDS discrepancies) and proposed audit adjustment entries evaluated against SA 320 performance materiality.
  </div>
</body>
</html>";
    }
}
