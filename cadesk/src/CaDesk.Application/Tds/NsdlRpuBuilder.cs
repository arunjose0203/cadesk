using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CaDesk.Domain.Common;
using CaDesk.Domain.Tds;

namespace CaDesk.Application.Tds;

/// <summary>
/// Generates NSDL/Protean e-TDS regular quarterly return text files and checks Protean FVU rules
/// across both Tax eras supported by CaDesk:
/// - FY up to 2025-26 (Income-tax Act, 1961; FVU 9.5): Forms 24Q (v6.3/7.5), 26Q (v7.8), 27Q (v7.5)
/// - Tax Year 2026-27+ (Income-tax Act, 2025; FVU 1.2): Forms 138 (24Q), 140 (26Q), 144 (27Q) v1.1
///
/// Field positions follow Protean File Format specifications archived under docs/schemas/tds/.
/// </summary>
public static partial class NsdlRpuBuilder
{
    public const string SoftwareName = "CaDesk RPU 1.0";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [GeneratedRegex("^[A-Z]{4}[0-9]{5}[A-Z]$")]
    private static partial Regex TanPattern();

    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex PanPattern();

    [GeneratedRegex("^[0-9]{7}$")]
    private static partial Regex BsrPattern();

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex ChallanSerialPattern();

    [GeneratedRegex("^[1-9][0-9]{5}$")]
    private static partial Regex PinPattern();

    private static readonly HashSet<string> ValidStateCodes =
    [
        "01","02","03","04","05","06","07","09","10","11","12","13","14","15","16","17","18",
        "19","20","21","22","23","24","25","26","27","28","29","30","31","32","33","34","35","36","37"
    ];

    private static readonly HashSet<string> PanExemptTokens = ["PANNOTAVBL", "PANAPPLIED", "PANINVALID"];

    public static (DateOnly Start, DateOnly End, string AyCompact, string FyCompact)? QuarterWindow(string financialYear, string quarter)
    {
        var startYear = IncomeTaxLaw.StartYear(financialYear);
        if (startYear is null) return null;
        var y = startYear.Value;
        var fyCompact = $"{y:D4}{(y + 1) % 100:D2}";
        var ayCompact = $"{y + 1:D4}{(y + 2) % 100:D2}";
        return quarter.Trim().ToUpperInvariant() switch
        {
            "Q1" => (new DateOnly(y, 4, 1), new DateOnly(y, 6, 30), ayCompact, fyCompact),
            "Q2" => (new DateOnly(y, 7, 1), new DateOnly(y, 9, 30), ayCompact, fyCompact),
            "Q3" => (new DateOnly(y, 10, 1), new DateOnly(y, 12, 31), ayCompact, fyCompact),
            "Q4" => (new DateOnly(y + 1, 1, 1), new DateOnly(y + 1, 3, 31), ayCompact, fyCompact),
            _ => null
        };
    }

    public static string OfficialFormNumber(string formType, bool isAct2025) =>
        (formType.Trim().ToUpperInvariant(), isAct2025) switch
        {
            ("24Q", true) => "138",
            ("26Q", true) => "140",
            ("27Q", true) => "144",
            ("27EQ", true) => "143",
            (var f, _) => f
        };

    public static string SuggestedFileName(string formType, string quarter, bool isAct2025)
    {
        var f = OfficialFormNumber(formType, isAct2025);
        var q = quarter.Trim().ToUpperInvariant();
        // Protean FVU requires <= 12 chars total (incl. .txt) and disallows underscores/spaces.
        var raw = $"{f}{q}";
        if (raw.Length > 8) raw = raw[..8];
        return $"{raw}.txt";
    }

    public static string MapReturnSectionCode(string section, string formType, string deducteePan, bool isAct2025)
    {
        var s = (section ?? "").Trim();
        var f = formType.Trim().ToUpperInvariant();
        if (isAct2025)
        {
            // Income-tax Act, 2025 (4-digit Annexure 2 section codes, Form 138/140/144 v1.1)
            if (Regex.IsMatch(s, "^10[0-9]{2}$")) return s;
            return s.ToLowerInvariant() switch
            {
                "192_govt" => "1001",
                "192_govt_union" => "1003",
                "192_salary" or "192" or "92b" => "1002",
                "192_govt_other" or "92a" => "1001",
                "92c" => "1003",
                "194c_company" or "194c_individual" => "1023",
                "194c_other" => IsCompanyPan(deducteePan) ? "1024" : "1023",
                "194c" or "94c" => IsCompanyPan(deducteePan) ? "1024" : "1023",
                "194j_tech" or "194j_technical" or "4ja" => "1026",
                "194j_fees" or "194j_professional" or "4jb" or "194j" => "1027",
                "194i_plant" or "194i_rent_plant" or "4ia" => "1008",
                "194i_land" or "194i_rent_land" or "4ib" or "194i" => "1009",
                "194q" or "94q" => "1031",
                "194a" or "94a" => "1021",
                "194h" or "94h" => "1006",
                "194d" or "94d" => "1005",
                "194da" or "4da" => "1030",
                "194b" or "94b" => "1058",
                "194n" or "94n" => "1065",
                "194r" or "94r" => "1033",
                "194s" or "94s" => "1037",
                "194t" or "94t" => "1067",
                "195" => f == "27Q" ? "1057" : "1027",
                _ => f switch { "24Q" => "1002", "27Q" => "1057", _ => "1027" }
            };
        }

        // Legacy Income-tax Act, 1961 (3-char Annexure 2 section codes, FVU 9.5)
        return s.ToLowerInvariant() switch
        {
            "192_govt" or "92a" => "92A",
            "192_govt_union" or "92c" => "92C",
            "192_salary" or "192" or "92b" => "92B",
            "194c_company" or "194c_individual" or "194c_other" or "194c" or "94c" => "94C",
            "194j_tech" or "194j_technical" or "4ja" => "4JA",
            "194j_fees" or "194j_professional" or "4jb" or "194j" => "4JB",
            "194i_plant" or "194i_rent_plant" or "4ia" => "4IA",
            "194i_land" or "194i_rent_land" or "4ib" or "194i" => "4IB",
            "194q" or "94q" => "94Q",
            "194a" or "94a" => "94A",
            "194h" or "94h" => "94H",
            "194d" or "94d" => "94D",
            "194da" or "4da" => "4DA",
            "194b" or "94b" => "94B",
            "194n" or "94n" => "94N",
            "194r" or "94r" => "94R",
            "194s" or "94s" => "94S",
            "194t" or "94t" => "94T",
            "195" => "195",
            _ when s.Length == 3 => s.ToUpperInvariant(),
            _ => f switch { "24Q" => "92B", "27Q" => "195", _ => "4JB" }
        };
    }

