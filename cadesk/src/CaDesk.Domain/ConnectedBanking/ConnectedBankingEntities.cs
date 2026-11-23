using System.Text.RegularExpressions;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.ConnectedBanking;

public static class BankIntegrationProvider
{
    public const string IciciCorporate = "ICICI_Corporate";
    public const string HdfcSmartHub = "HDFC_SmartHub";
    public const string AxisOpen = "Axis_Open";
    public const string OpenMoney = "Open_Money";

    public static readonly string[] All = [IciciCorporate, HdfcSmartHub, AxisOpen, OpenMoney];
}

public static class BankConnectionStatus
{
    public const string Connected = "Connected";
    public const string PendingConsent = "PendingConsent";
    public const string Expired = "Expired";
    public const string Disconnected = "Disconnected";

    public static readonly string[] All = [Connected, PendingConsent, Expired, Disconnected];
}

public static class PayoutBatchStatus
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Authorized = "Authorized";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";

    public static readonly string[] All = [Draft, PendingApproval, Authorized, Processing, Completed, Rejected, Failed];
}

public static class PayoutItemType
{
    public const string VendorInvoice = "VendorInvoice";
    public const string StaffSalary = "StaffSalary";
    public const string AdvanceTaxChallan = "AdvanceTaxChallan";
    public const string TdsChallan = "TdsChallan";
    public const string GstChallan = "GstChallan";
    public const string Reimbursement = "Reimbursement";

    public static readonly string[] All = [VendorInvoice, StaffSalary, AdvanceTaxChallan, TdsChallan, GstChallan, Reimbursement];
}

public static class PayoutPaymentMode
{
    public const string Neft = "NEFT";
    public const string Rtgs = "RTGS";
    public const string Imps = "IMPS";
    public const string Upi = "UPI";
    public const string InternalTransfer = "InternalTransfer";

    public static readonly string[] All = [Neft, Rtgs, Imps, Upi, InternalTransfer];

    public static bool ValidatePaymentMode(string mode, decimal amount, out string? error)
    {
        error = null;
        if (mode == Rtgs && amount < 200000m)
        {
            error = "RTGS requires a minimum transaction amount of ₹2,00,000 as per RBI guidelines.";
            return false;
        }
        if (mode == Upi && amount > 100000m)
        {
            error = "Standard UPI transactions cannot exceed ₹1,00,000 per transaction limit.";
            return false;
        }
        if (mode == Imps && amount > 500000m)
        {
            error = "IMPS transactions cannot exceed ₹5,00,000 per transaction ceiling.";
            return false;
        }
        return true;
    }
}

public static class BankingValidation
{
    private static readonly Regex IfscRegex = new("^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.Compiled);
    private static readonly Regex AccountNumberRegex = new("^[0-9]{9,18}$", RegexOptions.Compiled);

    public static bool IsValidIfsc(string ifsc) => !string.IsNullOrWhiteSpace(ifsc) && IfscRegex.IsMatch(ifsc.Trim().ToUpperInvariant());
    public static bool IsValidAccountNumber(string acc) => !string.IsNullOrWhiteSpace(acc) && AccountNumberRegex.IsMatch(acc.Trim());
}

public class ConnectedBankCredential : TenantEntity
{
    public Guid BankAccountId { get; set; }
    public Guid ClientId { get; set; }
    public string Provider { get; set; } = BankIntegrationProvider.IciciCorporate;
    public string CorporateId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EncryptedApiKey { get; set; } = string.Empty;
    public string EncryptedApiSecret { get; set; } = string.Empty;
    public string Status { get; set; } = BankConnectionStatus.Connected;
    public DateTime? ConsentExpiresAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public decimal? LastSyncBalance { get; set; }
    public int SyncFrequencyMinutes { get; set; } = 60;
    public string? WebhookSecret { get; set; }

    public BankAccount? BankAccount { get; set; }
    public Client? Client { get; set; }
}

public class PayoutBatch : TenantEntity
{
    public string BatchNumber { get; set; } = string.Empty;
    public Guid BankAccountId { get; set; }
    public Guid ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public string Status { get; set; } = PayoutBatchStatus.Draft;

    // Maker (Staff)
    public Guid CreatedByUserId { get; set; }
    public string? MakerRemarks { get; set; }

    // Checker (Managing Partner)
    public Guid? AuthorizedByUserId { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public string? AuthMethod { get; set; } // OTP, DSC_SHA256, Biometric
    public string? AuthReference { get; set; }
    public string? RejectionReason { get; set; }

    public DateOnly? ScheduledExecutionDate { get; set; }
    public DateTime? ExecutedAt { get; set; }

    public BankAccount? BankAccount { get; set; }
    public Client? Client { get; set; }
    public ICollection<PayoutItem> Items { get; set; } = new List<PayoutItem>();
}

public class PayoutItem : TenantEntity
{
    public Guid BatchId { get; set; }
    public string PayoutType { get; set; } = PayoutItemType.VendorInvoice;
    public string BeneficiaryName { get; set; } = string.Empty;
    public string BeneficiaryAccountNumber { get; set; } = string.Empty;
    public string BeneficiaryIfsc { get; set; } = string.Empty;
    public string? BeneficiaryPan { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMode { get; set; } = PayoutPaymentMode.Neft;
    public string Narration { get; set; } = string.Empty;
    public string? InvoiceOrChallanRef { get; set; }
    public string Status { get; set; } = "Pending";
    public string? UtrNumber { get; set; }
    public string? FailureReason { get; set; }

    public PayoutBatch? Batch { get; set; }
}
