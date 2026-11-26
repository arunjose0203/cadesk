using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Application.TrialBalance;
using CaDesk.Domain.FixedAssets;
using CaDesk.Domain.Tax;
using CaDesk.Domain.TrialBalance;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Tax;

public partial class TaxComputationService
{
    public Task<BusinessTaxComputationResultDto> PreviewBusinessAsync(CalculateBusinessTaxRequest req) =>
        Task.FromResult(BusinessTaxEngine.Compute(req));

    public async Task<BusinessPrefillResponseDto> PrefillBusinessAsync(
        Guid clientId,
        string? financialYear = null,
        string? assessmentYear = null,
        string? entityTypeOverride = null,
        CancellationToken ct = default)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        string fy = string.IsNullOrWhiteSpace(financialYear) ? "2025-26" : financialYear.Trim();
        string ay = string.IsNullOrWhiteSpace(assessmentYear) ? DeriveAyFromFy(fy) : assessmentYear.Trim();

        string rawEntity = (entityTypeOverride ?? client.EntityType ?? "").Trim().ToLowerInvariant();
        bool isFirm = rawEntity is "firm" or "partnership" or "llp" or "aop" or "boi";
        string entityType = isFirm ? "firm" : "company";
        string subType = rawEntity switch
        {
            "llp" => "LLP",
            "partnership" or "firm" => "Partnership",
            "aop" or "boi" => "AOP",
            "public" or "publiccompany" => "Public",
            _ => isFirm ? "Partnership" : "Private"
        };

        var sources = new List<BusinessPrefillSourceNote>();

