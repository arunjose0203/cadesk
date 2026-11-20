using CaDesk.Domain.Payroll;

namespace CaDesk.Application.Payroll;

public record PayrollEmployeeDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string EmployeeCode,
    string Name,
    string? Pan,
    string? Uan,
    string? EsicNumber,
    DateOnly DateOfJoining,
    string Designation,
    string? Department,
    string State,
    PayrollTaxRegime TaxRegime,
    decimal MonthlyCtc,
    decimal BasicSalary,
    decimal Hra,
    decimal SpecialAllowance,
    decimal OtherAllowances,
    decimal GrossSalary,
    bool IsPfEligible,
    bool IsEsiEligible,
    bool IsPtEligible,
    bool IsActive,
    decimal EstimatedPf,
    decimal EstimatedEsi,
    decimal EstimatedPt,
    decimal EstimatedTds,
    decimal EstimatedNet
);

public record PayrollRunDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string PayrollPeriod,
    int Month,
    int Year,
    PayrollRunStatus Status,
    int TotalEmployees,
    decimal TotalGrossPay,
    decimal TotalNetPay,
    decimal TotalEmployeePf,
    decimal TotalEmployerPf,
    decimal TotalEmployeeEsi,
    decimal TotalEmployerEsi,
    decimal TotalPt,
    decimal TotalTds,
    DateTimeOffset ProcessedAt,
    string? Notes
);

public record PayrollPayslipDto(
    Guid Id,
    Guid PayrollRunId,
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string? Pan,
    string? Uan,
    int DaysWorked,
    int DaysInMonth,
    decimal BasicSalary,
    decimal Hra,
    decimal SpecialAllowance,
    decimal BonusIncentive,
    decimal GrossSalary,
    decimal EmployeePf,
    decimal EmployeeEsi,
    decimal ProfessionalTax,
    decimal SalaryTds,
    decimal OtherDeductions,
    decimal TotalDeductions,
    decimal EmployerEps,
    decimal EmployerEpf,
    decimal EmployerEsi,
    decimal NetSalary
);

public record PayrollSummaryDto(
    int TotalEmployees,
    int ActiveEmployees,
    decimal TotalMonthlyGross,
    decimal TotalMonthlyPfLiability,
    decimal TotalMonthlyEsiLiability,
    decimal TotalMonthlyPt,
    decimal TotalMonthlyTds,
    string? LatestPayrollPeriod
);

public record EpfoEcrExportDto(
    Guid PayrollRunId,
    string PayrollPeriod,
    string ClientName,
    int EmployeeCount,
    decimal TotalWages,
    decimal TotalEmployeePf,
    decimal TotalEmployerEps,
    decimal TotalEmployerEpf,
    string EcrText
);

public record SavePayrollEmployeeRequest(
    Guid ClientId,
    string EmployeeCode,
    string Name,
    string? Pan,
    string? Uan,
    string? EsicNumber,
    DateOnly DateOfJoining,
    string Designation,
    string? Department,
    string State,
    PayrollTaxRegime TaxRegime,
    decimal MonthlyCtc,
    decimal BasicSalary,
    decimal Hra,
    decimal SpecialAllowance,
    decimal OtherAllowances,
    bool IsPfEligible,
    bool IsEsiEligible,
    bool IsPtEligible,
    bool IsActive
);

public record EmployeeBonusOverride(
    Guid EmployeeId,
    decimal BonusIncentive,
    int? DaysWorked
);

public record ProcessPayrollRunRequest(
    Guid ClientId,
    int Year,
    int Month,
    string? Notes,
    List<EmployeeBonusOverride>? Overrides
);

public record UpdatePayrollRunStatusRequest(
    PayrollRunStatus Status,
    string? Notes
);
