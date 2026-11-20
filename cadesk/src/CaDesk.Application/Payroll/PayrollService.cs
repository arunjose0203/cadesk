using CaDesk.Application.Abstractions;
using CaDesk.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Payroll;

public sealed class PayrollService(IAppDbContext db, ITenantContext tenant) : IPayrollService
{
    public async Task<List<PayrollEmployeeDto>> GetEmployeesAsync(Guid? clientId, bool? activeOnly, CancellationToken ct = default)
    {
        var q = db.PayrollEmployees
            .AsNoTracking()
            .Include(e => e.Client)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(e => e.ClientId == clientId.Value);
        if (activeOnly == true) q = q.Where(e => e.IsActive && e.DeletedAt == null);

        var list = await q.OrderBy(e => e.EmployeeCode).ToListAsync(ct);
        return list.Select(MapEmployeeToDto).ToList();
    }

    public async Task<PayrollEmployeeDto?> GetEmployeeByIdAsync(Guid id, CancellationToken ct = default)
    {
        var emp = await db.PayrollEmployees
            .AsNoTracking()
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return emp is null ? null : MapEmployeeToDto(emp);
    }

    public async Task<PayrollEmployeeDto> CreateEmployeeAsync(SavePayrollEmployeeRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client '{req.ClientId}' not found.");

        var existing = await db.PayrollEmployees.AnyAsync(
            e => e.ClientId == req.ClientId && e.EmployeeCode.ToLower() == req.EmployeeCode.Trim().ToLower() && e.DeletedAt == null, ct);
        if (existing)
        {
            throw new InvalidOperationException($"Employee with code '{req.EmployeeCode}' already exists for this client.");
        }

        var emp = new PayrollEmployee
        {
            ClientId = req.ClientId,
            EmployeeCode = req.EmployeeCode.Trim(),
            Name = req.Name.Trim(),
            Pan = string.IsNullOrWhiteSpace(req.Pan) ? null : req.Pan.Trim().ToUpperInvariant(),
            Uan = string.IsNullOrWhiteSpace(req.Uan) ? null : req.Uan.Trim(),
            EsicNumber = string.IsNullOrWhiteSpace(req.EsicNumber) ? null : req.EsicNumber.Trim(),
            DateOfJoining = req.DateOfJoining,
            Designation = req.Designation.Trim(),
            Department = req.Department?.Trim(),
            State = string.IsNullOrWhiteSpace(req.State) ? "Maharashtra" : req.State.Trim(),
            TaxRegime = req.TaxRegime,
            MonthlyCtc = req.MonthlyCtc,
            BasicSalary = req.BasicSalary,
            Hra = req.Hra,
            SpecialAllowance = req.SpecialAllowance,
            OtherAllowances = req.OtherAllowances,
            IsPfEligible = req.IsPfEligible,
            IsEsiEligible = req.IsEsiEligible,
            IsPtEligible = req.IsPtEligible,
            IsActive = req.IsActive
        };

        db.PayrollEmployees.Add(emp);
        await db.SaveChangesAsync(ct);
        emp.Client = client;

        return MapEmployeeToDto(emp);
    }

    public async Task<PayrollEmployeeDto> UpdateEmployeeAsync(Guid id, SavePayrollEmployeeRequest req, CancellationToken ct = default)
    {
        var emp = await db.PayrollEmployees
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException($"Employee '{id}' not found.");

        emp.EmployeeCode = req.EmployeeCode.Trim();
        emp.Name = req.Name.Trim();
        emp.Pan = string.IsNullOrWhiteSpace(req.Pan) ? null : req.Pan.Trim().ToUpperInvariant();
        emp.Uan = string.IsNullOrWhiteSpace(req.Uan) ? null : req.Uan.Trim();
        emp.EsicNumber = string.IsNullOrWhiteSpace(req.EsicNumber) ? null : req.EsicNumber.Trim();
        emp.DateOfJoining = req.DateOfJoining;
        emp.Designation = req.Designation.Trim();
        emp.Department = req.Department?.Trim();
        emp.State = string.IsNullOrWhiteSpace(req.State) ? "Maharashtra" : req.State.Trim();
        emp.TaxRegime = req.TaxRegime;
        emp.MonthlyCtc = req.MonthlyCtc;
        emp.BasicSalary = req.BasicSalary;
        emp.Hra = req.Hra;
        emp.SpecialAllowance = req.SpecialAllowance;
        emp.OtherAllowances = req.OtherAllowances;
        emp.IsPfEligible = req.IsPfEligible;
        emp.IsEsiEligible = req.IsEsiEligible;
        emp.IsPtEligible = req.IsPtEligible;
        emp.IsActive = req.IsActive;

        await db.SaveChangesAsync(ct);
        return MapEmployeeToDto(emp);
    }

