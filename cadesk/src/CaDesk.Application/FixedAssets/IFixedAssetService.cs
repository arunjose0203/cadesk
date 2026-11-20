using CaDesk.Domain.FixedAssets;

namespace CaDesk.Application.FixedAssets;

public interface IFixedAssetService
{
    Task<FixedAssetSummaryDto> GetSummaryAsync(Guid? clientId, string? financialYear, CancellationToken ct = default);
    Task<IReadOnlyList<FixedAssetDto>> GetAssetsAsync(Guid? clientId, AssetClass? assetClass, AssetStatus? status, CancellationToken ct = default);
    Task<FixedAssetDto?> GetAssetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FixedAssetDto> CreateAssetAsync(SaveFixedAssetRequest request, CancellationToken ct = default);
    Task<FixedAssetDto> UpdateAssetAsync(Guid id, SaveFixedAssetRequest request, CancellationToken ct = default);
    Task<FixedAssetDto> DisposeAssetAsync(Guid id, DisposeAssetRequest request, CancellationToken ct = default);
    Task DeleteAssetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AssetDepreciationEntryDto>> RunDepreciationAsync(RunDualDepreciationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AssetDepreciationEntryDto>> GetDepreciationEntriesAsync(Guid? assetId, Guid? clientId, string? financialYear, CancellationToken ct = default);

    Task<AssetPhysicalVerificationDto> RecordVerificationAsync(Guid assetId, RecordPhysicalVerificationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AssetPhysicalVerificationDto>> GetVerificationsAsync(Guid? assetId, Guid? clientId, CancellationToken ct = default);
}
