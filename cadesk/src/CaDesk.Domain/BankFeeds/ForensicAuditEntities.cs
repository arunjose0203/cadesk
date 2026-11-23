using System.Text.RegularExpressions;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.BankFeeds;

public static class ForensicAnomalyType
{
    public const string Sec40A3CashPayment = "Sec40A3CashPayment";
    public const string Sec269StCashReceipt = "Sec269StCashReceipt";
    public const string Sec269SsLoanAccepted = "Sec269SsLoanAccepted";
    public const string Sec269TLoanRepaid = "Sec269TLoanRepaid";
    public const string RoundTripping = "RoundTripping";
    public const string SmurfingStructuring = "SmurfingStructuring";
    public const string DormancySurge = "DormancySurge";
    public const string HighValueRoundSum = "HighValueRoundSum";
    public const string UnrecordedBankAccount = "UnrecordedBankAccount";

    public static readonly string[] All =
    [
        Sec40A3CashPayment,
        Sec269StCashReceipt,
        Sec269SsLoanAccepted,
        Sec269TLoanRepaid,
        RoundTripping,
        SmurfingStructuring,
        DormancySurge,
        HighValueRoundSum,
        UnrecordedBankAccount
    ];
}

public static class ForensicRiskLevel
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly string[] All = [Low, Medium, High, Critical];
}

public static class ForensicAnomalyStatus
{
    public const string Flagged = "Flagged";
    public const string Reviewed = "Reviewed";
    public const string DisallowedIn3CD = "DisallowedIn3CD";
    public const string Dismissed = "Dismissed";

    public static readonly string[] All = [Flagged, Reviewed, DisallowedIn3CD, Dismissed];
}

public class ForensicAnomalyItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid? TransactionId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string AnomalyType { get; set; } = ForensicAnomalyType.Sec40A3CashPayment;
    public string RiskLevel { get; set; } = ForensicRiskLevel.High;
    public decimal Amount { get; set; }
    public decimal ExposureAmount { get; set; }
    public string Counterparty { get; set; } = "";
    public string Narration { get; set; } = "";
    public string StatutorySection { get; set; } = "Section 40A(3)";
    public string RecommendedAction { get; set; } = "";
    public string? AuditorNotes { get; set; }
    public string Status { get; set; } = ForensicAnomalyStatus.Flagged;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public BankAccount? BankAccount { get; set; }
    public BankTransaction? Transaction { get; set; }
}

public static class ForensicAuditEngine
{
    public const decimal Sec40A3GeneralLimit = 10_000m;
    public const decimal Sec40A3TransportLimit = 35_000m;
    public const decimal Sec269StLimit = 200_000m;
    public const decimal Sec269SsTLimit = 20_000m;
    public const decimal HighValueRoundSumThreshold = 500_000m;

    private static readonly Regex CashKeywordsRegex = new(
        @"(?i)\b(cash|atm|self|bearer|withdrawal|wdl|vault|petty\s*cash|cash\s*pmt)\b",
        RegexOptions.Compiled);

    private static readonly Regex CashDepositRegex = new(
        @"(?i)\b(cash\s*dep|by\s*cash|cdm|cash\s*deposit|atm\s*dep|self\s*dep)\b",
        RegexOptions.Compiled);

    private static readonly Regex TransportKeywordsRegex = new(
        @"(?i)\b(transport|transporter|lorry|truck|freight|carriage|roadways|logistics|fleet)\b",
        RegexOptions.Compiled);

    private static readonly Regex LoanKeywordsRegex = new(
        @"(?i)\b(loan|hand\s*loan|deposit|unsecured\s*loan|advance\s*against|borrowing)\b",
        RegexOptions.Compiled);

