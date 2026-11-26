using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Tax;

public partial class TaxComputationService(IAppDbContext db, ITenantContext tenant)
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

    public async Task<TaxComputationDto> ImportAisToComputationAsync(
        Guid clientId,
        string aisJson,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == clientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        var parsed = AnnualInformationStatementParser.Parse(aisJson);
        AnnualInformationStatementParser.EnsureBelongsTo(parsed, client.TaxIds, expectedFinancialYear: null);

        decimal sec80Tta = Math.Min(parsed.SavingsInterest, 10_000m);

        var req = new CalculateTaxRequest(
            ClientId: clientId,
            AssessmentYear: parsed.AssessmentYear,
            FinancialYear: parsed.FinancialYear,
            TaxpayerCategory: TaxpayerCategory.Individual,
            OptedRegime: null,
            GrossSalary: parsed.GrossSalary,
            HraReceived: 0m,
            RentPaid: 0m,
            IsMetro: false,
            ProfessionalTax: 0m,
            AnnualRentReceived: 0m,
            MunicipalTaxesPaid: 0m,
            HousingLoanInterest: 0m,
            IsSelfOccupied: true,
            BusinessGrossTurnover: 0m,
            NetProfit: 0m,
            PresumptiveSection: null,
            Stcg111A: parsed.CapitalGainsStcg,
            StcgNormal: 0m,
            Ltcg112A: parsed.CapitalGainsLtcg,
            Ltcg112: 0m,
            SavingsInterest: parsed.SavingsInterest,
            FdInterest: parsed.FixedDepositInterest,
            DividendIncome: parsed.DividendIncome,
            OtherIncome: 0m,
            Section80C: 0m,
            Section80Ccd1B: 0m,
            Section80D: 0m,
            Section80E: 0m,
            Section80G: 0m,
            Section80Tta: sec80Tta,
            TdsPaid: parsed.TotalTds,
            TcsPaid: parsed.TotalTcs,
            AdvanceTaxPaid: parsed.TotalAdvanceTaxPaid,
            SelfAssessmentTaxPaid: parsed.TotalSelfAssessmentTaxPaid,
            Notes: $"Auto-imported from AIS JSON for PAN {parsed.Pan} (FY {parsed.FinancialYear})"
        );

        return await SaveComputationAsync(req, currentUserId, ct);
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

    public async Task<IncomeTaxJsonExportResult> ExportItr1JsonAsync(Guid id, ExportItrJsonRequest? req = null, CancellationToken ct = default)
    {
        var comp = await db.TaxComputations
            .Include(x => x.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (comp is null) throw new BusinessRuleException("Tax computation not found.", 404);

        var client = comp.Client ?? await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == comp.ClientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        return Itr1JsonBuilder.Build(comp, client, req);
    }

    private static TaxComputationDto ToDto(TaxComputation x, RegimeComparisonDto comp)
    {
        BusinessTaxComputationResultDto? business = null;
        PersonalExtendedTaxResultDto? extendedPersonal = null;
        TrustTaxResultDto? trust = null;
        if (!string.IsNullOrWhiteSpace(x.ComputationBreakdown))
        {
            if ((x.TaxpayerCategory == TaxpayerCategory.Firm || x.TaxpayerCategory == TaxpayerCategory.Company)
                && x.ComputationBreakdown.Contains("\"selectedRegimeCode\"", StringComparison.OrdinalIgnoreCase))
            {
                business = JsonSerializer.Deserialize<BusinessTaxComputationResultDto>(x.ComputationBreakdown, JsonOpts);
            }
            else if (x.ComputationBreakdown.Contains("\"returnFormType\"", StringComparison.OrdinalIgnoreCase))
            {
                extendedPersonal = JsonSerializer.Deserialize<PersonalExtendedTaxResultDto>(x.ComputationBreakdown, JsonOpts);
            }
            else if (x.ComputationBreakdown.Contains("\"grossVoluntaryNonCorpus\"", StringComparison.OrdinalIgnoreCase))
            {
                trust = JsonSerializer.Deserialize<TrustTaxResultDto>(x.ComputationBreakdown, JsonOpts);
            }
        }

        return new(
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
            x.UpdatedAt,
            business,
            extendedPersonal,
            trust
        );
    }
}
