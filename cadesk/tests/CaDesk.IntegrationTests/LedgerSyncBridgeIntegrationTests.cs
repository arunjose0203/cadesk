using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.LedgerSync;
using CaDesk.Application.TrialBalance;
using CaDesk.Domain.LedgerSync;
using Xunit;

namespace CaDesk.IntegrationTests;

public class LedgerSyncBridgeIntegrationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task WindowsTallyBridge_AnonymousAgentGateway_EndToEnd_WorksUnderRls()
    {
        var auth = await api.SignUpAsync("Kottayam Tally Audit Co");
        var http = api.WithToken(auth.AccessToken);

        // Enable ledger-sync module for firm
        var featRes = await http.PostAsJsonAsync("/api/features/ledger-sync", new { enabled = true });
        featRes.EnsureSuccessStatusCode();

        // Create Company Client
        var coRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Periyar Spices Exports Pvt Ltd", null, null, "Company",
            new Dictionary<string, string> { ["PAN"] = "AAACP3344K" },
            "accounts@periyarspices.test", "9847099112", null, null));
        coRes.EnsureSuccessStatusCode();
        var client = (await coRes.Content.ReadFromJsonAsync<ClientDto>())!;

        // 1. Partner creates TallyPrime Sync Agent
        var agentCreate = await http.PostAsJsonAsync("/api/ledger-sync/agents", new CreateOrUpdateAgentConfigRequest(
            Id: null,
            ClientId: client.Id,
            AgentName: "TallyPrime Office Server #1",
            ErpType: ErpType.TallyPrime,
            HostUrl: "http://localhost:9000",
            CompanyName: "Periyar Spices (FY 25-26)",
            SyncFrequencyMinutes: 10,
            SyncDaybook: true,
            SyncMasters: true,
            SyncBankFeeds: true));
        Assert.Equal(HttpStatusCode.OK, agentCreate.StatusCode);
        var agent = (await agentCreate.Content.ReadFromJsonAsync<ErpSyncAgentConfigDto>())!;
        Assert.StartsWith("ca_sync_", agent.AgentToken);

        // 2. Partner downloads pre-configured Windows PowerShell Bridge & .NET appsettings bundle
        var bundleRes = await http.GetAsync($"/api/ledger-sync/agents/{agent.Id}/bridge-bundle");
        Assert.Equal(HttpStatusCode.OK, bundleRes.StatusCode);
        var bundle = (await bundleRes.Content.ReadFromJsonAsync<WindowsTallyBridgeBundleDto>())!;
        Assert.EndsWith(".ps1", bundle.ScriptFileName);
        Assert.Contains(agent.AgentToken, bundle.PowerShellScript);
        Assert.Contains("http://localhost:9000", bundle.PowerShellScript);
        Assert.Contains("/api/ledger-sync/trial-balance/push", bundle.PowerShellScript);
        using (JsonDocument.Parse(bundle.AppSettingsJson)) { }

        // 3. Unauthenticated local Windows agent (NO JWT Bearer header) connects using AgentToken only
        using var anonClient = api.CreateClient();

        // Reject invalid token with 401
        var badHb = await anonClient.PostAsJsonAsync("/api/ledger-sync/agents/heartbeat",
            new AgentHeartbeatRequest("ca_sync_invalid_token_xyz", AgentSyncStatus.Connected, null));
        Assert.Equal(HttpStatusCode.Unauthorized, badHb.StatusCode);

        // Valid heartbeat
        var hbRes = await anonClient.PostAsJsonAsync("/api/ledger-sync/agents/heartbeat",
            new AgentHeartbeatRequest(agent.AgentToken, AgentSyncStatus.Connected, null));
        Assert.Equal(HttpStatusCode.OK, hbRes.StatusCode);

        // Push Daybook vouchers parsed from Tally port 9000
        var vchSyncRes = await anonClient.PostAsJsonAsync("/api/ledger-sync/vouchers/batch-sync", new BatchSyncVouchersRequest(
            AgentToken: agent.AgentToken,
            Vouchers:
            [
                new BatchSyncVouchersItem("PMT/001", "guid-001", ErpVoucherType.Payment, new DateOnly(2025, 11, 10), new DateOnly(2025, 11, 10), 125_000m, "Malabar Cardamom Suppliers", "SBI Cash Credit A/c", "NEFT Cardamom lot", "NEFT991", new DateOnly(2025, 11, 10), "Malabar Cardamom"),
                new BatchSyncVouchersItem("RCT/002", "guid-002", ErpVoucherType.Receipt, new DateOnly(2025, 11, 12), new DateOnly(2025, 11, 12), 210_000m, "SBI Cash Credit A/c", "Global Spice Importers", "Export realization", "UTR772", new DateOnly(2025, 11, 12), "Global Spice"),
            ]));
        Assert.Equal(HttpStatusCode.OK, vchSyncRes.StatusCode);
        var syncResult = (await vchSyncRes.Content.ReadFromJsonAsync<SyncVouchersResultDto>())!;
        Assert.Equal(2, syncResult.CreatedCount);

        // Push native Tally Trial Balance XML -> auto-maps ledgers across Schedule III / 3CD
        string tallyTbXml = """
        <ENVELOPE>
          <DSPACCNAME><DSPDISPNAME>SBI Cash Credit A/c</DSPDISPNAME></DSPACCNAME>
          <DSPACCINFO><DSPCLAMT><DSPCLAMTA>1850000.00</DSPCLAMTA></DSPCLAMT></DSPACCINFO>
          <DSPACCNAME><DSPDISPNAME>Export Sales Spices</DSPDISPNAME></DSPACCNAME>
          <DSPACCINFO><DSPCLAMT><DSPCLAMTA>-1850000.00</DSPCLAMTA></DSPCLAMT></DSPACCINFO>
        </ENVELOPE>
        """;
        var tbPushRes = await anonClient.PostAsJsonAsync("/api/ledger-sync/trial-balance/push",
            new PushAgentTrialBalanceRequest(agent.AgentToken, "2025-26", tallyTbXml, "live_tally_tb.xml"));
        Assert.Equal(HttpStatusCode.OK, tbPushRes.StatusCode);
        var tbImport = (await tbPushRes.Content.ReadFromJsonAsync<TrialBalanceImportDto>())!;
        Assert.Equal(2, tbImport.TotalLedgersCount);
        Assert.True(tbImport.IsBalanced);

        // 4. Partner queues & authorizes an Audit Journal -> Agent polls queue -> Reports PostedToErp
        var outCreate = await http.PostAsJsonAsync("/api/ledger-sync/outgoing-vouchers", new CreateOutgoingVoucherRequest(
            ClientId: client.Id,
            AgentConfigId: agent.Id,
            VoucherType: ErpVoucherType.Journal,
            VoucherDate: new DateOnly(2026, 3, 31),
            ReferenceNumber: "AUD/SEC32/2026-01",
            DebitLedger: "Depreciation Expense",
            CreditLedger: "Accumulated Depreciation",
            Amount: 95_000m,
            Narration: "Year-end statutory depreciation",
            AuditReason: AuditReasonCategory.DepreciationEntrySec32));
        Assert.Equal(HttpStatusCode.OK, outCreate.StatusCode);
        var outVch = (await outCreate.Content.ReadFromJsonAsync<ErpOutgoingVoucherDto>())!;

        var authOut = await http.PostAsync($"/api/ledger-sync/outgoing-vouchers/{outVch.Id}/authorize", null);
        Assert.Equal(HttpStatusCode.OK, authOut.StatusCode);

        var queueRes = await anonClient.GetAsync($"/api/ledger-sync/outgoing-vouchers/pending-queue?agentToken={Uri.EscapeDataString(agent.AgentToken)}");
        Assert.Equal(HttpStatusCode.OK, queueRes.StatusCode);
        var queue = (await queueRes.Content.ReadFromJsonAsync<List<ErpOutgoingVoucherDto>>())!;
        Assert.Single(queue);
        Assert.Contains("<TALLYREQUEST>Import Data</TALLYREQUEST>", queue[0].GeneratedXmlPayload);

        var reportRes = await anonClient.PostAsJsonAsync("/api/ledger-sync/outgoing-vouchers/report-status",
            new ReportOutgoingVoucherStatusRequest(queue[0].Id, agent.AgentToken, OutgoingVoucherStatus.PostedSuccessfully, "JRN/9001", "<RESPONSE><CREATED>1</CREATED></RESPONSE>", null));
        Assert.Equal(HttpStatusCode.OK, reportRes.StatusCode);

        var verifyList = await http.GetFromJsonAsync<List<ErpOutgoingVoucherDto>>($"/api/ledger-sync/outgoing-vouchers?clientId={client.Id}");
        Assert.Contains(verifyList!, v => v.Id == outVch.Id && v.PostingStatus == OutgoingVoucherStatus.PostedSuccessfully && v.ErpVoucherNumber == "JRN/9001");
    }
}
