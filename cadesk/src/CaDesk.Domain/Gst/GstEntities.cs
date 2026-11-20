using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Gst;

public static class GstMatchStatus
{
    public const string ExactMatch = "exact_match";
    public const string MatchWithTolerance = "match_with_tolerance";
    public const string MissingIn2B = "missing_in_2b";
    public const string MissingInBooks = "missing_in_books";
    public const string TaxMismatch = "tax_mismatch";
    public const string DateMismatch = "date_mismatch";

    public static readonly string[] All = [ExactMatch, MatchWithTolerance, MissingIn2B, MissingInBooks, TaxMismatch, DateMismatch];
}

public static class GstItcEligibility
{
    public const string Eligible = "eligible";
    public const string Ineligible17_5 = "ineligible_17_5";
    public const string PosMismatch = "pos_mismatch";
    public const string SupplierCancelled = "supplier_cancelled";

    public static readonly string[] All = [Eligible, Ineligible17_5, PosMismatch, SupplierCancelled];
}

public static class GstInvoiceType
{
    public const string Regular = "regular";
    public const string DebitNote = "debit_note";
    public const string CreditNote = "credit_note";
    public const string Sez = "sez";

    public static readonly string[] All = [Regular, DebitNote, CreditNote, Sez];
}

public static class GstBatchStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";

    public static readonly string[] All = [Pending, Processing, Completed, Failed];
}

public class GstReconciliationBatch : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string ReturnPeriod { get; set; } = "";
    public string FinancialYear { get; set; } = "";
    public string Status { get; set; } = GstBatchStatus.Completed;
    public decimal ToleranceAmount { get; set; } = 1.00m;

    // Counts
    public int PurchaseRegisterCount { get; set; }
    public int Gstr2BCount { get; set; }
    public int MatchedCount { get; set; }
    public int MatchedWithToleranceCount { get; set; }
    public int MissingIn2BCount { get; set; }
    public int MissingInBooksCount { get; set; }
    public int MismatchedTaxCount { get; set; }

    // Financial Totals
    public decimal TotalPrTaxable { get; set; }
    public decimal TotalPrTax { get; set; }
    public decimal Total2BTaxable { get; set; }
    public decimal Total2BTax { get; set; }
    public decimal TotalItcEligible { get; set; }
    public decimal TotalItcIneligible { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public List<GstReconciliationItem> Items { get; set; } = new();
}

public class GstReconciliationItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid BatchId { get; set; }

    public string SupplierGstin { get; set; } = "";
    public string? SupplierName { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string NormalizedInvoiceNumber { get; set; } = "";
    public DateOnly? InvoiceDate { get; set; }
    public string InvoiceType { get; set; } = GstInvoiceType.Regular;

    // Purchase Register (Books)
    public decimal PrTaxableValue { get; set; }
    public decimal PrCgst { get; set; }
    public decimal PrSgst { get; set; }
    public decimal PrIgst { get; set; }
    public decimal PrCess { get; set; }

    // GSTR-2B (Portal)
    public decimal Gstr2BTaxableValue { get; set; }
    public decimal Gstr2BCgst { get; set; }
    public decimal Gstr2BSgst { get; set; }
    public decimal Gstr2BIgst { get; set; }
    public decimal Gstr2BCess { get; set; }

    public string MatchStatus { get; set; } = GstMatchStatus.ExactMatch;
    public string ItcEligibility { get; set; } = GstItcEligibility.Eligible;
    public decimal TaxDifference { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GstReconciliationBatch? Batch { get; set; }
}
