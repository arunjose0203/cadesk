using CaDesk.Domain.EInvoicing;

namespace CaDesk.Application.EInvoicing;

public record EInvoiceDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    Guid? InvoiceId,
    EInvoiceDocType DocType,
    string DocNumber,
    DateOnly DocDate,
    string FinancialYear,
    string SupplierGstin,
    string SupplierTradeName,
    string RecipientGstin,
    string RecipientLegalName,
    EInvoiceSupplyType SupplyType,
    decimal TotalTaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal CessAmount,
    decimal TotalInvoiceValue,
    string? Irn,
    string? SignedQrCode,
    string? AckNo,
    DateTimeOffset? AckDate,
    EInvoiceStatus Status,
    string? CancelReason,
    string? CancelRemarks,
    DateTimeOffset? CancelledAt,
    DateTime CreatedAt
);

public record EWayBillDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    Guid? EInvoiceId,
    string EwbNumber,
    DateTimeOffset EwbDate,
    DateTimeOffset ValidUntil,
    int RemainingHours,
    EwbSupplyType SupplyType,
    EwbSubSupplyType SubSupplyType,
    string DocType,
    string DocNumber,
    DateOnly DocDate,
    string FromGstin,
    string FromTradeName,
    string FromPincode,
    int FromStateCode,
    string ToGstin,
    string ToTradeName,
    string ToPincode,
    int ToStateCode,
    decimal TotalValue,
    string HsnCode,
    int DistanceKm,
    EwbTransportMode TransportMode,
    EwbVehicleType VehicleType,
    string? VehicleNumber,
    string? TransporterId,
    string? TransporterName,
    EwbStatus Status,
    string? CancelReason,
    DateTimeOffset? CancelledAt,
    DateTime CreatedAt
);

public record EInvoicingSummaryDto(
    int TotalEInvoices,
    int ActiveIrnCount,
    int CancelledIrnCount,
    decimal TotalEInvoicedValue,
    int ActiveEWayBills,
    int ExpiringSoonEwbCount,
    decimal TotalEwbValue
);

public record GenerateEInvoiceRequest(
    Guid ClientId,
    Guid? InvoiceId,
    EInvoiceDocType DocType,
    string DocNumber,
    DateOnly DocDate,
    string FinancialYear,
    string SupplierGstin,
    string SupplierTradeName,
    string RecipientGstin,
    string RecipientLegalName,
    EInvoiceSupplyType SupplyType,
    decimal TotalTaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal CessAmount,
    string MainHsnCode,
    int ItemCount
);

public record CancelEInvoiceRequest(
    string Reason, // 1: Duplicate, 2: Data Entry Mistake, 3: Order Cancelled, 4: Others
    string? Remarks
);

public record GenerateEWayBillRequest(
    Guid ClientId,
    Guid? EInvoiceId,
    EwbSupplyType SupplyType,
    EwbSubSupplyType SubSupplyType,
    string DocType,
    string DocNumber,
    DateOnly DocDate,
    string FromGstin,
    string FromTradeName,
    string FromPincode,
    int FromStateCode,
    string ToGstin,
    string ToTradeName,
    string ToPincode,
    int ToStateCode,
    decimal TotalValue,
    string HsnCode,
    int DistanceKm,
    EwbTransportMode TransportMode,
    EwbVehicleType VehicleType,
    string? VehicleNumber,
    string? TransporterId,
    string? TransporterName
);

public record UpdateEWayBillPartBRequest(
    string VehicleNumber,
    string? TransporterId,
    string? TransporterName,
    string Reason // Transshipment, Vehicle Break down, etc.
);

public record CancelEWayBillRequest(
    string Reason // Duplicate, Order Cancelled, Data entry mistake
);
