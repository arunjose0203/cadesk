namespace CaDesk.Application.Tax;

public record IncomeTaxJsonExportIssue(
    string Severity, // "error" | "warning"
    string Path,
    string Code,
    string Message
);

public record IncomeTaxJsonExportResult(
    string FormType, // "ITR-1" | "ITR-4" | "FORM3CA" | "FORM3CB"
    string SchemaVersion,
    string AssessmentYear,
    string FinancialYear,
    string FileName,
    bool ValidAgainstSchema,
    int SchemaErrorCount,
    string JsonContent,
    List<IncomeTaxJsonExportIssue> Issues
);

public record ExportItrJsonRequest(
    string? FatherName = null,
    string? Dob = null,
    string? ResidenceNo = null,
    string? LocalityOrArea = null,
    string? City = null,
    string? StateCode = null,
    string? PinCode = null,
    string? MobileNo = null,
    string? EmailAddress = null,
    string? EmployerCategory = null,
    int? ReturnFileSec = null,
    string? Place = null,
    string? IfscCode = null,
    string? BankName = null,
    string? BankAccountNo = null,
    string? BankAccountType = null,
    string? IntermediaryCity = null,
    bool? OptOutNewRegime = null,
    decimal? SalaryIncome = null,
    decimal? OtherSourcesIncome = null,
    decimal? ChapterVia80C = null,
    decimal? TdsPaid = null,
    decimal? AdvanceTaxPaid = null
);

public record ExportForm3CdJsonRequest(
    string? AuditorName = null,
    string? AuditorMembershipNo = null,
    string? CaFirmName = null,
    string? FirmRegNum = null,
    string? AuditorAddress = null,
    string? AuditorCity = null,
    string? AuditorStateCode = null,
    string? AuditorPinCode = null,
    string? Place = null,
    string? SigningDate = null,
    string? AuditReportDate = null,
    string? OtherLawActCode = null,
    string? AssesseeCity = null,
    string? AssesseeStateCode = null,
    string? AssesseePinCode = null,
    int? NumberOfBranches = null
);
