using CaDesk.Domain.EInvoicing;

namespace CaDesk.Application.EInvoicing;

public interface IEInvoicingService
{
    // E-Invoices
    Task<List<EInvoiceDto>> GetEInvoicesAsync(Guid? clientId, EInvoiceStatus? status, CancellationToken ct = default);
    Task<EInvoiceDto?> GetEInvoiceByIdAsync(Guid id, CancellationToken ct = default);
    Task<EInvoiceDto> GenerateEInvoiceAsync(GenerateEInvoiceRequest req, CancellationToken ct = default);
    Task<EInvoiceDto> CancelEInvoiceAsync(Guid id, CancelEInvoiceRequest req, CancellationToken ct = default);

    // E-Way Bills
    Task<List<EWayBillDto>> GetEWayBillsAsync(Guid? clientId, EwbStatus? status, CancellationToken ct = default);
    Task<EWayBillDto?> GetEWayBillByIdAsync(Guid id, CancellationToken ct = default);
    Task<EWayBillDto> GenerateEWayBillAsync(GenerateEWayBillRequest req, CancellationToken ct = default);
    Task<EWayBillDto> UpdateEWayBillPartBAsync(Guid id, UpdateEWayBillPartBRequest req, CancellationToken ct = default);
    Task<EWayBillDto> CancelEWayBillAsync(Guid id, CancelEWayBillRequest req, CancellationToken ct = default);

    // Summary
    Task<EInvoicingSummaryDto> GetSummaryAsync(Guid? clientId, CancellationToken ct = default);
}
