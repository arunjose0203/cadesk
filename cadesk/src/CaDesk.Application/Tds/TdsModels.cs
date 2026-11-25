namespace CaDesk.Application.Tds;

public record TdsLowerDeductionCertificateDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string DeducteePan,
    string DeducteeName,
    string Section,
    string CertificateNumber,
    string FinancialYear,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    decimal SpecifiedRate,
    decimal ThresholdAmount,
    decimal UtilizedAmount,
    decimal RemainingAmount,
    DateTime CreatedAt
);

public record CreateLdcRequest(
    Guid ClientId,
    string DeducteePan,
    string DeducteeName,
    string Section,
    string CertificateNumber,
    string FinancialYear,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    decimal SpecifiedRate,
    decimal ThresholdAmount
);

public record TdsChallanDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string Quarter,
    string FinancialYear,
    string BsrCode,
    DateOnly ChallanDate,
    string ChallanSerialNumber,
    string SectionCode,
    string MinorHead,
    decimal TdsAmount,
    decimal Surcharge,
    decimal Cess,
    decimal Interest,
    decimal LateFee234E,
    decimal TotalChallanAmount,
    decimal AllocatedAmount,
    decimal UnallocatedBalance,
    int DeductionsCount,
    string? Notes,
    DateTime CreatedAt,
    bool IsBookEntry = false,
    bool IsNil = false
);

public record CreateTdsChallanRequest(
    Guid ClientId,
    string Quarter,
    string FinancialYear,
    string BsrCode,
    DateOnly ChallanDate,
    string ChallanSerialNumber,
    string SectionCode,
    string? MinorHead,
    decimal TdsAmount,
    decimal Surcharge,
    decimal Cess,
    decimal Interest,
    decimal LateFee234E,
    string? Notes,
    bool IsBookEntry = false,
    bool IsNil = false
);

public record TdsDeductionDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    Guid? ChallanId,
    string? ChallanBsr,
    DateOnly? ChallanDate,
    string? ChallanSerial,
    string FormType,
    string Quarter,
    string FinancialYear,
    string DeducteePan,
    string DeducteeName,
    string Section,
    DateOnly PaymentDate,
    decimal AmountPaid,
    decimal TdsRate,
    decimal TdsAmount,
    decimal Surcharge,
    decimal Cess,
    decimal TotalTaxDeducted,
    DateOnly DeductionDate,
    Guid? CertificateId,
    string? CertificateNumber,
    string HigherRateReason,
    string BookingStatus,
    DateTime CreatedAt,
    string? DeducteeCode = null,
    string? RemarkCode = null,
    string? RemittanceCode = null,
    string? CountryCode = null,
    string? TaxRateSource = null,
    string? AckNumber = null,
    bool? OptedOutNewRegime = null
);

public record RecordTdsDeductionRequest(
    Guid ClientId,
    string? FormType,
    string Quarter,
    string FinancialYear,
    string DeducteePan,
    string DeducteeName,
    string Section,
    DateOnly PaymentDate,
    decimal AmountPaid,
    DateOnly? DeductionDate,
    Guid? CertificateId,
    bool? ForceHigherRate206Ab,
    decimal? CustomRate = null,
    string? DeducteeCode = null,
    string? RemarkCode = null,
    string? RemittanceCode = null,
    string? CountryCode = null,
    string? TaxRateSource = null,
    string? AckNumber = null,
    bool? OptedOutNewRegime = null
);

public record MapDeductionsToChallanRequest(
    Guid ChallanId,
    List<Guid> DeductionIds
);

public record CalculateTdsInterestRequest(
    DateOnly DeductionDate,
    DateOnly? PaymentDate,
    DateOnly DepositDate,
    decimal TdsAmount
);

public record TdsInterestResultDto(
    int DelayDeductionMonths,
    decimal DelayDeductionInterest,
    int DelayDepositMonths,
    decimal DelayDepositInterest,
    decimal TotalInterest
);

public record TdsSummaryDto(
    int TotalDeductionsCount,
    decimal TotalTaxDeducted,
    decimal TotalDeposited,
    decimal TotalPendingDeposit,
    int ActiveLdcCount,
    int TotalChallansCount
);

