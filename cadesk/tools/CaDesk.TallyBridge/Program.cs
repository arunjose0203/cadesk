using System.Text.Json;
using CaDesk.Domain.LedgerSync;
using CaDesk.TallyBridge;

static BridgeConfig LoadConfig(string[] args)
{
    var fileCfg = new BridgeConfig();
    string cfgPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (!File.Exists(cfgPath) && File.Exists("appsettings.json"))
        cfgPath = "appsettings.json";

    if (File.Exists(cfgPath))
    {
        var loaded = JsonSerializer.Deserialize<BridgeConfig>(File.ReadAllText(cfgPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (loaded != null) fileCfg = loaded;
    }

    string? GetArg(string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    return fileCfg with
    {
        ApiBaseUrl = GetArg("--url") ?? Environment.GetEnvironmentVariable("CADESK_URL") ?? fileCfg.ApiBaseUrl,
        AgentToken = GetArg("--token") ?? Environment.GetEnvironmentVariable("CADESK_AGENT_TOKEN") ?? fileCfg.AgentToken,
        TallyHostUrl = GetArg("--tally") ?? Environment.GetEnvironmentVariable("CADESK_TALLY_URL") ?? fileCfg.TallyHostUrl,
        CompanyName = GetArg("--company") ?? Environment.GetEnvironmentVariable("CADESK_TALLY_COMPANY") ?? fileCfg.CompanyName,
        FinancialYear = GetArg("--fy") ?? Environment.GetEnvironmentVariable("CADESK_FY") ?? fileCfg.FinancialYear,
    };
}

static (DateOnly From, DateOnly To) ResolveIndianFyDates(string fy)
{
    var parts = fy.Split('-', StringSplitOptions.RemoveEmptyEntries);
    int startYear = parts.Length > 0 && int.TryParse(parts[0], out int y) ? y : DateTime.UtcNow.Month >= 4 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
    return (new DateOnly(startYear, 4, 1), new DateOnly(startYear + 1, 3, 31));
}

var cfg = LoadConfig(args);
bool runOnce = args.Any(a => string.Equals(a, "--once", StringComparison.OrdinalIgnoreCase));

if (string.IsNullOrWhiteSpace(cfg.AgentToken) || string.IsNullOrWhiteSpace(cfg.CompanyName))
{
    Console.WriteLine("CaDesk Lightweight Windows TallyPrime XML Bridge");
    Console.WriteLine("Usage: CaDesk.TallyBridge --url https://your-cadesk --token ca_sync_xxx --tally http://localhost:9000 --company \"Acme Traders\" [--fy 2025-26] [--once]");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var gateway = new TallyHttpGateway(http);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"[CaDesk TallyBridge] Connected target: {cfg.ApiBaseUrl} <-> {cfg.TallyHostUrl} (Company: {cfg.CompanyName}, FY: {cfg.FinancialYear})");

while (!cts.IsCancellationRequested)
{
    try
    {
        var (fromDt, toDt) = ResolveIndianFyDates(cfg.FinancialYear);
        await gateway.SendHeartbeatAsync(cfg, AgentSyncStatus.Syncing, null, cts.Token);

        int vchCount = cfg.SyncVouchers ? await gateway.SyncDaybookVouchersAsync(cfg, fromDt, toDt, cts.Token) : 0;
        if (cfg.SyncTrialBalance) await gateway.PushTrialBalanceAsync(cfg, fromDt, toDt, cts.Token);
        int outCount = cfg.ProcessOutgoingAdjustments ? await gateway.ProcessOutgoingQueueAsync(cfg, cts.Token) : 0;

        await gateway.SendHeartbeatAsync(cfg, AgentSyncStatus.Connected, null, cts.Token);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Synced {vchCount} Daybook vouchers, pushed Trial Balance XML, posted {outCount} outgoing Audit Journals.");
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sync cycle error: {ex.Message}");
        try { await gateway.SendHeartbeatAsync(cfg, AgentSyncStatus.Error, ex.Message, CancellationToken.None); } catch { }
    }

    if (runOnce) break;
    await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, cfg.PollIntervalSeconds)), cts.Token);
}

return 0;
