using System.Globalization;
using System.Text.RegularExpressions;
using CaDesk.Domain.BankFeeds;

namespace CaDesk.Application.BankFeeds;

public static class BankStatementParser
{
    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
        "yyyy-MM-dd", "dd/MM/yy", "dd-MM-yy", "d/M/yy",
        "dd-MMM-yyyy", "dd MMM yyyy", "dd/MMM/yyyy"
    ];

    public static ParsedStatementResult ParseCsv(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            throw new ArgumentException("CSV statement content cannot be empty.");

        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            throw new ArgumentException("CSV statement contains no readable rows.");

        int headerIndex = -1;
        int dateCol = -1;
        int narrCol = -1;
        int refCol = -1;
        int debitCol = -1;
        int creditCol = -1;
        int amountCol = -1;
        int typeCol = -1;
        int balanceCol = -1;

        for (int i = 0; i < Math.Min(lines.Length, 20); i++)
        {
            var cols = SplitCsvLine(lines[i]);
            var (d, n, r, dr, cr, amt, typ, bal) = IdentifyColumns(cols);
            if (d >= 0 && (dr >= 0 || cr >= 0 || amt >= 0))
            {
                headerIndex = i;
                dateCol = d;
                narrCol = n;
                refCol = r;
                debitCol = dr;
                creditCol = cr;
                amountCol = amt;
                typeCol = typ;
                balanceCol = bal;
                break;
            }
        }

        // If no recognizable header row, assume default layout: Date, Narration, Ref, Debit, Credit, Balance
        if (headerIndex == -1)
        {
            headerIndex = 0;
            dateCol = 0;
            narrCol = 1;
            refCol = 2;
            debitCol = 3;
            creditCol = 4;
            balanceCol = 5;
        }

        var items = new List<ParsedTransactionItem>();
        decimal runningBal = 0m;
        decimal? firstBal = null;
        decimal? lastBal = null;

        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            var rawLine = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            var cols = SplitCsvLine(rawLine);
            if (cols.Count <= dateCol) continue;

            var dateStr = cols[dateCol].Trim().Trim('"', '\'');
            if (!TryParseDate(dateStr, out var txDate))
                continue;

            string narration = narrCol >= 0 && narrCol < cols.Count ? cols[narrCol].Trim().Trim('"', '\'') : "Transaction";
            string? refNo = refCol >= 0 && refCol < cols.Count ? cols[refCol].Trim().Trim('"', '\'') : null;
            if (string.IsNullOrWhiteSpace(refNo)) refNo = null;

            decimal debit = 0m;
            decimal credit = 0m;

            if (debitCol >= 0 && debitCol < cols.Count)
                debit = ParseAmount(cols[debitCol]);
            if (creditCol >= 0 && creditCol < cols.Count)
                credit = ParseAmount(cols[creditCol]);

            if (debit == 0m && credit == 0m && amountCol >= 0 && amountCol < cols.Count)
            {
                var amt = ParseAmount(cols[amountCol]);
                if (typeCol >= 0 && typeCol < cols.Count)
                {
                    var typStr = cols[typeCol].Trim().ToUpperInvariant();
                    if (typStr.Contains("CR") || typStr.Contains("DEPOSIT") || typStr.Contains("CREDIT"))
                        credit = Math.Abs(amt);
                    else
                        debit = Math.Abs(amt);
                }
                else
                {
                    if (amt < 0) debit = Math.Abs(amt);
                    else credit = amt;
                }
            }

            if (debit == 0m && credit == 0m)
                continue;

            decimal? rowBal = null;
            if (balanceCol >= 0 && balanceCol < cols.Count)
            {
                var balVal = ParseAmount(cols[balanceCol]);
                if (balVal != 0m || !string.IsNullOrWhiteSpace(cols[balanceCol]))
                    rowBal = balVal;
            }

            string txType = debit > 0m ? BankTransactionType.Debit : BankTransactionType.Credit;
            decimal finalAmt = debit > 0m ? debit : credit;

            if (rowBal.HasValue)
            {
                firstBal ??= rowBal.Value;
                lastBal = rowBal.Value;
                runningBal = rowBal.Value;
            }
            else
            {
                if (txType == BankTransactionType.Credit) runningBal += finalAmt;
                else runningBal -= finalAmt;
                rowBal = runningBal;
            }

            items.Add(new ParsedTransactionItem(
                Date: txDate,
                ValueDate: txDate,
                Narration: narration,
                ChqRefNo: refNo,
                Type: txType,
                Amount: finalAmt,
                Balance: rowBal));
        }

        if (items.Count == 0)
            throw new ArgumentException("No valid transaction rows found in the provided CSV statement.");

        var orderedItems = items.OrderBy(x => x.Date).ToList();
        var startDate = orderedItems.First().Date;
        var endDate = orderedItems.Last().Date;

        var totalDebits = items.Where(x => x.Type == BankTransactionType.Debit).Sum(x => x.Amount);
        var totalCredits = items.Where(x => x.Type == BankTransactionType.Credit).Sum(x => x.Amount);

        decimal openBal = firstBal ?? 0m;
        decimal closeBal = lastBal ?? (openBal + totalCredits - totalDebits);

        return new ParsedStatementResult(
            StartDate: startDate,
            EndDate: endDate,
            OpeningBalance: openBal,
            ClosingBalance: closeBal,
            TotalDebits: totalDebits,
            TotalCredits: totalCredits,
            Items: items);
    }

    private static (int date, int narr, int refNo, int debit, int credit, int amt, int type, int bal) IdentifyColumns(List<string> cols)
    {
        int date = -1, narr = -1, refNo = -1, debit = -1, credit = -1, amt = -1, type = -1, bal = -1;

        for (int i = 0; i < cols.Count; i++)
        {
            var h = cols[i].Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
            if (date == -1 && (h.Contains("date") || h.Contains("txn date") || h.Contains("value date")))
                date = i;
            else if (narr == -1 && (h.Contains("narration") || h.Contains("description") || h.Contains("particulars") || h.Contains("remark") || h.Contains("details")))
                narr = i;
            else if (refNo == -1 && (h.Contains("chq") || h.Contains("cheque") || h.Contains("ref") || h.Contains("utr") || h.Contains("reference")))
                refNo = i;
            else if (debit == -1 && (h.Contains("withdrawal") || h.Contains("debit") || h == "dr" || h.Contains("dr amount")))
                debit = i;
            else if (credit == -1 && (h.Contains("deposit") || h.Contains("credit") || h == "cr" || h.Contains("cr amount")))
                credit = i;
            else if (amt == -1 && (h.Contains("amount") || h == "txn amt"))
                amt = i;
            else if (type == -1 && (h.Contains("type") || h == "dr/cr" || h == "cr/dr"))
                type = i;
            else if (bal == -1 && (h.Contains("balance") || h.Contains("closing") || h.Contains("bal")))
                bal = i;
        }

        return (date, narr, refNo, debit, credit, amt, type, bal);
    }

    private static bool TryParseDate(string s, out DateOnly date)
    {
        s = s.Trim();
        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
        {
            date = DateOnly.FromDateTime(dt2);
            return true;
        }

        date = default;
        return false;
    }

    public static decimal ParseAmount(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0m;
        var s = val.Trim().Replace(",", "").Replace(" ", "").Replace("₹", "").Replace("$", "").Replace("Rs.", "", StringComparison.OrdinalIgnoreCase);

        bool isNeg = false;
        if (s.StartsWith('(') && s.EndsWith(')'))
        {
            isNeg = true;
            s = s[1..^1];
        }
        else if (s.EndsWith("Dr", StringComparison.OrdinalIgnoreCase))
        {
            isNeg = true;
            s = s[..^2].Trim();
        }
        else if (s.EndsWith("Cr", StringComparison.OrdinalIgnoreCase))
        {
            s = s[..^2].Trim();
        }

        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return isNeg ? -Math.Abs(result) : result;

        return 0m;
    }

    public static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
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
