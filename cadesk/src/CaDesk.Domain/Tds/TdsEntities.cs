using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Tds;

public static class TdsFormType
{
    public const string Form24Q = "24Q";
    public const string Form26Q = "26Q";
    public const string Form27Q = "27Q";
    public const string Form27Eq = "27EQ";

    public static readonly string[] All = [Form24Q, Form26Q, Form27Q, Form27Eq];
}

public static class TdsSection
{
    public const string Sec192Salary = "192_salary";
    public const string Sec192Govt = "192_govt";
    public const string Sec192GovtUnion = "192_govt_union";
    public const string Sec194CCompany = "194C_company";
    public const string Sec194COther = "194C_other";
    public const string Sec194JTech = "194J_tech";
    public const string Sec194JFees = "194J_fees";
    public const string Sec194ILand = "194I_land";
    public const string Sec194IPlant = "194I_plant";
    public const string Sec194Q = "194Q";
    public const string Sec194A = "194A";
    public const string Sec194H = "194H";
    public const string Sec194D = "194D";
    public const string Sec194DA = "194DA";
    public const string Sec194B = "194B";
    public const string Sec194N = "194N";
    public const string Sec194R = "194R";
    public const string Sec194S = "194S";
    public const string Sec194T = "194T";
    public const string Sec195 = "195";

    public static readonly string[] All =
    [
        Sec192Salary, Sec192Govt, Sec192GovtUnion,
        Sec194CCompany, Sec194COther, Sec194JTech, Sec194JFees,
        Sec194ILand, Sec194IPlant, Sec194Q, Sec194A, Sec194H,
        Sec194D, Sec194DA, Sec194B, Sec194N, Sec194R, Sec194S, Sec194T, Sec195
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
    public bool IsBookEntry { get; set; }
    public bool IsNil { get; set; }

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
    public string? DeducteeCode { get; set; }
    public string? RemarkCode { get; set; }
    public string? RemittanceCode { get; set; }
    public string? CountryCode { get; set; }
    public string? TaxRateSource { get; set; }
    public string? AckNumber { get; set; }
    public bool? OptedOutNewRegime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public TdsChallan? Challan { get; set; }
    public TdsLowerDeductionCertificate? Certificate { get; set; }
}

/// <summary>Deductor and responsible-person metadata required by Protean/NSDL e-TDS FH and BH records.</summary>
public class TdsDeductorProfile : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }

    public string Tan { get; set; } = "";
    public string Pan { get; set; } = "";
    public string DeductorType { get; set; } = "K";
    public string DeductorName { get; set; } = "";
    public string BranchDivision { get; set; } = "NA";
    public string? Gstin { get; set; }

    public string FlatBlock { get; set; } = "";
    public string? PremisesName { get; set; }
    public string? StreetRoad { get; set; }
    public string? AreaLocality { get; set; }
    public string? CityDistrict { get; set; }
    public string StateCode { get; set; } = "16";
    public string Pincode { get; set; } = "";
    public string CountryName { get; set; } = "INDIA";

    public string Email { get; set; } = "";
    public string? StdCode { get; set; }
    public string Phone { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string CountryCallingCode { get; set; } = "91";

    public string RespName { get; set; } = "";
    public string RespDesignation { get; set; } = "";
    public string RespPan { get; set; } = "";
    public string RespFlatBlock { get; set; } = "";
    public string? RespPremisesName { get; set; }
    public string? RespStreetRoad { get; set; }
    public string? RespAreaLocality { get; set; }
    public string? RespCityDistrict { get; set; }
    public string RespStateCode { get; set; } = "16";
    public string RespPincode { get; set; } = "";
    public string RespCountryName { get; set; } = "INDIA";
    public string RespEmail { get; set; } = "";
    public string RespMobile { get; set; } = "";
    public string RespCountryCallingCode { get; set; } = "91";

    public bool FiledPreviousRegular { get; set; }
    public string? PreviousTokenNo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
}
