using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using CaDesk.Application.Clients;

namespace CaDesk.Application.Documents;

/// <summary>
/// What a file really is. The extension must be one the firm allows AND the content must match it,
/// so a renamed program or web page can't be stored as "invoice.pdf". Office files with macros are refused.
/// </summary>
public static class FileTypes
{
    /// <summary>Extension → (code used in doc.allowed_types, content type sent on download).</summary>
    private static readonly Dictionary<string, (string Code, string ContentType)> Known = new(StringComparer.Ordinal)
    {
        ["pdf"] = ("pdf", "application/pdf"),
        ["jpg"] = ("jpg", "image/jpeg"),
        ["jpeg"] = ("jpg", "image/jpeg"),
        ["png"] = ("png", "image/png"),
        ["xlsx"] = ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        ["xls"] = ("xls", "application/vnd.ms-excel"),
        ["csv"] = ("csv", "text/csv"),
        ["docx"] = ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
    };

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> Ole => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static ReadOnlySpan<byte> Zip => [0x50, 0x4B, 0x03, 0x04];

    /// <summary>"Scan 01.PDF" → "pdf"; "" when there is none.</summary>
    public static string ExtensionOf(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var dot = name.LastIndexOf('.');
        return dot <= 0 || dot == name.Length - 1 ? "" : name[(dot + 1)..].ToLowerInvariant();
    }

    /// <summary>The doc.allowed_types code for an extension (jpeg → jpg), or null if it isn't a type the vault knows.</summary>
    public static string? CodeOf(string extension) => Known.TryGetValue(extension, out var k) ? k.Code : null;

    public static string ContentTypeOf(string extension) =>
        Known.TryGetValue(extension, out var k) ? k.ContentType : "application/octet-stream";

    /// <summary>Null when the content matches the extension; otherwise why not.</summary>
    public static string? CheckContent(string extension, Stream content)
    {
        var head = new byte[8192];
        var n = ReadUpTo(content, head);
        ReadOnlySpan<byte> h = head.AsSpan(0, n);
        switch (CodeOf(extension))
        {
            case "pdf":
                return h.IndexOf("%PDF-"u8) is >= 0 and < 1024 ? null : "This is not a PDF file.";
            case "jpg":
                return h.StartsWith(Jpeg) ? null : "This is not a JPEG image.";
            case "png":
                return h.StartsWith(Png) ? null : "This is not a PNG image.";
            case "xls":
                return h.StartsWith(Ole) ? null : "This is not an Excel 97-2003 file.";
            case "csv":
                if (h.IndexOf((byte)0) >= 0) return "This is not a text (CSV) file.";
                var text = Encoding.UTF8.GetString(h).TrimStart('﻿', ' ', '\t', '\r', '\n');
                return text.StartsWith('<') ? "This looks like a web page, not a CSV file." : null;
            case "xlsx":
            case "docx":
                if (!h.StartsWith(Zip)) return $"This is not a valid .{extension} file.";
                content.Position = 0;
                return CheckOfficePackage(extension, content);
            default:
                return "This file type is not supported.";
        }
    }

    /// <summary>A real Office Open XML package of the right kind, without macros.</summary>
    private static string? CheckOfficePackage(string extension, Stream content)
    {
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            if (names.Count > 10_000) return "This file has too many parts.";
            if (!names.Contains("[Content_Types].xml")) return $"This is not a valid .{extension} file.";
            var main = extension == "xlsx" ? "xl/workbook.xml" : "word/document.xml";
            if (!names.Contains(main)) return $"This is not a valid .{extension} file.";
            if (names.Any(n => n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                return "Files with macros are not accepted. Save it without macros (.xlsx / .docx) and upload again.";
            return null;
        }
        catch (InvalidDataException)
        {
            return $"This is not a valid .{extension} file.";
        }
    }

    private static int ReadUpTo(Stream s, byte[] buffer)
    {
        var total = 0;
        int read;
        while (total < buffer.Length && (read = s.Read(buffer, total, buffer.Length - total)) > 0) total += read;
        return total;
    }
}

public static partial class DocumentRules
{
    public const int MaxNameLength = 200;
    public const int MaxFileNameLength = 255;

    [GeneratedRegex(@"[\x00-\x1F\x7F<>:""/\\|?*]")]
    private static partial Regex UnsafeChars();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(?<start>[0-9]{4})(-(?<end>[0-9]{2}))?$")]
    private static partial Regex FyPattern();

    /// <summary>The name as given, without any folder part or characters that are unsafe in file names.</summary>
    public static string SafeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        name = Spaces().Replace(UnsafeChars().Replace(name, "_"), " ").Trim().Trim('.', ' ');
        if (name.Length == 0) name = "file";
        if (name.Length > MaxFileNameLength)
        {
            var ext = FileTypes.ExtensionOf(name);
            var keep = MaxFileNameLength - (ext.Length + 1);
            name = ext.Length > 0 && keep > 0 ? name[..keep].TrimEnd() + "." + ext : name[..MaxFileNameLength];
        }
        return name;
    }

    /// <summary>Shown in the vault: the given name, or the file name without its extension.</summary>
    public static string DisplayName(string? name, string safeFileName)
    {
        var n = Spaces().Replace(name ?? "", " ").Trim();
        if (n.Length == 0)
        {
            var ext = FileTypes.ExtensionOf(safeFileName);
            n = ext.Length > 0 ? safeFileName[..^(ext.Length + 1)] : safeFileName;
        }
        if (n.Length == 0) n = "Document";
        return n.Length > MaxNameLength ? n[..MaxNameLength].TrimEnd() : n;
    }

    /// <summary>Financial year label for a date: 2026-27 (April start) or 2026 (calendar year).</summary>
    public static string FyOf(DateOnly date, int fyStartMonth) => NumberFormat.FinancialYear(date, fyStartMonth);

    /// <summary>Is this a year label in the firm's style (2026-27 for April–March, 2026 for January–December)?</summary>
    public static bool IsValidFy(string? fy, int fyStartMonth)
    {
        if (fy is null) return false;
        var m = FyPattern().Match(fy);
        if (!m.Success) return false;
        var start = int.Parse(m.Groups["start"].Value, CultureInfo.InvariantCulture);
        if (start is < 1990 or > 2100) return false;
        if (fyStartMonth == 1) return !m.Groups["end"].Success;
        return m.Groups["end"].Success && int.Parse(m.Groups["end"].Value, CultureInfo.InvariantCulture) == (start + 1) % 100;
    }

    /// <summary>Last day of a financial year label: 2026-27 (April start) → 31 Mar 2027.</summary>
    public static DateOnly FyEnd(string fy, int fyStartMonth)
    {
        var start = int.Parse(fy[..4], CultureInfo.InvariantCulture);
        return new DateOnly(start, fyStartMonth, 1).AddYears(1).AddDays(-1);
    }

    /// <summary>The years offered when uploading: this year, the coming one and the last few.</summary>
    public static IReadOnlyList<string> RecentYears(DateOnly today, int fyStartMonth, int back = 7)
    {
        var list = new List<string>();
        for (var i = -1; i <= back; i++) list.Add(FyOf(today.AddYears(-i), fyStartMonth));
        return list.Distinct().ToList();
    }

    /// <summary>Where the bytes go. Only ids and dates — nothing a user typed.</summary>
    public static string StorageKey(Guid firmId, Guid documentId, DateTime uploadedAt) =>
        $"{firmId:N}/{uploadedAt:yyyy}/{uploadedAt:MM}/{documentId:N}";

    public static string SizeText(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB",
    };
}
