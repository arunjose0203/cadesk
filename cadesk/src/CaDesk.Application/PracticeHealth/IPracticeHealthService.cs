using CaDesk.Domain.PracticeHealth;

namespace CaDesk.Application.PracticeHealth;

public interface IPracticeHealthService
{
    // Telemetry
    Task<FirmTelemetryDto> GetFirmTelemetryAsync(CancellationToken ct = default);

    // Valuations
    PracticeValuationEngine.ValuationResult CalculateValuation(CalculateValuationRequest req);
    Task<PracticeValuationDto> SaveValuationAsync(SaveValuationRequest req, CancellationToken ct = default);
    Task<List<PracticeValuationDto>> GetValuationsAsync(CancellationToken ct = default);
    Task<PracticeValuationDto?> GetValuationByIdAsync(Guid id, CancellationToken ct = default);
    Task<PracticeValuationDto> ApproveValuationAsync(Guid id, CancellationToken ct = default);

    // Partner Equity
    Task<List<PartnerEquityDto>> GetPartnerEquitiesAsync(CancellationToken ct = default);
    Task<PartnerEquityDto> SavePartnerEquityAsync(SavePartnerEquityRequest req, CancellationToken ct = default);
    Task<bool> DeletePartnerEquityAsync(Guid id, CancellationToken ct = default);

    // Retirement & Succession
    Task<List<PartnerRetirementPlanDto>> GetRetirementPlansAsync(CancellationToken ct = default);
    Task<PartnerRetirementPlanDto> CreateRetirementPlanAsync(CreateRetirementPlanRequest req, CancellationToken ct = default);
    Task<PartnerRetirementPlanDto> UpdateRetirementPlanStatusAsync(Guid id, RetirementPlanStatus status, CancellationToken ct = default);

    // Client Transitions
    Task<List<ClientSuccessionTransitionDto>> GetClientTransitionsAsync(CancellationToken ct = default);
    Task<ClientSuccessionTransitionDto> SaveClientTransitionAsync(SaveClientTransitionRequest req, CancellationToken ct = default);
    Task<bool> DeleteClientTransitionAsync(Guid id, CancellationToken ct = default);

    // Peer Benchmarking
    Task<PracticeBenchmarkDto> GenerateBenchmarkAsync(GenerateBenchmarkRequest req, CancellationToken ct = default);
    Task<List<PracticeBenchmarkDto>> GetBenchmarksAsync(CancellationToken ct = default);
}
