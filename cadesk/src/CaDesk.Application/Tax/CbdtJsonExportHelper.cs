using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CaDesk.Application.Common;
using CaDesk.Domain.Clients;

namespace CaDesk.Application.Tax;

/// <summary>
/// Shared formatting, CBDT state-code conversion, PAN/PIN/phone normalization, Chapter VI-A builders,
/// and official schema validation runner across ITR-1, ITR-4, and Form 3CA/3CB JSON exports.
/// </summary>
public static partial class CbdtJsonExportHelper
{
    public const string SoftwareId = "SW00000001";
    public const string SoftwareVersion = "1.0";

    private static readonly JsonSerializerOptions PrettyOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [GeneratedRegex("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$")]
    private static partial Regex PanRegex();

    [GeneratedRegex("^[1-9][0-9]{5}$")]
    private static partial Regex PinRegex();

    // GSTIN 2-digit Census state prefixes -> CBDT ITR/Form-3CD 2-digit StateCode ("01".."37", "99")
    private static readonly Dictionary<string, string> GstinPrefixToCbdtState = new(StringComparer.Ordinal)
    {
        ["01"] = "14", // Jammu & Kashmir
        ["02"] = "13", // Himachal Pradesh
        ["03"] = "26", // Punjab
        ["04"] = "06", // Chandigarh
        ["05"] = "34", // Uttarakhand
        ["06"] = "12", // Haryana
        ["07"] = "09", // Delhi
        ["08"] = "27", // Rajasthan
        ["09"] = "31", // Uttar Pradesh
        ["10"] = "05", // Bihar
        ["11"] = "28", // Sikkim
        ["12"] = "03", // Arunachal Pradesh
        ["13"] = "23", // Nagaland
        ["14"] = "20", // Manipur
        ["15"] = "22", // Mizoram
        ["16"] = "30", // Tripura
        ["17"] = "21", // Meghalaya
        ["18"] = "04", // Assam
        ["19"] = "32", // West Bengal
        ["20"] = "35", // Jharkhand
        ["21"] = "24", // Odisha
        ["22"] = "33", // Chhattisgarh
        ["23"] = "18", // Madhya Pradesh
        ["24"] = "11", // Gujarat
        ["25"] = "08", // Daman & Diu
        ["26"] = "07", // Dadra & Nagar Haveli
        ["27"] = "19", // Maharashtra
        ["29"] = "15", // Karnataka
        ["30"] = "10", // Goa
        ["31"] = "17", // Lakshadweep
        ["32"] = "16", // Kerala
        ["33"] = "29", // Tamil Nadu
        ["34"] = "25", // Puducherry
        ["35"] = "01", // Andaman & Nicobar
        ["36"] = "36", // Telangana
        ["37"] = "02", // Andhra Pradesh
        ["38"] = "37", // Ladakh
    };

    private static readonly HashSet<string> ValidCbdtStateCodes =
    [
        "01","02","03","04","05","06","07","08","09","10","11","12","13","14","15","16","17","18",
        "19","20","21","22","23","24","25","26","27","28","29","30","31","32","33","34","35","36","37","99"
    ];

    public static long ToRupees(decimal amount) =>
        (long)Math.Max(0m, Math.Round(amount, 0, MidpointRounding.AwayFromZero));

