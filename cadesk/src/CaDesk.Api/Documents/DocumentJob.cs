using CaDesk.Application.Abstractions;
using CaDesk.Application.Documents;
using CaDesk.Infrastructure.Persistence;

namespace CaDesk.Api.Documents;

/// <summary>
/// Every 10 minutes, for every firm: scans files that are waiting (the scanner was down or busy) and removes files
/// that have been in the trash longer than doc.trash_days. Each firm runs in its own scope with the tenant set.
/// Files past retention are NOT removed here: they're listed for a person to remove (Settings → Documents).
/// </summary>
public sealed class DocumentJob(IServiceScopeFactory scopes, PlatformDb platform, ILogger<DocumentJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);   // let start-up (migrations) finish first
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Document maintenance failed"); }
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
                var r = await scope.ServiceProvider.GetRequiredService<DocumentService>().MaintainAsync(ct);
                if (r.Scanned + r.TrashPurged > 0)
                    log.LogInformation("Documents for firm {Firm}: {Scanned} scanned ({Infected} infected), {Purged} removed from the trash",
                        firmId, r.Scanned, r.Infected, r.TrashPurged);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Document maintenance failed for firm {Firm}", firmId);   // one firm never stops the others
            }
        }
    }
}
