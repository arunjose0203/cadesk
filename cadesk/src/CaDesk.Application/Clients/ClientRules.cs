using System.Globalization;
using System.Text.RegularExpressions;
using CaDesk.Domain.Clients;

namespace CaDesk.Application.Clients;

/// <summary>Tax ID checks driven by the country's tax_id_types rows. Pure functions, unit-tested.</summary>
public static class TaxIdRules
{
    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Upper-case, no spaces or dashes: "abcde 1234-f" → "ABCDE1234F".</summary>
    public static string Normalise(string value) =>
        new string(value.Where(ch => !char.IsWhiteSpace(ch) && ch != '-').ToArray()).ToUpperInvariant();

    /// <summary>Checks every tax ID against its country's rules. Keys must be types the country uses.</summary>
    public static Dictionary<string, string[]> Validate(IReadOnlyDictionary<string, string> taxIds, IReadOnlyCollection<TaxIdType> types)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (code, raw) in taxIds)
        {
            var type = types.FirstOrDefault(t => t.Code == code);
            if (type is null)
            {
                errors[$"taxIds.{code}"] = [$"{code} isn't used in this country. Use: {string.Join(", ", types.Select(t => t.Code))}."];
                continue;
            }
            var value = Normalise(raw);
            if (!Regex.IsMatch(value, type.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)))
                errors[$"taxIds.{code}"] = [$"{type.Label} should look like {type.Example}."];
            else if (type.Checksum == "gstin" && !GstinCheckDigitOk(value))
                errors[$"taxIds.{code}"] = [$"{type.Label} check digit is wrong. Please re-check the number."];
        }

        // India: a GSTIN contains the holder's PAN in characters 3-12
        if (taxIds.TryGetValue("PAN", out var pan) && taxIds.TryGetValue("GSTIN", out var gstin) &&
            !errors.ContainsKey("taxIds.PAN") && !errors.ContainsKey("taxIds.GSTIN") &&
            Normalise(gstin).Substring(2, 10) != Normalise(pan))
            errors["taxIds.GSTIN"] = ["This GSTIN belongs to a different PAN."];

        return errors;
    }

    /// <summary>GSTIN 15th character: base-36 weighted checksum over the first 14 characters.</summary>
    public static bool GstinCheckDigitOk(string gstin)
    {
        if (gstin.Length != 15) return false;
        var sum = 0;
        for (var i = 0; i < 14; i++)
        {
            var v = Base36.IndexOf(gstin[i]);
            if (v < 0) return false;
            var product = v * (i % 2 == 0 ? 1 : 2);
            sum += product / 36 + product % 36;
        }
        return Base36[(36 - sum % 36) % 36] == gstin[14];
    }
}

/// <summary>Validates and normalises custom field values against the firm's definitions.</summary>
public static class CustomFieldRules
{
    public static (Dictionary<string, string> Values, Dictionary<string, string[]> Errors) Validate(
        IReadOnlyDictionary<string, string>? input, IReadOnlyCollection<CustomFieldDefinition> defs)
    {
        var values = new Dictionary<string, string>();
        var errors = new Dictionary<string, string[]>();
        input ??= new Dictionary<string, string>();

        foreach (var key in input.Keys.Where(k => defs.All(d => d.Key != k)))
            errors[$"custom.{key}"] = [$"'{key}' isn't a field your firm uses."];

        foreach (var d in defs.Where(d => d.IsActive))
        {
            input.TryGetValue(d.Key, out var raw);
            raw = raw?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                if (d.IsRequired) errors[$"custom.{d.Key}"] = [$"{d.Label} is required."];
                continue;
            }

            string? normalised = d.FieldType switch
            {
                CustomFieldTypes.Number => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                    ? n.ToString(CultureInfo.InvariantCulture) : null,
                CustomFieldTypes.Date => DateOnly.TryParseExact(raw, ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt.ToString("yyyy-MM-dd") : null,
                CustomFieldTypes.Bool => raw.ToLowerInvariant() switch
                {
                    "true" or "yes" or "y" or "1" => "true",
                    "false" or "no" or "n" or "0" => "false",
                    _ => null,
                },
                CustomFieldTypes.Dropdown => d.Options?.FirstOrDefault(o => string.Equals(o, raw, StringComparison.OrdinalIgnoreCase)),
                _ => raw.Length <= 500 ? raw : null,
            };

            if (normalised is null)
            {
                errors[$"custom.{d.Key}"] = [d.FieldType switch
                {
                    CustomFieldTypes.Number => $"{d.Label} must be a number.",
                    CustomFieldTypes.Date => $"{d.Label} must be a date (yyyy-mm-dd or dd-mm-yyyy).",
                    CustomFieldTypes.Bool => $"{d.Label} must be yes or no.",
                    CustomFieldTypes.Dropdown => $"{d.Label} must be one of: {string.Join(", ", d.Options ?? [])}.",
                    _ => $"{d.Label} is too long (max 500 characters).",
                }];
                continue;
            }

            if (d.ValidationRegex is { Length: > 0 } rx && d.FieldType == CustomFieldTypes.Text &&
                !SafeMatch(normalised, rx))
            {
                errors[$"custom.{d.Key}"] = [$"{d.Label} is not in the expected format."];
                continue;
            }
            values[d.Key] = normalised;
        }
        return (values, errors);
    }

    private static bool SafeMatch(string value, string pattern)
    {
        try { return Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)); }
        catch (ArgumentException) { return false; }
        catch (RegexMatchTimeoutException) { return false; }
    }
}

/// <summary>
/// Number patterns from settings, e.g. "C{SEQ:4}" → C0007, "INV/{FY}/{SEQ:5}" → INV/2026-27/00042.
/// Tokens: {SEQ} or {SEQ:n} (zero-padded), {FY} financial year, {YYYY}, {YY}, {MM}.
/// The pattern also decides when numbering restarts: monthly if it has {MM}, yearly if {FY}/{YYYY}/{YY}, else never.
/// </summary>
public static class NumberFormat
{
    private static readonly Regex Seq = new(@"\{SEQ(?::([1-9]))?\}", RegexOptions.Compiled);

    public static string FinancialYear(DateOnly date, int fyStartMonth)
    {
        var start = date.Month >= fyStartMonth ? date.Year : date.Year - 1;
        return fyStartMonth == 1 ? start.ToString(CultureInfo.InvariantCulture) : $"{start}-{(start + 1) % 100:D2}";
    }

    public static string Period(string pattern, DateOnly date, int fyStartMonth) =>
        pattern.Contains("{MM}") ? $"{date:yyyy-MM}"
        : pattern.Contains("{FY}") ? FinancialYear(date, fyStartMonth)
        : pattern.Contains("{YYYY}") || pattern.Contains("{YY}") ? date.Year.ToString(CultureInfo.InvariantCulture)
        : "";

    public static string Format(string pattern, long seq, DateOnly date, int fyStartMonth)
    {
        var s = Seq.Replace(pattern, m => m.Groups[1].Success
            ? seq.ToString(CultureInfo.InvariantCulture).PadLeft(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), '0')
            : seq.ToString(CultureInfo.InvariantCulture));
        return s.Replace("{FY}", FinancialYear(date, fyStartMonth))
            .Replace("{YYYY}", date.Year.ToString("D4", CultureInfo.InvariantCulture))
            .Replace("{YY}", (date.Year % 100).ToString("D2", CultureInfo.InvariantCulture))
            .Replace("{MM}", date.Month.ToString("D2", CultureInfo.InvariantCulture));
    }
}
