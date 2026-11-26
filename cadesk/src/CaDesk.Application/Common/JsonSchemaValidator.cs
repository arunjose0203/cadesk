using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CaDesk.Application.Common;

public sealed record JsonSchemaValidationError(string Path, string Keyword, string Message);

/// <summary>
/// Lightweight JSON Schema Draft-04 / Draft-07 validator covering the constraints used in
/// official CBDT ITR-1, ITR-4, and Form 3CA/3CB schemas ($ref, allOf, type, required,
/// properties, additionalProperties, items, minItems, maxItems, enum, pattern, length, numeric bounds).
/// </summary>
public static class JsonSchemaValidator
{
    public static JsonObject LoadEmbeddedSchema(string resourceSuffix)
    {
        var asm = typeof(JsonSchemaValidator).Assembly;
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Embedded schema resource ending with '{resourceSuffix}' was not found.");

        using var stream = asm.GetManifestResourceStream(resName)!;
        using var doc = JsonDocument.Parse(stream);
        return JsonNode.Parse(doc.RootElement.GetRawText())!.AsObject();
    }

    public static List<JsonSchemaValidationError> Validate(JsonNode? instance, JsonObject schema)
    {
        var errors = new List<JsonSchemaValidationError>();
        ValidateNode(instance, schema, schema, "$", errors, depth: 0);
        return errors;
    }

