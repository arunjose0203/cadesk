using System.Text.RegularExpressions;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.TrialBalance;
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

    // =========================================================================
    // Forensic Fraud Detection & Statutory Compliance Engine
    // =========================================================================

    public async Task<ForensicScanSummaryDto> RunForensicScanAsync(
        Guid bankAccountId,
        RunForensicScanRequest req,
        CancellationToken ct = default)
    {
        var account = await _db.BankAccounts
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == bankAccountId, ct)
            ?? throw new KeyNotFoundException($"Bank account {bankAccountId} not found.");

        var query = _db.BankTransactions
            .Where(t => t.BankAccountId == bankAccountId);

        if (req.FromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= req.FromDate.Value);
        if (req.ToDate.HasValue)
            query = query.Where(t => t.TransactionDate <= req.ToDate.Value);

        var transactions = await query.OrderBy(t => t.TransactionDate).ToListAsync(ct);

        // Run detection rules
        var anomalies = new List<ForensicAnomalyItem>();
        anomalies.AddRange(ForensicAuditEngine.ScanSec40A3(transactions, account.FirmId, bankAccountId));
        anomalies.AddRange(ForensicAuditEngine.ScanSec269St(transactions, account.FirmId, bankAccountId));
        anomalies.AddRange(ForensicAuditEngine.ScanSec269SsT(transactions, account.FirmId, bankAccountId));
        anomalies.AddRange(ForensicAuditEngine.ScanRoundTripping(transactions, account.FirmId, bankAccountId));
        anomalies.AddRange(ForensicAuditEngine.ScanSmurfing(transactions, account.FirmId, bankAccountId));
        anomalies.AddRange(ForensicAuditEngine.ScanDormancySurge(transactions, account.FirmId, bankAccountId));
        anomalies.AddRange(ForensicAuditEngine.ScanHighValueRoundSum(transactions, account.FirmId, bankAccountId));

        if (req.OverwriteExisting)
        {
            var existing = await _db.ForensicAnomalies
                .Where(a => a.BankAccountId == bankAccountId)
                .ToListAsync(ct);
            _db.ForensicAnomalies.RemoveRange(existing);
        }

        _db.ForensicAnomalies.AddRange(anomalies);
        await _db.SaveChangesAsync(ct);

        return BuildScanSummary(account, transactions.Count, anomalies);
    }

    public async Task<ForensicScanSummaryDto> GetForensicReportAsync(
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        var account = await _db.BankAccounts
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == bankAccountId, ct)
            ?? throw new KeyNotFoundException($"Bank account {bankAccountId} not found.");

        var totalTxs = await _db.BankTransactions
            .CountAsync(t => t.BankAccountId == bankAccountId, ct);

        var anomalies = await _db.ForensicAnomalies
            .Where(a => a.BankAccountId == bankAccountId)
            .OrderByDescending(a => a.TransactionDate)
            .ToListAsync(ct);

        return BuildScanSummary(account, totalTxs, anomalies);
    }

    public async Task<ForensicAnomalyDto> UpdateAnomalyStatusAsync(
        Guid anomalyId,
        UpdateAnomalyStatusRequest req,
        CancellationToken ct = default)
    {
        var anomaly = await _db.ForensicAnomalies
            .FirstOrDefaultAsync(a => a.Id == anomalyId, ct)
            ?? throw new KeyNotFoundException($"Forensic anomaly {anomalyId} not found.");

        anomaly.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.AuditorNotes))
            anomaly.AuditorNotes = req.AuditorNotes;

        anomaly.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(anomaly);
    }

    public async Task<ClientTbBankReconciliationResultDto> ReconcileWithTrialBalanceAsync(
        Guid clientId,
        Guid? trialBalanceImportId = null,
        CancellationToken ct = default)
    {
        var bankAccounts = await _db.BankAccounts
            .Where(b => b.ClientId == clientId && b.IsActive)
            .ToListAsync(ct);

        // Fetch Trial Balance import
        TrialBalanceImport? tbImport = null;
        if (trialBalanceImportId.HasValue)
        {
            tbImport = await _db.TrialBalanceImports
                .FirstOrDefaultAsync(i => i.Id == trialBalanceImportId.Value, ct);
        }
        else
        {
            tbImport = await _db.TrialBalanceImports
                .Where(i => i.ClientId == clientId)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        List<TrialBalanceLedger> tbLedgers = [];
        if (tbImport is not null)
        {
            tbLedgers = await _db.TrialBalanceLedgers
                .Where(l => l.ImportId == tbImport.Id &&
                           (l.Schedule3Group == TrialBalanceConstants.S3CashAndBank ||
                            l.TallyGroup.Contains("Bank") ||
                            l.LedgerName.Contains("Bank") ||
                            l.LedgerName.Contains("A/c")))
                .ToListAsync(ct);
        }

        var matches = new List<TrialBalanceBankMatchDto>();
        var matchedLedgerIds = new HashSet<Guid>();

        foreach (var acc in bankAccounts)
        {
            // Match by account number or bank name in ledger
            var last4 = acc.AccountNumber.Length >= 4 ? acc.AccountNumber[^4..] : acc.AccountNumber;
            var matchedLedger = tbLedgers.FirstOrDefault(l =>
                !matchedLedgerIds.Contains(l.Id) &&
                (l.LedgerName.Contains(acc.AccountNumber, StringComparison.OrdinalIgnoreCase) ||
                 l.LedgerName.Contains(last4, StringComparison.OrdinalIgnoreCase) ||
                 l.LedgerName.Contains(acc.BankName, StringComparison.OrdinalIgnoreCase) ||
                 l.LedgerName.Contains(acc.AccountName, StringComparison.OrdinalIgnoreCase)));

            if (matchedLedger is not null)
            {
                matchedLedgerIds.Add(matchedLedger.Id);
                var diff = acc.CurrentBalance - matchedLedger.ClosingBalance;
                matches.Add(new TrialBalanceBankMatchDto(
                    acc.Id,
                    acc.AccountName,
                    acc.BankName,
                    acc.AccountNumber,
                    acc.CurrentBalance,
                    matchedLedger.ClosingBalance,
                    matchedLedger.LedgerName,
                    diff,
                    IsUnrecordedAccount: false,
                    IsMatched: true
                ));
            }
            else
            {
                // Unrecorded bank account in books!
                matches.Add(new TrialBalanceBankMatchDto(
                    acc.Id,
                    acc.AccountName,
                    acc.BankName,
                    acc.AccountNumber,
                    acc.CurrentBalance,
                    TbLedgerBalance: null,
                    TbLedgerName: null,
                    VarianceAmount: acc.CurrentBalance,
                    IsUnrecordedAccount: true,
                    IsMatched: false
                ));
            }
        }

        var totalBankBal = bankAccounts.Sum(b => b.CurrentBalance);
        var totalTbBal = tbLedgers.Sum(l => l.ClosingBalance);
        var unrecordedCount = matches.Count(m => m.IsUnrecordedAccount);
        var matchedCount = matches.Count(m => m.IsMatched);

        return new ClientTbBankReconciliationResultDto(
            clientId,
            bankAccounts.Count,
            tbLedgers.Count,
            matchedCount,
            unrecordedCount,
            totalBankBal,
            totalTbBal,
            totalBankBal - totalTbBal,
            matches
        );
    }

    public async Task<ForensicReportExportDto> ExportForensicReportHtmlAsync(
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        var summary = await GetForensicReportAsync(bankAccountId, ct);
        var account = await _db.BankAccounts.Include(b => b.Client).FirstOrDefaultAsync(b => b.Id == bankAccountId, ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine($"<title>Forensic Bank Statement Audit Report - {summary.BankName} ({summary.AccountNumber})</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.5; color: #1e293b; margin: 40px; background: #fff; }");
        sb.AppendLine("h1, h2, h3 { color: #0f172a; margin-top: 1.5em; }");
        sb.AppendLine(".header { border-bottom: 2px solid #dc2626; padding-bottom: 16px; margin-bottom: 24px; display: flex; justify-content: space-between; }");
        sb.AppendLine(".badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; font-size: 12px; font-weight: 700; }");
        sb.AppendLine(".badge-critical { background: #fee2e2; color: #dc2626; }");
        sb.AppendLine(".badge-high { background: #ffedd5; color: #ea580c; }");
        sb.AppendLine(".badge-medium { background: #fef3c7; color: #b45309; }");
        sb.AppendLine(".kpi-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin: 20px 0; }");
        sb.AppendLine(".kpi-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 16px 0; font-size: 13px; }");
        sb.AppendLine("th, td { border: 1px solid #e2e8f0; padding: 8px 12px; text-align: left; }");
        sb.AppendLine("th { background: #f1f5f9; font-weight: 600; }");
        sb.AppendLine(".text-right { text-align: right; }");
        sb.AppendLine("</style></head><body>");

        // Header
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine("  <div>");
        sb.AppendLine("    <h1>Forensic Bank Audit &amp; Statutory Violation Report</h1>");
        sb.AppendLine($"    <p style=\"font-size: 16px; margin: 4px 0;\"><strong>Client:</strong> {account?.Client?.Name} | <strong>Bank:</strong> {summary.BankName} ({summary.AccountName})</p>");
        sb.AppendLine($"    <p style=\"color: #64748b; margin: 0;\">Account Number: {summary.AccountNumber} • Scanned: {summary.ScannedAt:dd-MMM-yyyy HH:mm} UTC</p>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div style=\"text-align: right;\">");
        sb.AppendLine($"    <div style=\"font-size: 12px; color: #64748b; font-weight: 600;\">FORENSIC RISK SCORE</div>");
        sb.AppendLine($"    <div style=\"font-size: 28px; font-weight: 800; color: {(summary.OverallRiskScore >= 50 ? "#dc2626" : summary.OverallRiskScore >= 20 ? "#ea580c" : "#16a34a")};\">{summary.OverallRiskScore} / 100</div>");
        sb.AppendLine($"    <span class=\"badge {(summary.OverallRiskScore >= 50 ? "badge-critical" : summary.OverallRiskScore >= 20 ? "badge-high" : "badge-medium")}\">{summary.RiskCategory.ToUpper()} RISK</span>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        // KPI Exposure Grid
        sb.AppendLine("<div class=\"kpi-grid\">");
        sb.AppendLine($"  <div class=\"kpi-card\"><div style=\"font-size: 11px; color: #64748b; font-weight: 600;\">SEC 40A(3) CASH EXPOSURE</div><div style=\"font-size: 18px; font-weight: 700; color: #dc2626;\">₹{summary.Sec40A3ExposureAmount:N0}</div><small style=\"color: #64748b;\">100% P&amp;L Disallowance</small></div>");
        sb.AppendLine($"  <div class=\"kpi-card\"><div style=\"font-size: 11px; color: #64748b; font-weight: 600;\">SEC 269ST CASH PENALTY</div><div style=\"font-size: 18px; font-weight: 700; color: #dc2626;\">₹{summary.Sec269StExposureAmount:N0}</div><small style=\"color: #64748b;\">100% Sec 271DA Penalty</small></div>");
        sb.AppendLine($"  <div class=\"kpi-card\"><div style=\"font-size: 11px; color: #64748b; font-weight: 600;\">ROUND-TRIPPING PAIRS</div><div style=\"font-size: 18px; font-weight: 700; color: #ea580c;\">{summary.RoundTrippingCount}</div><small style=\"color: #64748b;\">CARO 2020 Clause (x)</small></div>");
        sb.AppendLine($"  <div class=\"kpi-card\"><div style=\"font-size: 11px; color: #64748b; font-weight: 600;\">SMURFING / STRUCTURING</div><div style=\"font-size: 18px; font-weight: 700; color: #ea580c;\">{summary.SmurfingCount}</div><small style=\"color: #64748b;\">Split Cash Transactions</small></div>");
        sb.AppendLine("</div>");

        // Anomalies Table
        sb.AppendLine("<h2>Forensic Exceptions &amp; Statutory Non-Compliance Register</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Date</th><th>Statutory Section / Rule</th><th>Risk</th><th>Counterparty</th><th class=\"text-right\">Amount (₹)</th><th class=\"text-right\">Exposure (₹)</th><th>Auditor Action &amp; Remediation</th><th>Status</th></tr>");
        foreach (var a in summary.Anomalies)
        {
            var riskBadge = a.RiskLevel == ForensicRiskLevel.Critical ? "badge-critical" : a.RiskLevel == ForensicRiskLevel.High ? "badge-high" : "badge-medium";
            sb.AppendLine($"<tr>");
            sb.AppendLine($"  <td><strong>{a.TransactionDate:dd-MMM-yyyy}</strong></td>");
            sb.AppendLine($"  <td><code>{a.StatutorySection}</code><br><small style=\"color: #64748b;\">{a.Narration}</small></td>");
            sb.AppendLine($"  <td><span class=\"badge {riskBadge}\">{a.RiskLevel.ToUpper()}</span></td>");
            sb.AppendLine($"  <td>{a.Counterparty}</td>");
            sb.AppendLine($"  <td class=\"text-right\">₹{a.Amount:N0}</td>");
            sb.AppendLine($"  <td class=\"text-right\"><strong>₹{a.ExposureAmount:N0}</strong></td>");
            sb.AppendLine($"  <td>{a.RecommendedAction}</td>");
            sb.AppendLine($"  <td>{a.Status}</td>");
            sb.AppendLine($"</tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("</body></html>");

        return new ForensicReportExportDto(sb.ToString(), summary.OverallRiskScore, DateTime.UtcNow);
    }

    private static ForensicScanSummaryDto BuildScanSummary(
        BankAccount account,
        int totalTransactions,
        List<ForensicAnomalyItem> anomalies)
    {
        var sec40A3 = anomalies
            .Where(a => a.AnomalyType == ForensicAnomalyType.Sec40A3CashPayment)
            .Sum(a => a.ExposureAmount);

        var sec269St = anomalies
            .Where(a => a.AnomalyType == ForensicAnomalyType.Sec269StCashReceipt)
            .Sum(a => a.ExposureAmount);

        var sec269SsT = anomalies
            .Where(a => a.AnomalyType is ForensicAnomalyType.Sec269SsLoanAccepted or ForensicAnomalyType.Sec269TLoanRepaid)
            .Sum(a => a.ExposureAmount);

        var roundTripping = anomalies.Count(a => a.AnomalyType == ForensicAnomalyType.RoundTripping);
        var smurfing = anomalies.Count(a => a.AnomalyType == ForensicAnomalyType.SmurfingStructuring);
        var dormancy = anomalies.Count(a => a.AnomalyType == ForensicAnomalyType.DormancySurge);
        var roundSum = anomalies.Count(a => a.AnomalyType == ForensicAnomalyType.HighValueRoundSum);

        var critical = anomalies.Count(a => a.RiskLevel == ForensicRiskLevel.Critical);
        var high = anomalies.Count(a => a.RiskLevel == ForensicRiskLevel.High);
        var medium = anomalies.Count(a => a.RiskLevel == ForensicRiskLevel.Medium);

        var score = ForensicAuditEngine.CalculateOverallRiskScore(anomalies);
        var category = score >= 50 ? "Severe" : score >= 20 ? "Moderate" : "Low";

        return new ForensicScanSummaryDto(
            account.Id,
            account.AccountName,
            account.BankName,
            account.AccountNumber,
            totalTransactions,
            anomalies.Count,
            sec40A3,
            sec269St,
            sec269SsT,
            roundTripping,
            smurfing,
            dormancy,
            roundSum,
            critical,
            high,
            medium,
            score,
            category,
            anomalies.Select(ToDto).ToList(),
            DateTime.UtcNow
        );
    }

    private static ForensicAnomalyDto ToDto(ForensicAnomalyItem a) => new(
        a.Id,
        a.BankAccountId,
        a.TransactionId,
        a.TransactionDate,
        a.AnomalyType,
        a.RiskLevel,
        a.Amount,
        a.ExposureAmount,
        a.Counterparty,
        a.Narration,
        a.StatutorySection,
        a.RecommendedAction,
        a.AuditorNotes,
        a.Status,
        a.CreatedAt
    );
}
