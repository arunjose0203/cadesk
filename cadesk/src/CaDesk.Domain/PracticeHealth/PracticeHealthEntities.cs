using System.Text.Json;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.PracticeHealth;

public enum ValuationMethod
{
    BlendedWeight = 0,
    DiscountedCashFlow = 1,
    RevenueMultiple = 2,
    EbitdaMultiple = 3
}

public enum ValuationStatus
{
    Draft = 0,
    Finalized = 1,
    ApprovedByPartners = 2
}

public enum PartnerVestingStatus
{
    FullyVested = 0,
    VestingInProgress = 1,
    Retired = 2
}

public enum PartnerTransitionRole
{
    SeniorPartner = 0,
    ManagingPartner = 1,
    JuniorPartner = 2,
    SuccessorInTraining = 3,
    RetiredHonorary = 4
}

public enum RetirementPlanStatus
{
    Draft = 0,
    ApprovedByPartnership = 1,
    ActivePayout = 2,
    FullySettled = 3
}

public enum TransitionStage
{
    Identified = 0,
    IntroductoryMeetingDone = 1,
    ShadowingAudit = 2,
    JointSignOff = 3,
    FullyHandedOver = 4
}

public enum ClientRetentionRisk
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum FirmTierBenchmark
{
    Tier1MetroBig10 = 0,
    Tier2MidSizedMultiPartner = 1,
    Tier3BoutiqueSmall = 2
}

public class PracticeValuation : TenantEntity
{
    public string ValuationName { get; set; } = "Annual Partnership Valuation";
    public DateOnly ValuationDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public ValuationMethod Method { get; set; } = ValuationMethod.BlendedWeight;
    public ValuationStatus Status { get; set; } = ValuationStatus.Draft;

    // Financial Inputs
    public decimal AnnualGrossRevenue { get; set; }
    public decimal RecurringComplianceRevenue { get; set; }
    public decimal AdvisoryAndOneOffRevenue { get; set; }
    public decimal Ebitda { get; set; }
    public decimal PartnerNormalizedCompensation { get; set; }
    public decimal NetWorkingCapital { get; set; }
    public decimal NetTangibleAssets { get; set; }

    // Valuation Assumptions
    public decimal ProjectedRevenueGrowthRate { get; set; } = 0.12m; // 12%
    public decimal DiscountRateWacc { get; set; } = 0.14m; // 14%
    public decimal TerminalGrowthRate { get; set; } = 0.05m; // 5%
    public int ForecastYears { get; set; } = 5;
    public decimal RevenueMultipleBase { get; set; } = 1.35m; // 1.35x
    public decimal EbitdaMultipleBase { get; set; } = 5.50m; // 5.5x
    public decimal AuditFranchiseStickinessPremiumPct { get; set; } = 0.10m; // +10%
    public decimal PartnerConcentrationDiscountPct { get; set; } = 0.15m; // -15%
    public decimal RecurringRevenuePremiumPct { get; set; } = 0.10m; // +10%

    // Valuation Outputs
    public decimal DcfEnterpriseValue { get; set; }
    public decimal MultipleEnterpriseValue { get; set; }
    public decimal BlendedFirmFairMarketValue { get; set; }
    public decimal FirmGoodwillValue { get; set; }
    public decimal ValuePerEquityPoint { get; set; }
    public string? ValuationSummaryJson { get; set; }