    public static List<ForensicAnomalyItem> ScanSec40A3(
        IEnumerable<BankTransaction> transactions,
        Guid firmId,
        Guid bankAccountId)
    {
        var anomalies = new List<ForensicAnomalyItem>();

        // Section 40A(3) applies to cash payments / withdrawals (Debits)
        var debitCashTxs = transactions
            .Where(t => t.TransactionType == BankTransactionType.Debit)
            .Where(t => CashKeywordsRegex.IsMatch(t.Narration))
            .ToList();

        // Group by Date to catch multiple cash withdrawals on the same date to the same person
        foreach (var tx in debitCashTxs)
        {
            var isTransport = TransportKeywordsRegex.IsMatch(tx.Narration);
            var limit = isTransport ? Sec40A3TransportLimit : Sec40A3GeneralLimit;

            if (tx.Amount > limit)
            {
                anomalies.Add(new ForensicAnomalyItem
                {
                    FirmId = firmId,
                    BankAccountId = bankAccountId,
                    TransactionId = tx.Id,
                    TransactionDate = tx.TransactionDate,
                    AnomalyType = ForensicAnomalyType.Sec40A3CashPayment,
                    RiskLevel = tx.Amount >= 100_000m ? ForensicRiskLevel.Critical : ForensicRiskLevel.High,
                    Amount = tx.Amount,
                    ExposureAmount = tx.Amount, // 100% disallowance under Sec 40A(3)
                    Counterparty = ExtractCounterparty(tx.Narration),
                    Narration = tx.Narration,
                    StatutorySection = isTransport ? "Section 40A(3) [Goods Carriage Limit ₹35k]" : "Section 40A(3) [General Limit ₹10k]",
                    RecommendedAction = "Verify if payment covered by Rule 6DD exceptions; otherwise disallow 100% in Form 3CD Clause 21(d) & P&L.",
                    Status = ForensicAnomalyStatus.Flagged
                });
            }
        }

        return anomalies;
    }

    public static List<ForensicAnomalyItem> ScanSec269St(
        IEnumerable<BankTransaction> transactions,
        Guid firmId,
        Guid bankAccountId)
    {
        var anomalies = new List<ForensicAnomalyItem>();

        // Section 269ST applies to Cash Receipts (Credits) >= ₹2,00,000
        var creditCashTxs = transactions
            .Where(t => t.TransactionType == BankTransactionType.Credit)
            .Where(t => CashDepositRegex.IsMatch(t.Narration) || CashKeywordsRegex.IsMatch(t.Narration))
            .Where(t => t.Amount >= Sec269StLimit)
            .ToList();

        foreach (var tx in creditCashTxs)
        {
            anomalies.Add(new ForensicAnomalyItem
            {
                FirmId = firmId,
                BankAccountId = bankAccountId,
                TransactionId = tx.Id,
                TransactionDate = tx.TransactionDate,
                AnomalyType = ForensicAnomalyType.Sec269StCashReceipt,
                RiskLevel = ForensicRiskLevel.Critical,
                Amount = tx.Amount,
                ExposureAmount = tx.Amount, // Penalty under Sec 271DA is 100% of receipt
                Counterparty = ExtractCounterparty(tx.Narration),
                Narration = tx.Narration,
                StatutorySection = "Section 269ST [Cash Receipt ≥ ₹2 Lakh]",
                RecommendedAction = "Verify source of cash receipt. Report in Form 3CD Clause 31(ba)/(bb) and alert client of 100% penalty exposure under Sec 271DA.",
                Status = ForensicAnomalyStatus.Flagged
            });
        }

        return anomalies;
    }

    public static List<ForensicAnomalyItem> ScanSec269SsT(
        IEnumerable<BankTransaction> transactions,
        Guid firmId,
        Guid bankAccountId)
    {
        var anomalies = new List<ForensicAnomalyItem>();

        // Look for Loan keywords combined with Cash keywords > ₹20,000
        foreach (var tx in transactions.Where(t => t.Amount > Sec269SsTLimit))
        {
            var isLoan = LoanKeywordsRegex.IsMatch(tx.Narration);
            var isCash = CashKeywordsRegex.IsMatch(tx.Narration) || CashDepositRegex.IsMatch(tx.Narration);

            if (isLoan && isCash)
            {
                var isAcceptance = tx.TransactionType == BankTransactionType.Credit;
                var section = isAcceptance ? "Section 269SS [Loan Accepted in Cash > ₹20k]" : "Section 269T [Loan Repaid in Cash > ₹20k]";
                var anomalyType = isAcceptance ? ForensicAnomalyType.Sec269SsLoanAccepted : ForensicAnomalyType.Sec269TLoanRepaid;

                anomalies.Add(new ForensicAnomalyItem
                {
                    FirmId = firmId,
                    BankAccountId = bankAccountId,
                    TransactionId = tx.Id,
                    TransactionDate = tx.TransactionDate,
                    AnomalyType = anomalyType,
                    RiskLevel = ForensicRiskLevel.High,
                    Amount = tx.Amount,
                    ExposureAmount = tx.Amount, // Penalty equal to amount under Sec 271D / 271E
                    Counterparty = ExtractCounterparty(tx.Narration),
                    Narration = tx.Narration,
                    StatutorySection = section,
                    RecommendedAction = $"Report under Form 3CD Clause 31(a)/(b) and disclose in CARO 2020 Clause (ix). Penalty under Sec 271D/E equal to loan amount.",
                    Status = ForensicAnomalyStatus.Flagged
                });
            }
        }

        return anomalies;
    }

