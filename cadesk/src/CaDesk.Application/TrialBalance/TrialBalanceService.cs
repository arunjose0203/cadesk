using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.TrialBalance;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.TrialBalance;

public class TrialBalanceService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TrialBalanceSyncEngine _syncEngine;

    public TrialBalanceService(
        IAppDbContext db,
        ITenantContext tenant,
        TrialBalanceSyncEngine syncEngine)
    {
        _db = db;
        _tenant = tenant;
        _syncEngine = syncEngine;
    }

    public async Task<TrialBalanceImportDto> UploadTrialBalanceAsync(
        UploadTrialBalanceRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenant.FirmId;
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId && c.FirmId == firmId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        if (string.IsNullOrWhiteSpace(req.FinancialYear))
            throw new RequestValidationException(nameof(req.FinancialYear), "Financial year is required (e.g. 2024-25).");

        // 1. Parse Tally content (XML or CSV)
        var parsed = TallyTrialBalanceParser.Parse(req.ContentPayload, req.SourceType);

        // 2. Load existing client-level mapping rules (mapped once per client!)
        var clientRules = await _db.ClientLedgerMappings
            .Where(r => r.ClientId == req.ClientId && r.FirmId == firmId)
            .ToListAsync(ct);

        // 3. Create TrialBalanceImport entity
        var import = new TrialBalanceImport
        {
            Id = Guid.NewGuid(),
            FirmId = firmId,
            ClientId = req.ClientId,
            FinancialYear = req.FinancialYear.Trim(),
            SourceType = req.SourceType,
            FileName = req.FileName,
            TotalDebit = parsed.TotalDebit,
            TotalCredit = parsed.TotalCredit,
            IsBalanced = parsed.IsBalanced,
            TotalLedgersCount = parsed.Ledgers.Count,
            Status = TrialBalanceConstants.StatusDraft,
            CreatedAt = DateTime.UtcNow
        };

        // 4. Create ledger items
        var ledgers = new List<TrialBalanceLedger>();
        foreach (var pl in parsed.Ledgers)
        {
            ledgers.Add(new TrialBalanceLedger
            {
                Id = Guid.NewGuid(),
                FirmId = firmId,
                ImportId = import.Id,
                LedgerName = pl.Name,
                TallyGroup = pl.ParentGroup,
                OpeningBalance = pl.OpeningBalance,
                DebitAmount = pl.DebitAmount,
                CreditAmount = pl.CreditAmount,
                ClosingBalance = pl.ClosingBalance,
                CreatedAt = DateTime.UtcNow
            });
        }

        // 5. Apply client mappings and standard Indian chart of accounts heuristics
        TrialBalanceMappingEngine.ApplyMappings(ledgers, clientRules);
        import.MappedLedgersCount = ledgers.Count(l => l.IsMapped);
        if (import.MappedLedgersCount == import.TotalLedgersCount)
        {
            import.Status = TrialBalanceConstants.StatusMapped;
        }

        import.Ledgers = ledgers;

        _db.TrialBalanceImports.Add(import);
        await _db.SaveChangesAsync(ct);

        return ToImportDto(import, client.Name);
    }

    public async Task<List<TrialBalanceImportDto>> GetImportsAsync(
        Guid? clientId,
        string? fy,
        CancellationToken ct = default)
    {
        var firmId = _tenant.FirmId;
        var q = _db.TrialBalanceImports
            .Include(i => i.Client)
            .Include(i => i.Ledgers)
            .Where(i => i.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(i => i.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(fy)) q = q.Where(i => i.FinancialYear == fy.Trim());

        var list = await q.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
        return list.Select(i => ToImportDto(i, i.Client?.Name)).ToList();
    }

    public async Task<TrialBalanceImportDto?> GetImportByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenant.FirmId;
        var import = await _db.TrialBalanceImports
            .Include(i => i.Client)
            .Include(i => i.Ledgers)
            .FirstOrDefaultAsync(i => i.Id == id && i.FirmId == firmId, ct);

        return import == null ? null : ToImportDto(import, import.Client?.Name);
    }

    public async Task<TrialBalanceImportDto> UpdateLedgerMappingAsync(
        Guid importId,
        Guid ledgerId,
        UpdateLedgerMappingRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenant.FirmId;
        var import = await _db.TrialBalanceImports
            .Include(i => i.Client)
            .Include(i => i.Ledgers)
            .FirstOrDefaultAsync(i => i.Id == importId && i.FirmId == firmId, ct)
            ?? throw new BusinessRuleException("Trial balance import not found.", 404);

        var ledger = import.Ledgers.FirstOrDefault(l => l.Id == ledgerId)
            ?? throw new BusinessRuleException("Ledger not found in this trial balance import.", 404);

        if (!TrialBalanceConstants.Schedule3Groups.Contains(req.Schedule3Group))
            throw new RequestValidationException(nameof(req.Schedule3Group),
                $"'{req.Schedule3Group}' is not a Schedule III group. Use one of: {string.Join(", ", TrialBalanceConstants.Schedule3Groups.Order())}.");

        ledger.Schedule3Group = req.Schedule3Group;
        ledger.Form3CdCategory = req.Form3CdCategory;
        ledger.CaroCategory = req.CaroCategory;
        ledger.FixedAssetBlock = req.FixedAssetBlock;
        ledger.IsMapped = true;
        ledger.UpdatedAt = DateTime.UtcNow;

        // If requested, save as client default rule so future imports reuse it
        if (req.SaveAsClientDefaultRule)
        {
            var existingRule = await _db.ClientLedgerMappings
                .FirstOrDefaultAsync(r => r.ClientId == import.ClientId && r.LedgerPattern == ledger.LedgerName && r.FirmId == firmId, ct);

            if (existingRule == null)
            {
                _db.ClientLedgerMappings.Add(new ClientLedgerMapping
                {
                    Id = Guid.NewGuid(),
                    FirmId = firmId,
                    ClientId = import.ClientId,
                    LedgerPattern = ledger.LedgerName,
                    TallyGroup = ledger.TallyGroup,
                    Schedule3Group = req.Schedule3Group,
                    Form3CdCategory = req.Form3CdCategory,
                    CaroCategory = req.CaroCategory,
                    FixedAssetBlock = req.FixedAssetBlock,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existingRule.Schedule3Group = req.Schedule3Group;
                existingRule.Form3CdCategory = req.Form3CdCategory;
                existingRule.CaroCategory = req.CaroCategory;
                existingRule.FixedAssetBlock = req.FixedAssetBlock;
                existingRule.UpdatedAt = DateTime.UtcNow;
            }
        }

        import.MappedLedgersCount = import.Ledgers.Count(l => l.IsMapped);
        if (import.MappedLedgersCount == import.TotalLedgersCount)
        {
            import.Status = TrialBalanceConstants.StatusMapped;
        }
        import.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToImportDto(import, import.Client?.Name);
    }

    public async Task<DeskSyncResultDto> SyncToDesksAsync(
        Guid importId,
        SyncTrialBalanceRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenant.FirmId;
        var import = await _db.TrialBalanceImports
            .Include(i => i.Client)
            .Include(i => i.Ledgers)
            .FirstOrDefaultAsync(i => i.Id == importId && i.FirmId == firmId, ct)
            ?? throw new BusinessRuleException("Trial balance import not found.", 404);

        return await _syncEngine.SyncToAllDesksAsync(import, req, ct);
    }

    public async Task<List<ClientLedgerMappingDto>> GetClientRulesAsync(Guid clientId, CancellationToken ct = default)
    {
        var firmId = _tenant.FirmId;
        var rules = await _db.ClientLedgerMappings
            .Where(r => r.ClientId == clientId && r.FirmId == firmId)
            .OrderBy(r => r.LedgerPattern)
            .ToListAsync(ct);

        return rules.Select(r => new ClientLedgerMappingDto(
            r.Id,
            r.ClientId,
            r.LedgerPattern,
            r.TallyGroup,
            r.Schedule3Group,
            r.Form3CdCategory,
            r.CaroCategory,
            r.FixedAssetBlock,
            r.Notes
        )).ToList();
    }

    private static TrialBalanceImportDto ToImportDto(TrialBalanceImport i, string? clientName)
    {
        return new TrialBalanceImportDto(
            i.Id,
            i.ClientId,
            clientName ?? "Client Company",
            i.FinancialYear,
            i.SourceType,
            i.FileName,
            i.TotalDebit,
            i.TotalCredit,
            i.IsBalanced,
            i.TotalLedgersCount,
            i.MappedLedgersCount,
            i.Status,
            i.SyncedAt,
            i.SyncedDesks,
            i.CreatedAt,
            i.Ledgers.OrderBy(l => l.LedgerName).Select(l => new TrialBalanceLedgerDto(
                l.Id,
                l.ImportId,
                l.LedgerName,
                l.TallyGroup,
                l.OpeningBalance,
                l.DebitAmount,
                l.CreditAmount,
                l.ClosingBalance,
                l.Schedule3Group,
                l.Form3CdCategory,
                l.CaroCategory,
                l.FixedAssetBlock,
                l.IsMapped
            )).ToList()
        );
    }
}
