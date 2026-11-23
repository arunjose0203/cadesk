using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CaDesk.Domain.TrialBalance;

namespace CaDesk.Application.TrialBalance;

public static class UniversalTrialBalanceParser
{
    private static readonly string[] DebitNatureGroups =
    [
        "sundry debtors", "cash-in-hand", "cash in hand", "bank accounts", "fixed assets", "stock-in-hand", "stock in hand",
        "loans & advances (asset)", "deposits (asset)", "purchase accounts", "direct expenses", "indirect expenses",
        "accounts receivable", "tangible assets", "inventories", "cash and bank"
    ];

    public static ParsedTrialBalanceResult Parse(string content, string? sourceType = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Trial balance content cannot be empty.");

        var trimmed = content.Trim();
        var effectiveType = ResolveSourceType(trimmed, sourceType);

        ParsedTrialBalanceResult parsed = effectiveType.Equals(TrialBalanceConstants.SourceTallyXml, StringComparison.OrdinalIgnoreCase)
            ? ParseXml(trimmed)
            : ParseDelimited(trimmed, effectiveType);

        return NormaliseSignConvention(parsed);
    }

    public static TrialBalancePreviewResultDto Preview(string content, string? sourceType = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Trial balance content cannot be empty.");

        var trimmed = content.Trim();
        var effectiveType = ResolveSourceType(trimmed, sourceType);
        var delimiter = effectiveType.Equals(TrialBalanceConstants.SourceTallyXml, StringComparison.OrdinalIgnoreCase)
            ? "XML"
            : DetectDelimiter(trimmed).ToString();

        var parsed = Parse(content, effectiveType);
        var diagnostics = new List<string>();

        // Run mapping inference to test auto-classification
        var domainLedgers = parsed.Ledgers.Select(l => new TrialBalanceLedger
        {
            Id = Guid.NewGuid(),
            LedgerName = l.Name,
            TallyGroup = l.ParentGroup,
            OpeningBalance = l.OpeningBalance,
            DebitAmount = l.DebitAmount,
            CreditAmount = l.CreditAmount,
            ClosingBalance = l.ClosingBalance
        }).ToList();

        foreach (var l in domainLedgers)
        {
            l.IsMapped = TrialBalanceMappingEngine.InferStandardMapping(l);
        }

        int mappedCount = domainLedgers.Count(l => l.IsMapped);
        decimal discrepancy = Math.Abs(parsed.TotalDebit - parsed.TotalCredit);

        if (parsed.IsBalanced)
        {
            diagnostics.Add("✓ Trial balance is mathematically balanced to ₹0 difference.");
        }
        else
        {
            diagnostics.Add($"⚠ Out of balance by ₹{discrepancy:N2} (Debits: ₹{parsed.TotalDebit:N2}, Credits: ₹{parsed.TotalCredit:N2}).");
        }

        if (parsed.SignsInverted)
        {
            diagnostics.Add("ℹ Detected Tally debit-negative convention; automatically converted to debit-positive standard.");
        }

        diagnostics.Add($"ℹ Auto-detected format: {effectiveType} with {parsed.Ledgers.Count} ledger accounts.");
        diagnostics.Add($"ℹ Indian Chart of Accounts classification: {mappedCount} of {parsed.Ledgers.Count} ({Math.Round((decimal)mappedCount / (parsed.Ledgers.Count > 0 ? parsed.Ledgers.Count : 1) * 100, 1)}%) auto-mapped.");

        // Compute KPIs if balanced or near balanced
        FinancialKpisDto? kpis = null;
        try
        {
            var figures = TrialBalanceSyncEngine.ComputeFigures(domainLedgers);
            decimal grossRevenue = figures.RevenueFromOperations;
            decimal otherInc = figures.OtherIncome;
            decimal totalInc = figures.TotalIncome;
            decimal totalExp = figures.TotalExpensesBeforeTax;
            decimal pbt = figures.ProfitBeforeTax;
            decimal pat = figures.ProfitAfterTax;
            decimal ebitda = figures.Ebit;
            decimal opProfit = figures.RevenueFromOperations - figures.CostOfGoodsSold;
            decimal assets = figures.TotalAssets;
            decimal liabilities = figures.CurrentLiabilities + figures.TotalDebt;
            decimal debt = figures.TotalDebt;
            decimal equity = figures.ShareholdersEquity;
            decimal currentRatio = figures.CurrentLiabilities != 0 ? Math.Round(figures.CurrentAssets / figures.CurrentLiabilities, 2) : 0m;

            kpis = new FinancialKpisDto(
                grossRevenue, otherInc, totalInc, totalExp, opProfit, ebitda, pbt, pat,
                assets, liabilities, debt, equity, currentRatio
            );
        }
        catch
        {
            // Fallback gracefully if figures cannot be derived
        }

        return new TrialBalancePreviewResultDto(
            DetectedSourceType: effectiveType,
            DelimiterUsed: delimiter == "\t" ? "Tab (\\t)" : delimiter,
            HeaderRowIndex: 0,
            TotalLedgersCount: parsed.Ledgers.Count,
            MappedLedgersCount: mappedCount,
            TotalDebit: parsed.TotalDebit,
            TotalCredit: parsed.TotalCredit,
            Discrepancy: discrepancy,
            IsBalanced: parsed.IsBalanced,
            Ledgers: parsed.Ledgers,
            Kpis: kpis,
            Diagnostics: diagnostics
        );
    }

