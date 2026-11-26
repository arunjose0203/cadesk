using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Tax;

public partial class TaxComputationService
{
    public Task<PersonalExtendedTaxResultDto> PreviewPersonalExtendedAsync(CalculatePersonalExtendedTaxRequest req) =>
        Task.FromResult(PersonalTaxExtendedEngine.Compute(req));

    public async Task<PersonalPrefillResponseDto> PrefillPersonalExtendedAsync(
        Guid clientId,
        string? financialYear = null,
        string? assessmentYear = null,
        string returnFormType = "ITR-3",
        CancellationToken ct = default)
    {
        var bizPre = await PrefillBusinessAsync(clientId, financialYear, assessmentYear, "firm", ct);
        var b = bizPre.PrefilledRequest;
        var client = await db.Clients.AsNoTracking().FirstAsync(c => c.Id == clientId, ct);
        bool isHuf = string.Equals(client.EntityType, "HUF", StringComparison.OrdinalIgnoreCase);

        var req = new CalculatePersonalExtendedTaxRequest(
            ClientId: clientId,
            AssessmentYear: b.AssessmentYear,
            FinancialYear: b.FinancialYear,
            ReturnFormType: returnFormType,
            TaxpayerCategory: isHuf ? TaxpayerCategory.Huf : TaxpayerCategory.Individual,
            StatusType: isHuf ? "H" : "I",
            BusinessName: client.LegalName ?? client.Name,
            NatureOfBusinessCode: b.NatureOfBusinessCode,
            IsTaxAuditApplicable: b.IsTaxAuditApplicable,
            PropCapital: b.PartnerOrShareCapital,
            ReservesAndSurplus: b.ReservesAndSurplus,
            SecuredLoans: b.LongTermBorrowings,
            UnsecuredLoans: b.ShortTermBorrowings,
            TradePayables: b.TradePayables,
            OtherCurrentLiabilities: b.OtherCurrentLiabilities,
            FixedAssetsGrossBlock: b.FixedAssetsGrossBlock,
            FixedAssetsAccumulatedDep: b.FixedAssetsAccumulatedDep,
            FixedAssetsNetBlock: b.FixedAssetsNetBlock,
            Inventories: b.Inventories,
            TradeReceivables: b.TradeReceivables,
            CashAndBankBalances: b.CashAndBankBalances,
            LoansAndAdvancesAsset: b.LoansAndAdvances,
            RevenueFromOperations: b.RevenueFromOperations,
            OtherBusinessIncome: b.OtherIncome,
            PurchasesAndDirectCost: b.PurchasesAndDirectCost,
            ChangesInInventories: b.ChangesInInventories,
            EmployeeBenefitExpense: b.EmployeeBenefitExpense,
            FinanceCost: b.FinanceCost,
            DepreciationPerBooks: b.DepreciationPerBooks,
            AuditFee: b.AuditFee,
            OtherBusinessExpenses: b.OtherExpenses,
            DisallowSec36: b.DisallowSec36,
            DisallowSec37: b.DisallowSec37,
            DisallowSec40a: b.DisallowSec40a,
            DisallowSec40A3: b.DisallowSec40A3,
            DisallowSec43B: b.DisallowSec43B,
            DisallowSec43BhMsme: b.DisallowSec43BhMsme,
            IncomeTaxDepreciationSec32: b.IncomeTaxDepreciationSec32,
            DepreciationBlocks: b.DepreciationBlocks,
            TdsOtherThanSalary: b.TdsCredit,
            AdvanceTaxPaid: b.AdvanceTaxPaid,
            SelfAssessmentTaxPaid: b.SelfAssessmentTaxPaid,
            AdvanceTaxChallans: b.AdvanceTaxChallans,
            Notes: $"Auto-prefilled {returnFormType} for {client.LegalName ?? client.Name} (FY {b.FinancialYear})"
        );

        return new PersonalPrefillResponseDto(req, bizPre.Sources);
    }

    public async Task<TaxComputationDto> SavePersonalExtendedComputationAsync(
        CalculatePersonalExtendedTaxRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        var result = PersonalTaxExtendedEngine.Compute(req);
        var active = result.ActiveSummary;
        var compatComparison = new RegimeComparisonDto(
            OldRegime: BuildCompatPersonal(result.OldRegime),
            NewRegime: BuildCompatPersonal(result.NewRegime),
            RecommendedRegime: result.RecommendedRegime,
            TaxSavings: result.TaxSavings,
            RecommendationReason: result.RecommendationReason
        );

        var comp = new TaxComputation
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            AssessmentYear = req.AssessmentYear,
            FinancialYear = req.FinancialYear,
            TaxpayerCategory = result.StatusType == "H" ? TaxpayerCategory.Huf : req.TaxpayerCategory,
            OptedRegime = result.SelectedRegime,
            Status = ComputationStatus.Computed,

            GrossSalary = active.GrossSalary,
            SalaryDeductions = active.SalaryDeductions,
            NetSalary = active.NetSalary,
            HousePropertyIncome = active.HousePropertyIncome,
            BusinessIncome = active.BusinessIncomeNetPgbp,
            CapitalGainsStcg = active.Stcg111A + active.StcgNormal,
            CapitalGainsLtcg = active.Ltcg112A + active.Ltcg112 + active.VdaIncome115BBH,
            OtherSourcesIncome = active.OtherSourcesIncome,

            GrossTotalIncome = active.GrossTotalIncome,
            ChapterViaDeductions = active.ChapterViaDeductions,
            TotalTaxableIncome = active.TaxableIncome,

            TaxOnIncome = active.SlabTaxOnNormalIncome + active.SpecialRateTax,
            Rebate87A = active.Rebate87A,
            Surcharge = active.Surcharge,
            Cess = active.Cess,
            TotalTaxLiability = active.TotalTaxLiability,

            TdsTcsCredit = result.NormalizedInput.TdsSalary + result.NormalizedInput.TdsOtherThanSalary + result.NormalizedInput.TcsPaid,
            AdvanceTaxPaid = result.NormalizedInput.AdvanceTaxPaid,
            SelfAssessmentTax = result.NormalizedInput.SelfAssessmentTaxPaid,
            NetTaxPayableOrRefund = active.NetPayableOrRefund,

            ComputationBreakdown = JsonSerializer.Serialize(result, JsonOpts),
            ComparisonData = JsonSerializer.Serialize(compatComparison, JsonOpts),
            Notes = req.Notes?.Trim() ?? $"{result.ReturnFormType} ({result.SelectedRegime.ToUpperInvariant()}) — {result.RecommendationReason}",
            CreatedBy = currentUserId,
        };

