using System;
using System.Collections.Generic;
using System.Linq;
using CaDesk.Application.BankFeeds;
using CaDesk.Application.Gst;
using CaDesk.Application.Tax;
using CaDesk.Application.TrialBalance;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.TrialBalance;
using Xunit;

namespace CaDesk.UnitTests;

public class DataInUnitTests
{
    // ==========================================
    // 1. TALLY TRIAL BALANCE & MAPPING TESTS
    // ==========================================

    [Fact]
    public void TallyTrialBalanceParser_ParsesXml_Successfully()
    {
        string tallyXml = """
            <ENVELOPE>
                <HEADER><TALLYREQUEST>Export Data</TALLYREQUEST></HEADER>
                <BODY>
                    <DATA>
                        <COLLECTION>
                            <LEDGER NAME="Equity Share Capital">
                                <PARENT>Capital Account</PARENT>
                                <OPENINGBALANCE>-1000000.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>-1000000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="HDFC Bank Current A/c">
                                <PARENT>Bank Accounts</PARENT>
                                <OPENINGBALANCE>450000.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>650000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="Domestic Sales">
                                <PARENT>Sales Accounts</PARENT>
                                <OPENINGBALANCE>0.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>-2500000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="Raw Material Purchases">
                                <PARENT>Purchase Accounts</PARENT>
                                <OPENINGBALANCE>0.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>1800000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="Plant &amp; Machinery">
                                <PARENT>Fixed Assets</PARENT>
                                <OPENINGBALANCE>1050000.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>1050000.00</CLOSINGBALANCE>
                            </LEDGER>
                        </COLLECTION>
                    </DATA>
                </BODY>
            </ENVELOPE>
            """;

        var parsed = TallyTrialBalanceParser.Parse(tallyXml, "TallyXml");

        Assert.Equal(5, parsed.Ledgers.Count);
        Assert.True(parsed.TotalDebit > 0);
        Assert.True(parsed.TotalCredit > 0);
        Assert.Equal(3500000.00m, parsed.TotalDebit);
        Assert.Equal(3500000.00m, parsed.TotalCredit);
        Assert.True(parsed.IsBalanced);

        var bank = parsed.Ledgers.First(l => l.Name.Contains("HDFC"));
        Assert.Equal(650000.00m, bank.DebitAmount);
        Assert.Equal("Bank Accounts", bank.ParentGroup);
    }

    [Fact]
    public void TallyTrialBalanceParser_ParsesCsv_Successfully()
    {
        string tallyCsv = """
            "Particulars","Group","Opening Balance","Debit","Credit","Closing Balance"
            "Share Capital","Capital Account","0.00","0.00","500000.00","500000.00 Cr"
            "State Bank of India","Bank Accounts","0.00","300000.00","0.00","300000.00 Dr"
            "Acme Corp Trade Debtor","Sundry Debtors","0.00","200000.00","0.00","200000.00 Dr"
            """;

        var parsed = TallyTrialBalanceParser.Parse(tallyCsv, "TallyCsv");

        Assert.Equal(3, parsed.Ledgers.Count);
        Assert.Equal(500000.00m, parsed.TotalDebit);
        Assert.Equal(500000.00m, parsed.TotalCredit);
        Assert.True(parsed.IsBalanced);
    }

