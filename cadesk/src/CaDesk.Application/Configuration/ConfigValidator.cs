using System.Text.Json;
using System.Text.RegularExpressions;

namespace CaDesk.Application.Configuration;

/// <summary>
/// Validates a setting value against the small JSON Schema subset the catalogue uses:
/// type (string, integer, number, boolean, array), enum, minimum, maximum, minLength, maxLength,
/// pattern, items, minItems, maxItems, uniqueItems. Returns plain-English problems; empty = valid.
/// </summary>
public static class ConfigValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static IReadOnlyList<string> Validate(JsonElement value, JsonElement schema)
    {
        var errors = new List<string>();
        Check(value, schema, "Value", errors);
        return errors;
    }

    private static void Check(JsonElement v, JsonElement s, string name, List<string> errors)
    {
        var type = Str(s, "type");
        var typeOk = type switch
        {
            "string" => v.ValueKind == JsonValueKind.String,
            "integer" => v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out _),
            "number" => v.ValueKind == JsonValueKind.Number,
            "boolean" => v.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" => v.ValueKind == JsonValueKind.Array,
            null => true,
            _ => false,
        };
        if (!typeOk)
        {
            errors.Add($"{name} must be {Describe(type)}.");
            return;
        }

        if (s.TryGetProperty("enum", out var options) && options.ValueKind == JsonValueKind.Array &&
            !options.EnumerateArray().Any(o => JsonEquals(o, v)))
            errors.Add($"{name} must be one of: {string.Join(", ", options.EnumerateArray().Select(Display))}.");

        if (v.ValueKind == JsonValueKind.Number)
        {
            if (!v.TryGetDecimal(out var n))
            {
                errors.Add($"{name} is out of range.");
                return;
            }
            if (Num(s, "minimum") is { } min && n < min) errors.Add($"{name} must be at least {min}.");
            if (Num(s, "maximum") is { } max && n > max) errors.Add($"{name} must be at most {max}.");
        }

        if (v.ValueKind == JsonValueKind.String)
        {
            var text = v.GetString()!;
            if (Num(s, "minLength") is { } minL && text.Length < minL) errors.Add($"{name} must have at least {minL} characters.");
            if (Num(s, "maxLength") is { } maxL && text.Length > maxL) errors.Add($"{name} must have at most {maxL} characters.");
            if (Str(s, "pattern") is { } pattern)
            {
                bool matches;
                try { matches = Regex.IsMatch(text, pattern, RegexOptions.None, RegexTimeout); }
                catch (RegexMatchTimeoutException) { matches = false; }
                if (!matches) errors.Add($"{name} is not in the expected format.");
            }
        }

        if (v.ValueKind == JsonValueKind.Array)
        {
            var items = v.EnumerateArray().ToList();
            if (Num(s, "minItems") is { } minI && items.Count < minI) errors.Add($"{name} needs at least {minI} item(s).");
            if (Num(s, "maxItems") is { } maxI && items.Count > maxI) errors.Add($"{name} can have at most {maxI} items.");
            if (s.TryGetProperty("uniqueItems", out var u) && u.ValueKind == JsonValueKind.True &&
                items.Select(i => i.GetRawText()).Distinct().Count() != items.Count)
                errors.Add($"{name} has repeated items.");
            if (s.TryGetProperty("items", out var itemSchema))
                for (var i = 0; i < items.Count; i++) Check(items[i], itemSchema, $"Item {i + 1}", errors);
        }
    }

    private static bool JsonEquals(JsonElement a, JsonElement b) =>
        a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number &&
        a.TryGetDecimal(out var x) && b.TryGetDecimal(out var y)
            ? x == y
            : a.GetRawText() == b.GetRawText();

    private static string Display(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText();

    private static string? Str(JsonElement s, string prop) =>
        s.ValueKind == JsonValueKind.Object && s.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static decimal? Num(JsonElement s, string prop) =>
        s.ValueKind == JsonValueKind.Object && s.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;

    private static string Describe(string? type) => type switch
    {
        "string" => "text",
        "integer" => "a whole number",
        "number" => "a number",
        "boolean" => "yes or no",
        "array" => "a list",
        _ => "valid",
    };
}
