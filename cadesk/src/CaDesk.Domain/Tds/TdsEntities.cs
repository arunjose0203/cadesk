using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Tds;

public static class TdsFormType
{
    public const string Form26Q = "26Q";
    public const string Form27Q = "27Q";
    public const string Form27Eq = "27EQ";

    public static readonly string[] All = [Form26Q, Form27Q, Form27Eq];
}

public static class TdsSection
{
    public const string Sec194CCompany = "194C_company";
    public const string Sec194COther = "194C_other";
    public const string Sec194JTech = "194J_tech";
    public const string Sec194JFees = "194J_fees";
    public const string Sec194ILand = "194I_land";
    public const string Sec194IPlant = "194I_plant";
    public const string Sec194Q = "194Q";
    public const string Sec194A = "194A";
    public const string Sec194H = "194H";
    public const string Sec195 = "195";

    public static readonly string[] All =
    [
        Sec194CCompany, Sec194COther, Sec194JTech, Sec194JFees,
        Sec194ILand, Sec194IPlant, Sec194Q, Sec194A, Sec194H, Sec195
    ];
}

public static class HigherRateReason
{
    public const string None = "none";
    public const string NoPan206Aa = "no_pan_206aa";
    public const string NonFiler206Ab = "non_filer_206ab";

    public static readonly string[] All = [None, NoPan206Aa, NonFiler206Ab];
}

public static class TdsBookingStatus
{
    public const string PendingDeposit = "pending_deposit";
    public const string Deposited = "deposited";
    public const string MappedToChallan = "mapped_to_challan";

    public static readonly string[] All = [PendingDeposit, Deposited, MappedToChallan];
}

public static class TdsMinorHead
{
    public const string Regular200 = "200_regular";
    public const string Assessment400 = "400_assessment";

    public static readonly string[] All = [Regular200, Assessment400];
}

public class TdsLowerDeductionCertificate : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string DeducteePan { get; set; } = "";
    public string DeducteeName { get; set; } = "";
    public string Section { get; set; } = "";
    public string CertificateNumber { get; set; } = "";
    public string FinancialYear { get; set; } = "2024-25";
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public decimal SpecifiedRate { get; set; }
    public decimal ThresholdAmount { get; set; }
    public decimal UtilizedAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
}

public class TdsChallan : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string Quarter { get; set; } = "Q1";
    public string FinancialYear { get; set; } = "2024-25";
    public string BsrCode { get; set; } = "";
    public DateOnly ChallanDate { get; set; }
    public string ChallanSerialNumber { get; set; } = "";
    public string SectionCode { get; set; } = "";
    public string MinorHead { get; set; } = TdsMinorHead.Regular200;

    public decimal TdsAmount { get; set; }
    public decimal Surcharge { get; set; }
    public decimal Cess { get; set; }
    public decimal Interest { get; set; }
    public decimal LateFee234E { get; set; }
    public decimal TotalChallanAmount { get; set; }
    public decimal AllocatedAmount { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public List<TdsDeduction> Deductions { get; set; } = new();
}

public class TdsDeduction : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ChallanId { get; set; }
    public string FormType { get; set; } = TdsFormType.Form26Q;
    public string Quarter { get; set; } = "Q1";
    public string FinancialYear { get; set; } = "2024-25";

    public string DeducteePan { get; set; } = "";
    public string DeducteeName { get; set; } = "";
    public string Section { get; set; } = TdsSection.Sec194CCompany;
    public DateOnly PaymentDate { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal TdsRate { get; set; }
    public decimal TdsAmount { get; set; }
    public decimal Surcharge { get; set; }
    public decimal Cess { get; set; }
    public decimal TotalTaxDeducted { get; set; }
    public DateOnly DeductionDate { get; set; }

    public Guid? CertificateId { get; set; }
    public string HigherRateReason { get; set; } = Domain.Tds.HigherRateReason.None;
    public string BookingStatus { get; set; } = TdsBookingStatus.PendingDeposit;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public TdsChallan? Challan { get; set; }
    public TdsLowerDeductionCertificate? Certificate { get; set; }
}
