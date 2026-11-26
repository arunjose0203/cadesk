using CaDesk.Domain.LedgerSync;

namespace CaDesk.Application.LedgerSync;

public record ErpSyncAgentConfigDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string AgentName,
    string AgentToken,
    ErpType ErpType,
    string HostUrl,
    string CompanyName,
    AgentSyncStatus SyncStatus,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? LastSyncAt,
    int SyncFrequencyMinutes,
    bool SyncDaybook,
    bool SyncMasters,
    bool SyncBankFeeds,
    int TotalVouchersSynced,
    int TotalMastersSynced,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateOrUpdateAgentConfigRequest(
    Guid? Id,
    Guid ClientId,
    string AgentName,
    ErpType ErpType,
    string HostUrl,
    string CompanyName,
    int SyncFrequencyMinutes,
    bool SyncDaybook,
    bool SyncMasters,
    bool SyncBankFeeds
);

public record AgentHeartbeatRequest(
    string AgentToken,
    AgentSyncStatus Status,
    string? ErrorMessage
);

public record ErpLedgerVoucherDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid AgentConfigId,
    ErpType ErpType,
    string VoucherNumber,
    string VoucherGuid,
    ErpVoucherType VoucherType,
    DateOnly VoucherDate,
    DateOnly EffectiveDate,
    decimal Amount,
    string DebitLedger,
    string CreditLedger,
    string Narration,
    string? ChequeNumber,
    DateOnly? InstrumentDate,
    string? BankPartyName,
    DateOnly? BankClearanceDate,
    VoucherReconciliationStatus ReconciliationStatus,
    DateTime CreatedAt
);

public record BatchSyncVouchersItem(
    string VoucherNumber,
    string VoucherGuid,
    ErpVoucherType VoucherType,
    DateOnly VoucherDate,
    DateOnly EffectiveDate,
    decimal Amount,
    string DebitLedger,
    string CreditLedger,
    string Narration,
    string? ChequeNumber,
    DateOnly? InstrumentDate,
    string? BankPartyName
);

public record BatchSyncVouchersRequest(
    string AgentToken,
    List<BatchSyncVouchersItem> Vouchers
);

public record SyncVouchersResultDto(
    int TotalProcessed,
    int CreatedCount,
    int UpdatedCount,
    string StatusMessage
);

public record CreateOutgoingVoucherRequest(
    Guid ClientId,
    Guid AgentConfigId,
    ErpVoucherType VoucherType,
    DateOnly VoucherDate,
    string ReferenceNumber,
    string DebitLedger,
    string CreditLedger,
    decimal Amount,
    string Narration,
    AuditReasonCategory AuditReason
);

public record ErpOutgoingVoucherDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid AgentConfigId,
    ErpVoucherType VoucherType,
    DateOnly VoucherDate,
    string ReferenceNumber,
    string DebitLedger,
    string CreditLedger,
    decimal Amount,
    string Narration,
    AuditReasonCategory AuditReason,
    OutgoingVoucherStatus PostingStatus,
    string? GeneratedXmlPayload,
    string? ErpVoucherNumber,
    string? ErrorMessage,
    string? AuthorizedByName,
    DateTimeOffset? AuthorizedAt,
    DateTime CreatedAt
);

public record ReportOutgoingVoucherStatusRequest(
    Guid VoucherId,
    string AgentToken,
    OutgoingVoucherStatus Status,
    string? ErpVoucherNumber,
    string? ErpResponseXml,
    string? ErrorMessage
);

public record RunAutoReconciliationRequest(
    Guid ClientId,
    Guid BankAccountId,
    DateOnly? FromDate,
    DateOnly? ToDate
);

public record BankLedgerReconciliationMatchDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid BankAccountId,
    Guid BankTransactionId,
    DateOnly TransactionDate,
    string Narration,
    string TransactionType,
    Guid? ErpLedgerVoucherId,
    string? ErpVoucherNumber,
    DateOnly? VoucherDate,
    string? DebitLedger,
    string? CreditLedger,
    ReconciliationMatchRule MatchRule,
    decimal ConfidenceScore,
    decimal BankAmount,
    decimal LedgerAmount,
    decimal VarianceAmount,
    string? VarianceExplanation,
    string MatchedVoucherNumbers,
    ReconciliationMatchStatus Status,
    string? ReconciledByName,
    DateTimeOffset? ReconciledAt,
    DateTime CreatedAt
);

public record ReconcileMatchActionRequest(
    ReconciliationMatchStatus Status,
    string? Notes
);

public record ReconciliationSummaryDto(
    int TotalBankTransactions,
    int ReconciledCount,
    int PendingCount,
    decimal TotalReconciledAmount,
    decimal TotalUnreconciledAmount,
    decimal ReconciledPercentage,
    int HighConfidenceSuggestionsCount
);

public record PushAgentTrialBalanceRequest(
    string AgentToken,
    string FinancialYear,
    string XmlContent,
    string? FileName = null
);

public record WindowsTallyBridgeBundleDto(
    Guid AgentId,
    string AgentName,
    string CompanyName,
    string HostUrl,
    string ScriptFileName,
    string ConfigFileName,
    string PowerShellScript,
    string AppSettingsJson,
    string DotNetRunCommand
);