    public Guid? PreparedByUserId { get; set; }
    public string? PreparedByName { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? Notes { get; set; }
}

public class PartnerEquity : TenantEntity
{
    public Guid UserId { get; set; }
    public string PartnerName { get; set; } = "";
    public string PartnerEmail { get; set; } = "";
    public string PartnerMembershipNo { get; set; } = "";
    public decimal EquityPercentage { get; set; } // e.g. 25.00
    public decimal CapitalAccountBalance { get; set; }
    public DateOnly JoiningDate { get; set; }
    public DateOnly? TargetRetirementDate { get; set; }
    public PartnerVestingStatus VestingStatus { get; set; } = PartnerVestingStatus.FullyVested;
    public decimal VestedPercentage { get; set; } = 100m;
    public decimal AnnualDrawShare { get; set; }
    public PartnerTransitionRole Role { get; set; } = PartnerTransitionRole.SeniorPartner;
    public bool IsActive { get; set; } = true;
}

public class PartnerRetirementPlan : TenantEntity
{
    public Guid PartnerEquityId { get; set; }
    public string PartnerName { get; set; } = "";
    public DateOnly RetirementDate { get; set; }
    public decimal AgreedFirmValuationAtRetirement { get; set; }
    public decimal PartnerEquityPct { get; set; }
    public decimal GrossEquityValue { get; set; }
    public decimal CapitalAccountRefundAmount { get; set; }
    public decimal GoodwillBuyoutAmount { get; set; }
    public int PayoutPeriodYears { get; set; } = 5;
    public decimal AnnualInterestRatePct { get; set; } = 8.0m;
    public decimal AnnualInstallmentAmount { get; set; }
    public RetirementPlanStatus Status { get; set; } = RetirementPlanStatus.Draft;
    public string? AmortizationScheduleJson { get; set; }
    public string? PartnershipNotes { get; set; }
}

public class ClientSuccessionTransition : TenantEntity
{
    public Guid RetiringPartnerId { get; set; }
    public string RetiringPartnerName { get; set; } = "";
    public Guid SuccessorPartnerId { get; set; }
    public string SuccessorPartnerName { get; set; } = "";
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public decimal AnnualClientFees { get; set; }
    public TransitionStage Stage { get; set; } = TransitionStage.Identified;
    public ClientRetentionRisk Risk { get; set; } = ClientRetentionRisk.Low;
    public DateOnly TargetHandoverDate { get; set; }
    public string? HandoverNotes { get; set; }
}

public class PracticeBenchmark : TenantEntity
{
    public int BenchmarkYear { get; set; } = DateTime.UtcNow.Year;
    public FirmTierBenchmark FirmTier { get; set; } = FirmTierBenchmark.Tier2MidSizedMultiPartner;

    // Firm Actuals
    public decimal ActualRevenuePerPartner { get; set; }
    public decimal ActualRevenuePerEmployee { get; set; }
    public decimal ActualRealizationRate { get; set; }
    public decimal ActualDsoDays { get; set; }
    public decimal ActualStaffLeverageRatio { get; set; }
    public decimal ActualWipLockupDays { get; set; }
    public decimal ActualEbitdaMarginPct { get; set; }

    // Benchmark Norms
    public decimal BenchmarkRevenuePerPartner { get; set; }
    public decimal BenchmarkRevenuePerEmployee { get; set; }
    public decimal BenchmarkRealizationRate { get; set; }
    public decimal BenchmarkDsoDays { get; set; }
    public decimal BenchmarkStaffLeverageRatio { get; set; }
    public decimal BenchmarkWipLockupDays { get; set; }
    public decimal BenchmarkEbitdaMarginPct { get; set; }

    // Health Score
    public decimal OverallHealthScore { get; set; } // 0-100
    public string HealthGrade { get; set; } = "A";
    public string? HealthInsightsJson { get; set; }
}

public static class PracticeValuationEngine
{
    public record ValuationResult(
        decimal DcfEnterpriseValue,
        decimal MultipleEnterpriseValue,
        decimal BlendedFirmFairMarketValue,
        decimal FirmGoodwillValue,
        decimal ValuePerEquityPoint,
        decimal EffectiveRevenueMultiple,
        decimal EffectiveEbitdaMultiple,
        List<ProjectedCashFlowItem> ProjectedCashFlows
    );

    public record ProjectedCashFlowItem(
        int Year,
        decimal ProjectedRevenue,
        decimal ProjectedEbitda,
        decimal FreeCashFlow,
        decimal PresentValue
    );

    public record AmortizationInstallment(
        int Year,
        decimal BeginningBalance,
        decimal PrincipalPaid,
        decimal InterestPaid,
        decimal TotalInstallment,
        decimal EndingBalance
    );

