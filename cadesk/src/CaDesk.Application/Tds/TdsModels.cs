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
    DateTime CreatedAt
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
    string? Notes
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
    DateTime CreatedAt
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
    bool? ForceHigherRate206Ab
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
