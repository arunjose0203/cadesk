using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Mca;

public static class CompanyClass
{
    public const string Private = "Private";
    public const string Public = "Public";
    public const string OnePersonCompany = "OnePersonCompany";
    public const string Section8 = "Section8";
    public const string Llp = "LLP";

    public static readonly string[] All = [Private, Public, OnePersonCompany, Section8, Llp];
}

public static class CompanyStatus
{
    public const string Active = "Active";
    public const string StrikeOff = "StrikeOff";
    public const string Amalgamated = "Amalgamated";
    public const string UnderLiquidation = "UnderLiquidation";
    public const string Dormant = "Dormant";

    public static readonly string[] All = [Active, StrikeOff, Amalgamated, UnderLiquidation, Dormant];
}

public static class DirectorDesignation
{
    public const string Director = "Director";
    public const string ManagingDirector = "ManagingDirector";
    public const string WholeTimeDirector = "WholeTimeDirector";
    public const string IndependentDirector = "IndependentDirector";
    public const string NomineeDirector = "NomineeDirector";
    public const string AdditionalDirector = "AdditionalDirector";
    public const string DesignatedPartner = "DesignatedPartner";

    public static readonly string[] All =
    [
        Director, ManagingDirector, WholeTimeDirector, IndependentDirector,
        NomineeDirector, AdditionalDirector, DesignatedPartner
    ];
}

public static class Dir3KycStatus
{
    public const string Complied = "Complied";
    public const string Pending = "Pending";
    public const string DeactivatedDueToNonFiling = "DeactivatedDueToNonFiling";

    public static readonly string[] All = [Complied, Pending, DeactivatedDueToNonFiling];
}

public static class McaFormType
{
    public const string Aoc4 = "AOC-4";
    public const string Aoc4Xbrl = "AOC-4_XBRL";
    public const string Aoc4Cfs = "AOC-4_CFS";
    public const string Mgt7 = "MGT-7";
    public const string Mgt7A = "MGT-7A";
    public const string Adt1 = "ADT-1";
    public const string Dpt3 = "DPT-3";
    public const string Msme1 = "MSME-1";
    public const string Dir12 = "DIR-12";
    public const string Mgt14 = "MGT-14";
    public const string Chg1 = "CHG-1";

    public static readonly string[] All =
    [
        Aoc4, Aoc4Xbrl, Aoc4Cfs, Mgt7, Mgt7A, Adt1, Dpt3, Msme1, Dir12, Mgt14, Chg1
    ];
}

public static class McaFilingStatus
{
    public const string Pending = "Pending";
    public const string ChallanGenerated = "ChallanGenerated";
    public const string Uploaded = "Uploaded";
    public const string Approved = "Approved";
    public const string Defective = "Defective";

    public static readonly string[] All = [Pending, ChallanGenerated, Uploaded, Approved, Defective];
}

public static class MeetingType
{
    public const string BoardMeeting = "BoardMeeting";
    public const string Agm = "AGM";
    public const string Egm = "EGM";
    public const string CommitteeMeeting = "CommitteeMeeting";

    public static readonly string[] All = [BoardMeeting, Agm, Egm, CommitteeMeeting];
}

public static class ResolutionCategory
{
    public const string Ordinary = "Ordinary";
    public const string Special = "Special";
    public const string Unanimous = "Unanimous";

    public static readonly string[] All = [Ordinary, Special, Unanimous];
}

/// <summary>
/// Registered Corporate Entity under MCA 21 / Companies Act 2013.
/// </summary>
public class CompanyMaster : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Cin { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyClass { get; set; } = Mca.CompanyClass.Private;
    public decimal AuthorizedCapital { get; set; } = 100000m;
    public decimal PaidUpCapital { get; set; } = 100000m;
    public string RegisteredOfficeAddress { get; set; } = string.Empty;
    public string RocJurisdiction { get; set; } = "RoC-Mumbai";
    public DateOnly IncorporationDate { get; set; }
    public string CompanyStatus { get; set; } = Mca.CompanyStatus.Active;
    public DateOnly? LastAgmDate { get; set; }
    public int FinancialYearEndMonth { get; set; } = 3;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public List<DirectorDinRecord> Directors { get; set; } = [];
    public List<McaAnnualFiling> Filings { get; set; } = [];
    public List<BoardResolution> Resolutions { get; set; } = [];
}

/// <summary>
/// Director DIN, Designation & DIR-3 KYC compliance profile.
/// </summary>
public class DirectorDinRecord : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FirmId { get; set; }
    public Guid CompanyId { get; set; }
    public CompanyMaster? Company { get; set; }

    public string Din { get; set; } = string.Empty;
    public string DirectorName { get; set; } = string.Empty;
    public string? Pan { get; set; }
    public string Designation { get; set; } = DirectorDesignation.Director;
    public DateOnly AppointmentDate { get; set; }
    public DateOnly? CessationDate { get; set; }
    public DateOnly? DscExpiryDate { get; set; }
    public string Dir3KycStatus { get; set; } = Mca.Dir3KycStatus.Complied;
    public DateOnly? Dir3KycLastFiledDate { get; set; }
    public bool IsSignatory { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// MCA 21 Annual & Event-based statutory filings with Section 403 late fee tracking.
/// </summary>
public class McaAnnualFiling : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FirmId { get; set; }
    public Guid CompanyId { get; set; }
    public CompanyMaster? Company { get; set; }

    public string FinancialYear { get; set; } = string.Empty;
    public string FormType { get; set; } = McaFormType.Aoc4;
    public DateOnly DueDate { get; set; }
    public DateOnly? FilingDate { get; set; }
    public string? Srn { get; set; }
    public string Status { get; set; } = McaFilingStatus.Pending;

    public decimal NormalFee { get; set; }
    public decimal LateFee { get; set; }
    public decimal TotalFee { get; set; }
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Secretarial documentation & Board / Committee / General Meeting Resolutions.
/// </summary>
public class BoardResolution : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FirmId { get; set; }
    public Guid CompanyId { get; set; }
    public CompanyMaster? Company { get; set; }

    public string ResolutionNumber { get; set; } = string.Empty;
    public string MeetingType { get; set; } = Mca.MeetingType.BoardMeeting;
    public DateOnly MeetingDate { get; set; }
    public string Category { get; set; } = ResolutionCategory.Ordinary;
    public string Subject { get; set; } = string.Empty;
    public string Verbiage { get; set; } = string.Empty;
    public bool PassedUnanimously { get; set; } = true;

    public string? SignatoryDin { get; set; }
    public string? SignatoryName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
