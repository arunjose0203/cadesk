using CaDesk.Application.Abstractions;
using CaDesk.Application.Compliance;
using CaDesk.Infrastructure.Persistence;

namespace CaDesk.Api.Compliance;

/// <summary>
/// Once a day, for every firm: creates the next deadlines, applies rule changes and government extensions.
/// Each firm runs in its own scope with the tenant set, so both isolation locks apply as in a request.
/// </summary>
public sealed class DeadlineJob(IServiceScopeFactory scopes, PlatformDb platform, ILogger<DeadlineJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);   // let start-up (migrations) finish first
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Deadline generation failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        foreach (var firmId in await platform.ActiveFirmIdsAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantSetter>().Set(firmId, null, null);
                var r = await scope.ServiceProvider.GetRequiredService<DeadlineService>().GenerateAsync(null, ct);
                if (r.Created + r.Updated + r.Removed > 0)
                    log.LogInformation("Deadlines for firm {Firm}: {Created} new, {Updated} changed, {Removed} removed",
                        firmId, r.Created, r.Updated, r.Removed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Deadline generation failed for firm {Firm}", firmId);   // one firm never stops the others
            }
        }
    }
}
