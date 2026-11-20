using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.Payroll;

public enum PayrollTaxRegime
{
    NewRegime = 1,
    OldRegime = 2
}

public enum PayrollRunStatus
{
    Draft = 1,
    Approved = 2,
    Disbursed = 3,
    Cancelled = 4
}

public class PayrollEmployee : TenantEntity, ISoftDelete
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string EmployeeCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Pan { get; set; }
    public string? Uan { get; set; }
    public string? EsicNumber { get; set; }
    public DateOnly DateOfJoining { get; set; }
    public string Designation { get; set; } = "";
    public string? Department { get; set; }
    public string State { get; set; } = "Maharashtra";
    public PayrollTaxRegime TaxRegime { get; set; } = PayrollTaxRegime.NewRegime;

    public decimal MonthlyCtc { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal Hra { get; set; }
    public decimal SpecialAllowance { get; set; }
    public decimal OtherAllowances { get; set; }

    public bool IsPfEligible { get; set; } = true;
    public bool IsEsiEligible { get; set; } = true;
    public bool IsPtEligible { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }

    public decimal GrossSalary => BasicSalary + Hra + SpecialAllowance + OtherAllowances;
}

public class PayrollRun : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string PayrollPeriod { get; set; } = ""; // e.g. "2024-10"
    public int Month { get; set; }
    public int Year { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

    public int TotalEmployees { get; set; }
    public decimal TotalGrossPay { get; set; }
    public decimal TotalNetPay { get; set; }
    public decimal TotalEmployeePf { get; set; }
    public decimal TotalEmployerPf { get; set; }
    public decimal TotalEmployeeEsi { get; set; }
    public decimal TotalEmployerEsi { get; set; }
    public decimal TotalPt { get; set; }
    public decimal TotalTds { get; set; }

    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ProcessedByUserId { get; set; }
    public string? Notes { get; set; }

    public List<PayrollPayslip> Payslips { get; set; } = new();
}

public class PayrollPayslip : TenantEntity
{
    public Guid PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }

    public Guid EmployeeId { get; set; }
    public PayrollEmployee? Employee { get; set; }

    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string? Pan { get; set; }
    public string? Uan { get; set; }

    public int DaysWorked { get; set; } = 30;
    public int DaysInMonth { get; set; } = 30;

    // Earnings
    public decimal BasicSalary { get; set; }
    public decimal Hra { get; set; }
    public decimal SpecialAllowance { get; set; }
    public decimal BonusIncentive { get; set; }
    public decimal GrossSalary { get; set; }

    // Deductions
    public decimal EmployeePf { get; set; }
    public decimal EmployeeEsi { get; set; }
    public decimal ProfessionalTax { get; set; }
    public decimal SalaryTds { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }

    // Employer Contributions
    public decimal EmployerEps { get; set; } // 8.33%
    public decimal EmployerEpf { get; set; } // 3.67%
    public decimal EmployerEsi { get; set; } // 3.25%

    public decimal NetSalary { get; set; }
}

public static class StatutoryDeductionCalculator
{
    public const decimal PfCeiling = 15000m;
    public const decimal EsiCeiling = 21000m;

    /// <summary>
    /// Computes Employee PF (12%) and Employer contribution split (EPS 8.33% max ₹1,250 + EPF 3.67%).
    /// Statutory wage ceiling is ₹15,000/month.
    /// </summary>
    public static (decimal EmployeePf, decimal EmployerEps, decimal EmployerEpf) CalculatePf(decimal basicSalary, bool isPfEligible)
    {
        if (!isPfEligible || basicSalary <= 0)
        {
            return (0m, 0m, 0m);
        }

        var pfWage = Math.Min(basicSalary, PfCeiling);
        var eePf = Math.Round(pfWage * 0.12m, 0, MidpointRounding.AwayFromZero);
        var erEps = Math.Min(1250m, Math.Round(pfWage * 0.0833m, 0, MidpointRounding.AwayFromZero));
        var erEpf = Math.Max(0m, eePf - erEps);

        return (eePf, erEps, erEpf);
    }

    /// <summary>
    /// Computes ESIC contributions if Gross Wage <= ₹21,000/month.
    /// Employee: 0.75%, Employer: 3.25%.
    /// </summary>
    public static (decimal EmployeeEsi, decimal EmployerEsi) CalculateEsi(decimal grossSalary, bool isEsiEligible)
    {
        if (!isEsiEligible || grossSalary <= 0 || grossSalary > EsiCeiling)
        {
            return (0m, 0m);
        }

        // ESIC round-off rule: Round up to nearest rupee
        var eeEsi = Math.Ceiling(grossSalary * 0.0075m);
        var erEsi = Math.Round(grossSalary * 0.0325m, 0, MidpointRounding.AwayFromZero);

        return (eeEsi, erEsi);
    }

    /// <summary>
    /// Calculates State-specific Professional Tax (PT).
    /// Defaults to Maharashtra slabs if specified, or generic Indian PT slab.
    /// </summary>
    public static decimal CalculatePt(decimal grossSalary, string state, int month, bool isPtEligible)
    {
        if (!isPtEligible || grossSalary <= 0) return 0m;

        var cleanState = state.Trim().ToLowerInvariant();

        if (cleanState.Contains("maharashtra"))
        {
            if (grossSalary <= 7500m) return 0m;
            if (grossSalary <= 10000m) return 175m;
            // Month 2 (February) is ₹300, all other months ₹200
            return month == 2 ? 300m : 200m;
        }

        if (cleanState.Contains("karnataka"))
        {
            return grossSalary > 15000m ? 200m : 0m;
        }

        if (cleanState.Contains("telangana") || cleanState.Contains("andhra"))
        {
            if (grossSalary <= 15000m) return 0m;
            if (grossSalary <= 20000m) return 150m;
            return 200m;
        }

        if (cleanState.Contains("west bengal"))
        {
            if (grossSalary <= 10000m) return 0m;
            if (grossSalary <= 15000m) return 110m;
            if (grossSalary <= 25000m) return 130m;
            if (grossSalary <= 40000m) return 150m;
            return 200m;
        }

        // Default standard PT: ₹200 for gross > ₹15,000
        return grossSalary > 15000m ? 200m : 0m;
    }

