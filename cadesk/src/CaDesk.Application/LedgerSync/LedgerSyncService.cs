using System.Security.Cryptography;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.LedgerSync;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.LedgerSync;

public class LedgerSyncService : ILedgerSyncService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public LedgerSyncService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
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
        var agent = await _db.ErpSyncAgentConfigs.FirstOrDefaultAsync(x => x.AgentToken == req.AgentToken, ct);
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
        var agent = await _db.ErpSyncAgentConfigs.FirstOrDefaultAsync(x => x.AgentToken == req.AgentToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid ERP Sync Agent Token");

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
        var agent = await _db.ErpSyncAgentConfigs.FirstOrDefaultAsync(x => x.AgentToken == agentToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid ERP Sync Agent Token");

        var list = await _db.ErpOutgoingVouchers
            .Include(x => x.Client)
            .Where(x => x.AgentConfigId == agent.Id && x.PostingStatus == OutgoingVoucherStatus.QueuedForAgent)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        return list.Select(x => ToOutgoingVoucherDto(x, x.Client?.Name ?? "Client")).ToList();
    }

    public async Task<bool> ReportOutgoingVoucherStatusAsync(ReportOutgoingVoucherStatusRequest req, CancellationToken ct = default)
    {
        var agent = await _db.ErpSyncAgentConfigs.FirstOrDefaultAsync(x => x.AgentToken == req.AgentToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid ERP Sync Agent Token");

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
