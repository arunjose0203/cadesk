using CaDesk.Domain.Billing;

namespace CaDesk.Application.Billing;

public record CreateTimeEntryRequest(
    Guid ClientId,
    Guid? TaskId,
    DateOnly Date,
    decimal Hours,
    decimal? HourlyRate,
    string Description,
    bool? Billable
);

public record UpdateTimeEntryRequest(
    DateOnly Date,
    decimal Hours,
    decimal? HourlyRate,
    string Description,
    bool? Billable
);

public record TimeEntryDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    Guid? TaskId,
    string? TaskTitle,
    Guid UserId,
    string? UserName,
    DateOnly Date,
    decimal Hours,
    decimal HourlyRate,
    string Description,
    bool Billable,
    bool IsBilled,
    Guid? InvoiceItemId,
    DateTime CreatedAt
);

public record TimesheetSummaryDto(
    decimal TotalHours,
    decimal BillableHours,
    decimal NonBillableHours,
    decimal UnbilledHours,
    decimal EstimatedUnbilledValue
);

public record InvoiceItemInput(
    string Description,
    string? SacCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal? GstRate,
    Guid? TimeEntryId = null,
    string? Unit = "NOS"
);

public record CreateInvoiceRequest(
    Guid ClientId,
    DateOnly IssueDate,
    DateOnly DueDate,
    string? Currency,
    string? Notes,
    string? Terms,
    List<InvoiceItemInput> Items,
    List<Guid>? TimeEntryIds = null,
    string? SupplierGstin = null,
    string? RecipientGstin = null,
    string? PlaceOfSupply = null,
    bool? IsReverseCharge = null
);

public record UpdateInvoiceRequest(
    DateOnly IssueDate,
    DateOnly DueDate,
    string? Notes,
    string? Terms,
    List<InvoiceItemInput> Items
);

public record InvoiceItemDto(
    Guid Id,
    string Description,
    string? SacCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal GstRate,
    decimal Amount,
    decimal TaxAmount,
    decimal TotalAmount,
    Guid? TimeEntryId,
    string Unit = "NOS",
    decimal CgstRate = 0,
    decimal CgstAmount = 0,
    decimal SgstRate = 0,
    decimal SgstAmount = 0,
    decimal IgstRate = 0,
    decimal IgstAmount = 0
);

public record RecordPaymentRequest(
    DateOnly PaymentDate,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumber,
    string? Notes
);

public record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    string? InvoiceNumber,
    Guid ClientId,
    string? ClientName,
    DateOnly PaymentDate,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumber,
    string? Notes,
    DateTime CreatedAt
);

public record InvoiceDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Status,
    string Currency,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    string? Notes,
    string? Terms,
    List<InvoiceItemDto> Items,
    List<PaymentDto> Payments,
    DateTime CreatedAt,
    string SupplierName = "",
    string SupplierAddress = "",
    string SupplierGstin = "",
    string SupplierState = "Kerala",
    string SupplierStateCode = "32",
    string RecipientLegalName = "",
    string RecipientTradeName = "",
    string RecipientAddress = "",
    string RecipientGstin = "",
    string RecipientState = "",
    string RecipientStateCode = "",
    string PlaceOfSupply = "",
    string PlaceOfSupplyStateCode = "",
    bool IsReverseCharge = false,
    bool IsInterState = false,
    decimal CgstAmount = 0,
    decimal SgstAmount = 0,
    decimal IgstAmount = 0,
    decimal CessAmount = 0
);

public record BillingSummaryDto(
    decimal TotalOutstanding,
    decimal TotalOverdue,
    decimal BilledThisMonth,
    decimal CollectedThisMonth,
    int OpenInvoicesCount
);

public record InvoiceStatusUpdateRequest(string Status);
