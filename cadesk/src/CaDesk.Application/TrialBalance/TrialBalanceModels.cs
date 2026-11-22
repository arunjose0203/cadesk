using System;
using System.Collections.Generic;

namespace CaDesk.Application.TrialBalance;

public record TrialBalanceLedgerDto(
    Guid Id,
    Guid ImportId,
    string LedgerName,
    string TallyGroup,
    decimal OpeningBalance,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal ClosingBalance,
    string Schedule3Group,
    string? Form3CdCategory,
    string? CaroCategory,
    string? FixedAssetBlock,
    bool IsMapped
);

public record TrialBalanceImportDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string FinancialYear,
    string SourceType,
    string FileName,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    int TotalLedgersCount,
    int MappedLedgersCount,
    string Status,
    DateTime? SyncedAt,
    string? SyncedDesks,
    DateTime CreatedAt,
    List<TrialBalanceLedgerDto> Ledgers
);

public record ClientLedgerMappingDto(
    Guid Id,
    Guid ClientId,
    string LedgerPattern,
    string TallyGroup,
    string Schedule3Group,
    string? Form3CdCategory,
    string? CaroCategory,
    string? FixedAssetBlock,
    string? Notes
);

public record DeskSyncResultDto(
    Guid ImportId,
    string FinancialYear,
    bool AuditSynced,
    string? AuditNotes,
    bool Schedule3Synced,
    string? Schedule3Notes,
    bool Form3CdSynced,
    string? Form3CdNotes,
    bool CaroSynced,
    string? CaroNotes,
    bool FixedAssetsSynced,
    string? FixedAssetsNotes,
    DateTime SyncedAt
);

public record UploadTrialBalanceRequest(
    Guid ClientId,
    string FinancialYear,
    string SourceType, // TallyXml, TallyCsv, Excel
    string FileName,
    string ContentPayload
);

public record UpdateLedgerMappingRequest(
    string Schedule3Group,
    string? Form3CdCategory,
    string? CaroCategory,
    string? FixedAssetBlock,
    bool SaveAsClientDefaultRule
);

public record SyncTrialBalanceRequest(
    bool SyncAudit = true,
    bool SyncSchedule3 = true,
    bool SyncForm3Cd = true,
    bool SyncCaro = true,
    bool SyncFixedAssets = true
);
