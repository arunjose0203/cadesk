using System;
using System.Linq;
using CaDesk.Application.TrialBalance;
using CaDesk.Domain.TrialBalance;
using Xunit;

namespace CaDesk.UnitTests;

/// <summary>Parser sign convention, mapping rules, multi-format ingestion, and derived figures.</summary>
public class TrialBalanceRulesUnitTests
{
    private static TrialBalanceLedger Ledger(string name, string tallyGroup, decimal closing, decimal opening = 0) =>
        new() { LedgerName = name, TallyGroup = tallyGroup, ClosingBalance = closing, OpeningBalance = opening };

    private static TrialBalanceLedger Mapped(string name, string tallyGroup, decimal closing, decimal opening = 0)
    {
        var l = Ledger(name, tallyGroup, closing, opening);
        l.IsMapped = TrialBalanceMappingEngine.InferStandardMapping(l);
        return l;
    }

    [Fact]
    public void Tally_native_xml_with_negative_debits_is_converted_to_debit_positive()
    {
        const string xml = """
            <ENVELOPE><LEDGER NAME="Cash"><PARENT>Cash-in-Hand</PARENT><CLOSINGBALANCE>-1000.00</CLOSINGBALANCE></LEDGER>
            <LEDGER NAME="ABC Traders"><PARENT>Sundry Debtors</PARENT><CLOSINGBALANCE>-4000.00</CLOSINGBALANCE></LEDGER>
            <LEDGER NAME="Capital"><PARENT>Capital Account</PARENT><CLOSINGBALANCE>5000.00</CLOSINGBALANCE></LEDGER></ENVELOPE>
            """;
        var parsed = TallyTrialBalanceParser.Parse(xml, "TallyXml");

        Assert.True(parsed.SignsInverted);
        Assert.Equal(1000m, parsed.Ledgers.Single(l => l.Name == "Cash").ClosingBalance);
        Assert.Equal(-5000m, parsed.Ledgers.Single(l => l.Name == "Capital").ClosingBalance);
        Assert.Equal(5000m, parsed.TotalDebit);
        Assert.True(parsed.IsBalanced);
    }

    [Fact]
    public void Debit_positive_files_are_left_alone()
    {
        const string csv = "Particulars,Group,Closing Balance\nCash,Cash-in-Hand,1000 Dr\nCapital,Capital Account,1000 Cr";
        var parsed = TallyTrialBalanceParser.Parse(csv, "TallyCsv");

        Assert.False(parsed.SignsInverted);
        Assert.Equal(1000m, parsed.Ledgers.Single(l => l.Name == "Cash").ClosingBalance);
    }

    [Fact]
    public void Xml_with_a_dtd_is_refused()
    {
        const string bomb = """<?xml version="1.0"?><!DOCTYPE lolz [<!ENTITY lol "lol"><!ENTITY lol2 "&lol;&lol;&lol;">]><ENVELOPE>&lol2;</ENVELOPE>""";
        Assert.Throws<ArgumentException>(() => TallyTrialBalanceParser.Parse(bomb, "TallyXml"));
    }

    [Theory]
    [InlineData("Bank Charges", "Indirect Expenses", TrialBalanceConstants.S3FinanceCosts)]
    [InlineData("Bank Charges", "", TrialBalanceConstants.S3FinanceCosts)]              // name alone: an expense, not a bank account
    [InlineData("Goods Transport", "Indirect Expenses", TrialBalanceConstants.S3OtherExpenses)] // "od" inside "goods" is not an overdraft
    [InlineData("Prepaid Expenses", "", TrialBalanceConstants.S3ShortTermLoansAndAdvances)]
    [InlineData("Outstanding Expenses", "", TrialBalanceConstants.S3OtherCurrentLiabilities)]
    [InlineData("Provision for Income Tax", "Provisions", TrialBalanceConstants.S3ShortTermProvisions)]
    [InlineData("Current Tax", "Indirect Expenses", TrialBalanceConstants.S3TaxExpense)]
    [InlineData("HDFC OD Account", "Bank OD A/c", TrialBalanceConstants.S3ShortTermBorrowings)]
    [InlineData("Carriage Inward", "Direct Expenses", TrialBalanceConstants.S3CostOfMaterialsConsumed)]
    public void Ledgers_map_by_tally_group_first_then_whole_words(string name, string group, string expected)
    {
        var l = Mapped(name, group, 1000m);
        Assert.True(l.IsMapped);
        Assert.Equal(expected, l.Schedule3Group);
    }

