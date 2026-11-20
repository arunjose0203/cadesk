using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Tax;

public class TaxComputationService(IAppDbContext db, ITenantContext tenant)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public Task<RegimeComparisonDto> PreviewComparisonAsync(CalculateTaxRequest req)
    {
        return Task.FromResult(TaxEngine.ComputeComparison(req));
    }

    public async Task<TaxComputationDto> SaveComputationAsync(CalculateTaxRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        var comparison = TaxEngine.ComputeComparison(req);
        var optedRegime = req.OptedRegime == "old" ? "old" : (req.OptedRegime == "new" ? "new" : comparison.RecommendedRegime);
        var chosen = optedRegime == "old" ? comparison.OldRegime : comparison.NewRegime;

        var comp = new TaxComputation
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            AssessmentYear = req.AssessmentYear,
            FinancialYear = req.FinancialYear,
            TaxpayerCategory = req.TaxpayerCategory ?? TaxpayerCategory.Individual,
            OptedRegime = optedRegime,
            Status = ComputationStatus.Computed,

            // 5 Heads
            GrossSalary = chosen.GrossSalary,
            SalaryDeductions = chosen.StandardDeduction,
            NetSalary = chosen.NetSalary,
            HousePropertyIncome = chosen.HousePropertyIncome,
            BusinessIncome = chosen.BusinessIncome,
            CapitalGainsStcg = chosen.CapitalGainsStcg,
            CapitalGainsLtcg = chosen.CapitalGainsLtcg,
            OtherSourcesIncome = chosen.OtherSourcesIncome,

            // Aggregates
            GrossTotalIncome = chosen.GrossTotalIncome,
            ChapterViaDeductions = chosen.ChapterViaDeductions,
            TotalTaxableIncome = chosen.TaxableIncome,

            // Tax
            TaxOnIncome = chosen.BaseTax + chosen.SpecialRateTax,
            Rebate87A = chosen.Rebate87A,
            Surcharge = chosen.Surcharge,
            Cess = chosen.Cess,
            TotalTaxLiability = chosen.TotalTaxLiability,

            // Prepaid
            TdsTcsCredit = req.TdsPaid + req.TcsPaid,
            AdvanceTaxPaid = req.AdvanceTaxPaid,
            SelfAssessmentTax = req.SelfAssessmentTaxPaid,
            NetTaxPayableOrRefund = chosen.NetPayableOrRefund,

            // JSON
            ComputationBreakdown = JsonSerializer.Serialize(chosen, JsonOpts),
            ComparisonData = JsonSerializer.Serialize(comparison, JsonOpts),

            Notes = req.Notes?.Trim(),
            CreatedBy = currentUserId
        };

        db.TaxComputations.Add(comp);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(comp.Id, ct);
    }

    public async Task<TaxComputationDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.TaxComputations
            .Include(c => c.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (x is null) throw new BusinessRuleException("Tax computation not found.", 404);

        var comparison = JsonSerializer.Deserialize<RegimeComparisonDto>(x.ComparisonData, JsonOpts)
            ?? new RegimeComparisonDto(null!, null!, x.OptedRegime, 0, "");

        return ToDto(x, comparison);
    }

    public async Task<List<TaxComputationDto>> ListComputationsAsync(
        Guid? clientId = null,
        string? assessmentYear = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var q = db.TaxComputations.Include(c => c.Client).AsNoTracking();
        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(assessmentYear) && assessmentYear != "all")
            q = q.Where(x => x.AssessmentYear == assessmentYear);

        var list = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(take, 100))
            .ToListAsync(ct);

        return list.Select(x =>
        {
            var comparison = JsonSerializer.Deserialize<RegimeComparisonDto>(x.ComparisonData, JsonOpts)
                ?? new RegimeComparisonDto(null!, null!, x.OptedRegime, 0, "");
            return ToDto(x, comparison);
        }).ToList();
    }

    public async Task<TaxComputationDto> UpdateStatusAsync(Guid id, string status, Guid currentUserId, CancellationToken ct = default)
    {
        var comp = await db.TaxComputations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (comp is null) throw new BusinessRuleException("Tax computation not found.", 404);

        if (!ComputationStatus.All.Contains(status))
            throw new RequestValidationException(nameof(status), $"Invalid status '{status}'.");

        comp.Status = status;
        comp.UpdatedBy = currentUserId;
        comp.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var comp = await db.TaxComputations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (comp is null) return false;

        db.TaxComputations.Remove(comp);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static TaxComputationDto ToDto(TaxComputation x, RegimeComparisonDto comp) =>
        new(
            x.Id,
            x.ClientId,
            x.Client?.Name,
            x.AssessmentYear,
            x.FinancialYear,
            x.TaxpayerCategory,
            x.OptedRegime,
            x.Status,
            x.GrossSalary,
            x.SalaryDeductions,
            x.NetSalary,
            x.HousePropertyIncome,
            x.BusinessIncome,
            x.CapitalGainsStcg,
            x.CapitalGainsLtcg,
            x.OtherSourcesIncome,
            x.GrossTotalIncome,
            x.ChapterViaDeductions,
            x.TotalTaxableIncome,
            x.TaxOnIncome,
            x.Rebate87A,
            x.Surcharge,
            x.Cess,
            x.TotalTaxLiability,
            x.TdsTcsCredit,
            x.AdvanceTaxPaid,
            x.SelfAssessmentTax,
            x.NetTaxPayableOrRefund,
            comp,
            x.Notes,
            x.CreatedAt,
            x.UpdatedAt
        );
}
