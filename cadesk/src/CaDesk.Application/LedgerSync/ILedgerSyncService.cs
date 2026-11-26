using CaDesk.Domain.LedgerSync;

namespace CaDesk.Application.LedgerSync;

public interface ILedgerSyncService
{
    // Agent Configuration & Diagnostics
    Task<List<ErpSyncAgentConfigDto>> GetAgentConfigsAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<ErpSyncAgentConfigDto?> GetAgentConfigByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErpSyncAgentConfigDto> SaveAgentConfigAsync(CreateOrUpdateAgentConfigRequest req, CancellationToken ct = default);
    Task<bool> DeleteAgentConfigAsync(Guid id, CancellationToken ct = default);
    Task<bool> RecordAgentHeartbeatAsync(AgentHeartbeatRequest req, CancellationToken ct = default);

    // Inbound Voucher Sync
    Task<SyncVouchersResultDto> BatchSyncVouchersAsync(BatchSyncVouchersRequest req, CancellationToken ct = default);
    Task<List<ErpLedgerVoucherDto>> GetVouchersAsync(Guid? clientId = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);

    // 2-Way Outgoing Vouchers (Push Adjustments)
    Task<ErpOutgoingVoucherDto> CreateOutgoingVoucherAsync(CreateOutgoingVoucherRequest req, CancellationToken ct = default);
    Task<ErpOutgoingVoucherDto> AuthorizeOutgoingVoucherAsync(Guid id, CancellationToken ct = default);
    Task<List<ErpOutgoingVoucherDto>> GetOutgoingVouchersAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<List<ErpOutgoingVoucherDto>> GetPendingOutgoingQueueForAgentAsync(string agentToken, CancellationToken ct = default);
    Task<bool> ReportOutgoingVoucherStatusAsync(ReportOutgoingVoucherStatusRequest req, CancellationToken ct = default);

    // AI Bank Feed Discrepancy Matching & Auto-Reconciliation
    Task<List<BankLedgerReconciliationMatchDto>> RunAutoReconciliationAsync(RunAutoReconciliationRequest req, CancellationToken ct = default);
    Task<List<BankLedgerReconciliationMatchDto>> GetReconciliationMatchesAsync(Guid? clientId = null, Guid? bankAccountId = null, CancellationToken ct = default);
    Task<BankLedgerReconciliationMatchDto> ProcessReconciliationActionAsync(Guid matchId, ReconcileMatchActionRequest req, CancellationToken ct = default);
    Task<ReconciliationSummaryDto> GetReconciliationSummaryAsync(Guid clientId, Guid bankAccountId, CancellationToken ct = default);
    // Agent Trial Balance Push & Windows Tally XML Bridge Generator
    Task<TrialBalance.TrialBalanceImportDto> PushAgentTrialBalanceAsync(PushAgentTrialBalanceRequest req, CancellationToken ct = default);
    Task<WindowsTallyBridgeBundleDto?> BuildWindowsBridgeBundleAsync(Guid agentId, string apiBaseUrl, CancellationToken ct = default);
}
