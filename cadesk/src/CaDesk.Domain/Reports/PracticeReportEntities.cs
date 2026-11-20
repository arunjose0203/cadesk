using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;

namespace CaDesk.Domain.Reports;

public class PracticeTarget : TenantEntity
{
    public string FinancialYear { get; set; } = "";
    public decimal TargetAnnualRevenue { get; set; }
    public decimal TargetBillableHours { get; set; }
    public decimal TargetRecoveryPercentage { get; set; } = 85.0m;
    public decimal TargetComplianceSlaPct { get; set; } = 95.0m;
}