    private static void ValidateNode(
        JsonNode? value,
        JsonObject nodeSchema,
        JsonObject rootSchema,
        string path,
        List<JsonSchemaValidationError> errors,
        int depth)
    {
        if (depth > 64) return;

        // Resolve $ref
        if (nodeSchema.TryGetPropertyValue("$ref", out var refNode) && refNode?.GetValue<string>() is { } refPtr)
        {
            var resolved = ResolvePointer(rootSchema, refPtr);
            if (resolved is not null)
                ValidateNode(value, resolved, rootSchema, path, errors, depth + 1);
            // In Draft-04 siblings next to $ref are ignored, though allOf handles combinations.
            return;
        }

        // Handle allOf
        if (nodeSchema.TryGetPropertyValue("allOf", out var allOfNode) && allOfNode is JsonArray allOfArr)
        {
            foreach (var sub in allOfArr.OfType<JsonObject>())
                ValidateNode(value, sub, rootSchema, path, errors, depth + 1);
        }

        // Check enum
        if (nodeSchema.TryGetPropertyValue("enum", out var enumNode) && enumNode is JsonArray enumArr)
        {
            var raw = value?.ToJsonString();
            var matched = enumArr.Any(e => JsonNode.DeepEquals(e, value)
                || (e is not null && value is not null && e.ToJsonString() == raw));
            if (!matched)
            {
                errors.Add(new(path, "enum", $"Value {raw ?? "null"} is not in allowed schema enum."));
                return;
            }
        }

        // Check type
        if (nodeSchema.TryGetPropertyValue("type", out var typeNode) && typeNode is not null)
        {
            if (!CheckType(value, typeNode, out var expectedType))
            {
                errors.Add(new(path, "type", $"Expected JSON type '{expectedType}' at {path}."));
                return;
            }
        }

        if (value is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var strVal))
            {
                if (TryGetInt(nodeSchema, "minLength", out var minLen) && strVal.Length < minLen)
                    errors.Add(new(path, "minLength", $"String length {strVal.Length} is less than minLength {minLen}."));
                if (TryGetInt(nodeSchema, "maxLength", out var maxLen) && strVal.Length > maxLen)
                    errors.Add(new(path, "maxLength", $"String length {strVal.Length} exceeds maxLength {maxLen}."));
                if (nodeSchema.TryGetPropertyValue("pattern", out var patNode) && patNode?.GetValue<string>() is { } pat)
                {
                    if (!RegexMatchesFullOrPattern(strVal, pat))
                        errors.Add(new(path, "pattern", $"Value '{strVal}' does not match schema pattern '{pat}'."));
                }
            }
            else if (TryGetDecimal(jv, out var numVal))
            {
                if (TryGetDecimalProp(nodeSchema, "minimum", out var min))
                {
                    var excl = nodeSchema.TryGetPropertyValue("exclusiveMinimum", out var em) && em is JsonValue emv && emv.TryGetValue<bool>(out var b) && b;
                    if (excl ? numVal <= min : numVal < min)
                        errors.Add(new(path, "minimum", $"Value {numVal} violates minimum {min}."));
                }
                if (TryGetDecimalProp(nodeSchema, "maximum", out var max))
                {
                    var excl = nodeSchema.TryGetPropertyValue("exclusiveMaximum", out var em) && em is JsonValue emv && emv.TryGetValue<bool>(out var b) && b;
                    if (excl ? numVal >= max : numVal > max)
                        errors.Add(new(path, "maximum", $"Value {numVal} exceeds maximum {max}."));
                }
                if (TryGetDecimalProp(nodeSchema, "multipleOf", out var mult) && mult > 0m)
                {
                    if (numVal % mult != 0m)
                        errors.Add(new(path, "multipleOf", $"Value {numVal} is not a multiple of {mult}."));
                }
                // CBDT Draft-04 schemas sometimes attach string regex patterns to integer types (e.g. PinCode/Status)
                if (nodeSchema.TryGetPropertyValue("pattern", out var patNode) && patNode?.GetValue<string>() is { } pat)
                {
                    var numStr = jv.ToJsonString();
                    if (!RegexMatchesFullOrPattern(numStr, pat))
                        errors.Add(new(path, "pattern", $"Numeric value {numStr} does not match schema pattern '{pat}'."));
                }
            }
        }
        else if (value is JsonObject obj)
        {
            if (nodeSchema.TryGetPropertyValue("required", out var reqNode) && reqNode is JsonArray reqArr)
            {
                foreach (var r in reqArr)
                {
                    var key = r?.GetValue<string>();
                    if (key is not null && (!obj.TryGetPropertyValue(key, out var propVal) || propVal is null))
                        errors.Add(new($"{path}.{key}", "required", $"Missing required property '{key}'."));
                }
            }

            nodeSchema.TryGetPropertyValue("properties", out var propsNode);
            var propsObj = propsNode as JsonObject;
            var forbidAdditional = nodeSchema.TryGetPropertyValue("additionalProperties", out var apNode)
                && apNode is JsonValue apv && apv.TryGetValue<bool>(out var apBool) && !apBool;

            foreach (var kv in obj)
            {
                if (propsObj is not null && propsObj.TryGetPropertyValue(kv.Key, out var propSchema) && propSchema is JsonObject psObj)
                {
                    ValidateNode(kv.Value, psObj, rootSchema, $"{path}.{kv.Key}", errors, depth + 1);
                }
                else if (forbidAdditional)
                {
                    errors.Add(new($"{path}.{kv.Key}", "additionalProperties", $"Property '{kv.Key}' is not allowed by schema."));
                }
            }
        }
        else if (value is JsonArray arr)
        {
            if (TryGetInt(nodeSchema, "minItems", out var minItems) && arr.Count < minItems)
                errors.Add(new(path, "minItems", $"Array count {arr.Count} is less than minItems {minItems}."));
            if (TryGetInt(nodeSchema, "maxItems", out var maxItems) && arr.Count > maxItems)
                errors.Add(new(path, "maxItems", $"Array count {arr.Count} exceeds maxItems {maxItems}."));

            if (nodeSchema.TryGetPropertyValue("items", out var itemsNode) && itemsNode is JsonObject itemSchema)
            {
                for (var i = 0; i < arr.Count; i++)
                    ValidateNode(arr[i], itemSchema, rootSchema, $"{path}[{i}]", errors, depth + 1);
            }
        }
    }

    private static JsonObject? ResolvePointer(JsonObject rootSchema, string refPtr)
    {
        if (!refPtr.StartsWith("#/", StringComparison.Ordinal)) return null;
        JsonNode? cur = rootSchema;
        foreach (var part in refPtr[2..].Split('/'))
        {
            var key = part.Replace("~1", "/").Replace("~0", "~");
            if (cur is JsonObject obj && obj.TryGetPropertyValue(key, out var next))
                cur = next;
            else
                return null;
        }
        return cur as JsonObject;
    }

    private static bool CheckType(JsonNode? node, JsonNode typeNode, out string expected)
    {
        var allowed = typeNode switch
        {
            JsonValue tv when tv.TryGetValue<string>(out var s) => [s],
            JsonArray ta => ta.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToArray(),
            _ => []
        };
        expected = string.Join("|", allowed);
        if (allowed.Length == 0 || node is null) return allowed.Contains("null");

        foreach (var t in allowed)
        {
            var ok = t switch
            {
                "object" => node is JsonObject,
                "array" => node is JsonArray,
                "string" => node is JsonValue jv && jv.GetValueKind() == JsonValueKind.String,
                "boolean" => node is JsonValue jv && (jv.GetValueKind() == JsonValueKind.True || jv.GetValueKind() == JsonValueKind.False),
                "integer" => node is JsonValue jv && IsIntegerNode(jv),
                "number" => node is JsonValue jv && jv.GetValueKind() == JsonValueKind.Number,
                "null" => false,
                _ => true
            };
            if (ok) return true;
        }
        return false;
    }

    private static bool IsIntegerNode(JsonValue jv)
    {
        if (jv.GetValueKind() != JsonValueKind.Number) return false;
        if (jv.TryGetValue<int>(out _) || jv.TryGetValue<long>(out _)) return true;
        if (jv.TryGetValue<decimal>(out var d)) return d == Math.Truncate(d);
        return false;
    }

    private static bool TryGetDecimal(JsonValue jv, out decimal d)
    {
        d = 0m;
        if (jv.GetValueKind() != JsonValueKind.Number) return false;
        if (jv.TryGetValue(out d)) return true;
        if (jv.TryGetValue<int>(out var iv)) { d = iv; return true; }
        if (jv.TryGetValue<long>(out var lv)) { d = lv; return true; }
        if (jv.TryGetValue<double>(out var dbl)) { d = (decimal)dbl; return true; }
        return false;
    }

    private static bool TryGetInt(JsonObject obj, string key, out int val)
    {
        val = 0;
        return obj.TryGetPropertyValue(key, out var n) && n is JsonValue jv && jv.TryGetValue(out val);
    }

    private static bool TryGetDecimalProp(JsonObject obj, string key, out decimal val)
    {
        val = 0m;
        return obj.TryGetPropertyValue(key, out var n) && n is JsonValue jv && TryGetDecimal(jv, out val);
    }

    private static bool RegexMatchesFullOrPattern(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, $"^(?:{pattern})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch
        {
            return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
    }
}
