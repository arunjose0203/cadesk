using System.Net;
using System.Net.Http.Json;
using CaDesk.Api.Auth;
using CaDesk.Application.PartnerCompanion;
using CaDesk.Domain.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaDesk.IntegrationTests;

public class Sprint62SecurityIntegrationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task DisabledDesks_Return404FromApi_ByDefault()
    {
        var firmA = await api.SignUpAsync("SecDefault A", plan: "ENTERPRISE");
        var clientA = api.ClientFor(firmA.FirmId);

        // All 9 unfinished desks must return 404 Not Found by default
        var desks = new[]
        {
            "/api/connected-banking/accounts",
            "/api/einvoicing/summary",
            "/api/tax-copilot/precedents",
            "/api/uae-tax/profiles",
            "/api/practice-health/telemetry",
            "/api/ledger-sync/agents",
            "/api/tax-litigation/appeals",
            "/api/esg-csr/assessments",
            "/api/ibc-cirp/cases"
        };

        foreach (var deskUrl in desks)
        {
            var res = await clientA.GetAsync(deskUrl);
            Assert.True(res.StatusCode == HttpStatusCode.NotFound,
                $"Expected 404 for disabled desk '{deskUrl}', but got {res.StatusCode}");
        }
    }

    [Fact]
    public async Task StaffUser_Gets403_OnPartnerQuickSignAndPayoutApprove()
    {
        // Issue staff token without partner permissions
        var staffToken = api.Services.GetRequiredService<TokenIssuer>()
            .Issue(Guid.NewGuid(), api.FirmA.FirmId, ["Staff"], ["clients.read", "audit.read"]);
        var staffClient = api.WithToken(staffToken);

        // 1. Quick-Sign endpoint requires Perms.AuditSignoff -> 403 Forbidden
        var signRes = await staffClient.PostAsJsonAsync($"/api/partner-companion/audit/{Guid.NewGuid()}/quick-sign",
            new QuickSignAuditRequest("24123456AAAA123456", "054321", "Attested"));
        Assert.Equal(HttpStatusCode.Forbidden, signRes.StatusCode);

        // 2. Authorize Payout endpoint requires Perms.BankingPayoutApprove -> 403 Forbidden
        var payoutRes = await staffClient.PostAsJsonAsync($"/api/connected-banking/payouts/{Guid.NewGuid()}/authorize",
            new { AuthMethod = "OTP", OtpCode = "123456" });
        Assert.Equal(HttpStatusCode.Forbidden, payoutRes.StatusCode);

        // 3. Admin apply plan upgrade requires Perms.SettingsManage -> 403 Forbidden
        var upgradeRes = await staffClient.PostAsJsonAsync("/api/subscriptions/admin/apply-upgrade",
            new { FirmId = api.FirmA.FirmId, TargetPlan = "PRO", BillingInterval = "Monthly" });
        Assert.Equal(HttpStatusCode.Forbidden, upgradeRes.StatusCode);
    }

    [Fact]
    public async Task FeatureFlags_EnablingForFirmA_DoesNotEnableForFirmB()
    {
        var firmA = await api.SignUpAsync("SecFlag A", plan: "ENTERPRISE");
        var firmB = await api.SignUpAsync("SecFlag B", plan: "ENTERPRISE");
        var clientA = api.ClientFor(firmA.FirmId);
        var clientB = api.ClientFor(firmB.FirmId);

        // Initially both get 404 for E-Invoicing
        var aInit = await clientA.GetAsync("/api/einvoicing/summary");
        Assert.Equal(HttpStatusCode.NotFound, aInit.StatusCode);

        var bInit = await clientB.GetAsync("/api/einvoicing/summary");
        Assert.Equal(HttpStatusCode.NotFound, bInit.StatusCode);

        // Enable einvoicing feature flag for Firm A
        var enableRes = await clientA.PostAsJsonAsync("/api/features/einvoicing", new { Enabled = true });
        Assert.Equal(HttpStatusCode.OK, enableRes.StatusCode);

        // Firm A can now access E-Invoicing (HTTP 200)
        var aAfter = await clientA.GetAsync("/api/einvoicing/summary");
        Assert.Equal(HttpStatusCode.OK, aAfter.StatusCode);

        // Multi-tenant isolation: Firm B still gets 404 Not Found
        var bAfter = await clientB.GetAsync("/api/einvoicing/summary");
        Assert.Equal(HttpStatusCode.NotFound, bAfter.StatusCode);
    }
}
