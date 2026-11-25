using CaDesk.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Tax;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Presumptive;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Presumptive;

public class PresumptiveTaxService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<List<PresumptiveTaxComputationDto>> ListComputationsAsync(
        Guid? clientId = null,
        string? fy = null,
        string? section = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = db.PresumptiveTaxComputations
            .Include(x => x.Client)
            .Include(x => x.CreatedByUser)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.Vehicles)
            .AsNoTracking()
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(fy) && fy != "all") q = q.Where(x => x.FinancialYear == fy);
        if (!string.IsNullOrWhiteSpace(section) && section != "all") q = q.Where(x => x.Section == section);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(x => x.Status == status);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToComputationDto).ToList();
    }

    public async Task<PresumptiveTaxComputationDto> GetComputationByIdAsync(Guid id, CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Client)
            .Include(x => x.CreatedByUser)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.Vehicles)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);
        return ToComputationDto(comp);
    }

    public async Task<PresumptiveTaxComputationDto> CreateComputationAsync(
        SavePresumptiveComputationRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        var ay = PresumptiveTaxEngine.ComputeAssessmentYear(req.FinancialYear);
        var comp = new PresumptiveTaxComputation
        {
            Id = Guid.NewGuid(),
            ClientId = req.ClientId,
            FinancialYear = req.FinancialYear,
            AssessmentYear = ay,
            Section = req.Section,
            EntityType = req.EntityType ?? "Individual",
            BusinessNatureCode = req.BusinessNatureCode ?? "",
            TradeName = req.TradeName ?? client.Name,
            GrossTurnover = req.GrossTurnover,
            DigitalTurnover = req.DigitalTurnover,
            CashTurnover = req.CashTurnover,
            DeclaredProfit = req.DeclaredProfit,
            OptedOutPriorYear = req.OptedOutPriorYear,
            OptOutAssessmentYear = req.OptOutAssessmentYear,
            SundryDebtors = req.SundryDebtors,
            SundryCreditors = req.SundryCreditors,
            StockInTrade = req.StockInTrade,
            CashBalance = req.CashBalance,
            BankBalance = req.BankBalance,
            FixedAssets = req.FixedAssets,
            OtherAssets = req.OtherAssets,
            PartnerOrProprietorCapital = req.PartnerOrProprietorCapital,
            LoansAndBorrowings = req.LoansAndBorrowings,
            Notes = req.Notes,
            Status = PresumptiveStatus.Draft,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        ApplyCalculations(comp);

        db.PresumptiveTaxComputations.Add(comp);
        await db.SaveChangesAsync(ct);

        return await GetComputationByIdAsync(comp.Id, ct);
    }

    public async Task<PresumptiveTaxComputationDto> UpdateComputationAsync(
        Guid id,
        SavePresumptiveComputationRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Vehicles)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);
        if (comp.Status == PresumptiveStatus.Approved)
            throw new BusinessRuleException("Approved computation cannot be edited. Please reopen first.", 400);

        comp.FinancialYear = req.FinancialYear;
        comp.AssessmentYear = PresumptiveTaxEngine.ComputeAssessmentYear(req.FinancialYear);
        comp.Section = req.Section;
        comp.EntityType = req.EntityType ?? comp.EntityType;
        comp.BusinessNatureCode = req.BusinessNatureCode ?? comp.BusinessNatureCode;
        comp.TradeName = req.TradeName ?? comp.TradeName;
        comp.GrossTurnover = req.GrossTurnover;
        comp.DigitalTurnover = req.DigitalTurnover;
        comp.CashTurnover = req.CashTurnover;
        comp.DeclaredProfit = req.DeclaredProfit;
        comp.OptedOutPriorYear = req.OptedOutPriorYear;
        comp.OptOutAssessmentYear = req.OptOutAssessmentYear;
        comp.SundryDebtors = req.SundryDebtors;
        comp.SundryCreditors = req.SundryCreditors;
        comp.StockInTrade = req.StockInTrade;
        comp.CashBalance = req.CashBalance;
        comp.BankBalance = req.BankBalance;
        comp.FixedAssets = req.FixedAssets;
        comp.OtherAssets = req.OtherAssets;
        comp.PartnerOrProprietorCapital = req.PartnerOrProprietorCapital;
        comp.LoansAndBorrowings = req.LoansAndBorrowings;
        comp.Notes = req.Notes;
        comp.UpdatedAt = DateTime.UtcNow;

        ApplyCalculations(comp);

        await db.SaveChangesAsync(ct);
        return await GetComputationByIdAsync(comp.Id, ct);
    }

    public async Task<PresumptiveTaxComputationDto> RecalculateAsync(Guid id, CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Vehicles)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);

        ApplyCalculations(comp);
        comp.Status = PresumptiveStatus.Computed;
        comp.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetComputationByIdAsync(comp.Id, ct);
    }

    public async Task<PresumptiveTaxComputationDto> AddVehicleAsync(
        Guid computationId,
        SaveVehicleRequest req,
        CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Vehicles)
            .FirstOrDefaultAsync(x => x.Id == computationId, ct);

        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);
        if (comp.Section != PresumptiveSection.Sec44AE)
            throw new BusinessRuleException("Vehicles can only be attached to Section 44AE computations.", 400);

        var (monthlyRate, deemedMonthly, deemedAnnual) = PresumptiveTaxEngine.CalculateSection44AeVehicle(
            req.VehicleType,
            req.GrossVehicleWeightTons,
            req.MonthsOwned
        );

        var vehicle = new Presumptive44AeVehicle
        {
            Id = Guid.NewGuid(),
            ComputationId = computationId,
            RegistrationNumber = req.RegistrationNumber.ToUpperInvariant().Trim(),
            VehicleType = req.VehicleType,
            GrossVehicleWeightTons = req.GrossVehicleWeightTons,
            MonthsOwned = Math.Clamp(req.MonthsOwned, 1, 12),
            RatePerMonth = monthlyRate,
            DeemedMonthlyProfit = deemedMonthly,
            DeemedAnnualProfit = deemedAnnual,
            DeclaredProfit = req.DeclaredProfit > 0 ? req.DeclaredProfit : deemedAnnual,
            CreatedAt = DateTime.UtcNow
        };

        db.Presumptive44AeVehicles.Add(vehicle);
        if (!comp.Vehicles.Contains(vehicle)) comp.Vehicles.Add(vehicle); // the db Add above already fixes up the navigation

        ApplyCalculations(comp);

        await db.SaveChangesAsync(ct);
        return await GetComputationByIdAsync(comp.Id, ct);
    }

    public async Task<PresumptiveTaxComputationDto> RemoveVehicleAsync(
        Guid computationId,
        Guid vehicleId,
        CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Vehicles)
            .FirstOrDefaultAsync(x => x.Id == computationId, ct);

        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);

        var vehicle = comp.Vehicles.FirstOrDefault(v => v.Id == vehicleId);
        if (vehicle is not null)
        {
            db.Presumptive44AeVehicles.Remove(vehicle);
            comp.Vehicles.Remove(vehicle);
            ApplyCalculations(comp);
            await db.SaveChangesAsync(ct);
        }

        return await GetComputationByIdAsync(comp.Id, ct);
    }

    public async Task<PresumptiveTaxComputationDto> ApproveAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Vehicles)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);

        ApplyCalculations(comp);
        comp.Status = PresumptiveStatus.Approved;
        comp.ApprovedByUserId = currentUserId;
        comp.ApprovedAt = DateTime.UtcNow;
        comp.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetComputationByIdAsync(comp.Id, ct);
    }

    public async Task<MinimalFinancialsStatementDto> GetMinimalFinancialsStatementAsync(Guid id, CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);

        var (totalAssets, totalLiab, diff, isBalanced, alerts) = PresumptiveTaxEngine.ValidateMinimalFinancials(
            comp.SundryDebtors,
            comp.SundryCreditors,
            comp.StockInTrade,
            comp.CashBalance,
            comp.BankBalance,
            comp.FixedAssets,
            comp.OtherAssets,
            comp.PartnerOrProprietorCapital,
            comp.LoansAndBorrowings
        );

        string? pan = null;
        if (comp.Client is not null && comp.Client.TaxIds.TryGetValue("PAN", out var p))
        {
            pan = p;
        }

        return new MinimalFinancialsStatementDto(
            comp.Id,
            comp.Client?.Name ?? "Client",
            pan,
            comp.FinancialYear,
            comp.AssessmentYear,
            comp.Section,
            comp.TradeName,
            comp.GrossTurnover,
            comp.DeclaredProfit,
            totalAssets,
            totalLiab,
            diff,
            isBalanced,
            alerts,
            comp.SundryDebtors,
            comp.SundryCreditors,
            comp.StockInTrade,
            comp.CashBalance,
            comp.BankBalance,
            comp.FixedAssets,
            comp.OtherAssets,
            comp.PartnerOrProprietorCapital,
            comp.LoansAndBorrowings
        );
    }

    public async Task<PresumptiveSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var comps = await db.PresumptiveTaxComputations.AsNoTracking().ToListAsync(ct);

        var total = comps.Count;
        var ad = comps.Count(x => x.Section == PresumptiveSection.Sec44AD);
        var ada = comps.Count(x => x.Section == PresumptiveSection.Sec44ADA);
        var ae = comps.Count(x => x.Section == PresumptiveSection.Sec44AE);
        var audit = comps.Count(x => x.IsAuditMandatory);
        var lockout = comps.Count(x => x.IsLockoutActive);
        var turnover = comps.Sum(x => x.GrossTurnover);
        var profit = comps.Sum(x => x.DeclaredProfit);

        return new PresumptiveSummaryDto(total, ad, ada, ae, audit, lockout, turnover, profit);
    }

    private static void ApplyCalculations(PresumptiveTaxComputation comp)
    {
        if (comp.Section == PresumptiveSection.Sec44AD)
        {
            var r = PresumptiveTaxEngine.CalculateSection44AD(
                comp.DigitalTurnover,
                comp.CashTurnover,
                comp.DeclaredProfit,
                comp.OptedOutPriorYear,
                comp.OptOutAssessmentYear,
                comp.AssessmentYear,
                comp.FinancialYear
            );

            comp.GrossTurnover = r.GrossTurnover;
            comp.DigitalTurnover = r.DigitalTurnover;
            comp.CashTurnover = r.CashTurnover;
            comp.DigitalPercentage = r.DigitalPercentage;
            comp.IsDigitalThresholdEligible = r.IsDigitalThresholdEligible;
            comp.TurnoverLimit = r.TurnoverLimit;
            comp.IsTurnoverWithinLimit = r.IsTurnoverWithinLimit;
            comp.MinimumDeemedProfit = r.MinimumDeemedProfit;
            comp.DeemedProfitDigitalRate = 6.00m;
            comp.DeemedProfitCashRate = 8.00m;
            comp.IsAuditMandatory = r.IsAuditMandatory;
            comp.AuditTriggerReason = r.AuditReason;
            comp.IsLockoutActive = r.IsLockoutActive;
            comp.LockoutExpiryAy = r.LockoutExpiryAy;
        }
        else if (comp.Section == PresumptiveSection.Sec44ADA)
        {
            var r = PresumptiveTaxEngine.CalculateSection44ADA(
                comp.DigitalTurnover,
                comp.CashTurnover,
                comp.DeclaredProfit,
                comp.FinancialYear
            );

            comp.GrossTurnover = r.GrossReceipts;
            comp.DigitalTurnover = r.DigitalReceipts;
            comp.CashTurnover = r.CashReceipts;
            comp.DigitalPercentage = r.DigitalPercentage;
            comp.IsDigitalThresholdEligible = r.IsDigitalThresholdEligible;
            comp.TurnoverLimit = r.ReceiptsLimit;
            comp.IsTurnoverWithinLimit = r.IsWithinLimit;
            comp.MinimumDeemedProfit = r.MinimumDeemedProfit;
            comp.DeemedProfitDigitalRate = 50.00m;
            comp.DeemedProfitCashRate = 50.00m;
            comp.IsAuditMandatory = r.IsAuditMandatory;
            comp.AuditTriggerReason = r.AuditReason;
            comp.IsLockoutActive = false;
            comp.LockoutExpiryAy = null;
        }
        else if (comp.Section == PresumptiveSection.Sec44AE)
        {
            var vehicleCount = comp.Vehicles.Count;
            var minDeemed = comp.Vehicles.Sum(v => v.DeemedAnnualProfit);
            var vehicleDeclared = comp.Vehicles.Sum(v => v.DeclaredProfit);

            comp.MinimumDeemedProfit = minDeemed;
            // each vehicle carries its own declared profit (defaulting to its deemed profit), so the total is
            // always re-derived; filling it only once left it stale when vehicles were added or removed
            if (vehicleCount > 0)
                comp.DeclaredProfit = vehicleDeclared > 0 ? vehicleDeclared : minDeemed;

            if (vehicleCount > PresumptiveTaxEngine.Sec44AEMaxVehiclesLimit)
            {
                comp.IsTurnoverWithinLimit = false;
                comp.IsAuditMandatory = true;
                comp.AuditTriggerReason = $"Assessee owns {vehicleCount} goods carriages, which exceeds the statutory maximum limit of {PresumptiveTaxEngine.Sec44AEMaxVehiclesLimit} vehicles under {IncomeTaxLaw.Sec("44AE", comp.FinancialYear)}. Tax Audit mandatory under {IncomeTaxLaw.Sec("44AB", comp.FinancialYear)}.";
            }
            else if (comp.DeclaredProfit < minDeemed)
            {
                comp.IsTurnoverWithinLimit = true;
                comp.IsAuditMandatory = true;
                comp.AuditTriggerReason = $"Declared profit ({comp.DeclaredProfit:N0}) is lower than the statutory minimum deemed profit ({minDeemed:N0}) calculated under {IncomeTaxLaw.Sec("44AE", comp.FinancialYear)}. Tax Audit mandatory under {IncomeTaxLaw.Sec("44AB(c)", comp.FinancialYear)}.";
            }
            else
            {
                comp.IsTurnoverWithinLimit = true;
                comp.IsAuditMandatory = false;
                comp.AuditTriggerReason = null;
            }

            comp.TurnoverLimit = PresumptiveTaxEngine.Sec44AEMaxVehiclesLimit; // represents max 10 vehicles
            comp.IsLockoutActive = false;
            comp.LockoutExpiryAy = null;
        }
    }

    public async Task<IncomeTaxJsonExportResult> ExportItr4JsonAsync(Guid id, ExportItrJsonRequest? req = null, CancellationToken ct = default)
    {
        var comp = await db.PresumptiveTaxComputations
            .Include(x => x.Client)
            .Include(x => x.Vehicles)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (comp is null) throw new BusinessRuleException("Presumptive tax computation not found.", 404);

        var client = comp.Client ?? await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == comp.ClientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        return Itr4JsonBuilder.Build(comp, client, req);
    }

    private static PresumptiveTaxComputationDto ToComputationDto(PresumptiveTaxComputation c)
    {
        string? pan = null;
        if (c.Client is not null && c.Client.TaxIds.TryGetValue("PAN", out var p))
        {
            pan = p;
        }

        var vehicleDtos = c.Vehicles?.Select(v => new Presumptive44AeVehicleDto(
            v.Id,
            v.ComputationId,
            v.RegistrationNumber,
            v.VehicleType,
            v.GrossVehicleWeightTons,
            v.MonthsOwned,
            v.RatePerMonth,
            v.DeemedMonthlyProfit,
            v.DeemedAnnualProfit,
            v.DeclaredProfit,
            v.CreatedAt
        )).ToList() ?? [];

        return new PresumptiveTaxComputationDto(
            c.Id,
            c.ClientId,
            c.Client?.Name,
            pan,
            c.FinancialYear,
            c.AssessmentYear,
            c.Section,
            c.EntityType,
            c.BusinessNatureCode,
            c.TradeName,
            c.GrossTurnover,
            c.DigitalTurnover,
            c.CashTurnover,
            c.DigitalPercentage,
            c.IsDigitalThresholdEligible,
            c.TurnoverLimit,
            c.IsTurnoverWithinLimit,
            c.DeemedProfitDigitalRate,
            c.DeemedProfitCashRate,
            c.MinimumDeemedProfit,
            c.DeclaredProfit,
            c.OptedOutPriorYear,
            c.OptOutAssessmentYear,
            c.IsLockoutActive,
            c.LockoutExpiryAy,
            c.IsAuditMandatory,
            c.AuditTriggerReason,
            c.SundryDebtors,
            c.SundryCreditors,
            c.StockInTrade,
            c.CashBalance,
            c.BankBalance,
            c.FixedAssets,
            c.OtherAssets,
            c.PartnerOrProprietorCapital,
            c.LoansAndBorrowings,
            c.Status,
            c.Notes,
            c.CreatedByUserId,
            c.CreatedByUser?.Name,
            c.ApprovedByUserId,
            c.ApprovedByUser?.Name,
            c.ApprovedAt,
            c.CreatedAt,
            c.UpdatedAt,
            vehicleDtos
        );
    }
}