        // 1. Latest Trial Balance for Client (matching FY if present, else latest available)
        var tbImport = await db.TrialBalanceImports
            .Include(x => x.Ledgers)
            .AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.FinancialYear == fy)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        decimal shareOrPartnerCap = 0m, authCap = 100_000m, reserves = 0m, ltBorrowings = 0m, stBorrowings = 0m;
        decimal dtl = 0m, tradePayables = 0m, otherCurrLiab = 0m;
        decimal ppeNet = 0m, cwipp = 0m, ncInv = 0m, cInv = 0m, inventories = 0m, receivables = 0m, cashBank = 0m, loansAdv = 0m, otherCurrAssets = 0m;
        decimal revenue = 0m, otherIncome = 0m, interestIncome = 0m, dividendIncome = 0m, rentIncome = 0m;
        decimal purchases = 0m, changesInInv = 0m, empBenefit = 0m, partnerRemun = 0m, financeCost = 0m, partnerInterest = 0m;
        decimal depBooks = 0m, otherExpenses = 0m, taxCurr = 0m;
        decimal disallow40a = 0m, disallow40A3 = 0m, disallow43B = 0m, disallow43Bh = 0m, disallow36 = 0m;

        if (tbImport is { Ledgers.Count: > 0 })
        {
            var ledgers = tbImport.Ledgers;
            var fig = TrialBalanceSyncEngine.ComputeFigures(ledgers);

            decimal NatSum(string s3Group) =>
                ledgers.Where(l => l.Schedule3Group == s3Group)
                       .Sum(l => TrialBalanceConstants.NaturalBalance(s3Group, l.ClosingBalance));

            shareOrPartnerCap = Math.Max(0m, NatSum(TrialBalanceConstants.S3ShareCapital));
            reserves = NatSum(TrialBalanceConstants.S3ReservesAndSurplus);
            ltBorrowings = Math.Max(0m, fig.LongTermBorrowings);
            stBorrowings = Math.Max(0m, fig.ShortTermBorrowings);
            dtl = Math.Max(0m, fig.DeferredTaxLiabilities);
            tradePayables = Math.Max(0m, NatSum(TrialBalanceConstants.S3TradePayablesMsme) + NatSum(TrialBalanceConstants.S3TradePayablesOthers));
            otherCurrLiab = Math.Max(0m, NatSum(TrialBalanceConstants.S3OtherCurrentLiabilities) + NatSum(TrialBalanceConstants.S3ShortTermProvisions));

            ppeNet = Math.Max(0m, fig.PropertyPlantEquipment);
            cwipp = Math.Max(0m, NatSum(TrialBalanceConstants.S3CapitalWip));
            ncInv = Math.Max(0m, NatSum(TrialBalanceConstants.S3NonCurrentInvestments));
            cInv = Math.Max(0m, NatSum(TrialBalanceConstants.S3CurrentInvestments));
            inventories = Math.Max(0m, NatSum(TrialBalanceConstants.S3Inventories));
            receivables = Math.Max(0m, NatSum(TrialBalanceConstants.S3TradeReceivables));
            cashBank = Math.Max(0m, NatSum(TrialBalanceConstants.S3CashAndBank));
            loansAdv = Math.Max(0m, NatSum(TrialBalanceConstants.S3LongTermLoansAndAdvances) + NatSum(TrialBalanceConstants.S3ShortTermLoansAndAdvances));
            otherCurrAssets = Math.Max(0m, NatSum(TrialBalanceConstants.S3OtherCurrentAssets) + NatSum(TrialBalanceConstants.S3OtherNonCurrentAssets));

            revenue = Math.Max(0m, fig.RevenueFromOperations);
            otherIncome = Math.Max(0m, fig.OtherIncome);
            purchases = Math.Max(0m, fig.Purchases);
            changesInInv = NatSum(TrialBalanceConstants.S3ChangesInInventories);
            empBenefit = Math.Max(0m, NatSum(TrialBalanceConstants.S3EmployeeBenefitsExpense));
            financeCost = Math.Max(0m, fig.FinanceCosts);
            depBooks = Math.Max(0m, NatSum(TrialBalanceConstants.S3DepreciationAndAmortization));
            otherExpenses = Math.Max(0m, NatSum(TrialBalanceConstants.S3OtherExpenses));
            taxCurr = Math.Max(0m, fig.TaxExpense);

            // Extract specific Form 3CD disallowance ledgers if mapped in TB
            disallow40a = Math.Max(0m, ledgers.Where(l => l.Form3CdCategory == TrialBalanceConstants.F3CdTdsDeductibleExpense).Sum(l => Math.Abs(l.ClosingBalance)));
            disallow40A3 = Math.Max(0m, ledgers.Where(l => l.Form3CdCategory == TrialBalanceConstants.F3CdSec40A3Disallowance).Sum(l => Math.Abs(l.ClosingBalance)));
            disallow43B = Math.Max(0m, ledgers.Where(l => l.Form3CdCategory == TrialBalanceConstants.F3CdSec43BStatutoryDue).Sum(l => Math.Abs(l.ClosingBalance)));
            disallow43Bh = Math.Max(0m, ledgers.Where(l => l.Form3CdCategory == TrialBalanceConstants.F3CdSec43BhMsme).Sum(l => Math.Abs(l.ClosingBalance)));
            disallow36 = Math.Max(0m, ledgers.Where(l => l.Form3CdCategory == TrialBalanceConstants.F3CdSec361vaEmployeeContribution).Sum(l => Math.Abs(l.ClosingBalance)));

            // Detect Partner Remuneration / Interest ledgers by name when Firm
            if (isFirm)
            {
                partnerRemun = ledgers.Where(l => l.LedgerName.Contains("Partner", StringComparison.OrdinalIgnoreCase) &&
                    (l.LedgerName.Contains("Remun", StringComparison.OrdinalIgnoreCase) || l.LedgerName.Contains("Salary", StringComparison.OrdinalIgnoreCase)))
                    .Sum(l => Math.Max(0m, l.ClosingBalance));
                if (partnerRemun > 0m && empBenefit >= partnerRemun) empBenefit -= partnerRemun;

                partnerInterest = ledgers.Where(l => l.LedgerName.Contains("Partner", StringComparison.OrdinalIgnoreCase) &&
                    l.LedgerName.Contains("Interest", StringComparison.OrdinalIgnoreCase))
                    .Sum(l => Math.Max(0m, l.ClosingBalance));
                if (partnerInterest > 0m && financeCost >= partnerInterest) financeCost -= partnerInterest;
            }

            sources.Add(new("TrialBalance", true, $"Imported {ledgers.Count} ledgers from '{tbImport.FileName}' (FY {tbImport.FinancialYear}); Revenue ₹{revenue:N0}, P&L Book Dep ₹{depBooks:N0}."));
        }
        else
        {
            sources.Add(new("TrialBalance", false, "No Trial Balance import found for client; using blank P&L & Balance Sheet."));
        }

        // 2. Fixed Assets & Income Tax Act Sec 32 Depreciation Blocks
        var assets = await db.FixedAssets
            .Include(a => a.DepreciationEntries)
            .AsNoTracking()
            .Where(a => a.ClientId == clientId && a.Status == AssetStatus.Active)
            .ToListAsync(ct);

        var depBlocks = new List<DepreciationBlockInput>();
        decimal itDepTotal = 0m;
        decimal grossBlock = 0m, accumDep = 0m;
        if (assets.Count > 0)
        {
            foreach (var asset in assets)
            {
                grossBlock += asset.GrossCost;
                var fyEntry = asset.DepreciationEntries.FirstOrDefault(d => d.FinancialYear == fy)
                    ?? asset.DepreciationEntries.OrderByDescending(d => d.CalculatedAt).FirstOrDefault();
                if (fyEntry is not null)
                    accumDep += Math.Max(0m, asset.GrossCost - fyEntry.ClosingBookValue);
            }

            if (ppeNet == 0m && grossBlock > 0m) ppeNet = Math.Max(0m, grossBlock - accumDep);

            // Group assets by IT block rate (15% PM, 40% Computers, 10% Furniture/Building, 25% Intangibles)
            foreach (var grp in assets.GroupBy(a => (a.AssetClass, a.IncomeTaxBlockRatePercent)))
            {
                decimal openWdv = 0m, addGt = 0m, addLt = 0m, blkTaxDep = 0m;
                foreach (var a in grp)
                {
                    var entry = a.DepreciationEntries.FirstOrDefault(d => d.FinancialYear == fy)
                        ?? a.DepreciationEntries.OrderByDescending(d => d.CalculatedAt).FirstOrDefault();
                    if (entry is not null)
                    {
                        openWdv += entry.OpeningTaxWdv;
                        blkTaxDep += entry.TaxDepreciation;
                    }
                    else
                    {
                        if (a.CapitalizationDate.Month >= 10 || a.CapitalizationDate.Month <= 3 && a.CapitalizationDate.Day >= 4)
                            addLt += a.GrossCost;
                        else
                            addGt += a.GrossCost;
                        blkTaxDep += Math.Round(a.GrossCost * (a.IncomeTaxBlockRatePercent / 100m), 0);
                    }
                }
                itDepTotal += blkTaxDep;
                string key = grp.Key.AssetClass switch
                {
                    AssetClass.Building => grp.Key.IncomeTaxBlockRatePercent >= 40m ? "Bldg40" : (grp.Key.IncomeTaxBlockRatePercent <= 5m ? "Bldg5" : "Bldg10"),
                    AssetClass.FurnitureAndFixtures => "Furn10",
                    AssetClass.Intangibles => "Intang25",
                    AssetClass.ComputersAndSoftware => "PM40",
                    _ => grp.Key.IncomeTaxBlockRatePercent >= 40m ? "PM40" : (grp.Key.IncomeTaxBlockRatePercent >= 30m ? "PM30" : "PM15")
                };
                depBlocks.Add(new DepreciationBlockInput(key, $"{grp.Key.AssetClass} @ {grp.Key.IncomeTaxBlockRatePercent:0}%", grp.Key.IncomeTaxBlockRatePercent, openWdv, addGt, addLt, 0m, 0m, blkTaxDep));
            }
            sources.Add(new("FixedAssets", true, $"Mapped {assets.Count} assets across {depBlocks.Count} IT Act blocks; Sec 32 Depreciation ₹{itDepTotal:N0}."));
        }
        else
        {
            sources.Add(new("FixedAssets", false, "No Fixed Asset register entries found; IT Act Sec 32 depreciation defaults to Book Depreciation unless edited."));
            itDepTotal = depBooks;
        }

        // 3. Form 3CD Assessment Disallowances & Audit Regime
        var form3cd = await db.Form3CdAssessments
            .AsNoTracking()
            .Where(f => f.ClientId == clientId)
            .OrderByDescending(f => f.FinancialYear == fy)
            .ThenByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);

        bool isAudit = false;
        string corpRegime = "auto";
        if (form3cd is not null)
        {
            isAudit = form3cd.IsTaxAuditMandatory;
            if (form3cd.TotalDisallowanceSec40a > 0m) disallow40a = form3cd.TotalDisallowanceSec40a;
            if (form3cd.TotalDisallowanceSec40A3 > 0m) disallow40A3 = form3cd.TotalDisallowanceSec40A3;
            if (form3cd.TotalDisallowanceSec43B > 0m) disallow43B = form3cd.TotalDisallowanceSec43B;
            if (form3cd.TotalDisallowanceSec43Bh > 0m) disallow43Bh = form3cd.TotalDisallowanceSec43Bh;
            if (form3cd.TotalDisallowanceSec361va > 0m) disallow36 = form3cd.TotalDisallowanceSec361va;
            if (form3cd.OptedSpecialTaxRegime is "115BAA" or "115BAB") corpRegime = form3cd.OptedSpecialTaxRegime;

            sources.Add(new("TaxAudit3CD", true, $"{form3cd.AuditType} (FY {form3cd.FinancialYear}): Disallowances 40(a) ₹{disallow40a:N0}, 43B/43B(h) ₹{(disallow43B + disallow43Bh):N0}, 40A(3) ₹{disallow40A3:N0}."));
        }
        else
        {
            sources.Add(new("TaxAudit3CD", false, "No Form 3CD assessment found; check statutory disallowances manually if applicable."));
        }

        // 4. Advance Tax Plan Challans & TDS Credits
        var advPlan = await db.AdvanceTaxPlans
            .Include(p => p.Installments)
            .Include(p => p.InterestComputations)
            .AsNoTracking()
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.FinancialYear == fy)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        decimal advPaid = 0m, tdsCred = 0m, satPaid = 0m;
        var challans = new List<ChallanTaxPaymentInput>();
        if (advPlan is not null)
        {
            foreach (var inst in advPlan.Installments.Where(i => i.IsPaid && i.AmountPaid > 0m))
            {
                advPaid += inst.AmountPaid;
                if (!string.IsNullOrWhiteSpace(inst.ChallanBsrCode) && !string.IsNullOrWhiteSpace(inst.ChallanNumber))
                {
                    challans.Add(new(
                        inst.ChallanBsrCode.Trim(),
                        (inst.ChallanDate ?? inst.DueDate).ToString("yyyy-MM-dd"),
                        inst.ChallanNumber.Trim(),
                        inst.AmountPaid));
                }
            }
            tdsCred = advPlan.EstimatedTdsTcs;
            satPaid = advPlan.InterestComputations.OrderByDescending(i => i.ComputedAt).FirstOrDefault()?.SelfAssessmentTaxPaid ?? 0m;
            sources.Add(new("AdvanceTax", true, $"Pulled paid Advance Tax ₹{advPaid:N0} ({challans.Count} challans) and TDS/TCS ₹{tdsCred:N0}."));
        }
        else
        {
            sources.Add(new("AdvanceTax", false, "No Advance Tax plan found; enter challans & TDS credits if paid."));
        }

        // 5. MCA Company Master & Directors -> KeyPersons (for Company / LLP)
        var mcaComp = await db.Companies
            .Include(c => c.Directors)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);

        var keyPersons = new List<KeyPersonInput>();
        string? incorpDate = null;
        if (mcaComp is not null)
        {
            authCap = mcaComp.AuthorizedCapital;
            if (shareOrPartnerCap == 0m) shareOrPartnerCap = mcaComp.PaidUpCapital;
            incorpDate = mcaComp.IncorporationDate.ToString("yyyy-MM-dd");
            subType = mcaComp.CompanyClass == Domain.Mca.CompanyClass.Public ? "Public" :
                      mcaComp.CompanyClass == Domain.Mca.CompanyClass.Llp ? "LLP" : subType;

            foreach (var dir in mcaComp.Directors.Where(d => !d.CessationDate.HasValue))
            {
                string desig = dir.Designation switch
                {
                    Domain.Mca.DirectorDesignation.ManagingDirector => "MD",
                    _ => "DIR"
                };
                keyPersons.Add(new(dir.DirectorName, desig, dir.Pan, dir.Din));
            }
            sources.Add(new("McaMaster", true, $"Linked MCA entity {mcaComp.Cin} ({mcaComp.CompanyClass}); {keyPersons.Count} active director(s) mapped to KeyPersons."));
        }

        var req = new CalculateBusinessTaxRequest(
            ClientId: clientId,
            AssessmentYear: ay,
            FinancialYear: fy,
            EntityType: entityType,
            SubType: subType,
            CorporateRegime: corpRegime,
            IsSmallTurnover25Percent: revenue <= 4_000_000_000m,
            IncorporationDate: incorpDate,
            IsTaxAuditApplicable: isAudit || revenue > 100_000_000m,
            PartnerOrShareCapital: shareOrPartnerCap,
            AuthorisedShareCapital: authCap,
            ReservesAndSurplus: reserves,
            LongTermBorrowings: ltBorrowings,
            ShortTermBorrowings: stBorrowings,
            DeferredTaxLiability: dtl,
            TradePayables: tradePayables,
            OtherCurrentLiabilities: otherCurrLiab,
            FixedAssetsGrossBlock: grossBlock > 0m ? grossBlock : ppeNet,
            FixedAssetsAccumulatedDep: accumDep,
            FixedAssetsNetBlock: ppeNet,
            CapitalWip: cwipp,
            NonCurrentInvestments: ncInv,
            CurrentInvestments: cInv,
            Inventories: inventories,
            TradeReceivables: receivables,
            CashAndBankBalances: cashBank,
            LoansAndAdvances: loansAdv,
            OtherCurrentAssets: otherCurrAssets,
            RevenueFromOperations: revenue,
            OtherIncome: otherIncome,
            InterestIncome: interestIncome,
            DividendIncome: dividendIncome,
            RentIncome: rentIncome,
            PurchasesAndDirectCost: purchases,
            ChangesInInventories: changesInInv,
            EmployeeBenefitExpense: empBenefit,
            PartnerRemunerationDebited: partnerRemun,
            FinanceCost: financeCost,
            PartnerInterestDebited: partnerInterest,
            DepreciationPerBooks: depBooks,
            OtherExpenses: otherExpenses,
            TaxProvisionCurrent: taxCurr,
            DisallowSec36: disallow36,
            DisallowSec40a: disallow40a,
            DisallowSec40A3: disallow40A3,
            DisallowSec43B: disallow43B,
            DisallowSec43BhMsme: disallow43Bh,
            IncomeTaxDepreciationSec32: itDepTotal,
            AdvanceTaxPaid: advPaid,
            TdsCredit: tdsCred,
            SelfAssessmentTaxPaid: satPaid,
            KeyPersons: keyPersons.Count > 0 ? keyPersons : null,
            DepreciationBlocks: depBlocks.Count > 0 ? depBlocks : null,
            AdvanceTaxChallans: challans.Count > 0 ? challans : null,
            Notes: $"Auto-prefilled for {client.LegalName ?? client.Name} (FY {fy} / AY {ay})"
        );

        return new BusinessPrefillResponseDto(req, sources);
    }

    public async Task<TaxComputationDto> SaveBusinessComputationAsync(
        CalculateBusinessTaxRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        var result = BusinessTaxEngine.Compute(req);
        bool isFirm = string.Equals(result.EntityType, "firm", StringComparison.OrdinalIgnoreCase);

        // Map to existing TaxComputation columns cleanly while persisting full BusinessTaxComputationResultDto in JSONB
        string optedColumn = result.SelectedRegimeCode == BusinessTaxEngine.RegimeCorp115Baa ||
                             result.SelectedRegimeCode == BusinessTaxEngine.RegimeCorp115Bab
            ? "new"
            : "old";

        var altA = isFirm
            ? BuildCompatibleSummary("old", result.ActiveSummary)
            : BuildCompatibleSummary("old", result.Alternatives.First(a => a.RegimeCode == BusinessTaxEngine.RegimeCorpNormal));
        var altB = isFirm
            ? BuildCompatibleSummary("new", result.ActiveSummary)
            : BuildCompatibleSummary("new", result.Alternatives.FirstOrDefault(a => a.RegimeCode == result.SelectedRegimeCode && a.RegimeCode != BusinessTaxEngine.RegimeCorpNormal)
                ?? result.Alternatives.First(a => a.RegimeCode == BusinessTaxEngine.RegimeCorp115Baa));

        var compatComparison = new RegimeComparisonDto(
            OldRegime: altA,
            NewRegime: altB,
            RecommendedRegime: result.RecommendedRegimeCode is BusinessTaxEngine.RegimeCorp115Baa or BusinessTaxEngine.RegimeCorp115Bab ? "new" : "old",
            TaxSavings: result.SavingsVsAlternative,
            RecommendationReason: isFirm
                ? $"ITR-5 Firm/LLP computation ({result.ActiveSummary.RegimeLabel}); Net PGBP ₹{result.NetPgbpIncome:N0}, Sec 40(b) allowable remuneration ₹{result.AllowableRemuneration40b:N0}."
                : $"ITR-6 Corporate computation — {result.ActiveSummary.RegimeLabel}; Savings ₹{result.SavingsVsAlternative:N0} vs alternative."
        );

        var comp = new TaxComputation
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            AssessmentYear = req.AssessmentYear,
            FinancialYear = req.FinancialYear,
            TaxpayerCategory = isFirm ? TaxpayerCategory.Firm : TaxpayerCategory.Company,
            OptedRegime = optedColumn,
            Status = ComputationStatus.Computed,

            GrossSalary = 0m,
            SalaryDeductions = 0m,
            NetSalary = 0m,
            HousePropertyIncome = result.HousePropertyIncome,
            BusinessIncome = result.NetPgbpIncome,
            CapitalGainsStcg = result.NormalizedInput.Stcg111A + result.NormalizedInput.StcgNormal,
            CapitalGainsLtcg = result.NormalizedInput.Ltcg112A + result.NormalizedInput.Ltcg112,
            OtherSourcesIncome = result.OtherSourcesIncome,

            GrossTotalIncome = result.GrossTotalIncome,
            ChapterViaDeductions = result.ChapterViaAllowed,
            TotalTaxableIncome = result.TotalTaxableIncome,

            TaxOnIncome = result.ActiveSummary.IsMatOrAmtApplied ? result.ActiveSummary.MatOrAmtBaseTax : result.ActiveSummary.BaseTax,
            Rebate87A = 0m,
            Surcharge = result.ActiveSummary.IsMatOrAmtApplied ? result.ActiveSummary.MatOrAmtSurcharge : result.ActiveSummary.Surcharge,
            Cess = result.ActiveSummary.IsMatOrAmtApplied ? result.ActiveSummary.MatOrAmtCess : result.ActiveSummary.HealthAndEducationCess,
            TotalTaxLiability = result.ActiveSummary.TotalTaxLiability,

            TdsTcsCredit = result.NormalizedInput.TdsCredit + result.NormalizedInput.TcsCredit,
            AdvanceTaxPaid = result.NormalizedInput.AdvanceTaxPaid,
            SelfAssessmentTax = result.NormalizedInput.SelfAssessmentTaxPaid,
            NetTaxPayableOrRefund = result.ActiveSummary.NetTaxPayableOrRefund,

            ComputationBreakdown = JsonSerializer.Serialize(result, JsonOpts),
            ComparisonData = JsonSerializer.Serialize(compatComparison, JsonOpts),

            Notes = req.Notes?.Trim() ?? compatComparison.RecommendationReason,
            CreatedBy = currentUserId,
        };

        db.TaxComputations.Add(comp);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(comp.Id, ct);
    }

    public async Task<IncomeTaxJsonExportResult> ExportItr5JsonAsync(Guid id, ExportItrJsonRequest? req = null, CancellationToken ct = default)
    {
        var (comp, client, biz) = await LoadBusinessComputationForExportAsync(id, expectedEntityType: "firm", ct);
        return Itr5JsonBuilder.Build(comp, client, biz, req);
    }

    public async Task<IncomeTaxJsonExportResult> ExportItr6JsonAsync(Guid id, ExportItrJsonRequest? req = null, CancellationToken ct = default)
    {
        var (comp, client, biz) = await LoadBusinessComputationForExportAsync(id, expectedEntityType: "company", ct);
        return Itr6JsonBuilder.Build(comp, client, biz, req);
    }

    private async Task<(TaxComputation Comp, Domain.Clients.Client Client, BusinessTaxComputationResultDto Biz)> LoadBusinessComputationForExportAsync(
        Guid id,
        string expectedEntityType,
        CancellationToken ct)
    {
        var comp = await db.TaxComputations
            .Include(x => x.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessRuleException("Tax computation not found.", 404);

        var client = comp.Client ?? await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == comp.ClientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        BusinessTaxComputationResultDto? biz = null;
        if (!string.IsNullOrWhiteSpace(comp.ComputationBreakdown)
            && comp.ComputationBreakdown.Contains("\"selectedRegimeCode\"", StringComparison.OrdinalIgnoreCase))
        {
            biz = JsonSerializer.Deserialize<BusinessTaxComputationResultDto>(comp.ComputationBreakdown, JsonOpts);
        }

        // Also allow exporting a legacy flat firm/company TaxComputation seamlessly by synthesizing a minimal BusinessTaxComputationResultDto
        if (biz is null)
        {
            var synthReq = new CalculateBusinessTaxRequest(
                ClientId: comp.ClientId,
                AssessmentYear: comp.AssessmentYear,
                FinancialYear: comp.FinancialYear,
                EntityType: expectedEntityType,
                SubType: expectedEntityType == "firm" ? "Partnership" : "Private",
                RevenueFromOperations: Math.Max(0m, comp.BusinessIncome),
                HousePropertyIncome: comp.HousePropertyIncome,
                StcgNormal: comp.CapitalGainsStcg,
                Ltcg112: comp.CapitalGainsLtcg,
                OtherSourcesIncome: comp.OtherSourcesIncome,
                OtherChapViaDeductions: comp.ChapterViaDeductions,
                TdsCredit: comp.TdsTcsCredit,
                AdvanceTaxPaid: comp.AdvanceTaxPaid,
                SelfAssessmentTaxPaid: comp.SelfAssessmentTax,
                Notes: comp.Notes
            );
            biz = BusinessTaxEngine.Compute(synthReq);
        }

        return (comp, client, biz);
    }

    private static string DeriveAyFromFy(string fy)
    {
        if (fy.Length >= 7 && int.TryParse(fy[..4], out int y))
            return $"{y + 1:D4}-{(y + 2) % 100:D2}";
        return "2026-27";
    }

    private static RegimeTaxResult BuildCompatibleSummary(string regimeKey, BusinessRegimeSummaryDto s) =>
        new(
            Regime: regimeKey,
            GrossSalary: 0m,
            StandardDeduction: 0m,
            NetSalary: 0m,
            HousePropertyIncome: 0m,
            BusinessIncome: s.TaxableIncome,
            CapitalGainsStcg: 0m,
            CapitalGainsLtcg: 0m,
            OtherSourcesIncome: 0m,
            GrossTotalIncome: s.TaxableIncome,
            ChapterViaDeductions: 0m,
            TaxableIncome: s.TaxableIncome,
            BaseTax: s.BaseTax,
            SpecialRateTax: s.TaxAtSpecialRates,
            Rebate87A: 0m,
            Surcharge: s.Surcharge,
            Cess: s.HealthAndEducationCess,
            TotalTaxLiability: s.TotalTaxLiability,
            PrepaidTaxes: s.PrepaidTaxes,
            NetPayableOrRefund: s.NetTaxPayableOrRefund,
            SlabBreakdown: []
        );
}
