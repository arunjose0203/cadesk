namespace CaDesk.Application.Mca;

public sealed record CompanyMasterDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string Cin,
    string CompanyName,
    string CompanyClass,
    decimal AuthorizedCapital,
    decimal PaidUpCapital,
    string RegisteredOfficeAddress,
    string RocJurisdiction,
    DateOnly IncorporationDate,
    string CompanyStatus,
    DateOnly? LastAgmDate,
    int FinancialYearEndMonth,
    int DirectorsCount,
    int PendingFilingsCount,
    DateTime CreatedAt
);

public sealed record CreateCompanyRequest(
    Guid ClientId,
    string Cin,
    string CompanyName,
    string? CompanyClass,
    decimal AuthorizedCapital,
    decimal PaidUpCapital,
    string RegisteredOfficeAddress,
    string RocJurisdiction,
    DateOnly IncorporationDate,
    string? CompanyStatus,
    DateOnly? LastAgmDate,
    int? FinancialYearEndMonth
);

public sealed record DirectorDinDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string Din,
    string DirectorName,
    string? Pan,
    string Designation,
    DateOnly AppointmentDate,
    DateOnly? CessationDate,
    DateOnly? DscExpiryDate,
    string Dir3KycStatus,
    DateOnly? Dir3KycLastFiledDate,
    bool IsSignatory,
    DateTime CreatedAt
);

public sealed record CreateDirectorRequest(
    Guid CompanyId,
    string Din,
    string DirectorName,
    string? Pan,
    string? Designation,
    DateOnly AppointmentDate,
    DateOnly? CessationDate,
    DateOnly? DscExpiryDate,
    string? Dir3KycStatus,
    DateOnly? Dir3KycLastFiledDate,
    bool? IsSignatory
);

public sealed record McaFilingDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string FinancialYear,
    string FormType,
    DateOnly DueDate,
    DateOnly? FilingDate,
    string? Srn,
    string Status,
    decimal NormalFee,
    decimal LateFee,
    decimal TotalFee,
    string? Remarks,
    int DaysDelayed,
    DateTime CreatedAt
);

public sealed record CreateFilingRequest(
    Guid CompanyId,
    string FinancialYear,
    string FormType,
    DateOnly DueDate,
    DateOnly? FilingDate,
    string? Srn,
    string? Status,
    decimal? NormalFee,
    string? Remarks
);

public sealed record BoardResolutionDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string ResolutionNumber,
    string MeetingType,
    DateOnly MeetingDate,
    string Category,
    string Subject,
    string Verbiage,
    bool PassedUnanimously,
    string? SignatoryDin,
    string? SignatoryName,
    DateTime CreatedAt
);

public sealed record CreateResolutionRequest(
    Guid CompanyId,
    string ResolutionNumber,
    string MeetingType,
    DateOnly MeetingDate,
    string? Category,
    string Subject,
    string Verbiage,
    bool? PassedUnanimously,
    string? SignatoryDin,
    string? SignatoryName
);

public sealed record McaComplianceSummaryDto(
    int TotalCompaniesCount,
    int ActiveCompaniesCount,
    int TotalDirectorsCount,
    int PendingKycCount,
    int PendingFilingsCount,
    int ApprovedFilingsCount,
    decimal TotalLateFeesAccrued
);

public sealed record CalculateMcaLateFeeRequest(
    string FormType,
    DateOnly DueDate,
    DateOnly FilingDate,
    decimal? PaidUpCapital
);

public sealed record McaLateFeeCalculationResult(
    string FormType,
    DateOnly DueDate,
    DateOnly FilingDate,
    int DaysDelayed,
    decimal NormalFee,
    decimal LateFee,
    decimal TotalFee,
    string LegalProvisionDescription
);
