using CaDesk.Application.Abstractions;
using CaDesk.Application.Notifications;
using CaDesk.Infrastructure.Persistence;

namespace CaDesk.Api.Notifications;

/// <summary>Every 5 minutes, for every firm: queue due reminders, then send what is due (outside quiet hours).</summary>
public sealed class NotificationJob(IServiceScopeFactory scopes, PlatformDb platform, ILogger<NotificationJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                foreach (var firmId in await platform.ActiveFirmIdsAsync(stoppingToken))
                    await RunForFirmAsync(firmId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Notification run failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunForFirmAsync(Guid firmId, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantSetter>().Set(firmId, null, null);
            await scope.ServiceProvider.GetRequiredService<NotificationService>().PlanAsync(ct);
            await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().DispatchAsync(200, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Notifications failed for firm {Firm}", firmId);    // one firm never blocks the others
        }
    }
}
