using CaDesk.Application.Abstractions;
using CaDesk.Domain.Gst;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Gst;

public class GstReconciliationService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<GstReconciliationBatchDto> RunReconciliationAsync(
        RunReconciliationRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        if (string.IsNullOrWhiteSpace(req.ReturnPeriod))
            throw new RequestValidationException(nameof(req.ReturnPeriod), "Return period is required (e.g. 042024 or 2024-04).");
        if (string.IsNullOrWhiteSpace(req.FinancialYear))
            throw new RequestValidationException(nameof(req.FinancialYear), "Financial year is required (e.g. 2024-25).");

        var tolerance = req.ToleranceAmount ?? 1.00m;
        var prList = req.PurchaseInvoices ?? [];
        var twoBList = req.Gstr2BInvoices ?? [];

        var result = GstReconciliationEngine.Reconcile(
            tenant.FirmId,
            req.ClientId,
            req.ReturnPeriod.Trim(),
            req.FinancialYear.Trim(),
            tolerance,
            prList,
            twoBList,
            req.Notes?.Trim(),
            currentUserId
        );

        db.GstReconciliationBatches.Add(result.Batch);
        db.GstReconciliationItems.AddRange(result.Items);

        await db.SaveChangesAsync(ct);

        return await GetBatchByIdAsync(result.Batch.Id, ct);
    }

    public async Task<GstReconciliationBatchDto> RunReconciliationFromGstr2BJsonAsync(
        Guid clientId,
        string returnPeriod,
        string financialYear,
        string gstr2bJson,
        List<PurchaseRegisterInvoice>? purchaseInvoices,
        decimal? toleranceAmount,
        string? notes,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var parsed2B = GstJsonParser.ParseGstr2B(gstr2bJson);

        var req = new RunReconciliationRequest(
            ClientId: clientId,
            ReturnPeriod: string.IsNullOrWhiteSpace(returnPeriod) ? parsed2B.ReturnPeriod : returnPeriod,
            FinancialYear: financialYear,
            ToleranceAmount: toleranceAmount ?? 1.00m,
            PurchaseInvoices: purchaseInvoices ?? [],
            Gstr2BInvoices: parsed2B.Invoices,
            Notes: notes ?? $"Auto-imported from GSTR-2B JSON (Total ITC Available: ₹{parsed2B.TotalItcAvailable:N2})"
        );

        return await RunReconciliationAsync(req, currentUserId, ct);
    }

    public async Task<GstReconciliationBatchDto> GetBatchByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = await db.GstReconciliationBatches
            .Include(x => x.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (b is null) throw new BusinessRuleException("GST reconciliation batch not found.", 404);

        return ToBatchDto(b);
    }

    public async Task<List<GstReconciliationBatchDto>> ListBatchesAsync(
        Guid? clientId = null,
        string? financialYear = null,
        string? returnPeriod = null,
        CancellationToken ct = default)
    {
        var q = db.GstReconciliationBatches
            .Include(x => x.Client)
            .AsNoTracking();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear) && financialYear != "all") q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(returnPeriod) && returnPeriod != "all") q = q.Where(x => x.ReturnPeriod == returnPeriod);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToBatchDto).ToList();
    }

    public async Task<List<GstReconciliationItemDto>> ListItemsAsync(
        Guid batchId,
        string? matchStatus = null,
        CancellationToken ct = default)
    {
        var q = db.GstReconciliationItems
            .AsNoTracking()
            .Where(x => x.BatchId == batchId);

        if (!string.IsNullOrWhiteSpace(matchStatus) && matchStatus != "all")
            q = q.Where(x => x.MatchStatus == matchStatus);

        var list = await q.OrderBy(x => x.SupplierGstin).ThenBy(x => x.InvoiceNumber).ToListAsync(ct);
        return list.Select(ToItemDto).ToList();
    }

    public async Task<GstReconciliationSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var batches = await db.GstReconciliationBatches.AsNoTracking().ToListAsync(ct);

        var totalBatches = batches.Count;
        var totalPr = batches.Sum(x => x.PurchaseRegisterCount);
        var total2B = batches.Sum(x => x.Gstr2BCount);
        var matched = batches.Sum(x => x.MatchedCount + x.MatchedWithToleranceCount);
        var missingIn2B = batches.Sum(x => x.MissingIn2BCount);
        var missingInBooks = batches.Sum(x => x.MissingInBooksCount);
        var itcEligible = batches.Sum(x => x.TotalItcEligible);

        // Missing in 2B tax is at risk
        var atRiskItems = await db.GstReconciliationItems
            .AsNoTracking()
            .Where(x => x.MatchStatus == GstMatchStatus.MissingIn2B)
            .ToListAsync(ct);

        var itcAtRisk = atRiskItems.Sum(x => x.PrCgst + x.PrSgst + x.PrIgst + x.PrCess);

        return new GstReconciliationSummaryDto(
            totalBatches,
            totalPr + total2B,
            matched,
            missingIn2B,
            missingInBooks,
            itcEligible,
            itcAtRisk
        );
    }

    public async Task<bool> DeleteBatchAsync(Guid id, CancellationToken ct = default)
    {
        var b = await db.GstReconciliationBatches.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return false;

        db.GstReconciliationBatches.Remove(b);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static GstReconciliationBatchDto ToBatchDto(GstReconciliationBatch b) =>
        new(
            b.Id,
            b.ClientId,
            b.Client?.Name,
            b.ReturnPeriod,
            b.FinancialYear,
            b.Status,
            b.ToleranceAmount,
            b.PurchaseRegisterCount,
            b.Gstr2BCount,
            b.MatchedCount,
            b.MatchedWithToleranceCount,
            b.MissingIn2BCount,
            b.MissingInBooksCount,
            b.MismatchedTaxCount,
            b.TotalPrTaxable,
            b.TotalPrTax,
            b.Total2BTaxable,
            b.Total2BTax,
            b.TotalItcEligible,
            b.TotalItcIneligible,
            b.Notes,
            b.CreatedAt
        );

    private static GstReconciliationItemDto ToItemDto(GstReconciliationItem x) =>
        new(
            x.Id,
            x.BatchId,
            x.SupplierGstin,
            x.SupplierName,
            x.InvoiceNumber,
            x.NormalizedInvoiceNumber,
            x.InvoiceDate,
            x.InvoiceType,
            x.PrTaxableValue,
            x.PrCgst,
            x.PrSgst,
            x.PrIgst,
            x.PrCess,
            x.Gstr2BTaxableValue,
            x.Gstr2BCgst,
            x.Gstr2BSgst,
            x.Gstr2BIgst,
            x.Gstr2BCess,
            x.MatchStatus,
            x.ItcEligibility,
            x.TaxDifference,
            x.Notes,
            x.CreatedAt
        );
}