    public static string ResolveSourceType(string trimmedContent, string? requestedSourceType)
    {
        if (!string.IsNullOrWhiteSpace(requestedSourceType) &&
            !requestedSourceType.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return requestedSourceType;
        }

        if (trimmedContent.StartsWith("<") ||
            trimmedContent.Contains("<ENVELOPE", StringComparison.OrdinalIgnoreCase) ||
            trimmedContent.Contains("<TALLYMESSAGE", StringComparison.OrdinalIgnoreCase) ||
            trimmedContent.Contains("<LEDGER", StringComparison.OrdinalIgnoreCase))
        {
            return TrialBalanceConstants.SourceTallyXml;
        }

        var lines = trimmedContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var sampleLines = lines.Take(15).ToList();

        // Check for SAP GL indicators
        bool isSap = sampleLines.Any(l =>
            l.Contains("g/l account", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("gl account", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("gl description", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("balance in local currency", StringComparison.OrdinalIgnoreCase));
        if (isSap) return TrialBalanceConstants.SourceSapGl;

        // Check for Busy Accounting indicators
        bool isBusy = sampleLines.Any(l =>
            l.Contains("parent group", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("op. bal", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("debit (rs.)", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("credit (rs.)", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("closing bal", StringComparison.OrdinalIgnoreCase));
        if (isBusy) return TrialBalanceConstants.SourceBusyCsv;

        char delimiter = DetectDelimiter(trimmedContent);
        if (delimiter == '\t') return TrialBalanceConstants.SourceClipboardTsv;

        return TrialBalanceConstants.SourceTallyCsv;
    }

    public static char DetectDelimiter(string content)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Take(10).ToList();
        if (lines.Count == 0) return ',';

        int tabCount = lines.Sum(l => l.Count(c => c == '\t'));
        int commaCount = lines.Sum(l => l.Count(c => c == ','));
        int semiCount = lines.Sum(l => l.Count(c => c == ';'));
        int pipeCount = lines.Sum(l => l.Count(c => c == '|'));

        if (tabCount > commaCount && tabCount > semiCount && tabCount > pipeCount) return '\t';
        if (pipeCount > commaCount && pipeCount > tabCount && pipeCount > semiCount) return '|';
        if (semiCount > commaCount && semiCount > tabCount && semiCount > pipeCount) return ';';

        return ',';
    }

    public static ParsedTrialBalanceResult ParseXml(string xmlContent)
    {
        var ledgers = new List<ParsedTallyLedger>();

        try
        {
            using var xmlReader = System.Xml.XmlReader.Create(new StringReader(xmlContent), new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 100_000_000,
            });
            var doc = XDocument.Load(xmlReader);

            var ledgerElements = doc.Descendants("LEDGER");
            foreach (var el in ledgerElements)
            {
                var name = el.Attribute("NAME")?.Value
                           ?? el.Element("NAME")?.Value
                           ?? el.Element("LANGUAGENAME.LIST")?.Element("NAME.LIST")?.Element("NAME")?.Value
                           ?? el.Attribute("VCHNAME")?.Value;

                if (string.IsNullOrWhiteSpace(name)) continue;

                var parent = el.Element("PARENT")?.Value ?? el.Element("PARENTNAME")?.Value ?? "";

                var opStr = el.Element("OPENINGBALANCE")?.Value ?? "0";
                var clStr = el.Element("CLOSINGBALANCE")?.Value ?? "0";

                var opening = ParseAmount(opStr);
                var closing = ParseAmount(clStr);

                var drStr = el.Element("DEBITTOTAL")?.Value ?? el.Element("DEBIT")?.Value;
                var crStr = el.Element("CREDITTOTAL")?.Value ?? el.Element("CREDIT")?.Value;

                decimal debit = drStr != null ? ParseAmount(drStr) : (closing > 0 ? closing : 0m);
                decimal credit = crStr != null ? ParseAmount(crStr) : (closing < 0 ? Math.Abs(closing) : 0m);

                ledgers.Add(new ParsedTallyLedger(name.Trim(), parent.Trim(), opening, debit, credit, closing));
            }

            if (ledgers.Count == 0)
            {
                var dspRecords = doc.Descendants("DSPRECORD");
                foreach (var el in dspRecords)
                {
                    var name = el.Element("DSPDISPNAME")?.Value ?? el.Element("DSPACCNAME")?.Value;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var parent = el.Element("DSPGROUP")?.Value ?? "";
                    var dr = ParseAmount(el.Element("DSPCLDR")?.Value ?? "0");
                    var cr = ParseAmount(el.Element("DSPCLCR")?.Value ?? "0");
                    var closing = dr != 0 ? dr : -cr;

                    ledgers.Add(new ParsedTallyLedger(name.Trim(), parent.Trim(), 0m, dr, cr, closing));
                }
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse XML Trial Balance: {ex.Message}", ex);
        }

        if (ledgers.Count == 0)
            throw new ArgumentException("No readable ledger accounts found in the XML payload.");

        return ComputeTotals(ledgers);
    }

    public static ParsedTrialBalanceResult ParseDelimited(string content, string sourceType)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            throw new ArgumentException("Financial data contains no rows.");

        char delimiter = DetectDelimiter(content);
        var ledgers = new List<ParsedTallyLedger>();

        int headerIndex = -1;
        int nameCol = -1, codeCol = -1, groupCol = -1, opCol = -1, drCol = -1, crCol = -1, clCol = -1;

        // Scan the first 25 rows to identify the header row
        for (int i = 0; i < Math.Min(lines.Length, 25); i++)
        {
            var cols = SplitLine(lines[i], delimiter);
            int matchedCols = 0;

            for (int c = 0; c < cols.Count; c++)
            {
                var h = NormalizeHeader(cols[c]);

                if (IsAccountNameHeader(h))
                {
                    nameCol = c;
                    matchedCols++;
                }
                else if (IsAccountCodeHeader(h) && codeCol == -1)
                {
                    codeCol = c;
                    matchedCols++;
                }
                else if (IsGroupHeader(h) && groupCol == -1)
                {
                    groupCol = c;
                    matchedCols++;
                }
                else if (IsOpeningHeader(h) && opCol == -1)
                {
                    opCol = c;
                    matchedCols++;
                }
                else if (IsDebitHeader(h) && drCol == -1)
                {
                    drCol = c;
                    matchedCols++;
                }
                else if (IsCreditHeader(h) && crCol == -1)
                {
                    crCol = c;
                    matchedCols++;
                }
                else if (IsClosingHeader(h) && clCol == -1)
                {
                    clCol = c;
                    matchedCols++;
                }
            }

            // A valid trial balance header must have at least an Account Name or Code, and one amount column
            if ((nameCol >= 0 || codeCol >= 0) && (drCol >= 0 || crCol >= 0 || clCol >= 0))
            {
                headerIndex = i;
                break;
            }

            // Reset if insufficient matches
            nameCol = codeCol = groupCol = opCol = drCol = crCol = clCol = -1;
        }

        // Default fallback if headers were ambiguous
        if (headerIndex == -1)
        {
            headerIndex = 0;
            nameCol = 0;
            if (SplitLine(lines[0], delimiter).Count > 1) drCol = 1;
            if (SplitLine(lines[0], delimiter).Count > 2) crCol = 2;
        }

        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            var cols = SplitLine(lines[i], delimiter);
            int effectiveNameIndex = nameCol >= 0 ? nameCol : codeCol;
            if (cols.Count <= effectiveNameIndex || effectiveNameIndex < 0) continue;

            var name = cols[effectiveNameIndex].Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Skip total, summary or decorative separator rows
            var lower = name.ToLowerInvariant();
            if (lower.Equals("total") || lower.Contains("grand total") || lower.StartsWith("---") ||
                lower.StartsWith("===") || lower.StartsWith("***") || lower.Contains("closing balance total"))
                continue;

            string group = groupCol >= 0 && groupCol < cols.Count ? cols[groupCol].Trim().Trim('"', '\'') : "";
            decimal opening = opCol >= 0 && opCol < cols.Count ? ParseAmount(cols[opCol]) : 0m;
            decimal debit = drCol >= 0 && drCol < cols.Count ? ParseAmount(cols[drCol]) : 0m;
            decimal credit = crCol >= 0 && crCol < cols.Count ? ParseAmount(cols[crCol]) : 0m;

            decimal closing = 0m;
            if (clCol >= 0 && clCol < cols.Count && !string.IsNullOrWhiteSpace(cols[clCol]))
            {
                closing = ParseAmount(cols[clCol]);
                // If closing was parsed but debit/credit were 0, derive debit/credit from closing
                if (debit == 0m && credit == 0m)
                {
                    debit = closing > 0 ? closing : 0m;
                    credit = closing < 0 ? Math.Abs(closing) : 0m;
                }
            }
            else
            {
                // Derive closing from Debit / Credit
                closing = debit > 0 ? debit : (credit > 0 ? -credit : 0m);
            }

            ledgers.Add(new ParsedTallyLedger(name, group, opening, debit, credit, closing));
        }

        if (ledgers.Count == 0)
            throw new ArgumentException("No readable ledger entries found in the file or pasted text.");

        return ComputeTotals(ledgers);
    }

    public static ParsedTrialBalanceResult NormaliseSignConvention(ParsedTrialBalanceResult parsed)
    {
        var probes = parsed.Ledgers
            .Where(l => l.ClosingBalance != 0 && DebitNatureGroups.Contains(l.ParentGroup.Trim().ToLowerInvariant()))
            .ToList();
        int negatives = probes.Count(l => l.ClosingBalance < 0);
        if (probes.Count == 0 || negatives * 2 <= probes.Count) return parsed;

        var flipped = parsed.Ledgers.Select(l =>
        {
            bool derivedFromClosing = l.DebitAmount == Math.Max(l.ClosingBalance, 0m) && l.CreditAmount == Math.Max(-l.ClosingBalance, 0m);
            decimal closing = -l.ClosingBalance;
            return l with
            {
                OpeningBalance = -l.OpeningBalance,
                ClosingBalance = closing,
                DebitAmount = derivedFromClosing ? Math.Max(closing, 0m) : Math.Abs(l.DebitAmount),
                CreditAmount = derivedFromClosing ? Math.Max(-closing, 0m) : Math.Abs(l.CreditAmount),
            };
        }).ToList();

        return ComputeTotals(flipped) with { SignsInverted = true };
    }

    public static decimal ParseAmount(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0m;
        var s = val.Trim();

        // Check for Dr / Cr indicators
        bool isCr = Regex.IsMatch(s, @"(?i)(\bcr\b|cr$|^cr)");
        bool isDr = Regex.IsMatch(s, @"(?i)(\bdr\b|dr$|^dr)");
        bool isParenthesized = s.StartsWith("(") && s.EndsWith(")");
        bool isTrailingMinus = s.EndsWith("-");

        // Remove currency symbols, Dr/Cr, parens, spaces, and commas
        s = Regex.Replace(s, @"[₹\$€£\(\)\s,]", "");
        // Remove currency text like "Rs." or "INR"
        s = Regex.Replace(s, @"(?i)\b(rs\.?|inr|usd|eur|gbp)\b", "");
        s = Regex.Replace(s, @"(?i)(\bcr\b|\bdr\b)", "");
        s = s.TrimEnd('-');

        // Keep only digits, dots, minus
        s = Regex.Replace(s, @"[^\d\.\-]", "");

        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            if ((isCr || isParenthesized || isTrailingMinus) && result > 0)
                return -result;
            return result;
        }

        return 0m;
    }

    private static ParsedTrialBalanceResult ComputeTotals(List<ParsedTallyLedger> ledgers)
    {
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var l in ledgers)
        {
            if (l.DebitAmount != 0 || l.CreditAmount != 0)
            {
                totalDebit += l.DebitAmount;
                totalCredit += l.CreditAmount;
            }
            else
            {
                if (l.ClosingBalance >= 0) totalDebit += l.ClosingBalance;
                else totalCredit += Math.Abs(l.ClosingBalance);
            }
        }

        bool isBalanced = Math.Abs(totalDebit - totalCredit) < 1.00m;
        return new ParsedTrialBalanceResult(ledgers, totalDebit, totalCredit, isBalanced);
    }

    private static string NormalizeHeader(string h)
    {
        return Regex.Replace(h.ToLowerInvariant(), @"[^\w\s]", " ").Trim();
    }

    private static bool IsAccountNameHeader(string h)
    {
        if (h.Contains("gl description") || h.Contains("account description") || h.Contains("account name") ||
            h.Contains("ledger name") || h.Contains("particular") || h.Contains("head of account") ||
            h.Contains("a c name") || h.Contains("long text") || h.Contains("description") ||
            h.Equals("name") || h.Contains("title"))
            return true;

        if (h.Contains("ledger") || (h.Contains("account") && !h.Contains("code") && !h.Contains("no") && !h.Contains("number") && !h.Contains("g l")))
            return true;

        return false;
    }

    private static bool IsAccountCodeHeader(string h)
    {
        return h.Contains("g l account") || h.Contains("gl account") || h.Contains("account code") ||
               h.Contains("account no") || h.Contains("account number") || h.Contains("ledger code") ||
               h.Equals("code") || h.Contains("gl a c");
    }

    private static bool IsGroupHeader(string h)
    {
        return h.Contains("parent group") || h.Contains("group name") || h.Contains("account group") ||
               h.Contains("parent") || h.Contains("tally group") || h.Contains("classification") ||
               h.Contains("category") || h.Equals("group") || h.Equals("head");
    }

    private static bool IsOpeningHeader(string h)
    {
        return h.Contains("opening") || h.Contains("op bal") || h.Contains("initial") || h.Contains("beginning");
    }

    private static bool IsDebitHeader(string h)
    {
        return Regex.IsMatch(h, @"\b(dr|debit|debits)\b") && !Regex.IsMatch(h, @"\b(cr|credit|credits)\b");
    }

    private static bool IsCreditHeader(string h)
    {
        return Regex.IsMatch(h, @"\b(cr|credit|credits)\b");
    }

    private static bool IsClosingHeader(string h)
    {
        return (h.Contains("closing") || h.Contains("cl bal") || h.Contains("balance in local currency") ||
                h.Contains("net balance") || h.Contains("final balance") || h.Equals("balance") || h.Equals("net")) &&
               !h.Contains("opening");
    }

    private static List<string> SplitLine(string line, char delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var sb = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    public static List<TrialBalanceSampleDto> GetPreloadedSamples()
    {
        return
        [
            new TrialBalanceSampleDto(
                Key: "tally-mfg",
                Title: "Tally Prime: Bharat Heavy Engineering Ltd",
                Description: "Manufacturing enterprise trial balance with 42 standard ledgers, factory power, raw materials, MSME creditors, GST/TDS dues, and ₹46.45 Cr turnover.",
                SourceType: TrialBalanceConstants.SourceTallyXml,
                TurnoverInr: 464500000m,
                LedgersCount: 42,
                ContentPayload: SampleTallyManufacturingXml
            ),
            new TrialBalanceSampleDto(
                Key: "busy-trading",
                Title: "Busy Accounting: Shree Ram Trading Enterprises",
                Description: "FMCG trading firm trial balance with Busy column layout (Tab-delimited), Dr/Cr suffixes, FMCG stock, sundry debtors, and ₹18.2 Cr turnover.",
                SourceType: TrialBalanceConstants.SourceBusyCsv,
                TurnoverInr: 182000000m,
                LedgersCount: 26,
                ContentPayload: SampleBusyTradingTsv
            ),
            new TrialBalanceSampleDto(
                Key: "sap-services",
                Title: "SAP ERP: NexaTech Cloud Solutions Corp",
                Description: "Enterprise cloud services trial balance with SAP GL account codes (100000-700000 series), local currency balances, and ₹85.0 Cr revenue.",
                SourceType: TrialBalanceConstants.SourceSapGl,
                TurnoverInr: 850000000m,
                LedgersCount: 26,
                ContentPayload: SampleSapServicesTsv
            )
        ];
    }

    private const string SampleTallyManufacturingXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <ENVELOPE>
            <HEADER><TALLYREQUEST>Export Data</TALLYREQUEST></HEADER>
            <BODY>
                <DATA>
                    <COLLECTION>
                        <LEDGER NAME="Equity Share Capital">
                            <PARENT>Capital Account</PARENT>
                            <OPENINGBALANCE>-100000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-100000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="General Reserve">
                            <PARENT>Reserves &amp; Surplus</PARENT>
                            <OPENINGBALANCE>-55000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-55000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Retained Earnings / P&amp;L A/c">
                            <PARENT>Reserves &amp; Surplus</PARENT>
                            <OPENINGBALANCE>-55000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-55000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="HDFC Bank Term Loan A/c">
                            <PARENT>Secured Loans</PARENT>
                            <OPENINGBALANCE>-80000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-80000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="SBI Working Capital OD Facility">
                            <PARENT>Bank OD A/c</PARENT>
                            <OPENINGBALANCE>-35000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-35000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Bharat Steel Works (MSME)">
                            <PARENT>Sundry Creditors</PARENT>
                            <OPENINGBALANCE>-14500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-14500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Tata Power Discom A/c">
                            <PARENT>Sundry Creditors</PARENT>
                            <OPENINGBALANCE>-3500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-3500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Reliance Polymers Ltd">
                            <PARENT>Sundry Creditors</PARENT>
                            <OPENINGBALANCE>-21000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-21000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="GST Output Payable">
                            <PARENT>Duties &amp; Taxes</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-4800000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="TDS Payable">
                            <PARENT>Duties &amp; Taxes</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-1200000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="PF &amp; ESI Payable">
                            <PARENT>Duties &amp; Taxes</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-600000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Provision for Income Tax">
                            <PARENT>Provisions</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-11000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Factory Land &amp; Buildings">
                            <PARENT>Fixed Assets</PARENT>
                            <OPENINGBALANCE>125000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>125000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="CNC Plant and Machinery">
                            <PARENT>Fixed Assets</PARENT>
                            <OPENINGBALANCE>98000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>98000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="IT Systems and Computers">
                            <PARENT>Fixed Assets</PARENT>
                            <OPENINGBALANCE>11500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>11500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Commercial Delivery Vehicles">
                            <PARENT>Fixed Assets</PARENT>
                            <OPENINGBALANCE>8500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>8500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Raw Materials Inventory">
                            <PARENT>Stock-in-hand</PARENT>
                            <OPENINGBALANCE>42000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>42000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Work-in-Progress">
                            <PARENT>Stock-in-hand</PARENT>
                            <OPENINGBALANCE>18000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>18000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Finished Goods Stock">
                            <PARENT>Stock-in-hand</PARENT>
                            <OPENINGBALANCE>36000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>36000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Sundry Debtors - Apex Engineering">
                            <PARENT>Sundry Debtors</PARENT>
                            <OPENINGBALANCE>29500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>29500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Sundry Debtors - Mahindra Auto">
                            <PARENT>Sundry Debtors</PARENT>
                            <OPENINGBALANCE>34000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>34000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Sundry Debtors - L&amp;T Projects">
                            <PARENT>Sundry Debtors</PARENT>
                            <OPENINGBALANCE>21000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>21000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="HDFC Current Account">
                            <PARENT>Bank Accounts</PARENT>
                            <OPENINGBALANCE>14500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>14500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="SBI Operating Account">
                            <PARENT>Bank Accounts</PARENT>
                            <OPENINGBALANCE>6500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>6500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Petty Cash A/c">
                            <PARENT>Cash-in-hand</PARENT>
                            <OPENINGBALANCE>100000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>100000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Advance to Suppliers">
                            <PARENT>Loans &amp; Advances (Asset)</PARENT>
                            <OPENINGBALANCE>4500000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>4500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="GST Input Tax Credit (ITC)">
                            <PARENT>Current Assets</PARENT>
                            <OPENINGBALANCE>6000000.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>6000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Domestic Product Sales">
                            <PARENT>Sales Accounts</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-421500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Export Sales of Goods">
                            <PARENT>Sales Accounts</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-43000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Scrap Sales Recovery">
                            <PARENT>Indirect Incomes</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-3500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Interest on Bank Deposits">
                            <PARENT>Indirect Incomes</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>-1500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Purchases - Raw Steel &amp; Alloys">
                            <PARENT>Purchase Accounts</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>235000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Purchases - Packaging Consumables">
                            <PARENT>Purchase Accounts</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>18000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Factory Electricity and Power">
                            <PARENT>Direct Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>24000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Factory Wages and Labor">
                            <PARENT>Direct Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>41000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Freight and Carriage Inward">
                            <PARENT>Direct Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>9500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Staff Salaries and Allowances">
                            <PARENT>Indirect Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>28500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Depreciation on Plant &amp; Assets">
                            <PARENT>Indirect Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>14000000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Factory Rent &amp; Godown Lease">
                            <PARENT>Indirect Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>7500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Interest on Bank Borrowings">
                            <PARENT>Indirect Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>11500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Legal and Audit Fees">
                            <PARENT>Indirect Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>2500000.00</CLOSINGBALANCE>
                        </LEDGER>
                        <LEDGER NAME="Office Administrative Expenses">
                            <PARENT>Indirect Expenses</PARENT>
                            <OPENINGBALANCE>0.00</OPENINGBALANCE>
                            <CLOSINGBALANCE>4500000.00</CLOSINGBALANCE>
                        </LEDGER>
                    </COLLECTION>
                </DATA>
            </BODY>
        </ENVELOPE>
        """;

    private const string SampleBusyTradingTsv =
        "Account Name\tParent Group\tOp. Bal.\tDebit (Rs.)\tCredit (Rs.)\tClosing Bal.\n" +
        "Capital A/c\tCapital A/c\t25000000.00 Cr\t0.00\t0.00\t25000000.00 Cr\n" +
        "Reserves & Surplus\tReserves & Surplus\t12000000.00 Cr\t0.00\t0.00\t12000000.00 Cr\n" +
        "Axis Bank Working Capital OD\tBank O/D A/c\t9500000.00 Cr\t0.00\t0.00\t9500000.00 Cr\n" +
        "Shree Trading Suppliers (MSME)\tSundry Creditors\t6500000.00 Cr\t0.00\t0.00\t6500000.00 Cr\n" +
        "Prime Distributor Corp\tSundry Creditors\t11000000.00 Cr\t0.00\t0.00\t11000000.00 Cr\n" +
        "GST Payable\tDuties & Taxes\t0.00\t0.00\t1800000.00\t1800000.00 Cr\n" +
        "TDS Payable\tDuties & Taxes\t0.00\t0.00\t400000.00\t400000.00 Cr\n" +
        "Commercial Godown & Office\tFixed Assets\t21000000.00 Dr\t0.00\t0.00\t21000000.00 Dr\n" +
        "Delivery Vans & Trucks\tFixed Assets\t4500000.00 Dr\t0.00\t0.00\t4500000.00 Dr\n" +
        "Computers & Barcode Scanners\tFixed Assets\t1500000.00 Dr\t0.00\t0.00\t1500000.00 Dr\n" +
        "Trading Stock-in-Hand\tStock-in-hand\t28000000.00 Dr\t0.00\t0.00\t28000000.00 Dr\n" +
        "Metro Retail Mart\tSundry Debtors\t14500000.00 Dr\t0.00\t0.00\t14500000.00 Dr\n" +
        "City Supermarket Network\tSundry Debtors\t18500000.00 Dr\t0.00\t0.00\t18500000.00 Dr\n" +
        "Axis Bank Current Account\tBank Accounts\t5500000.00 Dr\t0.00\t0.00\t5500000.00 Dr\n" +
        "Cash-in-Hand\tCash-in-hand\t400000.00 Dr\t0.00\t0.00\t400000.00 Dr\n" +
        "Input GST Credit (ITC)\tCurrent Assets\t2500000.00 Dr\t0.00\t0.00\t2500000.00 Dr\n" +
        "Wholesale Sales A/c\tSales A/c\t0.00\t0.00\t152000000.00\t152000000.00 Cr\n" +
        "Retail & Counter Sales\tSales A/c\t0.00\t0.00\t30000000.00\t30000000.00 Cr\n" +
        "Discount & Incentives Received\tIndirect Incomes\t0.00\t0.00\t1200000.00\t1200000.00 Cr\n" +
        "Purchase A/c - FMCG Goods\tPurchase A/c\t0.00\t131000000.00\t0.00\t131000000.00 Dr\n" +
        "Freight & Transportation Inward\tDirect Expenses\t0.00\t4500000.00\t0.00\t4500000.00 Dr\n" +
        "Staff Salary & Sales Commission\tIndirect Expenses\t0.00\t9500000.00\t0.00\t9500000.00 Dr\n" +
        "Godown & Shop Rent\tIndirect Expenses\t0.00\t4800000.00\t0.00\t4800000.00 Dr\n" +
        "Electricity & Maintenance\tIndirect Expenses\t0.00\t1200000.00\t0.00\t1200000.00 Dr\n" +
        "Bank Charges & Overdraft Interest\tIndirect Expenses\t0.00\t1500000.00\t0.00\t1500000.00 Dr\n" +
        "Audit & Accounting Fees\tIndirect Expenses\t0.00\t500000.00\t0.00\t500000.00 Dr\n";

    private const string SampleSapServicesTsv =
        "G/L Account\tGL Description\tDebit Amount\tCredit Amount\tBalance in Local Currency\n" +
        "100000\tShare Capital - Equity\t0.00\t150000000.00\t-150000000.00\n" +
        "110000\tRetained Earnings / P&L\t0.00\t124000000.00\t-124000000.00\n" +
        "200000\tLong-Term Borrowings - Term Loan\t0.00\t100000000.00\t-100000000.00\n" +
        "210000\tShort-Term Working Capital Facility\t0.00\t45000000.00\t-45000000.00\n" +
        "220000\tTrade Accounts Payable - Vendors\t0.00\t38000000.00\t-38000000.00\n" +
        "230000\tStatutory Due - GST Output Liability\t0.00\t11000000.00\t-11000000.00\n" +
        "231000\tStatutory Due - TDS Deductions Payable\t0.00\t4500000.00\t-4500000.00\n" +
        "232000\tEmployee PF & ESIC Contribution Payable\t0.00\t2500000.00\t-2500000.00\n" +
        "300000\tIT Server Hardware & Cloud Appliances\t145000000.00\t0.00\t145000000.00\n" +
        "310000\tOffice Premises & Leasehold Improvements\t82000000.00\t0.00\t82000000.00\n" +
        "320000\tEnterprise Software & IP Licenses\t56000000.00\t0.00\t56000000.00\n" +
        "400000\tCustomer Accounts Receivable\t195000000.00\t0.00\t195000000.00\n" +
        "410000\tGST Input Tax Credit Receivable\t18500000.00\t0.00\t18500000.00\n" +
        "420000\tCitibank Operating Current Account\t62500000.00\t0.00\t62500000.00\n" +
        "421000\tSBI Treasury Fixed Deposit Account\t40000000.00\t0.00\t40000000.00\n" +
        "500000\tRevenue from Enterprise Cloud Architecture\t0.00\t680000000.00\t-680000000.00\n" +
        "510000\tRevenue from Managed IT Services\t0.00\t170000000.00\t-170000000.00\n" +
        "520000\tInterest Income from Treasury Deposits\t0.00\t6500000.00\t-6500000.00\n" +
        "600000\tCloud Infrastructure Hosting (AWS & GCP)\t215000000.00\t0.00\t215000000.00\n" +
        "610000\tSoftware Engineering & Architect Salaries\t342000000.00\t0.00\t342000000.00\n" +
        "620000\tEmployee Healthcare, PF & Gratuity Expense\t41000000.00\t0.00\t41000000.00\n" +
        "630000\tOffice Facilities & Commercial Utilities Lease\t38000000.00\t0.00\t38000000.00\n" +
        "640000\tLegal, Compliance & Regulatory Advisory\t12000000.00\t0.00\t12000000.00\n" +
        "650000\tStatutory & Internal Audit Fees\t4500000.00\t0.00\t4500000.00\n" +
        "660000\tDepreciation on Fixed & IT Assets\t48000000.00\t0.00\t48000000.00\n" +
        "670000\tFinance Charges & Term Loan Interest\t16500000.00\t0.00\t16500000.00\n" +
        "700000\tCorporate Income Tax Expense\t15500000.00\t0.00\t15500000.00\n";
}
