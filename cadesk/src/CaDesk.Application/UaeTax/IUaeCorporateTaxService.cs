using CaDesk.Domain.UaeTax;

namespace CaDesk.Application.UaeTax;

public interface IUaeCorporateTaxService
{
    Task<List<UaeTaxProfileDto>> GetProfilesAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<UaeTaxProfileDto?> GetProfileByIdAsync(Guid id, CancellationToken ct = default);
    Task<UaeTaxProfileDto> CreateOrUpdateProfileAsync(CreateOrUpdateTaxProfileRequest req, CancellationToken ct = default);
    UaeTaxEngine.ComputationResult CalculateTax(CalculateUaeTaxRequest req);
    Task<UaeCorporateTaxReturnDto> SaveReturnAsync(SaveUaeCorporateTaxReturnRequest req, CancellationToken ct = default);
    Task<UaeCorporateTaxReturnDto?> GetReturnByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<UaeCorporateTaxReturnDto>> GetReturnsForProfileAsync(Guid profileId, CancellationToken ct = default);
    Task<UaeCorporateTaxReturnDto> ApproveReturnAsync(Guid returnId, ApproveUaeReturnRequest req, CancellationToken ct = default);
    Task<EmaraTaxSubmissionExportDto> ExportEmaraTaxPackageAsync(Guid returnId, CancellationToken ct = default);
}
