using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.LedgerSync;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ErpType
{
    TallyPrime = 0,
    BusyAccounting = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentSyncStatus
{
    Offline = 0,
    Connected = 1,
    Syncing = 2,
    Error = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ErpVoucherType
{
    Receipt = 0,
    Payment = 1,
    Journal = 2,
    Contra = 3,
    Sales = 4,
    Purchase = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VoucherReconciliationStatus
{
    Unreconciled = 0,
    AutoMatched = 1,
    ManuallyReconciled = 2,
    Discrepancy = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutgoingVoucherStatus
{
    PendingApproval = 0,
    QueuedForAgent = 1,
    PostedSuccessfully = 2,
    FailedInErp = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditReasonCategory
{
    DepreciationEntrySec32 = 0,
    TaxProvisionAY2526 = 1,
    StatutoryAuditAdjustmentSA700 = 2,
    GstReconciliationAdjustment = 3,
    PrepaidExpensesAmortization = 4,
    ProvisionForBadDebts = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReconciliationMatchRule
{
    ExactAmountAndDate = 0,
    ChequeOrUtrMatch = 1,
    TdsNetDeductionMatch = 2,
    MultiInvoiceBundleMatch = 3,
    ToleranceRoundingMatch = 4,
    ManualMatch = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReconciliationMatchStatus
{
    Suggested = 0,
    Approved = 1,
    Rejected = 2
}

public class ErpSyncAgentConfig : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string AgentName { get; set; } = "Office ERP Sync Agent";
    public string AgentToken { get; set; } = "";
    public ErpType ErpType { get; set; } = ErpType.TallyPrime;
    public string HostUrl { get; set; } = "http://localhost:9000";
    public string CompanyName { get; set; } = "";
    public AgentSyncStatus SyncStatus { get; set; } = AgentSyncStatus.Offline;
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public int SyncFrequencyMinutes { get; set; } = 15;
    public bool SyncDaybook { get; set; } = true;
    public bool SyncMasters { get; set; } = true;
    public bool SyncBankFeeds { get; set; } = true;
    public int TotalVouchersSynced { get; set; }
    public int TotalMastersSynced { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ErpLedgerVoucher : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid AgentConfigId { get; set; }

    public ErpType ErpType { get; set; } = ErpType.TallyPrime;
    public string VoucherNumber { get; set; } = "";
    public string VoucherGuid { get; set; } = "";
    public ErpVoucherType VoucherType { get; set; } = ErpVoucherType.Receipt;
    public DateOnly VoucherDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public decimal Amount { get; set; }
    public string DebitLedger { get; set; } = "";
    public string CreditLedger { get; set; } = "";
    public string Narration { get; set; } = "";
    public string? ChequeNumber { get; set; }
    public DateOnly? InstrumentDate { get; set; }
    public string? BankPartyName { get; set; }
    public DateOnly? BankClearanceDate { get; set; }
    public VoucherReconciliationStatus ReconciliationStatus { get; set; } = VoucherReconciliationStatus.Unreconciled;
    public Guid? ReconciliationMatchId { get; set; }
}

public class ErpOutgoingVoucher : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid AgentConfigId { get; set; }

    public ErpVoucherType VoucherType { get; set; } = ErpVoucherType.Journal;
    public DateOnly VoucherDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string ReferenceNumber { get; set; } = "";
    public string DebitLedger { get; set; } = "";
    public string CreditLedger { get; set; } = "";
    public decimal Amount { get; set; }
    public string Narration { get; set; } = "";
    public AuditReasonCategory AuditReason { get; set; } = AuditReasonCategory.StatutoryAuditAdjustmentSA700;
    public OutgoingVoucherStatus PostingStatus { get; set; } = OutgoingVoucherStatus.PendingApproval;
    public string? GeneratedXmlPayload { get; set; }
    public string? ErpVoucherNumber { get; set; }
    public string? ErpResponseXml { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? AuthorizedByUserId { get; set; }
    public string? AuthorizedByName { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
}

public class BankLedgerReconciliationMatch : TenantEntity
{
    public Guid ClientId { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid BankTransactionId { get; set; }
    public Guid? ErpLedgerVoucherId { get; set; }

    public ReconciliationMatchRule MatchRule { get; set; } = ReconciliationMatchRule.ExactAmountAndDate;
    public decimal ConfidenceScore { get; set; } = 95.0m; // 0 - 100%
    public decimal BankAmount { get; set; }
    public decimal LedgerAmount { get; set; }
    public decimal VarianceAmount { get; set; }
    public string? VarianceExplanation { get; set; }
    public string MatchedVoucherNumbers { get; set; } = "";
    public ReconciliationMatchStatus Status { get; set; } = ReconciliationMatchStatus.Suggested;
    public Guid? ReconciledByUserId { get; set; }
    public string? ReconciledByName { get; set; }
    public DateTimeOffset? ReconciledAt { get; set; }
}

public static class TallyXmlProtocol
{
    public static string GenerateExportVouchersXml(string companyName, DateOnly fromDate, DateOnly toDate)
    {
        string fromStr = fromDate.ToString("yyyyMMdd");
        string toStr = toDate.ToString("yyyyMMdd");

        return $@"<ENVELOPE>
  <HEADER>
    <TALLYREQUEST>Export Data</TALLYREQUEST>
  </HEADER>
  <BODY>
    <EXPORTDATA>
      <REQUESTDESC>
        <REPORTNAME>Voucher Register</REPORTNAME>
        <STATICVARIABLES>
          <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
          <SVCURRENTCOMPANY>{companyName}</SVCURRENTCOMPANY>
          <SVFROMDATE>{fromStr}</SVFROMDATE>
          <SVTODATE>{toStr}</SVTODATE>
        </STATICVARIABLES>
      </REQUESTDESC>
    </EXPORTDATA>
  </BODY>
</ENVELOPE>";
    }

    public static string GenerateExportMastersXml(string companyName)
    {
        return $@"<ENVELOPE>
  <HEADER>
    <TALLYREQUEST>Export Data</TALLYREQUEST>
  </HEADER>
  <BODY>
    <EXPORTDATA>
      <REQUESTDESC>
        <REPORTNAME>List of Accounts</REPORTNAME>
        <STATICVARIABLES>
          <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
          <SVCURRENTCOMPANY>{companyName}</SVCURRENTCOMPANY>
        </STATICVARIABLES>
      </REQUESTDESC>
    </EXPORTDATA>
  </BODY>
</ENVELOPE>";
    }

    public static string GenerateImportJournalVoucherXml(
        string companyName,
        string voucherType,
        DateOnly date,
        string reference,
        decimal amount,
        string debitLedger,
        string creditLedger,
        string narration
    )
    {
        string dateStr = date.ToString("yyyyMMdd");
        string posAmount = Math.Abs(amount).ToString("F2");
        string negAmount = (-Math.Abs(amount)).ToString("F2");

        return $@"<ENVELOPE>
  <HEADER>
    <TALLYREQUEST>Import Data</TALLYREQUEST>
  </HEADER>
  <BODY>
    <IMPORTDATA>
      <REQUESTDESC>
        <REPORTNAME>All Masters</REPORTNAME>
        <STATICVARIABLES>
          <SVCURRENTCOMPANY>{companyName}</SVCURRENTCOMPANY>
        </STATICVARIABLES>
      </REQUESTDESC>
      <REQUESTDATA>
        <TALLYMESSAGE xmlns:UDF=""TallyUDF"">
          <VOUCHER VCHTYPE=""{voucherType}"" ACTION=""Create"">
            <DATE>{dateStr}</DATE>
            <EFFECTIVEDATE>{dateStr}</EFFECTIVEDATE>
            <VOUCHERTYPENAME>{voucherType}</VOUCHERTYPENAME>
            <REFERENCE>{reference}</REFERENCE>
            <NARRATION>{narration} (Posted by CA Desk Audit Engine)</NARRATION>
            <ALLLEDGERENTRIES.LIST>
              <LEDGERNAME>{debitLedger}</LEDGERNAME>
              <ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE>
              <AMOUNT>{negAmount}</AMOUNT>
            </ALLLEDGERENTRIES.LIST>
            <ALLLEDGERENTRIES.LIST>
              <LEDGERNAME>{creditLedger}</LEDGERNAME>
              <ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE>
              <AMOUNT>{posAmount}</AMOUNT>
            </ALLLEDGERENTRIES.LIST>
          </VOUCHER>
        </TALLYMESSAGE>
      </REQUESTDATA>
    </IMPORTDATA>
  </BODY>
</ENVELOPE>";
    }

    public static (bool Success, string? VoucherNumber, string? ErrorMessage) ParseImportResponse(string responseXml)
    {
        if (string.IsNullOrWhiteSpace(responseXml))
            return (false, null, "Empty response from ERP");

        try
        {
            var doc = XDocument.Parse(responseXml);
            var errorsElem = doc.Descendants("ERRORS").FirstOrDefault();
            var createdElem = doc.Descendants("CREATED").FirstOrDefault();

            int errors = errorsElem != null && int.TryParse(errorsElem.Value, out int err) ? err : 0;
            int created = createdElem != null && int.TryParse(createdElem.Value, out int cr) ? cr : 0;

            if (errors == 0 && created > 0)
            {
                var vchElem = doc.Descendants("VOUCHERNUMBER").FirstOrDefault()
                    ?? doc.Descendants("LASTVCHID").FirstOrDefault();
                string vchNum = vchElem?.Value ?? $"TALLY-{DateTime.UtcNow:yyyyMMddHHmmss}";
                return (true, vchNum, null);
            }

            var lineError = doc.Descendants("LINEERROR").FirstOrDefault()?.Value
                ?? "Tally XML import error reported";
            return (false, null, lineError);
        }
        catch (Exception ex)
        {
            // If response contains "CREATED: 1" in plain text
            if (responseXml.Contains("<CREATED>1</CREATED>") || responseXml.Contains("CREATED: 1"))
            {
                return (true, $"TALLY-{DateTime.UtcNow:yyyyMMddHHmmss}", null);
            }
            return (false, null, $"XML parsing error: {ex.Message}");
        }
    }

    public static string GenerateExportTrialBalanceXml(string companyName, DateOnly fromDate, DateOnly toDate)
    {
        string safeCompany = System.Security.SecurityElement.Escape(companyName) ?? companyName;
        string fromStr = fromDate.ToString("yyyyMMdd");
        string toStr = toDate.ToString("yyyyMMdd");

        return $@"<ENVELOPE>
  <HEADER>
    <TALLYREQUEST>Export Data</TALLYREQUEST>
  </HEADER>
  <BODY>
    <EXPORTDATA>
      <REQUESTDESC>
        <REPORTNAME>Trial Balance</REPORTNAME>
        <STATICVARIABLES>
          <SVCURRENTCOMPANY>{safeCompany}</SVCURRENTCOMPANY>
          <SVFROMDATE>{fromStr}</SVFROMDATE>
          <SVTODATE>{toStr}</SVTODATE>
          <EXPLODEFLAG>Yes</EXPLODEFLAG>
          <SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>
        </STATICVARIABLES>
      </REQUESTDESC>
    </EXPORTDATA>
  </BODY>
</ENVELOPE>";
    }

    public record ParsedTallyVoucher(
        string VoucherNumber,
        string VoucherGuid,
        ErpVoucherType VoucherType,
        DateOnly VoucherDate,
        DateOnly EffectiveDate,
        decimal Amount,
        string DebitLedger,
        string CreditLedger,
        string Narration,
        string? ChequeNumber,
        DateOnly? InstrumentDate,
        string? BankPartyName
    );

    public static List<ParsedTallyVoucher> ParseExportVouchersXml(string responseXml)
    {
        var result = new List<ParsedTallyVoucher>();
        if (string.IsNullOrWhiteSpace(responseXml))
            return result;

        var doc = ParseSafeXml(responseXml);
        foreach (var vch in doc.Descendants("VOUCHER"))
        {
            string? action = vch.Attribute("ACTION")?.Value;
            if (string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(vch.Element("ISCANCELLED")?.Value, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string vchNum = (vch.Element("VOUCHERNUMBER")?.Value
                ?? vch.Element("REFERENCE")?.Value
                ?? vch.Element("MASTERID")?.Value
                ?? $"VCH-{result.Count + 1}").Trim();
            string vchGuid = (vch.Element("GUID")?.Value ?? vch.Attribute("REMOTEID")?.Value ?? vchNum).Trim();
            string vchTypeRaw = (vch.Element("VOUCHERTYPENAME")?.Value ?? vch.Attribute("VCHTYPE")?.Value ?? "Journal").Trim();
            ErpVoucherType vchType = vchTypeRaw.ToLowerInvariant() switch
            {
                var s when s.Contains("payment") => ErpVoucherType.Payment,
                var s when s.Contains("receipt") => ErpVoucherType.Receipt,
                var s when s.Contains("contra") => ErpVoucherType.Contra,
                var s when s.Contains("sales") || s.Contains("credit note") => ErpVoucherType.Sales,
                var s when s.Contains("purchase") || s.Contains("debit note") => ErpVoucherType.Purchase,
                _ => ErpVoucherType.Journal,
            };

            DateOnly vchDate = ParseTallyDate(vch.Element("DATE")?.Value) ?? DateOnly.FromDateTime(DateTime.UtcNow);
            DateOnly effDate = ParseTallyDate(vch.Element("EFFECTIVEDATE")?.Value) ?? vchDate;
            string narration = (vch.Element("NARRATION")?.Value ?? "").Trim();
            string? party = vch.Element("PARTYLEDGERNAME")?.Value?.Trim();

            var ledgerEntries = vch.Elements("ALLLEDGERENTRIES.LIST")
                .Concat(vch.Elements("LEDGERENTRIES.LIST"))
                .ToList();

            string debitLedger = "";
            string creditLedger = "";
            decimal totalDebit = 0m;
            decimal totalCredit = 0m;
            string? chequeNo = null;
            DateOnly? instrDate = null;

            foreach (var le in ledgerEntries)
            {
                string lName = (le.Element("LEDGERNAME")?.Value ?? "").Trim();
                if (string.IsNullOrWhiteSpace(lName)) continue;

                decimal amt = 0m;
                string? rawAmt = le.Element("AMOUNT")?.Value;
                if (!string.IsNullOrWhiteSpace(rawAmt))
                    decimal.TryParse(rawAmt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out amt);

                bool isDeemedPositive = string.Equals(le.Element("ISDEEMEDPOSITIVE")?.Value, "Yes", StringComparison.OrdinalIgnoreCase)
                    || amt < 0m;
                decimal absAmt = Math.Abs(amt);

                if (isDeemedPositive)
                {
                    totalDebit += absAmt;
                    if (string.IsNullOrEmpty(debitLedger)) debitLedger = lName;
                }
                else
                {
                    totalCredit += absAmt;
                    if (string.IsNullOrEmpty(creditLedger)) creditLedger = lName;
                }

                if (chequeNo == null)
                {
                    var bankAlloc = le.Elements("BANKALLOCATIONS.LIST").FirstOrDefault();
                    if (bankAlloc != null)
                    {
                        chequeNo = bankAlloc.Element("INSTRUMENTNUMBER")?.Value?.Trim();
                        instrDate = ParseTallyDate(bankAlloc.Element("INSTRUMENTDATE")?.Value);
                    }
                }
            }

            decimal finalAmt = Math.Max(totalDebit, totalCredit);
            if (finalAmt <= 0m) continue;
            if (string.IsNullOrEmpty(debitLedger)) debitLedger = party ?? "Sundry Debtors";
            if (string.IsNullOrEmpty(creditLedger)) creditLedger = party ?? "Sundry Creditors";

            result.Add(new ParsedTallyVoucher(
                VoucherNumber: vchNum,
                VoucherGuid: vchGuid,
                VoucherType: vchType,
                VoucherDate: vchDate,
                EffectiveDate: effDate,
                Amount: finalAmt,
                DebitLedger: debitLedger,
                CreditLedger: creditLedger,
                Narration: narration,
                ChequeNumber: string.IsNullOrWhiteSpace(chequeNo) ? null : chequeNo,
                InstrumentDate: instrDate,
                BankPartyName: string.IsNullOrWhiteSpace(party) ? null : party
            ));
        }

        return result;
    }

    private static XDocument ParseSafeXml(string xml)
    {
        using var reader = System.Xml.XmlReader.Create(new StringReader(xml), new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 100_000_000,
        });
        return XDocument.Load(reader);
    }

    private static DateOnly? ParseTallyDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string s = raw.Trim();
        if (s.Length == 8 && DateOnly.TryParseExact(s, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d1))
            return d1;
        if (DateOnly.TryParse(s, out var d2))
            return d2;
        return null;
    }
}

public static class AiBankLedgerReconciliationEngine
{
    private static readonly decimal[] KnownTdsRates = [0.01m, 0.02m, 0.05m, 0.10m]; // 1% (194C Ind), 2% (194C Co/194I P&M), 5% (194H/194IB), 10% (194J/194I Land)

    public record CandidateMatch(
        ErpLedgerVoucher Voucher,
        ReconciliationMatchRule Rule,
        decimal ConfidenceScore,
        decimal Variance,
        string Explanation
    );

    public static CandidateMatch? FindBestMatch(
        BankTransaction bankTx,
        List<ErpLedgerVoucher> openVouchers
    )
    {
        if (openVouchers.Count == 0) return null;

        decimal bankAmt = Math.Abs(bankTx.Amount);

        // 1. Cheque / UTR Narration Match
        if (!string.IsNullOrWhiteSpace(bankTx.ReferenceNumber) || !string.IsNullOrWhiteSpace(bankTx.Narration))
        {
            foreach (var v in openVouchers)
            {
                if (!string.IsNullOrWhiteSpace(v.ChequeNumber) &&
                    (bankTx.Narration.Contains(v.ChequeNumber) || bankTx.ReferenceNumber?.Contains(v.ChequeNumber) == true))
                {
                    decimal variance = Math.Round(bankAmt - v.Amount, 2);
                    if (Math.Abs(variance) <= 50.0m) // Small variance tolerance if cheque matches
                    {
                        return new CandidateMatch(
                            v,
                            ReconciliationMatchRule.ChequeOrUtrMatch,
                            96.0m,
                            variance,
                            $"Matched Cheque/Ref #{v.ChequeNumber} in bank narration (Variance: ₹{variance:F2})"
                        );
                    }
                }

                // Check for UTR number matches (UTR regex: 12-22 alphanumerics)
                var utrMatch = Regex.Match(v.Narration, @"\b([A-Z0-9]{12,22})\b", RegexOptions.IgnoreCase);
                if (utrMatch.Success && bankTx.Narration.Contains(utrMatch.Value))
                {
                    decimal variance = Math.Round(bankAmt - v.Amount, 2);
                    return new CandidateMatch(
                        v,
                        ReconciliationMatchRule.ChequeOrUtrMatch,
                        97.0m,
                        variance,
                        $"Matched NEFT/RTGS UTR '{utrMatch.Value}' between bank statement and voucher"
                    );
                }
            }
        }

        // 2. Exact Amount & Date Window (+/- 3 days) Match
        var exactCandidates = openVouchers
            .Where(v => Math.Abs(v.Amount - bankAmt) <= 0.01m)
            .OrderBy(v => Math.Abs(v.VoucherDate.DayNumber - bankTx.TransactionDate.DayNumber))
            .ToList();

        if (exactCandidates.Count > 0)
        {
            var bestExact = exactCandidates.First();
            int dayDiff = Math.Abs(bestExact.VoucherDate.DayNumber - bankTx.TransactionDate.DayNumber);
            decimal confidence = dayDiff <= 3 ? 98.0m : dayDiff <= 7 ? 92.0m : 85.0m;

            return new CandidateMatch(
                bestExact,
                ReconciliationMatchRule.ExactAmountAndDate,
                confidence,
                0m,
                $"Exact amount match ₹{bankAmt:N2} (Voucher #{bestExact.VoucherNumber}, {dayDiff} days delta)"
            );
        }

        // 3. TDS Net-of-Tax Deduction Match
        // E.g., Invoice voucher for ₹1,00,000, Bank receipt of ₹98,000 (TDS 2% = ₹2,000)
        foreach (var v in openVouchers.Where(x => x.Amount > bankAmt))
        {
            foreach (var rate in KnownTdsRates)
            {
                decimal expectedNet = Math.Round(v.Amount * (1m - rate), 2);
                if (Math.Abs(expectedNet - bankAmt) <= 1.00m)
                {
                    decimal tdsAmt = Math.Round(v.Amount * rate, 2);
                    return new CandidateMatch(
                        v,
                        ReconciliationMatchRule.TdsNetDeductionMatch,
                        91.0m,
                        Math.Round(bankAmt - v.Amount, 2),
                        $"Net TDS deduction match: Gross ₹{v.Amount:N2} less {(rate * 100):F0}% TDS (₹{tdsAmt:N2}) = Net ₹{bankAmt:N2}"
                    );
                }
            }
        }

        // 4. Minor Tolerance / Rounding FX Match (+/- ₹1 to ₹10)
        var toleranceCandidate = openVouchers
            .Where(v => Math.Abs(v.Amount - bankAmt) <= 10.0m && Math.Abs(v.VoucherDate.DayNumber - bankTx.TransactionDate.DayNumber) <= 5)
            .OrderBy(v => Math.Abs(v.Amount - bankAmt))
            .FirstOrDefault();

        if (toleranceCandidate != null)
        {
            decimal diff = Math.Round(bankAmt - toleranceCandidate.Amount, 2);
            return new CandidateMatch(
                toleranceCandidate,
                ReconciliationMatchRule.ToleranceRoundingMatch,
                84.0m,
                diff,
                $"Rounding variance of ₹{diff:F2} within statutory tolerance window (+/- ₹10)"
            );
        }

        // 5. Multi-Invoice / Bundle Matching (sum of 2 open vouchers matches bankTx)
        for (int i = 0; i < openVouchers.Count; i++)
        {
            for (int j = i + 1; j < openVouchers.Count; j++)
            {
                decimal bundleSum = openVouchers[i].Amount + openVouchers[j].Amount;
                if (Math.Abs(bundleSum - bankAmt) <= 1.0m)
                {
                    return new CandidateMatch(
                        openVouchers[i],
                        ReconciliationMatchRule.MultiInvoiceBundleMatch,
                        88.0m,
                        Math.Round(bankAmt - bundleSum, 2),
                        $"Multi-invoice payment bundle: Voucher #{openVouchers[i].VoucherNumber} (₹{openVouchers[i].Amount:N2}) + #{openVouchers[j].VoucherNumber} (₹{openVouchers[j].Amount:N2})"
                    );
                }
            }
        }

        return null;
    }
}
