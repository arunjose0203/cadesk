using System.Globalization;
using System.Text;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Clients;
using ClosedXML.Excel;

namespace CaDesk.Api.Clients;

/// <summary>Reads an uploaded .csv / .xlsx into an ImportTable, and writes the import template.</summary>
public static class SpreadsheetFiles
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static async Task<ImportTable> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new RequestValidationException("file", "Choose a .xlsx or .csv file.");
        if (file.Length > MaxBytes) throw new RequestValidationException("file", "The file is larger than 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        return ext switch
        {
            ".csv" or ".txt" => CsvParser.Parse(Encoding.UTF8.GetString(buffer.ToArray())),
            ".xlsx" => ReadXlsx(buffer),
            _ => throw new RequestValidationException("file", "Only .xlsx and .csv files can be imported (save .xls files as .xlsx first)."),
        };
    }

    private static ImportTable ReadXlsx(Stream s)
    {
        // a 5 MB .xlsx can unpack to gigabytes: check the unpacked size before ClosedXML loads it
        try
        {
            using var zip = new System.IO.Compression.ZipArchive(s, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Sum(e => e.Length) > 60L * 1024 * 1024)
                throw new RequestValidationException("file", "This workbook is too large. Keep only the client rows and try again.");
        }
        catch (InvalidDataException) { throw new RequestValidationException("file", "This file couldn't be opened as an Excel workbook."); }
        s.Position = 0;

        XLWorkbook wb;
        try { wb = new XLWorkbook(s); }
        catch (Exception) { throw new RequestValidationException("file", "This file couldn't be opened as an Excel workbook."); }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault();
            var used = ws?.RangeUsed();
            if (ws is null || used is null) return new ImportTable([], []);

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var lastCol = Math.Min(used.LastColumn().ColumnNumber(), 100);
            if (lastRow - firstRow > ClientImportService.MaxRows)
                throw new RequestValidationException("file", $"A file can have at most {ClientImportService.MaxRows} clients. Split it into smaller files.");

            string[] ReadRow(int r) => Enumerable.Range(1, lastCol).Select(c => CellText(ws.Cell(r, c))).ToArray();
            var headers = ReadRow(firstRow).Select(h => h.Trim()).ToList();
            var rows = new List<string[]>();
            for (var r = firstRow + 1; r <= lastRow; r++) rows.Add(ReadRow(r));
            return new ImportTable(headers, rows);
        }
    }

    private static string CellText(IXLCell cell)
    {
        var v = cell.Value;
        return v.Type switch
        {
            XLDataType.Blank => "",
            XLDataType.Text => v.GetText(),
            // 15-digit TRNs and phone numbers typed as numbers come back whole, not as 1.0E+14
            XLDataType.Number => v.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture),
            XLDataType.DateTime => v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Boolean => v.GetBoolean() ? "yes" : "no",
            _ => cell.GetFormattedString(),
        };
    }

    /// <summary>Template workbook: header row (all columns as text, so PAN / phone keep leading zeros) + a help sheet.</summary>
    public static byte[] TemplateXlsx(IReadOnlyList<string> headers, ClientFormInfo info)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Clients");
        for (var i = 0; i < headers.Count; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        ws.Range(1, 1, ClientImportService.MaxRows + 1, headers.Count).Style.NumberFormat.Format = "@";
        ws.Columns(1, headers.Count).Width = 22;
        ws.SheetView.FreezeRows(1);

        var help = wb.Worksheets.Add("Help");
        var lines = new List<(string, string)>
        {
            ("Code", $"Leave empty to number automatically ({info.NextCodeExample}, ...)."),
            ("Name", "Required."),
            ("Country", $"2-letter code. Empty = {info.FirmCountry}."),
            ("Entity type", string.Join(", ", info.EntityTypes)),
            ("Required", info.RequiredFields.Count == 0 ? "Nothing extra" : string.Join(", ", info.RequiredFields)),
            ("Group", "Family or business group name. New names are created."),
            ("Assigned to (email)", "Email of a team member."),
            ("Active", "yes / no. Empty = yes."),
        };
        lines.AddRange(info.TaxIdTypes.Select(t => (t.Code, $"{t.Label}, e.g. {t.Example}")));
        lines.AddRange(info.CustomFields.Select(f => (f.Label,
            (f.IsRequired ? "Required. " : "") + f.FieldType switch
            {
                CaDesk.Domain.Clients.CustomFieldTypes.Dropdown => "One of: " + string.Join(", ", f.Options ?? []),
                CaDesk.Domain.Clients.CustomFieldTypes.Date => "Date, yyyy-mm-dd or dd-mm-yyyy",
                CaDesk.Domain.Clients.CustomFieldTypes.Bool => "yes / no",
                CaDesk.Domain.Clients.CustomFieldTypes.Number => "Number",
                _ => "Text",
            })));
        help.Cell(1, 1).Value = "Column";
        help.Cell(1, 2).Value = "How to fill it";
        help.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < lines.Count; i++)
        {
            help.Cell(i + 2, 1).Value = lines[i].Item1;
            help.Cell(i + 2, 2).Value = lines[i].Item2;
        }
        help.Column(1).Width = 24;
        help.Column(2).Width = 80;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
