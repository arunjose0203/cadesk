using CaDesk.Application.Abstractions;
using CaDesk.Domain.EInvoicing;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.EInvoicing;

public sealed class EInvoicingService(IAppDbContext db, ITenantContext tenant) : IEInvoicingService
{
    public async Task<List<EInvoiceDto>> GetEInvoicesAsync(Guid? clientId, EInvoiceStatus? status, CancellationToken ct = default)
    {
        var q = db.EInvoices
            .AsNoTracking()
            .Include(e => e.Client)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(e => e.ClientId == clientId.Value);
        if (status.HasValue) q = q.Where(e => e.Status == status.Value);

        var list = await q.OrderByDescending(e => e.DocDate).ToListAsync(ct);
        return list.Select(MapEInvoiceToDto).ToList();
    }

    public async Task<EInvoiceDto?> GetEInvoiceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.EInvoices
            .AsNoTracking()
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return item is null ? null : MapEInvoiceToDto(item);
    }

    public async Task<EInvoiceDto> GenerateEInvoiceAsync(GenerateEInvoiceRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client '{req.ClientId}' not found.");

        var totalInvVal = req.TotalTaxableValue + req.CgstAmount + req.SgstAmount + req.IgstAmount + req.CessAmount;
        var irn = IrnGenerator.GenerateIrn(req.SupplierGstin, req.FinancialYear, req.DocType.ToString(), req.DocNumber);

        var existing = await db.EInvoices
            .FirstOrDefaultAsync(e => e.Irn == irn && e.Status != EInvoiceStatus.Cancelled, ct);
        if (existing != null)
        {
            throw new InvalidOperationException($"An active E-Invoice with IRN already exists for Invoice {req.DocNumber} ({req.FinancialYear}).");
        }

        var qrCode = IrnGenerator.GenerateSignedQrPayload(
            req.SupplierGstin,
            req.RecipientGstin,
            req.DocNumber,
            req.DocDate,
            totalInvVal,
            req.ItemCount,
            req.MainHsnCode,
            irn
        );

        var now = DateTimeOffset.UtcNow;
        var ackNumber = $"11{now:yyMMdd}{Random.Shared.Next(100000, 999999)}";

        var record = new EInvoiceRecord
        {
            ClientId = req.ClientId,
            InvoiceId = req.InvoiceId,
            DocType = req.DocType,
            DocNumber = req.DocNumber.Trim(),
            DocDate = req.DocDate,
            FinancialYear = req.FinancialYear.Trim(),
            SupplierGstin = req.SupplierGstin.Trim().ToUpperInvariant(),
            SupplierTradeName = req.SupplierTradeName.Trim(),
            RecipientGstin = req.RecipientGstin.Trim().ToUpperInvariant(),
            RecipientLegalName = req.RecipientLegalName.Trim(),
            SupplyType = req.SupplyType,
            TotalTaxableValue = req.TotalTaxableValue,
            CgstAmount = req.CgstAmount,
            SgstAmount = req.SgstAmount,
            IgstAmount = req.IgstAmount,
            CessAmount = req.CessAmount,
            TotalInvoiceValue = totalInvVal,
            Irn = irn,
            SignedQrCode = qrCode,
            AckNo = ackNumber,
            AckDate = now,
            Status = EInvoiceStatus.Generated
        };

        db.EInvoices.Add(record);
        await db.SaveChangesAsync(ct);
        record.Client = client;

        return MapEInvoiceToDto(record);
    }

    public async Task<EInvoiceDto> CancelEInvoiceAsync(Guid id, CancelEInvoiceRequest req, CancellationToken ct = default)
    {
        var record = await db.EInvoices
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException($"E-Invoice record '{id}' not found.");

        if (record.Status == EInvoiceStatus.Cancelled)
        {
            throw new InvalidOperationException("This E-Invoice is already cancelled.");
        }

        var now = DateTimeOffset.UtcNow;
        // Statutory rule: IRP cancellation is allowed within 24 hours of generation
        if (record.AckDate.HasValue && (now - record.AckDate.Value).TotalHours > 24)
        {
            throw new InvalidOperationException("E-Invoices can only be cancelled within 24 hours of generation on IRP. Please issue a Credit Note (CRN) instead.");
        }

        record.Status = EInvoiceStatus.Cancelled;
        record.CancelReason = req.Reason;
        record.CancelRemarks = req.Remarks?.Trim();
        record.CancelledAt = now;

        await db.SaveChangesAsync(ct);
        return MapEInvoiceToDto(record);
    }

    public async Task<List<EWayBillDto>> GetEWayBillsAsync(Guid? clientId, EwbStatus? status, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var q = db.EWayBills
            .AsNoTracking()
            .Include(e => e.Client)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(e => e.ClientId == clientId.Value);
        if (status.HasValue) q = q.Where(e => e.Status == status.Value);

        var list = await q.OrderByDescending(e => e.EwbDate).ToListAsync(ct);
        return list.Select(e => MapEwbToDto(e, now)).ToList();
    }

    public async Task<EWayBillDto?> GetEWayBillByIdAsync(Guid id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var item = await db.EWayBills
            .AsNoTracking()
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return item is null ? null : MapEwbToDto(item, now);
    }

    public async Task<EWayBillDto> GenerateEWayBillAsync(GenerateEWayBillRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client '{req.ClientId}' not found.");

        var now = DateTimeOffset.UtcNow;
        var validUntil = EwbValidityCalculator.CalculateExpiry(now, req.DistanceKm, req.VehicleType);
        var ewbNumber = $"24{now:yyMMdd}{Random.Shared.Next(1000, 9999)}";

        var ewb = new EWayBillRecord
        {
            ClientId = req.ClientId,
            EInvoiceId = req.EInvoiceId,
            EwbNumber = ewbNumber,
            EwbDate = now,
            ValidUntil = validUntil,
            SupplyType = req.SupplyType,
            SubSupplyType = req.SubSupplyType,
            DocType = req.DocType.Trim().ToUpperInvariant(),
            DocNumber = req.DocNumber.Trim(),
            DocDate = req.DocDate,
            FromGstin = req.FromGstin.Trim().ToUpperInvariant(),
            FromTradeName = req.FromTradeName.Trim(),
            FromPincode = req.FromPincode.Trim(),
            FromStateCode = req.FromStateCode,
            ToGstin = req.ToGstin.Trim().ToUpperInvariant(),
            ToTradeName = req.ToTradeName.Trim(),
            ToPincode = req.ToPincode.Trim(),
            ToStateCode = req.ToStateCode,
            TotalValue = req.TotalValue,
            HsnCode = req.HsnCode.Trim(),
            DistanceKm = req.DistanceKm,
            TransportMode = req.TransportMode,
            VehicleType = req.VehicleType,
            VehicleNumber = req.VehicleNumber?.Trim().ToUpperInvariant(),
            TransporterId = req.TransporterId?.Trim().ToUpperInvariant(),
            TransporterName = req.TransporterName?.Trim(),
            Status = EwbStatus.Active
        };

        db.EWayBills.Add(ewb);
        await db.SaveChangesAsync(ct);
        ewb.Client = client;

        return MapEwbToDto(ewb, now);
    }

    public async Task<EWayBillDto> UpdateEWayBillPartBAsync(Guid id, UpdateEWayBillPartBRequest req, CancellationToken ct = default)
    {
        var ewb = await db.EWayBills
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException($"E-Way Bill '{id}' not found.");

        if (ewb.Status is EwbStatus.Cancelled or EwbStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot update vehicle on a {ewb.Status} E-Way Bill.");
        }

        ewb.VehicleNumber = req.VehicleNumber.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(req.TransporterId))
        {
            ewb.TransporterId = req.TransporterId.Trim().ToUpperInvariant();
        }
        if (!string.IsNullOrWhiteSpace(req.TransporterName))
        {
            ewb.TransporterName = req.TransporterName.Trim();
        }

        await db.SaveChangesAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return MapEwbToDto(ewb, now);
    }

    public async Task<EWayBillDto> CancelEWayBillAsync(Guid id, CancelEWayBillRequest req, CancellationToken ct = default)
    {
        var ewb = await db.EWayBills
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException($"E-Way Bill '{id}' not found.");

        ewb.Status = EwbStatus.Cancelled;
        ewb.CancelReason = req.Reason;
        ewb.CancelledAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return MapEwbToDto(ewb, now);
    }

    public async Task<EInvoicingSummaryDto> GetSummaryAsync(Guid? clientId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var qInv = db.EInvoices.AsNoTracking();
        var qEwb = db.EWayBills.AsNoTracking();

        if (clientId.HasValue)
        {
            qInv = qInv.Where(e => e.ClientId == clientId.Value);
            qEwb = qEwb.Where(e => e.ClientId == clientId.Value);
        }

        var totalInv = await qInv.CountAsync(ct);
        var activeIrn = await qInv.CountAsync(e => e.Status == EInvoiceStatus.Generated, ct);
        var cancelledIrn = await qInv.CountAsync(e => e.Status == EInvoiceStatus.Cancelled, ct);
        var totalInvValue = await qInv.Where(e => e.Status == EInvoiceStatus.Generated).SumAsync(e => e.TotalInvoiceValue, ct);

        var activeEwb = await qEwb.CountAsync(e => e.Status == EwbStatus.Active, ct);
        var totalEwbValue = await qEwb.Where(e => e.Status == EwbStatus.Active).SumAsync(e => e.TotalValue, ct);

        var threshold24h = now.AddHours(24);
        var expiringSoon = await qEwb.CountAsync(e => e.Status == EwbStatus.Active && e.ValidUntil <= threshold24h && e.ValidUntil >= now, ct);

        return new EInvoicingSummaryDto(
            totalInv,
            activeIrn,
            cancelledIrn,
            totalInvValue,
            activeEwb,
            expiringSoon,
            totalEwbValue
        );
    }

    private static EInvoiceDto MapEInvoiceToDto(EInvoiceRecord e) =>
        new(
            e.Id,
            e.ClientId,
            e.Client?.Name ?? "",
            e.Client?.Code ?? "",
            e.InvoiceId,
            e.DocType,
            e.DocNumber,
            e.DocDate,
            e.FinancialYear,
            e.SupplierGstin,
            e.SupplierTradeName,
            e.RecipientGstin,
            e.RecipientLegalName,
            e.SupplyType,
            e.TotalTaxableValue,
            e.CgstAmount,
            e.SgstAmount,
            e.IgstAmount,
            e.CessAmount,
            e.TotalInvoiceValue,
            e.Irn,
            e.SignedQrCode,
            e.AckNo,
            e.AckDate,
            e.Status,
            e.CancelReason,
            e.CancelRemarks,
            e.CancelledAt,
            e.CreatedAt
        );

    private static EWayBillDto MapEwbToDto(EWayBillRecord e, DateTimeOffset now) =>
        new(
            e.Id,
            e.ClientId,
            e.Client?.Name ?? "",
            e.Client?.Code ?? "",
            e.EInvoiceId,
            e.EwbNumber,
            e.EwbDate,
            e.ValidUntil,
            e.RemainingHours(now),
            e.SupplyType,
            e.SubSupplyType,
            e.DocType,
            e.DocNumber,
            e.DocDate,
            e.FromGstin,
            e.FromTradeName,
            e.FromPincode,
            e.FromStateCode,
            e.ToGstin,
            e.ToTradeName,
            e.ToPincode,
            e.ToStateCode,
            e.TotalValue,
            e.HsnCode,
            e.DistanceKm,
            e.TransportMode,
            e.VehicleType,
            e.VehicleNumber,
            e.TransporterId,
            e.TransporterName,
            e.Status,
            e.CancelReason,
            e.CancelledAt,
            e.CreatedAt
        );
}
