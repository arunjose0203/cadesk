using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Schedule3;
using CaDesk.Domain.TrialBalance;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Schedule3;

public class Schedule3Service
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public Schedule3Service(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Schedule3SummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var assessments = await _db.Schedule3Assessments
            .AsNoTracking()
            .ToListAsync(ct);

        int total = assessments.Count;
        int inReview = assessments.Count(a => a.AssessmentStatus == Schedule3Status.InReview);
        int finalized = assessments.Count(a => a.AssessmentStatus == Schedule3Status.Finalized);
        int compliantRounding = assessments.Count(a => a.IsRoundingRuleCompliant);
        int totalSignificant = assessments.Sum(a => a.SignificantVariancesCount);
        int totalMissingExp = assessments.Sum(a => a.MissingExplanationsCount);

        return new Schedule3SummaryDto(
            TotalAssessments: total,
            InReviewCount: inReview,
            FinalizedCount: finalized,
            TotalSignificantVariances: totalSignificant,
            TotalMissingExplanations: totalMissingExp,
            RoundingCompliantCount: compliantRounding
        );
    }

    public async Task<List<Schedule3AssessmentDto>> ListAssessmentsAsync(
        Guid? clientId = null,
        string? fy = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = _db.Schedule3Assessments
            .Include(a => a.Client)
            .Include(a => a.AuditEngagement)
            .Include(a => a.FinalizedByUser)
            .Include(a => a.Ratios)
            .Include(a => a.AgeingItems)
            .AsNoTracking()
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(a => a.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(fy)) q = q.Where(a => a.FinancialYear == fy);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.AssessmentStatus == status);

        var list = await q.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<Schedule3AssessmentDto> GetAssessmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var assessment = await _db.Schedule3Assessments
            .Include(a => a.Client)
            .Include(a => a.AuditEngagement)
            .Include(a => a.FinalizedByUser)
            .Include(a => a.Ratios)
            .Include(a => a.AgeingItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Schedule III assessment {id} not found.");

        return ToDto(assessment);
    }

    public async Task<Schedule3AssessmentDto> CreateOrUpdateAssessmentAsync(
        Guid? id,
        SaveSchedule3AssessmentRequest req,
        CancellationToken ct = default)
    {
        Schedule3Assessment? assessment = null;
        bool isNew = !id.HasValue;

        if (id.HasValue)
        {
            assessment = await _db.Schedule3Assessments
                .Include(a => a.Ratios)
                .Include(a => a.AgeingItems)
                .FirstOrDefaultAsync(a => a.Id == id.Value, ct);

            if (assessment == null)
                throw new KeyNotFoundException($"Schedule III assessment {id.Value} not found.");

            if (assessment.AssessmentStatus == Schedule3Status.Finalized)
                throw new InvalidOperationException("Cannot modify a finalized Schedule III financial ratios assessment.");
        }
        else
        {
            assessment = new Schedule3Assessment
            {
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId
            };
            _db.Schedule3Assessments.Add(assessment);
        }

        assessment.ClientId = req.ClientId;
        assessment.AuditEngagementId = req.AuditEngagementId;
        assessment.FinancialYear = req.FinancialYear;
        assessment.PrecedingFinancialYear = req.PrecedingFinancialYear;
        assessment.AccountingStandard = req.AccountingStandard;
        assessment.RoundingUnit = req.RoundingUnit;

        // Metric inputs
        assessment.TotalIncomeCurrent = req.TotalIncomeCurrent;
        assessment.TotalIncomePreceding = req.TotalIncomePreceding;
        assessment.CurrentAssetsCurrent = req.CurrentAssetsCurrent;
        assessment.CurrentAssetsPreceding = req.CurrentAssetsPreceding;
        assessment.CurrentLiabilitiesCurrent = req.CurrentLiabilitiesCurrent;
        assessment.CurrentLiabilitiesPreceding = req.CurrentLiabilitiesPreceding;
        assessment.TotalDebtCurrent = req.TotalDebtCurrent;
        assessment.TotalDebtPreceding = req.TotalDebtPreceding;
        assessment.ShareholdersEquityCurrent = req.ShareholdersEquityCurrent;
        assessment.ShareholdersEquityPreceding = req.ShareholdersEquityPreceding;
        assessment.NetProfitAfterTaxesCurrent = req.NetProfitAfterTaxesCurrent;
        assessment.NetProfitAfterTaxesPreceding = req.NetProfitAfterTaxesPreceding;
        assessment.EbitCurrent = req.EbitCurrent;
        assessment.EbitPreceding = req.EbitPreceding;
        assessment.DebtServiceCurrent = req.DebtServiceCurrent;
        assessment.DebtServicePreceding = req.DebtServicePreceding;
        assessment.CostOfGoodsSoldCurrent = req.CostOfGoodsSoldCurrent;
        assessment.CostOfGoodsSoldPreceding = req.CostOfGoodsSoldPreceding;
        assessment.AverageInventoryCurrent = req.AverageInventoryCurrent;
        assessment.AverageInventoryPreceding = req.AverageInventoryPreceding;
        assessment.RevenueFromOperationsCurrent = req.RevenueFromOperationsCurrent;
        assessment.RevenueFromOperationsPreceding = req.RevenueFromOperationsPreceding;
        assessment.AverageTradeReceivablesCurrent = req.AverageTradeReceivablesCurrent;
        assessment.AverageTradeReceivablesPreceding = req.AverageTradeReceivablesPreceding;
        assessment.NetCreditPurchasesCurrent = req.NetCreditPurchasesCurrent;
        assessment.NetCreditPurchasesPreceding = req.NetCreditPurchasesPreceding;
        assessment.AverageTradePayablesCurrent = req.AverageTradePayablesCurrent;
        assessment.AverageTradePayablesPreceding = req.AverageTradePayablesPreceding;
        assessment.CapitalEmployedCurrent = req.CapitalEmployedCurrent;
        assessment.CapitalEmployedPreceding = req.CapitalEmployedPreceding;
        assessment.IncomeFromInvestmentsCurrent = req.IncomeFromInvestmentsCurrent;
        assessment.IncomeFromInvestmentsPreceding = req.IncomeFromInvestmentsPreceding;
        assessment.AverageInvestmentsCurrent = req.AverageInvestmentsCurrent;
        assessment.AverageInvestmentsPreceding = req.AverageInvestmentsPreceding;

        if (!string.IsNullOrWhiteSpace(req.AuditorRemarks))
            assessment.AuditorRemarks = req.AuditorRemarks;

        // Rounding rule validation
        var roundingEval = Schedule3Engine.ValidateRoundingRule(assessment.TotalIncomeCurrent, assessment.RoundingUnit);
        assessment.IsRoundingRuleCompliant = roundingEval.IsCompliant;
        assessment.RoundingRuleRecommendation = roundingEval.Rationale;

        // Compute 11 ratios
        var computedRatios = Schedule3Engine.Compute11Ratios(assessment);

        // Preserve existing explanations
        var existingExplanations = assessment.Ratios.ToDictionary(r => r.RatioCode, r => r.VarianceExplanation);

        // Clear and reload ratios
        _db.Schedule3RatioItems.RemoveRange(assessment.Ratios);
        assessment.Ratios.Clear();

        foreach (var cr in computedRatios)
        {
            cr.FirmId = _tenant.FirmId;
            cr.AssessmentId = assessment.Id;
            if (existingExplanations.TryGetValue(cr.RatioCode, out var exp) && !string.IsNullOrWhiteSpace(exp))
            {
                cr.VarianceExplanation = exp;
            }
            assessment.Ratios.Add(cr);
            _db.Schedule3RatioItems.Add(cr);
        }

        // Default Ageing templates if new
        if (isNew)
        {
            var defaultAgeing = Schedule3Engine.GetDefaultAgeingTemplates();
            foreach (var ai in defaultAgeing)
            {
                ai.FirmId = _tenant.FirmId;
                ai.AssessmentId = assessment.Id;
                assessment.AgeingItems.Add(ai);
                _db.Schedule3AgeingItems.Add(ai);
            }
        }

        assessment.SignificantVariancesCount = assessment.Ratios.Count(r => r.IsVarianceSignificant);
        assessment.MissingExplanationsCount = assessment.Ratios.Count(r => r.IsVarianceSignificant && string.IsNullOrWhiteSpace(r.VarianceExplanation));

        if (assessment.AssessmentStatus == Schedule3Status.Draft && assessment.SignificantVariancesCount > 0)
        {
            assessment.AssessmentStatus = Schedule3Status.InReview;
        }

        assessment.GeneratedDisclosureNoteText = Schedule3Engine.GenerateDisclosureNoteText(assessment, assessment.Ratios, assessment.AgeingItems);
        assessment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(assessment.Id, ct);
    }

    public async Task<Schedule3AssessmentDto> UpdateRatioExplanationAsync(
        Guid assessmentId,
        Guid ratioId,
        UpdateRatioExplanationRequest req,
        CancellationToken ct = default)
    {
        var assessment = await _db.Schedule3Assessments
            .Include(a => a.Ratios)
            .Include(a => a.AgeingItems)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Schedule III assessment {assessmentId} not found.");

        if (assessment.AssessmentStatus == Schedule3Status.Finalized)
            throw new InvalidOperationException("Cannot update ratios of a finalized Schedule III assessment.");

        var ratio = assessment.Ratios.FirstOrDefault(r => r.Id == ratioId);
        if (ratio == null)
            throw new KeyNotFoundException($"Ratio {ratioId} not found.");

        ratio.VarianceExplanation = req.VarianceExplanation;

        assessment.SignificantVariancesCount = assessment.Ratios.Count(r => r.IsVarianceSignificant);
        assessment.MissingExplanationsCount = assessment.Ratios.Count(r => r.IsVarianceSignificant && string.IsNullOrWhiteSpace(r.VarianceExplanation));
        assessment.GeneratedDisclosureNoteText = Schedule3Engine.GenerateDisclosureNoteText(assessment, assessment.Ratios, assessment.AgeingItems);
        assessment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(assessmentId, ct);
    }

    public async Task<Schedule3AssessmentDto> UpdateAgeingItemAsync(
        Guid assessmentId,
        Guid ageingId,
        UpdateAgeingItemRequest req,
        CancellationToken ct = default)
    {
        var assessment = await _db.Schedule3Assessments
            .Include(a => a.Ratios)
            .Include(a => a.AgeingItems)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Schedule III assessment {assessmentId} not found.");

        if (assessment.AssessmentStatus == Schedule3Status.Finalized)
            throw new InvalidOperationException("Cannot update ageing items of a finalized Schedule III assessment.");

        var item = assessment.AgeingItems.FirstOrDefault(i => i.Id == ageingId);
        if (item == null)
            throw new KeyNotFoundException($"Ageing item {ageingId} not found.");

        item.Less6MonthsOr1Year = req.Less6MonthsOr1Year;
        item.OneToTwoYears = req.OneToTwoYears;
        item.TwoToThreeYears = req.TwoToThreeYears;
        item.MoreThanThreeYears = req.MoreThanThreeYears;
        item.UnbilledDues = req.UnbilledDues;

        assessment.GeneratedDisclosureNoteText = Schedule3Engine.GenerateDisclosureNoteText(assessment, assessment.Ratios, assessment.AgeingItems);
        assessment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(assessmentId, ct);
    }

    public async Task<string> GenerateDisclosureNoteAsync(Guid id, CancellationToken ct = default)
    {
        var assessment = await _db.Schedule3Assessments
            .Include(a => a.Ratios)
            .Include(a => a.AgeingItems)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Schedule III assessment {id} not found.");

        var text = Schedule3Engine.GenerateDisclosureNoteText(assessment, assessment.Ratios, assessment.AgeingItems);
        assessment.GeneratedDisclosureNoteText = text;
        assessment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return text;
    }

    public async Task<Schedule3AssessmentDto> FinalizeAssessmentAsync(
        Guid id,
        string? auditorRemarks,
        CancellationToken ct = default)
    {
        var assessment = await _db.Schedule3Assessments
            .Include(a => a.Ratios)
            .Include(a => a.AgeingItems)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Schedule III assessment {id} not found.");

        if (assessment.AssessmentStatus == Schedule3Status.Finalized)
            throw new InvalidOperationException("Schedule III assessment is already finalized.");

        if (!string.IsNullOrWhiteSpace(auditorRemarks))
            assessment.AuditorRemarks = auditorRemarks;

        assessment.AssessmentStatus = Schedule3Status.Finalized;
        assessment.FinalizedByUserId = _tenant.UserId;
        assessment.FinalizedAt = DateTime.UtcNow;
        assessment.UpdatedAt = DateTime.UtcNow;

        assessment.GeneratedDisclosureNoteText = Schedule3Engine.GenerateDisclosureNoteText(assessment, assessment.Ratios, assessment.AgeingItems);

        await _db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(id, ct);
    }

    public async Task<Schedule3FinancialPackDto> GetFinancialPackDataAsync(Guid id, CancellationToken ct = default)
    {
        var assessment = await _db.Schedule3Assessments
            .Include(a => a.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"Schedule III assessment {id} not found.");

        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == assessment.ClientId, ct);

        var imports = await _db.TrialBalanceImports
            .Include(x => x.Ledgers)
            .AsNoTracking()
            .Where(x => x.ClientId == assessment.ClientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var currTb = imports.FirstOrDefault(i => i.FinancialYear == assessment.FinancialYear) ?? imports.FirstOrDefault();
        var prevTb = imports.FirstOrDefault(i => i.FinancialYear == assessment.PrecedingFinancialYear);

        static Schedule3LineSnapshot FromLedgers(IReadOnlyCollection<TrialBalanceLedger> ledgers, bool useOpening)
        {
            decimal Nat(string group) => ledgers
                .Where(l => l.Schedule3Group == group)
                .Sum(l => TrialBalanceConstants.NaturalBalance(group, useOpening ? l.OpeningBalance : l.ClosingBalance));

            return new Schedule3LineSnapshot(
                ShareCapital: Nat(TrialBalanceConstants.S3ShareCapital),
                ReservesAndSurplus: Nat(TrialBalanceConstants.S3ReservesAndSurplus),
                LongTermBorrowings: Nat(TrialBalanceConstants.S3LongTermBorrowings),
                DeferredTaxLiabilities: Nat(TrialBalanceConstants.S3DeferredTaxLiabilities),
                OtherLongTermLiabilities: Nat(TrialBalanceConstants.S3OtherLongTermLiabilities),
                LongTermProvisions: Nat(TrialBalanceConstants.S3LongTermProvisions),
                ShortTermBorrowings: Nat(TrialBalanceConstants.S3ShortTermBorrowings),
                TradePayablesMsme: Nat(TrialBalanceConstants.S3TradePayablesMsme),
                TradePayablesOthers: Nat(TrialBalanceConstants.S3TradePayablesOthers),
                OtherCurrentLiabilities: Nat(TrialBalanceConstants.S3OtherCurrentLiabilities),
                ShortTermProvisions: Nat(TrialBalanceConstants.S3ShortTermProvisions),
                PropertyPlantEquipment: Nat(TrialBalanceConstants.S3PropertyPlantEquipment),
                CapitalWip: Nat(TrialBalanceConstants.S3CapitalWip),
                NonCurrentInvestments: Nat(TrialBalanceConstants.S3NonCurrentInvestments),
                DeferredTaxAssets: Nat(TrialBalanceConstants.S3DeferredTaxAssets),
                LongTermLoansAndAdvances: Nat(TrialBalanceConstants.S3LongTermLoansAndAdvances),
                OtherNonCurrentAssets: Nat(TrialBalanceConstants.S3OtherNonCurrentAssets),
                CurrentInvestments: Nat(TrialBalanceConstants.S3CurrentInvestments),
                Inventories: Nat(TrialBalanceConstants.S3Inventories),
                TradeReceivables: Nat(TrialBalanceConstants.S3TradeReceivables),
                CashAndBank: Nat(TrialBalanceConstants.S3CashAndBank),
                ShortTermLoansAndAdvances: Nat(TrialBalanceConstants.S3ShortTermLoansAndAdvances),
                OtherCurrentAssets: Nat(TrialBalanceConstants.S3OtherCurrentAssets),
                RevenueFromOperations: useOpening ? 0m : Nat(TrialBalanceConstants.S3RevenueFromOperations),
                OtherIncome: useOpening ? 0m : Nat(TrialBalanceConstants.S3OtherIncome),
                CostOfMaterialsConsumed: useOpening ? 0m : Nat(TrialBalanceConstants.S3CostOfMaterialsConsumed),
                PurchasesOfStockInTrade: useOpening ? 0m : Nat(TrialBalanceConstants.S3PurchasesOfStockInTrade),
                ChangesInInventories: useOpening ? 0m : Nat(TrialBalanceConstants.S3ChangesInInventories),
                EmployeeBenefitsExpense: useOpening ? 0m : Nat(TrialBalanceConstants.S3EmployeeBenefitsExpense),
                FinanceCosts: useOpening ? 0m : Nat(TrialBalanceConstants.S3FinanceCosts),
                DepreciationAndAmortization: useOpening ? 0m : Nat(TrialBalanceConstants.S3DepreciationAndAmortization),
                OtherExpenses: useOpening ? 0m : Nat(TrialBalanceConstants.S3OtherExpenses),
                TaxExpense: useOpening ? 0m : Nat(TrialBalanceConstants.S3TaxExpense)
            );
        }

        static Schedule3LineSnapshot FromSummary(
            decimal rev, decimal totInc, decimal cogs, decimal ebit, decimal pat,
            decimal equity, decimal totDebt, decimal currLiab, decimal currAssets, decimal avgInv, decimal avgRec, decimal avgPay, decimal avgInvst, decimal capEmp, decimal debtSvc)
        {
            decimal ltDebt = Math.Max(0m, totDebt - Math.Min(totDebt, currLiab * 0.25m));
            decimal stDebt = Math.Max(0m, totDebt - ltDebt);
            decimal payables = Math.Min(avgPay, Math.Max(0m, currLiab - stDebt));
            decimal othCurrLiab = Math.Max(0m, currLiab - stDebt - payables);
            decimal shareCap = Math.Round(equity * 0.30m, 0);
            decimal reserves = equity - shareCap;
            decimal nonCurrAssets = Math.Max(0m, capEmp + currLiab - currAssets);
            decimal cash = Math.Max(0m, currAssets - avgInv - avgRec - avgInvst);
            decimal otherInc = Math.Max(0m, totInc - rev);
            decimal finCost = Math.Max(0m, Math.Min(debtSvc, ebit * 0.35m));
            decimal pbt = ebit - finCost;
            decimal taxExp = Math.Max(0m, pbt - pat);
            decimal opEx = Math.Max(0m, totInc - cogs - ebit);

            return new Schedule3LineSnapshot(
                ShareCapital: shareCap, ReservesAndSurplus: reserves,
                LongTermBorrowings: ltDebt, DeferredTaxLiabilities: 0m, OtherLongTermLiabilities: 0m, LongTermProvisions: 0m,
                ShortTermBorrowings: stDebt, TradePayablesMsme: 0m, TradePayablesOthers: payables, OtherCurrentLiabilities: othCurrLiab, ShortTermProvisions: 0m,
                PropertyPlantEquipment: Math.Max(0m, nonCurrAssets - Math.Min(nonCurrAssets, avgInvst)), CapitalWip: 0m,
                NonCurrentInvestments: Math.Min(nonCurrAssets, avgInvst), DeferredTaxAssets: 0m, LongTermLoansAndAdvances: 0m, OtherNonCurrentAssets: 0m,
                CurrentInvestments: 0m, Inventories: avgInv, TradeReceivables: avgRec, CashAndBank: cash, ShortTermLoansAndAdvances: 0m, OtherCurrentAssets: 0m,
                RevenueFromOperations: rev, OtherIncome: otherInc, CostOfMaterialsConsumed: cogs, PurchasesOfStockInTrade: 0m, ChangesInInventories: 0m,
                EmployeeBenefitsExpense: Math.Round(opEx * 0.50m, 0), FinanceCosts: finCost, DepreciationAndAmortization: Math.Round(opEx * 0.15m, 0),
                OtherExpenses: Math.Round(opEx * 0.35m, 0), TaxExpense: taxExp);
        }

        Schedule3LineSnapshot curr = currTb is { Ledgers.Count: > 0 }
            ? FromLedgers(currTb.Ledgers, useOpening: false)
            : FromSummary(
                assessment.RevenueFromOperationsCurrent, assessment.TotalIncomeCurrent, assessment.CostOfGoodsSoldCurrent,
                assessment.EbitCurrent, assessment.NetProfitAfterTaxesCurrent, assessment.ShareholdersEquityCurrent,
                assessment.TotalDebtCurrent, assessment.CurrentLiabilitiesCurrent, assessment.CurrentAssetsCurrent,
                assessment.AverageInventoryCurrent, assessment.AverageTradeReceivablesCurrent, assessment.AverageTradePayablesCurrent,
                assessment.AverageInvestmentsCurrent, assessment.CapitalEmployedCurrent, assessment.DebtServiceCurrent);

        Schedule3LineSnapshot prev;
        if (prevTb is { Ledgers.Count: > 0 })
        {
            prev = FromLedgers(prevTb.Ledgers, useOpening: false);
        }
        else if (currTb is { Ledgers.Count: > 0 } && currTb.Ledgers.Any(l => l.OpeningBalance != 0m))
        {
            var openBs = FromLedgers(currTb.Ledgers, useOpening: true);
            var sumPrev = FromSummary(
                assessment.RevenueFromOperationsPreceding, assessment.TotalIncomePreceding, assessment.CostOfGoodsSoldPreceding,
                assessment.EbitPreceding, assessment.NetProfitAfterTaxesPreceding, assessment.ShareholdersEquityPreceding,
                assessment.TotalDebtPreceding, assessment.CurrentLiabilitiesPreceding, assessment.CurrentAssetsPreceding,
                assessment.AverageInventoryPreceding, assessment.AverageTradeReceivablesPreceding, assessment.AverageTradePayablesPreceding,
                assessment.AverageInvestmentsPreceding, assessment.CapitalEmployedPreceding, assessment.DebtServicePreceding);
            prev = openBs with
            {
                RevenueFromOperations = sumPrev.RevenueFromOperations,
                OtherIncome = sumPrev.OtherIncome,
                CostOfMaterialsConsumed = sumPrev.CostOfMaterialsConsumed,
                EmployeeBenefitsExpense = sumPrev.EmployeeBenefitsExpense,
                FinanceCosts = sumPrev.FinanceCosts,
                DepreciationAndAmortization = sumPrev.DepreciationAndAmortization,
                OtherExpenses = sumPrev.OtherExpenses,
                TaxExpense = sumPrev.TaxExpense,
            };
        }
        else
        {
            prev = FromSummary(
                assessment.RevenueFromOperationsPreceding, assessment.TotalIncomePreceding, assessment.CostOfGoodsSoldPreceding,
                assessment.EbitPreceding, assessment.NetProfitAfterTaxesPreceding, assessment.ShareholdersEquityPreceding,
                assessment.TotalDebtPreceding, assessment.CurrentLiabilitiesPreceding, assessment.CurrentAssetsPreceding,
                assessment.AverageInventoryPreceding, assessment.AverageTradeReceivablesPreceding, assessment.AverageTradePayablesPreceding,
                assessment.AverageInvestmentsPreceding, assessment.CapitalEmployedPreceding, assessment.DebtServicePreceding);
        }

        string? pan = assessment.Client?.TaxIds is not null
            ? assessment.Client.TaxIds.FirstOrDefault(kv => string.Equals(kv.Key, "PAN", StringComparison.OrdinalIgnoreCase)).Value
            : null;
        string? cin = !string.IsNullOrWhiteSpace(company?.Cin)
            ? company.Cin
            : assessment.Client?.TaxIds?.FirstOrDefault(kv => string.Equals(kv.Key, "CIN", StringComparison.OrdinalIgnoreCase)).Value;
        string note = currTb is not null
            ? $"Derived from synced Trial Balance '{currTb.FileName}' (FY {currTb.FinancialYear}; {currTb.TotalLedgersCount} ledgers)."
            : "Derived from Schedule III Assessment analytical summary figures (upload & sync Trial Balance for full ledger-level breakup).";

        return new Schedule3FinancialPackDto(
            AssessmentId: assessment.Id,
            ClientId: assessment.ClientId,
            ClientName: assessment.Client?.LegalName ?? assessment.Client?.Name ?? "Company Assessee",
            Cin: cin,
            Pan: pan,
            FinancialYear: assessment.FinancialYear,
            PrecedingFinancialYear: assessment.PrecedingFinancialYear,
            AccountingStandard: assessment.AccountingStandard,
            RoundingUnit: assessment.RoundingUnit,
            DerivedFromTrialBalance: currTb is not null,
            SourceNote: note,
            CurrentYear: curr,
            PrecedingYear: prev
        );
    }

    private static Schedule3AssessmentDto ToDto(Schedule3Assessment a)
    {
        return new Schedule3AssessmentDto(
            Id: a.Id,
            ClientId: a.ClientId,
            ClientName: a.Client?.Name,
            AuditEngagementId: a.AuditEngagementId,
            AuditEngagementTitle: a.AuditEngagement?.Title,
            FinancialYear: a.FinancialYear,
            PrecedingFinancialYear: a.PrecedingFinancialYear,
            AccountingStandard: a.AccountingStandard,
            RoundingUnit: a.RoundingUnit,

            TotalIncomeCurrent: a.TotalIncomeCurrent,
            TotalIncomePreceding: a.TotalIncomePreceding,
            CurrentAssetsCurrent: a.CurrentAssetsCurrent,
            CurrentAssetsPreceding: a.CurrentAssetsPreceding,
            CurrentLiabilitiesCurrent: a.CurrentLiabilitiesCurrent,
            CurrentLiabilitiesPreceding: a.CurrentLiabilitiesPreceding,
            TotalDebtCurrent: a.TotalDebtCurrent,
            TotalDebtPreceding: a.TotalDebtPreceding,
            ShareholdersEquityCurrent: a.ShareholdersEquityCurrent,
            ShareholdersEquityPreceding: a.ShareholdersEquityPreceding,
            NetProfitAfterTaxesCurrent: a.NetProfitAfterTaxesCurrent,
            NetProfitAfterTaxesPreceding: a.NetProfitAfterTaxesPreceding,
            EbitCurrent: a.EbitCurrent,
            EbitPreceding: a.EbitPreceding,
            DebtServiceCurrent: a.DebtServiceCurrent,
            DebtServicePreceding: a.DebtServicePreceding,
            CostOfGoodsSoldCurrent: a.CostOfGoodsSoldCurrent,
            CostOfGoodsSoldPreceding: a.CostOfGoodsSoldPreceding,
            AverageInventoryCurrent: a.AverageInventoryCurrent,
            AverageInventoryPreceding: a.AverageInventoryPreceding,
            RevenueFromOperationsCurrent: a.RevenueFromOperationsCurrent,
            RevenueFromOperationsPreceding: a.RevenueFromOperationsPreceding,
            AverageTradeReceivablesCurrent: a.AverageTradeReceivablesCurrent,
            AverageTradeReceivablesPreceding: a.AverageTradeReceivablesPreceding,
            NetCreditPurchasesCurrent: a.NetCreditPurchasesCurrent,
            NetCreditPurchasesPreceding: a.NetCreditPurchasesPreceding,
            AverageTradePayablesCurrent: a.AverageTradePayablesCurrent,
            AverageTradePayablesPreceding: a.AverageTradePayablesPreceding,
            CapitalEmployedCurrent: a.CapitalEmployedCurrent,
            CapitalEmployedPreceding: a.CapitalEmployedPreceding,
            IncomeFromInvestmentsCurrent: a.IncomeFromInvestmentsCurrent,
            IncomeFromInvestmentsPreceding: a.IncomeFromInvestmentsPreceding,
            AverageInvestmentsCurrent: a.AverageInvestmentsCurrent,
            AverageInvestmentsPreceding: a.AverageInvestmentsPreceding,

            IsRoundingRuleCompliant: a.IsRoundingRuleCompliant,
            RoundingRuleRecommendation: a.RoundingRuleRecommendation,
            SignificantVariancesCount: a.SignificantVariancesCount,
            MissingExplanationsCount: a.MissingExplanationsCount,
            AssessmentStatus: a.AssessmentStatus,
            AuditorRemarks: a.AuditorRemarks,
            GeneratedDisclosureNoteText: a.GeneratedDisclosureNoteText,
            FinalizedByUserId: a.FinalizedByUserId,
            FinalizedByName: a.FinalizedByUser?.Name,
            FinalizedAt: a.FinalizedAt,
            CreatedAt: a.CreatedAt,
            UpdatedAt: a.UpdatedAt,
            Ratios: a.Ratios
                .OrderBy(r => r.RatioCode)
                .Select(r => new Schedule3RatioItemDto(
                    Id: r.Id,
                    AssessmentId: r.AssessmentId,
                    RatioCode: r.RatioCode,
                    RatioName: r.RatioName,
                    NumeratorDescription: r.NumeratorDescription,
                    DenominatorDescription: r.DenominatorDescription,
                    CurrentYearValue: r.CurrentYearValue,
                    PrecedingYearValue: r.PrecedingYearValue,
                    VariancePercent: r.VariancePercent,
                    IsVarianceSignificant: r.IsVarianceSignificant,
                    VarianceExplanation: r.VarianceExplanation,
                    IsExplanationProvided: r.IsExplanationProvided
                )).ToList(),
            AgeingItems: a.AgeingItems
                .OrderBy(i => i.ScheduleType).ThenBy(i => i.ParticularsCategory)
                .Select(i => new Schedule3AgeingItemDto(
                    Id: i.Id,
                    AssessmentId: i.AssessmentId,
                    ScheduleType: i.ScheduleType,
                    ParticularsCategory: i.ParticularsCategory,
                    Less6MonthsOr1Year: i.Less6MonthsOr1Year,
                    OneToTwoYears: i.OneToTwoYears,
                    TwoToThreeYears: i.TwoToThreeYears,
                    MoreThanThreeYears: i.MoreThanThreeYears,
                    UnbilledDues: i.UnbilledDues,
                    Total: i.Total
                )).ToList()
        );
    }
}