    public static List<ForensicAnomalyItem> ScanRoundTripping(
        IEnumerable<BankTransaction> transactions,
        Guid firmId,
        Guid bankAccountId,
        int dayWindow = 5,
        decimal tolerancePct = 1.0m)
    {
        var anomalies = new List<ForensicAnomalyItem>();
        var list = transactions.OrderBy(t => t.TransactionDate).ToList();

        var debits = list.Where(t => t.TransactionType == BankTransactionType.Debit).ToList();
        var credits = list.Where(t => t.TransactionType == BankTransactionType.Credit).ToList();

        foreach (var d in debits)
        {
            // Find credits within dayWindow with matching amount
            var matches = credits.Where(c =>
                c.TransactionDate >= d.TransactionDate &&
                c.TransactionDate <= d.TransactionDate.AddDays(dayWindow) &&
                Math.Abs(c.Amount - d.Amount) <= (d.Amount * (tolerancePct / 100m))
            ).ToList();

            foreach (var c in matches)
            {
                anomalies.Add(new ForensicAnomalyItem
                {
                    FirmId = firmId,
                    BankAccountId = bankAccountId,
                    TransactionId = d.Id,
                    TransactionDate = d.TransactionDate,
                    AnomalyType = ForensicAnomalyType.RoundTripping,
                    RiskLevel = d.Amount >= 500_000m ? ForensicRiskLevel.Critical : ForensicRiskLevel.High,
                    Amount = d.Amount,
                    ExposureAmount = d.Amount,
                    Counterparty = ExtractCounterparty(d.Narration),
                    Narration = $"Debit of ₹{d.Amount:N0} ({d.TransactionDate:dd-MMM}) circularized with Credit of ₹{c.Amount:N0} ({c.TransactionDate:dd-MMM}) within {c.TransactionDate.DayNumber - d.TransactionDate.DayNumber} days.",
                    StatutorySection = "CARO 2020 Clause (x) / PMLA Round-Tripping",
                    RecommendedAction = "Inquire into commercial rationale, obtain contracts/invoices, check related party status, and verify if circular flow without economic substance.",
                    Status = ForensicAnomalyStatus.Flagged
                });
            }
        }

        return anomalies;
    }

    public static List<ForensicAnomalyItem> ScanSmurfing(
        IEnumerable<BankTransaction> transactions,
        Guid firmId,
        Guid bankAccountId)
    {
        var anomalies = new List<ForensicAnomalyItem>();
        var list = transactions.OrderBy(t => t.TransactionDate).ToList();

        // Check for multiple cash transactions just below ₹10,000 (₹8,000 to ₹9,999) within 48 hours
        var cashTxs = list
            .Where(t => CashKeywordsRegex.IsMatch(t.Narration))
            .Where(t => t.Amount >= 8_000m && t.Amount <= 9_999m)
            .ToList();

        for (int i = 0; i < cashTxs.Count; i++)
        {
            var cluster = cashTxs
                .Where(t => t.Id != cashTxs[i].Id &&
                            Math.Abs(t.TransactionDate.DayNumber - cashTxs[i].TransactionDate.DayNumber) <= 2)
                .ToList();

            if (cluster.Count >= 2)
            {
                var totalCluster = cashTxs[i].Amount + cluster.Sum(x => x.Amount);
                anomalies.Add(new ForensicAnomalyItem
                {
                    FirmId = firmId,
                    BankAccountId = bankAccountId,
                    TransactionId = cashTxs[i].Id,
                    TransactionDate = cashTxs[i].TransactionDate,
                    AnomalyType = ForensicAnomalyType.SmurfingStructuring,
                    RiskLevel = ForensicRiskLevel.High,
                    Amount = cashTxs[i].Amount,
                    ExposureAmount = totalCluster,
                    Counterparty = ExtractCounterparty(cashTxs[i].Narration),
                    Narration = $"Structured cash withdrawal of ₹{cashTxs[i].Amount:N0} alongside {cluster.Count} other transactions totaling ₹{totalCluster:N0} within 48h.",
                    StatutorySection = "Section 40A(3) Anti-Avoidance / Structuring",
                    RecommendedAction = "Scrutinize single-person aggregate threshold breach across consecutive withdrawals to prevent artificial disallowance avoidance.",
                    Status = ForensicAnomalyStatus.Flagged
                });
                break; // avoid duplicate cluster flood
            }
        }

        return anomalies;
    }

