using System.Text.RegularExpressions;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.LedgerSync;
using Xunit;

namespace CaDesk.UnitTests;

public class LedgerSyncUnitTests
{
    [Fact]
    public void TallyXmlProtocol_GenerateExportVouchersXml_ContainsValidEnvelopeAndDates()
    {
        string company = "Acme Corp Ltd";
        var fromDate = new DateOnly(2026, 4, 1);
        var toDate = new DateOnly(2026, 9, 30);

        string xml = TallyXmlProtocol.GenerateExportVouchersXml(company, fromDate, toDate);

        Assert.Contains("<TALLYREQUEST>Export Data</TALLYREQUEST>", xml);
        Assert.Contains("<REPORTNAME>Voucher Register</REPORTNAME>", xml);
        Assert.Contains("<SVCURRENTCOMPANY>Acme Corp Ltd</SVCURRENTCOMPANY>", xml);
        Assert.Contains("<SVFROMDATE>20260401</SVFROMDATE>", xml);
        Assert.Contains("<SVTODATE>20260930</SVTODATE>", xml);
    }

    [Fact]
    public void TallyXmlProtocol_GenerateImportJournalVoucherXml_CreatesCorrectDeemedPositiveNegativeStructure()
    {
        string xml = TallyXmlProtocol.GenerateImportJournalVoucherXml(
            companyName: "Acme Corp Ltd",
            voucherType: "Journal",
            date: new DateOnly(2026, 9, 30),
            reference: "AUD-ADJ-2026-001",
            amount: 75000.50m,
            debitLedger: "Depreciation on Plant & Machinery",
            creditLedger: "Accumulated Depreciation",
            narration: "Statutory depreciation provision under Sec 32 IT Act"
        );

        Assert.Contains("<TALLYREQUEST>Import Data</TALLYREQUEST>", xml);
        Assert.Contains("<VOUCHER VCHTYPE=\"Journal\" ACTION=\"Create\">", xml);
        Assert.Contains("<DATE>20260930</DATE>", xml);
        Assert.Contains("<REFERENCE>AUD-ADJ-2026-001</REFERENCE>", xml);
        Assert.Contains("<LEDGERNAME>Depreciation on Plant &amp; Machinery</LEDGERNAME>", xml.Replace("&", "&amp;"));
        // Debit leg has ISDEEMEDPOSITIVE Yes and negative amount
        Assert.Contains("<ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE>", xml);
        Assert.Contains("<AMOUNT>-75000.50</AMOUNT>", xml);
        // Credit leg has ISDEEMEDPOSITIVE No and positive amount
        Assert.Contains("<ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE>", xml);
        Assert.Contains("<AMOUNT>75000.50</AMOUNT>", xml);
    }

    [Fact]
    public void TallyXmlProtocol_ParseImportResponse_Success_ExtractsVoucherNumber()
    {
        string responseXml = """
        <RESPONSE>
            <CREATED>1</CREATED>
            <ALTERED>0</ALTERED>
            <DELETED>0</DELETED>
            <LASTVCHID>VCH-2026-9901</LASTVCHID>
            <ERRORS>0</ERRORS>
        </RESPONSE>
        """;

        var (success, voucherNumber, error) = TallyXmlProtocol.ParseImportResponse(responseXml);

        Assert.True(success);
        Assert.Equal("VCH-2026-9901", voucherNumber);
        Assert.Null(error);
    }

    [Fact]
    public void TallyXmlProtocol_ParseImportResponse_WithError_ReturnsErrorMessage()
    {
        string responseXml = """
        <RESPONSE>
            <CREATED>0</CREATED>
            <ERRORS>1</ERRORS>
            <LINEERROR>Ledger 'Depreciation on Plant &amp; Machinery' does not exist</LINEERROR>
        </RESPONSE>
        """;

        var (success, voucherNumber, error) = TallyXmlProtocol.ParseImportResponse(responseXml);

        Assert.False(success);
        Assert.Null(voucherNumber);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void AiBankLedgerReconciliation_ExactAmountAndDate_MatchesWithHighConfidence()
    {
        var bankTx = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 15),
            Amount = 150000.00m,
            Narration = "NEFT CR-M/S ABC TRADERS-CLIENT REC",
            TransactionType = "Credit"
        };