    [Fact]
    public void TrialBalanceMappingEngine_AppliesClientRulesFirst_ThenIndianCoaHeuristics()
    {
        var firmId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var clientRules = new List<ClientLedgerMapping>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FirmId = firmId,
                ClientId = clientId,
                LedgerPattern = "Custom Hardware Expense",
                TallyGroup = "Indirect Expenses",
                Schedule3Group = TrialBalanceConstants.S3OtherExpenses,
                Form3CdCategory = TrialBalanceConstants.F3CdSec40A3Disallowance,
                CreatedAt = DateTime.UtcNow
            }
        };

        var ledgers = new List<TrialBalanceLedger>
        {
            new()
            {
                Id = Guid.NewGuid(),
                LedgerName = "Custom Hardware Expense",
                TallyGroup = "Indirect Expenses",
                ClosingBalance = 45000m
            },
            new()
            {
                Id = Guid.NewGuid(),
                LedgerName = "Infosys Sundry Debtor",
                TallyGroup = "Sundry Debtors",
                ClosingBalance = 150000m
            },
            new()
            {
                Id = Guid.NewGuid(),
                LedgerName = "Outstanding GST Payable",
                TallyGroup = "Duties & Taxes",
                ClosingBalance = -32000m
            },
            new()
            {
                Id = Guid.NewGuid(),
                LedgerName = "Computers & Laptops",
                TallyGroup = "Fixed Assets",
                ClosingBalance = 85000m
            }
        };

        TrialBalanceMappingEngine.ApplyMappings(ledgers, clientRules);

        // Custom rule matched
        var custom = ledgers.First(l => l.LedgerName == "Custom Hardware Expense");
        Assert.True(custom.IsMapped);
        Assert.Equal(TrialBalanceConstants.S3OtherExpenses, custom.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.F3CdSec40A3Disallowance, custom.Form3CdCategory);

        // Indian COA heuristics matched
        var debtor = ledgers.First(l => l.LedgerName == "Infosys Sundry Debtor");
        Assert.True(debtor.IsMapped);
        Assert.Equal(TrialBalanceConstants.S3TradeReceivables, debtor.Schedule3Group);

        var gst = ledgers.First(l => l.LedgerName == "Outstanding GST Payable");
        Assert.True(gst.IsMapped);
        Assert.Equal(TrialBalanceConstants.S3OtherCurrentLiabilities, gst.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.F3CdSec43BStatutoryDue, gst.Form3CdCategory);

        var computers = ledgers.First(l => l.LedgerName == "Computers & Laptops");
        Assert.True(computers.IsMapped);
        Assert.Equal(TrialBalanceConstants.S3PropertyPlantEquipment, computers.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.FaComputers, computers.FixedAssetBlock);
    }

    // ==========================================
    // 2. AIS / 26AS / TIS PARSER TESTS
    // ==========================================

    [Fact]
    public void AnnualInformationStatementParser_ParsesFullAisJson()
    {
        string aisJson = """
            {
                "pan": "ABCDE1234F",
                "financialYear": "2024-25",
                "assessmentYear": "2025-26",
                "salary": [
                    { "employerName": "Tata Consultancy Services", "tan": "MUMB12345A", "grossSalary": 1450000, "tdsDeducted": 125000 }
                ],
                "savingsInterest": [
                    { "bankName": "HDFC Bank", "accountNumber": "50100123456", "amount": 18500, "tdsDeducted": 0 }
                ],
                "fixedDepositInterest": [
                    { "bankName": "State Bank of India", "amount": 42000, "tdsDeducted": 4200 }
                ],
                "dividend": [
                    { "companyName": "Reliance Industries Ltd", "amount": 15000, "tdsDeducted": 1500 }
                ],
                "securities": [
                    { "scripName": "HDFC Bank Ltd", "saleValue": 250000, "purchaseValue": 180000, "isLongTerm": true }
                ],
                "tdsDetails": [
                    { "deductorName": "Tata Consultancy Services", "section": "192", "amountPaid": 1450000, "tdsClaimed": 125000 },
                    { "deductorName": "State Bank of India", "section": "194A", "amountPaid": 42000, "tdsClaimed": 4200 },
                    { "deductorName": "Reliance Industries Ltd", "section": "194", "amountPaid": 15000, "tdsClaimed": 1500 }
                ],
                "taxPayments": [
                    { "minorHead": "Advance Tax (100)", "bsrCode": "0002145", "challanNumber": "04912", "tenderDate": "2024-09-12", "amount": 35000 }
                ]
            }
            """;

        var parsed = AnnualInformationStatementParser.Parse(aisJson);

        Assert.Equal("ABCDE1234F", parsed.Pan);
        Assert.Equal("2024-25", parsed.FinancialYear);
        Assert.Equal(1450000m, parsed.GrossSalary);
        Assert.Equal(18500m, parsed.SavingsInterest);
        Assert.Equal(42000m, parsed.FixedDepositInterest);
        Assert.Equal(15000m, parsed.DividendIncome);
        Assert.Equal(70000m, parsed.CapitalGainsLtcg);
        Assert.Equal(130700m, parsed.TotalTds);
        Assert.Single(parsed.TaxPayments);
        Assert.Equal(35000m, parsed.TaxPayments[0].Amount);
    }

    // ==========================================
    // 3. GST JSON PARSER TESTS
    // ==========================================

    [Fact]
    public void GstJsonParser_ParsesGstr2BJson_WithB2BAndCdnr()
    {
        string gstr2bJson = """
            {
                "gstin": "27AAACB1234F1Z9",
                "fp": "042024",
                "b2b": [
                    {
                        "ctin": "27AABCT2345M1Z2",
                        "cname": "Tech Supplies Pvt Ltd",
                        "inv": [
                            {
                                "inum": "INV-2024-001",
                                "idt": "15-04-2024",
                                "val": 118000.00,
                                "pos": "27",
                                "itcavl": "Y",
                                "items": [
                                    { "txval": 100000.00, "cgst": 9000.00, "sgst": 9000.00, "igst": 0.00, "cess": 0.00 }
                                ]
                            }
                        ]
                    }
                ],
                "cdnr": [
                    {
                        "ctin": "27AABCT2345M1Z2",
                        "nt": [
                            {
                                "nt_num": "CRN-001",
                                "nt_dt": "25-04-2024",
                                "ntty": "C",
                                "val": 11800.00,
                                "items": [
                                    { "txval": 10000.00, "cgst": 900.00, "sgst": 900.00, "igst": 0.00, "cess": 0.00 }
                                ]
                            }
                        ]
                    }
                ]
            }
            """;

        var result = GstJsonParser.ParseGstr2B(gstr2bJson);

        Assert.Equal("27AAACB1234F1Z9", result.Gstin);
        Assert.Equal("042024", result.ReturnPeriod);
        Assert.Equal(2, result.Invoices.Count);

        // B2B 100000 - CDNR 10000 = 90000 net taxable
        Assert.Equal(90000.00m, result.TotalTaxable);
        Assert.Equal(8100.00m, result.TotalCgst);
        Assert.Equal(8100.00m, result.TotalSgst);
        Assert.Equal(16200.00m, result.TotalItcAvailable);
    }

    [Fact]
    public void GstJsonParser_ParsesGstr1Json_TurnoverAndTaxes()
    {
        string gstr1Json = """
            {
                "gstin": "27AAACB1234F1Z9",
                "fp": "2024-25",
                "b2b": [
                    {
                        "inv": [
                            {
                                "items": [
                                    { "txval": 500000.00, "igst": 90000.00, "cgst": 0.00, "sgst": 0.00, "cess": 0.00 }
                                ]
                            }
                        ]
                    }
                ],
                "b2cs": [
                    { "txval": 150000.00, "cgst": 13500.00, "sgst": 13500.00, "igst": 0.00, "cess": 0.00 }
                ],
                "nil": {
                    "inv": [
                        { "expt_amt": 50000.00, "nil_amt": 0.00, "ngsup_amt": 0.00 }
                    ]
                }
            }
            """;

        var result = GstJsonParser.ParseGstr1(gstr1Json);

        Assert.Equal(650000.00m, result.OutwardTaxableSupplies);
        Assert.Equal(50000.00m, result.OutwardExemptedSupplies);
        Assert.Equal(700000.00m, result.TotalTurnover);
        Assert.Equal(90000.00m, result.TotalIgst);
        Assert.Equal(13500.00m, result.TotalCgst);
        Assert.Equal(13500.00m, result.TotalSgst);
    }

    // ==========================================
    // 4. PDF BANK STATEMENT & OFX PARSER TESTS
    // ==========================================

    [Fact]
    public void PdfBankStatementParser_ParsesExtractedStatementLines()
    {
        string statementText = """
            STATE BANK OF INDIA - STATEMENT OF ACCOUNT
            Account Number: 30129481920
            Branch: Nariman Point, Mumbai
            Date            Narration                           Chq/Ref No    Debit        Credit        Balance
            01/04/2024      OPENING BALANCE                                                              150000.00
            05/04/2024      NEFT-AXIS001-CLIENT PAYMENT         UTR9821421                 75000.00     225000.00
            10/04/2024      CHQ PAID TO VENDOR                  CHQ-501293    25000.00                   200000.00
            15/04/2024      UPI/31429182/SWIGGY FOOD ORDER      UPI-314291    1250.00                    198750.00
            30/04/2024      INTEREST CREDITED                                              1250.00      200000.00
            """;

        var result = PdfBankStatementParser.Parse(statementText);

        Assert.Equal(4, result.Items.Count);
        Assert.Equal(150000.00m, result.OpeningBalance);
        Assert.Equal(200000.00m, result.ClosingBalance);
        Assert.Equal(26250.00m, result.TotalDebits);
        Assert.Equal(76250.00m, result.TotalCredits);

        var neft = result.Items.First(i => i.Narration.Contains("NEFT"));
        Assert.Equal(BankTransactionType.Credit, neft.Type);
        Assert.Equal(75000.00m, neft.Amount);

        var chq = result.Items.First(i => i.Narration.Contains("VENDOR"));
        Assert.Equal(BankTransactionType.Debit, chq.Type);
        Assert.Equal(25000.00m, chq.Amount);
    }

    [Fact]
    public void BankStatementParser_ParsesOfxFormat()
    {
        string ofx = """
            OFXHEADER:100
            DATA:OFXSGML
            <OFX>
                <BANKMSGSRSV1>
                    <STMTTRNRS>
                        <STMTRS>
                            <BANKTRANLIST>
                                <STMTTRN>
                                    <TRNTYPE>CREDIT</TRNTYPE>
                                    <DTPOSTED>20240410</DTPOSTED>
                                    <TRNAMT>45000.00</TRNAMT>
                                    <FITID>TXN001</FITID>
                                    <NAME>CLIENT INVOICE PAYMENT</NAME>
                                </STMTTRN>
                                <STMTTRN>
                                    <TRNTYPE>DEBIT</TRNTYPE>
                                    <DTPOSTED>20240415</DTPOSTED>
                                    <TRNAMT>-15000.00</TRNAMT>
                                    <FITID>TXN002</FITID>
                                    <NAME>OFFICE RENT</NAME>
                                </STMTTRN>
                            </BANKTRANLIST>
                            <LEDGERBAL>
                                <BALAMT>130000.00</BALAMT>
                            </LEDGERBAL>
                        </STMTRS>
                    </STMTTRNRS>
                </BANKMSGSRSV1>
            </OFX>
            """;

        var result = BankStatementParser.ParseOfx(ofx);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(15000.00m, result.TotalDebits);
        Assert.Equal(45000.00m, result.TotalCredits);
        Assert.Equal(130000.00m, result.ClosingBalance);
    }
}
