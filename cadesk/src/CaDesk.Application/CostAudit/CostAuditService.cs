using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.CostAudit;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.CostAudit;

public class CostAuditService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CostAuditService(IAppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<CostAuditSummaryDto> GetSummaryAsync(
        Guid? clientId,
        string? financialYear,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var q = _db.CostAuditEngagements.Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);

        var list = await q.ToListAsync(ct);

        int total = list.Count;
        int active = list.Count(x => x.AuditStatus != CostAuditConstants.StatusCertified && x.AuditStatus != CostAuditConstants.StatusFiledCra4);
        int certified = list.Count(x => x.AuditStatus == CostAuditConstants.StatusCertified || x.AuditStatus == CostAuditConstants.StatusFiledCra4);
        int regulated = list.Count(x => x.SectorType == CostAuditConstants.SectorRegulated);
        int nonRegulated = list.Count(x => x.SectorType == CostAuditConstants.SectorNonRegulated);
        int overdue = list.Count(x => x.Cra2FilingStatus == CostAuditConstants.FilingStatusOverdue || x.Cra4FilingStatus == CostAuditConstants.FilingStatusOverdue);

        decimal totalProduction = list.Sum(x => x.TotalProductionValueInr);
        decimal totalCostProfit = list.Sum(x => x.TotalCostProfitInr);
        decimal totalUnreconciledDiff = list.Sum(x => Math.Abs(x.NetReconciliationDifferenceInr));
        decimal avgCompliance = total > 0 ? Math.Round(list.Average(x => x.RecordsComplianceScorePct), 2) : 100m;

        return new CostAuditSummaryDto(
            total,
            active,
            certified,
            regulated,
            nonRegulated,
            overdue,
            totalProduction,
            totalCostProfit,
            totalUnreconciledDiff,
            avgCompliance);
    }

    public ApplicabilityCheckResult CheckApplicability(ApplicabilityCheckRequest req)
    {
        var (recordsApp, auditApp, exportExempt, reasoning) = CostAuditEngine.EvaluateApplicability(
            req.SectorType,
            req.OverallTurnoverInr,
            req.ProductTurnoverInr,
            req.ExportTurnoverInr,
            req.IsSezUnit,
            req.IsCaptivePower);

        return new ApplicabilityCheckResult(recordsApp, auditApp, exportExempt, reasoning);
    }

    public MaterialVarianceResult CalculateMaterialVariance(MaterialVarianceRequest req)
    {
        var (priceVar, usageVar, totalVar) = CostAuditEngine.CalculateMaterialVariance(
            req.StandardQuantity,
            req.ActualQuantity,
            req.StandardPrice,
            req.ActualPrice);

        return new MaterialVarianceResult(priceVar, usageVar, totalVar);
    }

    public async Task<List<CostAuditEngagementDto>> GetEngagementsAsync(
        Guid? clientId,
        string? financialYear,
        string? auditStatus,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var q = _db.CostAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(auditStatus)) q = q.Where(x => x.AuditStatus == auditStatus);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<CostAuditEngagementDto?> GetEngagementByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        return engagement == null ? null : MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto> CreateEngagementAsync(
        CreateCostAuditEngagementRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var (recordsApp, auditApp, exportExempt, reasoning) = CostAuditEngine.EvaluateApplicability(
            req.SectorType,
            req.OverallTurnoverInr,
            req.ProductTurnoverInr,
            req.ExportTurnoverInr,
            req.IsSezExempt,
            req.IsCaptivePowerExempt);

        var (cra2Due, cra2Status, _) = CostAuditEngine.CalculateCra2DueDates(
            req.BoardResolutionDate,
            req.FyCommencementDate,
            null);

        var engagement = new CostAuditEngagement
        {
            Id = Guid.NewGuid(),
            FirmId = firmId,
            ClientId = req.ClientId,
            EngagementReference = req.EngagementReference,
            FinancialYear = req.FinancialYear,
            PeriodStart = req.PeriodStart,
            PeriodEnd = req.PeriodEnd,
            SectorType = req.SectorType,
            SectorCategory = req.SectorCategory,
            OverallTurnoverInr = req.OverallTurnoverInr,
            ProductTurnoverInr = req.ProductTurnoverInr,
            ExportTurnoverInr = req.ExportTurnoverInr,
            IsExportExempt = exportExempt,
            IsSezExempt = req.IsSezExempt,
            IsCaptivePowerExempt = req.IsCaptivePowerExempt,
            IsCostRecordsApplicable = recordsApp,
            IsCostAuditApplicable = auditApp,
            ApplicabilityReasoning = reasoning,
            BoardResolutionDate = req.BoardResolutionDate,
            FyCommencementDate = req.FyCommencementDate,
            AuditCommitteeRecommended = req.AuditCommitteeRecommended,
            CostAuditorName = req.CostAuditorName,
            CostAuditorFirmName = req.CostAuditorFirmName,
            CostAuditorMembershipNo = req.CostAuditorMembershipNo,
            CostAuditorFirmRegNo = req.CostAuditorFirmRegNo,
            Cra2FilingDueDate = cra2Due,
            Cra2FilingStatus = cra2Status,
            AuditedFinancialProfitInr = req.AuditedFinancialProfitInr,
            AuditStatus = CostAuditConstants.StatusDraft,
            AuditorOpinion = CostAuditConstants.OpinionUnmodified,
            CreatedAt = DateTime.UtcNow
        };

        if (req.SeedDefaultHeads)
        {
            engagement.RecordHeads = CostAuditEngine.GetStandardCostRecordHeads(firmId, engagement.Id);
        }

        CostAuditEngine.RecalculateEngagementSummary(engagement);

        _db.CostAuditEngagements.Add(engagement);
        await _db.SaveChangesAsync(ct);

        return (await GetEngagementByIdAsync(engagement.Id, ct))!;
    }

    public async Task<CostAuditEngagementDto?> UpdateEngagementAsync(
        Guid id,
        UpdateCostAuditEngagementRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var (recordsApp, auditApp, exportExempt, reasoning) = CostAuditEngine.EvaluateApplicability(
            req.SectorType,
            req.OverallTurnoverInr,
            req.ProductTurnoverInr,
            req.ExportTurnoverInr,
            req.IsSezExempt,
            req.IsCaptivePowerExempt);

        engagement.EngagementReference = req.EngagementReference;
        engagement.FinancialYear = req.FinancialYear;
        engagement.PeriodStart = req.PeriodStart;
        engagement.PeriodEnd = req.PeriodEnd;
        engagement.SectorType = req.SectorType;
        engagement.SectorCategory = req.SectorCategory;
        engagement.OverallTurnoverInr = req.OverallTurnoverInr;
        engagement.ProductTurnoverInr = req.ProductTurnoverInr;
        engagement.ExportTurnoverInr = req.ExportTurnoverInr;
        engagement.IsExportExempt = exportExempt;
        engagement.IsSezExempt = req.IsSezExempt;
        engagement.IsCaptivePowerExempt = req.IsCaptivePowerExempt;
        engagement.IsCostRecordsApplicable = recordsApp;
        engagement.IsCostAuditApplicable = auditApp;
        engagement.ApplicabilityReasoning = reasoning;
        engagement.BoardResolutionDate = req.BoardResolutionDate;
        engagement.FyCommencementDate = req.FyCommencementDate;
        engagement.AuditCommitteeRecommended = req.AuditCommitteeRecommended;
        engagement.CostAuditorName = req.CostAuditorName;
        engagement.CostAuditorFirmName = req.CostAuditorFirmName;
        engagement.CostAuditorMembershipNo = req.CostAuditorMembershipNo;
        engagement.CostAuditorFirmRegNo = req.CostAuditorFirmRegNo;
        engagement.AuditedFinancialProfitInr = req.AuditedFinancialProfitInr;
        engagement.AuditorOpinion = req.AuditorOpinion;
        engagement.ReservationsQualifications = req.ReservationsQualifications;
        engagement.ObservationsSuggestions = req.ObservationsSuggestions;
        engagement.AuditStatus = req.AuditStatus;
        engagement.UpdatedAt = DateTime.UtcNow;

        CostAuditEngine.RecalculateEngagementSummary(engagement);

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<bool> DeleteEngagementAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return false;

        _db.CostAuditEngagements.Remove(engagement);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CostAuditEngagementDto?> SeedStandardHeadsAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var existingCodes = engagement.RecordHeads.Select(h => h.HeadCode).ToHashSet();
        var defaultList = CostAuditEngine.GetStandardCostRecordHeads(firmId, engagement.Id);

        foreach (var h in defaultList)
        {
            if (!existingCodes.Contains(h.HeadCode))
            {
                engagement.RecordHeads.Add(h);
            }
        }

        CostAuditEngine.RecalculateEngagementSummary(engagement);
        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> UpdateRecordHeadAsync(
        Guid id,
        Guid headId,
        UpdateRecordHeadRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var head = engagement.RecordHeads.FirstOrDefault(h => h.Id == headId);
        if (head == null) return null;

        head.Status = req.Status;
        head.RecordMaintenanceSystem = req.RecordMaintenanceSystem;
        head.VerificationNotes = req.VerificationNotes;
        head.DeficiencyOrAnomaly = req.DeficiencyOrAnomaly;
        head.CorrectiveActionRequired = req.CorrectiveActionRequired;
        head.UpdatedAt = DateTime.UtcNow;

        CostAuditEngine.RecalculateEngagementSummary(engagement);
        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> AddProductCostStatementAsync(
        Guid id,
        CreateProductCostStatementRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var statement = new ProductCostStatement
        {
            Id = Guid.NewGuid(),
            FirmId = firmId,
            EngagementId = engagement.Id,
            ProductCode = req.ProductCode,
            ProductName = req.ProductName,
            CtaHeading = req.CtaHeading,
            UnitOfMeasurement = req.UnitOfMeasurement,
            InstalledCapacity = req.InstalledCapacity,
            AvailableCapacity = req.AvailableCapacity,
            ActualProduction = req.ActualProduction,
            DirectMaterialsCost = req.DirectMaterialsCost,
            DirectLaborCost = req.DirectLaborCost,
            UtilitiesCost = req.UtilitiesCost,
            OtherDirectExpenses = req.OtherDirectExpenses,
            WorksOverheads = req.WorksOverheads,
            QualityControlCost = req.QualityControlCost,
            RndExpenses = req.RndExpenses,
            AdministrationOverheads = req.AdministrationOverheads,
            OpeningWipCost = req.OpeningWipCost,
            ClosingWipCost = req.ClosingWipCost,
            OpeningFinishedGoodsCost = req.OpeningFinishedGoodsCost,
            ClosingFinishedGoodsCost = req.ClosingFinishedGoodsCost,
            SellingAndDistributionOverheads = req.SellingAndDistributionOverheads,
            NetSalesRealization = req.NetSalesRealization,
            CreatedAt = DateTime.UtcNow
        };

        CostAuditEngine.CalculateProductCostStatement(statement);
        engagement.ProductStatements.Add(statement);

        CostAuditEngine.RecalculateEngagementSummary(engagement);
        await _db.SaveChangesAsync(ct);

        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> UpdateProductCostStatementAsync(
        Guid id,
        Guid productId,
        UpdateProductCostStatementRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var statement = engagement.ProductStatements.FirstOrDefault(p => p.Id == productId);
        if (statement == null) return null;

        statement.ProductCode = req.ProductCode;
        statement.ProductName = req.ProductName;
        statement.CtaHeading = req.CtaHeading;
        statement.UnitOfMeasurement = req.UnitOfMeasurement;
        statement.InstalledCapacity = req.InstalledCapacity;
        statement.AvailableCapacity = req.AvailableCapacity;
        statement.ActualProduction = req.ActualProduction;
        statement.DirectMaterialsCost = req.DirectMaterialsCost;
        statement.DirectLaborCost = req.DirectLaborCost;
        statement.UtilitiesCost = req.UtilitiesCost;
        statement.OtherDirectExpenses = req.OtherDirectExpenses;
        statement.WorksOverheads = req.WorksOverheads;
        statement.QualityControlCost = req.QualityControlCost;
        statement.RndExpenses = req.RndExpenses;
        statement.AdministrationOverheads = req.AdministrationOverheads;
        statement.OpeningWipCost = req.OpeningWipCost;
        statement.ClosingWipCost = req.ClosingWipCost;
        statement.OpeningFinishedGoodsCost = req.OpeningFinishedGoodsCost;
        statement.ClosingFinishedGoodsCost = req.ClosingFinishedGoodsCost;
        statement.SellingAndDistributionOverheads = req.SellingAndDistributionOverheads;
        statement.NetSalesRealization = req.NetSalesRealization;
        statement.UpdatedAt = DateTime.UtcNow;

        CostAuditEngine.CalculateProductCostStatement(statement);
        CostAuditEngine.RecalculateEngagementSummary(engagement);

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> DeleteProductCostStatementAsync(
        Guid id,
        Guid productId,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var statement = engagement.ProductStatements.FirstOrDefault(p => p.Id == productId);
        if (statement == null) return null;

        engagement.ProductStatements.Remove(statement);
        CostAuditEngine.RecalculateEngagementSummary(engagement);

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> AddReconciliationItemAsync(
        Guid id,
        CreateReconciliationItemRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var item = new CostAuditReconciliationItem
        {
            Id = Guid.NewGuid(),
            FirmId = firmId,
            EngagementId = engagement.Id,
            ItemType = req.ItemType,
            Description = req.Description,
            AmountInr = req.AmountInr,
            Impact = req.Impact,
            FinancialScheduleRef = req.FinancialScheduleRef,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow
        };

        engagement.ReconciliationItems.Add(item);
        CostAuditEngine.RecalculateEngagementSummary(engagement);

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> UpdateReconciliationItemAsync(
        Guid id,
        Guid itemId,
        UpdateReconciliationItemRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var item = engagement.ReconciliationItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return null;

        item.ItemType = req.ItemType;
        item.Description = req.Description;
        item.AmountInr = req.AmountInr;
        item.Impact = req.Impact;
        item.FinancialScheduleRef = req.FinancialScheduleRef;
        item.Notes = req.Notes;
        item.UpdatedAt = DateTime.UtcNow;

        CostAuditEngine.RecalculateEngagementSummary(engagement);
        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> DeleteReconciliationItemAsync(
        Guid id,
        Guid itemId,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        var item = engagement.ReconciliationItems.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return null;

        engagement.ReconciliationItems.Remove(item);
        CostAuditEngine.RecalculateEngagementSummary(engagement);

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> RecordCra2FilingAsync(
        Guid id,
        RecordCra2FilingRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        engagement.Cra2Srn = req.Cra2Srn;
        engagement.Cra2FiledDate = req.Cra2FiledDate;
        engagement.Cra2FilingStatus = req.Cra2FilingStatus;
        engagement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> RecordCra4FilingAsync(
        Guid id,
        RecordCra4FilingRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        engagement.Cra4Srn = req.Cra4Srn;
        engagement.Cra4FiledDate = req.Cra4FiledDate;
        engagement.Cra4FilingStatus = req.Cra4FilingStatus;
        engagement.Cra4XbrlValidated = req.Cra4XbrlValidated;
        engagement.AuditStatus = CostAuditConstants.StatusFiledCra4;
        engagement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> GenerateCra3ReportAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        CostAuditEngine.RecalculateEngagementSummary(engagement);
        engagement.CostAuditReportText = CostAuditEngine.GenerateFormCra3Report(engagement, engagement.Client);
        engagement.Cra3HandoverDate ??= DateTime.UtcNow;
        engagement.Cra4FilingDueDate = engagement.Cra3HandoverDate.Value.AddDays(30);
        engagement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    public async Task<CostAuditEngagementDto?> CertifyEngagementAsync(
        Guid id,
        CertifyEngagementRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var engagement = await _db.CostAuditEngagements
            .Include(x => x.RecordHeads)
            .Include(x => x.ProductStatements)
            .Include(x => x.ReconciliationItems)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (engagement == null) return null;

        if (string.IsNullOrWhiteSpace(req.Udin) || req.Udin.Trim().Length != 18)
        {
            throw new ArgumentException("A valid 18-character ICMAI / ICAI UDIN is mandatory for certifying the Cost Audit Report (Form CRA-3).");
        }

        engagement.Udin = req.Udin.Trim().ToUpperInvariant();
        engagement.CertifiedBy = req.CertifiedBy;
        engagement.CertifiedAt = DateTime.UtcNow;
        engagement.AuditStatus = CostAuditConstants.StatusCertified;
        engagement.Cra3HandoverDate = DateTime.UtcNow;
        engagement.Cra4FilingDueDate = DateTime.UtcNow.AddDays(30);

        CostAuditEngine.RecalculateEngagementSummary(engagement);
        engagement.CostAuditReportText = CostAuditEngine.GenerateFormCra3Report(engagement, engagement.Client);
        engagement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(engagement);
    }

    private static CostAuditEngagementDto MapToDto(CostAuditEngagement e)
    {
        return new CostAuditEngagementDto(
            e.Id,
            e.ClientId,
            e.Client?.Name ?? "Client Company",
            e.EngagementReference,
            e.FinancialYear,
            e.PeriodStart,
            e.PeriodEnd,
            e.SectorType,
            e.SectorCategory,
            e.OverallTurnoverInr,
            e.ProductTurnoverInr,
            e.ExportTurnoverInr,
            e.IsExportExempt,
            e.IsSezExempt,
            e.IsCaptivePowerExempt,
            e.IsCostRecordsApplicable,
            e.IsCostAuditApplicable,
            e.ApplicabilityReasoning,
            e.BoardResolutionDate,
            e.FyCommencementDate,
            e.AuditCommitteeRecommended,
            e.CostAuditorName,
            e.CostAuditorFirmName,
            e.CostAuditorMembershipNo,
            e.CostAuditorFirmRegNo,
            e.Cra2FilingDueDate,
            e.Cra2FilingStatus,
            e.Cra2Srn,
            e.Cra2FiledDate,
            e.AuditStatus,
            e.AuditorOpinion,
            e.ReservationsQualifications,
            e.ObservationsSuggestions,
            e.Udin,
            e.CertifiedAt,
            e.CertifiedBy,
            e.CostAuditReportText,
            e.Cra3HandoverDate,
            e.Cra4FilingDueDate,
            e.Cra4FilingStatus,
            e.Cra4Srn,
            e.Cra4FiledDate,
            e.Cra4XbrlValidated,
            e.TotalProductsCount,
            e.TotalProductionValueInr,
            e.TotalCostOfSalesInr,
            e.TotalSalesRealizationInr,
            e.TotalCostProfitInr,
            e.AuditedFinancialProfitInr,
            e.ReconciledFinancialProfitInr,
            e.NetReconciliationDifferenceInr,
            e.RecordsComplianceScorePct,
            e.CreatedAt,
            e.UpdatedAt,
            e.RecordHeads.OrderBy(h => h.HeadCode).Select(h => new CostRecordHeadDto(
                h.Id,
                h.EngagementId,
                h.HeadCode,
                h.HeadName,
                h.RuleReference,
                h.Status,
                h.RecordMaintenanceSystem,
                h.VerificationNotes,
                h.DeficiencyOrAnomaly,
                h.CorrectiveActionRequired,
                h.CreatedAt,
                h.UpdatedAt)).ToList(),
            e.ProductStatements.OrderBy(p => p.ProductCode).Select(p => new ProductCostStatementDto(
                p.Id,
                p.EngagementId,
                p.ProductCode,
                p.ProductName,
                p.CtaHeading,
                p.UnitOfMeasurement,
                p.InstalledCapacity,
                p.AvailableCapacity,
                p.ActualProduction,
                p.CapacityUtilizationPct,
                p.DirectMaterialsCost,
                p.DirectLaborCost,
                p.UtilitiesCost,
                p.OtherDirectExpenses,
                p.PrimeCost,
                p.WorksOverheads,
                p.WorksCost,
                p.QualityControlCost,
                p.RndExpenses,
                p.AdministrationOverheads,
                p.OpeningWipCost,
                p.ClosingWipCost,
                p.CostOfProduction,
                p.OpeningFinishedGoodsCost,
                p.ClosingFinishedGoodsCost,
                p.SellingAndDistributionOverheads,
                p.CostOfSales,
                p.NetSalesRealization,
                p.ProductMarginInr,
                p.ProductMarginPct,
                p.UnitCostOfSales,
                p.UnitSalesRealization,
                p.UnitMarginInr,
                p.CreatedAt,
                p.UpdatedAt)).ToList(),
            e.ReconciliationItems.OrderBy(i => i.ItemType).ThenBy(i => i.Description).Select(i => new CostAuditReconciliationItemDto(
                i.Id,
                i.EngagementId,
                i.ItemType,
                i.Description,
                i.AmountInr,
                i.Impact,
                i.FinancialScheduleRef,
                i.Notes,
                i.CreatedAt,
                i.UpdatedAt)).ToList());
    }
}
