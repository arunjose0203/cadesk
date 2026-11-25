using System.Text;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Clients;

/// <summary>A sheet read from CSV or Excel: the first row is headers.</summary>
public sealed record ImportTable(IReadOnlyList<string> Headers, IReadOnlyList<string[]> Rows);

public sealed record ImportRowResult(int Row, string? Name, bool Ok, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public sealed record ImportPreview(int TotalRows, int ValidRows, int ErrorRows, IReadOnlyList<string> IgnoredColumns,
    IReadOnlyList<string> NewGroups, IReadOnlyList<ImportRowResult> Rows);

public sealed record ImportResult(int Created, int Skipped, ImportPreview Preview);

/// <summary>
/// Bulk client import. Preview checks every row with the same rules as the client form and saves nothing.
/// Commit saves all valid rows in one transaction (all or nothing), creating missing groups by name.
/// </summary>
public sealed class ClientImportService(IAppDbContext db, ClientService clients, CaDesk.Application.Subscriptions.PlanAccessService plans)
{
    public const int MaxRows = 2000;

    private static readonly Dictionary<string, string> FixedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code", ["client code"] = "code",
        ["name"] = "name", ["client name"] = "name",
        ["legal name"] = "legalName",
        ["country"] = "country", ["country code"] = "country",
        ["entity type"] = "entityType", ["type"] = "entityType",
        ["email"] = "email", ["phone"] = "phone", ["mobile"] = "phone",
        ["group"] = "group", ["family / group"] = "group",
        ["assigned to"] = "assigned", ["assigned to (email)"] = "assigned",
        ["active"] = "active",
    };

    /// <summary>Header row for the template: fixed columns, the firm country's tax IDs, then custom fields.</summary>
    public async Task<IReadOnlyList<string>> TemplateHeadersAsync(CancellationToken ct)
    {
        var rules = await clients.LoadRulesAsync(ct);
        var tax = await rules.TaxTypesAsync(rules.FirmCountry, ct);
        var custom = rules.CustomFields.Where(d => d.IsActive).OrderBy(d => d.SortOrder).ThenBy(d => d.Label);
        return ["Code", "Name", "Legal name", "Country", "Entity type", .. tax.Select(t => t.Code), "Email", "Phone",
            "Group", "Assigned to (email)", "Active", .. custom.Select(d => d.Label)];
    }

    public static string TemplateCsv(IReadOnlyList<string> headers) =>
        string.Join(",", headers.Select(CsvParser.Quote)) + "\r\n";

    public async Task<ImportPreview> PreviewAsync(ImportTable table, CancellationToken ct) =>
        (await CheckAsync(table, ct)).Preview;

    public async Task<ImportResult> CommitAsync(ImportTable table, bool skipInvalid, CancellationToken ct)
    {
        var check = await CheckAsync(table, ct);
        if (check.Preview.ErrorRows > 0 && !skipInvalid)
            return new ImportResult(0, check.Preview.TotalRows, check.Preview);
        var valid = check.Rows.Where(r => r.Prepared.Errors.Count == 0).ToList();
        if (valid.Count == 0) return new ImportResult(0, check.Preview.TotalRows, check.Preview);
        await plans.EnsureClientRoomAsync(valid.Count, ct);   // the plan's client limit, before anything is saved

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // groups by name: existing ones were matched in the check; create the rest once each
        var groupIds = new Dictionary<string, Guid>(check.ExistingGroups, StringComparer.OrdinalIgnoreCase);
        foreach (var name in check.Preview.NewGroups)
        {
            var g = new ClientGroup { Name = name };
            db.ClientGroups.Add(g);
            groupIds[name] = g.Id;
        }
        await db.SaveChangesAsync(ct);   // groups first: clients reference them by foreign key

        // numbers are taken inside the transaction, so a failed import leaves no gaps
        var fileCodes = valid.Where(v => v.Prepared.Code != null).Select(v => v.Prepared.Code!).ToHashSet();
        foreach (var v in valid)
        {
            if (v.GroupName is { } gn) v.Prepared.GroupId = groupIds[gn];
            var c = new Client();
            v.Prepared.ApplyTo(c);
            c.Code = v.Prepared.Code ?? await clients.NextFreeCodeAsync(check.Rules, fileCodes, ct);
            fileCodes.Add(c.Code);
            db.Clients.Add(c);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new ImportResult(valid.Count, check.Preview.TotalRows - valid.Count, check.Preview);
    }

    // ------------------------------------------------------------ checking

    private sealed record CheckedRow(int Row, PreparedClient Prepared, string? GroupName);
    private sealed record CheckResult(ImportPreview Preview, List<CheckedRow> Rows, ClientRulesSnapshot Rules,
        Dictionary<string, Guid> ExistingGroups);

    private async Task<CheckResult> CheckAsync(ImportTable table, CancellationToken ct)
    {
        if (table.Rows.Count > MaxRows)
            throw new RequestValidationException("file", $"A file can have at most {MaxRows} clients. Split it into smaller files.");

        var rules = await clients.LoadRulesAsync(ct);
        var allTaxCodes = await db.TaxIdTypes.AsNoTracking().Select(t => t.Code).Distinct().ToListAsync(ct);
        var uniqueTax = await db.TaxIdTypes.AsNoTracking().Where(t => t.IsUnique).Select(t => t.Code).Distinct().ToListAsync(ct);

        // map columns
        var map = new Dictionary<int, (string Kind, string Key)>();
        var ignored = new List<string>();
        for (var i = 0; i < table.Headers.Count; i++)
        {
            var h = table.Headers[i].Trim().TrimEnd('*').Trim();
            if (h.Length == 0) continue;
            (string Kind, string Key)? target =
                FixedColumns.TryGetValue(h, out var f) ? ("field", f)
                : allTaxCodes.FirstOrDefault(c => c.Equals(h, StringComparison.OrdinalIgnoreCase)) is { } tc ? ("tax", tc)
                : rules.CustomFields.FirstOrDefault(d => d.IsActive &&
                    (d.Label.Equals(h, StringComparison.OrdinalIgnoreCase) || d.Key.Equals(h, StringComparison.OrdinalIgnoreCase))) is { } cf
                    ? ("custom", cf.Key)
                : null;
            if (target is null) ignored.Add(h);
            else if (map.ContainsValue(target.Value)) ignored.Add($"{h} (second column for the same field)");
            else map[i] = target.Value;
        }
        if (!map.Values.Contains(("field", "name")))
            throw new RequestValidationException("file", "The file needs a 'Name' column. Download the template to see the columns.");

        var existingGroups = (await db.ClientGroups.AsNoTracking().ToListAsync(ct))
            .ToDictionary(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase);
        var users = (await db.Users.AsNoTracking().Where(u => u.IsActive).Select(u => new { u.Id, u.Email }).ToListAsync(ct))
            .ToDictionary(u => u.Email, u => u.Id, StringComparer.OrdinalIgnoreCase);

        var results = new List<ImportRowResult>();
        var checkedRows = new List<CheckedRow>();
        var newGroups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var codesInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var taxInFile = new Dictionary<(string, string), int>();

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var cells = table.Rows[r];
            var rowNo = r + 2;   // row 1 is the header
            if (cells.All(string.IsNullOrWhiteSpace)) continue;

            string? Get(string key) => map.Where(m => m.Value == ("field", key)).Select(m => Cell(cells, m.Key)).FirstOrDefault(v => v != null);
            var errors = new List<string>();
            var warnings = new List<string>();

            var taxIds = map.Where(m => m.Value.Kind == "tax").Select(m => (m.Value.Key, V: Cell(cells, m.Key)))
                .Where(x => x.V != null).ToDictionary(x => x.Key, x => x.V!);
            var custom = map.Where(m => m.Value.Kind == "custom").Select(m => (m.Value.Key, V: Cell(cells, m.Key)))
                .Where(x => x.V != null).ToDictionary(x => x.Key, x => x.V!);

            Guid? assigned = null;
            if (Get("assigned") is { } who)
            {
                if (users.TryGetValue(who, out var uid)) assigned = uid;
                else errors.Add($"Assigned to: no active user with email {who}.");
            }

            bool? active = null;
            if (Get("active") is { } act)
            {
                active = act.ToLowerInvariant() switch { "yes" or "y" or "true" or "1" => true, "no" or "n" or "false" or "0" => false, _ => null };
                if (active is null) errors.Add("Active must be yes or no.");
            }

            string? groupName = null;
            Guid? groupId = null;
            if (Get("group") is { } gname)
            {
                if (gname.Length > 120) errors.Add("Group name can be at most 120 characters.");
                else if (existingGroups.TryGetValue(gname, out var gid)) groupId = gid;
                else groupName = newGroups.TryGetValue(gname, out var same) ? same : gname;
            }

            var req = new SaveClientRequest(Get("code"), Get("name") ?? "", Get("legalName"), Get("country"), Get("entityType"),
                taxIds, Get("email"), Get("phone"), active, null, groupId, assigned, custom);
            var prepared = await clients.PrepareAsync(req, existing: null, rules, ct, skipGroupCheck: true);
            errors.AddRange(prepared.Errors.SelectMany(e => e.Value));
            warnings.AddRange(prepared.Warnings);

            // duplicates inside the file
            if (prepared.Code is { } code)
            {
                if (codesInFile.TryGetValue(code, out var first)) errors.Add($"Code {code} is also on row {first}.");
                else codesInFile[code] = rowNo;
            }
            foreach (var (k, v) in prepared.TaxIds)
            {
                if (taxInFile.TryGetValue((k, v), out var first))
                {
                    if (uniqueTax.Contains(k)) errors.Add($"{k} {v} is also on row {first}.");
                    else warnings.Add($"{k} {v} is also on row {first}.");
                }
                else taxInFile[(k, v)] = rowNo;
            }

            var ok = errors.Count == 0;
            if (ok && groupName != null) newGroups.Add(groupName);
            // keep the row's own errors on the prepared object so commit can tell valid rows apart
            if (!ok && prepared.Errors.Count == 0) prepared.Errors["row"] = errors.ToArray();
            results.Add(new ImportRowResult(rowNo, prepared.Name.Length > 0 ? prepared.Name : null, ok, errors, warnings));
            checkedRows.Add(new CheckedRow(rowNo, prepared, groupName));
        }

        var valid = results.Count(x => x.Ok);
        var preview = new ImportPreview(results.Count, valid, results.Count - valid, ignored, newGroups.ToList(), results);
        return new CheckResult(preview, checkedRows, rules, existingGroups);
    }

    private static string? Cell(string[] cells, int i) =>
        i < cells.Length && !string.IsNullOrWhiteSpace(cells[i]) ? cells[i].Trim() : null;
}

/// <summary>RFC 4180 CSV: quoted fields, "" escapes, CRLF or LF, UTF-8 BOM. Semicolon files (Excel in some locales) too.</summary>
public static class CsvParser
{
    public static ImportTable Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var firstLine = text.Split('\n', 2)[0];
        var sep = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';

        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else if (ch == '"' && field.Length == 0) inQuotes = true;
            else if (ch == sep) { row.Add(field.ToString()); field.Clear(); }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row.ToArray()); row.Clear();
            }
            else field.Append(ch);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
        rows.RemoveAll(r => r.All(string.IsNullOrWhiteSpace));

        return rows.Count == 0
            ? new ImportTable([], [])
            : new ImportTable(rows[0].Select(h => h.Trim()).ToList(), rows.Skip(1).ToList());
    }

    /// <summary>Quotes a value when needed. Values starting with = + - @ get a leading ' so Excel won't run them as formulas.</summary>
    public static string Quote(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n', ';']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