    /// <summary>
    /// Estimates monthly Section 192 Salary TDS based on annualized gross and selected regime.
    /// Under New Regime: Standard Deduction ₹75,000, 87A rebate for taxable income <= ₹7,00,000.
    /// Under Old Regime: Standard Deduction ₹50,000.
    /// </summary>
    public static decimal CalculateMonthlySalaryTds(decimal monthlyGross, PayrollTaxRegime regime)
    {
        if (monthlyGross <= 0) return 0m;

        var annualGross = monthlyGross * 12m;
        decimal standardDeduction = regime == PayrollTaxRegime.NewRegime ? 75000m : 50000m;
        var taxableIncome = Math.Max(0m, annualGross - standardDeduction);

        decimal annualTax = 0m;

        if (regime == PayrollTaxRegime.NewRegime)
        {
            // Section 87A Rebate: if taxable income <= 700,000, zero tax
            if (taxableIncome <= 700000m) return 0m;

            if (taxableIncome > 1500000m)
            {
                annualTax += (taxableIncome - 1500000m) * 0.30m;
                annualTax += 300000m * 0.20m; // 12-15L
                annualTax += 200000m * 0.15m; // 10-12L
                annualTax += 300000m * 0.10m; // 7-10L
                annualTax += 400000m * 0.05m; // 3-7L
            }
            else if (taxableIncome > 1200000m)
            {
                annualTax += (taxableIncome - 1200000m) * 0.20m;
                annualTax += 200000m * 0.15m;
                annualTax += 300000m * 0.10m;
                annualTax += 400000m * 0.05m;
            }
            else if (taxableIncome > 1000000m)
            {
                annualTax += (taxableIncome - 1000000m) * 0.15m;
                annualTax += 300000m * 0.10m;
                annualTax += 400000m * 0.05m;
            }
            else if (taxableIncome > 700000m)
            {
                annualTax += (taxableIncome - 700000m) * 0.10m;
                annualTax += 400000m * 0.05m;
            }
        }
        else
        {
            // Old Regime
            if (taxableIncome <= 500000m) return 0m; // 87A rebate

            if (taxableIncome > 1000000m)
            {
                annualTax += (taxableIncome - 1000000m) * 0.30m;
                annualTax += 500000m * 0.20m;
                annualTax += 250000m * 0.05m;
            }
            else if (taxableIncome > 500000m)
            {
                annualTax += (taxableIncome - 500000m) * 0.20m;
                annualTax += 250000m * 0.05m;
            }
            else if (taxableIncome > 250000m)
            {
                annualTax += (taxableIncome - 250000m) * 0.05m;
            }
        }

        // Add 4% Health & Education Cess
        annualTax += Math.Round(annualTax * 0.04m, 2);

        return Math.Round(annualTax / 12m, 0, MidpointRounding.AwayFromZero);
    }
}

public static class EpfoEcrGenerator
{
    /// <summary>
    /// Generates EPFO standard ECR text format string (#~# delimited).
    /// Record structure:
    /// #~#UAN#~#MEMBER_NAME#~#GROSS_WAGES#~#EPF_WAGES#~#EPS_WAGES#~#EDLI_WAGES#~#EE_EPF_CONTRI#~#ER_EPS_CONTRI#~#ER_EPF_CONTRI#~#NCP_DAYS#~#REFUND_ADV#~#
    /// </summary>
    public static string GenerateEcrText(List<PayrollPayslip> payslips)
    {
        var sb = new StringBuilder();

        foreach (var p in payslips)
        {
            if (p.EmployeePf <= 0 && string.IsNullOrWhiteSpace(p.Uan)) continue;

            var uan = string.IsNullOrWhiteSpace(p.Uan) ? "NOTAVAILABLE" : p.Uan.Trim();
            var name = p.EmployeeName.Replace("#", "").Trim();
            var gross = Math.Round(p.GrossSalary, 0);
            var epfWages = Math.Min(StatutoryDeductionCalculator.PfCeiling, Math.Round(p.BasicSalary, 0));
            var epsWages = epfWages;
            var edliWages = epfWages;
            var eeEpf = Math.Round(p.EmployeePf, 0);
            var erEps = Math.Round(p.EmployerEps, 0);
            var erEpf = Math.Round(p.EmployerEpf, 0);
            var ncpDays = Math.Max(0, p.DaysInMonth - p.DaysWorked);

            sb.Append("#~#");
            sb.Append(uan).Append("#~#");
            sb.Append(name).Append("#~#");
            sb.Append(gross).Append("#~#");
            sb.Append(epfWages).Append("#~#");
            sb.Append(epsWages).Append("#~#");
            sb.Append(edliWages).Append("#~#");
            sb.Append(eeEpf).Append("#~#");
            sb.Append(erEps).Append("#~#");
            sb.Append(erEpf).Append("#~#");
            sb.Append(ncpDays).Append("#~#");
            sb.Append("0#~#"); // Refund of Advance
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
