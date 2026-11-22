using System.Text.RegularExpressions;
using CaDesk.Domain.Gst;

namespace CaDesk.Application.Gst;

public record ReconciledResult(
    GstReconciliationBatch Batch,
    List<GstReconciliationItem> Items
);

public static class GstReconciliationEngine
{
    /// <summary>
    /// Normalizes GST invoice numbers by splitting on delimiters, stripping leading zeroes on numeric chunks,
    /// and converting to uppercase alphanumeric string.
    /// E.g. "INV/2024/0045" -> "INV202445", "inv-2024-45" -> "INV202445", "00042" -> "42".
    /// </summary>
    public static string NormalizeInvoiceNumber(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return "";

        var tokens = Regex.Split(invoiceNumber.Trim(), @"[^A-Za-z0-9]+");
        var normalizedTokens = tokens
            .Where(t => t.Length > 0)
            .Select(t =>
            {
                if (Regex.IsMatch(t, @"^\d+$"))
                {
                    var trimmed = t.TrimStart('0');
                    return trimmed.Length == 0 ? "0" : trimmed;
                }
                return t;
            });

        return string.Concat(normalizedTokens).ToUpperInvariant();
    }

    public static ReconciledResult Reconcile(
        Guid firmId,
        Guid clientId,
        string returnPeriod,
        string financialYear,
        decimal toleranceAmount,
        List<PurchaseRegisterInvoice> prInvoices,
        List<Gstr2BInvoice> gstr2BInvoices,
        string? notes = null,
        Guid? currentUserId = null)
    {
        var batch = new GstReconciliationBatch
        {
            FirmId = firmId,
            ClientId = clientId,
            ReturnPeriod = returnPeriod,
            FinancialYear = financialYear,
            ToleranceAmount = toleranceAmount,
            Status = GstBatchStatus.Completed,
            Notes = notes,
            CreatedBy = currentUserId
        };

        var items = new List<GstReconciliationItem>();

        // Index 2B records by (Gstin, NormalizedInvoice)
        // In case supplier uploaded duplicates, keep list
        var gstr2BMap = new Dictionary<(string Gstin, string Inv), List<Gstr2BInvoice>>();
        foreach (var b in gstr2BInvoices)
        {
            var gstin = b.SupplierGstin.Trim().ToUpperInvariant();
            var normInv = NormalizeInvoiceNumber(b.InvoiceNumber);
            var key = (gstin, normInv);
            if (!gstr2BMap.TryGetValue(key, out var list))
            {
                list = [];
                gstr2BMap[key] = list;
            }
            list.Add(b);
        }

        var matched2B = new HashSet<Gstr2BInvoice>();

        // 1. Process each Purchase Register invoice
        foreach (var pr in prInvoices)
        {
            var gstin = pr.SupplierGstin.Trim().ToUpperInvariant();
            var normInv = NormalizeInvoiceNumber(pr.InvoiceNumber);
            var key = (gstin, normInv);

            var prTax = pr.Cgst + pr.Sgst + pr.Igst + pr.Cess;
            batch.PurchaseRegisterCount++;
            batch.TotalPrTaxable += pr.TaxableValue;
            batch.TotalPrTax += prTax;

            if (gstr2BMap.TryGetValue(key, out var matching2BList) && matching2BList.Count > 0)
            {
                // Pick best matching 2B record (first unconsumed or lowest tax diff)
                var candidate = matching2BList.FirstOrDefault(x => !matched2B.Contains(x)) ?? matching2BList[0];
                matched2B.Add(candidate);

                var twoBTax = candidate.Cgst + candidate.Sgst + candidate.Igst + candidate.Cess;
                var taxDiff = Math.Abs(prTax - twoBTax);
                var taxableDiff = Math.Abs(pr.TaxableValue - candidate.TaxableValue);

                string matchStatus;
                if (taxDiff == 0 && taxableDiff == 0)
                {
                    matchStatus = GstMatchStatus.ExactMatch;
                    batch.MatchedCount++;
                }
                else if (taxDiff <= toleranceAmount)
                {
                    matchStatus = GstMatchStatus.MatchWithTolerance;
                    batch.MatchedWithToleranceCount++;
                }
                else
                {
                    matchStatus = GstMatchStatus.TaxMismatch;
                    batch.MismatchedTaxCount++;
                }

                var itcEligibility = candidate.ItcAvailable == false
                    ? (candidate.IneligibleReason?.Contains("17(5)") == true ? GstItcEligibility.Ineligible17_5 : GstItcEligibility.PosMismatch)
                    : GstItcEligibility.Eligible;

                if (itcEligibility == GstItcEligibility.Eligible)
                    batch.TotalItcEligible += twoBTax;
                else
                    batch.TotalItcIneligible += twoBTax;

                items.Add(new GstReconciliationItem
                {
                    FirmId = firmId,
                    BatchId = batch.Id,
                    SupplierGstin = gstin,
                    SupplierName = pr.SupplierName ?? candidate.SupplierName,
                    InvoiceNumber = pr.InvoiceNumber,
                    NormalizedInvoiceNumber = normInv,
                    InvoiceDate = pr.InvoiceDate ?? candidate.InvoiceDate,
                    InvoiceType = NormalizeInvoiceType(pr.InvoiceType ?? candidate.InvoiceType),
                    PrTaxableValue = pr.TaxableValue,
                    PrCgst = pr.Cgst,
                    PrSgst = pr.Sgst,
                    PrIgst = pr.Igst,
                    PrCess = pr.Cess,
                    Gstr2BTaxableValue = candidate.TaxableValue,
                    Gstr2BCgst = candidate.Cgst,
                    Gstr2BSgst = candidate.Sgst,
                    Gstr2BIgst = candidate.Igst,
                    Gstr2BCess = candidate.Cess,
                    MatchStatus = matchStatus,
                    ItcEligibility = itcEligibility,
                    TaxDifference = taxDiff,
                    Notes = matchStatus == GstMatchStatus.TaxMismatch
                        ? $"Tax value discrepancy of {taxDiff:F2} between Books ({prTax:F2}) and GSTR-2B ({twoBTax:F2})."
                        : null
                });
            }
            else
            {
                // Missing in GSTR-2B
                batch.MissingIn2BCount++;

                items.Add(new GstReconciliationItem
                {
                    FirmId = firmId,
                    BatchId = batch.Id,
                    SupplierGstin = gstin,
                    SupplierName = pr.SupplierName,
                    InvoiceNumber = pr.InvoiceNumber,
                    NormalizedInvoiceNumber = normInv,
                    InvoiceDate = pr.InvoiceDate,
                    InvoiceType = NormalizeInvoiceType(pr.InvoiceType),
                    PrTaxableValue = pr.TaxableValue,
                    PrCgst = pr.Cgst,
                    PrSgst = pr.Sgst,
                    PrIgst = pr.Igst,
                    PrCess = pr.Cess,
                    Gstr2BTaxableValue = 0,
                    Gstr2BCgst = 0,
                    Gstr2BSgst = 0,
                    Gstr2BIgst = 0,
                    Gstr2BCess = 0,
                    MatchStatus = GstMatchStatus.MissingIn2B,
                    ItcEligibility = GstItcEligibility.Eligible,
                    TaxDifference = prTax,
                    Notes = "Invoice present in Books but not filed by supplier in GSTR-1/IFF (ITC at risk u/s 16(2)(aa))."
                });
            }
        }

        // 2. Process GSTR-2B items missing in Purchase Register
        foreach (var b in gstr2BInvoices)
        {
            var twoBTax = b.Cgst + b.Sgst + b.Igst + b.Cess;
            batch.Gstr2BCount++;
            batch.Total2BTaxable += b.TaxableValue;
            batch.Total2BTax += twoBTax;

            if (!matched2B.Contains(b))
            {
                batch.MissingInBooksCount++;
                var gstin = b.SupplierGstin.Trim().ToUpperInvariant();
                var normInv = NormalizeInvoiceNumber(b.InvoiceNumber);

                var itcEligibility = b.ItcAvailable == false
                    ? GstItcEligibility.Ineligible17_5
                    : GstItcEligibility.Eligible;

                if (itcEligibility == GstItcEligibility.Eligible)
                    batch.TotalItcEligible += twoBTax;
                else
                    batch.TotalItcIneligible += twoBTax;

                items.Add(new GstReconciliationItem
                {
                    FirmId = firmId,
                    BatchId = batch.Id,
                    SupplierGstin = gstin,
                    SupplierName = b.SupplierName,
                    InvoiceNumber = b.InvoiceNumber,
                    NormalizedInvoiceNumber = normInv,
                    InvoiceDate = b.InvoiceDate,
                    InvoiceType = NormalizeInvoiceType(b.InvoiceType),
                    PrTaxableValue = 0,
                    PrCgst = 0,
                    PrSgst = 0,
                    PrIgst = 0,
                    PrCess = 0,
                    Gstr2BTaxableValue = b.TaxableValue,
                    Gstr2BCgst = b.Cgst,
                    Gstr2BSgst = b.Sgst,
                    Gstr2BIgst = b.Igst,
                    Gstr2BCess = b.Cess,
                    MatchStatus = GstMatchStatus.MissingInBooks,
                    ItcEligibility = itcEligibility,
                    TaxDifference = twoBTax,
                    Notes = "Invoice reflected in GSTR-2B but unbooked in client Purchase Register."
                });
            }
        }

        return new ReconciledResult(batch, items);
    }

    private static string NormalizeInvoiceType(string? raw) => (raw?.Trim().ToLowerInvariant()) switch
    {
        "credit_note" or "credit" or "c" or "cdn" or "cdnr" or "cr_note" => GstInvoiceType.CreditNote,
        "debit_note" or "debit" or "d" or "dn" or "dr_note" => GstInvoiceType.DebitNote,
        "sez" or "sezwp" or "sezwop" => GstInvoiceType.Sez,
        _ => GstInvoiceType.Regular
    };
}
