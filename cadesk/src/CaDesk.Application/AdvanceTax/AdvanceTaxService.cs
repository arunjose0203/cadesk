using CaDesk.Application.Abstractions;
using CaDesk.Domain.AdvanceTax;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.AdvanceTax;

public class AdvanceTaxService(IAppDbContext db) : IAdvanceTaxService
{
    public async Task<AdvanceTaxSummaryDto> GetSummaryAsync(Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var qPlans = db.AdvanceTaxPlans
            .Include(p => p.Installments)
            .Include(p => p.InterestComputations)
            .AsNoTracking();

        if (clientId.HasValue)
        {
            qPlans = qPlans.Where(p => p.ClientId == clientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(financialYear))
        {
            qPlans = qPlans.Where(p => p.FinancialYear == financialYear);
        }

        var plans = await qPlans.ToListAsync(ct);
        var totalPlans = plans.Count;
        var activePlans = plans.Count(p => p.Status == AdvanceTaxPlanStatus.Active);
        var totalEstIncome = plans.Sum(p => p.EstimatedGrossIncome);
        var totalAssessedTax = plans.Sum(p => p.AssessedTax);

        var allInstallments = plans.SelectMany(p => p.Installments).ToList();
        var totalPaid = allInstallments.Sum(i => i.AmountPaid);
        var totalShortfall = allInstallments.Sum(i => i.ShortfallAmount);
        var total234c = allInstallments.Sum(i => i.Interest234c);

        var allComputations = plans.SelectMany(p => p.InterestComputations).ToList();
        var total234b = allComputations.Sum(c => c.Interest234b);
        var total234a = allComputations.Sum(c => c.Interest234a);

        return new AdvanceTaxSummaryDto(
            totalPlans,
            activePlans,
            totalEstIncome,
            totalAssessedTax,
            totalPaid,
            totalShortfall,
            total234c,
            total234b,
            total234a
        );
    }

    public async Task<IReadOnlyList<AdvanceTaxPlanDto>> GetPlansAsync(Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var q = db.AdvanceTaxPlans
            .AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.Installments)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(p => p.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(p => p.FinancialYear == financialYear);

        var list = await q.OrderByDescending(p => p.FinancialYear).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<AdvanceTaxPlanDto?> GetPlanByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.AdvanceTaxPlans
            .AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return plan is null ? null : ToDto(plan);
    }

    public async Task<AdvanceTaxPlanDto> CreatePlanAsync(SaveAdvanceTaxPlanRequest req, CancellationToken ct = default)
    {
        var plan = new AdvanceTaxPlan
        {
            Id = Guid.NewGuid(),
            ClientId = req.ClientId,
            FinancialYear = req.FinancialYear.Trim(),
            AssessmentYear = req.AssessmentYear.Trim(),
            Regime = req.Regime,
            IsPresumptive = req.IsPresumptive,
            IsSeniorCitizen = req.IsSeniorCitizen,
            EstimatedGrossIncome = req.EstimatedGrossIncome,
            EstimatedDeductions = req.EstimatedDeductions,
            EstimatedTaxBeforeCredits = req.EstimatedTaxBeforeCredits,
            EstimatedTdsTcs = req.EstimatedTdsTcs,
            OtherReliefs = req.OtherReliefs,
            Status = AdvanceTaxPlanStatus.Active,
            Notes = req.Notes?.Trim()
        };

        plan.RecalculateAssessedTax();

        // Generate standard quarterly installments
        var installments = AdvanceTaxInterestEngine.GenerateInstallmentSchedule(
            plan.Id,
            plan.FinancialYear,
            plan.AssessedTax,
            plan.IsPresumptive
        );

        foreach (var inst in installments)
        {
            plan.Installments.Add(inst);
        }

        db.AdvanceTaxPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        return (await GetPlanByIdAsync(plan.Id, ct))!;
    }

    public async Task<AdvanceTaxPlanDto> UpdatePlanAsync(Guid id, SaveAdvanceTaxPlanRequest req, CancellationToken ct = default)
    {
        var plan = await db.AdvanceTaxPlans
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Advance tax plan {id} not found.");

        plan.FinancialYear = req.FinancialYear.Trim();
        plan.AssessmentYear = req.AssessmentYear.Trim();
        plan.Regime = req.Regime;
        plan.IsPresumptive = req.IsPresumptive;
        plan.IsSeniorCitizen = req.IsSeniorCitizen;
        plan.EstimatedGrossIncome = req.EstimatedGrossIncome;
        plan.EstimatedDeductions = req.EstimatedDeductions;
        plan.EstimatedTaxBeforeCredits = req.EstimatedTaxBeforeCredits;
        plan.EstimatedTdsTcs = req.EstimatedTdsTcs;
        plan.OtherReliefs = req.OtherReliefs;
        plan.Notes = req.Notes?.Trim();

        plan.RecalculateAssessedTax();

        // Update target amounts on existing installments
        foreach (var inst in plan.Installments)
        {
            inst.CumulativeTargetAmount = Math.Round(plan.AssessedTax * (inst.TargetPercentage / 100m), 2);
        }

        // Recalculate 234C
        AdvanceTaxInterestEngine.RecalculateSection234C(
            plan.Installments.OrderBy(i => i.InstallmentNumber).ToList(),
            plan.AssessedTax,
            plan.IsPresumptive,
            plan.IsSeniorCitizen
        );

        await db.SaveChangesAsync(ct);
        return (await GetPlanByIdAsync(plan.Id, ct))!;
    }

    public async Task DeletePlanAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.AdvanceTaxPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is not null)
        {
            db.AdvanceTaxPlans.Remove(plan);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<AdvanceTaxInstallmentDto>> GetInstallmentsAsync(Guid planId, CancellationToken ct = default)
    {
        var list = await db.AdvanceTaxInstallments
            .AsNoTracking()
            .Where(i => i.PlanId == planId)
            .OrderBy(i => i.InstallmentNumber)
            .ToListAsync(ct);

        return list.Select(ToInstallmentDto).ToList();
    }

    public async Task<AdvanceTaxInstallmentDto> RecordPaymentAsync(Guid installmentId, RecordInstallmentPaymentRequest req, CancellationToken ct = default)
    {
        var inst = await db.AdvanceTaxInstallments
            .Include(i => i.Plan)
            .ThenInclude(p => p!.Installments)
            .FirstOrDefaultAsync(i => i.Id == installmentId, ct)
            ?? throw new KeyNotFoundException($"Advance tax installment {installmentId} not found.");

        inst.AmountPaid = req.AmountPaid;
        inst.ChallanBsrCode = req.ChallanBsrCode?.Trim();
        inst.ChallanNumber = req.ChallanNumber?.Trim();
        inst.ChallanDate = req.ChallanDate;
        inst.IsPaid = req.AmountPaid > 0;

        var plan = inst.Plan!;
        var orderedInstallments = plan.Installments.OrderBy(i => i.InstallmentNumber).ToList();

        // Recalculate 234C across installments
        AdvanceTaxInterestEngine.RecalculateSection234C(
            orderedInstallments,
            plan.AssessedTax,
            plan.IsPresumptive,
            plan.IsSeniorCitizen
        );

        await db.SaveChangesAsync(ct);
        return ToInstallmentDto(inst);
    }

    public async Task<TaxInterestComputationDto> CalculateInterestAsync(Guid planId, CalculateStatutoryInterestRequest req, CancellationToken ct = default)
    {
        var plan = await db.AdvanceTaxPlans
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Advance tax plan {planId} not found.");

        var orderedInstallments = plan.Installments.OrderBy(i => i.InstallmentNumber).ToList();

        // 1. Recalculate 234C
        AdvanceTaxInterestEngine.RecalculateSection234C(
            orderedInstallments,
            plan.AssessedTax,
            plan.IsPresumptive,
            plan.IsSeniorCitizen
        );
        var total234c = orderedInstallments.Sum(i => i.Interest234c);
        var totalAdvancePaid = orderedInstallments.Sum(i => i.AmountPaid);

        // 2. Calculate 234B
        var (_, ayEndYear) = AdvanceTaxInterestEngine.ParseYears(plan.FinancialYear);
        var (is234bApplicable, months234b, interest234b) = AdvanceTaxInterestEngine.CalculateSection234B(
            plan.AssessedTax,
            totalAdvancePaid,
            req.ActualReturnFilingDate,
            ayEndYear
        );

        // 3. Calculate 234A
        var (months234a, interest234a) = AdvanceTaxInterestEngine.CalculateSection234A(
            plan.AssessedTax,
            totalAdvancePaid,
            req.SelfAssessmentTaxPaid,
            req.ReturnDueDate,
            req.ActualReturnFilingDate
        );

        var totalInterest = Math.Round(total234c + interest234b + interest234a, 2);
        var netTaxPayable = Math.Max(0m, plan.AssessedTax - totalAdvancePaid - req.SelfAssessmentTaxPaid + totalInterest);

        var computation = new TaxInterestComputation
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            AssessmentYear = plan.AssessmentYear,
            AssessedTax = plan.AssessedTax,
            AdvanceTaxPaid = totalAdvancePaid,
            TdsTcsCredit = plan.EstimatedTdsTcs,
            SelfAssessmentTaxPaid = req.SelfAssessmentTaxPaid,
            ReturnDueDate = req.ReturnDueDate,
            ActualReturnFilingDate = req.ActualReturnFilingDate,
            MonthsDelayed234a = months234a,
            Interest234a = interest234a,
            Is234bApplicable = is234bApplicable,
            MonthsDelayed234b = months234b,
            Interest234b = interest234b,
            Interest234cTotal = total234c,
            TotalStatutoryInterest = totalInterest,
            NetTaxPayable = netTaxPayable,
            ComputedAt = DateTime.UtcNow
        };

        db.TaxInterestComputations.Add(computation);
        await db.SaveChangesAsync(ct);

        return ToComputationDto(computation);
    }

