using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.EInvoicing;

public enum EInvoiceDocType
{
    INV = 1, // Tax Invoice
    CRN = 2, // Credit Note
    DBN = 3  // Debit Note
}

public enum EInvoiceSupplyType
{
    B2B = 1,
    SEZWP = 2,
    SEZWOP = 3,
    EXPWP = 4,
    EXPWOP = 5,
    DEXP = 6
}

public enum EInvoiceStatus
{
    Draft = 1,
    Generated = 2,
    Cancelled = 3,
    Failed = 4
}

public enum EwbSupplyType
{
    Outward = 1,
    Inward = 2
}

public enum EwbSubSupplyType
{
    Supply = 1,
    Export = 2,
    JobWork = 3,
    SKD_CKD = 4,
    LineSale = 5,
    Exhibition = 6,
    Others = 99
}

public enum EwbTransportMode
{
    Road = 1,
    Rail = 2,
    Air = 3,
    Ship = 4
}

public enum EwbVehicleType
{
    Regular = 1,
    OverDimensionalCargo = 2
}

public enum EwbStatus
{
    Active = 1,
    Extended = 2,
    Cancelled = 3,
    Expired = 4
}

public class EInvoiceRecord : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public EInvoiceDocType DocType { get; set; } = EInvoiceDocType.INV;
    public string DocNumber { get; set; } = "";
    public DateOnly DocDate { get; set; }
    public string FinancialYear { get; set; } = "";

    public string SupplierGstin { get; set; } = "";
    public string SupplierTradeName { get; set; } = "";
    public string RecipientGstin { get; set; } = "";
    public string RecipientLegalName { get; set; } = "";
    public EInvoiceSupplyType SupplyType { get; set; } = EInvoiceSupplyType.B2B;

    public decimal TotalTaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    public decimal TotalInvoiceValue { get; set; }

    public string? Irn { get; set; } // 64-character SHA-256 hash
    public string? SignedQrCode { get; set; }
    public string? AckNo { get; set; }
    public DateTimeOffset? AckDate { get; set; }
    public EInvoiceStatus Status { get; set; } = EInvoiceStatus.Draft;

    public string? CancelReason { get; set; }
    public string? CancelRemarks { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public class EWayBillRecord : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? EInvoiceId { get; set; }
    public EInvoiceRecord? EInvoice { get; set; }

    public string EwbNumber { get; set; } = ""; // 12-digit number
    public DateTimeOffset EwbDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ValidUntil { get; set; }

    public EwbSupplyType SupplyType { get; set; } = EwbSupplyType.Outward;
    public EwbSubSupplyType SubSupplyType { get; set; } = EwbSubSupplyType.Supply;
    public string DocType { get; set; } = "INV";
    public string DocNumber { get; set; } = "";
    public DateOnly DocDate { get; set; }

    public string FromGstin { get; set; } = "";
    public string FromTradeName { get; set; } = "";
    public string FromPincode { get; set; } = "";
    public int FromStateCode { get; set; }

    public string ToGstin { get; set; } = "";
    public string ToTradeName { get; set; } = "";
    public string ToPincode { get; set; } = "";
    public int ToStateCode { get; set; }

    public decimal TotalValue { get; set; }
    public string HsnCode { get; set; } = "";
    public int DistanceKm { get; set; }
    public EwbTransportMode TransportMode { get; set; } = EwbTransportMode.Road;
    public EwbVehicleType VehicleType { get; set; } = EwbVehicleType.Regular;

    public string? VehicleNumber { get; set; }
    public string? TransporterId { get; set; }
    public string? TransporterName { get; set; }

    public EwbStatus Status { get; set; } = EwbStatus.Active;
    public string? CancelReason { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public int RemainingHours(DateTimeOffset asOf) =>
        (int)Math.Max(0, (ValidUntil - asOf).TotalHours);
}

public static class IrnGenerator
{
    /// <summary>
    /// Computes the statutory 64-character SHA-256 Invoice Reference Number (IRN)
    /// generated from SupplierGSTIN + FinancialYear + DocType + DocNumber.
    /// </summary>
    public static string GenerateIrn(string supplierGstin, string financialYear, string docType, string docNumber)
    {
        var rawInput = $"{supplierGstin.Trim().ToUpperInvariant()}{financialYear.Trim()}{docType.Trim().ToUpperInvariant()}{docNumber.Trim()}";
        var bytes = Encoding.UTF8.GetBytes(rawInput);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Constructs the signed QR code string representation containing mandatory fields:
    /// SupplierGSTIN, RecipientGSTIN, DocNo, DocDate, TotalValue, ItemCount, MainHsn, IRN.
    /// </summary>
    public static string GenerateSignedQrPayload(
        string supplierGstin,
        string recipientGstin,
        string docNo,
        DateOnly docDate,
        decimal invoiceValue,
        int itemCount,
        string mainHsn,
        string irn)
    {
        var payload = new
        {
            data = new
            {
                SellerGstin = supplierGstin.Trim().ToUpperInvariant(),
                BuyerGstin = recipientGstin.Trim().ToUpperInvariant(),
                DocNo = docNo.Trim(),
                DocTyp = "INV",
                DocDt = docDate.ToString("dd/MM/yyyy"),
                TotInvVal = invoiceValue,
                ItemCnt = Math.Max(1, itemCount),
                MainHsnCode = mainHsn.Trim(),
                Irn = irn
            },
            sig = $"NIC-IRP-SIG-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(irn)))[..16]}"
        };

        return JsonSerializer.Serialize(payload);
    }
}

public static class EwbValidityCalculator
{
    /// <summary>
    /// Computes statutory validity period under Rule 138(10) of CGST Rules:
    /// - Regular Cargo: 1 day for every 200 km (or part thereof). Minimum 1 day.
    /// - Over Dimensional Cargo: 1 day for every 20 km (or part thereof). Minimum 1 day.
    /// </summary>
    public static int CalculateValidityDays(int distanceKm, EwbVehicleType vehicleType)
    {
        if (distanceKm <= 0) return 1;

        return vehicleType switch
        {
            EwbVehicleType.OverDimensionalCargo => Math.Max(1, (int)Math.Ceiling(distanceKm / 20.0)),
            _ => Math.Max(1, (int)Math.Ceiling(distanceKm / 200.0))
        };
    }

    /// <summary>
    /// Calculates expiration timestamp based on generated date and validity days.
    /// </summary>
    public static DateTimeOffset CalculateExpiry(DateTimeOffset generatedAt, int distanceKm, EwbVehicleType vehicleType)
    {
        var days = CalculateValidityDays(distanceKm, vehicleType);
        return generatedAt.AddDays(days);
    }
}
