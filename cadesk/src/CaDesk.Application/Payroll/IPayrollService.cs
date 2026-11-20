namespace CaDesk.Application.Payroll;

public interface IPayrollService
{
    // Employee Management
    Task<List<PayrollEmployeeDto>> GetEmployeesAsync(Guid? clientId, bool? activeOnly, CancellationToken ct = default);
    Task<PayrollEmployeeDto?> GetEmployeeByIdAsync(Guid id, CancellationToken ct = default);
    Task<PayrollEmployeeDto> CreateEmployeeAsync(SavePayrollEmployeeRequest req, CancellationToken ct = default);
    Task<PayrollEmployeeDto> UpdateEmployeeAsync(Guid id, SavePayrollEmployeeRequest req, CancellationToken ct = default);
    Task DeleteEmployeeAsync(Guid id, CancellationToken ct = default);

    // Payroll Processing
    Task<List<PayrollRunDto>> GetPayrollRunsAsync(Guid? clientId, int? year, CancellationToken ct = default);
    Task<PayrollRunDto?> GetPayrollRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollRunDto> ProcessPayrollRunAsync(ProcessPayrollRunRequest req, CancellationToken ct = default);
    Task<PayrollRunDto> UpdatePayrollRunStatusAsync(Guid runId, UpdatePayrollRunStatusRequest req, CancellationToken ct = default);

    // Payslips & Details
    Task<List<PayrollPayslipDto>> GetPayslipsAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollPayslipDto?> GetPayslipByIdAsync(Guid payslipId, CancellationToken ct = default);

    // Statutory Exports
    Task<EpfoEcrExportDto> ExportEpfoEcrAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollSummaryDto> GetPayrollSummaryAsync(Guid? clientId, CancellationToken ct = default);
}