    public async Task<IReadOnlyList<TaxInterestComputationDto>> GetInterestComputationsAsync(Guid planId, CancellationToken ct = default)
    {
        var list = await db.TaxInterestComputations
            .AsNoTracking()
            .Where(c => c.PlanId == planId)
            .OrderByDescending(c => c.ComputedAt)
            .ToListAsync(ct);

        return list.Select(ToComputationDto).ToList();
    }

    private static AdvanceTaxPlanDto ToDto(AdvanceTaxPlan p)
    {
        var totalPaid = p.Installments.Sum(i => i.AmountPaid);
        var total234c = p.Installments.Sum(i => i.Interest234c);

        return new AdvanceTaxPlanDto(
            p.Id,
            p.ClientId,
            p.Client?.Name ?? string.Empty,
            p.Client?.Code ?? string.Empty,
            p.FinancialYear,
            p.AssessmentYear,
            p.Regime,
            p.IsPresumptive,
            p.IsSeniorCitizen,
            p.EstimatedGrossIncome,
            p.EstimatedDeductions,
            p.EstimatedTaxableIncome,
            p.EstimatedTaxBeforeCredits,
            p.EstimatedTdsTcs,
            p.OtherReliefs,
            p.AssessedTax,
            p.Status,
            p.Notes,
            totalPaid,
            total234c,
            p.CreatedAt
        );
    }

