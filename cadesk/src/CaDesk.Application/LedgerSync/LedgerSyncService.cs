using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Features;
using CaDesk.Application.TrialBalance;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.Features;
using CaDesk.Domain.LedgerSync;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.LedgerSync;

public class LedgerSyncService : ILedgerSyncService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TrialBalanceService? _trialBalance;
    private readonly IFeatureFlagService? _featureFlags;

    public LedgerSyncService(
        IAppDbContext db,
        ITenantContext tenant,
        TrialBalanceService? trialBalance = null,
        IFeatureFlagService? featureFlags = null)
    {
        _db = db;
        _tenant = tenant;
        _trialBalance = trialBalance;
        _featureFlags = featureFlags;
    }

    // 1. Agent Configuration & Diagnostics
    public async Task<List<ErpSyncAgentConfigDto>> GetAgentConfigsAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var query = _db.ErpSyncAgentConfigs.Include(x => x.Client).AsQueryable();
        if (clientId.HasValue && clientId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToAgentConfigDto).ToList();
    }

    public async Task<ErpSyncAgentConfigDto?> GetAgentConfigByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.ErpSyncAgentConfigs.Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : ToAgentConfigDto(item);
    }

    public async Task<ErpSyncAgentConfigDto> SaveAgentConfigAsync(CreateOrUpdateAgentConfigRequest req, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found");

        ErpSyncAgentConfig entity;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.ErpSyncAgentConfigs.Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct)
                ?? throw new KeyNotFoundException("Agent config not found");
        }
        else
        {
            string token = "ca_sync_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            entity = new ErpSyncAgentConfig
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                AgentToken = token,
                SyncStatus = AgentSyncStatus.Offline
            };
            _db.ErpSyncAgentConfigs.Add(entity);
        }

        entity.AgentName = req.AgentName;
        entity.ErpType = req.ErpType;
        entity.HostUrl = req.HostUrl;
        entity.CompanyName = req.CompanyName;
        entity.SyncFrequencyMinutes = req.SyncFrequencyMinutes > 0 ? req.SyncFrequencyMinutes : 15;
        entity.SyncDaybook = req.SyncDaybook;
        entity.SyncMasters = req.SyncMasters;
        entity.SyncBankFeeds = req.SyncBankFeeds;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToAgentConfigDto(entity, client.Name, client.Code);
    }

    public async Task<bool> DeleteAgentConfigAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.ErpSyncAgentConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return false;

        _db.ErpSyncAgentConfigs.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RecordAgentHeartbeatAsync(AgentHeartbeatRequest req, CancellationToken ct = default)
    {
        var agent = await ResolveAgentByTokenOrNullAsync(req.AgentToken, ct);
        if (agent is null) return false;

        agent.SyncStatus = req.Status;
        agent.LastHeartbeatAt = DateTimeOffset.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // 2. Inbound Voucher Sync
    public async Task<SyncVouchersResultDto> BatchSyncVouchersAsync(BatchSyncVouchersRequest req, CancellationToken ct = default)
    {
        var agent = await ResolveAgentByTokenAsync(req.AgentToken, ct);

        int created = 0;
        int updated = 0;

        foreach (var v in req.Vouchers)
        {
            var existing = await _db.ErpLedgerVouchers.FirstOrDefaultAsync(x =>
                x.ClientId == agent.ClientId &&
                (x.VoucherNumber == v.VoucherNumber || (!string.IsNullOrEmpty(v.VoucherGuid) && x.VoucherGuid == v.VoucherGuid)), ct);

            if (existing is null)
            {
                var newVoucher = new ErpLedgerVoucher
                {
                    Id = Guid.NewGuid(),
                    FirmId = agent.FirmId,
                    ClientId = agent.ClientId,
                    AgentConfigId = agent.Id,
                    ErpType = agent.ErpType,
                    VoucherNumber = v.VoucherNumber,
                    VoucherGuid = v.VoucherGuid,
                    VoucherType = v.VoucherType,
                    VoucherDate = v.VoucherDate,
                    EffectiveDate = v.EffectiveDate,
                    Amount = v.Amount,
                    DebitLedger = v.DebitLedger,
                    CreditLedger = v.CreditLedger,
                    Narration = v.Narration,
                    ChequeNumber = v.ChequeNumber,
                    InstrumentDate = v.InstrumentDate,
                    BankPartyName = v.BankPartyName,
                    ReconciliationStatus = VoucherReconciliationStatus.Unreconciled
                };
                _db.ErpLedgerVouchers.Add(newVoucher);
                created++;
            }
            else
            {
                existing.VoucherDate = v.VoucherDate;
                existing.EffectiveDate = v.EffectiveDate;
                existing.Amount = v.Amount;
                existing.DebitLedger = v.DebitLedger;
                existing.CreditLedger = v.CreditLedger;
                existing.Narration = v.Narration;
                existing.ChequeNumber = v.ChequeNumber;
                existing.InstrumentDate = v.InstrumentDate;
                existing.BankPartyName = v.BankPartyName;
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        agent.TotalVouchersSynced += created;
        agent.LastSyncAt = DateTimeOffset.UtcNow;
        agent.SyncStatus = AgentSyncStatus.Connected;
        agent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new SyncVouchersResultDto(
            TotalProcessed: req.Vouchers.Count,
            CreatedCount: created,
            UpdatedCount: updated,
            StatusMessage: $"Batch synced {req.Vouchers.Count} vouchers from {agent.CompanyName} ({agent.ErpType})"
        );
    }

    public async Task<List<ErpLedgerVoucherDto>> GetVouchersAsync(Guid? clientId = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default)
    {
        var query = _db.ErpLedgerVouchers.Include(x => x.Client).AsQueryable();

        if (clientId.HasValue && clientId.Value != Guid.Empty)
            query = query.Where(x => x.ClientId == clientId.Value);

        if (fromDate.HasValue)
            query = query.Where(x => x.VoucherDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.VoucherDate <= toDate.Value);

        var list = await query.OrderByDescending(x => x.VoucherDate).Take(300).ToListAsync(ct);

        return list.Select(v => new ErpLedgerVoucherDto(
            Id: v.Id,
            ClientId: v.ClientId,
            ClientName: v.Client?.Name ?? "Client",
            AgentConfigId: v.AgentConfigId,
            ErpType: v.ErpType,
            VoucherNumber: v.VoucherNumber,
            VoucherGuid: v.VoucherGuid,
            VoucherType: v.VoucherType,
            VoucherDate: v.VoucherDate,
            EffectiveDate: v.EffectiveDate,
            Amount: v.Amount,
            DebitLedger: v.DebitLedger,
            CreditLedger: v.CreditLedger,
            Narration: v.Narration,
            ChequeNumber: v.ChequeNumber,
            InstrumentDate: v.InstrumentDate,
            BankPartyName: v.BankPartyName,
            BankClearanceDate: v.BankClearanceDate,
            ReconciliationStatus: v.ReconciliationStatus,
            CreatedAt: v.CreatedAt
        )).ToList();
    }

    // 3. 2-Way Outgoing Vouchers (Push Adjustments)
    public async Task<ErpOutgoingVoucherDto> CreateOutgoingVoucherAsync(CreateOutgoingVoucherRequest req, CancellationToken ct = default)
    {
        var agent = await _db.ErpSyncAgentConfigs.Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == req.AgentConfigId, ct)
            ?? throw new KeyNotFoundException("ERP Sync Agent not found");

        string xmlPayload = TallyXmlProtocol.GenerateImportJournalVoucherXml(
            companyName: agent.CompanyName,
            voucherType: req.VoucherType.ToString(),
            date: req.VoucherDate,
            reference: req.ReferenceNumber,
            amount: req.Amount,
            debitLedger: req.DebitLedger,
            creditLedger: req.CreditLedger,
            narration: $"{req.Narration} [{req.AuditReason}]"
        );

        var entity = new ErpOutgoingVoucher
        {
            Id = Guid.NewGuid(),
            FirmId = _tenant.FirmId,
            ClientId = req.ClientId,
            AgentConfigId = req.AgentConfigId,
            VoucherType = req.VoucherType,
            VoucherDate = req.VoucherDate,
            ReferenceNumber = req.ReferenceNumber,
            DebitLedger = req.DebitLedger,
            CreditLedger = req.CreditLedger,
            Amount = req.Amount,
            Narration = req.Narration,
            AuditReason = req.AuditReason,
            PostingStatus = OutgoingVoucherStatus.PendingApproval,
            GeneratedXmlPayload = xmlPayload
        };

        _db.ErpOutgoingVouchers.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ToOutgoingVoucherDto(entity, agent.Client?.Name ?? "Client");
    }

    public async Task<ErpOutgoingVoucherDto> AuthorizeOutgoingVoucherAsync(Guid id, CancellationToken ct = default)
    {
        var voucher = await _db.ErpOutgoingVouchers.Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Outgoing voucher not found");

        voucher.PostingStatus = OutgoingVoucherStatus.QueuedForAgent;
        voucher.AuthorizedByUserId = _tenant.UserId;
        voucher.AuthorizedByName = "Audit Partner";
        voucher.AuthorizedAt = DateTimeOffset.UtcNow;
        voucher.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToOutgoingVoucherDto(voucher, voucher.Client?.Name ?? "Client");
    }

    public async Task<List<ErpOutgoingVoucherDto>> GetOutgoingVouchersAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var query = _db.ErpOutgoingVouchers.Include(x => x.Client).AsQueryable();
        if (clientId.HasValue && clientId.Value != Guid.Empty)
            query = query.Where(x => x.ClientId == clientId.Value);

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(x => ToOutgoingVoucherDto(x, x.Client?.Name ?? "Client")).ToList();
    }

    public async Task<List<ErpOutgoingVoucherDto>> GetPendingOutgoingQueueForAgentAsync(string agentToken, CancellationToken ct = default)
    {
        var agent = await ResolveAgentByTokenAsync(agentToken, ct);

        var list = await _db.ErpOutgoingVouchers
            .Include(x => x.Client)
            .Where(x => x.AgentConfigId == agent.Id && x.PostingStatus == OutgoingVoucherStatus.QueuedForAgent)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        return list.Select(x => ToOutgoingVoucherDto(x, x.Client?.Name ?? "Client")).ToList();
    }

    public async Task<bool> ReportOutgoingVoucherStatusAsync(ReportOutgoingVoucherStatusRequest req, CancellationToken ct = default)
    {
        var agent = await ResolveAgentByTokenAsync(req.AgentToken, ct);

        var voucher = await _db.ErpOutgoingVouchers.FirstOrDefaultAsync(x => x.Id == req.VoucherId && x.AgentConfigId == agent.Id, ct);
        if (voucher is null) return false;

        voucher.PostingStatus = req.Status;
        voucher.ErpVoucherNumber = req.ErpVoucherNumber;
        voucher.ErpResponseXml = req.ErpResponseXml;
        voucher.ErrorMessage = req.ErrorMessage;
        voucher.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // 4. AI Bank Feed Discrepancy Matching & Auto-Reconciliation
    public async Task<List<BankLedgerReconciliationMatchDto>> RunAutoReconciliationAsync(RunAutoReconciliationRequest req, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found");

        // Load unmatched bank transactions
        var txQuery = _db.BankTransactions
            .Where(x => x.BankAccountId == req.BankAccountId && x.MatchStatus != BankMatchStatus.Matched)
            .AsQueryable();

        if (req.FromDate.HasValue) txQuery = txQuery.Where(x => x.TransactionDate >= req.FromDate.Value);
        if (req.ToDate.HasValue) txQuery = txQuery.Where(x => x.TransactionDate <= req.ToDate.Value);

        var bankTxs = await txQuery.OrderBy(x => x.TransactionDate).ToListAsync(ct);

        // Load open ledger vouchers for this client
        var openVouchers = await _db.ErpLedgerVouchers
            .Where(x => x.ClientId == req.ClientId && x.ReconciliationStatus == VoucherReconciliationStatus.Unreconciled)
            .OrderBy(x => x.VoucherDate)
            .ToListAsync(ct);

        var matches = new List<BankLedgerReconciliationMatch>();

        foreach (var tx in bankTxs)
        {
            var candidate = AiBankLedgerReconciliationEngine.FindBestMatch(tx, openVouchers);
            if (candidate is null) continue;

            // Check if existing match exists
            var match = await _db.BankLedgerReconciliationMatches
                .FirstOrDefaultAsync(x => x.BankTransactionId == tx.Id, ct);

            if (match is null)
            {
                match = new BankLedgerReconciliationMatch
                {
                    Id = Guid.NewGuid(),
                    FirmId = _tenant.FirmId,
                    ClientId = req.ClientId,
                    BankAccountId = req.BankAccountId,
                    BankTransactionId = tx.Id,
                    ErpLedgerVoucherId = candidate.Voucher.Id,
                    MatchRule = candidate.Rule,
                    ConfidenceScore = candidate.ConfidenceScore,
                    BankAmount = Math.Abs(tx.Amount),
                    LedgerAmount = candidate.Voucher.Amount,
                    VarianceAmount = candidate.Variance,
                    VarianceExplanation = candidate.Explanation,
                    MatchedVoucherNumbers = candidate.Voucher.VoucherNumber,
                    Status = ReconciliationMatchStatus.Suggested
                };
                _db.BankLedgerReconciliationMatches.Add(match);
            }
            else if (match.Status == ReconciliationMatchStatus.Suggested)
            {
                match.ErpLedgerVoucherId = candidate.Voucher.Id;
                match.MatchRule = candidate.Rule;
                match.ConfidenceScore = candidate.ConfidenceScore;
                match.BankAmount = Math.Abs(tx.Amount);
                match.LedgerAmount = candidate.Voucher.Amount;
                match.VarianceAmount = candidate.Variance;
                match.VarianceExplanation = candidate.Explanation;
                match.MatchedVoucherNumbers = candidate.Voucher.VoucherNumber;
                match.UpdatedAt = DateTime.UtcNow;
            }

            matches.Add(match);
        }

        await _db.SaveChangesAsync(ct);

        return await GetReconciliationMatchesAsync(req.ClientId, req.BankAccountId, ct);
    }

    public async Task<List<BankLedgerReconciliationMatchDto>> GetReconciliationMatchesAsync(Guid? clientId = null, Guid? bankAccountId = null, CancellationToken ct = default)
    {
        var query = _db.BankLedgerReconciliationMatches.AsQueryable();

        if (clientId.HasValue && clientId.Value != Guid.Empty)
            query = query.Where(x => x.ClientId == clientId.Value);

        if (bankAccountId.HasValue && bankAccountId.Value != Guid.Empty)
            query = query.Where(x => x.BankAccountId == bankAccountId.Value);

        var list = await query.OrderByDescending(x => x.ConfidenceScore).ToListAsync(ct);
        var dtos = new List<BankLedgerReconciliationMatchDto>();

        foreach (var m in list)
        {
            var tx = await _db.BankTransactions.FirstOrDefaultAsync(x => x.Id == m.BankTransactionId, ct);
            var vch = m.ErpLedgerVoucherId.HasValue ? await _db.ErpLedgerVouchers.FirstOrDefaultAsync(x => x.Id == m.ErpLedgerVoucherId.Value, ct) : null;
            var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == m.ClientId, ct);

            dtos.Add(new BankLedgerReconciliationMatchDto(
                Id: m.Id,
                ClientId: m.ClientId,
                ClientName: client?.Name ?? "Client",
                BankAccountId: m.BankAccountId,
                BankTransactionId: m.BankTransactionId,
                TransactionDate: tx?.TransactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Narration: tx?.Narration ?? "Bank Entry",
                TransactionType: tx?.TransactionType ?? "Debit",
                ErpLedgerVoucherId: m.ErpLedgerVoucherId,
                ErpVoucherNumber: vch?.VoucherNumber ?? m.MatchedVoucherNumbers,
                VoucherDate: vch?.VoucherDate,
                DebitLedger: vch?.DebitLedger,
                CreditLedger: vch?.CreditLedger,
                MatchRule: m.MatchRule,
                ConfidenceScore: m.ConfidenceScore,
                BankAmount: m.BankAmount,
                LedgerAmount: m.LedgerAmount,
                VarianceAmount: m.VarianceAmount,
                VarianceExplanation: m.VarianceExplanation,
                MatchedVoucherNumbers: m.MatchedVoucherNumbers,
                Status: m.Status,
                ReconciledByName: m.ReconciledByName,
                ReconciledAt: m.ReconciledAt,
                CreatedAt: m.CreatedAt
            ));
        }

        return dtos;
    }

    public async Task<BankLedgerReconciliationMatchDto> ProcessReconciliationActionAsync(Guid matchId, ReconcileMatchActionRequest req, CancellationToken ct = default)
    {
        var match = await _db.BankLedgerReconciliationMatches.FirstOrDefaultAsync(x => x.Id == matchId, ct)
            ?? throw new KeyNotFoundException("Reconciliation match not found");

        match.Status = req.Status;
        match.ReconciledByUserId = _tenant.UserId;
        match.ReconciledByName = "Audit Associate";
        match.ReconciledAt = DateTimeOffset.UtcNow;
        match.UpdatedAt = DateTime.UtcNow;

        if (req.Status == ReconciliationMatchStatus.Approved)
        {
            // Mark bank transaction as matched
            var tx = await _db.BankTransactions.FirstOrDefaultAsync(x => x.Id == match.BankTransactionId, ct);
            if (tx != null)
            {
                tx.MatchStatus = BankMatchStatus.Matched;
                tx.MatchedEntityType = "ErpLedgerVoucher";
                tx.MatchedEntityId = match.ErpLedgerVoucherId;
            }

            // Mark ERP voucher as reconciled
            if (match.ErpLedgerVoucherId.HasValue)
            {
                var vch = await _db.ErpLedgerVouchers.FirstOrDefaultAsync(x => x.Id == match.ErpLedgerVoucherId.Value, ct);
                if (vch != null)
                {
                    vch.ReconciliationStatus = VoucherReconciliationStatus.AutoMatched;
                    vch.BankClearanceDate = tx?.TransactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                    vch.ReconciliationMatchId = match.Id;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == match.ClientId, ct);
        var refreshedTx = await _db.BankTransactions.FirstOrDefaultAsync(x => x.Id == match.BankTransactionId, ct);
        var refreshedVch = match.ErpLedgerVoucherId.HasValue ? await _db.ErpLedgerVouchers.FirstOrDefaultAsync(x => x.Id == match.ErpLedgerVoucherId.Value, ct) : null;

        return new BankLedgerReconciliationMatchDto(
            Id: match.Id,
            ClientId: match.ClientId,
            ClientName: client?.Name ?? "Client",
            BankAccountId: match.BankAccountId,
            BankTransactionId: match.BankTransactionId,
            TransactionDate: refreshedTx?.TransactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Narration: refreshedTx?.Narration ?? "Bank Entry",
            TransactionType: refreshedTx?.TransactionType ?? "Debit",
            ErpLedgerVoucherId: match.ErpLedgerVoucherId,
            ErpVoucherNumber: refreshedVch?.VoucherNumber ?? match.MatchedVoucherNumbers,
            VoucherDate: refreshedVch?.VoucherDate,
            DebitLedger: refreshedVch?.DebitLedger,
            CreditLedger: refreshedVch?.CreditLedger,
            MatchRule: match.MatchRule,
            ConfidenceScore: match.ConfidenceScore,
            BankAmount: match.BankAmount,
            LedgerAmount: match.LedgerAmount,
            VarianceAmount: match.VarianceAmount,
            VarianceExplanation: match.VarianceExplanation,
            MatchedVoucherNumbers: match.MatchedVoucherNumbers,
            Status: match.Status,
            ReconciledByName: match.ReconciledByName,
            ReconciledAt: match.ReconciledAt,
            CreatedAt: match.CreatedAt
        );
    }

    public async Task<ReconciliationSummaryDto> GetReconciliationSummaryAsync(Guid clientId, Guid bankAccountId, CancellationToken ct = default)
    {
        var txs = await _db.BankTransactions
            .Where(x => x.BankAccountId == bankAccountId)
            .ToListAsync(ct);

        int totalCount = txs.Count;
        int reconciledCount = txs.Count(x => x.MatchStatus == BankMatchStatus.Matched);
        int pendingCount = totalCount - reconciledCount;

        decimal totalReconciledAmt = txs.Where(x => x.MatchStatus == BankMatchStatus.Matched).Sum(x => Math.Abs(x.Amount));
        decimal totalUnreconciledAmt = txs.Where(x => x.MatchStatus != BankMatchStatus.Matched).Sum(x => Math.Abs(x.Amount));
        decimal pct = totalCount > 0 ? Math.Round(((decimal)reconciledCount / totalCount) * 100m, 1) : 0m;

        int highConfidenceCount = await _db.BankLedgerReconciliationMatches
            .CountAsync(x => x.BankAccountId == bankAccountId && x.Status == ReconciliationMatchStatus.Suggested && x.ConfidenceScore >= 90m, ct);

        return new ReconciliationSummaryDto(
            TotalBankTransactions: totalCount,
            ReconciledCount: reconciledCount,
            PendingCount: pendingCount,
            TotalReconciledAmount: totalReconciledAmt,
            TotalUnreconciledAmount: totalUnreconciledAmt,
            ReconciledPercentage: pct,
            HighConfidenceSuggestionsCount: highConfidenceCount
        );
    }

    private static ErpSyncAgentConfigDto ToAgentConfigDto(ErpSyncAgentConfig c) =>
        ToAgentConfigDto(c, c.Client?.Name ?? "Client", c.Client?.Code ?? "");

    private static ErpSyncAgentConfigDto ToAgentConfigDto(ErpSyncAgentConfig c, string clientName, string clientCode)
    {
        return new ErpSyncAgentConfigDto(
            Id: c.Id,
            ClientId: c.ClientId,
            ClientName: clientName,
            ClientCode: clientCode,
            AgentName: c.AgentName,
            AgentToken: c.AgentToken,
            ErpType: c.ErpType,
            HostUrl: c.HostUrl,
            CompanyName: c.CompanyName,
            SyncStatus: c.SyncStatus,
            LastHeartbeatAt: c.LastHeartbeatAt,
            LastSyncAt: c.LastSyncAt,
            SyncFrequencyMinutes: c.SyncFrequencyMinutes,
            SyncDaybook: c.SyncDaybook,
            SyncMasters: c.SyncMasters,
            SyncBankFeeds: c.SyncBankFeeds,
            TotalVouchersSynced: c.TotalVouchersSynced,
            TotalMastersSynced: c.TotalMastersSynced,
            IsActive: c.IsActive,
            CreatedAt: c.CreatedAt
        );
    }

    public async Task<TrialBalanceImportDto> PushAgentTrialBalanceAsync(PushAgentTrialBalanceRequest req, CancellationToken ct = default)
    {
        if (_trialBalance is null)
            throw new InvalidOperationException("TrialBalanceService is unavailable in this scope.");

        var agent = await ResolveAgentByTokenAsync(req.AgentToken, ct);
        string safeCompany = string.Concat(agent.CompanyName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        string fileName = string.IsNullOrWhiteSpace(req.FileName)
            ? $"tally_tb_{safeCompany}_{req.FinancialYear}.xml"
            : req.FileName.Trim();

        var importDto = await _trialBalance.UploadTrialBalanceAsync(new UploadTrialBalanceRequest(
            ClientId: agent.ClientId,
            FinancialYear: string.IsNullOrWhiteSpace(req.FinancialYear) ? "2025-26" : req.FinancialYear.Trim(),
            SourceType: "TallyXml",
            FileName: fileName,
            ContentPayload: req.XmlContent
        ), ct);

        agent.TotalMastersSynced = importDto.TotalLedgersCount;
        agent.LastSyncAt = DateTimeOffset.UtcNow;
        agent.SyncStatus = AgentSyncStatus.Connected;
        agent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return importDto;
    }

    public async Task<WindowsTallyBridgeBundleDto?> BuildWindowsBridgeBundleAsync(Guid agentId, string apiBaseUrl, CancellationToken ct = default)
    {
        var agent = await _db.ErpSyncAgentConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == agentId, ct);
        if (agent is null) return null;

        string cleanApiUrl = apiBaseUrl.TrimEnd('/');
        string cleanTallyUrl = string.IsNullOrWhiteSpace(agent.HostUrl) ? "http://localhost:9000" : agent.HostUrl.TrimEnd('/');
        string safePrefix = string.Concat((agent.CompanyName.Length > 0 ? agent.CompanyName : agent.AgentName)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')).Trim('_');
        if (string.IsNullOrWhiteSpace(safePrefix)) safePrefix = "TallyCompany";

        string scriptFile = $"CaDesk_TallyBridge_{safePrefix}.ps1";
        string configFile = $"appsettings.{safePrefix}.json";

        var cfgObj = new
        {
            apiBaseUrl = cleanApiUrl,
            agentToken = agent.AgentToken,
            tallyHostUrl = cleanTallyUrl,
            companyName = agent.CompanyName,
            financialYear = "2025-26",
            pollIntervalSeconds = Math.Max(30, agent.SyncFrequencyMinutes * 60),
            syncVouchers = agent.SyncDaybook,
            syncTrialBalance = agent.SyncMasters,
            processOutgoingAdjustments = true
        };
        string appSettingsJson = JsonSerializer.Serialize(cfgObj, new JsonSerializerOptions { WriteIndented = true });

        string escapedCompany = System.Security.SecurityElement.Escape(agent.CompanyName) ?? agent.CompanyName;
        string psScript = $$"""
<#
.SYNOPSIS
  CaDesk Lightweight Windows TallyPrime XML Bridge ({{agent.AgentName}})
  Runs locally on Windows and connects TallyPrime XML Gateway ({{cleanTallyUrl}}) -> CaDesk API ({{cleanApiUrl}}).
.NOTES
  1. In TallyPrime: Press F1 -> Settings -> Connectivity -> Client/Server Configuration:
     - TallyPrime acts as: Both (Server + Client)
     - Enable ODBC Server: Yes | Port: 9000
  2. Open your company "{{agent.CompanyName}}" in TallyPrime, then right-click this script -> Run with PowerShell (or run via Task Scheduler).
#>
param(
  [string]$CaDeskUrl = "{{cleanApiUrl}}",
  [string]$AgentToken = "{{agent.AgentToken}}",
  [string]$TallyUrl = "{{cleanTallyUrl}}",
  [string]$CompanyName = "{{agent.CompanyName.Replace("\"", "`\"")}}",
  [string]$FinancialYear = "2025-26",
  [int]$PollSeconds = {{Math.Max(60, agent.SyncFrequencyMinutes * 60)}},
  [switch]$RunOnce
)

$ErrorActionPreference = "Stop"

function Get-FyWindow([string]$fy) {
  $parts = $fy.Split("-")
  $yr = [int]$parts[0]
  return @{ From = "$($yr)0401"; To = "$($yr + 1)0331" }
}

function Invoke-TallyXml([string]$xmlBody) {
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($xmlBody)
  $resp = Invoke-WebRequest -Uri $TallyUrl -Method Post -Body $bytes -ContentType "application/xml" -UseBasicParsing -TimeoutSec 45
  return $resp.Content
}

function Send-CaDeskHeartbeat([string]$status, [string]$errMsg = $null) {
  $body = @{ agentToken = $AgentToken; status = $status; errorMessage = $errMsg } | ConvertTo-Json
  Invoke-RestMethod -Uri "$CaDeskUrl/api/ledger-sync/agents/heartbeat" -Method Post -Body $body -ContentType "application/json" | Out-Null
}

function Parse-TallyVouchers([string]$xmlText) {
  if ([string]::IsNullOrWhiteSpace($xmlText)) { return @() }
  [xml]$x = $xmlText
  $nodes = $x.SelectNodes("//VOUCHER")
  $out = @()
  foreach ($v in $nodes) {
    if ($v.GetAttribute("ACTION") -eq "Cancel" -or $v.ISCANCELLED -eq "Yes") { continue }
    $vNum = if ($v.VOUCHERNUMBER) { $v.VOUCHERNUMBER.Trim() } elseif ($v.REFERENCE) { $v.REFERENCE.Trim() } else { "VCH-$($out.Count + 1)" }
    $guid = if ($v.GUID) { $v.GUID.Trim() } else { $vNum }
    $rawType = if ($v.VOUCHERTYPENAME) { $v.VOUCHERTYPENAME.ToLowerInvariant() } else { "journal" }
    $vType = if ($rawType -like "*payment*") { "Payment" } elseif ($rawType -like "*receipt*") { "Receipt" } elseif ($rawType -like "*contra*") { "Contra" } elseif ($rawType -like "*sales*") { "Sales" } elseif ($rawType -like "*purchase*") { "Purchase" } else { "Journal" }
    $rawDt = if ($v.DATE) { $v.DATE.Trim() } else { (Get-Date).ToString("yyyyMMdd") }
    $isoDt = if ($rawDt.Length -eq 8) { "$($rawDt.Substring(0,4))-$($rawDt.Substring(4,2))-$($rawDt.Substring(6,2))" } else { (Get-Date).ToString("yyyy-MM-dd") }

    $drLedger = ""; $crLedger = ""; [decimal]$drSum = 0; [decimal]$crSum = 0
    foreach ($le in $v.SelectNodes("ALLLEDGERENTRIES.LIST | LEDGERENTRIES.LIST")) {
      $lName = [string]$le.LEDGERNAME
      if ([string]::IsNullOrWhiteSpace($lName)) { continue }
      [decimal]$amt = 0; [decimal]::TryParse(([string]$le.AMOUNT), [ref]$amt) | Out-Null
      $isDr = ($le.ISDEEMEDPOSITIVE -eq "Yes") -or ($amt -lt 0)
      $absAmt = [Math]::Abs($amt)
      if ($isDr) { $drSum += $absAmt; if (-not $drLedger) { $drLedger = $lName.Trim() } }
      else { $crSum += $absAmt; if (-not $crLedger) { $crLedger = $lName.Trim() } }
    }
    $finalAmt = [Math]::Max($drSum, $crSum)
    if ($finalAmt -le 0) { continue }
    $out += @{
      voucherNumber = $vNum; voucherGuid = $guid; voucherType = $vType;
      voucherDate = $isoDt; effectiveDate = $isoDt; amount = $finalAmt;
      debitLedger = if ($drLedger) { $drLedger } else { "Sundry Debtors" };
      creditLedger = if ($crLedger) { $crLedger } else { "Sundry Creditors" };
      narration = ([string]$v.NARRATION).Trim(); chequeNumber = $null; instrumentDate = $null; bankPartyName = $null
    }
  }
  return $out
}

Write-Host "[CaDesk Tally Bridge] Connecting $TallyUrl (Company: $CompanyName) -> $CaDeskUrl" -ForegroundColor Cyan

do {
  try {
    $win = Get-FyWindow $FinancialYear
    Send-CaDeskHeartbeat "Syncing"

    # 1. Pull Daybook Vouchers
    $vchReqXml = "<ENVELOPE><HEADER><TALLYREQUEST>Export Data</TALLYREQUEST></HEADER><BODY><EXPORTDATA><REQUESTDESC><REPORTNAME>Voucher Register</REPORTNAME><STATICVARIABLES><SVCURRENTCOMPANY>{{escapedCompany}}</SVCURRENTCOMPANY><SVFROMDATE>$($win.From)</SVFROMDATE><SVTODATE>$($win.To)</SVTODATE></STATICVARIABLES></REQUESTDESC></EXPORTDATA></BODY></ENVELOPE>"
    $vchResp = Invoke-TallyXml $vchReqXml
    $parsedVouchers = Parse-TallyVouchers $vchResp
    if ($parsedVouchers.Count -gt 0) {
      $batchBody = @{ agentToken = $AgentToken; vouchers = $parsedVouchers } | ConvertTo-Json -Depth 6
      Invoke-RestMethod -Uri "$CaDeskUrl/api/ledger-sync/vouchers/batch-sync" -Method Post -Body $batchBody -ContentType "application/json" | Out-Null
    }

    # 2. Pull Trial Balance -> Schedule III / 3CD / Fixed Assets Sync
    $tbReqXml = "<ENVELOPE><HEADER><TALLYREQUEST>Export Data</TALLYREQUEST></HEADER><BODY><EXPORTDATA><REQUESTDESC><REPORTNAME>Trial Balance</REPORTNAME><STATICVARIABLES><SVCURRENTCOMPANY>{{escapedCompany}}</SVCURRENTCOMPANY><SVFROMDATE>$($win.From)</SVFROMDATE><SVTODATE>$($win.To)</SVTODATE><EXPLODEFLAG>Yes</EXPLODEFLAG><SVEXPORTFORMAT>`$`$SysName:XML</SVEXPORTFORMAT></STATICVARIABLES></REQUESTDESC></EXPORTDATA></BODY></ENVELOPE>"
    $tbResp = Invoke-TallyXml $tbReqXml
    $tbBody = @{ agentToken = $AgentToken; financialYear = $FinancialYear; xmlContent = $tbResp; fileName = "tally_tb_$FinancialYear.xml" } | ConvertTo-Json
    Invoke-RestMethod -Uri "$CaDeskUrl/api/ledger-sync/trial-balance/push" -Method Post -Body $tbBody -ContentType "application/json" | Out-Null

    # 3. Post Pending Audit Adjustments back to Tally
    $queue = Invoke-RestMethod -Uri "$CaDeskUrl/api/ledger-sync/outgoing-vouchers/pending-queue?agentToken=$([uri]::EscapeDataString($AgentToken))" -Method Get
    foreach ($item in $queue) {
      $tResp = Invoke-TallyXml $item.generatedXmlPayload
      $createdOk = ($tResp -match "<CREATED>1</CREATED>") -and -not ($tResp -match "<ERRORS>[1-9]")
      $rep = @{
        voucherId = $item.id; agentToken = $AgentToken;
        status = if ($createdOk) { "PostedSuccessfully" } else { "FailedInErp" };
        erpVoucherNumber = $item.referenceNumber; erpResponseXml = $tResp;
        errorMessage = if ($createdOk) { $null } else { "Tally import reported an error" }
      } | ConvertTo-Json
      Invoke-RestMethod -Uri "$CaDeskUrl/api/ledger-sync/outgoing-vouchers/report-status" -Method Post -Body $rep -ContentType "application/json" | Out-Null
    }

    Send-CaDeskHeartbeat "Connected"
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Sync OK: $($parsedVouchers.Count) vouchers + Trial Balance + $($queue.Count) outgoing adjustments." -ForegroundColor Green
  } catch {
    Write-Warning "[$(Get-Date -Format 'HH:mm:ss')] Sync cycle error: $($_.Exception.Message)"
    try { Send-CaDeskHeartbeat "Error" $_.Exception.Message } catch {}
  }

  if (-not $RunOnce) { Start-Sleep -Seconds $PollSeconds }
} while (-not $RunOnce)
""";

        string cliCmd = $"dotnet run --project tools/CaDesk.TallyBridge -- --url \"{cleanApiUrl}\" --token \"{agent.AgentToken}\" --tally \"{cleanTallyUrl}\" --company \"{agent.CompanyName}\"";

        return new WindowsTallyBridgeBundleDto(
            AgentId: agent.Id,
            AgentName: agent.AgentName,
            CompanyName: agent.CompanyName,
            HostUrl: cleanTallyUrl,
            ScriptFileName: scriptFile,
            ConfigFileName: configFile,
            PowerShellScript: psScript,
            AppSettingsJson: appSettingsJson,
            DotNetRunCommand: cliCmd
        );
    }

    private async Task<ErpSyncAgentConfig> ResolveAgentByTokenAsync(string agentToken, CancellationToken ct) =>
        await ResolveAgentByTokenOrNullAsync(agentToken, ct)
        ?? throw new UnauthorizedAccessException("Invalid ERP Sync Agent Token");

    private async Task<ErpSyncAgentConfig?> ResolveAgentByTokenOrNullAsync(string? agentToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agentToken))
            return null;

        string token = agentToken.Trim();
        if (!_tenant.HasTenant)
        {
            var found = (await _db.Database.SqlQuery<AgentTokenLookup>($"""
                select id as "Id", firm_id as "FirmId", client_id as "ClientId"
                from ledger_sync_find_agent_by_token({token})
                """).ToListAsync(ct)).FirstOrDefault();

            if (found is null)
                return null;

            if (_tenant is ITenantSetter setter)
                setter.Set(found.FirmId, null, "ErpSyncAgent");
            else
                throw new InvalidOperationException("Tenant context cannot be initialized for ERP Sync Agent.");
        }

        if (_featureFlags is not null && !await _featureFlags.IsEnabledAsync(FeatureKeys.LedgerSync, ct))
            throw new BusinessRuleException($"Feature module '{FeatureKeys.LedgerSync}' is not enabled for this firm.", 404);

        return await _db.ErpSyncAgentConfigs.FirstOrDefaultAsync(x => x.AgentToken == token && x.IsActive, ct);
    }

    public sealed class AgentTokenLookup
    {
        public Guid Id { get; set; }
        public Guid FirmId { get; set; }
        public Guid ClientId { get; set; }
    }

    private static ErpOutgoingVoucherDto ToOutgoingVoucherDto(ErpOutgoingVoucher v, string clientName)
    {
        return new ErpOutgoingVoucherDto(
            Id: v.Id,
            ClientId: v.ClientId,
            ClientName: clientName,
            AgentConfigId: v.AgentConfigId,
            VoucherType: v.VoucherType,
            VoucherDate: v.VoucherDate,
            ReferenceNumber: v.ReferenceNumber,
            DebitLedger: v.DebitLedger,
            CreditLedger: v.CreditLedger,
            Amount: v.Amount,
            Narration: v.Narration,
            AuditReason: v.AuditReason,
            PostingStatus: v.PostingStatus,
            GeneratedXmlPayload: v.GeneratedXmlPayload,
            ErpVoucherNumber: v.ErpVoucherNumber,
            ErrorMessage: v.ErrorMessage,
            AuthorizedByName: v.AuthorizedByName,
            AuthorizedAt: v.AuthorizedAt,
            CreatedAt: v.CreatedAt
        );
    }
}
