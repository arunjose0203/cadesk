namespace CaDesk.Application.Gst;

public record PurchaseRegisterInvoice(
    string SupplierGstin,
    string? SupplierName,
    string InvoiceNumber,
    DateOnly? InvoiceDate,
    string? InvoiceType,
    decimal TaxableValue,
    decimal Cgst,
    decimal Sgst,
    decimal Igst,
    decimal Cess
);

public record Gstr2BInvoice(
    string SupplierGstin,
    string? SupplierName,
    string InvoiceNumber,
    DateOnly? InvoiceDate,
    string? InvoiceType,
    decimal TaxableValue,
    decimal Cgst,
    decimal Sgst,
    decimal Igst,
    decimal Cess,
    bool? ItcAvailable,
    string? IneligibleReason
);

public record RunReconciliationRequest(
    Guid ClientId,
    string ReturnPeriod,
    string FinancialYear,
    decimal? ToleranceAmount,
    List<PurchaseRegisterInvoice> PurchaseInvoices,
    List<Gstr2BInvoice> Gstr2BInvoices,
    string? Notes
);

public record GstReconciliationBatchDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string ReturnPeriod,
    string FinancialYear,
    string Status,
    decimal ToleranceAmount,
    int PurchaseRegisterCount,
    int Gstr2BCount,
    int MatchedCount,
    int MatchedWithToleranceCount,
    int MissingIn2BCount,
    int MissingInBooksCount,
    int MismatchedTaxCount,
    decimal TotalPrTaxable,
    decimal TotalPrTax,
    decimal Total2BTaxable,
    decimal Total2BTax,
    decimal TotalItcEligible,
    decimal TotalItcIneligible,
    string? Notes,
    DateTime CreatedAt
);

public record GstReconciliationItemDto(
    Guid Id,
    Guid BatchId,
    string SupplierGstin,
    string? SupplierName,
    string InvoiceNumber,
    string NormalizedInvoiceNumber,
    DateOnly? InvoiceDate,
    string InvoiceType,
    decimal PrTaxableValue,
    decimal PrCgst,
    decimal PrSgst,
    decimal PrIgst,
    decimal PrCess,
    decimal Gstr2BTaxableValue,
    decimal Gstr2BCgst,
    decimal Gstr2BSgst,
    decimal Gstr2BIgst,
    decimal Gstr2BCess,
    string MatchStatus,
    string ItcEligibility,
    decimal TaxDifference,
    string? Notes,
    DateTime CreatedAt
);

public record GstReconciliationSummaryDto(
    int TotalBatches,
    int TotalInvoicesReconciled,
    int TotalMatchedCount,
    int TotalMissingIn2BCount,
    int TotalMissingInBooksCount,
    decimal TotalItcEligible,
    decimal TotalItcAtRisk
);
