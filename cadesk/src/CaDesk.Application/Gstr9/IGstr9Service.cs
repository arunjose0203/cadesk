namespace CaDesk.Application.Gstr9;

public interface IGstr9Service
{
    Task<IReadOnlyList<Gstr9ReturnDto>> GetReturnsAsync(Guid? clientId = null, string? financialYear = null, CancellationToken ct = default);
    Task<Gstr9ReturnDto?> GetReturnByIdAsync(Guid id, CancellationToken ct = default);
    Task<Gstr9ReturnDto> CompileReturnAsync(CompileGstr9ReturnRequest req, CancellationToken ct = default);
    Task<Gstr9ReturnDto> UpdateReturnAsync(Guid id, UpdateGstr9ReturnRequest req, CancellationToken ct = default);
    Task<Gstr9ReturnDto> FileReturnAsync(Guid id, FileGstr9ReturnRequest req, CancellationToken ct = default);
    
    Task<Gstr9cReconciliationDto?> GetGstr9cAsync(Guid returnId, CancellationToken ct = default);
    Task<Gstr9cReconciliationDto> ReconcileGstr9cAsync(Guid returnId, ReconcileGstr9cRequest req, CancellationToken ct = default);
    Task<Gstr9cDiscrepancyDto> AddDiscrepancyAsync(Guid returnId, AddDiscrepancyRequest req, CancellationToken ct = default);
    Task<Gstr9cDiscrepancyDto> ResolveDiscrepancyAsync(Guid discrepancyId, ResolveDiscrepancyRequest req, CancellationToken ct = default);
    Task<Gstr9cReconciliationDto> CertifyGstr9cAsync(Guid returnId, CertifyGstr9cRequest req, CancellationToken ct = default);
    Task<Gstr9cReconciliationDto> RecordDrc03PaymentAsync(Guid returnId, RecordDrc03PaymentRequest req, CancellationToken ct = default);
    
    Task<Gstr9SummaryDto> GetSummaryAsync(string? financialYear = null, CancellationToken ct = default);
}
