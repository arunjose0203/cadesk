using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.AdvanceTax;
using CaDesk.Application.BankFeeds;
using CaDesk.Application.Clients;
using CaDesk.Application.Gst;
using CaDesk.Application.Gstr9;
using CaDesk.Application.Tax;
using CaDesk.Application.TrialBalance;
using CaDesk.Domain.AdvanceTax;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.TrialBalance;
using Xunit;

namespace CaDesk.IntegrationTests;

public class DataInIntegrationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "DataIn Audit Firm")
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
    public async Task TrialBalance_Upload_Map_Sync_And_NextYearRetention_Succeeds()
    {
        var (http, _) = await NewFirmAsync("DataIn Partner Firm");
        var client = await CreateClientAsync(http, "Bharat Manufacturing Ltd");

        // 1. Upload FY 2023-24 Tally XML Trial Balance
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
                            <LEDGER NAME="HDFC Bank Current Account">
                                <PARENT>Bank Accounts</PARENT>
                                <OPENINGBALANCE>500000.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>850000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="Domestic Product Sales">
                                <PARENT>Sales Accounts</PARENT>
                                <OPENINGBALANCE>0.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>-5000000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="Raw Material Purchases">
                                <PARENT>Purchase Accounts</PARENT>
                                <OPENINGBALANCE>0.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>3200000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="Custom Software License Fees">
                                <PARENT>Indirect Expenses</PARENT>
                                <OPENINGBALANCE>0.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>250000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="Computers and Laptops">
                                <PARENT>Fixed Assets</PARENT>
                                <OPENINGBALANCE>1700000.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>1700000.00</CLOSINGBALANCE>
                            </LEDGER>
                        </COLLECTION>
                    </DATA>
                </BODY>
            </ENVELOPE>
            """;

        var uploadReq = new UploadTrialBalanceRequest(
            ClientId: client.Id,
            FinancialYear: "2023-24",
            SourceType: "TallyXml",
            FileName: "Tally_TB_2023_24.xml",
            ContentPayload: tallyXml
        );

        var uploadRes = await http.PostAsJsonAsync("/api/trial-balance/upload", uploadReq);
        if (uploadRes.StatusCode != HttpStatusCode.Created)
        {
            var err = await uploadRes.Content.ReadAsStringAsync();
            throw new Exception($"UPLOAD FAILED: {uploadRes.StatusCode} - {err}");
        }
        var import = (await uploadRes.Content.ReadFromJsonAsync<TrialBalanceImportDto>())!;

        Assert.Equal("2023-24", import.FinancialYear);
        Assert.Equal(6, import.TotalLedgersCount);
        Assert.True(import.IsBalanced);

        // 2. Map the custom software license ledger and check SaveAsClientDefaultRule
        var softwareLedger = import.Ledgers.First(l => l.LedgerName == "Custom Software License Fees");
        var updateMapReq = new UpdateLedgerMappingRequest(
            Schedule3Group: TrialBalanceConstants.S3OtherExpenses,
            Form3CdCategory: TrialBalanceConstants.F3CdSec40A3Disallowance,
            CaroCategory: null,
            FixedAssetBlock: null,
            SaveAsClientDefaultRule: true
        );

        var mapRes = await http.PutAsJsonAsync($"/api/trial-balance/imports/{import.Id}/ledgers/{softwareLedger.Id}", updateMapReq);
        Assert.Equal(HttpStatusCode.OK, mapRes.StatusCode);
        var updatedImport = (await mapRes.Content.ReadFromJsonAsync<TrialBalanceImportDto>())!;
        var updatedLedger = updatedImport.Ledgers.First(l => l.Id == softwareLedger.Id);
        Assert.True(updatedLedger.IsMapped);
        Assert.Equal(TrialBalanceConstants.S3OtherExpenses, updatedLedger.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.F3CdSec40A3Disallowance, updatedLedger.Form3CdCategory);

        // Verify client default rules endpoint
        var rulesRes = await http.GetAsync($"/api/trial-balance/clients/{client.Id}/rules");
        Assert.Equal(HttpStatusCode.OK, rulesRes.StatusCode);
        var rules = (await rulesRes.Content.ReadFromJsonAsync<List<ClientLedgerMappingDto>>())!;
        Assert.Contains(rules, r => r.LedgerPattern == "Custom Software License Fees");

        // 3. Multi-Desk Sync: Push TB to Audit, Schedule III, Form 3CD, CARO, Fixed Assets
        var syncRes = await http.PostAsJsonAsync($"/api/trial-balance/imports/{import.Id}/sync", new SyncTrialBalanceRequest());
        Assert.Equal(HttpStatusCode.OK, syncRes.StatusCode);
        var syncResult = (await syncRes.Content.ReadFromJsonAsync<DeskSyncResultDto>())!;
        // materiality is the auditor's judgement (SA 320): the audit desk is only ever given a suggestion
        Assert.False(syncResult.AuditSynced);
        Assert.Contains("No audit engagement", syncResult.AuditNotes);
        Assert.True(syncResult.Schedule3Synced);
        Assert.Contains("debt service", syncResult.Schedule3Notes); // not invented, left for the user
        Assert.True(syncResult.Form3CdSynced);
        Assert.True(syncResult.CaroSynced);
        Assert.True(syncResult.FixedAssetsSynced);

        // 4. Test mapped once per client retention: Upload FY 2024-25 TB containing the same ledger
        string tallyNextYear = """
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
                            <LEDGER NAME="Custom Software License Fees">
                                <PARENT>Indirect Expenses</PARENT>
                                <OPENINGBALANCE>0.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>350000.00</CLOSINGBALANCE>
                            </LEDGER>
                            <LEDGER NAME="HDFC Bank Current Account">
                                <PARENT>Bank Accounts</PARENT>
                                <OPENINGBALANCE>850000.00</OPENINGBALANCE>
                                <CLOSINGBALANCE>650000.00</CLOSINGBALANCE>
                            </LEDGER>
                        </COLLECTION>
                    </DATA>
                </BODY>
            </ENVELOPE>
            """;

        var uploadNextReq = new UploadTrialBalanceRequest(
            ClientId: client.Id,
            FinancialYear: "2024-25",
            SourceType: "TallyXml",
            FileName: "Tally_TB_2024_25.xml",
            ContentPayload: tallyNextYear
        );

        var uploadNextRes = await http.PostAsJsonAsync("/api/trial-balance/upload", uploadNextReq);
        Assert.Equal(HttpStatusCode.Created, uploadNextRes.StatusCode);
        var nextImport = (await uploadNextRes.Content.ReadFromJsonAsync<TrialBalanceImportDto>())!;

        // The custom ledger in next year must be auto-mapped from client retention rules!
        var nextCustom = nextImport.Ledgers.First(l => l.LedgerName == "Custom Software License Fees");
        Assert.True(nextCustom.IsMapped);
        Assert.Equal(TrialBalanceConstants.S3OtherExpenses, nextCustom.Schedule3Group);
        Assert.Equal(TrialBalanceConstants.F3CdSec40A3Disallowance, nextCustom.Form3CdCategory);
    }

    [Fact]
    public async Task Sync_keeps_existing_desk_data_and_refuses_unmapped_or_unbalanced_trial_balances()
    {
        var (http, _) = await NewFirmAsync("DataIn Safety Firm");
        var client = await CreateClientAsync(http, "Kaveri Textiles Pvt Ltd");

        // the auditor has already filled in the Form 3CD assessment by hand
        var manual = await http.PostAsJsonAsync("/api/taxaudit/assessments", new CaDesk.Application.TaxAudit.SaveForm3CdAssessmentRequest
        {
            ClientId = client.Id, FinancialYear = "2025-26", AssessmentYear = "2026-27",
            AssesseeName = "Kaveri Textiles Private Limited", Pan = "AAACB1234F", Address = "12 Mill Road, Coimbatore",
            Status = "Company", AuditorObservations = "Stock register reviewed on 10 May.",
            TotalTurnoverOrGrossReceipts = 1m,
        });
        Assert.True(manual.IsSuccessStatusCode, await manual.Content.ReadAsStringAsync());
        var before = (await manual.Content.ReadFromJsonAsync<CaDesk.Application.TaxAudit.Form3CdAssessmentDto>())!;

        // Tally's own convention (debit balances negative), one ledger under a user-made group, out of balance
        string Xml(string extraClosing) => $"""
            <ENVELOPE><BODY><DATA><COLLECTION>
              <LEDGER NAME="Share Capital"><PARENT>Capital Account</PARENT><CLOSINGBALANCE>1000000.00</CLOSINGBALANCE></LEDGER>
              <LEDGER NAME="Ramesh Kumar A/c"><PARENT>Promoter Funding</PARENT><CLOSINGBALANCE>{extraClosing}</CLOSINGBALANCE></LEDGER>
              <LEDGER NAME="Cash"><PARENT>Cash-in-Hand</PARENT><CLOSINGBALANCE>-50000.00</CLOSINGBALANCE></LEDGER>
              <LEDGER NAME="SBI Current Account"><PARENT>Bank Accounts</PARENT><CLOSINGBALANCE>-2450000.00</CLOSINGBALANCE></LEDGER>
              <LEDGER NAME="Bank Charges"><PARENT>Indirect Expenses</PARENT><CLOSINGBALANCE>-10000.00</CLOSINGBALANCE></LEDGER>
              <LEDGER NAME="Fabric Sales"><PARENT>Sales Accounts</PARENT><CLOSINGBALANCE>4010000.00</CLOSINGBALANCE></LEDGER>
              <LEDGER NAME="Yarn Purchases"><PARENT>Purchase Accounts</PARENT><CLOSINGBALANCE>-3000000.00</CLOSINGBALANCE></LEDGER>
            </COLLECTION></DATA></BODY></ENVELOPE>
            """;

        async Task<TrialBalanceImportDto> Upload(string extraClosing)
        {
            var res = await http.PostAsJsonAsync("/api/trial-balance/upload",
                new UploadTrialBalanceRequest(client.Id, "2025-26", "TallyXml", "tb.xml", Xml(extraClosing)));
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            return (await res.Content.ReadFromJsonAsync<TrialBalanceImportDto>())!;
        }

        // out of balance: refused
        var unbalanced = await Upload("400000.00");
        Assert.False(unbalanced.IsBalanced);
        Assert.Equal(HttpStatusCode.Conflict,
            (await http.PostAsJsonAsync($"/api/trial-balance/imports/{unbalanced.Id}/sync", new SyncTrialBalanceRequest())).StatusCode);

        var tb = await Upload("500000.00");
        Assert.True(tb.IsBalanced);
        // Tally's negative-debit convention was recognised: cash is a debit (positive) balance
        Assert.Equal(50000m, tb.Ledgers.Single(l => l.LedgerName == "Cash").ClosingBalance);
        Assert.Equal(TrialBalanceConstants.S3FinanceCosts, tb.Ledgers.Single(l => l.LedgerName == "Bank Charges").Schedule3Group);
        // "Promoter Funding" is the firm's own group: left for review instead of guessed as an expense
        var loan = tb.Ledgers.Single(l => l.LedgerName == "Ramesh Kumar A/c");
        Assert.False(loan.IsMapped);
        var refused = await http.PostAsJsonAsync($"/api/trial-balance/imports/{tb.Id}/sync", new SyncTrialBalanceRequest());
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Ramesh Kumar A/c", await refused.Content.ReadAsStringAsync());

        // a mapping to a group that does not exist is rejected
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync($"/api/trial-balance/imports/{tb.Id}/ledgers/{loan.Id}",
            new UpdateLedgerMappingRequest("Loans", null, null, null, false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PutAsJsonAsync($"/api/trial-balance/imports/{tb.Id}/ledgers/{loan.Id}",
            new UpdateLedgerMappingRequest(TrialBalanceConstants.S3LongTermBorrowings, null, TrialBalanceConstants.CaroTermLoan, null, false))).StatusCode);

        var sync = await http.PostAsJsonAsync($"/api/trial-balance/imports/{tb.Id}/sync",
            new SyncTrialBalanceRequest { SyncAudit = false, SyncSchedule3 = false, SyncCaro = false, SyncFixedAssets = false });
        Assert.True(sync.IsSuccessStatusCode, await sync.Content.ReadAsStringAsync());
        var result = (await sync.Content.ReadFromJsonAsync<DeskSyncResultDto>())!;
        Assert.True(result.Form3CdSynced, result.Form3CdNotes);

        // the trial balance updated turnover and nothing else the auditor had entered
        var after = (await http.GetFromJsonAsync<CaDesk.Application.TaxAudit.Form3CdAssessmentDto>($"/api/taxaudit/assessments/{before.Id}"))!;
        Assert.Equal(4010000m, after.TotalTurnoverOrGrossReceipts);
        Assert.Equal("Kaveri Textiles Private Limited", after.AssesseeName);
        Assert.Equal("12 Mill Road, Coimbatore", after.Address);
        Assert.Equal("Stock register reviewed on 10 May.", after.AuditorObservations);
    }

    [Fact]
    public async Task Ais_Import_To_TaxComputation_And_AdvanceTax_Succeeds()
    {
        var (http, _) = await NewFirmAsync("Tax DataIn Firm");
        var client = await CreateClientAsync(http, "Rajesh Sharma");

        string aisJson = """
            {
                "pan": "AAACB1234F",
                "financialYear": "2024-25",
                "assessmentYear": "2025-26",
                "salary": [
                    { "employerName": "Global Tech Corp", "tan": "MUMB98765A", "grossSalary": 2400000, "tdsDeducted": 320000 }
                ],
                "savingsInterest": [
                    { "bankName": "ICICI Bank", "accountNumber": "00110192837", "amount": 25000, "tdsDeducted": 0 }
                ],
                "fixedDepositInterest": [
                    { "bankName": "HDFC Bank", "amount": 50000, "tdsDeducted": 5000 }
                ],
                "dividend": [
                    { "companyName": "TCS Ltd", "amount": 20000, "tdsDeducted": 2000 }
                ],
                "tdsDetails": [
                    { "deductorName": "Global Tech Corp", "section": "192", "amountPaid": 2400000, "tdsClaimed": 320000 },
                    { "deductorName": "HDFC Bank", "section": "194A", "amountPaid": 50000, "tdsClaimed": 5000 },
                    { "deductorName": "TCS Ltd", "section": "194", "amountPaid": 20000, "tdsClaimed": 2000 }
                ],
                "taxPayments": [
                    { "minorHead": "Advance Tax (100)", "bsrCode": "0210045", "challanNumber": "88192", "tenderDate": "2024-06-14", "amount": 50000 }
                ]
            }
            """;

        // 1. Import AIS directly into Tax Computation
        var compRes = await http.PostAsJsonAsync("/api/tax/computations/import-ais", new ImportAisToComputationRequest(client.Id, aisJson));
        if (compRes.StatusCode != HttpStatusCode.Created)
        {
            var err = await compRes.Content.ReadAsStringAsync();
            throw new Exception($"IMPORT AIS FAILED: {compRes.StatusCode} - {err}");
        }
        var comp = (await compRes.Content.ReadFromJsonAsync<TaxComputationDto>())!;

        Assert.Equal("2025-26", comp.AssessmentYear);
        Assert.Equal(2400000m, comp.GrossSalary);
        Assert.Equal(95000m, comp.OtherSourcesIncome); // 25k savings + 50k FD + 20k dividend
        Assert.True(comp.Comparison.NewRegime.TotalTaxLiability > 0);
        Assert.True(comp.Comparison.OldRegime.TotalTaxLiability > 0);
        Assert.NotNull(comp.Comparison.RecommendedRegime);

        // 2. Create Advance Tax Plan and import AIS into it
        var planRes = await http.PostAsJsonAsync("/api/advance-tax/plans", new SaveAdvanceTaxPlanRequest(
            ClientId: client.Id,
            FinancialYear: "2024-25",
            AssessmentYear: "2025-26",
            Regime: AdvanceTaxRegime.NewRegime,
            IsPresumptive: false,
            IsSeniorCitizen: false,
            EstimatedGrossIncome: 2500000m,
            EstimatedDeductions: 75000m,
            EstimatedTaxBeforeCredits: 420000m,
            EstimatedTdsTcs: 0m,
            OtherReliefs: 0m,
            Notes: "Initial Plan"
        ));
        Assert.Equal(HttpStatusCode.Created, planRes.StatusCode);
        var plan = (await planRes.Content.ReadFromJsonAsync<AdvanceTaxPlanDto>())!;

        var importAisRes = await http.PostAsJsonAsync($"/api/advance-tax/plans/{plan.Id}/import-ais", new ImportAisToPlanRequest(aisJson));
        Assert.Equal(HttpStatusCode.OK, importAisRes.StatusCode);
        var updatedPlan = (await importAisRes.Content.ReadFromJsonAsync<AdvanceTaxPlanDto>())!;

        Assert.Equal(327000m, updatedPlan.EstimatedTdsTcs); // 320k salary + 5k FD + 2k dividend
        
        var instRes = await http.GetAsync($"/api/advance-tax/plans/{plan.Id}/installments");
        Assert.Equal(HttpStatusCode.OK, instRes.StatusCode);
        var installments = (await instRes.Content.ReadFromJsonAsync<List<AdvanceTaxInstallmentDto>>())!;
        Assert.True(installments.Count > 0);
        var inst1 = installments.First(i => i.InstallmentNumber == 1);
        Assert.Equal(50000m, inst1.AmountPaid);
        Assert.True(inst1.IsPaid);
    }

    [Fact]
    public async Task Gst_Json_Import_To_Reconciliation_And_Gstr9_Succeeds()
    {
        var (http, _) = await NewFirmAsync("GST DataIn Firm");
        var client = await CreateClientAsync(http, "Apex Logistics Pvt Ltd");

        string gstr2bJson = """
            {
                "gstin": "27AAACB1234F1Z9",
                "fp": "042024",
                "b2b": [
                    {
                        "ctin": "27BBBCB5678M1Z1",
                        "cname": "Supplier Logistics",
                        "inv": [
                            {
                                "inum": "INV-1001",
                                "idt": "12-04-2024",
                                "val": 236000.00,
                                "pos": "27",
                                "itcavl": "Y",
                                "items": [
                                    { "txval": 200000.00, "cgst": 18000.00, "sgst": 18000.00, "igst": 0.00, "cess": 0.00 }
                                ]
                            }
                        ]
                    }
                ]
            }
            """;

        // 1. Run GST reconciliation directly from GSTR-2B JSON
        var reconReq = new RunReconciliationFromJsonRequest(
            ClientId: client.Id,
            ReturnPeriod: "042024",
            FinancialYear: "2024-25",
            Gstr2BJson: gstr2bJson,
            PurchaseInvoices:
            [
                new PurchaseRegisterInvoice(
                    SupplierGstin: "27BBBCB5678M1Z1",
                    SupplierName: "Supplier Logistics",
                    InvoiceNumber: "INV-1001",
                    InvoiceDate: new DateOnly(2024, 4, 12),
                    InvoiceType: "B2B",
                    TaxableValue: 200000.00m,
                    Cgst: 18000.00m,
                    Sgst: 18000.00m,
                    Igst: 0m,
                    Cess: 0m
                )
            ]
        );

        var reconRes = await http.PostAsJsonAsync("/api/gst/reconcile-json", reconReq);
        if (reconRes.StatusCode != HttpStatusCode.Created)
        {
            var err = await reconRes.Content.ReadAsStringAsync();
            throw new Exception($"RECON FAILED: {reconRes.StatusCode} - {err}");
        }
        var batch = (await reconRes.Content.ReadFromJsonAsync<GstReconciliationBatchDto>())!;
        Assert.Equal(1, batch.MatchedCount);
        Assert.Equal(0, batch.MissingIn2BCount);
        Assert.Equal(36000.00m, batch.TotalItcEligible);

        // 2. Import GSTR-1 and GSTR-2B JSON directly into GSTR-9 annual return
        string gstr1Json = """
            {
                "gstin": "27AAACB1234F1Z9",
                "fp": "2024-25",
                "b2b": [
                    {
                        "inv": [
                            {
                                "items": [
                                    { "txval": 4500000.00, "igst": 810000.00, "cgst": 0.00, "sgst": 0.00, "cess": 0.00 }
                                ]
                            }
                        ]
                    }
                ],
                "nil": {
                    "inv": [
                        { "expt_amt": 500000.00, "nil_amt": 0.00, "ngsup_amt": 0.00 }
                    ]
                }
            }
            """;

        var gstr9Req = new ImportGstJsonToReturnRequest(
            ClientId: client.Id,
            FinancialYear: "2024-25",
            Gstr1Json: gstr1Json,
            Gstr2BJson: gstr2bJson
        );

        var gstr9Res = await http.PostAsJsonAsync("/api/gstr9/import-gst-json", gstr9Req);
        Assert.Equal(HttpStatusCode.OK, gstr9Res.StatusCode);
        var gstr9 = (await gstr9Res.Content.ReadFromJsonAsync<Gstr9ReturnDto>())!;

        Assert.Equal(4500000.00m, gstr9.OutwardTaxableSupplies);
        Assert.Equal(500000.00m, gstr9.OutwardExemptedSupplies);
        Assert.Equal(5000000.00m, gstr9.TotalTurnover);
        Assert.Equal(36000.00m, gstr9.ItcAsPer2a);
    }

    [Fact]
    public async Task BankFeed_PdfStatement_Upload_Succeeds()
    {
        var (http, _) = await NewFirmAsync("Bank DataIn Firm");
        var client = await CreateClientAsync(http, "Zenith Exports Ltd");

        // 1. Create Bank Account
        var accRes = await http.PostAsJsonAsync("/api/bank/accounts", new CreateBankAccountRequest(
            ClientId: client.Id,
            AccountName: "SBI Operations A/c",
            BankName: "State Bank of India",
            BranchName: "Fort Mumbai",
            AccountNumber: "30291823901",
            IfscCode: "SBIN0000300",
            AccountType: BankAccountType.Current,
            OpeningBalance: 100000m,
            Currency: "INR"
        ));
        Assert.Equal(HttpStatusCode.Created, accRes.StatusCode);
        var account = (await accRes.Content.ReadFromJsonAsync<BankAccountDto>())!;

        // 2. Upload PDF Bank Statement
        string statementText = """
            STATE BANK OF INDIA - STATEMENT OF ACCOUNT
            Account Number: 30291823901
            Branch: Fort Mumbai
            Date            Narration                           Chq/Ref No    Debit        Credit        Balance
            01/04/2024      OPENING BALANCE                                                              100000.00
            05/04/2024      NEFT-CLIENT-SETTLEMENT              UTR1029381                 95000.00     195000.00
            10/04/2024      OFFICE RENT CHEQUE                  CHQ-100291    30000.00                   165000.00
            """;

        var uploadReq = new UploadStatementRequest(
            FileName: "SBI_Statement_April_2024.pdf",
            CsvContent: statementText,
            StatementDate: new DateOnly(2024, 4, 30)
        );

        var stmtRes = await http.PostAsJsonAsync($"/api/bank/accounts/{account.Id}/statements", uploadReq);
        Assert.Equal(HttpStatusCode.Created, stmtRes.StatusCode);
        var stmt = (await stmtRes.Content.ReadFromJsonAsync<BankStatementDto>())!;

        Assert.Equal(BankStatementFormat.PDF_PARSED, stmt.Format);
        Assert.Equal(2, stmt.TotalTransactions);
        Assert.Equal(30000m, stmt.TotalDebits);
        Assert.Equal(95000m, stmt.TotalCredits);
        Assert.Equal(165000m, stmt.ClosingBalance);

        // Verify account current balance was updated
        var getAccRes = await http.GetAsync($"/api/bank/accounts/{account.Id}");
        Assert.Equal(HttpStatusCode.OK, getAccRes.StatusCode);
        var updatedAcc = (await getAccRes.Content.ReadFromJsonAsync<BankAccountDto>())!;
        Assert.Equal(165000m, updatedAcc.CurrentBalance);
    }
}
