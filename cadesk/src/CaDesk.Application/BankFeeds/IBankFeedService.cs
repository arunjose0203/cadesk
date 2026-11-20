namespace CaDesk.Application.BankFeeds;

public interface IBankFeedService
{
    Task<IReadOnlyList<BankAccountDto>> GetAccountsAsync(Guid? clientId, CancellationToken ct = default);
    Task<BankAccountDto> GetAccountByIdAsync(Guid id, CancellationToken ct = default);
    Task<BankAccountDto> CreateAccountAsync(CreateBankAccountRequest req, CancellationToken ct = default);
    Task<BankAccountDto> UpdateAccountAsync(Guid id, UpdateBankAccountRequest req, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid id, CancellationToken ct = default);

    Task<BankStatementDto> UploadStatementAsync(Guid accountId, UploadStatementRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<BankStatementDto>> GetStatementsAsync(Guid accountId, CancellationToken ct = default);

    Task<IReadOnlyList<BankTransactionDto>> GetTransactionsAsync(
        Guid accountId,
        Guid? statementId,
        string? matchStatus,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default);

    Task<BankTransactionDto> MatchTransactionAsync(Guid transactionId, MatchTransactionRequest req, CancellationToken ct = default);
    Task<BankReconciliationSummaryDto> GetBrsSummaryAsync(Guid accountId, DateOnly? asOfDate, CancellationToken ct = default);

    Task<IReadOnlyList<BankReconciliationRuleDto>> GetRulesAsync(CancellationToken ct = default);
    Task<BankReconciliationRuleDto> CreateRuleAsync(CreateBankReconciliationRuleRequest req, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);
}
