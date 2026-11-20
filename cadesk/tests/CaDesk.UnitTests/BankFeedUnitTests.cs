using CaDesk.Application.BankFeeds;
using CaDesk.Domain.BankFeeds;

namespace CaDesk.UnitTests;

public class BankFeedUnitTests
{
    [Fact]
    public void ParseCsv_ValidStandardCsv_ExtractsTransactionsAndBalances()
    {
        var csv = """
Date,Narration,Chq/Ref No,Debit,Credit,Balance
01/10/2026,UPI/12345/Vendor Payment,UPI12345,5000.00,,95000.00
03/10/2026,NEFT/Client Fee Collection,NEFT9988,,25000.00,120000.00
05/10/2026,SALARY OCTOBER 2026,SAL001,40000.00,,80000.00
10/10/2026,BANK CHARGES + SMS,CHG11,250.00,,79750.00
""";

        var result = BankStatementParser.ParseCsv(csv);

        Assert.Equal(4, result.Items.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), result.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 10), result.EndDate);
        Assert.Equal(45250.00m, result.TotalDebits);
        Assert.Equal(25000.00m, result.TotalCredits);
        Assert.Equal(79750.00m, result.ClosingBalance);

        // Individual item checks
        var salaryItem = result.Items.Single(x => x.Narration.Contains("SALARY"));
        Assert.Equal("Debit", salaryItem.Type);
        Assert.Equal(40000.00m, salaryItem.Amount);
        Assert.Equal("SAL001", salaryItem.ChqRefNo);
    }

    [Fact]
    public void ParseCsv_AmountAndTypeFormat_ParsesCorrectly()
    {
        var csv = """
Date,Description,Reference,Amount,Type,Balance
2026-11-01,Office Rent,CHQ1001,15000.00,DR,85000.00
2026-11-05,Consulting Income,RTGS555,50000.00,CR,135000.00
""";

        var result = BankStatementParser.ParseCsv(csv);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(15000.00m, result.TotalDebits);
        Assert.Equal(50000.00m, result.TotalCredits);
        Assert.Equal("Debit", result.Items[0].Type);
        Assert.Equal("Credit", result.Items[1].Type);
    }

    [Fact]
    public void ParseCsv_EmptyContent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BankStatementParser.ParseCsv(""));
        Assert.Throws<ArgumentException>(() => BankStatementParser.ParseCsv("   "));
    }

    [Fact]
    public void CategorizeTransaction_DefaultHeuristics_MatchesExpectedCategories()
    {
        var rules = Array.Empty<BankReconciliationRule>();

        var (catSalary, status1) = BankReconciliationEngine.CategorizeTransaction("SALARY TO STAFF", "Debit", rules);
        Assert.Equal("Payroll / Salaries", catSalary);
        Assert.Equal(BankMatchStatus.AutoCategorized, status1);

        var (catGst, _) = BankReconciliationEngine.CategorizeTransaction("GST PMT OCT26", "Debit", rules);
        Assert.Equal("GST Payment", catGst);

        var (catUpi, _) = BankReconciliationEngine.CategorizeTransaction("UPI-9876543210-Merchant", "Debit", rules);
        Assert.Equal("UPI Payments & Collections", catUpi);

        var (catCharges, _) = BankReconciliationEngine.CategorizeTransaction("SMS ALERT CHARGES", "Debit", rules);
        Assert.Equal("Bank Charges & SMS Fees", catCharges);
    }

    [Fact]
    public void CategorizeTransaction_CustomRuleTakesPriorityOverDefault()
    {
        var customRule = new BankReconciliationRule
        {
            Id = Guid.NewGuid(),
            RuleName = "Custom Vendor Rule",
            MatchKeyword = "ACME",
            TransactionType = "Debit",
            AssignCategory = "Key Supplier - Acme Corp",
            Priority = 100,
            IsActive = true
        };

        var (category, status) = BankReconciliationEngine.CategorizeTransaction("NEFT TO ACME ENTERPRISES", "Debit", [customRule]);

        Assert.Equal("Key Supplier - Acme Corp", category);
        Assert.Equal(BankMatchStatus.AutoCategorized, status);
    }

    [Fact]
    public void CalculateBrsSummary_ComputesCorrectDifferences()
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountName = "Primary Current A/C",
            BankName = "HDFC Bank",
            AccountNumber = "50200012345678",
            OpeningBalance = 100000.00m,
            CurrentBalance = 100000.00m
        };

        var transactions = new List<BankTransaction>
        {
            // Unmatched Debit in Bank = Direct debit not yet booked in ledger
            new()
            {
                Id = Guid.NewGuid(),
                BankAccountId = account.Id,
                TransactionDate = new DateOnly(2026, 11, 10),
                Narration = "Bank Locker Charges",
                Category = "Bank Charges",
                TransactionType = "Debit",
                Amount = 2500.00m,
                MatchStatus = BankMatchStatus.Unmatched
            },
            // Unmatched Credit in Bank = Direct credit not yet booked in ledger
            new()
            {
                Id = Guid.NewGuid(),
                BankAccountId = account.Id,
                TransactionDate = new DateOnly(2026, 11, 12),
                Narration = "Bank Interest Received",
                Category = "Bank Interest",
                TransactionType = "Credit",
                Amount = 15000.00m,
                MatchStatus = BankMatchStatus.Unmatched
            },
            // Matched Debit
            new()
            {
                Id = Guid.NewGuid(),
                BankAccountId = account.Id,
                TransactionDate = new DateOnly(2026, 11, 15),
                Narration = "Vendor Cheque Cleared",
                TransactionType = "Debit",
                Amount = 20000.00m,
                MatchStatus = BankMatchStatus.Matched
            }
        };

        var summary = BankReconciliationEngine.CalculateBrsSummary(account, transactions, new DateOnly(2026, 11, 30));

        Assert.Equal(account.CurrentBalance, summary.BankStatementBalance);
        Assert.Equal(2500.00m, summary.DirectDebitsNotBooked);
        Assert.Equal(15000.00m, summary.DirectCreditsNotBooked);
        Assert.Equal(2, summary.UnmatchedCount);
        Assert.Equal(1, summary.MatchedCount);
    }
}
