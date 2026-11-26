using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CaDesk.Domain.LedgerSync;

namespace CaDesk.TallyBridge;

public sealed record BridgeConfig
{
    public string ApiBaseUrl { get; init; } = "http://localhost:5080";
    public string AgentToken { get; init; } = "";
    public string TallyHostUrl { get; init; } = "http://localhost:9000";
    public string CompanyName { get; init; } = "";
    public string FinancialYear { get; init; } = "2025-26";
    public int PollIntervalSeconds { get; init; } = 300;
    public bool SyncVouchers { get; init; } = true;
    public bool SyncTrialBalance { get; init; } = true;
    public bool ProcessOutgoingAdjustments { get; init; } = true;
}

public sealed record OutgoingQueueVoucher(
    Guid Id,
    string VoucherType,
    DateOnly VoucherDate,
    string ReferenceNumber,
    string DebitLedger,
    string CreditLedger,
    decimal Amount,
    string Narration,
    string? GeneratedXmlPayload
);

public sealed class TallyHttpGateway(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<string> PostTallyXmlAsync(string tallyHostUrl, string xmlEnvelope, CancellationToken ct)
    {
        using var content = new StringContent(xmlEnvelope, Encoding.UTF8, "application/xml");
        using var res = await http.PostAsync(tallyHostUrl.TrimEnd('/'), content, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task SendHeartbeatAsync(BridgeConfig cfg, AgentSyncStatus status, string? error, CancellationToken ct)
    {
        var payload = new { agentToken = cfg.AgentToken, status = status.ToString(), errorMessage = error };
        using var res = await http.PostAsJsonAsync($"{cfg.ApiBaseUrl.TrimEnd('/')}/api/ledger-sync/agents/heartbeat", payload, JsonOpts, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<int> SyncDaybookVouchersAsync(BridgeConfig cfg, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        string reqXml = TallyXmlProtocol.GenerateExportVouchersXml(cfg.CompanyName, fromDate, toDate);
        string respXml = await PostTallyXmlAsync(cfg.TallyHostUrl, reqXml, ct);
        var vouchers = TallyXmlProtocol.ParseExportVouchersXml(respXml);
        if (vouchers.Count == 0) return 0;

        var payload = new
        {
            agentToken = cfg.AgentToken,
            vouchers = vouchers.Select(v => new
            {
                voucherNumber = v.VoucherNumber,
                voucherGuid = v.VoucherGuid,
                voucherType = v.VoucherType.ToString(),
                voucherDate = v.VoucherDate.ToString("yyyy-MM-dd"),
                effectiveDate = v.EffectiveDate.ToString("yyyy-MM-dd"),
                amount = v.Amount,
                debitLedger = v.DebitLedger,
                creditLedger = v.CreditLedger,
                narration = v.Narration,
                chequeNumber = v.ChequeNumber,
                instrumentDate = v.InstrumentDate?.ToString("yyyy-MM-dd"),
                bankPartyName = v.BankPartyName
            }).ToList()
        };

        using var res = await http.PostAsJsonAsync($"{cfg.ApiBaseUrl.TrimEnd('/')}/api/ledger-sync/vouchers/batch-sync", payload, JsonOpts, ct);
        res.EnsureSuccessStatusCode();
        return vouchers.Count;
    }

    public async Task PushTrialBalanceAsync(BridgeConfig cfg, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        string reqXml = TallyXmlProtocol.GenerateExportTrialBalanceXml(cfg.CompanyName, fromDate, toDate);
        string respXml = await PostTallyXmlAsync(cfg.TallyHostUrl, reqXml, ct);

        var payload = new
        {
            agentToken = cfg.AgentToken,
            financialYear = cfg.FinancialYear,
            xmlContent = respXml,
            fileName = $"tally_tb_{cfg.FinancialYear}_{DateTime.UtcNow:yyyyMMdd}.xml"
        };

        using var res = await http.PostAsJsonAsync($"{cfg.ApiBaseUrl.TrimEnd('/')}/api/ledger-sync/trial-balance/push", payload, JsonOpts, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<int> ProcessOutgoingQueueAsync(BridgeConfig cfg, CancellationToken ct)
    {
        string url = $"{cfg.ApiBaseUrl.TrimEnd('/')}/api/ledger-sync/outgoing-vouchers/pending-queue?agentToken={Uri.EscapeDataString(cfg.AgentToken)}";
        var queue = await http.GetFromJsonAsync<List<OutgoingQueueVoucher>>(url, JsonOpts, ct) ?? [];
        int processed = 0;

        foreach (var item in queue)
        {
            string xmlToPost = !string.IsNullOrWhiteSpace(item.GeneratedXmlPayload)
                ? item.GeneratedXmlPayload!
                : TallyXmlProtocol.GenerateImportJournalVoucherXml(
                    cfg.CompanyName,
                    item.VoucherType,
                    item.VoucherDate,
                    item.ReferenceNumber,
                    item.Amount,
                    item.DebitLedger,
                    item.CreditLedger,
                    item.Narration);

            string tallyResp = await PostTallyXmlAsync(cfg.TallyHostUrl, xmlToPost, ct);
            var (ok, erpVchNo, err) = TallyXmlProtocol.ParseImportResponse(tallyResp);

            var statusReq = new
            {
                voucherId = item.Id,
                agentToken = cfg.AgentToken,
                status = ok ? "PostedToErp" : "Failed",
                erpVoucherNumber = erpVchNo,
                erpResponseXml = tallyResp,
                errorMessage = err
            };

            using var repRes = await http.PostAsJsonAsync($"{cfg.ApiBaseUrl.TrimEnd('/')}/api/ledger-sync/outgoing-vouchers/report-status", statusReq, JsonOpts, ct);
            repRes.EnsureSuccessStatusCode();
            processed++;
        }

        return processed;
    }
}
