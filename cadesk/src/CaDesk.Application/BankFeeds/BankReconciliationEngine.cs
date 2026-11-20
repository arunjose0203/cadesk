using CaDesk.Domain.BankFeeds;

namespace CaDesk.Application.BankFeeds;

public static class BankReconciliationEngine
{
    private static readonly (string Keyword, string Category)[] DefaultHeuristics =
    [
        ("SALARY", "Payroll / Salaries"),
        ("GST", "GST Payment"),
        ("ITAX", "Direct Tax / Advance Tax"),
        ("TDS", "TDS Deposit"),
        ("CHQ", "Cheque Clearing"),
        ("CHEQUE", "Cheque Clearing"),
        ("CLEARING", "Cheque Clearing"),
        ("UPI", "UPI Payments & Collections"),
        ("NEFT", "Electronic Fund Transfer - NEFT"),
        ("RTGS", "Electronic Fund Transfer - RTGS"),
        ("IMPS", "Immediate Payment Service - IMPS"),
        ("INTEREST", "Bank Interest"),
        ("INT PD", "Bank Interest"),
        ("CHARGES", "Bank Charges & SMS Fees"),
        ("CHRG", "Bank Charges & Commission"),
        ("DIVIDEND", "Dividend Income"),
        ("ATM", "Cash Withdrawal"),
        ("CASH DEP", "Cash Deposit"),
        ("POS", "Point of Sale Merchant Spend")
    ];

    public static (string? Category, string MatchStatus) CategorizeTransaction(
        string narration,
        string txType,
        IReadOnlyList<BankReconciliationRule> rules)
    {
        var upperNarr = narration.ToUpperInvariant();

        // 1. Check custom user-defined rules in priority order
        foreach (var rule in rules.Where(r => r.IsActive).OrderByDescending(r => r.Priority))
        {
            if (string.IsNullOrWhiteSpace(rule.MatchKeyword)) continue;
            var kw = rule.MatchKeyword.ToUpperInvariant();

            if (!upperNarr.Contains(kw)) continue;

            if (rule.TransactionType != "Any" && !string.Equals(rule.TransactionType, txType, StringComparison.OrdinalIgnoreCase))
                continue;

            return (rule.AssignCategory, BankMatchStatus.AutoCategorized);
        }

        // 2. Check default built-in heuristics
        foreach (var (kw, cat) in DefaultHeuristics)
        {
            if (upperNarr.Contains(kw))
            {
                return (cat, BankMatchStatus.AutoCategorized);
            }
        }

        return (null, BankMatchStatus.Unmatched);
    }

    public static BankReconciliationSummaryDto CalculateBrsSummary(
        BankAccount account,
        IReadOnlyList<BankTransaction> transactions,
        DateOnly asOfDate)
    {
        var relevantTx = transactions.Where(t => t.TransactionDate <= asOfDate).ToList();

        // Latest running balance or account current balance
        var latestTx = relevantTx.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.CreatedAt).FirstOrDefault();
        decimal bankStatementBalance = latestTx?.RunningBalance ?? account.CurrentBalance;

        // Uncleared debits: Outward payments/cheques in books or bank transactions unmatched
        var unmatchedDebits = relevantTx
            .Where(t => t.TransactionType == BankTransactionType.Debit && t.MatchStatus != BankMatchStatus.Matched)
            .ToList();
        decimal totalUnclearedDebits = unmatchedDebits.Sum(t => t.Amount);

        // Uncredited credits: Inward receipts/cheques deposited but not cleared
        var unmatchedCredits = relevantTx
            .Where(t => t.TransactionType == BankTransactionType.Credit && t.MatchStatus != BankMatchStatus.Matched)
            .ToList();
        decimal totalUncreditedCredits = unmatchedCredits.Sum(t => t.Amount);

        // Direct bank debits (charges, interest paid) not booked
        decimal directDebitsNotBooked = unmatchedDebits
            .Where(t => (t.Category ?? "").Contains("Charges", StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.Amount);

        // Direct bank credits (interest received, auto-credits) not booked
        decimal directCreditsNotBooked = unmatchedCredits
            .Where(t => (t.Category ?? "").Contains("Interest", StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.Amount);

        // Standard BRS formula:
        // Balance as per Bank Statement
        // + Cheques issued but not presented (Uncleared Debits)
        // - Cheques deposited but not cleared (Uncredited Credits)
        // = Balance as per Books of Account
        decimal balanceAsPerBooks = bankStatementBalance + totalUnclearedDebits - totalUncreditedCredits;
        decimal reconciledDiff = (bankStatementBalance + totalUnclearedDebits - totalUncreditedCredits) - balanceAsPerBooks;

        int unmatchedCount = relevantTx.Count(t => t.MatchStatus != BankMatchStatus.Matched);
        int matchedCount = relevantTx.Count(t => t.MatchStatus == BankMatchStatus.Matched);

        return new BankReconciliationSummaryDto(
            BankAccountId: account.Id,
            AccountName: account.AccountName,
            BankName: account.BankName,
            AccountNumber: account.AccountNumber,
            AsOfDate: asOfDate,
            BankStatementBalance: bankStatementBalance,
            TotalUnclearedDebits: totalUnclearedDebits,
            TotalUncreditedCredits: totalUncreditedCredits,
            DirectDebitsNotBooked: directDebitsNotBooked,
            DirectCreditsNotBooked: directCreditsNotBooked,
            BalanceAsPerBooks: balanceAsPerBooks,
            ReconciledDifference: reconciledDiff,
            UnmatchedCount: unmatchedCount,
            MatchedCount: matchedCount);
    }
}
