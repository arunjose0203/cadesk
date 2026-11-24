using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Tasks;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Billing;

public static class InvoiceStatus
{
    public const string Draft = "draft";
    public const string Sent = "sent";
    public const string PartiallyPaid = "partially_paid";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Draft, Sent, PartiallyPaid, Paid, Cancelled];
}

public static class PaymentMode
{
    public const string BankTransfer = "bank_transfer";
    public const string Upi = "upi";
    public const string Cheque = "cheque";
    public const string Cash = "cash";
    public const string Card = "card";
    public const string Other = "other";

    public static readonly string[] All = [BankTransfer, Upi, Cheque, Cash, Card, Other];
}

/// <summary>
/// Staff timesheet record for billable / non-billable client or compliance work.
/// Can be imported directly into invoice line items.
/// </summary>
public class TimeEntry : TenantEntity
{
    public Guid ClientId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public string Description { get; set; } = "";
    public bool Billable { get; set; } = true;
    public bool IsBilled { get; set; }
    public Guid? InvoiceItemId { get; set; }

    public Client? Client { get; set; }
    public WorkTask? Task { get; set; }
    public User? User { get; set; }
}

/// <summary>
/// Client fee invoice with statutory GST / VAT tax calculation and sequential numbering.
/// </summary>
public class Invoice : TenantEntity
{
    public Guid ClientId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = InvoiceStatus.Draft;
    public string Currency { get; set; } = "INR";
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }

    // Rule 46 & Section 31 CGST Act Statutory B2B Tax Invoice Fields
    public string SupplierName { get; set; } = "";
    public string SupplierAddress { get; set; } = "";
    public string SupplierGstin { get; set; } = "";
    public string SupplierState { get; set; } = "Kerala";
    public string SupplierStateCode { get; set; } = "32";

    public string RecipientLegalName { get; set; } = "";
    public string RecipientTradeName { get; set; } = "";
    public string RecipientAddress { get; set; } = "";
    public string RecipientGstin { get; set; } = "";
    public string RecipientState { get; set; } = "";
    public string RecipientStateCode { get; set; } = "";

    public string PlaceOfSupply { get; set; } = "";
    public string PlaceOfSupplyStateCode { get; set; } = "";
    public bool IsReverseCharge { get; set; }
    public bool IsInterState { get; set; }

    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }

    public Client? Client { get; set; }
    public List<InvoiceItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

/// <summary>
/// Single service line item on an invoice, including SAC code, tax percentage, and optional timesheet link.
/// </summary>
public class InvoiceItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = "";
    public string? SacCode { get; set; }
    public string Unit { get; set; } = "NOS";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal GstRate { get; set; } = 18m;
    public decimal CgstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstRate { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstRate { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessRate { get; set; }
    public decimal CessAmount { get; set; }
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? TimeEntryId { get; set; }
}

/// <summary>
/// Client payment received against an issued fee invoice.
/// </summary>
public class Payment : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ClientId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMode { get; set; } = Billing.PaymentMode.BankTransfer;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
