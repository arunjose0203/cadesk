namespace CaDesk.Application.BankFeeds;

public sealed record BankAccountDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string AccountName,
    string BankName,
    string? BranchName,
    string AccountNumber,
    string IfscCode,
    string AccountType,
    decimal OpeningBalance,
    decimal CurrentBalance,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateBankAccountRequest(
    Guid ClientId,
    string AccountName,
    string BankName,
    string? BranchName,
    string AccountNumber,
    string IfscCode,
    string AccountType,
    decimal OpeningBalance,
    string? Currency);

public sealed record UpdateBankAccountRequest(
    string AccountName,
    string BankName,
    string? BranchName,
    string AccountNumber,
    string IfscCode,
    string AccountType,
    bool IsActive);

public sealed record BankStatementDto(
    Guid Id,
    Guid BankAccountId,
    DateOnly StatementDate,
    DateOnly StartDate,
    DateOnly EndDate,
    string FileName,
    string Format,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDebits,
    decimal TotalCredits,
    int TotalTransactions,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record BankTransactionDto(
    Guid Id,
    Guid StatementId,
    Guid BankAccountId,
    DateOnly TransactionDate,
    DateOnly? ValueDate,
    string Narration,
    string? ReferenceNumber,
    string TransactionType,
    decimal Amount,
    decimal? RunningBalance,
    string? Category,
    string MatchStatus,
    string? MatchedEntityType,
    Guid? MatchedEntityId,
    string? Notes);

public sealed record MatchTransactionRequest(
    string MatchStatus,
    string? MatchedEntityType,
    Guid? MatchedEntityId,
    string? Notes);

public sealed record BankReconciliationRuleDto(
    Guid Id,
    string RuleName,
    string MatchKeyword,
    string TransactionType,
    string AssignCategory,
    int Priority,
    bool IsActive);

public sealed record CreateBankReconciliationRuleRequest(
    string RuleName,
    string MatchKeyword,
    string TransactionType,
    string AssignCategory,
    int Priority);

public sealed record UploadStatementRequest(
    string FileName,
    string? CsvContent = null,
    DateOnly? StatementDate = null,
    string? FileBase64 = null);

public sealed record BankReconciliationSummaryDto(
    Guid BankAccountId,
    string AccountName,
    string BankName,
    string AccountNumber,
    DateOnly AsOfDate,
    decimal BankStatementBalance,
    decimal TotalUnclearedDebits,
    decimal TotalUncreditedCredits,
    decimal DirectDebitsNotBooked,
    decimal DirectCreditsNotBooked,
    decimal BalanceAsPerBooks,
    decimal ReconciledDifference,
    int UnmatchedCount,
    int MatchedCount);

public sealed record ParsedStatementResult(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDebits,
    decimal TotalCredits,
    List<ParsedTransactionItem> Items);

public sealed record ParsedTransactionItem(
    DateOnly Date,
    DateOnly? ValueDate,
    string Narration,
    string? ChqRefNo,
    string Type,
    decimal Amount,
    decimal? Balance);
