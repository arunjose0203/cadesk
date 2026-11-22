using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CaDesk.Application.TrialBalance;

public record ParsedTallyLedger(
    string Name,
    string ParentGroup,
    decimal OpeningBalance,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal ClosingBalance
);

public record ParsedTrialBalanceResult(
    List<ParsedTallyLedger> Ledgers,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    bool SignsInverted = false // true when the file used Tally's own convention (debit negative) and was converted
);

public static class TallyTrialBalanceParser
{
    public static ParsedTrialBalanceResult Parse(string content, string sourceType)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Trial balance content cannot be empty.");

        var trimmed = content.Trim();
        var parsed = sourceType.Equals("TallyXml", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("<")
            ? ParseTallyXml(trimmed)
            : ParseTallyCsv(trimmed);
        return NormaliseSignConvention(parsed);
    }

    // Tally's predefined groups whose ledgers are, in practice, always debit balances
    private static readonly string[] DebitNatureTallyGroups =
        ["sundry debtors", "cash-in-hand", "cash in hand", "bank accounts", "fixed assets", "stock-in-hand", "stock in hand",
         "loans & advances (asset)", "deposits (asset)", "purchase accounts", "direct expenses", "indirect expenses"];

    /// <summary>
    /// Balances are stored debit-positive. Tally's own XML writes debit balances as negative numbers, while CSV and
    /// "Dr/Cr" exports are debit-positive; nothing in the file says which. Ledgers under Tally groups that are always
    /// debit (debtors, cash, bank, fixed assets, expenses) decide it: if most of them are negative, the file uses
    /// Tally's convention and every balance is flipped.
    /// </summary>
    public static ParsedTrialBalanceResult NormaliseSignConvention(ParsedTrialBalanceResult parsed)
    {
        var probes = parsed.Ledgers
            .Where(l => l.ClosingBalance != 0 && DebitNatureTallyGroups.Contains(l.ParentGroup.Trim().ToLowerInvariant()))
            .ToList();
        int negatives = probes.Count(l => l.ClosingBalance < 0);
        if (probes.Count == 0 || negatives * 2 <= probes.Count) return parsed;

        var flipped = parsed.Ledgers.Select(l =>
        {
            // debit/credit derived from the closing balance flip with it; explicit (signed) totals just lose their sign
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

    public static ParsedTrialBalanceResult ParseTallyXml(string xmlContent)
    {
        var ledgers = new List<ParsedTallyLedger>();

        try
        {
            // Tally exports never carry a DTD: refuse one, so entity-expansion ("billion laughs") input can't exhaust memory
            using var xmlReader = System.Xml.XmlReader.Create(new StringReader(xmlContent), new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 100_000_000,
            });
            var doc = XDocument.Load(xmlReader);

            // Look for <LEDGER> or <TALLYMESSAGE> or <DSPACCNAME> elements
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

                var opening = ParseTallyAmount(opStr);
                var closing = ParseTallyAmount(clStr);

                // Debit / Credit amounts
                var drStr = el.Element("DEBITTOTAL")?.Value ?? el.Element("DEBIT")?.Value;
                var crStr = el.Element("CREDITTOTAL")?.Value ?? el.Element("CREDIT")?.Value;

                decimal debit = drStr != null ? ParseTallyAmount(drStr) : (closing > 0 ? closing : 0m);
                decimal credit = crStr != null ? ParseTallyAmount(crStr) : (closing < 0 ? Math.Abs(closing) : 0m);

                ledgers.Add(new ParsedTallyLedger(name.Trim(), parent.Trim(), opening, debit, credit, closing));
            }

            // Fallback for Tally Display Account export format: <DSPRECORD> / <DSPACCNAME>
            if (ledgers.Count == 0)
            {
                var dspRecords = doc.Descendants("DSPRECORD");
                foreach (var el in dspRecords)
                {
                    var name = el.Element("DSPDISPNAME")?.Value ?? el.Element("DSPACCNAME")?.Value;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var parent = el.Element("DSPGROUP")?.Value ?? "";
                    var dr = ParseTallyAmount(el.Element("DSPCLDR")?.Value ?? "0");
                    var cr = ParseTallyAmount(el.Element("DSPCLCR")?.Value ?? "0");
                    var closing = dr != 0 ? dr : -cr;

                    ledgers.Add(new ParsedTallyLedger(name.Trim(), parent.Trim(), 0m, dr, cr, closing));
                }
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse Tally XML: {ex.Message}", ex);
        }

        if (ledgers.Count == 0)
        {
            throw new ArgumentException("No readable ledger accounts found in the uploaded Tally XML.");
        }

        return ComputeTotals(ledgers);
    }

    public static ParsedTrialBalanceResult ParseTallyCsv(string csvContent)
    {
        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            throw new ArgumentException("CSV file contains no rows.");

        var ledgers = new List<ParsedTallyLedger>();

        int headerIndex = -1;
        int nameCol = 0, groupCol = -1, opCol = -1, drCol = -1, crCol = -1, clCol = -1;

        for (int i = 0; i < Math.Min(lines.Length, 15); i++)
        {
            var cols = SplitCsvLine(lines[i]);
            for (int c = 0; c < cols.Count; c++)
            {
                var h = cols[c].ToLowerInvariant().Trim();
                if (h.Contains("particular") || h.Contains("ledger") || h.Contains("account")) nameCol = c;
                else if (h.Contains("group") || h.Contains("parent")) groupCol = c;
                else if (h.Contains("opening")) opCol = c;
                else if (h.Contains("debit") || h.Equals("dr")) drCol = c;
                else if (h.Contains("credit") || h.Equals("cr")) crCol = c;
                else if (h.Contains("closing") || h.Contains("balance")) clCol = c;
            }

            if (drCol >= 0 || crCol >= 0 || clCol >= 0)
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex == -1) headerIndex = 0;

        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count <= nameCol) continue;

            var name = cols[nameCol].Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Skip total/summary rows
            var lower = name.ToLowerInvariant();
            if (lower.Equals("total") || lower.Contains("grand total") || lower.StartsWith("---"))
                continue;

            string group = groupCol >= 0 && groupCol < cols.Count ? cols[groupCol].Trim().Trim('"', '\'') : "";
            decimal opening = opCol >= 0 && opCol < cols.Count ? ParseTallyAmount(cols[opCol]) : 0m;
            decimal debit = drCol >= 0 && drCol < cols.Count ? ParseTallyAmount(cols[drCol]) : 0m;
            decimal credit = crCol >= 0 && crCol < cols.Count ? ParseTallyAmount(cols[crCol]) : 0m;
            decimal closing = clCol >= 0 && clCol < cols.Count ? ParseTallyAmount(cols[clCol]) : (debit != 0 ? debit : -credit);

            ledgers.Add(new ParsedTallyLedger(name, group, opening, debit, credit, closing));
        }

        if (ledgers.Count == 0)
            throw new ArgumentException("No readable ledger entries found in the CSV data.");

        return ComputeTotals(ledgers);
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

        bool isBalanced = Math.Abs(totalDebit - totalCredit) < 1.00m; // within rounding tolerance
        return new ParsedTrialBalanceResult(ledgers, totalDebit, totalCredit, isBalanced);
    }

    public static decimal ParseTallyAmount(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0m;
        var s = val.Trim();

        // Tally format check for "Cr" or "Dr"
        bool isCr = s.EndsWith("Cr", StringComparison.OrdinalIgnoreCase) || s.EndsWith("cr", StringComparison.OrdinalIgnoreCase);
        bool isDr = s.EndsWith("Dr", StringComparison.OrdinalIgnoreCase) || s.EndsWith("dr", StringComparison.OrdinalIgnoreCase);

        // Remove non-numeric characters except '.', '-', ','
        s = Regex.Replace(s, @"[^\d\.\-,]", "");
        s = s.Replace(",", "");

        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            if (isCr && result > 0) return -result;
            return result;
        }

        return 0m;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
