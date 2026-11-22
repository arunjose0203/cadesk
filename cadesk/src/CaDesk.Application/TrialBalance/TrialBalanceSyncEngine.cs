using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Caro;
using CaDesk.Application.Schedule3;
using CaDesk.Application.TaxAudit;
using CaDesk.Domain.Audit;
using CaDesk.Domain.TaxAudit;
using CaDesk.Domain.TrialBalance;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.TrialBalance;

/// <summary>
/// Carries a mapped trial balance into the statutory desks. Rules that keep it safe for audit work:
/// <list type="bullet">
/// <item>Only figures the trial balance actually supports are written; every other field on an existing assessment is
/// kept exactly as the user left it (the desks' save methods replace every field, so requests start from the stored values).</item>
/// <item>Nothing is invented: a figure the trial balance cannot give (debt service, aggregate payments) is left alone and named in the notes.</item>
/// <item>Balances are signed by group nature, so contra balances (an overdrawn bank, accumulated losses) reduce their group.</item>
/// <item>Audit materiality is the auditor's judgement (SA 320): the sync only suggests it, never overwrites it.</item>
/// <item>Each desk is all-or-nothing: if one fails, its half-made changes are discarded and the other desks still sync.</item>
/// </list>
/// </summary>
public class TrialBalanceSyncEngine
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly Schedule3Service _schedule3Service;
    private readonly Form3CdService _form3CdService;
    private readonly CaroService _caroService;

    public TrialBalanceSyncEngine(
        IAppDbContext db,
        ITenantContext tenant,
        Schedule3Service schedule3Service,
        Form3CdService form3CdService,
        CaroService caroService)
    {
        _db = db;
        _tenant = tenant;
        _schedule3Service = schedule3Service;
        _form3CdService = form3CdService;
        _caroService = caroService;
    }

    /// <summary>Signed group totals and the figures derived from them.</summary>
    public sealed record TrialBalanceFigures(
        decimal RevenueFromOperations, decimal OtherIncome, decimal TotalIncome,
        decimal CurrentAssets, decimal CurrentLiabilities, decimal LongTermBorrowings, decimal ShortTermBorrowings, decimal TotalDebt,
        decimal ShareholdersEquity, decimal DeferredTaxLiabilities, decimal TotalAssets, decimal PropertyPlantEquipment,
        decimal CostOfGoodsSold, decimal Purchases, decimal FinanceCosts, decimal TotalExpensesBeforeTax, decimal TaxExpense,
        decimal ProfitBeforeTax, decimal ProfitAfterTax, decimal Ebit,
        decimal AverageInventory, decimal AverageTradeReceivables, decimal AverageTradePayables,
        bool HasProfitAndLoss, bool HasOpeningBalances);

    public static TrialBalanceFigures ComputeFigures(IReadOnlyCollection<TrialBalanceLedger> ledgers)
    {
        decimal Closing(string group) => ledgers.Where(l => l.Schedule3Group == group)
            .Sum(l => TrialBalanceConstants.NaturalBalance(group, l.ClosingBalance));
        decimal Opening(string group) => ledgers.Where(l => l.Schedule3Group == group)
            .Sum(l => TrialBalanceConstants.NaturalBalance(group, l.OpeningBalance));
        decimal Sum(params string[] groups) => groups.Sum(Closing);

        bool hasOpening = ledgers.Any(l => l.OpeningBalance != 0);
        // average of opening and closing; without opening balances in the file, closing is the best available figure
        decimal Average(params string[] groups) => hasOpening
            ? Math.Round(groups.Sum(g => (Opening(g) + Closing(g)) / 2m), 2)
            : groups.Sum(Closing);

        decimal revenue = Closing(TrialBalanceConstants.S3RevenueFromOperations);
        decimal otherIncome = Closing(TrialBalanceConstants.S3OtherIncome);
        decimal currentAssets = Sum(TrialBalanceConstants.S3CurrentInvestments, TrialBalanceConstants.S3Inventories,
            TrialBalanceConstants.S3TradeReceivables, TrialBalanceConstants.S3CashAndBank,
            TrialBalanceConstants.S3ShortTermLoansAndAdvances, TrialBalanceConstants.S3OtherCurrentAssets);
        decimal currentLiabilities = Sum(TrialBalanceConstants.S3ShortTermBorrowings, TrialBalanceConstants.S3TradePayablesMsme,
            TrialBalanceConstants.S3TradePayablesOthers, TrialBalanceConstants.S3OtherCurrentLiabilities, TrialBalanceConstants.S3ShortTermProvisions);
        decimal ltBorrowings = Closing(TrialBalanceConstants.S3LongTermBorrowings);
        decimal stBorrowings = Closing(TrialBalanceConstants.S3ShortTermBorrowings);
        decimal equity = Sum(TrialBalanceConstants.S3ShareCapital, TrialBalanceConstants.S3ReservesAndSurplus);
        decimal dtl = Closing(TrialBalanceConstants.S3DeferredTaxLiabilities);
        decimal ppe = Closing(TrialBalanceConstants.S3PropertyPlantEquipment);
        decimal totalAssets = currentAssets + Sum(TrialBalanceConstants.S3PropertyPlantEquipment, TrialBalanceConstants.S3CapitalWip,
            TrialBalanceConstants.S3NonCurrentInvestments, TrialBalanceConstants.S3DeferredTaxAssets,
            TrialBalanceConstants.S3LongTermLoansAndAdvances, TrialBalanceConstants.S3OtherNonCurrentAssets);

        decimal purchases = Sum(TrialBalanceConstants.S3CostOfMaterialsConsumed, TrialBalanceConstants.S3PurchasesOfStockInTrade);
        decimal cogs = purchases + Closing(TrialBalanceConstants.S3ChangesInInventories);
        decimal financeCosts = Closing(TrialBalanceConstants.S3FinanceCosts);
        decimal expensesBeforeTax = cogs + Sum(TrialBalanceConstants.S3EmployeeBenefitsExpense, TrialBalanceConstants.S3FinanceCosts,
            TrialBalanceConstants.S3DepreciationAndAmortization, TrialBalanceConstants.S3OtherExpenses);
        decimal tax = Closing(TrialBalanceConstants.S3TaxExpense);
        decimal pbt = revenue + otherIncome - expensesBeforeTax;

        bool hasPnl = ledgers.Any(l => l.ClosingBalance != 0 && !IsBalanceSheetGroup(l.Schedule3Group));

        return new TrialBalanceFigures(
            revenue, otherIncome, revenue + otherIncome,
            currentAssets, currentLiabilities, ltBorrowings, stBorrowings, ltBorrowings + stBorrowings,
            equity, dtl, totalAssets, ppe,
            cogs, purchases, financeCosts, expensesBeforeTax, tax,
            pbt, pbt - tax, pbt + financeCosts,
            Average(TrialBalanceConstants.S3Inventories),
            Average(TrialBalanceConstants.S3TradeReceivables),
            Average(TrialBalanceConstants.S3TradePayablesMsme, TrialBalanceConstants.S3TradePayablesOthers),
            hasPnl, hasOpening);
    }

    private static readonly HashSet<string> ProfitAndLossGroups =
    [
        TrialBalanceConstants.S3RevenueFromOperations, TrialBalanceConstants.S3OtherIncome, TrialBalanceConstants.S3CostOfMaterialsConsumed,
        TrialBalanceConstants.S3PurchasesOfStockInTrade, TrialBalanceConstants.S3ChangesInInventories, TrialBalanceConstants.S3EmployeeBenefitsExpense,
        TrialBalanceConstants.S3FinanceCosts, TrialBalanceConstants.S3DepreciationAndAmortization, TrialBalanceConstants.S3OtherExpenses,
        TrialBalanceConstants.S3TaxExpense,
    ];

    private static bool IsBalanceSheetGroup(string group) => !ProfitAndLossGroups.Contains(group);

    public async Task<DeskSyncResultDto> SyncToAllDesksAsync(
        TrialBalanceImport import,
        SyncTrialBalanceRequest req,
        CancellationToken ct = default)
    {
        var ledgers = import.Ledgers.ToList();
        if (!import.IsBalanced)
            throw new BusinessRuleException(
                $"This trial balance does not balance (debits ₹{import.TotalDebit:N2}, credits ₹{import.TotalCredit:N2}). Correct it in Tally and import again before syncing.");
        var unmapped = ledgers.Where(l => !l.IsMapped).Select(l => l.LedgerName).OrderBy(n => n).ToList();
        if (unmapped.Count > 0)
            throw new BusinessRuleException(
                $"Map every ledger before syncing. Still unmapped ({unmapped.Count}): {string.Join(", ", unmapped.Take(10))}{(unmapped.Count > 10 ? ", ..." : "")}.");

        var f = ComputeFigures(ledgers);
        var fy = import.FinancialYear;
        var clientId = import.ClientId;
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);
        var userId = _tenant.UserId ?? throw new BusinessRuleException("Sign in again to sync.", 401);

        var audit = req.SyncAudit ? await RunDeskAsync(() => SuggestAuditMaterialityAsync(clientId, fy, f, ct)) : null;
        var s3 = req.SyncSchedule3 ? await RunDeskAsync(() => SyncSchedule3Async(clientId, fy, f, ct)) : null;
        var f3cd = req.SyncForm3Cd ? await RunDeskAsync(() => SyncForm3CdAsync(clientId, client, fy, f, ledgers, ct)) : null;
        var caro = req.SyncCaro ? await RunDeskAsync(() => SyncCaroAsync(clientId, fy, f, userId, ct)) : null;
        var fa = req.SyncFixedAssets ? await RunDeskAsync(() => ReconcileFixedAssetsAsync(clientId, f, ledgers, ct)) : null;

        var synced = new List<string>();
        if (audit?.Synced == true) synced.Add("Audit");
        if (s3?.Synced == true) synced.Add("Schedule3");
        if (f3cd?.Synced == true) synced.Add("Form3Cd");
        if (caro?.Synced == true) synced.Add("Caro");
        if (fa?.Synced == true) synced.Add("FixedAssets");

        if (synced.Count > 0)
        {
            import.Status = TrialBalanceConstants.StatusSynced;
            import.SyncedAt = DateTime.UtcNow;
            import.SyncedDesks = string.Join(",", synced);
        }
        import.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new DeskSyncResultDto(
            import.Id, fy,
            audit?.Synced ?? false, audit?.Notes,
            s3?.Synced ?? false, s3?.Notes,
            f3cd?.Synced ?? false, f3cd?.Notes,
            caro?.Synced ?? false, caro?.Notes,
            fa?.Synced ?? false, fa?.Notes,
            DateTime.UtcNow);
    }

    private sealed record DeskOutcome(bool Synced, string Notes);

    /// <summary>
    /// Runs one desk. On failure, anything that desk changed but did not save is discarded, so the final save of the
    /// import never writes a half-finished desk. (Desk services save their own work as a single SaveChanges.)
    /// </summary>
    private async Task<DeskOutcome> RunDeskAsync(Func<Task<DeskOutcome>> desk)
    {
        try
        {
            return await desk();
        }
        catch (Exception ex) when (ex is BusinessRuleException or RequestValidationException or InvalidOperationException
                                       or KeyNotFoundException or ArgumentException or DbUpdateException)
        {
            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                                     && e.Entity is not TrialBalanceImport and not TrialBalanceLedger)
                         .ToList())
                entry.State = EntityState.Detached;
            return new DeskOutcome(false, $"Not synced: {ex.Message}");
        }
    }

    // ---- 1. Statutory audit: suggest, never overwrite, the auditor's materiality ----

    private async Task<DeskOutcome> SuggestAuditMaterialityAsync(Guid clientId, string fy, TrialBalanceFigures f, CancellationToken ct)
    {
        var engagement = await _db.AuditEngagements.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ClientId == clientId && a.FinancialYear == fy, ct);
        if (engagement is null)
            return new DeskOutcome(false, "No audit engagement for this client and year yet: create one, then sync again for a materiality suggestion.");

        decimal benchmark = f.TotalIncome > 0 ? f.TotalIncome : f.TotalAssets;
        string basis = f.TotalIncome > 0 ? "total income" : "total assets";
        decimal suggested = Math.Round(benchmark * 0.01m, 2);
        return new DeskOutcome(false,
            $"Suggestion only (materiality is the auditor's judgement under SA 320, so it was not changed): 1% of {basis} " +
            $"₹{benchmark:N0} = ₹{suggested:N0}. Current overall materiality on '{engagement.Title}': ₹{engagement.OverallMateriality:N0}" +
            (engagement.IsArchived || engagement.Status == EngagementStatus.Archived ? " (engagement is archived and locked)." : "."));
    }

    // ---- 2. Schedule III ratios ----

    private async Task<DeskOutcome> SyncSchedule3Async(Guid clientId, string fy, TrialBalanceFigures f, CancellationToken ct)
    {
        var existing = await _db.Schedule3Assessments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClientId == clientId && s.FinancialYear == fy, ct);

        var r = existing is null
            ? new SaveSchedule3AssessmentRequest { ClientId = clientId, FinancialYear = fy, PrecedingFinancialYear = PrecedingYear(fy) }
            : PrefillFrom<SaveSchedule3AssessmentRequest>(existing);

        var notes = new List<string>();
        r.CurrentAssetsCurrent = f.CurrentAssets;
        r.CurrentLiabilitiesCurrent = f.CurrentLiabilities;
        r.TotalDebtCurrent = f.TotalDebt;
        r.ShareholdersEquityCurrent = f.ShareholdersEquity;
        r.CapitalEmployedCurrent = f.ShareholdersEquity + f.TotalDebt + f.DeferredTaxLiabilities;
        r.AverageInventoryCurrent = f.AverageInventory;
        r.AverageTradeReceivablesCurrent = f.AverageTradeReceivables;
        r.AverageTradePayablesCurrent = f.AverageTradePayables;
        if (!f.HasOpeningBalances) notes.Add("no opening balances in the file, so 'average' inventory, receivables and payables use closing balances");

        if (f.HasProfitAndLoss)
        {
            r.TotalIncomeCurrent = f.TotalIncome;
            r.RevenueFromOperationsCurrent = f.RevenueFromOperations;
            r.NetProfitAfterTaxesCurrent = f.ProfitAfterTax;
            r.EbitCurrent = f.Ebit;
            r.CostOfGoodsSoldCurrent = f.CostOfGoodsSold;
            r.NetCreditPurchasesCurrent = f.Purchases;
            notes.Add("net credit purchases taken as total purchases (cash purchases are not separable in a trial balance)");
            if (f.TaxExpense == 0) notes.Add("no ledger mapped to tax expense, so profit after tax equals profit before tax");
        }
        else notes.Add("no profit and loss balances (closed trial balance?), so income, profit and turnover figures were left unchanged");

        // debt service needs principal repayments, which a trial balance does not show
        notes.Add("debt service (interest + principal repaid) left for you to enter");

        await _schedule3Service.CreateOrUpdateAssessmentAsync(existing?.Id, r, ct);
        return new DeskOutcome(true,
            $"{(existing is null ? "Created" : "Updated")} current-year figures: income ₹{f.TotalIncome:N0}, profit after tax ₹{f.ProfitAfterTax:N0}, " +
            $"current ratio {(f.CurrentLiabilities != 0 ? (f.CurrentAssets / f.CurrentLiabilities).ToString("F2") : "n/a")}. Note: {string.Join("; ", notes)}.");
    }

    // ---- 3. Form 3CD ----

    private async Task<DeskOutcome> SyncForm3CdAsync(Guid clientId, Domain.Clients.Client client, string fy, TrialBalanceFigures f,
        List<TrialBalanceLedger> ledgers, CancellationToken ct)
    {
        var existing = await _db.Form3CdAssessments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ClientId == clientId && a.FinancialYear == fy, ct);

        SaveForm3CdAssessmentRequest r;
        if (existing is null)
        {
            r = new SaveForm3CdAssessmentRequest
            {
                ClientId = clientId,
                FinancialYear = fy,
                AssessmentYear = NextYear(fy),
                AssesseeName = client.Name,
                Pan = client.TaxIds.GetValueOrDefault("PAN") ?? "",
                Gstin = client.TaxIds.GetValueOrDefault("GSTIN"),
                Status = client.EntityType,
                AuditType = client.EntityType == "Company" ? Form3CdAuditType.Form3CA : Form3CdAuditType.Form3CB,
                IsAuditedUnderOtherLaw = client.EntityType == "Company",
            };
        }
        else
        {
            r = PrefillFrom<SaveForm3CdAssessmentRequest>(existing);
            // these applicability inputs are not stored; read them back from the stored result so a re-save keeps it
            r.IsAuditedUnderOtherLaw = existing.AuditType == Form3CdAuditType.Form3CA;
            r.IsBusiness = existing.Section44AbClause != Form3CdSection44AbClause.Clause_b_ProfessionReceipts
                           && existing.Section44AbClause != Form3CdSection44AbClause.Clause_d_PresumptiveProfession;
            r.Is44AdLockout = existing.Section44AbClause == Form3CdSection44AbClause.Clause_e_Presumptive44AdLockout;
            r.DeclaresLowerPresumptiveProfit = existing.Section44AbClause is Form3CdSection44AbClause.Clause_c_PresumptiveBusiness
                or Form3CdSection44AbClause.Clause_d_PresumptiveProfession;
        }

        var notes = new List<string>();
        if (f.HasProfitAndLoss) r.TotalTurnoverOrGrossReceipts = f.RevenueFromOperations; // other income is not turnover for 44AB
        else notes.Add("no profit and loss balances, so turnover was left unchanged");

        // the 5% cash test needs the cash book's period receipts and payments; a closing-balance trial balance does not carry them
        var cash = ledgers.Where(l => l.Schedule3Group == TrialBalanceConstants.S3CashAndBank
                                      && l.Form3CdCategory == TrialBalanceConstants.F3CdCashReceipt).ToList();
        bool hasPeriodTotals = cash.Count > 0 && cash.All(l => !(l.DebitAmount == Math.Max(l.ClosingBalance, 0m) && l.CreditAmount == Math.Max(-l.ClosingBalance, 0m)));
        if (hasPeriodTotals)
        {
            r.TotalCashReceipts = cash.Sum(l => l.DebitAmount);
            r.TotalCashPayments = cash.Sum(l => l.CreditAmount);
        }
        else notes.Add("cash receipts / payments for the 5% test need the cash book totals (the file has closing balances only), so they were left unchanged");
        notes.Add("aggregate payments for the 5% test are not in a trial balance and were left unchanged");

        decimal sec43B = ledgers.Where(l => l.Form3CdCategory == TrialBalanceConstants.F3CdSec43BStatutoryDue)
            .Sum(l => Math.Max(0m, -l.ClosingBalance));
        decimal msme = ledgers.Where(l => l.Form3CdCategory == TrialBalanceConstants.F3CdSec43BhMsme)
            .Sum(l => Math.Max(0m, -l.ClosingBalance));

        await _form3CdService.CreateOrUpdateAssessmentAsync(existing?.Id, r, ct);
        return new DeskOutcome(true,
            $"{(existing is null ? "Created" : "Updated")} turnover ₹{r.TotalTurnoverOrGrossReceipts:N0}. To examine for disallowance: unpaid statutory dues " +
            $"₹{sec43B:N0} (43B) and MSME creditors ₹{msme:N0} (43B(h)) at year end. Note: {string.Join("; ", notes)}.");
    }

    // ---- 4. CARO 2020 applicability inputs ----

    private async Task<DeskOutcome> SyncCaroAsync(Guid clientId, string fy, TrialBalanceFigures f, Guid userId, CancellationToken ct)
    {
        var existing = await _db.CaroAssessments.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.FinancialYear == fy, ct);

        var r = existing is null
            ? new SaveCaroAssessmentRequest { ClientId = clientId, FinancialYear = fy }
            : PrefillFrom<SaveCaroAssessmentRequest>(existing);
        r.PaidUpCapitalAndReserves = f.ShareholdersEquity;
        r.TotalBorrowings = f.TotalDebt;
        if (f.HasProfitAndLoss) r.TotalRevenue = f.TotalIncome;

        if (existing is null) await _caroService.CreateAssessmentAsync(r, userId, ct);
        else await _caroService.UpdateAssessmentAsync(existing.Id, r, userId, ct);
        return new DeskOutcome(true,
            $"{(existing is null ? "Created" : "Updated")} applicability inputs: paid-up capital and reserves ₹{f.ShareholdersEquity:N0}, " +
            $"borrowings ₹{f.TotalDebt:N0}{(f.HasProfitAndLoss ? $", total revenue ₹{f.TotalIncome:N0}" : "")}.");
    }

    // ---- 5. Fixed assets: reconcile only (the register is the source of truth) ----

    private async Task<DeskOutcome> ReconcileFixedAssetsAsync(Guid clientId, TrialBalanceFigures f, List<TrialBalanceLedger> ledgers, CancellationToken ct)
    {
        var assets = await _db.FixedAssets.AsNoTracking().Where(a => a.ClientId == clientId).ToListAsync(ct);
        decimal registerGross = assets.Sum(a => a.GrossCost);
        decimal tbBalance = f.PropertyPlantEquipment;
        return new DeskOutcome(true,
            $"Reconciliation only, nothing changed: trial balance fixed assets ₹{tbBalance:N0} vs asset register gross cost ₹{registerGross:N0} " +
            $"(difference ₹{tbBalance - registerGross:N0}; a trial balance usually shows net of depreciation, the register gross).");
    }

    // ---- helpers ----

    /// <summary>Starts a save request from the stored record, so fields the sync does not set are kept as they are.</summary>
    private static T PrefillFrom<T>(object entity) where T : new()
    {
        var request = new T();
        var source = entity.GetType();
        foreach (var target in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            var prop = source.GetProperty(target.Name, BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null && target.PropertyType.IsAssignableFrom(prop.PropertyType))
                target.SetValue(request, prop.GetValue(entity));
        }
        return request;
    }

    /// <summary>"2024-25" → "2023-24".</summary>
    private static string PrecedingYear(string fy) =>
        int.TryParse(fy.AsSpan(0, Math.Min(4, fy.Length)), out var y) ? $"{y - 1}-{y % 100:D2}" : fy;

    /// <summary>"2024-25" → "2025-26" (assessment year under the 1961 Act).</summary>
    private static string NextYear(string fy) =>
        int.TryParse(fy.AsSpan(0, Math.Min(4, fy.Length)), out var y) ? $"{y + 1}-{(y + 2) % 100:D2}" : fy;
}