    [Fact]
    public void Other_income_is_not_turnover_for_the_tax_audit_test()
    {
        var l = Mapped("Interest Received", "Indirect Incomes", -1000m);
        Assert.Equal(TrialBalanceConstants.S3OtherIncome, l.Schedule3Group);
        Assert.NotEqual(TrialBalanceConstants.F3CdTurnover, l.Form3CdCategory);
    }

    [Fact]
    public void An_unrecognised_ledger_is_left_for_review_not_guessed_as_an_expense()
    {
        var l = Mapped("Ramesh Kumar A/c", "Promoter Funding", -500000m); // a personal account under the firm's own group
        Assert.False(l.IsMapped);
    }

    [Fact]
    public void A_client_rule_applies_to_that_exact_ledger_only()
    {
        var rent = Ledger("Rent", "", 1000m);
        var rentReceived = Ledger("Rent Received", "", -1000m);
        TrialBalanceMappingEngine.ApplyMappings([rent, rentReceived],
            [new ClientLedgerMapping { LedgerPattern = "Rent", Schedule3Group = TrialBalanceConstants.S3OtherExpenses }]);

        Assert.Equal(TrialBalanceConstants.S3OtherExpenses, rent.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.S3OtherIncome, rentReceived.Schedule3Group);
    }

    [Fact]
    public void Figures_respect_contra_balances_and_use_true_averages()
    {
        var ledgers = new[]
        {
            Mapped("Share Capital", "Capital Account", -1_000_000m),
            Mapped("Profit & Loss A/c", "Capital Account", 300_000m),        // accumulated loss: a debit inside reserves
            Mapped("HDFC Current Account", "Bank Accounts", 200_000m),
            Mapped("ICICI Current Account", "Bank Accounts", -50_000m),       // overdrawn: reduces cash and bank
            Mapped("Stock", "Stock-in-Hand", 400_000m, opening: 200_000m),
            Mapped("Sales", "Sales Accounts", -2_000_000m),
            Mapped("Purchases", "Purchase Accounts", 1_500_000m),
            Mapped("Current Tax", "Indirect Expenses", 100_000m),
        };
        var f = TrialBalanceSyncEngine.ComputeFigures(ledgers);

        Assert.Equal(700_000m, f.ShareholdersEquity);         // 10 lakh capital less 3 lakh accumulated loss
        Assert.Equal(550_000m, f.CurrentAssets);              // 4 lakh stock + 2 lakh bank - 0.5 lakh overdrawn
        Assert.Equal(300_000m, f.AverageInventory);           // (2 + 4) / 2 lakh
        Assert.Equal(500_000m, f.ProfitBeforeTax);
        Assert.Equal(400_000m, f.ProfitAfterTax);
    }

    // =========================================================================
    // SPRINT 42: UNIVERSAL IMPORTER (BUSY, SAP, EXCEL CLIPBOARD, FUZZY HEADERS)
    // =========================================================================

    [Fact]
    public void Busy_accounting_tab_delimited_file_with_dr_cr_suffixes_parses_accurately()
    {
        const string busyTsv =
            "Account Name\tParent Group\tOp. Bal.\tDebit (Rs.)\tCredit (Rs.)\tClosing Bal.\n" +
            "Capital A/c\tCapital A/c\t500000.00 Cr\t0.00\t0.00\t500000.00 Cr\n" +
            "Axis Bank Current A/c\tBank Accounts\t150000.00 Dr\t0.00\t0.00\t150000.00 Dr\n" +
            "Sundry Debtors - ABC Mart\tSundry Debtors\t350000.00 Dr\t0.00\t0.00\t350000.00 Dr\n";

        var parsed = UniversalTrialBalanceParser.Parse(busyTsv, "BusyCsv");

        Assert.Equal(3, parsed.Ledgers.Count);
        Assert.Equal(500000m, parsed.TotalDebit);
        Assert.Equal(500000m, parsed.TotalCredit);
        Assert.True(parsed.IsBalanced);

        var capital = parsed.Ledgers.Single(l => l.Name.Contains("Capital"));
        Assert.Equal(-500000m, capital.ClosingBalance); // Credit is negative in standard internal representation

        var bank = parsed.Ledgers.Single(l => l.Name.Contains("Axis Bank"));
        Assert.Equal(150000m, bank.ClosingBalance);
    }

