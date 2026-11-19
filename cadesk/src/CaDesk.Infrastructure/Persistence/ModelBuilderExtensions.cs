using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaDesk.Infrastructure.Persistence;

public static class NamingExtensions
{
    /// <summary>"AssignedUserId" -> "assigned_user_id", "TaxIds" -> "tax_ids".</summary>
    public static string ToSnakeCase(this string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch))
            {
                var prevLower = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var nextLower = i > 0 && i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]);
                if (prevLower || nextLower) sb.Append('_');
                sb.Append(char.ToLowerInvariant(ch));
            }
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}

public static class JsonbExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Stores a string dictionary in a jsonb column, with change tracking that compares contents.</summary>
    public static PropertyBuilder<Dictionary<string, string>> HasJsonbDictionary(
        this PropertyBuilder<Dictionary<string, string>> property)
    {
        var comparer = new ValueComparer<Dictionary<string, string>>(
            // same entries in any order = equal (no needless UPDATE)
            (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
            v => v.Count,
            v => new Dictionary<string, string>(v));

        property.HasConversion(
                v => JsonSerializer.Serialize(v, Options),
                s => string.IsNullOrEmpty(s)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(s, Options) ?? new Dictionary<string, string>(),
                comparer)
            .HasColumnType("jsonb");
        return property;
    }

    /// <summary>Stores an optional string list (e.g. dropdown options) in a jsonb column.</summary>
    public static PropertyBuilder<List<string>?> HasJsonbList(this PropertyBuilder<List<string>?> property)
    {
        var comparer = new ValueComparer<List<string>?>(
            (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
            v => v == null ? 0 : v.Count,
            v => v == null ? null : new List<string>(v));

        property.HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, Options),
                s => string.IsNullOrEmpty(s) ? null : JsonSerializer.Deserialize<List<string>>(s, Options),
                comparer)
            .HasColumnType("jsonb");
        return property;
    }

    /// <summary>Stores a non-null string list in a jsonb column.</summary>
    public static PropertyBuilder<List<string>> HasJsonbStringList(this PropertyBuilder<List<string>> property)
    {
        var comparer = new ValueComparer<List<string>>(
            (a, b) => a!.SequenceEqual(b!), v => v.Count, v => new List<string>(v));
        property.HasConversion(
                v => JsonSerializer.Serialize(v, Options),
                s => string.IsNullOrEmpty(s) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(s, Options) ?? new List<string>(),
                comparer)
            .HasColumnType("jsonb");
        return property;
    }
}
