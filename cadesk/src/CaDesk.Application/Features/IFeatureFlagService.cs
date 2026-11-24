namespace CaDesk.Application.Features;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string featureKey, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, bool>> GetFirmFeaturesAsync(CancellationToken ct = default);
    Task SetFeatureAsync(string featureKey, bool enabled, CancellationToken ct = default);
}
