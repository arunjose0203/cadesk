using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.AdvanceTax;

public enum AdvanceTaxRegime
{
    NewRegime = 1,
    OldRegime = 2
}

public enum AdvanceTaxPlanStatus
{
    Draft = 1,
    Active = 2,
    Completed = 3
}

public class AdvanceTaxPlan : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = string.Empty; // e.g. 2024-25
    public string AssessmentYear { get; set; } = string.Empty; // e.g. 2025-26

    public AdvanceTaxRegime Regime { get; set; } = AdvanceTaxRegime.NewRegime;
    public bool IsPresumptive { get; set; } // Section 44AD / 44ADA (single March 15 installment)
    public bool IsSeniorCitizen { get; set; } // Section 207(2)

    public decimal EstimatedGrossIncome { get; set; }
    public decimal EstimatedDeductions { get; set; }
    public decimal EstimatedTaxableIncome { get; set; }
    public decimal EstimatedTaxBeforeCredits { get; set; }
    public decimal EstimatedTdsTcs { get; set; }
    public decimal OtherReliefs { get; set; }
    public decimal AssessedTax { get; set; }

    public AdvanceTaxPlanStatus Status { get; set; } = AdvanceTaxPlanStatus.Draft;
    public string? Notes { get; set; }

    public ICollection<AdvanceTaxInstallment> Installments { get; set; } = new List<AdvanceTaxInstallment>();
    public ICollection<TaxInterestComputation> InterestComputations { get; set; } = new List<TaxInterestComputation>();

    public void RecalculateAssessedTax()
    {
        EstimatedTaxableIncome = Math.Max(0m, EstimatedGrossIncome - EstimatedDeductions);
        AssessedTax = Math.Max(0m, EstimatedTaxBeforeCredits - EstimatedTdsTcs - OtherReliefs);
    }
}

public class AdvanceTaxInstallment : TenantEntity
{
    public Guid PlanId { get; set; }
    public AdvanceTaxPlan? Plan { get; set; }

    public int InstallmentNumber { get; set; } // 1, 2, 3, 4
    public DateOnly DueDate { get; set; }
    public decimal TargetPercentage { get; set; } // 15, 45, 75, 100
    public decimal CumulativeTargetAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ShortfallAmount { get; set; }
    public decimal Interest234c { get; set; }

    public string? ChallanBsrCode { get; set; }
    public string? ChallanNumber { get; set; }
    public DateOnly? ChallanDate { get; set; }
    public bool IsPaid { get; set; }
}

public class TaxInterestComputation : TenantEntity
{
    public Guid PlanId { get; set; }
    public AdvanceTaxPlan? Plan { get; set; }

    public string AssessmentYear { get; set; } = string.Empty;
    public decimal AssessedTax { get; set; }
    public decimal AdvanceTaxPaid { get; set; }
    public decimal TdsTcsCredit { get; set; }
    public decimal SelfAssessmentTaxPaid { get; set; }

    public DateOnly ReturnDueDate { get; set; }
    public DateOnly ActualReturnFilingDate { get; set; }

    public int MonthsDelayed234a { get; set; }
    public decimal Interest234a { get; set; }

    public bool Is234bApplicable { get; set; }
    public int MonthsDelayed234b { get; set; }
    public decimal Interest234b { get; set; }

    public decimal Interest234cTotal { get; set; }
    public decimal TotalStatutoryInterest { get; set; }
    public decimal NetTaxPayable { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}

public static class AdvanceTaxInterestEngine
{
    public const decimal Section208MandatoryThreshold = 10_000m;

