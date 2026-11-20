using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Dsc;
using CaDesk.Domain.Dsc;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 12: Digital Signature Certificates (DSC) & Token Management.
/// Tests DSC register creation, expiry tracking, custody transfer with movement history, and tenant isolation.
/// </summary>
public class DscTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "DSC Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Apex Traders")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Dsc_Create_ComputesStatusAndInitialMovementLog()
    {
        var (http, userId) = await NewFirmAsync("DSC Registry Firm");
        var client = await CreateClientAsync(http, "Sigma Tech");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var validFrom = today.AddYears(-1);
        var validTo = today.AddYears(1); // Active (> 30 days)

        var createRes = await http.PostAsJsonAsync("/api/dsc", new CreateDscRequest(
            client.Id,
            "Rajesh Sharma",
            "ABCDE1234F",
            DscType.Class3,
            "eMudhra",
            validFrom,
            validTo,
            "EM-2024-88912",
            "TOKEN-USB-01",
            DscLocation.Vault,
            null,
            "Original token kept in office vault"
        ));

        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var dsc = (await createRes.Content.ReadFromJsonAsync<DscDto>())!;

        Assert.Equal("Rajesh Sharma", dsc.HolderName);
        Assert.Equal("ABCDE1234F", dsc.Pan);
        Assert.Equal(DscStatus.Active, dsc.Status);
        Assert.Equal(DscLocation.Vault, dsc.Location);
        Assert.True(dsc.DaysRemaining > 300);
        Assert.Single(dsc.MovementLogs);
        Assert.Equal("vault", dsc.MovementLogs[0].ToLocation);
    }

    [Fact]
    public async Task Dsc_TransferCustody_RecordsAuditTrail()
    {
        var (http, userId) = await NewFirmAsync("Custody Firm");
        var client = await CreateClientAsync(http, "Omega Retail");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var createRes = await http.PostAsJsonAsync("/api/dsc", new CreateDscRequest(
            client.Id,
            "Anita Roy",
            "BCDEF2345G",
            DscType.Class3,
            "Pantasync",
            today.AddYears(-1),
            today.AddDays(20), // Expiring soon (within 30 days)
            "SN-1002",
            "TOKEN-USB-02",
            DscLocation.Vault,
            null,
            "Client handed over token for GST annual filing"
        ));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var dsc = (await createRes.Content.ReadFromJsonAsync<DscDto>())!;
        Assert.Equal(DscStatus.ExpiringSoon, dsc.Status);

        // Transfer custody to staff
        var transferRes = await http.PostAsJsonAsync($"/api/dsc/{dsc.Id}/transfer", new TransferCustodyRequest(
            DscLocation.WithStaff,
            userId,
            "Checked out by tax associate for GSTR-9 signing"
        ));
        Assert.Equal(HttpStatusCode.OK, transferRes.StatusCode);
        var updated = (await transferRes.Content.ReadFromJsonAsync<DscDto>())!;

        Assert.Equal(DscLocation.WithStaff, updated.Location);
        Assert.Equal(userId, updated.CustodianUserId);
        Assert.Equal(2, updated.MovementLogs.Count);

        var latestLog = updated.MovementLogs[0];
        Assert.Equal(DscLocation.Vault, latestLog.FromLocation);
        Assert.Equal(DscLocation.WithStaff, latestLog.ToLocation);
        Assert.Equal("Checked out by tax associate for GSTR-9 signing", latestLog.Remarks);
    }

    [Fact]
    public async Task Dsc_Summary_ReflectsAccurateCounts()
    {
        var (http, _) = await NewFirmAsync("Summary Firm");
        var client = await CreateClientAsync(http, "Zeta Logistics");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Active in vault
        await http.PostAsJsonAsync("/api/dsc", new CreateDscRequest(
            client.Id, "User 1", "ABCDE1234F", DscType.Class3, "eMudhra",
            today.AddYears(-1), today.AddYears(1), null, null, DscLocation.Vault, null, null));

        // 2. Expired with client
        await http.PostAsJsonAsync("/api/dsc", new CreateDscRequest(
            client.Id, "User 2", "BCDEF2345G", DscType.Class3, "Capricorn",
            today.AddYears(-2), today.AddDays(-10), null, null, DscLocation.WithClient, null, null));

        var summary = await http.GetFromJsonAsync<DscSummaryDto>("/api/dsc/summary");
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalActive);
        Assert.Equal(1, summary.Expired);
        Assert.Equal(1, summary.InVault);
        Assert.Equal(1, summary.CheckedOut);
    }

    [Fact]
    public async Task Dsc_TenantIsolation_CrossFirmAccessBlocked()
    {
        var (firmA, _) = await NewFirmAsync("Firm A DSC");
        var (firmB, _) = await NewFirmAsync("Firm B DSC");
        var clientA = await CreateClientAsync(firmA, "Firm A Client");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var createRes = await firmA.PostAsJsonAsync("/api/dsc", new CreateDscRequest(
            clientA.Id, "Director A", "ABCDE1234F", DscType.Class3, "eMudhra",
            today.AddYears(-1), today.AddYears(1), null, null, DscLocation.Vault, null, null));
        var dscA = (await createRes.Content.ReadFromJsonAsync<DscDto>())!;

        // Firm B tries to get Firm A's DSC
        var getRes = await firmB.GetAsync($"/api/dsc/{dscA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Firm B tries to transfer Firm A's DSC
        var transferRes = await firmB.PostAsJsonAsync($"/api/dsc/{dscA.Id}/transfer",
            new TransferCustodyRequest(DscLocation.WithStaff, null, "Illicit transfer"));
        Assert.Equal(HttpStatusCode.NotFound, transferRes.StatusCode);

        // Firm B list does not contain Firm A's DSC
        var listB = await firmB.GetFromJsonAsync<List<DscDto>>("/api/dsc");
        Assert.NotNull(listB);
        Assert.DoesNotContain(listB, x => x.Id == dscA.Id);
    }
}
