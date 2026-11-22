using CaDesk.Application.TrialBalance;
using CaDesk.Domain.TrialBalance;
using Xunit;

namespace CaDesk.UnitTests;

/// <summary>Parser sign convention, mapping rules and the figures the trial balance sync derives.</summary>
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
}