    private static AdvanceTaxInstallmentDto ToInstallmentDto(AdvanceTaxInstallment i)
    {
        return new AdvanceTaxInstallmentDto(
            i.Id,
            i.PlanId,
            i.InstallmentNumber,
            i.DueDate,
            i.TargetPercentage,
            i.CumulativeTargetAmount,
            i.AmountPaid,
            i.ShortfallAmount,
            i.Interest234c,
            i.ChallanBsrCode,
            i.ChallanNumber,
            i.ChallanDate,
            i.IsPaid,
            i.CreatedAt
        );
    }

    private static TaxInterestComputationDto ToComputationDto(TaxInterestComputation c)
    {
        return new TaxInterestComputationDto(
            c.Id,
            c.PlanId,
            c.AssessmentYear,
            c.AssessedTax,
            c.AdvanceTaxPaid,
            c.TdsTcsCredit,
            c.SelfAssessmentTaxPaid,
            c.ReturnDueDate,
            c.ActualReturnFilingDate,
            c.MonthsDelayed234a,
            c.Interest234a,
            c.Is234bApplicable,
            c.MonthsDelayed234b,
            c.Interest234b,
            c.Interest234cTotal,
            c.TotalStatutoryInterest,
            c.NetTaxPayable,
            c.ComputedAt
        );
    }
}