    public static ValuationResult ComputeValuation(
        decimal grossRevenue,
        decimal recurringRevenue,
        decimal advisoryRevenue,
        decimal ebitda,
        decimal partnerNormalizedComp,
        decimal netTangibleAssets,
        decimal revenueGrowthRate,
        decimal discountRateWacc,
        decimal terminalGrowthRate,
        int forecastYears,
        decimal revenueMultipleBase,
        decimal ebitdaMultipleBase,
        decimal auditFranchiseStickinessPremiumPct,
        decimal partnerConcentrationDiscountPct,
        decimal recurringRevenuePremiumPct,
        ValuationMethod method
    )
    {
        // 1. DCF Model
        var cashFlows = new List<ProjectedCashFlowItem>();
        decimal currentRev = grossRevenue;
        decimal ebitdaMargin = grossRevenue > 0 ? (ebitda / grossRevenue) : 0.25m;
        if (ebitdaMargin <= 0) ebitdaMargin = 0.20m;

        decimal totalPvCashFlows = 0;
        int years = forecastYears > 0 ? forecastYears : 5;
        decimal wacc = discountRateWacc > 0 ? discountRateWacc : 0.14m;
        decimal g = terminalGrowthRate > 0 ? terminalGrowthRate : 0.05m;
        if (wacc <= g) wacc = g + 0.04m; // Prevent division by zero or negative denominator

        for (int y = 1; y <= years; y++)
        {
            currentRev *= (1 + revenueGrowthRate);
            decimal projEbitda = currentRev * ebitdaMargin;
            // Free Cash Flow = EBITDA - normalized partner adjustments
            decimal fcf = Math.Max(0, projEbitda - (partnerNormalizedComp * 0.15m));
            decimal discountFactor = (decimal)Math.Pow((double)(1 + wacc), y);
            decimal pv = fcf / discountFactor;
            totalPvCashFlows += pv;

            cashFlows.Add(new ProjectedCashFlowItem(y, Math.Round(currentRev, 2), Math.Round(projEbitda, 2), Math.Round(fcf, 2), Math.Round(pv, 2)));
        }

        // Terminal Value
        decimal finalYearFcf = cashFlows.Last().FreeCashFlow;
        decimal terminalValue = (finalYearFcf * (1 + g)) / (wacc - g);
        decimal pvTerminalValue = terminalValue / (decimal)Math.Pow((double)(1 + wacc), years);
        decimal dcfEnterpriseValue = Math.Round(totalPvCashFlows + pvTerminalValue, 2);

        // 2. Multiples Model
        // Quality premiums / discounts
        decimal recurringRatio = grossRevenue > 0 ? (recurringRevenue / grossRevenue) : 0.5m;
        decimal premiumAdjustment = 0m;
        if (recurringRatio >= 0.70m) premiumAdjustment += recurringRevenuePremiumPct;
        premiumAdjustment += auditFranchiseStickinessPremiumPct;
        premiumAdjustment -= partnerConcentrationDiscountPct;

        decimal effectiveRevMultiple = Math.Max(0.5m, revenueMultipleBase * (1 + premiumAdjustment));
        decimal effectiveEbitdaMultiple = Math.Max(2.0m, ebitdaMultipleBase * (1 + premiumAdjustment));

        decimal revValuation = grossRevenue * effectiveRevMultiple;
        decimal ebitdaValuation = Math.Max(0, ebitda) * effectiveEbitdaMultiple;
        decimal multipleEnterpriseValue = Math.Round((revValuation * 0.5m) + (ebitdaValuation * 0.5m), 2);

        // 3. Blended Fair Market Value
        decimal blendedFmv = method switch
        {
            ValuationMethod.DiscountedCashFlow => dcfEnterpriseValue,
            ValuationMethod.RevenueMultiple => revValuation,
            ValuationMethod.EbitdaMultiple => ebitdaValuation,
            _ => Math.Round((dcfEnterpriseValue * 0.5m) + (multipleEnterpriseValue * 0.5m), 2)
        };

        if (blendedFmv <= 0) blendedFmv = grossRevenue; // Floor at 1x revenue

        decimal goodwill = Math.Max(0, blendedFmv - netTangibleAssets);
        decimal perEquityPoint = Math.Round(blendedFmv / 100m, 2);

        return new ValuationResult(
            DcfEnterpriseValue: dcfEnterpriseValue,
            MultipleEnterpriseValue: multipleEnterpriseValue,
            BlendedFirmFairMarketValue: blendedFmv,
            FirmGoodwillValue: goodwill,
            ValuePerEquityPoint: perEquityPoint,
            EffectiveRevenueMultiple: Math.Round(effectiveRevMultiple, 2),
            EffectiveEbitdaMultiple: Math.Round(effectiveEbitdaMultiple, 2),
            ProjectedCashFlows: cashFlows
        );
    }