    public static (int StartYear, int EndYear) ParseYears(string financialYear)
    {
        var parts = financialYear.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out var sy))
        {
            return (sy, sy + 1);
        }
        var cur = DateTime.UtcNow.Month >= 4 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
        return (cur, cur + 1);
    }

    public static List<AdvanceTaxInstallment> GenerateInstallmentSchedule(
        Guid planId,
        string financialYear,
        decimal assessedTax,
        bool isPresumptive)
    {
        var (startYear, endYear) = ParseYears(financialYear);
        var list = new List<AdvanceTaxInstallment>();

        if (isPresumptive)
        {
            // Section 44AD / 44ADA: 100% on or before March 15
            list.Add(new AdvanceTaxInstallment
            {
                Id = Guid.NewGuid(),
                PlanId = planId,
                InstallmentNumber = 1,
                DueDate = new DateOnly(endYear, 3, 15),
                TargetPercentage = 100m,
                CumulativeTargetAmount = assessedTax,
                AmountPaid = 0m,
                ShortfallAmount = assessedTax,
                Interest234c = 0m,
                IsPaid = false
            });
            return list;
        }

        // Standard Schedule: June 15 (15%), Sept 15 (45%), Dec 15 (75%), March 15 (100%)
        list.Add(new AdvanceTaxInstallment
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            InstallmentNumber = 1,
            DueDate = new DateOnly(startYear, 6, 15),
            TargetPercentage = 15m,
            CumulativeTargetAmount = Math.Round(assessedTax * 0.15m, 2),
            AmountPaid = 0m,
            ShortfallAmount = Math.Round(assessedTax * 0.15m, 2),
            Interest234c = 0m,
            IsPaid = false
        });

        list.Add(new AdvanceTaxInstallment
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            InstallmentNumber = 2,
            DueDate = new DateOnly(startYear, 9, 15),
            TargetPercentage = 45m,
            CumulativeTargetAmount = Math.Round(assessedTax * 0.45m, 2),
            AmountPaid = 0m,
            ShortfallAmount = Math.Round(assessedTax * 0.45m, 2),
            Interest234c = 0m,
            IsPaid = false
        });

        list.Add(new AdvanceTaxInstallment
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            InstallmentNumber = 3,
            DueDate = new DateOnly(startYear, 12, 15),
            TargetPercentage = 75m,
            CumulativeTargetAmount = Math.Round(assessedTax * 0.75m, 2),
            AmountPaid = 0m,
            ShortfallAmount = Math.Round(assessedTax * 0.75m, 2),
            Interest234c = 0m,
            IsPaid = false
        });

        list.Add(new AdvanceTaxInstallment
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            InstallmentNumber = 4,
            DueDate = new DateOnly(endYear, 3, 15),
            TargetPercentage = 100m,
            CumulativeTargetAmount = assessedTax,
            AmountPaid = 0m,
            ShortfallAmount = assessedTax,
            Interest234c = 0m,
            IsPaid = false
        });

        return list;
    }

    public static void RecalculateSection234C(
        IList<AdvanceTaxInstallment> installments,
        decimal assessedTax,
        bool isPresumptive,
        bool isSeniorCitizenExempt = false)
    {
        // Senior citizens with no business income or tax < 10,000 are not liable
        if (assessedTax < Section208MandatoryThreshold || isSeniorCitizenExempt)
        {
            foreach (var inst in installments)
            {
                inst.Interest234c = 0m;
                inst.ShortfallAmount = 0m;
            }
            return;
        }

        if (isPresumptive)
        {
            if (installments.Count > 0)
            {
                var inst = installments[0];
                var shortfall = Math.Max(0m, assessedTax - inst.AmountPaid);
                inst.ShortfallAmount = shortfall;
                // 1% for 1 month on shortfall
                inst.Interest234c = shortfall > 0m ? Math.Round(shortfall * 0.01m * 1m, 2) : 0m;
            }
            return;
        }

        decimal runningPaid = 0m;

        for (int i = 0; i < installments.Count; i++)
        {
            var inst = installments[i];
            runningPaid += inst.AmountPaid;

            if (inst.InstallmentNumber == 1)
            {
                // Target: 15%. Safe harbor: 12%
                var target = Math.Round(assessedTax * 0.15m, 2);
                var safeHarbor = Math.Round(assessedTax * 0.12m, 2);
                if (runningPaid < safeHarbor)
                {
                    var shortfall = Math.Max(0m, target - runningPaid);
                    inst.ShortfallAmount = shortfall;
                    inst.Interest234c = Math.Round(shortfall * 0.01m * 3m, 2); // 1% per month for 3 months
                }
                else
                {
                    inst.ShortfallAmount = 0m;
                    inst.Interest234c = 0m;
                }
            }
            else if (inst.InstallmentNumber == 2)
            {
                // Target: 45%. Safe harbor: 36%
                var target = Math.Round(assessedTax * 0.45m, 2);
                var safeHarbor = Math.Round(assessedTax * 0.36m, 2);
                if (runningPaid < safeHarbor)
                {
                    var shortfall = Math.Max(0m, target - runningPaid);
                    inst.ShortfallAmount = shortfall;
                    inst.Interest234c = Math.Round(shortfall * 0.01m * 3m, 2); // 1% per month for 3 months
                }
                else
                {
                    inst.ShortfallAmount = 0m;
                    inst.Interest234c = 0m;
                }
            }
            else if (inst.InstallmentNumber == 3)
            {
                // Target: 75%
                var target = Math.Round(assessedTax * 0.75m, 2);
                if (runningPaid < target)
                {
                    var shortfall = Math.Max(0m, target - runningPaid);
                    inst.ShortfallAmount = shortfall;
                    inst.Interest234c = Math.Round(shortfall * 0.01m * 3m, 2); // 1% per month for 3 months
                }
                else
                {
                    inst.ShortfallAmount = 0m;
                    inst.Interest234c = 0m;
                }
            }
            else if (inst.InstallmentNumber == 4)
            {
                // Target: 100%
                var target = assessedTax;
                if (runningPaid < target)
                {
                    var shortfall = Math.Max(0m, target - runningPaid);
                    inst.ShortfallAmount = shortfall;
                    inst.Interest234c = Math.Round(shortfall * 0.01m * 1m, 2); // 1% per month for 1 month
                }
                else
                {
                    inst.ShortfallAmount = 0m;
                    inst.Interest234c = 0m;
                }
            }
        }
    }

    public static (bool IsApplicable, int Months, decimal Interest) CalculateSection234B(
        decimal assessedTax,
        decimal totalAdvanceTaxPaid,
        DateOnly calculationDate, // usually assessment date or return filing date
        int assessmentYearStart)   // e.g. 2025 for AY 2025-26
    {
        if (assessedTax < Section208MandatoryThreshold)
        {
            return (false, 0, 0m);
        }

        // Section 234B applies if advance tax paid < 90% of assessed tax
        if (totalAdvanceTaxPaid >= Math.Round(assessedTax * 0.90m, 2))
        {
            return (false, 0, 0m);
        }

        var ayStartDate = new DateOnly(assessmentYearStart, 4, 1);
        if (calculationDate < ayStartDate)
        {
            calculationDate = ayStartDate;
        }

        // Months from April 1 to calculation date
        // Any fraction of a month is counted as a full month
        int months = CountMonthsOrPart(ayStartDate, calculationDate);
        if (months < 1) months = 1;

        var shortfall = Math.Max(0m, assessedTax - totalAdvanceTaxPaid);
        var interest = Math.Round(shortfall * 0.01m * months, 2);

        return (true, months, interest);
    }

    public static (int Months, decimal Interest) CalculateSection234A(
        decimal assessedTax,
        decimal advanceTaxPaid,
        decimal selfAssessmentTaxPaidBeforeDueDate,
        DateOnly returnDueDate,
        DateOnly actualReturnFilingDate)
    {
        if (actualReturnFilingDate <= returnDueDate)
        {
            return (0, 0m);
        }

        int months = CountMonthsOrPart(returnDueDate.AddDays(1), actualReturnFilingDate);
        if (months < 1) months = 1;

        var baseAmount = Math.Max(0m, assessedTax - advanceTaxPaid - selfAssessmentTaxPaidBeforeDueDate);
        var interest = Math.Round(baseAmount * 0.01m * months, 2);

        return (months, interest);
    }

    public static int CountMonthsOrPart(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate) return 0;

        int months = ((toDate.Year - fromDate.Year) * 12) + toDate.Month - fromDate.Month;
        if (toDate.Day >= fromDate.Day)
        {
            months += 1;
        }
        else
        {
            if (months <= 0) months = 1;
        }

        return Math.Max(1, months);
    }
}
