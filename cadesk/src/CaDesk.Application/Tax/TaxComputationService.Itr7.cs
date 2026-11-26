using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Tax;

public partial class TaxComputationService
{
    public Task<TrustTaxResultDto> PreviewTrustAsync(CalculateTrustTaxRequest req) =>
        Task.FromResult(TrustTaxEngine.Compute(req));

    public async Task<TaxComputationDto> SaveTrustComputationAsync(
        CalculateTrustTaxRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        var result = TrustTaxEngine.Compute(req);
        var zeroReg = new RegimeTaxResult("old", 0, 0, 0, 0, result.ShortfallTaxableNormalIncome, 0, 0,
            result.OtherObjectAndInvestmentReceipts, result.TotalEligibleIncome, result.NetAllowedApplication + result.Form10Accumulation,
            result.TotalTaxableIncome, result.TaxOnNormalShortfall, result.TaxOnAnonymous115BBC + result.TaxOn115BBI,
            0, result.Surcharge, result.Cess, result.TotalTaxLiability, result.PrepaidTaxes, result.NetPayableOrRefund, []);
        var compat = new RegimeComparisonDto(zeroReg, zeroReg, "old", 0m, result.SummaryNote);

        var comp = new TaxComputation
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            AssessmentYear = req.AssessmentYear,
            FinancialYear = req.FinancialYear,
            TaxpayerCategory = TaxpayerCategory.Trust,
            OptedRegime = "old",
            Status = ComputationStatus.Computed,

            BusinessIncome = result.ShortfallTaxableNormalIncome,
            OtherSourcesIncome = result.OtherObjectAndInvestmentReceipts,
            GrossTotalIncome = result.TotalEligibleIncome,
            ChapterViaDeductions = result.NetAllowedApplication + result.Form10Accumulation,
            TotalTaxableIncome = result.TotalTaxableIncome,

            TaxOnIncome = result.TaxOnNormalShortfall + result.TaxOnAnonymous115BBC + result.TaxOn115BBI,
            Surcharge = result.Surcharge,
            Cess = result.Cess,
            TotalTaxLiability = result.TotalTaxLiability,

            TdsTcsCredit = result.NormalizedInput.TdsCredit + result.NormalizedInput.TcsCredit,
            AdvanceTaxPaid = result.NormalizedInput.AdvanceTaxPaid,
            SelfAssessmentTax = result.NormalizedInput.SelfAssessmentTaxPaid,
            NetTaxPayableOrRefund = result.NetPayableOrRefund,

            ComputationBreakdown = JsonSerializer.Serialize(result, JsonOpts),
            ComparisonData = JsonSerializer.Serialize(compat, JsonOpts),
            Notes = req.Notes?.Trim() ?? $"ITR-7 ({client.LegalName ?? client.Name}) — {result.SummaryNote}",
            CreatedBy = currentUserId,
        };

        db.TaxComputations.Add(comp);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(comp.Id, ct);
    }

    public async Task<IncomeTaxJsonExportResult> ExportItr7JsonAsync(Guid id, ExportItrJsonRequest? req = null, CancellationToken ct = default)
    {
        var comp = await db.TaxComputations
            .Include(x => x.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessRuleException("Tax computation not found.", 404);

        var client = comp.Client ?? await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == comp.ClientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        TrustTaxResultDto? trust = null;
        if (!string.IsNullOrWhiteSpace(comp.ComputationBreakdown)
            && comp.ComputationBreakdown.Contains("\"grossVoluntaryNonCorpus\"", StringComparison.OrdinalIgnoreCase))
        {
            trust = JsonSerializer.Deserialize<TrustTaxResultDto>(comp.ComputationBreakdown, JsonOpts);
        }

        trust ??= TrustTaxEngine.Compute(new CalculateTrustTaxRequest(
            ClientId: comp.ClientId,
            AssessmentYear: comp.AssessmentYear,
            FinancialYear: comp.FinancialYear,
            OtherSpecificAndGeneralDonations: comp.GrossTotalIncome,
            RevenueApplicationCharitable: comp.ChapterViaDeductions,
            TdsCredit: comp.TdsTcsCredit,
            AdvanceTaxPaid: comp.AdvanceTaxPaid,
            SelfAssessmentTaxPaid: comp.SelfAssessmentTax));

        return Itr7JsonBuilder.Build(comp, client, trust, req);
    }
}
