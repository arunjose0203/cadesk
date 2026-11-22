using System.Text.RegularExpressions;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.BankFeeds;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.BankFeeds;

public class BankFeedService : IBankFeedService
{
    private static readonly Regex IfscRegex = new(@"^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.Compiled);
    private readonly IAppDbContext _db;

    public BankFeedService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BankAccountDto>> GetAccountsAsync(Guid? clientId, CancellationToken ct = default)
    {
        var query = _db.BankAccounts.AsNoTracking().Include(b => b.Client).AsQueryable();

        if (clientId.HasValue)
            query = query.Where(b => b.ClientId == clientId.Value);

        var list = await query.OrderBy(b => b.BankName).ThenBy(b => b.AccountName).ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<BankAccountDto> GetAccountByIdAsync(Guid id, CancellationToken ct = default)
    {
        var acc = await _db.BankAccounts.AsNoTracking().Include(b => b.Client).FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException($"Bank account with ID '{id}' was not found.");

        return ToDto(acc);
    }

    public async Task<BankAccountDto> CreateAccountAsync(CreateBankAccountRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.AccountName))
            throw new ArgumentException("Account name is required.");
        if (string.IsNullOrWhiteSpace(req.BankName))
            throw new ArgumentException("Bank name is required.");
        if (string.IsNullOrWhiteSpace(req.AccountNumber) || req.AccountNumber.Length < 5)
            throw new ArgumentException("Valid account number is required (min 5 characters).");
        if (string.IsNullOrWhiteSpace(req.IfscCode) || !IfscRegex.IsMatch(req.IfscCode.ToUpperInvariant()))
            throw new ArgumentException("Invalid IFSC code format (must be 11 characters e.g. HDFC0001234).");
        if (!BankAccountType.All.Contains(req.AccountType))
            throw new ArgumentException($"Invalid account type. Allowed: {string.Join(", ", BankAccountType.All)}");

        var client = await _db.Clients.FindAsync([req.ClientId], ct)
            ?? throw new KeyNotFoundException($"Client with ID '{req.ClientId}' was not found.");

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            ClientId = req.ClientId,
            AccountName = req.AccountName.Trim(),
            BankName = req.BankName.Trim(),
            BranchName = string.IsNullOrWhiteSpace(req.BranchName) ? null : req.BranchName.Trim(),
            AccountNumber = req.AccountNumber.Trim(),
            IfscCode = req.IfscCode.Trim().ToUpperInvariant(),
            AccountType = req.AccountType,
            OpeningBalance = req.OpeningBalance,
            CurrentBalance = req.OpeningBalance,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "INR" : req.Currency.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BankAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        account.Client = client;
        return ToDto(account);
    }

