using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.FixedAssets;
using CaDesk.Domain.FixedAssets;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 25: Fixed Asset Register & Dual Depreciation Engine.
/// Tests asset creation, Schedule II vs. Section 32 dual depreciation runs,
/// 180-day rule, additional depreciation, AS 22 deferred tax, and multi-tenant firm isolation.
/// </summary>
public class FixedAssetTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Asset Advisory LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Tata Steel Manufacturing Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task FixedAsset_Lifecycle_Registers_CalculatesDualDepreciation_And_Verifies()
    {
        var (http, _) = await NewFirmAsync("KPMG Asset Practice");
        var client = await CreateClientAsync(http, "Bharat Forge Heavy Industries");

        // 1. Create Fixed Asset
        var createReq = new SaveFixedAssetRequest(
            ClientId: client.Id,
            AssetTag: "P&M-2024-001",
            Name: "Heavy CNC Milling Machine 5-Axis",
            AssetClass: AssetClass.PlantAndMachinery,
            PurchaseDate: new DateOnly(2024, 4, 15),
            CapitalizationDate: new DateOnly(2024, 5, 1), // > 180 days in FY 2024-25
            PurchaseCost: 1_000_000m,
            InwardFreight: 50_000m,
            InstallationCost: 100_000m,
            PreOperatingCost: 50_000m,
            GstItcClaimed: 180_000m, // 1,000,000 + 50,000 + 100,000 + 50,000 - 180,000 = 1,020,000 Gross Cost
            SalvageValue: 51_000m, // 5%
            UsefulLifeYears: 10m,
            CompanyDepreciationMethod: CompanyDepreciationMethod.SLM,
            ShiftType: ShiftType.DoubleShift, // 1.5x shift multiplier
            IncomeTaxBlockRatePercent: 15m,
            IsEligibleForAdditionalDepreciation: true, // +20% additional depr
            Location: "Pune Plant 2",
            Department: "Machining Division",
            CustodianName: "Vikram Malhotra"
        );

        var createRes = await http.PostAsJsonAsync("/api/fixed-assets", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var asset = await createRes.Content.ReadFromJsonAsync<FixedAssetDto>();
        Assert.NotNull(asset);
        Assert.Equal("P&M-2024-001", asset.AssetTag);
        Assert.Equal(1_020_000m, asset.GrossCost);
        Assert.Equal(AssetStatus.Active, asset.Status);

        // 2. Run Dual Depreciation Engine for FY 2024-25
        var deprReq = new RunDualDepreciationRequest(
            ClientId: client.Id,
            FinancialYear: "2024-25",
            AssetId: asset.Id,
            CorporateTaxRate: 0.2517m
        );

        var deprRes = await http.PostAsJsonAsync("/api/fixed-assets/depreciation", deprReq);
        Assert.Equal(HttpStatusCode.OK, deprRes.StatusCode);
        var entries = await deprRes.Content.ReadFromJsonAsync<List<AssetDepreciationEntryDto>>();
        Assert.NotNull(entries);
        Assert.Single(entries);

        var entry = entries[0];
        Assert.Equal("2024-25", entry.FinancialYear);
        Assert.Equal(1_020_000m, entry.OpeningBookValue);
        Assert.Equal(1_020_000m, entry.OpeningTaxWdv);

        // Schedule II SLM: Depreciable: 1,020,000 - 51,000 = 969,000. Annual: 96,900. Double shift (1.5x) = 145,350.
        // Active days: May 1 to Mar 31 = 335 / 365. Prorated: 145,350 * (335/365) ~ 133,404.25
        Assert.True(entry.BookDepreciation > 130_000m);

        // Section 32: Days = 335 (>= 180 days) -> Full 15% Normal (153,000) + 20% Additional on actual cost (204,000) = 357,000
        Assert.False(entry.IsUnder180Days);
        Assert.Equal(357_000m, entry.TaxDepreciation);

        // Timing difference = TaxDepr - BookDepr > 0 -> Deferred Tax Liability
        Assert.True(entry.TimingDifference > 0m);
        Assert.True(entry.DeferredTaxImpact > 0m);

        // 3. Record Physical Verification
        var verReq = new RecordPhysicalVerificationRequest(
            VerificationDate: new DateOnly(2024, 11, 15),
            VerifiedBy: "Ashok Verma, Senior Auditor",
            Status: PhysicalVerificationStatus.FoundAndGood,
            Remarks: "Asset physically verified on factory floor; operational and calibrated.",
            LocationVerified: "Pune Plant 2 - Bay 4"
        );

        var verRes = await http.PostAsJsonAsync($"/api/fixed-assets/{asset.Id}/verifications", verReq);
        Assert.Equal(HttpStatusCode.Created, verRes.StatusCode);
        var ver = await verRes.Content.ReadFromJsonAsync<AssetPhysicalVerificationDto>();
        Assert.NotNull(ver);
        Assert.Equal(PhysicalVerificationStatus.FoundAndGood, ver.Status);

        // 4. Check Summary
        var sumRes = await http.GetAsync($"/api/fixed-assets/summary?clientId={client.Id}&financialYear=2024-25");
        Assert.Equal(HttpStatusCode.OK, sumRes.StatusCode);
        var summary = await sumRes.Content.ReadFromJsonAsync<FixedAssetSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalAssets);
        Assert.Equal(1, summary.ActiveAssets);
        Assert.Equal(1_020_000m, summary.TotalGrossBlock);
        Assert.Equal(1, summary.TotalVerifications);
        Assert.Equal(100m, summary.VerificationCompliancePercent);
    }

    [Fact]
    public async Task FixedAsset_MultiTenant_Isolation_FirmB_Cannot_Access_FirmA_Assets()
    {
        var (httpA, _) = await NewFirmAsync("Firm A Asset Management");
        var (httpB, _) = await NewFirmAsync("Firm B Unauthorized Firm");

        var clientA = await CreateClientAsync(httpA, "Client Alpha Industries");

        // Firm A creates an asset
        var createReq = new SaveFixedAssetRequest(
            ClientId: clientA.Id,
            AssetTag: "ALPHA-FA-001",
            Name: "Confidential R&D Server Cluster",
            AssetClass: AssetClass.ComputersAndSoftware,
            PurchaseDate: new DateOnly(2024, 6, 1),
            CapitalizationDate: new DateOnly(2024, 6, 1),
            PurchaseCost: 500_000m,
            InwardFreight: 0m,
            InstallationCost: 0m,
            PreOperatingCost: 0m,
            GstItcClaimed: 0m,
            SalvageValue: 25_000m,
            UsefulLifeYears: 3m,
            CompanyDepreciationMethod: CompanyDepreciationMethod.SLM,
            ShiftType: ShiftType.SingleShift,
            IncomeTaxBlockRatePercent: 40m,
            IsEligibleForAdditionalDepreciation: false,
            Location: "Data Center Alpha",
            Department: "IT",
            CustodianName: "Admin"
        );

        var resA = await httpA.PostAsJsonAsync("/api/fixed-assets", createReq);
        Assert.Equal(HttpStatusCode.Created, resA.StatusCode);
        var assetA = await resA.Content.ReadFromJsonAsync<FixedAssetDto>();
        Assert.NotNull(assetA);

        // Firm B tries to read Firm A's asset -> 404 NotFound
        var resBRead = await httpB.GetAsync($"/api/fixed-assets/{assetA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resBRead.StatusCode);

        // Firm B tries to list assets -> empty list
        var resBList = await httpB.GetAsync("/api/fixed-assets");
        Assert.Equal(HttpStatusCode.OK, resBList.StatusCode);
        var listB = await resBList.Content.ReadFromJsonAsync<List<FixedAssetDto>>();
        Assert.NotNull(listB);
        Assert.Empty(listB);

        // Firm B tries to run depreciation on Firm A's client/asset -> returns empty list
        var resBDepr = await httpB.PostAsJsonAsync("/api/fixed-assets/depreciation", new RunDualDepreciationRequest(
            ClientId: clientA.Id,
            FinancialYear: "2024-25",
            AssetId: assetA.Id
        ));
        Assert.Equal(HttpStatusCode.OK, resBDepr.StatusCode);
        var entriesB = await resBDepr.Content.ReadFromJsonAsync<List<AssetDepreciationEntryDto>>();
        Assert.NotNull(entriesB);
        Assert.Empty(entriesB);
    }
}