        db.TaxComputations.Add(comp);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(comp.Id, ct);
    }

    public async Task<IncomeTaxJsonExportResult> ExportItr2JsonAsync(Guid id, ExportItrJsonRequest? req = null, CancellationToken ct = default)
    {
        var (comp, client, ext) = await LoadPersonalExtendedForExportAsync(id, "ITR-2", ct);
        return Itr2JsonBuilder.Build(comp, client, ext, req);
    }

    public async Task<IncomeTaxJsonExportResult> ExportItr3JsonAsync(Guid id, ExportItrJsonRequest? req = null, CancellationToken ct = default)
    {
        var (comp, client, ext) = await LoadPersonalExtendedForExportAsync(id, "ITR-3", ct);
        return Itr3JsonBuilder.Build(comp, client, ext, req);
    }

    private async Task<(TaxComputation Comp, Domain.Clients.Client Client, PersonalExtendedTaxResultDto Ext)> LoadPersonalExtendedForExportAsync(
        Guid id,
        string fallbackFormType,
        CancellationToken ct)
    {
        var comp = await db.TaxComputations
            .Include(x => x.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessRuleException("Tax computation not found.", 404);

        var client = comp.Client ?? await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == comp.ClientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        PersonalExtendedTaxResultDto? ext = null;
        if (!string.IsNullOrWhiteSpace(comp.ComputationBreakdown)
            && comp.ComputationBreakdown.Contains("\"returnFormType\"", StringComparison.OrdinalIgnoreCase))
        {
            ext = JsonSerializer.Deserialize<PersonalExtendedTaxResultDto>(comp.ComputationBreakdown, JsonOpts);
        }

        if (ext is null)
        {
            var synth = new CalculatePersonalExtendedTaxRequest(
                ClientId: comp.ClientId,
                AssessmentYear: comp.AssessmentYear,
                FinancialYear: comp.FinancialYear,
                ReturnFormType: fallbackFormType,
                TaxpayerCategory: comp.TaxpayerCategory,
                StatusType: comp.TaxpayerCategory == TaxpayerCategory.Huf ? "H" : "I",
                OptedRegime: comp.OptedRegime,
                GrossSalary: comp.GrossSalary,
                AnnualRentReceived: Math.Max(0m, comp.HousePropertyIncome),
                HousingLoanInterest: comp.HousePropertyIncome < 0m ? Math.Abs(comp.HousePropertyIncome) : 0m,
                IsSelfOccupied: comp.HousePropertyIncome <= 0m,
                RevenueFromOperations: Math.Max(0m, comp.BusinessIncome),
                Stcg111A: comp.CapitalGainsStcg,
                Ltcg112A: comp.CapitalGainsLtcg,
                OtherSourcesIncome: comp.OtherSourcesIncome,
                Section80C: comp.ChapterViaDeductions,
                TdsOtherThanSalary: comp.TdsTcsCredit,
                AdvanceTaxPaid: comp.AdvanceTaxPaid,
                SelfAssessmentTaxPaid: comp.SelfAssessmentTax
            );
            ext = PersonalTaxExtendedEngine.Compute(synth);
        }

        return (comp, client, ext);
    }

    private static RegimeTaxResult BuildCompatPersonal(PersonalRegimeBreakdownDto p) =>
        new(
            Regime: p.Regime,
            GrossSalary: p.GrossSalary,
            StandardDeduction: p.SalaryDeductions,
            NetSalary: p.NetSalary,
            HousePropertyIncome: p.HousePropertyIncome,
            BusinessIncome: p.BusinessIncomeNetPgbp,
            CapitalGainsStcg: p.Stcg111A + p.StcgNormal,
            CapitalGainsLtcg: p.Ltcg112A + p.Ltcg112 + p.VdaIncome115BBH,
            OtherSourcesIncome: p.OtherSourcesIncome,
            GrossTotalIncome: p.GrossTotalIncome,
            ChapterViaDeductions: p.ChapterViaDeductions,
            TaxableIncome: p.TaxableIncome,
            BaseTax: p.SlabTaxOnNormalIncome,
            SpecialRateTax: p.SpecialRateTax,
            Rebate87A: p.Rebate87A,
            Surcharge: p.Surcharge,
            Cess: p.Cess,
            TotalTaxLiability: p.TotalTaxLiability,
            PrepaidTaxes: p.PrepaidTaxes,
            NetPayableOrRefund: p.NetPayableOrRefund,
            SlabBreakdown: p.SlabBreakdown
        );
}