    [Fact]
    public void Sap_gl_trial_balance_with_account_codes_and_local_currency_parses_accurately()
    {
        const string sapTsv =
            "G/L Account\tGL Description\tDebit Amount\tCredit Amount\tBalance in Local Currency\n" +
            "100000\tShare Capital - Equity\t0.00\t10000000.00\t-10000000.00\n" +
            "200000\tLong-Term Borrowings\t0.00\t5000000.00\t-5000000.00\n" +
            "300000\tTangible Fixed Assets - Hardware\t8000000.00\t0.00\t8000000.00\n" +
            "400000\tCustomer Accounts Receivable\t7000000.00\t0.00\t7000000.00\n";

        var parsed = UniversalTrialBalanceParser.Parse(sapTsv, "SapGl");

        Assert.Equal(4, parsed.Ledgers.Count);
        Assert.Equal(15000000m, parsed.TotalDebit);
        Assert.Equal(15000000m, parsed.TotalCredit);
        Assert.True(parsed.IsBalanced);

        var fa = parsed.Ledgers.Single(l => l.Name.Contains("Tangible"));
        Assert.Equal(8000000m, fa.DebitAmount);
        Assert.Equal(8000000m, fa.ClosingBalance);
    }

    [Fact]
    public void Excel_clipboard_tsv_paste_parses_and_balances()
    {
        // Direct clipboard TSV copied from Excel without explicit format declaration
        const string excelPaste =
            "Particulars\tGroup\tClosing Balance\n" +
            "Sales Revenue\tSales Accounts\t-1200000.00\n" +
            "Office Rent\tIndirect Expenses\t200000.00\n" +
            "Trade Debtors\tSundry Debtors\t600000.00\n" +
            "HDFC Current Account\tBank Accounts\t400000.00\n";

        var parsed = UniversalTrialBalanceParser.Parse(excelPaste, "Auto");

        Assert.Equal(4, parsed.Ledgers.Count);
        Assert.Equal(1200000m, parsed.TotalDebit);
        Assert.Equal(1200000m, parsed.TotalCredit);
        Assert.True(parsed.IsBalanced);
    }

    [Fact]
    public void Fuzzy_column_headers_in_arbitrary_casing_and_order_are_correctly_identified()
    {
        // Irregular columns: Cr first, then Dr, strange capitalization, punctuation
        const string weirdCsv =
            "Head of Account,  Classification / Parent , Cr. (INR) , Dr. (INR) \n" +
            "Equity Share Capital, Capital Account, 2500000, 0\n" +
            "Commercial Trucks, Fixed Assets, 0, 1500000\n" +
            "HDFC Bank Overdraft, Bank OD A/c, 500000, 0\n" +
            "Petty Cash Balance, Cash-in-Hand, 0, 1500000\n";

        var parsed = UniversalTrialBalanceParser.Parse(weirdCsv, "GenericCsv");

        Assert.Equal(4, parsed.Ledgers.Count);
        Assert.Equal(3000000m, parsed.TotalDebit);
        Assert.Equal(3000000m, parsed.TotalCredit);
        Assert.True(parsed.IsBalanced);
    }