        var vouchers = new List<ErpLedgerVoucher>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "RCPT/26-27/045",
                VoucherDate = new DateOnly(2026, 10, 16),
                Amount = 150000.00m,
                DebitLedger = "HDFC Bank Current A/c",
                CreditLedger = "ABC Traders",
                Narration = "Receipt from ABC Traders against Inv 104"
            },
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "RCPT/26-27/046",
                VoucherDate = new DateOnly(2026, 10, 20),
                Amount = 50000.00m,
                DebitLedger = "HDFC Bank Current A/c",
                CreditLedger = "XYZ Corp"
            }
        };

        var match = AiBankLedgerReconciliationEngine.FindBestMatch(bankTx, vouchers);

        Assert.NotNull(match);
        Assert.Equal(ReconciliationMatchRule.ExactAmountAndDate, match.Rule);
        Assert.True(match.ConfidenceScore >= 95.0m);
        Assert.Equal(0m, match.Variance);
        Assert.Equal("RCPT/26-27/045", match.Voucher.VoucherNumber);
    }

    [Fact]
    public void AiBankLedgerReconciliation_ChequeNumberInNarration_MatchesAccurately()
    {
        var bankTx = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 12),
            Amount = 45000.00m,
            Narration = "CHQ CLG / 405912 / SUPPLIER CLEARANCE",
            TransactionType = "Debit"
        };

        var vouchers = new List<ErpLedgerVoucher>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "PMT/26-27/101",
                VoucherDate = new DateOnly(2026, 10, 10),
                Amount = 45000.00m,
                ChequeNumber = "405912",
                DebitLedger = "Steel Supplies India",
                CreditLedger = "HDFC Bank Current A/c",
                Narration = "Cheque payment for raw materials"
            }
        };

        var match = AiBankLedgerReconciliationEngine.FindBestMatch(bankTx, vouchers);

        Assert.NotNull(match);
        Assert.Equal(ReconciliationMatchRule.ChequeOrUtrMatch, match.Rule);
        Assert.True(match.ConfidenceScore >= 95.0m);
        Assert.Contains("405912", match.Explanation);
    }

    [Fact]
    public void AiBankLedgerReconciliation_TdsNetDeduction_2PercentCorporate_Matches()
    {
        // Invoice / Voucher is gross ₹1,00,000; bank receipt net of 2% TDS (Sec 194C) is ₹98,000
        var bankTx = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 18),
            Amount = 98000.00m,
            Narration = "RTGS-INFOSYS TECH LTD-INVOICE CLEARANCE",
            TransactionType = "Credit"
        };

        var vouchers = new List<ErpLedgerVoucher>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "SALES/26-27/889",
                VoucherDate = new DateOnly(2026, 10, 14),
                Amount = 100000.00m,
                DebitLedger = "Infosys Tech Ltd",
                CreditLedger = "Software Consulting Income",
                Narration = "Monthly consulting services"
            }
        };

        var match = AiBankLedgerReconciliationEngine.FindBestMatch(bankTx, vouchers);

        Assert.NotNull(match);
        Assert.Equal(ReconciliationMatchRule.TdsNetDeductionMatch, match.Rule);
        Assert.True(match.ConfidenceScore >= 90.0m);
        Assert.Contains("2% TDS", match.Explanation);
    }

    [Fact]
    public void AiBankLedgerReconciliation_TdsNetDeduction_10PercentProfessionalFee_Matches()
    {
        // Invoice / Voucher is gross ₹50,000; bank receipt net of 10% TDS (Sec 194J) is ₹45,000
        var bankTx = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 22),
            Amount = 45000.00m,
            Narration = "NEFT-TECH LABS-LEGAL RETAINER",
            TransactionType = "Credit"
        };

        var vouchers = new List<ErpLedgerVoucher>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "SALES/26-27/912",
                VoucherDate = new DateOnly(2026, 10, 20),
                Amount = 50000.00m,
                DebitLedger = "Tech Labs",
                CreditLedger = "Professional Fees"
            }
        };

        var match = AiBankLedgerReconciliationEngine.FindBestMatch(bankTx, vouchers);

        Assert.NotNull(match);
        Assert.Equal(ReconciliationMatchRule.TdsNetDeductionMatch, match.Rule);
        Assert.Contains("10% TDS", match.Explanation);
    }

    [Fact]
    public void AiBankLedgerReconciliation_ToleranceRounding_Within10Rupees_Matches()
    {
        // Bank debit has small rounding / FX difference: ₹12,504.50 vs voucher ₹12,500.00 (+₹4.50)
        var bankTx = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 25),
            Amount = 12504.50m,
            Narration = "POS-AWS CLOUD HOSTING-OCT",
            TransactionType = "Debit"
        };

        var vouchers = new List<ErpLedgerVoucher>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "PUR/26-27/451",
                VoucherDate = new DateOnly(2026, 10, 25),
                Amount = 12500.00m,
                DebitLedger = "Cloud Infrastructure Expenses",
                CreditLedger = "HDFC Bank Current A/c"
            }
        };

        var match = AiBankLedgerReconciliationEngine.FindBestMatch(bankTx, vouchers);

        Assert.NotNull(match);
        Assert.Equal(ReconciliationMatchRule.ToleranceRoundingMatch, match.Rule);
        Assert.Equal(4.50m, match.Variance);
        Assert.True(match.ConfidenceScore >= 80.0m);
    }

    [Fact]
    public void AiBankLedgerReconciliation_MultiInvoiceBundle_MatchesCombinedSum()
    {
        // Single bank receipt of ₹1,00,000 bundles two open sales invoices (₹35,000 + ₹65,000)
        var bankTx = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 28),
            Amount = 100000.00m,
            Narration = "RTGS-APEX MOTORS-MULTIPLE INV PAYMENT",
            TransactionType = "Credit"
        };

        var vouchers = new List<ErpLedgerVoucher>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "INV/26-27/101",
                Amount = 35000.00m,
                DebitLedger = "Apex Motors Ltd",
                CreditLedger = "Sales"
            },
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "INV/26-27/102",
                Amount = 65000.00m,
                DebitLedger = "Apex Motors Ltd",
                CreditLedger = "Sales"
            }
        };

        var match = AiBankLedgerReconciliationEngine.FindBestMatch(bankTx, vouchers);

        Assert.NotNull(match);
        Assert.Equal(ReconciliationMatchRule.MultiInvoiceBundleMatch, match.Rule);
        Assert.Contains("INV/26-27/101", match.Explanation);
        Assert.Contains("INV/26-27/102", match.Explanation);
        Assert.True(match.ConfidenceScore >= 85.0m);
    }

    [Fact]
    public void AiBankLedgerReconciliation_UnrelatedTransaction_ReturnsNull()
    {
        var bankTx = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = new DateOnly(2026, 10, 30),
            Amount = 887250.00m,
            Narration = "RANDOM UNKNOWN TRANSACTION",
            TransactionType = "Debit"
        };

        var vouchers = new List<ErpLedgerVoucher>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VoucherNumber = "VCH/001",
                Amount = 1000.00m
            }
        };

        var match = AiBankLedgerReconciliationEngine.FindBestMatch(bankTx, vouchers);

        Assert.Null(match);
    }
}
