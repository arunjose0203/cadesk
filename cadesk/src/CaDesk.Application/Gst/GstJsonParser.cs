using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CaDesk.Domain.Gst;

namespace CaDesk.Application.Gst;

public record ParsedGstr2BResult(
    string Gstin,
    string ReturnPeriod,
    List<Gstr2BInvoice> Invoices,
    decimal TotalTaxable,
    decimal TotalIgst,
    decimal TotalCgst,
    decimal TotalSgst,
    decimal TotalCess,
    decimal TotalItcAvailable,
    List<string>? Warnings = null
);

public record ParsedGstr1Result(
    string Gstin,
    string ReturnPeriod,
    decimal OutwardTaxableSupplies,
    decimal OutwardExemptedSupplies,
    decimal TotalTurnover,
    decimal TotalIgst,
    decimal TotalCgst,
    decimal TotalSgst,
    decimal TotalCess,
    List<string>? Warnings = null
);

/// <summary>
/// Reads the JSON files downloaded from the GST portal.
/// GSTR-2B: { "data": { "gstin", "rtnprd", "docdata": { "b2b": [ { "ctin", "trdnm", "inv": [ { "inum", "dt", "val", "pos",
/// "rev", "itcavl", "rsn", "txval", "igst", "cgst", "sgst", "cess" } ] } ], "cdnr": [ { "ctin", "trdnm", "nt": [ { "ntnum",
/// "dt", "typ", "txval", ... } ] } ] } } } (tax amounts sit on the document).
/// GSTR-1: { "gstin", "fp", "b2b": [ { "ctin", "inv": [ { "inum", "idt", "val", "itms": [ { "num", "itm_det": { "txval",
/// "iamt", "camt", "samt", "csamt" } } ] } ] } ], "b2cl", "b2cs", "exp", "cdnr", "nil" }.
/// Simple camelCase exports are accepted too. Nothing is invented: a document without a number is skipped and
/// reported, a missing date stays empty, and a file without a GSTIN or return period is refused.
/// </summary>
public static class GstJsonParser
{
    private static readonly string[] DateFormats = ["dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd/MM/yyyy"];

    public static ParsedGstr2BResult ParseGstr2B(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException("GSTR-2B JSON content cannot be empty.");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(jsonContent); }
        catch (JsonException ex) { throw new ArgumentException($"This is not a valid GSTR-2B JSON file: {ex.Message}", ex); }

