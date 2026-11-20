using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.BankFeeds;
using CaDesk.Application.Clients;
using CaDesk.Domain.BankFeeds;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 20: Cash & Bank Feed Integration, Multi-Bank Statement Parser & BRS Auto-Matcher.
/// Tests Bank Account CRUD, CSV statement uploading, auto-categorization heuristics,
/// manual reconciliation matching, BRS summary calculations, and multi-tenant firm isolation.
/// </summary>
public class BankFeedTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Banking Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Tata Motors Ltd")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Bank_Accounts_CRUD_And_Statement_Upload_With_Auto_Categorization_Succeeds()
    {
        var (http, _) = await NewFirmAsync("Deloitte Advisory");
        var client = await CreateClientAsync(http, "Tata Motors Ltd");

        // 1. Create Bank Account
        var createAccReq = new CreateBankAccountRequest(
            ClientId: client.Id,
            AccountName: "Main Operations Account",
            BankName: "HDFC Bank",
            BranchName: "Nariman Point Mumbai",
            AccountNumber: "50200098765432",
            IfscCode: "HDFC0000123",
            AccountType: BankAccountType.Current,
            OpeningBalance: 500_000m,
            Currency: "INR"
        );

        var accRes = await http.PostAsJsonAsync("/api/bank/accounts", createAccReq);
        Assert.Equal(HttpStatusCode.Created, accRes.StatusCode);
        var account = (await accRes.Content.ReadFromJsonAsync<BankAccountDto>())!;
        Assert.Equal("Main Operations Account", account.AccountName);
        Assert.Equal("HDFC0000123", account.IfscCode);
        Assert.Equal(500_000m, account.OpeningBalance);

        // 2. Upload CSV Statement
        var sampleCsv = """
Date,Narration,Chq/Ref No,Debit,Credit,Balance
01/11/2026,UPI/889900/Vendor Parts Payment,UPI889900,25000.00,,475000.00
05/11/2026,SALARY NOV 2026,SAL1101,150000.00,,325000.00
10/11/2026,NEFT/Client Fee Collection,NEFT4422,,75000.00,400000.00
15/11/2026,BANK SMS & ANNUAL CHARGES,CHG99,500.00,,399500.00
""";

        var uploadReq = new UploadStatementRequest(
            FileName: "hdfc_nov_2026.csv",
            CsvContent: sampleCsv,
            StatementDate: new DateOnly(2026, 11, 15)
        );

        var stmtRes = await http.PostAsJsonAsync($"/api/bank/accounts/{account.Id}/statements", uploadReq);
        Assert.Equal(HttpStatusCode.Created, stmtRes.StatusCode);
        var stmt = (await stmtRes.Content.ReadFromJsonAsync<BankStatementDto>())!;
        Assert.Equal(4, stmt.TotalTransactions);
        Assert.Equal(175500.00m, stmt.TotalDebits);
        Assert.Equal(75000.00m, stmt.TotalCredits);
        Assert.Equal(399500.00m, stmt.ClosingBalance);

        // 3. Fetch Transactions & Verify Auto-Categorization
        var txRes = await http.GetAsync($"/api/bank/accounts/{account.Id}/transactions");
        Assert.Equal(HttpStatusCode.OK, txRes.StatusCode);
        var transactions = (await txRes.Content.ReadFromJsonAsync<List<BankTransactionDto>>())!;
        Assert.Equal(4, transactions.Count);

        var salaryTx = transactions.Single(t => t.Narration.Contains("SALARY"));
        Assert.Equal("Payroll / Salaries", salaryTx.Category);
        Assert.Equal(BankMatchStatus.AutoCategorized, salaryTx.MatchStatus);

        var chargesTx = transactions.Single(t => t.Narration.Contains("CHARGES"));
        Assert.Contains("Charges", chargesTx.Category ?? "");

        // 4. Match Transaction to an entity
        var matchReq = new MatchTransactionRequest(
            MatchStatus: BankMatchStatus.Matched,
            MatchedEntityType: BankMatchedEntityType.Expense,
            MatchedEntityId: Guid.NewGuid(),
            Notes: "Verified with payroll approval voucher"
        );

        var matchRes = await http.PostAsJsonAsync($"/api/bank/transactions/{salaryTx.Id}/match", matchReq);
        Assert.Equal(HttpStatusCode.OK, matchRes.StatusCode);
        var matchedTx = (await matchRes.Content.ReadFromJsonAsync<BankTransactionDto>())!;
        Assert.Equal(BankMatchStatus.Matched, matchedTx.MatchStatus);
        Assert.Equal(BankMatchedEntityType.Expense, matchedTx.MatchedEntityType);

        // 5. Fetch BRS Summary
        var brsRes = await http.GetAsync($"/api/bank/accounts/{account.Id}/brs");
        Assert.Equal(HttpStatusCode.OK, brsRes.StatusCode);
        var brs = (await brsRes.Content.ReadFromJsonAsync<BankReconciliationSummaryDto>())!;
        Assert.Equal(account.Id, brs.BankAccountId);
        Assert.Equal(1, brs.MatchedCount);
        Assert.Equal(3, brs.UnmatchedCount);
    }

    [Fact]
    public async Task Bank_Feeds_MultiTenant_Isolation_FirmA_Cannot_Access_FirmB()
    {
        var (httpA, _) = await NewFirmAsync("Firm Alpha Advisory");
        var (httpB, _) = await NewFirmAsync("Firm Beta Partners");

        var clientA = await CreateClientAsync(httpA, "Client of Alpha");

        var createReq = new CreateBankAccountRequest(
            ClientId: clientA.Id,
            AccountName: "Alpha Secure Vault",
            BankName: "State Bank of India",
            BranchName: "Fort Branch",
            AccountNumber: "1002003004005",
            IfscCode: "SBIN0001234",
            AccountType: BankAccountType.Current,
            OpeningBalance: 1_000_000m,
            Currency: "INR"
        );

        var resA = await httpA.PostAsJsonAsync("/api/bank/accounts", createReq);
        Assert.Equal(HttpStatusCode.Created, resA.StatusCode);
        var accountA = (await resA.Content.ReadFromJsonAsync<BankAccountDto>())!;

        // Firm B lists bank accounts -> Should see 0 accounts
        var listResB = await httpB.GetAsync("/api/bank/accounts");
        Assert.Equal(HttpStatusCode.OK, listResB.StatusCode);
        var accountsB = (await listResB.Content.ReadFromJsonAsync<List<BankAccountDto>>())!;
        Assert.DoesNotContain(accountsB, a => a.Id == accountA.Id);

        // Firm B attempts direct lookup of Firm A's account -> 404 / 500 KeyNotFound
        var directResB = await httpB.GetAsync($"/api/bank/accounts/{accountA.Id}");
        Assert.False(directResB.IsSuccessStatusCode);
    }
}
