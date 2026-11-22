using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CaDesk.Domain.BankFeeds;

namespace CaDesk.Application.BankFeeds;

public static class PdfBankStatementParser
{
    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
        "yyyy-MM-dd", "dd/MM/yy", "dd-MM-yy", "d/M/yy",
        "dd-MMM-yyyy", "dd MMM yyyy", "dd/MMM/yyyy",
        "dd-MMM-yy", "dd MMM yy"
    ];

    private static readonly Regex DateRegex = new(
        @"\b(?:\d{1,2}[-/.](?:\d{1,2}|[A-Za-z]{3})[-/.]\d{2,4})\b",
        RegexOptions.Compiled);

    private static readonly Regex AmountRegex = new(
        @"(?:(?<=[^\w\d]|^))(?:\d{1,3}(?:,\d{2,3})*(?:\.\d{1,2})|\d+\.\d{2})(?:\s*(?:Dr|Cr|DR|CR))?(?=[^\w\d]|$)",
        RegexOptions.Compiled);

    public static ParsedStatementResult Parse(byte[] pdfBytes)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
            throw new ArgumentException("PDF file content cannot be empty.");

        string text;
        if (IsPdfBinary(pdfBytes))
        {
            text = ExtractTextFromPdf(pdfBytes);
        }
        else
        {
            text = Encoding.UTF8.GetString(pdfBytes);
        }

        return ParseExtractedText(text);
    }

    public static ParsedStatementResult Parse(string textOrBase64)
    {
        if (string.IsNullOrWhiteSpace(textOrBase64))
            throw new ArgumentException("Bank statement content cannot be empty.");

        var trimmed = textOrBase64.Trim();
        if (trimmed.StartsWith("JVBERi0", StringComparison.Ordinal) ||
            trimmed.StartsWith("%PDF", StringComparison.Ordinal) ||
            trimmed.Length > 200 && IsLikelyBase64(trimmed))
        {
            try
            {
                byte[] bytes = trimmed.StartsWith("%PDF", StringComparison.Ordinal)
                    ? Encoding.Latin1.GetBytes(trimmed)
                    : Convert.FromBase64String(trimmed);
                return Parse(bytes);
            }
            catch (Exception ex2) when (ex2 is not ArgumentException)
            {
                // Fall back to text parsing if base64 decode fails
            }
        }

        return ParseExtractedText(trimmed);
    }

    // A compressed stream can expand ~1000x ("zip bomb"): cap what one stream and one file may decompress to.
    private const int MaxDecompressedCharsPerStream = 20_000_000;
    private const long MaxDecompressedCharsPerFile = 100_000_000;

    private static string ReadBounded(Stream decompressed)
    {
        using var reader = new StreamReader(decompressed, Encoding.Latin1);
        var buffer = new char[81920];
        var sb = new StringBuilder();
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (sb.Length + read > MaxDecompressedCharsPerStream)
                throw new ArgumentException("A compressed section of this PDF is too large to be a bank statement, so it was not processed.");
            sb.Append(buffer, 0, read);
        }
        return sb.ToString();
    }

    public static string ExtractTextFromPdf(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        int index = 0;
        long totalDecompressed = 0;

        // Extract streams
        while (index < pdfBytes.Length)
        {
            int streamStartTag = FindSequence(pdfBytes, "stream"u8, index);
            if (streamStartTag == -1) break;

            int headerStart = Math.Max(0, streamStartTag - 300);
            string header = Encoding.Latin1.GetString(pdfBytes, headerStart, streamStartTag - headerStart);
            bool isFlate = header.Contains("/FlateDecode") || header.Contains("/Fl");

            int contentStart = streamStartTag + 6;
            if (contentStart < pdfBytes.Length && pdfBytes[contentStart] == '\r') contentStart++;
            if (contentStart < pdfBytes.Length && pdfBytes[contentStart] == '\n') contentStart++;

            int streamEndTag = FindSequence(pdfBytes, "endstream"u8, contentStart);
            if (streamEndTag == -1) break;

            int length = streamEndTag - contentStart;
            if (length > 0 && contentStart + length <= pdfBytes.Length)
            {
                byte[] streamData = new byte[length];
                Buffer.BlockCopy(pdfBytes, contentStart, streamData, 0, length);

                string streamText = string.Empty;
                if (isFlate)
                {
                    try
                    {
                        using var ms = new MemoryStream(streamData);
                        using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
                        streamText = ReadBounded(zlib);
                    }
                    catch (Exception ex1) when (ex1 is not ArgumentException) // size-limit errors must reach the caller
                    {
                        try
                        {
                            // Strip 2-byte zlib header if raw deflate was used
                            int skip = (streamData.Length > 2 && streamData[0] == 0x78) ? 2 : 0;
                            using var ms = new MemoryStream(streamData, skip, streamData.Length - skip);
                            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                            streamText = ReadBounded(deflate);
                        }
                        catch (Exception ex2) when (ex2 is not ArgumentException)
                        {
                            streamText = Encoding.Latin1.GetString(streamData);
                        }
                    }
                }
                else
                {
                    streamText = Encoding.Latin1.GetString(streamData);
                }

                totalDecompressed += streamText.Length;
                if (totalDecompressed > MaxDecompressedCharsPerFile)
                    throw new ArgumentException("This PDF expands to more content than a bank statement can have, so it was not processed.");

                if (!string.IsNullOrWhiteSpace(streamText))
                {
                    var extracted = ExtractTextFromPdfCommands(streamText);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        sb.AppendLine(extracted);
                    }
                }
            }

            index = streamEndTag + 9;
        }

        // If stream extraction produced minimal text, scan entire file for literals
        if (sb.Length < 50)
        {
            string raw = Encoding.Latin1.GetString(pdfBytes);
            sb.AppendLine(ExtractTextFromPdfCommands(raw));
        }

        return sb.ToString();
    }

    private static string ExtractTextFromPdfCommands(string streamContent)
    {
        var sb = new StringBuilder();
        int i = 0;
        int len = streamContent.Length;

        while (i < len)
        {
            char c = streamContent[i];

            if (c == '(')
            {
                var (str, nextPos) = ParsePdfString(streamContent, i);
                i = nextPos;

                int opScan = i;
                while (opScan < len && char.IsWhiteSpace(streamContent[opScan])) opScan++;
                if (opScan + 2 <= len && (streamContent.Substring(opScan, 2) == "Tj" || streamContent[opScan] == '\'' || streamContent[opScan] == '"'))
                {
                    sb.Append(str).Append(' ');
                }
                else
                {
                    sb.Append(str).Append(' ');
                }
            }
            else if (c == '[')
            {
                // Array, e.g. [(Hello) -100 (World)] TJ
                int endArr = streamContent.IndexOf(']', i);
                if (endArr > i)
                {
                    string arrBody = streamContent.Substring(i + 1, endArr - i - 1);
                    int afterArr = endArr + 1;
                    while (afterArr < len && char.IsWhiteSpace(streamContent[afterArr])) afterArr++;
                    if (afterArr + 2 <= len && streamContent.Substring(afterArr, 2) == "TJ")
                    {
                        var arrText = ExtractStringsFromArray(arrBody);
                        sb.Append(arrText).Append(' ');
                        i = afterArr + 2;
                        continue;
                    }
                }
                i++;
            }
            else if (c == 'T' && i + 1 < len && (streamContent[i + 1] == '*' || streamContent[i + 1] == 'd' || streamContent[i + 1] == 'D' || streamContent[i + 1] == 'm'))
            {
                sb.AppendLine();
                i += 2;
            }
            else if (c == '\r' || c == '\n')
            {
                sb.AppendLine();
                i++;
            }
            else
            {
                i++;
            }
        }

        return sb.ToString();
    }

    private static (string text, int nextPos) ParsePdfString(string s, int start)
    {
        var sb = new StringBuilder();
        int depth = 0;
        int i = start;

        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char next = s[i + 1];
                switch (next)
                {
                    case 'n': sb.Append('\n'); i += 2; continue;
                    case 'r': sb.Append('\r'); i += 2; continue;
                    case 't': sb.Append('\t'); i += 2; continue;
                    case 'b': sb.Append('\b'); i += 2; continue;
                    case 'f': sb.Append('\f'); i += 2; continue;
                    case '(': sb.Append('('); i += 2; continue;
                    case ')': sb.Append(')'); i += 2; continue;
                    case '\\': sb.Append('\\'); i += 2; continue;
                    default:
                        if (next >= '0' && next <= '7')
                        {
                            // Octal
                            int octLen = 1;
                            while (octLen < 3 && i + 1 + octLen < s.Length && s[i + 1 + octLen] >= '0' && s[i + 1 + octLen] <= '7')
                                octLen++;
                            string octStr = s.Substring(i + 1, octLen);
                            sb.Append((char)Convert.ToInt32(octStr, 8));
                            i += 1 + octLen;
                            continue;
                        }
                        sb.Append(next);
                        i += 2;
                        continue;
                }
            }
            else if (c == '(')
            {
                if (depth > 0) sb.Append('(');
                depth++;
                i++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    i++;
                    break;
                }
                sb.Append(')');
                i++;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return (sb.ToString(), i);
    }

    private static string ExtractStringsFromArray(string arrayContent)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < arrayContent.Length)
        {
            if (arrayContent[i] == '(')
            {
                var (text, next) = ParsePdfString(arrayContent, i);
                sb.Append(text);
                i = next;
            }
            else if (char.IsDigit(arrayContent[i]) || arrayContent[i] == '-')
            {
                int numStart = i;
                while (i < arrayContent.Length && (char.IsDigit(arrayContent[i]) || arrayContent[i] == '-' || arrayContent[i] == '.'))
                    i++;
                if (float.TryParse(arrayContent.Substring(numStart, i - numStart), NumberStyles.Any, CultureInfo.InvariantCulture, out float displacement))
                {
                    // Negative displacement > 100 often marks a word space
                    if (displacement < -120)
                    {
                        sb.Append(' ');
                    }
                }
            }
            else
            {
                i++;
            }
        }
        return sb.ToString();
    }

    public static ParsedStatementResult ParseExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Bank statement contains no readable text content.");

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0)
            throw new ArgumentException("Extracted statement text contains no rows.");

        var items = new List<ParsedTransactionItem>();
        decimal runningBalance = 0m;
        decimal? firstBalance = null;
        decimal? lastBalance = null;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            // Ignore page header/footer boilerplates
            if (IsIgnoredLine(line)) continue;

            // Find dates in this line
            var dateMatch = DateRegex.Match(line);
            if (!dateMatch.Success) continue;

            if (!TryParseDate(dateMatch.Value, out var txDate))
                continue;

            // Extract all numeric currency tokens from the line
            var amountMatches = AmountRegex.Matches(line).Cast<Match>().ToList();
            if (amountMatches.Count == 0) continue;

            // Check for opening balance rows
            if (line.Contains("OPENING BALANCE", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("BROUGHT FORWARD", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("BAL B/F", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("BALANCE B/F", StringComparison.OrdinalIgnoreCase))
            {
                var balMatch = amountMatches.LastOrDefault();
                if (balMatch != null)
                {
                    decimal ob = BankStatementParser.ParseAmount(balMatch.Value);
                    if (ob != 0m)
                    {
                        firstBalance ??= ob;
                        runningBalance = ob;
                    }
                }
                continue;
            }

            // Look ahead for continuation of narration or amounts on next line
            string combinedLine = line;
            if (amountMatches.Count < 2 && i + 1 < lines.Count && !DateRegex.IsMatch(lines[i + 1]))
            {
                combinedLine = line + " " + lines[i + 1];
                amountMatches = AmountRegex.Matches(combinedLine).Cast<Match>().ToList();
            }

            // Extract Ref No / UTR / Cheque No
            string? refNo = ExtractReferenceNumber(combinedLine);

            // Determine Debit, Credit, and Balance amounts
            var (debit, credit, balance, narration) = ParseRowAmounts(combinedLine, dateMatch, amountMatches);

            if (balance.HasValue && runningBalance != 0m)
            {
                var diff = balance.Value - runningBalance;
                if (diff > 0m)
                {
                    credit = diff;
                    debit = 0m;
                }
                else if (diff < 0m)
                {
                    debit = Math.Abs(diff);
                    credit = 0m;
                }
            }

            if (debit == 0m && credit == 0m)
                continue;

            string txType = debit > 0m ? BankTransactionType.Debit : BankTransactionType.Credit;
            decimal txAmount = debit > 0m ? debit : credit;

            if (balance.HasValue)
            {
                firstBalance ??= balance.Value;
                lastBalance = balance.Value;
                runningBalance = balance.Value;
            }
            else
            {
                if (txType == BankTransactionType.Credit) runningBalance += txAmount;
                else runningBalance -= txAmount;
                balance = runningBalance;
            }

            if (string.IsNullOrWhiteSpace(narration))
                narration = "Bank Transaction";

            items.Add(new ParsedTransactionItem(
                Date: txDate,
                ValueDate: txDate,
                Narration: narration.Trim(),
                ChqRefNo: refNo,
                Type: txType,
                Amount: txAmount,
                Balance: balance
            ));
        }

        if (items.Count == 0)
        {
            // If line-by-line tabular parsing found nothing, fallback to CSV split parser
            try
            {
                return BankStatementParser.ParseCsv(text);
            }
            catch
            {
                throw new ArgumentException("Unable to extract transactions from the provided PDF bank statement. Ensure the document is an unencrypted bank statement with transaction text.");
            }
        }

        var ordered = items.OrderBy(x => x.Date).ToList();
        var startDate = ordered.First().Date;
        var endDate = ordered.Last().Date;

        var totalDebits = items.Where(x => x.Type == BankTransactionType.Debit).Sum(x => x.Amount);
        var totalCredits = items.Where(x => x.Type == BankTransactionType.Credit).Sum(x => x.Amount);

        decimal openBal = firstBalance ?? 0m;
        decimal closeBal = lastBalance ?? (openBal + totalCredits - totalDebits);

        return new ParsedStatementResult(
            StartDate: startDate,
            EndDate: endDate,
            OpeningBalance: openBal,
            ClosingBalance: closeBal,
            TotalDebits: totalDebits,
            TotalCredits: totalCredits,
            Items: items
        );
    }

    private static (decimal debit, decimal credit, decimal? balance, string narration) ParseRowAmounts(
        string line,
        Match dateMatch,
        List<Match> amountMatches)
    {
        decimal debit = 0m;
        decimal credit = 0m;
        decimal? balance = null;

        var amounts = new List<(decimal val, int index, int length, string raw)>();
        foreach (var m in amountMatches)
        {
            decimal parsed = BankStatementParser.ParseAmount(m.Value);
            if (parsed != 0m)
            {
                amounts.Add((parsed, m.Index, m.Length, m.Value));
            }
        }

        if (amounts.Count == 1)
        {
            // Single amount - check for Dr/Cr markers in the string
            var single = amounts[0];
            bool isCredit = line.Contains(" CR", StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("Cr", StringComparison.Ordinal) ||
                           line.Contains("Deposit", StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("CREDIT", StringComparison.OrdinalIgnoreCase);

            if (isCredit) credit = Math.Abs(single.val);
            else debit = Math.Abs(single.val);
        }
        else if (amounts.Count == 2)
        {
            // Two amounts: typically [TxnAmount, Balance] or [Debit, Credit]
            var a1 = amounts[0];
            var a2 = amounts[1];

            bool isCredit = line.Contains(" CR", StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("Cr", StringComparison.Ordinal) ||
                           line.Contains("Deposit", StringComparison.OrdinalIgnoreCase);

            if (isCredit) credit = Math.Abs(a1.val);
            else debit = Math.Abs(a1.val);
            balance = a2.val;
        }
        else if (amounts.Count >= 3)
        {
            // Three amounts: [Debit, Credit, Balance]
            var d = amounts[0].val;
            var c = amounts[1].val;
            var b = amounts[2].val;

            if (d > 0m && c == 0m)
            {
                debit = d;
            }
            else if (c > 0m && d == 0m)
            {
                credit = c;
            }
            else
            {
                // Both non-zero: check column headers or position
                debit = d;
                credit = c;
            }
            balance = b;
        }

        // Construct narration by stripping date and amounts
        int textStart = dateMatch.Index + dateMatch.Length;
        int textEnd = amounts.Count > 0 ? amounts[0].index : line.Length;
        string narration;
        if (textEnd > textStart)
        {
            narration = line.Substring(textStart, textEnd - textStart).Trim();
        }
        else
        {
            narration = line;
            narration = narration.Replace(dateMatch.Value, " ");
            foreach (var a in amounts)
            {
                narration = narration.Replace(a.raw, " ");
            }
        }

        narration = Regex.Replace(narration, @"\s+", " ").Trim();
        return (debit, credit, balance, narration);
    }

    private static string? ExtractReferenceNumber(string line)
    {
        var m = Regex.Match(line, @"\b(?:UPI|NEFT|IMPS|RTGS|CHQ|CHEQUE|REF|UTR)[-/: ]+([A-Za-z0-9]+)\b", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return m.Groups[0].Value;
        }

        var mNum = Regex.Match(line, @"\b\d{6,16}\b");
        if (mNum.Success)
        {
            return mNum.Value;
        }

        return null;
    }

    private static bool IsPdfBinary(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        return bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46; // %PDF
    }

    private static bool IsLikelyBase64(string s)
    {
        if (s.Length % 4 != 0) return false;
        int checkLen = Math.Min(s.Length, 100);
        for (int i = 0; i < checkLen; i++)
        {
            char c = s[i];
            if (!char.IsLetterOrDigit(c) && c != '+' && c != '/' && c != '=')
                return false;
        }
        return true;
    }

    private static bool IsIgnoredLine(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.Contains("page ") ||
               lower.Contains("account statement") ||
               lower.Contains("statement of account") ||
               lower.Contains("branch address") ||
               lower.Contains("customer care") ||
               lower.Contains("generated on") ||
               lower.Contains("transaction date") && lower.Contains("balance");
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

    private static int FindSequence(byte[] source, ReadOnlySpan<byte> pattern, int startIndex)
    {
        for (int i = startIndex; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
}
