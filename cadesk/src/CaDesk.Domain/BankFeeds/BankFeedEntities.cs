using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.BankFeeds;

public static class BankAccountType
{
    public const string Current = "Current";
    public const string Savings = "Savings";
    public const string Overdraft = "Overdraft";
    public const string CashCredit = "CashCredit";

    public static readonly string[] All = [Current, Savings, Overdraft, CashCredit];
}

public static class BankStatementFormat
{
    public const string CSV = "CSV";
    public const string OFX = "OFX";
    public const string PDF_PARSED = "PDF_PARSED";
    public const string MANUAL = "MANUAL";

    public static readonly string[] All = [CSV, OFX, PDF_PARSED, MANUAL];
}

public static class BankStatementStatus
{
    public const string Parsed = "Parsed";
    public const string PartiallyReconciled = "PartiallyReconciled";
    public const string Reconciled = "Reconciled";
    public const string Error = "Error";

    public static readonly string[] All = [Parsed, PartiallyReconciled, Reconciled, Error];
}

public static class BankTransactionType
{
    public const string Debit = "Debit";
    public const string Credit = "Credit";

    public static readonly string[] All = [Debit, Credit];
}

public static class BankMatchStatus
{
    public const string Unmatched = "Unmatched";
    public const string Matched = "Matched";
    public const string AutoCategorized = "AutoCategorized";
    public const string Ignored = "Ignored";

    public static readonly string[] All = [Unmatched, Matched, AutoCategorized, Ignored];
}

public static class BankMatchedEntityType
{
    public const string Cheque = "Cheque";
    public const string Invoice = "Invoice";
    public const string Bill = "Bill";
    public const string Expense = "Expense";
    public const string Journal = "Journal";

    public static readonly string[] All = [Cheque, Invoice, Bill, Expense, Journal];
}

public class BankAccount : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string IfscCode { get; set; } = string.Empty;
    public string AccountType { get; set; } = BankAccountType.Current;
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public string Currency { get; set; } = "INR";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public ICollection<BankStatement> Statements { get; set; } = new List<BankStatement>();
    public ICollection<BankTransaction> Transactions { get; set; } = new List<BankTransaction>();
}

public class BankStatement : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid FirmId { get; set; }
    public Guid BankAccountId { get; set; }
    public DateOnly StatementDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = BankStatementFormat.CSV;
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public int TotalTransactions { get; set; }
    public string Status { get; set; } = BankStatementStatus.Parsed;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }

    public Firm? Firm { get; set; }
    public BankAccount? BankAccount { get; set; }
    public ICollection<BankTransaction> Transactions { get; set; } = new List<BankTransaction>();
}

public class BankTransaction : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid FirmId { get; set; }
    public Guid StatementId { get; set; }
    public Guid BankAccountId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public DateOnly? ValueDate { get; set; }
    public string Narration { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string TransactionType { get; set; } = BankTransactionType.Debit;
    public decimal Amount { get; set; }
    public decimal? RunningBalance { get; set; }
    public string? Category { get; set; }
    public string MatchStatus { get; set; } = BankMatchStatus.Unmatched;
    public string? MatchedEntityType { get; set; }
    public Guid? MatchedEntityId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Firm? Firm { get; set; }
    public BankStatement? Statement { get; set; }
    public BankAccount? BankAccount { get; set; }
}

public class BankReconciliationRule : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid FirmId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string MatchKeyword { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "Any";
    public string AssignCategory { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }

    public Firm? Firm { get; set; }
}