    public static long ToSignedRupees(decimal amount) =>
        (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static string ExtractTaxId(Client client, string key) =>
        client.TaxIds.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val.Trim() : "";

    public static string NormalizePan(string? raw, List<IncomeTaxJsonExportIssue> issues, string fieldPath)
    {
        var up = (raw ?? "").Trim().ToUpperInvariant();
        if (PanRegex().IsMatch(up)) return up;
        issues.Add(new("error", fieldPath, "CBDT-PAN", $"Client PAN '{raw}' is missing or invalid; update client Tax IDs before filing."));
        return string.IsNullOrEmpty(up) ? "AAAAA0000A" : up;
    }

    public static string ResolveStateCode(string? explicitCode, Client? client)
    {
        if (!string.IsNullOrWhiteSpace(explicitCode))
        {
            var code = DigitsOnly(explicitCode, 2).PadLeft(2, '0');
            if (ValidCbdtStateCodes.Contains(code)) return code;
        }
        if (client is not null)
        {
            var gstin = ExtractTaxId(client, "GSTIN");
            if (gstin.Length >= 2 && GstinPrefixToCbdtState.TryGetValue(gstin[..2], out var mapped))
                return mapped;
        }
        return "16"; // Kerala default matching pilot profile
    }

    public static string ResolvePinString(string? explicitPin, string fallback = "682001")
    {
        var d = DigitsOnly(explicitPin, 6);
        return PinRegex().IsMatch(d) ? d : fallback;
    }

    public static long ResolvePinInt(string? explicitPin, int fallback = 682001) =>
        long.Parse(ResolvePinString(explicitPin, fallback.ToString()));

    public static long ResolveMobileLong(string? explicitMob, string? clientPhone)
    {
        var d = DigitsOnly(!string.IsNullOrWhiteSpace(explicitMob) ? explicitMob : clientPhone, 10);
        if (d.Length > 10) d = d[^10..];
        if (d.Length >= 5 && long.TryParse(d, out var val) && d[0] != '0') return val;
        return 9847000001L;
    }

    public static string ResolveEmail(string? explicitEmail, string? clientEmail)
    {
        var em = (!string.IsNullOrWhiteSpace(explicitEmail) ? explicitEmail : clientEmail)?.Trim();
        return !string.IsNullOrWhiteSpace(em) && em.Contains('@') && em.Contains('.') ? Trunc(em, 125) : "accounts@client.in";
    }

    public static string Trunc(string? value, int maxLen, string fallback = "NA")
    {
        var s = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    public static string DigitsOnly(string? s, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = new string(s.Where(char.IsAsciiDigit).ToArray());
        return d.Length > maxLen ? d[..maxLen] : d;
    }

    public static (string StartYear4, string AssessmentYearDashed, DateOnly PrevFyStart, DateOnly PrevFyEnd) ParseAyWindow(string ayOrFy, bool inputIsFy = false)
    {
        var m = Regex.Match((ayOrFy ?? "").Trim(), @"^(\d{4})-(\d{2})$");
        int fyStart = m.Success
            ? (inputIsFy ? int.Parse(m.Groups[1].Value) : int.Parse(m.Groups[1].Value) - 1)
            : 2025;
        var ayStart = fyStart + 1;
        return ($"{ayStart:D4}", $"{ayStart:D4}-{(ayStart + 1) % 100:D2}", new DateOnly(fyStart, 4, 1), new DateOnly(fyStart + 1, 3, 31));
    }

    public static JsonObject BuildCreationInfo(string city, DateOnly? date = null) => new()
    {
        ["SWVersionNo"] = SoftwareVersion,
        ["SWCreatedBy"] = SoftwareId,
        ["JSONCreatedBy"] = SoftwareId,
        ["JSONCreationDate"] = (date ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd"),
        ["IntermediaryCity"] = Trunc(city, 25, "Kochi"),
        ["Digest"] = "-",
    };

    public static JsonObject BuildItr1UsrChapVia(long d80C, long d80Ccd1B, long d80D, long d80E, long d80G, long d80Tta, long total) => new()
    {
        ["Section80C"] = d80C,
        ["Section80CCC"] = 0,
        ["Section80CCDEmployeeOrSE"] = 0,
        ["Section80CCD1B"] = d80Ccd1B,
        ["Section80CCDEmployer"] = 0,
        ["Section80D"] = d80D,
        ["Section80DD"] = 0,
        ["Section80DDB"] = 0,
        ["Section80E"] = d80E,
        ["Section80EE"] = 0,
        ["Section80G"] = d80G,
        ["Section80GG"] = 0,
        ["Section80GGA"] = 0,
        ["Section80GGC"] = 0,
        ["Section80U"] = 0,
        ["Section80TTA"] = d80Tta,
        ["Section80TTB"] = 0,
        ["AnyOthSec80CCH"] = 0,
        ["TotalChapVIADeductions"] = total,
    };

    public static JsonObject BuildItr1AllowedChapVia(long d80C, long d80Ccd1B, long d80D, long d80E, long d80G, long d80Tta, long total)
    {
        var obj = BuildItr1UsrChapVia(
            Math.Min(d80C, 150_000L),
            Math.Min(d80Ccd1B, 50_000L),
            Math.Min(d80D, 100_000L),
            d80E,
            d80G,
            Math.Min(d80Tta, 10_000L),
            total);
        obj["Section80EEA"] = 0;
        obj["Section80EEB"] = 0;
        return obj;
    }

    public static JsonObject BuildItr4ChapVia(long d80C, long d80Ccd1B, long d80D, long d80E, long d80G, long d80Tta, long total, bool applyCaps) => new()
    {
        ["Section80C"] = applyCaps ? Math.Min(d80C, 150_000L) : d80C,
        ["Section80CCC"] = 0,
        ["Section80CCDEmployeeOrSE"] = 0,
        ["Section80CCD1B"] = applyCaps ? Math.Min(d80Ccd1B, 50_000L) : d80Ccd1B,
        ["Section80CCDEmployer"] = 0,
        ["Section80D"] = applyCaps ? Math.Min(d80D, 100_000L) : d80D,
        ["Section80DD"] = 0,
        ["Section80DDB"] = 0,
        ["Section80E"] = d80E,
        ["Section80G"] = d80G,
        ["Section80GG"] = 0,
        ["Section80GGC"] = 0,
        ["Section80U"] = 0,
        ["Section80TTA"] = applyCaps ? Math.Min(d80Tta, 10_000L) : d80Tta,
        ["Section80TTB"] = 0,
        ["AnyOthSec80CCH"] = 0,
        ["TotalChapVIADeductions"] = total,
    };

    public static JsonObject? BuildBankAccountDtls(ExportItrJsonRequest? req)
    {
        var outObj = new JsonObject();
        if (!string.IsNullOrWhiteSpace(req?.IfscCode) && !string.IsNullOrWhiteSpace(req?.BankAccountNo))
        {
            outObj["AddtnlBankDetails"] = new JsonArray
            {
                new JsonObject
                {
                    ["IFSCCode"] = req.IfscCode.Trim().ToUpperInvariant(),
                    ["BankName"] = Trunc(req.BankName, 125, "State Bank of India"),
                    ["BankAccountNo"] = Trunc(req.BankAccountNo, 20),
                    ["AccountType"] = string.IsNullOrWhiteSpace(req.BankAccountType) ? "SB" : req.BankAccountType.Trim().ToUpperInvariant(),
                    ["UseForRefund"] = "true",
                }
            };
        }
        return outObj;
    }

    public static IncomeTaxJsonExportResult ValidateAndFinalize(
        JsonNode rootPayload,
        string embeddedSchemaSuffix,
        string formType,
        string schemaVersion,
        string assessmentYear,
        string financialYear,
        string fileName,
        List<IncomeTaxJsonExportIssue> issues)
    {
        var schema = JsonSchemaValidator.LoadEmbeddedSchema(embeddedSchemaSuffix);
        var schemaErrors = JsonSchemaValidator.Validate(rootPayload, schema);
        foreach (var err in schemaErrors)
            issues.Add(new("error", err.Path, $"SCHEMA-{err.Keyword.ToUpperInvariant()}", err.Message));

        var hasErrors = issues.Any(i => i.Severity == "error");
        return new IncomeTaxJsonExportResult(
            FormType: formType,
            SchemaVersion: schemaVersion,
            AssessmentYear: assessmentYear,
            FinancialYear: financialYear,
            FileName: fileName,
            ValidAgainstSchema: schemaErrors.Count == 0 && !hasErrors,
            SchemaErrorCount: schemaErrors.Count,
            JsonContent: rootPayload.ToJsonString(PrettyOpts),
            Issues: issues);
    }
}