    public static List<AmortizationInstallment> ComputeRetirementAmortization(
        decimal totalPayoutAmount,
        int payoutYears,
        decimal annualInterestRatePct
    )
    {
        var schedule = new List<AmortizationInstallment>();
        if (totalPayoutAmount <= 0 || payoutYears <= 0) return schedule;

        decimal r = annualInterestRatePct / 100m;
        decimal installment;

        if (r <= 0)
        {
            installment = Math.Round(totalPayoutAmount / payoutYears, 2);
        }
        else
        {
            // EMI Formula: P * r * (1+r)^n / ((1+r)^n - 1)
            double rDouble = (double)r;
            double factor = Math.Pow(1 + rDouble, payoutYears);
            installment = Math.Round(totalPayoutAmount * (decimal)(rDouble * factor / (factor - 1)), 2);
        }

        decimal balance = totalPayoutAmount;

        for (int y = 1; y <= payoutYears; y++)
        {
            decimal interest = Math.Round(balance * r, 2);
            decimal principal = installment - interest;
            if (y == payoutYears || principal > balance)
            {
                principal = balance;
                installment = principal + interest;
            }
            decimal ending = Math.Max(0, balance - principal);

            schedule.Add(new AmortizationInstallment(
                Year: y,
                BeginningBalance: balance,
                PrincipalPaid: principal,
                InterestPaid: interest,
                TotalInstallment: installment,
                EndingBalance: ending
            ));

            balance = ending;
        }

        return schedule;
    }

    public static (decimal Score, string Grade) ComputePracticeHealthScore(
        decimal actualRevPerPartner,
        decimal benchmarkRevPerPartner,
        decimal actualRealizationRate,
        decimal benchmarkRealizationRate,
        decimal actualDsoDays,
        decimal benchmarkDsoDays,
        decimal actualLeverage,
        decimal benchmarkLeverage,
        decimal actualMargin,
        decimal benchmarkMargin
    )
    {
        // 5 scoring pillars (20 points each = 100 points max)
        decimal score = 0;

        // Pillar 1: Revenue per Partner (target: >= benchmark)
        decimal revRatio = benchmarkRevPerPartner > 0 ? (actualRevPerPartner / benchmarkRevPerPartner) : 1m;
        score += Math.Min(20m, Math.Max(0m, revRatio * 20m));

        // Pillar 2: Realization Rate (target: >= benchmark e.g. 85%)
        decimal realRatio = benchmarkRealizationRate > 0 ? (actualRealizationRate / benchmarkRealizationRate) : 1m;
        score += Math.Min(20m, Math.Max(0m, realRatio * 20m));

        // Pillar 3: DSO Days (lower is better, benchmark e.g. 60 days)
        if (actualDsoDays <= benchmarkDsoDays)
        {
            score += 20m;
        }
        else
        {
            decimal dsoPenalty = ((actualDsoDays - benchmarkDsoDays) / benchmarkDsoDays) * 20m;
            score += Math.Max(0m, 20m - dsoPenalty);
        }

        // Pillar 4: Staff Leverage (target e.g. 5:1 staff to partner)
        decimal levRatio = benchmarkLeverage > 0 ? (actualLeverage / benchmarkLeverage) : 1m;
        score += Math.Min(20m, Math.Max(0m, levRatio * 20m));

        // Pillar 5: EBITDA Margin (target e.g. >= benchmark 30%)
        decimal marginRatio = benchmarkMargin > 0 ? (actualMargin / benchmarkMargin) : 1m;
        score += Math.Min(20m, Math.Max(0m, marginRatio * 20m));

        score = Math.Round(score, 1);

        string grade = score switch
        {
            >= 90m => "A+ Premier Excellence",
            >= 75m => "A Strong Performance",
            >= 60m => "B Healthy Standard",
            >= 45m => "C Needs Improvement",
            _ => "D Critical Attention Required"
        };

        return (score, grade);
    }
}
