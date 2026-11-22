using CaDesk.Application.Abstractions;
using CaDesk.Application.Gst;
using CaDesk.Application.Tax;
using CaDesk.Domain.Gst;
using Xunit;

namespace CaDesk.UnitTests;

/// <summary>GST portal JSON in its official shapes, and the AIS ownership check.</summary>
public class GstAndAisImportUnitTests
{
    [Fact]
    public void Gstr2b_portal_file_reads_docdata_invoice_level_tax_and_dt()
    {
        const string json = """
            { "chksum": "x", "data": { "gstin": "27AAACB1234F1Z9", "rtnprd": "042025", "docdata": {
              "b2b": [ { "ctin": "29AABCT1332L1ZU", "trdnm": "Tara Fabrics", "inv": [
                { "inum": "TF/101", "dt": "15-04-2025", "val": 118000, "pos": "27", "rev": "N", "itcavl": "Y",
                  "txval": 100000, "igst": 18000, "cgst": 0, "sgst": 0, "cess": 0 },
                { "inum": "TF/102", "dt": "20-04-2025", "val": 11800, "itcavl": "N", "rsn": "P",
                  "txval": 10000, "igst": 1800 } ] } ],
              "cdnr": [ { "ctin": "29AABCT1332L1ZU", "trdnm": "Tara Fabrics", "nt": [
                { "ntnum": "CN/7", "dt": "25-04-2025", "typ": "C", "txval": 5000, "igst": 900 } ] } ] } } }
            """;
        var r = GstJsonParser.ParseGstr2B(json);

        Assert.Equal("042025", r.ReturnPeriod);
        Assert.Equal(3, r.Invoices.Count);
        var first = r.Invoices[0];
        Assert.Equal("Tara Fabrics", first.SupplierName);
        Assert.Equal(new DateOnly(2025, 4, 15), first.InvoiceDate);
        Assert.Equal(100000m, first.TaxableValue);   // taxable value, never the invoice value
        Assert.Equal(18000m, first.Igst);
        Assert.False(r.Invoices[1].ItcAvailable);
        Assert.Equal("P", r.Invoices[1].IneligibleReason);
        Assert.Equal(GstInvoiceType.CreditNote, r.Invoices[2].InvoiceType);
        Assert.Equal(-900m, r.Invoices[2].Igst);
        Assert.Equal(18000m - 900m, r.TotalItcAvailable); // ineligible invoice excluded, credit note reduces
    }

    [Fact]
    public void Gstr2b_never_invents_missing_values()
    {
        const string json = """{ "data": { "gstin": "27AAACB1234F1Z9", "rtnprd": "042025", "docdata": { "b2b": [ { "ctin": "29AABCT1332L1ZU", "inv": [ { "txval": 100 }, { "inum": "A1", "txval": 50 } ] } ] } } }""";
        var r = GstJsonParser.ParseGstr2B(json);

        Assert.Single(r.Invoices);                  // the invoice without a number is skipped and reported, not called "INV-001"
        Assert.Single(r.Warnings!);
        Assert.Null(r.Invoices[0].InvoiceDate);     // no date in the file: none invented

        Assert.Throws<ArgumentException>(() => GstJsonParser.ParseGstr2B("""{ "data": { "rtnprd": "042025" } }""")); // no GSTIN
    }

    [Fact]
    public void Gstr1_reads_itm_det_and_nets_credit_notes()
    {
        const string json = """
            { "gstin": "27AAACB1234F1Z9", "fp": "042025",
              "b2b": [ { "ctin": "29AABCT1332L1ZU", "inv": [ { "inum": "S1", "idt": "02-04-2025", "val": 59000,
                 "itms": [ { "num": 1, "itm_det": { "txval": 50000, "rt": 18, "iamt": 9000, "csamt": 0 } } ] } ] } ],
              "cdnr": [ { "ctin": "29AABCT1332L1ZU", "nt": [ { "nt_num": "C1", "ntty": "C",
                 "itms": [ { "num": 1, "itm_det": { "txval": 10000, "iamt": 1800 } } ] } ] } ],
              "nil": { "inv": [ { "sply_ty": "INTRB2B", "expt_amt": 2000, "nil_amt": 0, "ngsup_amt": 0 } ] } }
            """;
        var r = GstJsonParser.ParseGstr1(json);

        Assert.Equal(40000m, r.OutwardTaxableSupplies);
        Assert.Equal(7200m, r.TotalIgst);
        Assert.Equal(2000m, r.OutwardExemptedSupplies);
        Assert.Equal(42000m, r.TotalTurnover);
    }

    private static ParsedAisResult Ais(string pan, string fy) =>
        new(pan, "Test", fy, "2026-27", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], []);

    [Fact]
    public void Ais_for_another_pan_or_year_is_refused()
    {
        var client = new Dictionary<string, string> { ["PAN"] = "AAACB1234F" };

        AnnualInformationStatementParser.EnsureBelongsTo(Ais("aaacb1234f", "2025-26"), client, "2025-26"); // same PAN, any case: fine
        Assert.Throws<BusinessRuleException>(() => AnnualInformationStatementParser.EnsureBelongsTo(Ais("ABCDE1234F", "2025-26"), client, null));
        Assert.Throws<BusinessRuleException>(() => AnnualInformationStatementParser.EnsureBelongsTo(Ais("AAACB1234F", "2024-25"), client, "2025-26"));
        Assert.Throws<BusinessRuleException>(() => AnnualInformationStatementParser.EnsureBelongsTo(Ais("AAACB1234F", "2025-26"), new Dictionary<string, string>(), null));
    }
}
