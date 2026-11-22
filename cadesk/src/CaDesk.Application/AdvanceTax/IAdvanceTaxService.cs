namespace CaDesk.Application.AdvanceTax;

public interface IAdvanceTaxService
{
    Task<AdvanceTaxSummaryDto> GetSummaryAsync(Guid? clientId, string? financialYear, CancellationToken ct = default);
    Task<IReadOnlyList<AdvanceTaxPlanDto>> GetPlansAsync(Guid? clientId, string? financialYear, CancellationToken ct = default);
    Task<AdvanceTaxPlanDto?> GetPlanByIdAsync(Guid id, CancellationToken ct = default);
    Task<AdvanceTaxPlanDto> CreatePlanAsync(SaveAdvanceTaxPlanRequest request, CancellationToken ct = default);
    Task<AdvanceTaxPlanDto> UpdatePlanAsync(Guid id, SaveAdvanceTaxPlanRequest request, CancellationToken ct = default);
    Task DeletePlanAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AdvanceTaxInstallmentDto>> GetInstallmentsAsync(Guid planId, CancellationToken ct = default);
    Task<AdvanceTaxInstallmentDto> RecordPaymentAsync(Guid installmentId, RecordInstallmentPaymentRequest request, CancellationToken ct = default);
    Task<AdvanceTaxPlanDto> ImportAisToPlanAsync(Guid planId, string aisJson, CancellationToken ct = default);

    Task<TaxInterestComputationDto> CalculateInterestAsync(Guid planId, CalculateStatutoryInterestRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaxInterestComputationDto>> GetInterestComputationsAsync(Guid planId, CancellationToken ct = default);
}
