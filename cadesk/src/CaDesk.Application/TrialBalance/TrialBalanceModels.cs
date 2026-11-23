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
    DateTime SyncedAt,
    bool CostAuditSynced = false,
    string? CostAuditNotes = null
);

public record UploadTrialBalanceRequest(
    Guid ClientId,
    string FinancialYear,
    string SourceType, // TallyXml, TallyCsv, BusyCsv, SapGl, Excel, ClipboardTsv, Auto
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
    bool SyncFixedAssets = true,
    bool SyncCostAudit = true
);

public record FinancialKpisDto(
    decimal GrossRevenue,
    decimal OtherIncome,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal OperatingProfit,
    decimal Ebitda,
    decimal ProfitBeforeTax,
    decimal ProfitAfterTax,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalDebt,
    decimal ShareholdersEquity,
    decimal CurrentRatio
);

public record TrialBalancePreviewResultDto(
    string DetectedSourceType,
    string DelimiterUsed,
    int HeaderRowIndex,
    int TotalLedgersCount,
    int MappedLedgersCount,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Discrepancy,
    bool IsBalanced,
    List<ParsedTallyLedger> Ledgers,
    FinancialKpisDto? Kpis,
    List<string> Diagnostics
);

public record PreviewTrialBalanceRequest(
    string ContentPayload,
    string? SourceType = null
);

public record TrialBalanceSampleDto(
    string Key,
    string Title,
    string Description,
    string SourceType,
    decimal TurnoverInr,
    int LedgersCount,
    string ContentPayload
);