    public async Task<BankAccountDto> UpdateAccountAsync(Guid id, UpdateBankAccountRequest req, CancellationToken ct = default)
    {
        var account = await _db.BankAccounts.Include(b => b.Client).FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException($"Bank account with ID '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(req.AccountName))
            throw new ArgumentException("Account name is required.");
        if (string.IsNullOrWhiteSpace(req.BankName))
            throw new ArgumentException("Bank name is required.");
        if (string.IsNullOrWhiteSpace(req.AccountNumber) || req.AccountNumber.Length < 5)
            throw new ArgumentException("Valid account number is required.");
        if (string.IsNullOrWhiteSpace(req.IfscCode) || !IfscRegex.IsMatch(req.IfscCode.ToUpperInvariant()))
            throw new ArgumentException("Invalid IFSC code format.");
        if (!BankAccountType.All.Contains(req.AccountType))
            throw new ArgumentException($"Invalid account type. Allowed: {string.Join(", ", BankAccountType.All)}");

        account.AccountName = req.AccountName.Trim();
        account.BankName = req.BankName.Trim();
        account.BranchName = string.IsNullOrWhiteSpace(req.BranchName) ? null : req.BranchName.Trim();
        account.AccountNumber = req.AccountNumber.Trim();
        account.IfscCode = req.IfscCode.Trim().ToUpperInvariant();
        account.AccountType = req.AccountType;
        account.IsActive = req.IsActive;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    public async Task DeleteAccountAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException($"Bank account with ID '{id}' was not found.");

        _db.BankAccounts.Remove(account);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BankStatementDto> UploadStatementAsync(Guid accountId, UploadStatementRequest req, CancellationToken ct = default)
    {
        var account = await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == accountId, ct)
            ?? throw new KeyNotFoundException($"Bank account with ID '{accountId}' was not found.");

        if (string.IsNullOrWhiteSpace(req.FileName))
            throw new ArgumentException("File name is required.");

        string? content = !string.IsNullOrWhiteSpace(req.FileBase64) ? req.FileBase64 : req.CsvContent;
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Statement content (CSV, OFX, or PDF) is required.");

        ParsedStatementResult parsed;
        string statementFormat;

        if (req.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("JVBERi", StringComparison.Ordinal) ||
            content.StartsWith("%PDF", StringComparison.Ordinal))
        {
            statementFormat = BankStatementFormat.PDF_PARSED;
            parsed = PdfBankStatementParser.Parse(content);
        }
        else if (req.FileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase) ||
                 content.Contains("<OFX>", StringComparison.OrdinalIgnoreCase) ||
                 content.Contains("<STMTTRN>", StringComparison.OrdinalIgnoreCase))
        {
            statementFormat = BankStatementFormat.OFX;
            parsed = BankStatementParser.ParseOfx(content);
        }
        else
        {
            statementFormat = BankStatementFormat.CSV;
            parsed = BankStatementParser.ParseCsv(content);
        }

        var rules = await _db.BankReconciliationRules.Where(r => r.IsActive).ToListAsync(ct);

        var statement = new BankStatement
        {
            Id = Guid.NewGuid(),
            BankAccountId = account.Id,
            StatementDate = req.StatementDate ?? parsed.EndDate,
            StartDate = parsed.StartDate,
            EndDate = parsed.EndDate,
            FileName = req.FileName.Trim(),
            Format = statementFormat,
            OpeningBalance = parsed.OpeningBalance,
            ClosingBalance = parsed.ClosingBalance,
            TotalDebits = parsed.TotalDebits,
            TotalCredits = parsed.TotalCredits,
            TotalTransactions = parsed.Items.Count,
            Status = BankStatementStatus.Parsed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BankStatements.Add(statement);

        foreach (var item in parsed.Items)
        {
            var (category, matchStatus) = BankReconciliationEngine.CategorizeTransaction(item.Narration, item.Type, rules);

            var tx = new BankTransaction
            {
                Id = Guid.NewGuid(),
                StatementId = statement.Id,
                BankAccountId = account.Id,
                TransactionDate = item.Date,
                ValueDate = item.ValueDate,
                Narration = item.Narration,
                ReferenceNumber = item.ChqRefNo,
                TransactionType = item.Type,
                Amount = item.Amount,
                RunningBalance = item.Balance,
                Category = category,
                MatchStatus = matchStatus,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.BankTransactions.Add(tx);
        }

        // Update current balance of bank account to latest statement balance
        account.CurrentBalance = parsed.ClosingBalance;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ToDto(statement);
    }

    public async Task<IReadOnlyList<BankStatementDto>> GetStatementsAsync(Guid accountId, CancellationToken ct = default)
    {
        var list = await _db.BankStatements
            .AsNoTracking()
            .Where(s => s.BankAccountId == accountId)
            .OrderByDescending(s => s.EndDate)
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<BankTransactionDto>> GetTransactionsAsync(
        Guid accountId,
        Guid? statementId,
        string? matchStatus,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default)
    {
        var query = _db.BankTransactions.AsNoTracking().Where(t => t.BankAccountId == accountId).AsQueryable();

        if (statementId.HasValue)
            query = query.Where(t => t.StatementId == statementId.Value);

        if (!string.IsNullOrWhiteSpace(matchStatus))
            query = query.Where(t => t.MatchStatus == matchStatus);

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.TransactionDate <= toDate.Value);

        var list = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<BankTransactionDto> MatchTransactionAsync(Guid transactionId, MatchTransactionRequest req, CancellationToken ct = default)
    {
        var tx = await _db.BankTransactions.FirstOrDefaultAsync(t => t.Id == transactionId, ct)
            ?? throw new KeyNotFoundException($"Bank transaction with ID '{transactionId}' was not found.");

        if (!BankMatchStatus.All.Contains(req.MatchStatus))
            throw new ArgumentException($"Invalid match status. Allowed: {string.Join(", ", BankMatchStatus.All)}");

        if (!string.IsNullOrWhiteSpace(req.MatchedEntityType) && !BankMatchedEntityType.All.Contains(req.MatchedEntityType))
            throw new ArgumentException($"Invalid matched entity type. Allowed: {string.Join(", ", BankMatchedEntityType.All)}");

        tx.MatchStatus = req.MatchStatus;
        tx.MatchedEntityType = string.IsNullOrWhiteSpace(req.MatchedEntityType) ? null : req.MatchedEntityType;
        tx.MatchedEntityId = req.MatchedEntityId;
        tx.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();

        await _db.SaveChangesAsync(ct);
        return ToDto(tx);
    }

    public async Task<BankReconciliationSummaryDto> GetBrsSummaryAsync(Guid accountId, DateOnly? asOfDate, CancellationToken ct = default)
    {
        var account = await _db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(b => b.Id == accountId, ct)
            ?? throw new KeyNotFoundException($"Bank account with ID '{accountId}' was not found.");

        var targetDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var transactions = await _db.BankTransactions
            .AsNoTracking()
            .Where(t => t.BankAccountId == accountId && t.TransactionDate <= targetDate)
            .ToListAsync(ct);

        return BankReconciliationEngine.CalculateBrsSummary(account, transactions, targetDate);
    }

    public async Task<IReadOnlyList<BankReconciliationRuleDto>> GetRulesAsync(CancellationToken ct = default)
    {
        var rules = await _db.BankReconciliationRules
            .AsNoTracking()
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.RuleName)
            .ToListAsync(ct);

        return rules.Select(ToDto).ToList();
    }

    public async Task<BankReconciliationRuleDto> CreateRuleAsync(CreateBankReconciliationRuleRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.RuleName))
            throw new ArgumentException("Rule name is required.");
        if (string.IsNullOrWhiteSpace(req.MatchKeyword))
            throw new ArgumentException("Match keyword is required.");
        if (string.IsNullOrWhiteSpace(req.AssignCategory))
            throw new ArgumentException("Assign category is required.");

        var rule = new BankReconciliationRule
        {
            Id = Guid.NewGuid(),
            RuleName = req.RuleName.Trim(),
            MatchKeyword = req.MatchKeyword.Trim(),
            TransactionType = string.IsNullOrWhiteSpace(req.TransactionType) ? "Any" : req.TransactionType.Trim(),
            AssignCategory = req.AssignCategory.Trim(),
            Priority = req.Priority,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BankReconciliationRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return ToDto(rule);
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _db.BankReconciliationRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Rule with ID '{id}' was not found.");

        _db.BankReconciliationRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    private static BankAccountDto ToDto(BankAccount a) => new(
        a.Id,
        a.ClientId,
        a.Client?.Name ?? "Unknown Client",
        a.AccountName,
        a.BankName,
        a.BranchName,
        a.AccountNumber,
        a.IfscCode,
        a.AccountType,
        a.OpeningBalance,
        a.CurrentBalance,
        a.Currency,
        a.IsActive,
        a.CreatedAt);

    private static BankStatementDto ToDto(BankStatement s) => new(
        s.Id,
        s.BankAccountId,
        s.StatementDate,
        s.StartDate,
        s.EndDate,
        s.FileName,
        s.Format,
        s.OpeningBalance,
        s.ClosingBalance,
        s.TotalDebits,
        s.TotalCredits,
        s.TotalTransactions,
        s.Status,
        s.CreatedAt);

    private static BankTransactionDto ToDto(BankTransaction t) => new(
        t.Id,
        t.StatementId,
        t.BankAccountId,
        t.TransactionDate,
        t.ValueDate,
        t.Narration,
        t.ReferenceNumber,
        t.TransactionType,
        t.Amount,
        t.RunningBalance,
        t.Category,
        t.MatchStatus,
        t.MatchedEntityType,
        t.MatchedEntityId,
        t.Notes);

    private static BankReconciliationRuleDto ToDto(BankReconciliationRule r) => new(
        r.Id,
        r.RuleName,
        r.MatchKeyword,
        r.TransactionType,
        r.AssignCategory,
        r.Priority,
        r.IsActive);
}