        using (doc)
        {
            var data = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
            var docs = data.TryGetProperty("docdata", out var dd) ? dd : data; // the portal file nests documents under docdata

            string gstin = GetString(data, "gstin", "GSTIN") ?? GetString(doc.RootElement, "gstin", "GSTIN")
                ?? throw new ArgumentException("The GSTR-2B file has no GSTIN. Download it again from the GST portal.");
            string period = GetString(data, "rtnprd", "fp", "returnPeriod") ?? GetString(doc.RootElement, "rtnprd", "fp", "returnPeriod")
                ?? throw new ArgumentException("The GSTR-2B file has no return period. Download it again from the GST portal.");

            var invoices = new List<Gstr2BInvoice>();
            var warnings = new List<string>();

            foreach (var supplier in Array(docs, "b2b", "B2B"))
            {
                string ctin = GetString(supplier, "ctin", "supplierGstin") ?? "";
                string? name = GetString(supplier, "trdnm", "cname", "tradeName", "legalName");
                foreach (var inv in Array(supplier, "inv", "invoices"))
                {
                    var number = GetString(inv, "inum", "invoiceNumber");
                    if (string.IsNullOrWhiteSpace(number)) { warnings.Add($"Skipped an invoice from {ctin} with no invoice number."); continue; }
                    var a = Amounts(inv);
                    bool itcAvailable = !string.Equals(GetString(inv, "itcavl", "itcAvailable"), "N", StringComparison.OrdinalIgnoreCase);
                    invoices.Add(new Gstr2BInvoice(ctin, name, number.Trim(), Date(inv, "dt", "idt", "invoiceDate"), GstInvoiceType.Regular,
                        a.Taxable, a.Cgst, a.Sgst, a.Igst, a.Cess, itcAvailable,
                        itcAvailable ? null : GetString(inv, "rsn", "ineligibleReason")));
                }
            }

            foreach (var supplier in Array(docs, "cdnr", "CDNR"))
            {
                string ctin = GetString(supplier, "ctin", "supplierGstin") ?? "";
                string? name = GetString(supplier, "trdnm", "cname", "tradeName", "legalName");
                foreach (var note in Array(supplier, "nt", "notes"))
                {
                    var number = GetString(note, "ntnum", "nt_num", "noteNumber");
                    if (string.IsNullOrWhiteSpace(number)) { warnings.Add($"Skipped a credit/debit note from {ctin} with no note number."); continue; }
                    string type = GetString(note, "typ", "ntty", "noteType") ?? "C";
                    bool credit = type.Equals("C", StringComparison.OrdinalIgnoreCase);
                    decimal sign = credit ? -1m : 1m; // a credit note reduces ITC
                    var a = Amounts(note);
                    bool itcAvailable = !string.Equals(GetString(note, "itcavl", "itcAvailable"), "N", StringComparison.OrdinalIgnoreCase);
                    invoices.Add(new Gstr2BInvoice(ctin, name, number.Trim(), Date(note, "dt", "nt_dt", "noteDate"),
                        credit ? GstInvoiceType.CreditNote : GstInvoiceType.DebitNote,
                        a.Taxable * sign, a.Cgst * sign, a.Sgst * sign, a.Igst * sign, a.Cess * sign, itcAvailable,
                        itcAvailable ? null : GetString(note, "rsn", "ineligibleReason")));
                }
            }

            return new ParsedGstr2BResult(gstin, period, invoices,
                invoices.Sum(i => i.TaxableValue), invoices.Sum(i => i.Igst), invoices.Sum(i => i.Cgst),
                invoices.Sum(i => i.Sgst), invoices.Sum(i => i.Cess),
                invoices.Where(i => i.ItcAvailable != false).Sum(i => i.Igst + i.Cgst + i.Sgst + i.Cess),
                warnings);
        }
    }

    public static ParsedGstr1Result ParseGstr1(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException("GSTR-1 JSON content cannot be empty.");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(jsonContent); }
        catch (JsonException ex) { throw new ArgumentException($"This is not a valid GSTR-1 JSON file: {ex.Message}", ex); }

        using (doc)
        {
            var target = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
            string gstin = GetString(target, "gstin", "GSTIN")
                ?? throw new ArgumentException("The GSTR-1 file has no GSTIN. Download it again from the GST portal.");
            string period = GetString(target, "fp", "rtnprd", "returnPeriod")
                ?? throw new ArgumentException("The GSTR-1 file has no return period. Download it again from the GST portal.");

            var t = new Totals();
            foreach (var party in Array(target, "b2b", "B2B").Concat(Array(target, "b2cl", "B2CL")).Concat(Array(target, "exp", "EXP")))
                foreach (var inv in Array(party, "inv", "invoices"))
                    t.Add(Amounts(inv), 1m);
            foreach (var row in Array(target, "b2cs", "B2CS"))
                t.Add(Amounts(row), 1m);
            // credit notes reduce outward supplies, debit notes add to them
            foreach (var party in Array(target, "cdnr", "CDNR").Concat(Array(target, "cdnur", "CDNUR")))
                foreach (var note in party.ValueKind == JsonValueKind.Object && party.TryGetProperty("nt", out _) ? Array(party, "nt", "notes") : [party])
                {
                    string type = GetString(note, "ntty", "typ", "noteType") ?? "C";
                    t.Add(Amounts(note), type.Equals("C", StringComparison.OrdinalIgnoreCase) ? -1m : 1m);
                }

            decimal exempt = 0m;
            if (target.TryGetProperty("nil", out var nil) && nil.ValueKind == JsonValueKind.Object)
                foreach (var row in Array(nil, "inv"))
                    exempt += GetDecimal(row, "nil_amt") + GetDecimal(row, "expt_amt") + GetDecimal(row, "ngsup_amt");

            return new ParsedGstr1Result(gstin, period, t.Taxable, exempt, t.Taxable + exempt, t.Igst, t.Cgst, t.Sgst, t.Cess, []);
        }
    }

    private sealed record DocAmounts(decimal Taxable, decimal Igst, decimal Cgst, decimal Sgst, decimal Cess);

    private sealed class Totals
    {
        public decimal Taxable, Igst, Cgst, Sgst, Cess;
        public void Add(DocAmounts a, decimal sign)
        {
            Taxable += a.Taxable * sign; Igst += a.Igst * sign; Cgst += a.Cgst * sign; Sgst += a.Sgst * sign; Cess += a.Cess * sign;
        }
    }

    /// <summary>
    /// Taxable value and taxes of one document: from its item lines when it has them (GSTR-1 nests each line's
    /// amounts in "itm_det"), otherwise from the document itself (GSTR-2B). Never the invoice value as taxable value.
    /// </summary>
    private static DocAmounts Amounts(JsonElement doc)
    {
        var items = Array(doc, "itms", "items").ToList();
        if (items.Count == 0) return Line(doc);
        var lines = items.Select(i => Line(i.TryGetProperty("itm_det", out var det) ? det : i)).ToList();
        return new DocAmounts(lines.Sum(l => l.Taxable), lines.Sum(l => l.Igst), lines.Sum(l => l.Cgst), lines.Sum(l => l.Sgst), lines.Sum(l => l.Cess));
    }

    private static DocAmounts Line(JsonElement el) => new(
        GetDecimal(el, "txval", "taxableValue"),
        GetDecimal(el, "igst", "iamt", "igstAmount"),
        GetDecimal(el, "cgst", "camt", "cgstAmount"),
        GetDecimal(el, "sgst", "samt", "sgstAmount"),
        GetDecimal(el, "cess", "csamt", "cessAmount"));

    private static DateOnly? Date(JsonElement el, params string[] names) =>
        GetString(el, names) is { } s && DateOnly.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;

    private static IEnumerable<JsonElement> Array(JsonElement el, params string[] names)
    {
        if (el.ValueKind != JsonValueKind.Object) yield break;
        foreach (var name in names)
            if (el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray()) yield return item;
                yield break;
            }
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (el.TryGetProperty(name, out var val))
                return val.ValueKind switch
                {
                    JsonValueKind.String => val.GetString(),
                    JsonValueKind.Number => val.GetRawText(),
                    _ => null,
                };
        return null;
    }

    private static decimal GetDecimal(JsonElement el, params string[] names)
    {
        if (el.ValueKind != JsonValueKind.Object) return 0m;
        foreach (var name in names)
            if (el.TryGetProperty(name, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDecimal();
                if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
        return 0m;
    }
}
