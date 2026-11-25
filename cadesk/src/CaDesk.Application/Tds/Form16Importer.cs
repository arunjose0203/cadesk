using System.Globalization;
using System.Text.RegularExpressions;
using CaDesk.Domain.Tds;

namespace CaDesk.Application.Tds;

/// <summary>
/// Parses Form 16 / Form 16A data from:
/// 1) NSDL / Protean RPU .txt files (FH^BH^CD^DD across both 24Q/26Q/27Q and Act 2025 Forms 138/140/144),
/// 2) CSV registers matching CaDesk's Form 16 / 16A template, or
/// 3) Pasted TRACES Form 16 / Form 16A text exports (Certificate No., TAN, PAN, Section, Quarter, Challan, Amounts).
/// </summary>
public static partial class Form16Importer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [GeneratedRegex(@"[A-Z]{4}[0-9]{5}[A-Z]")]
    private static partial Regex TanMatch();

    [GeneratedRegex(@"[A-Z]{5}[0-9]{4}[A-Z]")]
    private static partial Regex PanMatch();

    [GeneratedRegex(@"(?<!\d)([0-9]{7})(?!\d)")]
    private static partial Regex BsrMatch();

    public static ImportForm16Result Parse(
        string content,
        string? defaultFormType = null,
        string? defaultFinancialYear = null,
        string? defaultQuarter = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ImportForm16Result("unknown", null, null, null, null, null, [], [], [], ["Import content is empty."], 0, 0, false);

        var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count > 0 && lines.Any(l => l.Contains("^FH^") || l.Contains("^BH^") || l.Contains("^CD^") || l.Contains("^DD^")))
            return ParseNsdlTxt(lines);

        if (lines.Count > 1 && lines[0].Contains(',') &&
            (lines[0].Contains("PAN", StringComparison.OrdinalIgnoreCase) || lines[0].Contains("Amount", StringComparison.OrdinalIgnoreCase)))
            return ParseCsv(lines, defaultFormType, defaultFinancialYear, defaultQuarter);

        return ParseTracesCertificateText(content, defaultFormType, defaultFinancialYear, defaultQuarter);
    }

    private static ImportForm16Result ParseNsdlTxt(List<string> lines)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        string? deductorTan = null, deductorName = null, fy = null, quarter = null, formType = null;
        var challans = new List<ImportedTdsChallanRow>();
        var deductions = new List<ImportedTdsDeductionRow>();
        var challanMap = new Dictionary<int, ImportedTdsChallanRow>();
        var isAct2025Layout = false;

        foreach (var raw in lines)
        {
            var c = raw.Split('^');
            var recType = c.ElementAtOrDefault(1)?.Trim().ToUpperInvariant();
            if (recType == "FH")
            {
                deductorTan ??= c.ElementAtOrDefault(7)?.Trim().ToUpperInvariant();
            }
            else if (recType == "BH")
            {
                var rawForm = c.ElementAtOrDefault(4)?.Trim().ToUpperInvariant() ?? "26Q";
                formType = rawForm switch { "138" => "24Q", "140" => "26Q", "144" => "27Q", "143" => "27EQ", _ => rawForm };
                isAct2025Layout = rawForm is "138" or "140" or "144" or "143";
                deductorTan = c.ElementAtOrDefault(12)?.Trim().ToUpperInvariant() is { Length: > 0 } t ? t : deductorTan;
                var rawFy = c.ElementAtOrDefault(16)?.Trim() ?? "";
                if (rawFy.Length == 6 && int.TryParse(rawFy[..4], out var sy))
                    fy = $"{sy:D4}-{(sy + 1) % 100:D2}";
                quarter = c.ElementAtOrDefault(17)?.Trim().ToUpperInvariant();
                deductorName = c.ElementAtOrDefault(18)?.Trim();
            }
            else if (recType == "CD")
            {
                var idx = int.TryParse(c.ElementAtOrDefault(3), out var ci) ? ci : challanMap.Count + 1;
                var isNil = string.Equals(c.ElementAtOrDefault(5), "Y", StringComparison.OrdinalIgnoreCase);

                ImportedTdsChallanRow ch;
                if (isAct2025Layout || c.Length <= 32)
                {
                    var bsr = (c.ElementAtOrDefault(14) ?? "").Trim();
                    var serial = (c.ElementAtOrDefault(16) ?? "").Trim();
                    var dt = ParseDdmmyyyy(c.ElementAtOrDefault(18)) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                    var tax = ParseDecimal(c.ElementAtOrDefault(7));
                    var intr = ParseDecimal(c.ElementAtOrDefault(8));
                    var fee = ParseDecimal(c.ElementAtOrDefault(9));
                    var tot = ParseDecimal(c.ElementAtOrDefault(11));
                    var minor = c.ElementAtOrDefault(22)?.Trim() is { Length: > 0 } m ? m : "200";
                    var book = string.Equals(c.ElementAtOrDefault(12), "B", StringComparison.OrdinalIgnoreCase);
                    ch = new(bsr, serial.PadLeft(5, '0'), dt, NormalizeMinor(minor), tax, intr, fee, tot > 0 ? tot : tax + intr + fee, book, false);
                }
                else
                {
                    var book = string.Equals(c.ElementAtOrDefault(36), "Y", StringComparison.OrdinalIgnoreCase);
                    var bsr = (c.ElementAtOrDefault(15) ?? "").Trim();
                    var serial = (book ? c.ElementAtOrDefault(13) : c.ElementAtOrDefault(11))?.Trim() ?? "";
                    var dt = ParseDdmmyyyy(c.ElementAtOrDefault(17)) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                    var tax = ParseDecimal(c.ElementAtOrDefault(21)) + ParseDecimal(c.ElementAtOrDefault(22)) + ParseDecimal(c.ElementAtOrDefault(23));
                    var intr = ParseDecimal(c.ElementAtOrDefault(24));
                    var fee = ParseDecimal(c.ElementAtOrDefault(38));
                    var tot = ParseDecimal(c.ElementAtOrDefault(26));
                    var minor = c.ElementAtOrDefault(39)?.Trim() is { Length: > 0 } m ? m : "200";
                    ch = new(bsr, serial.PadLeft(5, '0'), dt, NormalizeMinor(minor), tax, intr, fee, tot > 0 ? tot : tax + intr + fee, book, false);
                }

                if (!isNil)
                {
                    challans.Add(ch);
                    challanMap[idx] = ch;
                }
            }
            else if (recType == "DD")
            {
                ImportedTdsChallanRow? linked = null;
                int chRef;
                string pan, name, secCode;
                DateOnly payDate, dedDate;
                decimal amt, rate, tds;
                string? certNo;

                if (isAct2025Layout || c.Length <= 47)
                {
                    chRef = int.TryParse(c.ElementAtOrDefault(4), out var r5) ? r5 : 1;
                    pan = (c.ElementAtOrDefault(7) ?? "").Trim().ToUpperInvariant();
                    name = (c.ElementAtOrDefault(8) ?? "Deductee").Trim();
                    secCode = (c.ElementAtOrDefault(14) ?? "").Trim();
                    payDate = ParseDdmmyyyy(c.ElementAtOrDefault(18)) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                    amt = ParseDecimal(c.ElementAtOrDefault(19));
                    tds = ParseDecimal(c.ElementAtOrDefault(23));
                    dedDate = ParseDdmmyyyy(c.ElementAtOrDefault(26)) ?? payDate;
                    rate = ParseDecimal(c.ElementAtOrDefault(27));
                    certNo = c.ElementAtOrDefault(32)?.Trim();
                }
                else
                {
                    chRef = int.TryParse(c.ElementAtOrDefault(3), out var r4) ? r4 : 1;
                    pan = (c.ElementAtOrDefault(9) ?? "").Trim().ToUpperInvariant();
                    name = (c.ElementAtOrDefault(12) ?? "Deductee").Trim();
                    tds = ParseDecimal(c.ElementAtOrDefault(16));
                    amt = ParseDecimal(c.ElementAtOrDefault(21));
                    payDate = ParseDdmmyyyy(c.ElementAtOrDefault(22)) ?? DateOnly.FromDateTime(DateTime.UtcNow);
                    dedDate = ParseDdmmyyyy(c.ElementAtOrDefault(23)) ?? payDate;
                    rate = ParseDecimal(c.ElementAtOrDefault(25));
                    secCode = (c.ElementAtOrDefault(32) ?? "").Trim();
                    certNo = c.ElementAtOrDefault(33)?.Trim();
                }

                if (rate == 0m && amt > 0m && tds > 0m)
                    rate = Math.Round(tds * 100m / amt, 2);

                challanMap.TryGetValue(chRef, out linked);
                deductions.Add(new ImportedTdsDeductionRow(
                    FormType: formType ?? "26Q",
                    Quarter: quarter ?? "Q1",
                    FinancialYear: fy ?? "2026-27",
                    DeducteePan: string.IsNullOrWhiteSpace(pan) ? "PANNOTAVBL" : pan,
                    DeducteeName: name,
                    Section: MapImportedSectionToCaDesk(secCode, formType ?? "26Q"),
                    PaymentDate: payDate,
                    DeductionDate: dedDate,
                    AmountPaid: amt,
                    Rate: rate,
                    TdsAmount: tds,
                    CertificateNo: string.IsNullOrWhiteSpace(certNo) ? null : certNo,
                    ChallanBsr: linked?.BsrCode,
                    ChallanSerial: linked?.ChallanSerialNumber,
                    ChallanDate: linked?.ChallanDate
                ));
            }
        }

        if (deductions.Count == 0 && challans.Count == 0)
            errors.Add("No BH/CD/DD records could be parsed from the NSDL RPU text file.");

        return new ImportForm16Result("nsdl_rpu_txt", deductorTan, deductorName, fy, quarter, formType, challans, deductions, warnings, errors, 0, 0, false);
    }

    private static ImportForm16Result ParseCsv(List<string> lines, string? defaultForm, string? defaultFy, string? defaultQtr)
    {
        var headers = SplitCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names)
        {
            foreach (var n in names)
            {
                var idx = headers.FindIndex(h => h.Contains(n, StringComparison.Ordinal));
                if (idx >= 0) return idx;
            }
            return -1;
        }

        var iForm = Col("form");
        var iFy = Col("fy", "financial year", "tax year");
        var iQtr = Col("quarter", "qtr");
        var iPan = Col("deductee pan", "employee pan", "pan");
        var iName = Col("deductee name", "employee name", "party name", "name");
        var iSec = Col("section");
        var iPayDt = Col("payment date", "date of payment", "credit date");
        var iDedDt = Col("deduction date");
        var iAmt = Col("amount paid", "gross", "amount");
        var iRate = Col("rate");
        var iTds = Col("tds amount", "tax deducted", "tds");
        var iCert = Col("certificate");
        var iBsr = Col("bsr");
        var iSerial = Col("challan serial", "challan no", "serial");
        var iChalDt = Col("challan date", "deposit date");

        if (iPan < 0 || iAmt < 0 || iTds < 0)
        {
            return new ImportForm16Result("form16a_csv", null, null, defaultFy, defaultQtr, defaultForm, [], [], [],
                ["CSV must contain columns for Deductee PAN, Amount Paid/Credited, and TDS/Tax Deducted."], 0, 0, false);
        }

        var deds = new List<ImportedTdsDeductionRow>();
        var challanAgg = new Dictionary<(string Bsr, string Serial, DateOnly Date), (decimal Tax, string Sec, string Fy, string Qtr)>();
        var warnings = new List<string>();
        var errors = new List<string>();

        for (var r = 1; r < lines.Count; r++)
        {
            var cols = SplitCsvLine(lines[r]);
            if (cols.All(string.IsNullOrWhiteSpace)) continue;
            string Get(int idx) => idx >= 0 && idx < cols.Count ? cols[idx].Trim() : "";

            var form = Get(iForm) is { Length: > 0 } fv ? fv.ToUpperInvariant() : (defaultForm ?? "26Q");
            var fy = Get(iFy) is { Length: > 0 } yv ? yv : (defaultFy ?? "2026-27");
            var qtr = Get(iQtr) is { Length: > 0 } qv ? qv.ToUpperInvariant() : (defaultQtr ?? "Q1");
            var pan = Get(iPan).ToUpperInvariant();
            if (string.IsNullOrEmpty(pan)) pan = "PANNOTAVBL";
            var name = Get(iName) is { Length: > 0 } nv ? nv : "Deductee";
            var sec = MapImportedSectionToCaDesk(Get(iSec), form);
            var amt = ParseDecimal(Get(iAmt));
            var tds = ParseDecimal(Get(iTds));
            var rate = ParseDecimal(Get(iRate));
            if (rate == 0m && amt > 0m && tds > 0m) rate = Math.Round(tds * 100m / amt, 2);

            var payDt = ParseFlexibleDate(Get(iPayDt)) ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var dedDt = ParseFlexibleDate(Get(iDedDt)) ?? payDt;
            var cert = Get(iCert) is { Length: > 0 } cv ? cv : null;
            var bsr = Get(iBsr);
            var serial = Get(iSerial);
            var chalDt = ParseFlexibleDate(Get(iChalDt));

            if (amt <= 0m)
            {
                errors.Add($"Row {r + 1}: Amount paid must be greater than zero.");
                continue;
            }

            string? normBsr = bsr.Length == 7 ? bsr : null;
            string? normSerial = serial.Length is >= 1 and <= 5 ? serial.PadLeft(5, '0') : null;
            if (!string.IsNullOrEmpty(bsr) && normBsr is null)
                warnings.Add($"Row {r + 1}: BSR code '{bsr}' ignored because it is not 7 digits.");

            if (normBsr is not null && normSerial is not null)
            {
                var cDt = chalDt ?? dedDt;
                var key = (normBsr, normSerial, cDt);
                challanAgg[key] = challanAgg.TryGetValue(key, out var cur)
                    ? (cur.Tax + tds, sec, fy, qtr)
                    : (tds, sec, fy, qtr);
                chalDt = cDt;
            }

            deds.Add(new ImportedTdsDeductionRow(form, qtr, fy, pan, name, sec, payDt, dedDt, amt, rate, tds, cert, normBsr, normSerial, chalDt));
        }

        var challans = challanAgg.Select(kv => new ImportedTdsChallanRow(
            kv.Key.Bsr, kv.Key.Serial, kv.Key.Date, TdsMinorHead.Regular200,
            Math.Ceiling(kv.Value.Tax), 0m, 0m, Math.Ceiling(kv.Value.Tax), false, false)).ToList();

        return new ImportForm16Result("form16a_csv", null, null, defaultFy, defaultQtr, defaultForm, challans, deds, warnings, errors, 0, 0, false);
    }

    private static ImportForm16Result ParseTracesCertificateText(string text, string? defaultForm, string? defaultFy, string? defaultQtr)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        var upper = text.ToUpperInvariant();
        var isForm16Salary = upper.Contains("FORM NO. 16") || upper.Contains("FORM 16 ") || upper.Contains("FORM NO 16") || (upper.Contains("SALARY") && !upper.Contains("16A"));
        var form = defaultForm ?? (isForm16Salary ? "24Q" : "26Q");

        var certMatch = Regex.Match(text, @"Certificate\s*(?:No\.?|Number)\s*[:\-]?\s*([A-Z0-9]{6,15})", RegexOptions.IgnoreCase);
        var certNo = certMatch.Success ? certMatch.Groups[1].Value.ToUpperInvariant() : null;

        var tan = TanMatch().Match(upper) is { Success: true } tm ? tm.Value : null;
        var pans = PanMatch().Matches(upper).Select(m => m.Value).Distinct().ToList();
        // Exclude TAN-embedded substrings or pick deductee PAN when labelled
        var labelledPan = Regex.Match(text, @"PAN\s+of\s+(?:the\s+)?(?:Employee|Deductee)\s*[:\-]?\s*([A-Z]{5}[0-9]{4}[A-Z])", RegexOptions.IgnoreCase);
        var deducteePan = labelledPan.Success ? labelledPan.Groups[1].Value.ToUpperInvariant() : pans.LastOrDefault() ?? "PANNOTAVBL";

        var ayMatch = Regex.Match(text, @"Assessment\s*Year\s*[:\-]?\s*(\d{4})\s*-\s*(\d{2,4})", RegexOptions.IgnoreCase);
        var fy = defaultFy ?? "2026-27";
        if (ayMatch.Success && int.TryParse(ayMatch.Groups[1].Value, out var ayStart))
            fy = $"{ayStart - 1:D4}-{ayStart % 100:D2}";

        var qtrMatch = Regex.Match(upper, @"\b(Q[1-4])\b");
        var quarter = defaultQtr ?? (qtrMatch.Success ? qtrMatch.Value : "Q1");

        var secMatch = Regex.Match(upper, @"\b(192[A]?|194[A-Z]{1,2}|195)\b");
        var sec = MapImportedSectionToCaDesk(secMatch.Success ? secMatch.Value : (form == "24Q" ? "192_salary" : "194J_fees"), form);

        var nameMatch = Regex.Match(text, @"(?:Name\s+and\s+address\s+of\s+the\s+(?:Employee|Deductee)|Deductee\s+Name)\s*[:\-]?\s*([A-Za-z0-9 .,&'()-]{2,75})", RegexOptions.IgnoreCase);
        var deducteeName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Imported Certificate Party";

        var grossMatch = Regex.Match(text, @"(?:Total\s*(?:\(Rs\.\))?\s*|Amount\s+(?:paid|credited)[^\d\r\n]{0,25}|Gross\s+Salary[^\d\r\n]{0,25})([\d,]+\.\d{2}|\d[\d,]+)", RegexOptions.IgnoreCase);
        var tdsMatch = Regex.Match(text, @"(?:Tax\s+Deposited|Total\s+Tax\s+Deducted|TDS\s+Deposited)[^\d\r\n]{0,25}([\d,]+\.\d{2}|\d[\d,]+)", RegexOptions.IgnoreCase);
        var amt = grossMatch.Success ? ParseDecimal(grossMatch.Groups[1].Value) : 0m;
        var tds = tdsMatch.Success ? ParseDecimal(tdsMatch.Groups[1].Value) : 0m;

        var challans = new List<ImportedTdsChallanRow>();
        var bsr = BsrMatch().Match(text) is { Success: true } bm ? bm.Groups[1].Value : null;
        var serMatch = Regex.Match(text, @"(?:Challan\s*Serial\s*(?:No\.?|Number)?|Serial\s*Number\s+of\s+Challan)\s*[:\-]?\s*(\d{5})", RegexOptions.IgnoreCase);
        var ser = serMatch.Success ? serMatch.Groups[1].Value : null;
        var dtMatch = Regex.Match(text, @"\b(\d{2}[-/]\d{2}[-/]\d{4}|\d{2}-[A-Za-z]{3}-\d{4})\b");
        var chalDt = dtMatch.Success ? ParseFlexibleDate(dtMatch.Groups[1].Value) : null;

        if (bsr is not null && ser is not null)
        {
            var dt = chalDt ?? DateOnly.FromDateTime(DateTime.UtcNow);
            challans.Add(new(bsr, ser, dt, TdsMinorHead.Regular200, Math.Ceiling(tds), 0m, 0m, Math.Ceiling(tds), false, false));
        }

        if (amt <= 0m && tds <= 0m)
        {
            errors.Add("Could not detect Form 16 / 16A amounts from the pasted text. Use NSDL RPU .txt, CaDesk CSV template, or TRACES certificate summary containing Amount Paid/Credited and Tax Deducted.");
            return new ImportForm16Result("traces_certificate_text", tan, null, fy, quarter, form, [], [], warnings, errors, 0, 0, false);
        }

        if (amt <= 0m && tds > 0m)
        {
            amt = tds;
            warnings.Add("Gross payment amount could not be detected; defaulted to Tax Deposited. Review before committing.");
        }

        var rate = amt > 0m && tds > 0m ? Math.Round(tds * 100m / amt, 2) : 0m;
        var rowDate = chalDt ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var deds = new List<ImportedTdsDeductionRow>
        {
            new(form, quarter, fy, deducteePan, deducteeName, sec, rowDate, rowDate, amt, rate, tds, certNo, bsr, ser, chalDt)
        };

        return new ImportForm16Result("traces_certificate_text", tan, null, fy, quarter, form, challans, deds, warnings, errors, 0, 0, false);
    }

    public static string MapImportedSectionToCaDesk(string raw, string formType)
    {
        var s = (raw ?? "").Trim().ToUpperInvariant();
        return s switch
        {
            "92A" or "1001" or "192_GOVT" => TdsSection.Sec192Govt,
            "92C" or "1003" or "192_GOVT_UNION" => TdsSection.Sec192GovtUnion,
            "92B" or "192" or "1002" or "192_SALARY" => TdsSection.Sec192Salary,
            "94C" or "194C" or "1023" or "194C_COMPANY" => TdsSection.Sec194CCompany,
            "1024" or "194C_OTHER" or "194C_INDIVIDUAL" => TdsSection.Sec194COther,
            "4JA" or "1026" or "194J_TECH" or "194J_TECHNICAL" => TdsSection.Sec194JTech,
            "4JB" or "194J" or "1027" or "194J_FEES" or "194J_PROFESSIONAL" => TdsSection.Sec194JFees,
            "4IA" or "1008" or "194I_PLANT" or "194I_RENT_PLANT" => TdsSection.Sec194IPlant,
            "4IB" or "194I" or "194-I" or "1009" or "194I_LAND" or "194I_RENT_LAND" => TdsSection.Sec194ILand,
            "94Q" or "194Q" or "1031" => TdsSection.Sec194Q,
            "94A" or "194A" or "1020" or "1021" => TdsSection.Sec194A,
            "94H" or "194H" or "1006" => TdsSection.Sec194H,
            "94D" or "194D" or "1005" => TdsSection.Sec194D,
            "4DA" or "194DA" or "1030" => TdsSection.Sec194DA,
            "94B" or "194B" or "1058" => TdsSection.Sec194B,
            "94N" or "194N" or "1065" or "1064" => TdsSection.Sec194N,
            "94R" or "194R" or "1033" => TdsSection.Sec194R,
            "94S" or "194S" or "1037" => TdsSection.Sec194S,
            "94T" or "194T" or "1067" => TdsSection.Sec194T,
            "195" or "1057" => TdsSection.Sec195,
            _ => formType == "24Q" ? TdsSection.Sec192Salary : formType == "27Q" ? TdsSection.Sec195 : TdsSection.Sec194JFees
        };
    }

    private static DateOnly? ParseDdmmyyyy(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Trim().Length != 8) return null;
        return DateOnly.TryParseExact(s.Trim(), "ddMMyyyy", Inv, DateTimeStyles.None, out var d) ? d : null;
    }

    private static DateOnly? ParseFlexibleDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        string[] fmts = ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "ddMMyyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "d/M/yyyy", "d-M-yyyy"];
        return DateOnly.TryParseExact(t, fmts, Inv, DateTimeStyles.None, out var d) ? d : null;
    }

    private static decimal ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        var clean = s.Trim().Replace(",", "");
        return decimal.TryParse(clean, NumberStyles.Number, Inv, out var v) ? v : 0m;
    }

    private static string NormalizeMinor(string minor) =>
        minor.StartsWith("400") ? TdsMinorHead.Assessment400 : TdsMinorHead.Regular200;

    private static List<string> SplitCsvLine(string line)
    {
        var list = new List<string>();
        var cur = new System.Text.StringBuilder();
        var inQ = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else inQ = !inQ;
            }
            else if (ch == ',' && !inQ)
            {
                list.Add(cur.ToString());
                cur.Clear();
            }
            else cur.Append(ch);
        }
        list.Add(cur.ToString());
        return list;
    }
}
