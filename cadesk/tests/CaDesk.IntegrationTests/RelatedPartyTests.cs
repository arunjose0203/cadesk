using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CaDesk.Application.Clients;
using CaDesk.Application.RelatedParty;
using Xunit;

namespace CaDesk.IntegrationTests;

public class RelatedPartyTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "BDO Corporate Governance Advisory LLP")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Bharat Manufacturing Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task RelatedParty_FullLifecycle_Entity_Transaction_Thresholds_Omnibus_Reports_And_Isolation()
    {
        var (httpA, _) = await NewFirmAsync("Grant Thornton Secretarial Advisory");
        var clientA = await CreateClientAsync(httpA, "Apex Engineering Industries Ltd");

        // 1. Create Related Party Entity
        var createEntityReq = new SaveRelatedPartyEntityRequest(
            ClientId: clientA.Id,
            Name: "Apex Component Technologies Pvt Ltd",
            RelationshipType: "Subsidiary",
            Pan: "AAACA5555M",
            Cin: "U28910MH2020PTC123456",
            DirectorOrKmpName: "Sunil K. Deshmukh (Managing Director)",
            NatureOfInterest: "Holding 75% equity share capital",
            IsActive: true
        );

        var entityRes = await httpA.PostAsJsonAsync("/api/related-party/entities", createEntityReq);
        entityRes.EnsureSuccessStatusCode();
        var entity = (await entityRes.Content.ReadFromJsonAsync<RelatedPartyEntityDto>())!;
        Assert.NotNull(entity);
        Assert.Equal("Apex Component Technologies Pvt Ltd", entity.Name);
        Assert.Equal("Subsidiary", entity.RelationshipType);

        // 2. Create Section 177 Omnibus Approval
        var createOmnibusReq = new SaveOmnibusApprovalRequest(
            ClientId: clientA.Id,
            FinancialYear: "2024-25",
            AuditCommitteeMeetingDate: DateTime.UtcNow.AddDays(-30),
            Title: "Omnibus Approval for Inter-Company Component Sales FY 24-25",
            MaximumApprovedLimit: 50000000m, // 5 Cr limit
            ApprovalCriteria: "Transactions in ordinary course of business benchmarked under Cost Plus Method",
            ValidityPeriodTo: DateTime.UtcNow.AddDays(300)
        );

        var omnibusRes = await httpA.PostAsJsonAsync("/api/related-party/omnibus", createOmnibusReq);
        omnibusRes.EnsureSuccessStatusCode();
        var omnibus = (await omnibusRes.Content.ReadFromJsonAsync<RelatedPartyOmnibusApprovalDto>())!;
        Assert.NotNull(omnibus);
        Assert.Equal(50000000m, omnibus.MaximumApprovedLimit);

        // 3. Create Related Party Transaction
        var createTxReq = new SaveRelatedPartyTransactionRequest(
            ClientId: clientA.Id,
            RelatedPartyId: entity.Id,
            FinancialYear: "2024-25",
            TransactionDate: DateTime.UtcNow.AddDays(-15),
            ContractReference: "CTR-RPT-2024-001",
            TransactionType: "SaleOfGoods",
            Description: "Supply of precision aluminum forged components",
            TransactionAmount: 25000000m, // 2.5 Cr
            Currency: "INR",
            AnnualTurnoverBenchmark: 150000000m, // 15 Cr turnover (10% threshold = 1.5 Cr => exceeds limit)
            NetWorthBenchmark: 80000000m,
            IsOrdinaryCourseOfBusiness: true,
            ArmLengthMethod: "CostPlusMethod",
            BenchmarkArmLengthPrice: 24500000m, // Market price 2.45 Cr (within 3% tolerance)
            AuditCommitteeApprovalStatus: "OmnibusApproved",
            AuditCommitteeMeetingDate: DateTime.UtcNow.AddDays(-30),
            OmnibusApprovalId: omnibus.Id,
            BoardResolutionDate: DateTime.UtcNow.AddDays(-28),
            ShareholdersResolutionDate: null,
            Mbp4EnteredDate: DateTime.UtcNow.AddDays(-10),
            OutstandingBalanceAtYearEnd: 4500000m,
            BalanceType: "Debit"
        );

        var txRes = await httpA.PostAsJsonAsync("/api/related-party/transactions", createTxReq);
        txRes.EnsureSuccessStatusCode();
        var tx = (await txRes.Content.ReadFromJsonAsync<RelatedPartyTransactionDto>())!;
        Assert.NotNull(tx);
        Assert.Equal(25000000m, tx.TransactionAmount);
        Assert.True(tx.ExceedsStatutoryThreshold); // 2.5 Cr >= 1.5 Cr
        Assert.True(tx.IsArmLength); // within 3% tolerance
        Assert.False(tx.RequiresShareholderApproval); // Ordinary course + Arm length = 4th proviso exempt!

        // 4. Check Summary API
        var summaryRes = await httpA.GetFromJsonAsync<RelatedPartySummaryDto>($"/api/related-party/summary?clientId={clientA.Id}&fy=2024-25");
        Assert.NotNull(summaryRes);
        Assert.Equal(1, summaryRes.TotalParties);
        Assert.Equal(1, summaryRes.TotalTransactions);
        Assert.Equal(25000000m, summaryRes.TotalVolumeInr);
        Assert.Equal(100m, summaryRes.ArmLengthComplianceRate);
        Assert.Equal(0, summaryRes.SpecialResolutionRequiredCount);
        Assert.Equal(1, summaryRes.AuditCommitteeApprovedCount);

        // 5. Generate Reports (MBP-4 Register and AS 18 Disclosure Note)
        var reportsRes = await httpA.GetFromJsonAsync<RelatedPartyReportsDto>($"/api/related-party/reports?clientId={clientA.Id}&fy=2024-25");
        Assert.NotNull(reportsRes);
        Assert.Contains("FORM MBP - 4", reportsRes.Mbp4RegisterText);
        Assert.Contains("Apex Engineering Industries Ltd", reportsRes.Mbp4RegisterText);
        Assert.Contains("Apex Component Technologies Pvt Ltd", reportsRes.Mbp4RegisterText);
        Assert.Contains("RELATED PARTY DISCLOSURES (AS 18 / Ind AS 24)", reportsRes.As18DisclosureNoteText);
        Assert.Contains("Subsidiary", reportsRes.As18DisclosureNoteText);

        // 6. Two-Lock Isolation Check: Firm B cannot access Firm A's transaction
        var (httpB, _) = await NewFirmAsync("Mazars Statutory Audit LLP");
        var firmBGetRes = await httpB.GetAsync($"/api/related-party/transactions/{tx.Id}");
        Assert.Equal(HttpStatusCode.NotFound, firmBGetRes.StatusCode);
    }
}