    public static TdsReturnExportResult Build(
        TdsDeductorProfileDto profile,
        string formType,
        string financialYear,
        string quarter,
        IReadOnlyList<TdsChallan> allChallans,
        IReadOnlyList<TdsDeduction> allDeductions,
        DateOnly fileCreationDate,
        int fileSequenceNo = 1)
    {
        var form = formType.Trim().ToUpperInvariant();
        if (form is not ("24Q" or "26Q" or "27Q"))
            throw new ArgumentException($"Unsupported form type '{formType}'. Use 24Q, 26Q, or 27Q.", nameof(formType));

        var q = quarter.Trim().ToUpperInvariant();
        var isAct2025 = IncomeTaxLaw.IsAct2025(financialYear);
        var officialForm = OfficialFormNumber(form, isAct2025);
        var win = QuarterWindow(financialYear, q);
        var issues = new List<FvuValidationIssue>();

        if (win is null)
            issues.Add(Err("RETURN", null, "FY-001", $"Invalid Financial/Tax Year '{financialYear}' or Quarter '{quarter}'."));

        ValidateProfile(profile, isAct2025, issues);

        // Filter deductions for the requested Form + FY + Quarter
        var formDeductions = allDeductions
            .Where(d => string.Equals(d.FormType, form, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(d.FinancialYear, financialYear, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(d.Quarter, q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.PaymentDate).ThenBy(d => d.Id)
            .ToList();

        var unmapped = formDeductions.Where(d => d.ChallanId is null).ToList();
        foreach (var u in unmapped)
        {
            issues.Add(Err("DEDUCTEE", null, "DD-CHALLAN",
                $"Deduction for {u.DeducteeName} ({u.DeducteePan}, ₹{u.TotalTaxDeducted:0.00}) is not linked to any ITNS 281 challan."));
        }

        // Group mapped deductions under their challans for this FY & Quarter
        var mappedIds = formDeductions.Where(d => d.ChallanId is not null).Select(d => d.ChallanId!.Value).ToHashSet();
        var challans = allChallans
            .Where(c => (string.Equals(c.FinancialYear, financialYear, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(c.Quarter, q, StringComparison.OrdinalIgnoreCase))
                     && (mappedIds.Contains(c.Id) || c.IsNil))
            .OrderBy(c => c.ChallanDate).ThenBy(c => c.ChallanSerialNumber)
            .ToList();

        if (challans.Count == 0)
            issues.Add(Err("RETURN", null, "BH-ZERO", $"No challans with mapped {form} deductions found for {financialYear} {q}."));
        if (formDeductions.Count == 0)
            issues.Add(Err("RETURN", null, "DD-ZERO", $"No {form} deductee entries recorded for {financialYear} {q}. Statements must have at least 1 deductee."));

        var sb = new StringBuilder();
        var lineNo = 1;
        var batchChallanTotal = challans.Sum(c => c.TotalChallanAmount);

        // 1. File Header (FH) - 18 fields, 17 carets
        var fileType = form == "24Q" ? "SL1" : "NS1";
        var fh = new string[18];
        fh[0] = (lineNo++).ToString(Inv);
        fh[1] = "FH";
        fh[2] = fileType;
        fh[3] = "R";
        fh[4] = FmtDate(fileCreationDate);
        fh[5] = Math.Max(1, fileSequenceNo).ToString(Inv);
        fh[6] = "D";
        fh[7] = CleanUpper(profile.Tan, 10);
        fh[8] = "1";
        fh[9] = CleanText(SoftwareName, 75);
        AppendRecord(sb, fh);

        // 2. Batch Header (BH) - 72 fields, 71 carets
        var bh = BuildBatchHeader(
            lineNo++, profile, form, officialForm, q,
            win?.AyCompact ?? "", win?.FyCompact ?? "",
            challans.Count, batchChallanTotal, isAct2025);
        AppendRecord(sb, bh);

        decimal totalPaid = 0m, totalDeducted = 0m;
        var exportedDeducteeCount = 0;

        for (var ci = 0; ci < challans.Count; ci++)
        {
            var c = challans[ci];
            var challanSerialIdx = ci + 1;
            var deds = formDeductions.Where(d => d.ChallanId == c.Id).ToList();
            var challanLine = lineNo++;

            ValidateChallan(c, deds, challanLine, challanSerialIdx, win, fileCreationDate, issues);

            var sumDed = deds.Sum(d => d.TotalTaxDeducted);
            var sumIncTax = deds.Sum(d => d.TdsAmount);
            var sumSur = deds.Sum(d => d.Surcharge);
            var sumCess = deds.Sum(d => d.Cess);

            var cd = isAct2025
                ? BuildChallan2025(challanLine, challanSerialIdx, c, deds.Count, sumDed, sumDed, win?.End)
                : BuildChallan1961(challanLine, challanSerialIdx, c, deds.Count, sumDed, sumIncTax, sumSur, sumCess, sumDed, win?.End);
            AppendRecord(sb, cd);

            for (var di = 0; di < deds.Count; di++)
            {
                var d = deds[di];
                var ddLine = lineNo++;
                var ddRecNo = di + 1;

                ValidateDeductee(d, c, form, ddLine, ddRecNo, win, fileCreationDate, isAct2025, issues);

                var dd = isAct2025
                    ? BuildDeductee2025(ddLine, ddRecNo, challanSerialIdx, form, c, d)
                    : BuildDeductee1961(ddLine, challanSerialIdx, ddRecNo, form, c, d);
                AppendRecord(sb, dd);

                totalPaid += d.AmountPaid;
                totalDeducted += d.TotalTaxDeducted;
                exportedDeducteeCount++;
            }
        }

        // Check total batch deposit integer requirement
        if (batchChallanTotal != Math.Truncate(batchChallanTotal))
            issues.Add(Err("CHALLAN", 2, "CD-PAISE", "Batch total challan deposit amount must be whole rupees (.00)."));

        // Non-blocking reminder about TIN .csi & 24Q Q4 Salary Detail (SD)
        if (form == "24Q" && q == "Q4")
        {
            issues.Add(Warn("RETURN", null, "24Q-Q4-SD",
                "Form 24Q Q4 requires Annexure II Salary Details (SD records) inside Protean RPU/FVU before final submission."));
        }
        issues.Add(Warn("RETURN", null, "FVU-CSI",
            $"Before uploading, validate {SuggestedFileName(form, q, isAct2025)} with Protean FVU alongside your bank-verified .csi file from TIN."));

        var hasError = issues.Any(i => i.Severity == "error");
        var ay = win is not null ? $"{win.Value.AyCompact[..4]}-{win.Value.AyCompact[4..]}" : "";

        return new TdsReturnExportResult(
            FileName: SuggestedFileName(form, q, isAct2025),
            FormType: form,
            OfficialFormNumber: officialForm,
            ActLabel: IncomeTaxLaw.ActName(financialYear),
            FinancialYear: financialYear,
            AssessmentYear: ay,
            Quarter: q,
            ValidForFvu: !hasError,
            LineCount: lineNo - 1,
            ChallanCount: challans.Count,
            DeducteeCount: exportedDeducteeCount,
            TotalPaid: Math.Round(totalPaid, 2),
            TotalDeducted: Math.Round(totalDeducted, 2),
            TotalDeposited: Math.Round(batchChallanTotal, 2),
            Content: sb.ToString(),
            Issues: issues
        );
    }

    private static string[] BuildBatchHeader(
        int lineNo, TdsDeductorProfileDto p, string logicalForm, string officialForm,
        string quarter, string ayCompact, string fyCompact,
        int challanCount, decimal batchTotal, bool isAct2025)
    {
        var bh = new string[72];
        bh[0] = lineNo.ToString(Inv);
        bh[1] = "BH";
        bh[2] = "1";
        bh[3] = challanCount.ToString(Inv);
        bh[4] = officialForm;
        bh[8] = p.FiledPreviousRegular ? CleanText(p.PreviousTokenNo, 15) : "";
        bh[12] = CleanUpper(p.Tan, 10);
        bh[14] = CleanUpper(p.Pan, 10);
        bh[15] = ayCompact;
        bh[16] = fyCompact;
        bh[17] = quarter;
        bh[18] = CleanText(p.DeductorName, 75);
        bh[19] = isAct2025
            ? CleanText(string.IsNullOrWhiteSpace(p.CountryName) ? "INDIA" : p.CountryName.ToUpperInvariant(), 25)
            : CleanText(string.IsNullOrWhiteSpace(p.BranchDivision) ? "NA" : p.BranchDivision, 75);
        bh[20] = CleanText(p.FlatBlock, 25);
        bh[21] = CleanText(p.PremisesName, 25);
        bh[22] = CleanText(p.StreetRoad, 25);
        bh[23] = CleanText(p.AreaLocality, 25);
        bh[24] = CleanText(p.CityDistrict, 25);
        bh[25] = NormalizeStateCode(p.StateCode);
        bh[26] = DigitsOnly(p.Pincode, 6);
        bh[27] = CleanText(p.Email, 75);

        if (isAct2025)
        {
            bh[28] = DigitsOnly(string.IsNullOrWhiteSpace(p.CountryCallingCode) ? "91" : p.CountryCallingCode, 5);
            var contact = DigitsOnly(!string.IsNullOrWhiteSpace(p.Phone) ? p.Phone : p.Mobile, 10);
            bh[29] = contact;
            bh[30] = ""; // Filler 1
        }
        else
        {
            bh[28] = DigitsOnly(p.StdCode, 5);
            bh[29] = DigitsOnly(p.Phone, 10);
            bh[30] = "N"; // Address changed since last return
        }

        bh[31] = CleanUpper(string.IsNullOrWhiteSpace(p.DeductorType) ? "K" : p.DeductorType, 1);
        bh[32] = CleanText(p.RespName, 75);
        bh[33] = CleanText(p.RespDesignation, 20);
        bh[34] = CleanText(p.RespFlatBlock, 25);
        bh[35] = CleanText(p.RespPremisesName, 25);
        bh[36] = CleanText(p.RespStreetRoad, 25);
        bh[37] = CleanText(p.RespAreaLocality, 25);
        bh[38] = CleanText(p.RespCityDistrict, 25);
        bh[39] = NormalizeStateCode(p.RespStateCode);
        bh[40] = DigitsOnly(p.RespPincode, 6);
        bh[41] = CleanText(p.RespEmail, 75);

        if (isAct2025)
        {
            bh[42] = CleanText(string.IsNullOrWhiteSpace(p.RespCountryName) ? "INDIA" : p.RespCountryName.ToUpperInvariant(), 25);
            bh[43] = DigitsOnly(string.IsNullOrWhiteSpace(p.RespCountryCallingCode) ? "91" : p.RespCountryCallingCode, 5);
            bh[44] = DigitsOnly(p.RespMobile, 10);
            bh[45] = ""; // Filler 3
        }
        else
        {
            bh[42] = DigitsOnly(p.RespMobile, 10);
            bh[43] = DigitsOnly(p.StdCode, 5);
            bh[44] = DigitsOnly(p.Phone, 10);
            bh[45] = "N";
        }

        bh[46] = FmtIntRupee(batchTotal);
        bh[48] = logicalForm == "24Q" && isAct2025 ? "0" : "";
        bh[50] = isAct2025 ? "" : "N"; // AO approval in 1961 Act
        bh[51] = p.FiledPreviousRegular ? "Y" : "N";
        bh[53] = (bh[31] is "S" or "E" or "H" or "N") ? NormalizeStateCode(p.StateCode) : "";
        bh[58] = CleanUpper(p.RespPan, 10);
        bh[68] = CleanUpper(p.Gstin, 15);
        return bh;
    }

    private static string[] BuildChallan2025(
        int lineNo, int challanIdx, TdsChallan c, int dedCount,
        decimal sumDdDeposited, decimal sumDdDeducted, DateOnly? quarterEnd)
    {
        var cd = new string[30];
        cd[0] = lineNo.ToString(Inv);
        cd[1] = "CD";
        cd[2] = "1";
        cd[3] = challanIdx.ToString(Inv);
        cd[4] = dedCount.ToString(Inv);
        cd[5] = c.IsNil ? "Y" : "N";

        var taxSum = c.TdsAmount + c.Surcharge + c.Cess;
        cd[7] = FmtIntRupee(taxSum);
        cd[8] = FmtIntRupee(c.Interest);
        cd[9] = FmtIntRupee(c.LateFee234E);
        cd[10] = FmtIntRupee(0m);
        cd[11] = FmtIntRupee(taxSum + c.Interest + c.LateFee234E);

        if (!c.IsNil)
        {
            cd[12] = c.IsBookEntry ? "B" : "C";
            cd[14] = DigitsOnly(c.BsrCode, 7);
            cd[16] = DigitsOnly(c.ChallanSerialNumber, 5);
            cd[18] = FmtDate(c.ChallanDate);
        }
        else
        {
            cd[18] = FmtDate(quarterEnd ?? c.ChallanDate);
        }

        cd[20] = FmtDec2(sumDdDeposited);
        cd[21] = FmtDec2(sumDdDeducted);
        cd[22] = c.IsNil || c.IsBookEntry ? "" : NormalizeMinorHead(c.MinorHead);
        cd[23] = FmtIntRupee(c.Interest);
        cd[24] = FmtIntRupee(0m);
        return cd;
    }

    private static string[] BuildChallan1961(
        int lineNo, int challanIdx, TdsChallan c, int dedCount,
        decimal sumDdDeposited, decimal sumDdTax, decimal sumDdSur, decimal sumDdCess, decimal sumDdTotalTax,
        DateOnly? quarterEnd)
    {
        var cd = new string[41];
        cd[0] = lineNo.ToString(Inv);
        cd[1] = "CD";
        cd[2] = "1";
        cd[3] = challanIdx.ToString(Inv);
        cd[4] = dedCount.ToString(Inv);
        cd[5] = c.IsNil ? "Y" : "N";

        if (!c.IsNil)
        {
            if (c.IsBookEntry)
                cd[13] = DigitsOnly(c.ChallanSerialNumber, 5);
            else
                cd[11] = DigitsOnly(c.ChallanSerialNumber, 5);
            cd[15] = DigitsOnly(c.BsrCode, 7);
            cd[17] = FmtDate(c.ChallanDate);
            cd[36] = c.IsBookEntry ? "Y" : "N";
            cd[39] = c.IsBookEntry ? "" : NormalizeMinorHead(c.MinorHead);
        }
        else
        {
            cd[17] = FmtDate(quarterEnd ?? c.ChallanDate);
        }

        cd[21] = FmtIntRupee(c.TdsAmount);
        cd[22] = FmtIntRupee(c.Surcharge);
        cd[23] = FmtIntRupee(c.Cess);
        cd[24] = FmtIntRupee(c.Interest);
        cd[25] = FmtIntRupee(0m);
        cd[26] = FmtIntRupee(c.TdsAmount + c.Surcharge + c.Cess + c.Interest + c.LateFee234E);
        cd[28] = FmtDec2(sumDdDeposited);
        cd[29] = FmtDec2(sumDdTax);
        cd[30] = FmtDec2(sumDdSur);
        cd[31] = FmtDec2(sumDdCess);
        cd[32] = FmtDec2(sumDdTotalTax);
        cd[33] = FmtIntRupee(c.Interest);
        cd[34] = FmtIntRupee(0m);
        cd[38] = FmtIntRupee(c.LateFee234E);
        return cd;
    }

    private static string[] BuildDeductee2025(
        int lineNo, int ddRecNo, int challanIdx, string form, TdsChallan c, TdsDeduction d)
    {
        var dd = new string[45];
        var pan = NormalizeDeducteePan(d.DeducteePan);
        var remark = ResolveRemark(d, pan);

        dd[0] = lineNo.ToString(Inv);
        dd[1] = "DD";
        dd[2] = "1";
        // Under Tax Year 2026-27+ specs (Form 138/140/144), field 4 is Deductee No and field 5 is Challan Ref No
        dd[3] = ddRecNo.ToString(Inv);
        dd[4] = challanIdx.ToString(Inv);
        dd[5] = "O";
        dd[7] = pan;
        dd[8] = CleanText(d.DeducteeName, 75);

        if (form == "27Q")
        {
            dd[11] = d.DeducteeCode?.Trim() is "1" or "01" || IsCompanyPan(pan) ? "1" : "2";
            dd[13] = (d.OptedOutNewRegime ?? false) ? "Y" : "N";
            dd[15] = string.IsNullOrWhiteSpace(d.RemittanceCode) ? "21" : d.RemittanceCode.Trim().PadLeft(2, '0');
            dd[16] = string.IsNullOrWhiteSpace(d.CountryCode) ? "267" : d.CountryCode.Trim();
            dd[17] = "Y";
            dd[20] = "N";
            dd[29] = string.Equals(d.TaxRateSource, "B", StringComparison.OrdinalIgnoreCase) ? "B" : "A";
            dd[30] = CleanText(d.AckNumber, 26);
        }
        else if (form == "26Q")
        {
            dd[17] = "Y";
        }
        else // 24Q (Form 138)
        {
            dd[17] = "Y";
            dd[28] = FmtDate(c.ChallanDate);
        }

        dd[14] = MapReturnSectionCode(d.Section, form, pan, isAct2025: true);
        dd[18] = FmtDate(d.PaymentDate);
        dd[19] = FmtDec2(d.AmountPaid);
        dd[23] = FmtDec2(d.TotalTaxDeducted);
        dd[24] = FmtDec2(d.TotalTaxDeducted);
        dd[26] = d.TotalTaxDeducted > 0m ? FmtDate(d.DeductionDate) : "";

        if (form != "24Q")
            dd[27] = FmtRate4(d.TotalTaxDeducted == 0m ? 0m : d.TdsRate);

        dd[31] = remark;
        if (remark is "A" or "B" && d.Certificate is not null)
            dd[32] = CleanUpper(d.Certificate.CertificateNumber, 15);
        return dd;
    }

    private static string[] BuildDeductee1961(
        int lineNo, int challanIdx, int ddRecNo, string form, TdsChallan c, TdsDeduction d)
    {
        var dd = new string[54];
        var pan = NormalizeDeducteePan(d.DeducteePan);
        var remark = ResolveRemark(d, pan);

        dd[0] = lineNo.ToString(Inv);
        dd[1] = "DD";
        dd[2] = "1";
        // Under Act 1961 specs (Form 24Q/26Q/27Q v6.3/v7.8/v7.5), field 4 is Challan No and field 5 is Deductee No
        dd[3] = challanIdx.ToString(Inv);
        dd[4] = ddRecNo.ToString(Inv);
        dd[5] = "O";

        if (form == "24Q")
        {
            dd[6] = ddRecNo.ToString(Inv);
        }
        else
        {
            dd[7] = string.IsNullOrWhiteSpace(d.DeducteeCode)
                ? (IsCompanyPan(pan) ? "01" : "02")
                : d.DeducteeCode.Trim();
        }

        dd[9] = pan;
        if (PanExemptTokens.Contains(pan))
            dd[11] = ddRecNo.ToString(Inv);
        dd[12] = CleanText(d.DeducteeName, 75);
        dd[13] = FmtDec2(d.TdsAmount);
        dd[14] = FmtDec2(d.Surcharge);
        dd[15] = FmtDec2(d.Cess);
        dd[16] = FmtDec2(d.TotalTaxDeducted);
        dd[18] = FmtDec2(d.TotalTaxDeducted);
        dd[21] = FmtDec2(d.AmountPaid);
        dd[22] = FmtDate(d.PaymentDate);
        dd[23] = d.TotalTaxDeducted > 0m ? FmtDate(d.DeductionDate) : "";

        if (form == "24Q")
        {
            dd[24] = FmtDate(c.ChallanDate);
        }
        else
        {
            dd[25] = FmtRate4(d.TotalTaxDeducted == 0m ? 0m : d.TdsRate);
        }

        if (form == "27Q")
        {
            dd[26] = "N";
            dd[34] = string.Equals(d.TaxRateSource, "B", StringComparison.OrdinalIgnoreCase) ? "B" : "A";
            dd[35] = string.IsNullOrWhiteSpace(d.RemittanceCode) ? "21" : d.RemittanceCode.Trim().PadLeft(2, '0');
            dd[36] = CleanText(d.AckNumber, 15);
            dd[37] = string.IsNullOrWhiteSpace(d.CountryCode) ? "267" : d.CountryCode.Trim();
            dd[48] = (d.OptedOutNewRegime ?? false) ? "Y" : "N";
        }

        dd[29] = remark;
        dd[32] = MapReturnSectionCode(d.Section, form, pan, isAct2025: false);
        if (remark is "A" or "B" && d.Certificate is not null)
            dd[33] = CleanUpper(d.Certificate.CertificateNumber, 10);

        return dd;
    }

    private static void ValidateProfile(TdsDeductorProfileDto p, bool isAct2025, List<FvuValidationIssue> issues)
    {
        var tan = CleanUpper(p.Tan, 10);
        if (!TanPattern().IsMatch(tan))
            issues.Add(Err("PROFILE", 1, "BH-TAN", "Deductor TAN must be 10 characters in AAAA99999A format."));

        var pan = CleanUpper(p.Pan, 10);
        if (pan != "PANNOTREQD" && !PanPattern().IsMatch(pan))
            issues.Add(Err("PROFILE", 2, "BH-PAN", "Deductor PAN must be a valid 10-character PAN (or PANNOTREQD)."));

        if (string.IsNullOrWhiteSpace(p.DeductorName))
            issues.Add(Err("PROFILE", 2, "BH-NAME", "Deductor name is required."));
        if (string.IsNullOrWhiteSpace(p.FlatBlock))
            issues.Add(Err("PROFILE", 2, "BH-ADDR", "Deductor address line 1 (Flat/Door/Block) is required."));
        if (!ValidStateCodes.Contains(NormalizeStateCode(p.StateCode)))
            issues.Add(Err("PROFILE", 2, "BH-STATE", $"State code '{p.StateCode}' is invalid (expected Protean code 01..37, e.g., 16 for Kerala)."));
        if (!PinPattern().IsMatch(DigitsOnly(p.Pincode, 6)))
            issues.Add(Err("PROFILE", 2, "BH-PIN", "Deductor PIN code must be 6 digits."));

        var respPan = CleanUpper(p.RespPan, 10);
        if (!PanPattern().IsMatch(respPan))
            issues.Add(Err("PROFILE", 2, "BH-RPAN", "Responsible Person PAN is mandatory (10 characters AAAAA9999A)."));
        if (string.IsNullOrWhiteSpace(p.RespName))
            issues.Add(Err("PROFILE", 2, "BH-RNAME", "Responsible Person name is required."));
        if (string.IsNullOrWhiteSpace(p.RespDesignation))
            issues.Add(Err("PROFILE", 2, "BH-RDESIG", "Responsible Person designation is required."));
        if (string.IsNullOrWhiteSpace(p.RespFlatBlock))
            issues.Add(Err("PROFILE", 2, "BH-RADDR", "Responsible Person address line 1 is required."));
        if (!ValidStateCodes.Contains(NormalizeStateCode(p.RespStateCode)))
            issues.Add(Err("PROFILE", 2, "BH-RSTATE", $"Responsible Person state code '{p.RespStateCode}' is invalid."));
        if (!PinPattern().IsMatch(DigitsOnly(p.RespPincode, 6)))
            issues.Add(Err("PROFILE", 2, "BH-RPIN", "Responsible Person PIN code must be 6 digits."));

        var email = !string.IsNullOrWhiteSpace(p.RespEmail) ? p.RespEmail : p.Email;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
            issues.Add(Err("PROFILE", 2, "BH-EMAIL", "A valid email address is required for the Deductor or Responsible Person."));

        var mob = DigitsOnly(p.RespMobile, 10);
        if (mob.Length != 10)
            issues.Add(Err("PROFILE", 2, "BH-MOB", "Responsible Person 10-digit mobile/contact number is mandatory for FVU."));

        if (isAct2025 && DigitsOnly(p.Phone, 10).Length == 0 && DigitsOnly(p.Mobile, 10).Length == 0)
            issues.Add(Err("PROFILE", 2, "BH-PHONE", "Deductor contact number is mandatory under FVU 1.2 (Tax Year 2026-27+)."));
    }

    private static void ValidateChallan(
        TdsChallan c, IReadOnlyList<TdsDeduction> mappedDeds, int lineNo, int idx,
        (DateOnly Start, DateOnly End, string AyCompact, string FyCompact)? win,
        DateOnly fileCreationDate, List<FvuValidationIssue> issues)
    {
        if (c.TdsAmount != Math.Truncate(c.TdsAmount)
            || c.Interest != Math.Truncate(c.Interest)
            || c.LateFee234E != Math.Truncate(c.LateFee234E)
            || c.Surcharge != Math.Truncate(c.Surcharge)
            || c.Cess != Math.Truncate(c.Cess))
        {
            issues.Add(Err("CHALLAN", lineNo, "CD-PAISE",
                $"Challan #{idx} ({c.ChallanSerialNumber}): OLTAS deposit amounts in CD record must be integer rupees (.00)."));
        }

        if (!c.IsNil)
        {
            if (!BsrPattern().IsMatch((c.BsrCode ?? "").Trim()))
                issues.Add(Err("CHALLAN", lineNo, "CD-BSR", $"Challan #{idx}: BSR / Form 24G receipt code must be 7 digits."));
            if (!ChallanSerialPattern().IsMatch((c.ChallanSerialNumber ?? "").Trim()))
                issues.Add(Err("CHALLAN", lineNo, "CD-SERIAL", $"Challan #{idx}: Challan serial / DDO serial must be 5 digits."));
            if (c.ChallanDate > fileCreationDate)
                issues.Add(Err("CHALLAN", lineNo, "CD-DATE", $"Challan #{idx}: Challan date {c.ChallanDate:dd/MM/yyyy} cannot be in the future."));
            if (mappedDeds.Count == 0)
                issues.Add(Err("CHALLAN", lineNo, "CD-EMPTY", $"Challan #{idx}: Must have at least 1 mapped deductee record."));
        }

        var sumDeposited = mappedDeds.Sum(d => d.TotalTaxDeducted);
        var availableTaxBalance = c.TdsAmount + c.Surcharge + c.Cess;
        if (sumDeposited > availableTaxBalance)
        {
            issues.Add(Err("CHALLAN", lineNo, "CD-SHORT",
                $"Challan #{idx} ({c.ChallanSerialNumber}): Mapped deductee tax (₹{sumDeposited:0.00}) exceeds challan TDS deposit (₹{availableTaxBalance:0.00})."));
        }
    }

    private static void ValidateDeductee(
        TdsDeduction d, TdsChallan c, string form, int lineNo, int recNo,
        (DateOnly Start, DateOnly End, string AyCompact, string FyCompact)? win,
        DateOnly fileCreationDate, bool isAct2025, List<FvuValidationIssue> issues)
    {
        var pan = NormalizeDeducteePan(d.DeducteePan);
        var remark = ResolveRemark(d, pan);

        if (!PanPattern().IsMatch(pan) && !PanExemptTokens.Contains(pan))
            issues.Add(Err("DEDUCTEE", lineNo, "DD-PAN", $"Row #{recNo} ({d.DeducteeName}): Invalid PAN '{d.DeducteePan}'."));

        if (PanExemptTokens.Contains(pan) && remark != "C" && d.TotalTaxDeducted > 0m)
            issues.Add(Err("DEDUCTEE", lineNo, "DD-206AA", $"Row #{recNo} ({d.DeducteeName}): Missing/invalid PAN requires higher deduction remark 'C'."));

        if (PanExemptTokens.Contains(pan) && d.AmountPaid > 0m && d.TotalTaxDeducted < Math.Round(d.AmountPaid * 0.20m, 2) - 0.01m)
            issues.Add(Err("DEDUCTEE", lineNo, "DD-RATE20", $"Row #{recNo} ({d.DeducteeName}): Section 206AA/397(2) requires at least 20% TDS when PAN is unavailable."));

        if (d.AmountPaid <= 0m)
            issues.Add(Err("DEDUCTEE", lineNo, "DD-AMT", $"Row #{recNo} ({d.DeducteeName}): Amount paid/credited must be greater than 0.00."));

        if (d.PaymentDate > fileCreationDate)
            issues.Add(Err("DEDUCTEE", lineNo, "DD-FUTURE", $"Row #{recNo} ({d.DeducteeName}): Payment date cannot be in the future."));

        if (win is { } w)
        {
            if (isAct2025 && (d.PaymentDate < w.Start || d.PaymentDate > w.End))
            {
                issues.Add(Err("DEDUCTEE", lineNo, "DD-QTR-PAY",
                    $"Row #{recNo} ({d.DeducteeName}): Under FVU 1.2, payment date {d.PaymentDate:dd/MM/yyyy} must lie within {d.Quarter} ({w.Start:dd/MM/yyyy}–{w.End:dd/MM/yyyy})."));
            }
            if (d.TotalTaxDeducted > 0m && d.DeductionDate < w.Start)
            {
                issues.Add(Err("DEDUCTEE", lineNo, "DD-QTR-DED",
                    $"Row #{recNo} ({d.DeducteeName}): Deduction date {d.DeductionDate:dd/MM/yyyy} cannot precede quarter start {w.Start:dd/MM/yyyy}."));
            }
        }

        if (remark == "A")
        {
            var certNo = d.Certificate?.CertificateNumber?.Trim() ?? "";
            if (certNo.Length is < 10 or > 15)
            {
                issues.Add(Err("DEDUCTEE", lineNo, "DD-CERT",
                    $"Row #{recNo} ({d.DeducteeName}): Section 197/395 certificate number must be 10–15 characters (found '{certNo}')."));
            }
        }

        if (c.IsNil && d.TotalTaxDeducted > 0m)
            issues.Add(Err("DEDUCTEE", lineNo, "DD-NIL", $"Row #{recNo} ({d.DeducteeName}): NIL challan cannot contain non-zero tax deducted."));

        if (form == "27Q" && string.IsNullOrWhiteSpace(d.CountryCode))
            issues.Add(Warn("DEDUCTEE", lineNo, "DD-27Q-CTRY", $"Row #{recNo} ({d.DeducteeName}): Country code defaulted to 267 (USA) for Form 27Q/144."));
    }

    /// <summary>Parses a caret-delimited Protean FVU .err file into structured entries.</summary>
    public static List<ParsedFvuErrEntry> ParseFvuErrFile(string content)
    {
        var list = new List<ParsedFvuErrEntry>();
        if (string.IsNullOrWhiteSpace(content)) return list;
        foreach (var raw in content.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || !line.Contains('^')) continue;
            var cols = line.Split('^');
            // Skip HTML/header lines if someone uploads the .html error report accidentally
            if (cols[0].StartsWith('<') || cols[0].Equals("Line Number", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new ParsedFvuErrEntry(
                LineNumber: int.TryParse(cols.ElementAtOrDefault(0), out var ln) ? ln : null,
                RecordType: cols.ElementAtOrDefault(1)?.Trim() ?? "",
                ChallanNo: int.TryParse(cols.ElementAtOrDefault(3), out var cn) ? cn : null,
                DeducteeNo: int.TryParse(cols.ElementAtOrDefault(4), out var dn) ? dn : null,
                ErrorCode: cols.ElementAtOrDefault(6)?.Trim() is { Length: > 0 } c6 ? c6 : (cols.ElementAtOrDefault(7)?.Trim() ?? "FVU"),
                Description: cols.LastOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? line
            ));
        }
        return list;
    }

    private static string ResolveRemark(TdsDeduction d, string normalizedPan)
    {
        if (!string.IsNullOrWhiteSpace(d.RemarkCode)) return d.RemarkCode.Trim().ToUpperInvariant()[..1];
        if (PanExemptTokens.Contains(normalizedPan)) return "C";
        if (d.CertificateId is not null) return "A";
        return "";
    }

    private static string NormalizeDeducteePan(string? pan)
    {
        var p = CleanUpper(pan, 10);
        return string.IsNullOrEmpty(p) ? "PANNOTAVBL" : p;
    }

    private static bool IsCompanyPan(string? pan) => pan is { Length: 10 } && pan[3] == 'C';

    private static string NormalizeStateCode(string? state)
    {
        var digits = DigitsOnly(state, 2);
        return digits.Length == 1 ? "0" + digits : digits;
    }

    private static string NormalizeMinorHead(string? minorHead)
    {
        if (string.IsNullOrWhiteSpace(minorHead)) return "200";
        if (minorHead.StartsWith("400", StringComparison.Ordinal)) return "400";
        if (minorHead.StartsWith("100", StringComparison.Ordinal)) return "100";
        return "200";
    }

    private static string CleanUpper(string? s, int maxLen) => CleanText(s, maxLen).ToUpperInvariant();

    private static string CleanText(string? s, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var clean = s.Replace('^', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        // Ensure pure ASCII printable bytes so Protean FVU never trips on Unicode punctuation
        var ascii = new StringBuilder(Math.Min(clean.Length, maxLen));
        foreach (var ch in clean)
        {
            if (ascii.Length >= maxLen) break;
            ascii.Append(ch >= 32 && ch <= 126 ? ch : ' ');
        }
        return ascii.ToString().Trim();
    }

    private static string DigitsOnly(string? s, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = new string(s.Where(char.IsAsciiDigit).ToArray());
        return d.Length > maxLen ? d[..maxLen] : d;
    }

    private static string FmtDate(DateOnly d) => d.ToString("ddMMyyyy", Inv);
    private static string FmtDec2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero).ToString("0.00", Inv);
    private static string FmtIntRupee(decimal v) => Math.Truncate(v).ToString("0", Inv) + ".00";
    private static string FmtRate4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero).ToString("0.0000", Inv);

    private static void AppendRecord(StringBuilder sb, string[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append('^');
            sb.Append(fields[i] ?? "");
        }
        sb.Append("\r\n");
    }

    private static FvuValidationIssue Err(string rec, int? line, string code, string msg) => new("error", rec, line, code, msg);
    private static FvuValidationIssue Warn(string rec, int? line, string code, string msg) => new("warning", rec, line, code, msg);
}