public record TdsDeductorProfileDto(
    Guid ClientId,
    string Tan,
    string Pan,
    string DeductorType,
    string DeductorName,
    string BranchDivision,
    string? Gstin,
    string FlatBlock,
    string? PremisesName,
    string? StreetRoad,
    string? AreaLocality,
    string? CityDistrict,
    string StateCode,
    string Pincode,
    string CountryName,
    string Email,
    string? StdCode,
    string Phone,
    string Mobile,
    string CountryCallingCode,
    string RespName,
    string RespDesignation,
    string RespPan,
    string RespFlatBlock,
    string? RespPremisesName,
    string? RespStreetRoad,
    string? RespAreaLocality,
    string? RespCityDistrict,
    string RespStateCode,
    string RespPincode,
    string RespCountryName,
    string RespEmail,
    string RespMobile,
    string RespCountryCallingCode,
    bool FiledPreviousRegular,
    string? PreviousTokenNo
);

public record SaveTdsDeductorProfileRequest(
    string Tan,
    string Pan,
    string DeductorType,
    string DeductorName,
    string? BranchDivision,
    string? Gstin,
    string FlatBlock,
    string? PremisesName,
    string? StreetRoad,
    string? AreaLocality,
    string? CityDistrict,
    string StateCode,
    string Pincode,
    string? CountryName,
    string Email,
    string? StdCode,
    string? Phone,
    string? Mobile,
    string? CountryCallingCode,
    string RespName,
    string RespDesignation,
    string RespPan,
    string RespFlatBlock,
    string? RespPremisesName,
    string? RespStreetRoad,
    string? RespAreaLocality,
    string? RespCityDistrict,
    string RespStateCode,
    string RespPincode,
    string? RespCountryName,
    string RespEmail,
    string RespMobile,
    string? RespCountryCallingCode,
    bool FiledPreviousRegular = false,
    string? PreviousTokenNo = null
);

public record ExportTdsReturnRequest(
    Guid ClientId,
    string FormType,
    string FinancialYear,
    string Quarter,
    DateOnly? FileCreationDate = null,
    int FileSequenceNo = 1
);

public record FvuValidationIssue(
    string Severity, // "error" | "warning"
    string RecordType, // "PROFILE" | "CHALLAN" | "DEDUCTEE" | "RETURN"
    int? LineNumber,
    string? Code,
    string Message
);

public record TdsReturnExportResult(
    string FileName,
    string FormType,
    string OfficialFormNumber,
    string ActLabel,
    string FinancialYear,
    string AssessmentYear,
    string Quarter,
    bool ValidForFvu,
    int LineCount,
    int ChallanCount,
    int DeducteeCount,
    decimal TotalPaid,
    decimal TotalDeducted,
    decimal TotalDeposited,
    string Content,
    List<FvuValidationIssue> Issues
);

public record ParseFvuErrRequest(string ErrContent);

public record ParsedFvuErrEntry(
    int? LineNumber,
    string RecordType,
    int? ChallanNo,
    int? DeducteeNo,
    string ErrorCode,
    string Description
);

public record ImportForm16Request(
    Guid ClientId,
    string Content,
    string? FormType = null,
    string? FinancialYear = null,
    string? Quarter = null,
    bool Commit = false
);

public record ImportedTdsChallanRow(
    string BsrCode,
    string ChallanSerialNumber,
    DateOnly ChallanDate,
    string MinorHead,
    decimal TaxDeposited,
    decimal Interest,
    decimal LateFee,
    decimal TotalAmount,
    bool IsBookEntry,
    bool AlreadyExists
);

public record ImportedTdsDeductionRow(
    string FormType,
    string Quarter,
    string FinancialYear,
    string DeducteePan,
    string DeducteeName,
    string Section,
    DateOnly PaymentDate,
    DateOnly DeductionDate,
    decimal AmountPaid,
    decimal Rate,
    decimal TdsAmount,
    string? CertificateNo,
    string? ChallanBsr,
    string? ChallanSerial,
    DateOnly? ChallanDate
);

public record ImportForm16Result(
    string DetectedFormat, // "nsdl_rpu_txt" | "form16a_csv" | "traces_certificate_text"
    string? DeductorTan,
    string? DeductorName,
    string? FinancialYear,
    string? Quarter,
    string? FormType,
    List<ImportedTdsChallanRow> Challans,
    List<ImportedTdsDeductionRow> Deductions,
    List<string> Warnings,
    List<string> Errors,
    int ImportedChallansCount,
    int ImportedDeductionsCount,
    bool Committed
);