    public async Task DeleteEmployeeAsync(Guid id, CancellationToken ct = default)
    {
        var emp = await db.PayrollEmployees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException($"Employee '{id}' not found.");

        emp.DeletedAt = DateTime.UtcNow;
        emp.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PayrollRunDto>> GetPayrollRunsAsync(Guid? clientId, int? year, CancellationToken ct = default)
    {
        var q = db.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Client)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(r => r.ClientId == clientId.Value);
        if (year.HasValue) q = q.Where(r => r.Year == year.Value);

        var list = await q.OrderByDescending(r => r.PayrollPeriod).ToListAsync(ct);
        return list.Select(MapRunToDto).ToList();
    }

    public async Task<PayrollRunDto?> GetPayrollRunByIdAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

        return run is null ? null : MapRunToDto(run);
    }

    public async Task<PayrollRunDto> ProcessPayrollRunAsync(ProcessPayrollRunRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client '{req.ClientId}' not found.");

        var period = $"{req.Year:D4}-{req.Month:D2}";
        var daysInMonth = DateTime.DaysInMonth(req.Year, req.Month);

        var existingRun = await db.PayrollRuns
            .Include(r => r.Payslips)
            .FirstOrDefaultAsync(r => r.ClientId == req.ClientId && r.PayrollPeriod == period, ct);

        if (existingRun != null)
        {
            if (existingRun.Status is PayrollRunStatus.Approved or PayrollRunStatus.Disbursed)
            {
                throw new InvalidOperationException($"Payroll for {period} is already {existingRun.Status} and cannot be re-calculated.");
            }
            // Remove previous draft payslips to recompute fresh
            db.PayrollPayslips.RemoveRange(existingRun.Payslips);
            db.PayrollRuns.Remove(existingRun);
            await db.SaveChangesAsync(ct);
        }

        var employees = await db.PayrollEmployees
            .Where(e => e.ClientId == req.ClientId && e.IsActive && e.DeletedAt == null)
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync(ct);

        if (employees.Count == 0)
        {
            throw new InvalidOperationException("No active employees found for this client to process payroll.");
        }

        var overrides = req.Overrides?.ToDictionary(o => o.EmployeeId) ?? new();

        var run = new PayrollRun
        {
            ClientId = req.ClientId,
            PayrollPeriod = period,
            Month = req.Month,
            Year = req.Year,
            Status = PayrollRunStatus.Draft,
            ProcessedAt = DateTimeOffset.UtcNow,
            ProcessedByUserId = tenant.HasTenant ? tenant.UserId : null,
            Notes = req.Notes?.Trim()
        };

        var payslips = new List<PayrollPayslip>();

        foreach (var emp in employees)
        {
            var workedDays = daysInMonth;
            decimal bonus = 0m;

            if (overrides.TryGetValue(emp.Id, out var ov))
            {
                if (ov.DaysWorked.HasValue && ov.DaysWorked.Value >= 0 && ov.DaysWorked.Value <= daysInMonth)
                {
                    workedDays = ov.DaysWorked.Value;
                }
                bonus = Math.Max(0m, ov.BonusIncentive);
            }

            var proRataFactor = daysInMonth > 0 ? (decimal)workedDays / daysInMonth : 1m;

            var basic = Math.Round(emp.BasicSalary * proRataFactor, 2);
            var hra = Math.Round(emp.Hra * proRataFactor, 2);
            var special = Math.Round(emp.SpecialAllowance * proRataFactor, 2);
            var other = Math.Round(emp.OtherAllowances * proRataFactor, 2);
            var gross = basic + hra + special + other + bonus;

            var (eePf, erEps, erEpf) = StatutoryDeductionCalculator.CalculatePf(basic, emp.IsPfEligible);
            var (eeEsi, erEsi) = StatutoryDeductionCalculator.CalculateEsi(gross, emp.IsEsiEligible);
            var pt = StatutoryDeductionCalculator.CalculatePt(gross, emp.State, req.Month, emp.IsPtEligible);
            var tds = StatutoryDeductionCalculator.CalculateMonthlySalaryTds(gross, emp.TaxRegime);
            var totalDeductions = eePf + eeEsi + pt + tds;
            var net = Math.Max(0m, gross - totalDeductions);

            var payslip = new PayrollPayslip
            {
                PayrollRun = run,
                EmployeeId = emp.Id,
                EmployeeCode = emp.EmployeeCode,
                EmployeeName = emp.Name,
                Pan = emp.Pan,
                Uan = emp.Uan,
                DaysWorked = workedDays,
                DaysInMonth = daysInMonth,
                BasicSalary = basic,
                Hra = hra,
                SpecialAllowance = special,
                BonusIncentive = bonus,
                GrossSalary = gross,
                EmployeePf = eePf,
                EmployeeEsi = eeEsi,
                ProfessionalTax = pt,
                SalaryTds = tds,
                OtherDeductions = 0m,
                TotalDeductions = totalDeductions,
                EmployerEps = erEps,
                EmployerEpf = erEpf,
                EmployerEsi = erEsi,
                NetSalary = net
            };

            payslips.Add(payslip);
        }

        // Aggregate run summaries
        run.TotalEmployees = payslips.Count;
        run.TotalGrossPay = payslips.Sum(p => p.GrossSalary);
        run.TotalNetPay = payslips.Sum(p => p.NetSalary);
        run.TotalEmployeePf = payslips.Sum(p => p.EmployeePf);
        run.TotalEmployerPf = payslips.Sum(p => p.EmployerEps + p.EmployerEpf);
        run.TotalEmployeeEsi = payslips.Sum(p => p.EmployeeEsi);
        run.TotalEmployerEsi = payslips.Sum(p => p.EmployerEsi);
        run.TotalPt = payslips.Sum(p => p.ProfessionalTax);
        run.TotalTds = payslips.Sum(p => p.SalaryTds);
        run.Payslips = payslips;

        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        run.Client = client;

        return MapRunToDto(run);
    }

    public async Task<PayrollRunDto> UpdatePayrollRunStatusAsync(Guid runId, UpdatePayrollRunStatusRequest req, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run '{runId}' not found.");

        run.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.Notes))
        {
            run.Notes = req.Notes.Trim();
        }

        await db.SaveChangesAsync(ct);
        return MapRunToDto(run);
    }

    public async Task<List<PayrollPayslipDto>> GetPayslipsAsync(Guid runId, CancellationToken ct = default)
    {
        var list = await db.PayrollPayslips
            .AsNoTracking()
            .Where(p => p.PayrollRunId == runId)
            .OrderBy(p => p.EmployeeCode)
            .ToListAsync(ct);

        return list.Select(MapPayslipToDto).ToList();
    }

    public async Task<PayrollPayslipDto?> GetPayslipByIdAsync(Guid payslipId, CancellationToken ct = default)
    {
        var p = await db.PayrollPayslips
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == payslipId, ct);

        return p is null ? null : MapPayslipToDto(p);
    }

    public async Task<EpfoEcrExportDto> ExportEpfoEcrAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns
            .Include(r => r.Client)
            .Include(r => r.Payslips)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new KeyNotFoundException($"Payroll run '{runId}' not found.");

        var ecrText = EpfoEcrGenerator.GenerateEcrText(run.Payslips);

        return new EpfoEcrExportDto(
            run.Id,
            run.PayrollPeriod,
            run.Client?.Name ?? "",
            run.Payslips.Count,
            run.Payslips.Sum(p => p.GrossSalary),
            run.Payslips.Sum(p => p.EmployeePf),
            run.Payslips.Sum(p => p.EmployerEps),
            run.Payslips.Sum(p => p.EmployerEpf),
            ecrText
        );
    }

    public async Task<PayrollSummaryDto> GetPayrollSummaryAsync(Guid? clientId, CancellationToken ct = default)
    {
        var qEmp = db.PayrollEmployees.AsNoTracking().Where(e => e.DeletedAt == null);
        var qRuns = db.PayrollRuns.AsNoTracking();

        if (clientId.HasValue)
        {
            qEmp = qEmp.Where(e => e.ClientId == clientId.Value);
            qRuns = qRuns.Where(r => r.ClientId == clientId.Value);
        }

        var totalEmployees = await qEmp.CountAsync(ct);
        var activeEmployees = await qEmp.CountAsync(e => e.IsActive, ct);

        var activeEmps = await qEmp.Where(e => e.IsActive).ToListAsync(ct);
        decimal monthlyGross = activeEmps.Sum(e => e.GrossSalary);

        decimal totalPf = 0m;
        decimal totalEsi = 0m;
        decimal totalPt = 0m;
        decimal totalTds = 0m;

        foreach (var e in activeEmps)
        {
            var (eePf, erEps, erEpf) = StatutoryDeductionCalculator.CalculatePf(e.BasicSalary, e.IsPfEligible);
            totalPf += (eePf + erEps + erEpf);

            var (eeEsi, erEsi) = StatutoryDeductionCalculator.CalculateEsi(e.GrossSalary, e.IsEsiEligible);
            totalEsi += (eeEsi + erEsi);

            totalPt += StatutoryDeductionCalculator.CalculatePt(e.GrossSalary, e.State, DateTime.UtcNow.Month, e.IsPtEligible);
            totalTds += StatutoryDeductionCalculator.CalculateMonthlySalaryTds(e.GrossSalary, e.TaxRegime);
        }

        var latestRun = await qRuns.OrderByDescending(r => r.PayrollPeriod).FirstOrDefaultAsync(ct);

        return new PayrollSummaryDto(
            totalEmployees,
            activeEmployees,
            monthlyGross,
            totalPf,
            totalEsi,
            totalPt,
            totalTds,
            latestRun?.PayrollPeriod
        );
    }

    private static PayrollEmployeeDto MapEmployeeToDto(PayrollEmployee e)
    {
        var (eePf, _, _) = StatutoryDeductionCalculator.CalculatePf(e.BasicSalary, e.IsPfEligible);
        var (eeEsi, _) = StatutoryDeductionCalculator.CalculateEsi(e.GrossSalary, e.IsEsiEligible);
        var pt = StatutoryDeductionCalculator.CalculatePt(e.GrossSalary, e.State, DateTime.UtcNow.Month, e.IsPtEligible);
        var tds = StatutoryDeductionCalculator.CalculateMonthlySalaryTds(e.GrossSalary, e.TaxRegime);
        var estNet = Math.Max(0m, e.GrossSalary - (eePf + eeEsi + pt + tds));

        return new PayrollEmployeeDto(
            e.Id,
            e.ClientId,
            e.Client?.Name ?? "",
            e.Client?.Code ?? "",
            e.EmployeeCode,
            e.Name,
            e.Pan,
            e.Uan,
            e.EsicNumber,
            e.DateOfJoining,
            e.Designation,
            e.Department,
            e.State,
            e.TaxRegime,
            e.MonthlyCtc,
            e.BasicSalary,
            e.Hra,
            e.SpecialAllowance,
            e.OtherAllowances,
            e.GrossSalary,
            e.IsPfEligible,
            e.IsEsiEligible,
            e.IsPtEligible,
            e.IsActive,
            eePf,
            eeEsi,
            pt,
            tds,
            estNet
        );
    }

    private static PayrollRunDto MapRunToDto(PayrollRun r) =>
        new(
            r.Id,
            r.ClientId,
            r.Client?.Name ?? "",
            r.Client?.Code ?? "",
            r.PayrollPeriod,
            r.Month,
            r.Year,
            r.Status,
            r.TotalEmployees,
            r.TotalGrossPay,
            r.TotalNetPay,
            r.TotalEmployeePf,
            r.TotalEmployerPf,
            r.TotalEmployeeEsi,
            r.TotalEmployerEsi,
            r.TotalPt,
            r.TotalTds,
            r.ProcessedAt,
            r.Notes
        );

    private static PayrollPayslipDto MapPayslipToDto(PayrollPayslip p) =>
        new(
            p.Id,
            p.PayrollRunId,
            p.EmployeeId,
            p.EmployeeCode,
            p.EmployeeName,
            p.Pan,
            p.Uan,
            p.DaysWorked,
            p.DaysInMonth,
            p.BasicSalary,
            p.Hra,
            p.SpecialAllowance,
            p.BonusIncentive,
            p.GrossSalary,
            p.EmployeePf,
            p.EmployeeEsi,
            p.ProfessionalTax,
            p.SalaryTds,
            p.OtherDeductions,
            p.TotalDeductions,
            p.EmployerEps,
            p.EmployerEpf,
            p.EmployerEsi,
            p.NetSalary
        );
}