    [Theory]
    [InlineData("1,23,456.78 Dr", 123456.78)]
    [InlineData("1,23,456.78 Cr", -123456.78)]
    [InlineData("₹ 50,000.00", 50000.00)]
    [InlineData("Rs. 25,500.50", 25500.50)]
    [InlineData("(75,000.00)", -75000.00)]
    [InlineData("10000-", -10000.00)]
    [InlineData("0.00", 0.0)]
    [InlineData("-", 0.0)]
    [InlineData("", 0.0)]
    public void ParseAmount_handles_indian_commas_parentheses_trailing_minuses_and_currency_symbols(string raw, decimal expected)
    {
        decimal actual = UniversalTrialBalanceParser.ParseAmount(raw);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Preloaded_sample_datasets_are_mathematically_balanced_and_auto_classify()
    {
        var samples = UniversalTrialBalanceParser.GetPreloadedSamples();
        Assert.Equal(3, samples.Count);

        foreach (var sample in samples)
        {
            Assert.False(string.IsNullOrWhiteSpace(sample.ContentPayload));
            Assert.True(sample.TurnoverInr > 0);

            var preview = UniversalTrialBalanceParser.Preview(sample.ContentPayload, sample.SourceType);

            Assert.True(preview.IsBalanced, $"Sample '{sample.Key}' must balance mathematically.");
            Assert.True(preview.Discrepancy < 1.00m, $"Sample '{sample.Key}' difference must be under ₹1 rounding.");
            Assert.True(preview.TotalLedgersCount >= 20, $"Sample '{sample.Key}' must have full realistic chart of accounts.");

            // Over 80% of accounts in realistic sample must auto-map to Indian Chart of Accounts
            decimal mapRate = (decimal)preview.MappedLedgersCount / preview.TotalLedgersCount;
            Assert.True(mapRate >= 0.80m, $"Sample '{sample.Key}' auto-mapping rate was only {mapRate:P0}.");

            Assert.NotNull(preview.Kpis);
            Assert.True(preview.Kpis.TotalIncome > 0);
            Assert.True(preview.Kpis.TotalAssets > 0);
        }
    }

    [Fact]
    public void Preview_computes_accurate_financial_kpis_and_diagnostics()
    {
        var samples = UniversalTrialBalanceParser.GetPreloadedSamples();
        var tallyMfg = samples.First(s => s.Key == "tally-mfg");

        var preview = UniversalTrialBalanceParser.Preview(tallyMfg.ContentPayload, tallyMfg.SourceType);

        Assert.Equal(tallyMfg.LedgersCount, preview.TotalLedgersCount);
        Assert.True(preview.IsBalanced);
        Assert.NotNull(preview.Kpis);

        // Revenue from Operations should match turnover (~₹46.45 Cr)
        Assert.Equal(tallyMfg.TurnoverInr, preview.Kpis.GrossRevenue);
        Assert.True(preview.Kpis.OperatingProfit > 0);
        Assert.True(preview.Kpis.TotalAssets > 0);
        Assert.True(preview.Diagnostics.Count >= 3);
        Assert.Contains(preview.Diagnostics, d => d.Contains("balanced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Busy_and_sap_group_names_map_accurately_to_schedule_iii()
    {
        // Busy group aliases
        var b1 = Mapped("Commercial Godown", "Fixed Assets", 21000000m);
        Assert.Equal(TrialBalanceConstants.S3PropertyPlantEquipment, b1.Schedule3Group);

        var b2 = Mapped("Shree Trading Supplies (MSME)", "Sundry Creditors", -6500000m);
        Assert.Equal(TrialBalanceConstants.S3TradePayablesMsme, b2.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.F3CdSec43BhMsme, b2.Form3CdCategory);

        var b3 = Mapped("Wholesale Sales A/c", "Sales A/c", -152000000m);
        Assert.Equal(TrialBalanceConstants.S3RevenueFromOperations, b3.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.F3CdTurnover, b3.Form3CdCategory);

        // SAP group aliases
        var s1 = Mapped("IT Server Hardware & Cloud Appliances", "Tangible Assets", 145000000m);
        Assert.Equal(TrialBalanceConstants.S3PropertyPlantEquipment, s1.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.FaComputers, s1.FixedAssetBlock);

        var s2 = Mapped("Software Engineering & Architect Salaries", "Personnel Expenses", 342000000m);
        Assert.Equal(TrialBalanceConstants.S3EmployeeBenefitsExpense, s2.Schedule3Group);

        var s3 = Mapped("Statutory Due - GST Output Liability", "Duties & Taxes", -11000000m);
        Assert.Equal(TrialBalanceConstants.S3OtherCurrentLiabilities, s3.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.F3CdSec43BStatutoryDue, s3.Form3CdCategory);
    }
}