    public static List<ForensicAnomalyItem> ScanDormancySurge(
        IEnumerable<BankTransaction> transactions,
        Guid firmId,
        Guid bankAccountId,
        int dormancyThresholdDays = 60)
    {
        var anomalies = new List<ForensicAnomalyItem>();
        var list = transactions.OrderBy(t => t.TransactionDate).ToList();
        if (list.Count < 2) return anomalies;

        for (int i = 1; i < list.Count; i++)
        {
            var prevDate = list[i - 1].TransactionDate;
            var currDate = list[i].TransactionDate;
            var gap = currDate.DayNumber - prevDate.DayNumber;

            if (gap >= dormancyThresholdDays && list[i].Amount >= 200_000m)
            {
                anomalies.Add(new ForensicAnomalyItem
                {
                    FirmId = firmId,
                    BankAccountId = bankAccountId,
                    TransactionId = list[i].Id,
                    TransactionDate = currDate,
                    AnomalyType = ForensicAnomalyType.DormancySurge,
                    RiskLevel = ForensicRiskLevel.Medium,
                    Amount = list[i].Amount,
                    ExposureAmount = list[i].Amount,
                    Counterparty = ExtractCounterparty(list[i].Narration),
                    Narration = $"Sudden reactivation of account after {gap} days of dormancy with high-value transaction of ₹{list[i].Amount:N0}.",
                    StatutorySection = "Forensic Red Flag / Tax Evasion Indicator",
                    RecommendedAction = "Examine reason for dormant account reactivation and verify underlying documentation.",
                    Status = ForensicAnomalyStatus.Flagged
                });
            }
        }

        return anomalies;
    }

    public static List<ForensicAnomalyItem> ScanHighValueRoundSum(
        IEnumerable<BankTransaction> transactions,
        Guid firmId,
        Guid bankAccountId)
    {
        var anomalies = new List<ForensicAnomalyItem>();

        foreach (var tx in transactions.Where(t => t.Amount >= HighValueRoundSumThreshold))
        {
            // Exact multiple of 1,00,000 (1 Lakh)
            if (tx.Amount % 100_000m == 0)
            {
                anomalies.Add(new ForensicAnomalyItem
                {
                    FirmId = firmId,
                    BankAccountId = bankAccountId,
                    TransactionId = tx.Id,
                    TransactionDate = tx.TransactionDate,
                    AnomalyType = ForensicAnomalyType.HighValueRoundSum,
                    RiskLevel = tx.Amount >= 2_500_000m ? ForensicRiskLevel.High : ForensicRiskLevel.Medium,
                    Amount = tx.Amount,
                    ExposureAmount = tx.Amount,
                    Counterparty = ExtractCounterparty(tx.Narration),
                    Narration = $"Round-sum transfer of ₹{tx.Amount:N0} with generic narration: '{tx.Narration}'.",
                    StatutorySection = "SA 240 / Forensic Round-Sum Audit Risk",
                    RecommendedAction = "Verify purpose of round-sum transfer against underlying contracts, vendor purchase orders, or Board resolutions.",
                    Status = ForensicAnomalyStatus.Flagged
                });
            }
        }

        return anomalies;
    }

    public static int CalculateOverallRiskScore(List<ForensicAnomalyItem> anomalies)
    {
        if (anomalies.Count == 0) return 0;

        var score = 0;
        foreach (var a in anomalies)
        {
            score += a.RiskLevel switch
            {
                ForensicRiskLevel.Critical => 25,
                ForensicRiskLevel.High => 15,
                ForensicRiskLevel.Medium => 5,
                _ => 2
            };
        }

        return Math.Min(score, 100);
    }

    public static string ExtractCounterparty(string narration)
    {
        if (string.IsNullOrWhiteSpace(narration)) return "Unknown";

        // Try extracting after common bank tokens like UPI/..., NEFT/..., RTGS/..., IMPS/...
        var match = Regex.Match(narration, @"(?i)(?:upi|neft|rtgs|imps|trf|chq)[\s/:-]+([A-Za-z0-9\s\.\-_]{3,25})");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        var parts = narration.Split(['/', '-', ':'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : narration.Trim();
    }
}
