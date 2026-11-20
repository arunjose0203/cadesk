using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.InternalAudit;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.InternalAudit;

public class InternalAuditService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public InternalAuditService(IAppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<InternalAuditSummaryDto> GetSummaryAsync(Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var engQuery = _db.InternalAuditEngagements
            .Include(x => x.Controls)
            .Include(x => x.Findings)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) engQuery = engQuery.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) engQuery = engQuery.Where(x => x.FinancialYear == financialYear);

        var engagements = await engQuery.ToListAsync(ct);

        int totalEng = engagements.Count;
        var allControls = engagements.SelectMany(x => x.Controls).ToList();
        var allFindings = engagements.SelectMany(x => x.Findings).ToList();

        decimal healthScore = InternalAuditEngine.CalculateHealthScore(allControls);
        int totalDeficiencies = allFindings.Count;
        int criticalDeficiencies = allFindings.Count(x => x.Severity == "Critical");
        int materialWeaknesses = allFindings.Count(x => x.IfcClassification == "MaterialWeakness");
        decimal totalExposure = allFindings.Sum(x => x.EstimatedRiskAmount ?? 0);
        int openActions = allFindings.Count(x => x.ImplementationStatus == "Open" || x.ImplementationStatus == "InProgress");

        return new InternalAuditSummaryDto(
            TotalEngagements: totalEng,
            TotalControlsTested: allControls.Count(x => x.OperatingEffectiveness != "NotTested"),
            OverallHealthScore: healthScore,
            TotalDeficiencies: totalDeficiencies,
            CriticalDeficiencies: criticalDeficiencies,
            MaterialWeaknesses: materialWeaknesses,
            TotalFinancialExposure: totalExposure,
            OpenActionsCount: openActions
        );
    }

    // -------------------------------------------------------------
    // Engagements
    // -------------------------------------------------------------

    public async Task<List<InternalAuditEngagementDto>> ListEngagementsAsync(Guid? clientId, string? financialYear, string? status, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.InternalAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.Controls)
            .Include(x => x.Findings)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) query = query.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToEngDto).ToList();
    }

    public async Task<InternalAuditEngagementDto?> GetEngagementByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.InternalAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.Controls)
            .Include(x => x.Findings)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        return eng is null ? null : ToEngDto(eng);
    }

    public async Task<InternalAuditEngagementDto> CreateOrUpdateEngagementAsync(
        Guid? id,
        SaveInternalAuditEngagementRequest req,
        bool populateStandardRcm,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        InternalAuditEngagement? eng = null;

        if (id.HasValue && id.Value != Guid.Empty)
        {
            eng = await _db.InternalAuditEngagements
                .Include(x => x.Controls)
                .Include(x => x.Findings)
                .FirstOrDefaultAsync(x => x.Id == id.Value && x.FirmId == firmId, ct);

            if (eng is null) throw new KeyNotFoundException("Internal audit engagement not found.");
        }
        else
        {
            eng = new InternalAuditEngagement
            {
                FirmId = firmId,
                ClientId = req.ClientId,
                FinancialYear = req.FinancialYear,
                AuditPeriod = req.AuditPeriod,
                Title = req.Title.Trim(),
                EngagementLeadName = req.EngagementLeadName?.Trim() ?? "",
                AuditScopeDescription = req.AuditScopeDescription?.Trim() ?? "",
                Status = req.Status ?? "Planning",
                CreatedAt = DateTime.UtcNow
            };

            if (populateStandardRcm)
            {
                eng.Controls = InternalAuditEngine.GenerateStandardRcmTemplate(firmId, eng.Id);
                eng.HealthScorePercentage = InternalAuditEngine.CalculateHealthScore(eng.Controls);
            }

            _db.InternalAuditEngagements.Add(eng);
            await _db.SaveChangesAsync(ct);
            return (await GetEngagementByIdAsync(eng.Id, ct))!;
        }

        eng.FinancialYear = req.FinancialYear;
        eng.AuditPeriod = req.AuditPeriod;
        eng.Title = req.Title.Trim();
        eng.EngagementLeadName = req.EngagementLeadName?.Trim() ?? "";
        eng.AuditScopeDescription = req.AuditScopeDescription?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(req.Status)) eng.Status = req.Status;
        eng.UpdatedAt = DateTime.UtcNow;

        eng.HealthScorePercentage = InternalAuditEngine.CalculateHealthScore(eng.Controls);

        await _db.SaveChangesAsync(ct);
        return (await GetEngagementByIdAsync(eng.Id, ct))!;
    }

    public async Task<bool> DeleteEngagementAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.InternalAuditEngagements.FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);
        if (eng is null) return false;

        _db.InternalAuditEngagements.Remove(eng);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // RCM Controls
    // -------------------------------------------------------------

    public async Task<List<RcmControlItemDto>> ListControlsAsync(Guid engagementId, string? processCycle, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.RcmControlItems.Where(x => x.EngagementId == engagementId && x.FirmId == firmId);

        if (!string.IsNullOrWhiteSpace(processCycle))
        {
            query = query.Where(x => x.ProcessCycle == processCycle);
        }

        var list = await query.OrderBy(x => x.ControlCode).ToListAsync(ct);
        return list.Select(ToControlDto).ToList();
    }

    public async Task<RcmControlItemDto> CreateOrUpdateControlAsync(
        Guid engagementId,
        Guid? controlId,
        SaveRcmControlItemRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.InternalAuditEngagements
            .Include(x => x.Controls)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct);

        if (eng is null) throw new KeyNotFoundException("Internal audit engagement not found.");

        RcmControlItem? c = null;
        if (controlId.HasValue && controlId.Value != Guid.Empty)
        {
            c = await _db.RcmControlItems.FirstOrDefaultAsync(x => x.Id == controlId.Value && x.EngagementId == engagementId && x.FirmId == firmId, ct);
            if (c is null) throw new KeyNotFoundException("RCM control item not found.");
        }
        else
        {
            c = new RcmControlItem
            {
                FirmId = firmId,
                EngagementId = engagementId,
                CreatedAt = DateTime.UtcNow
            };
            _db.RcmControlItems.Add(c);
        }

        c.ControlCode = req.ControlCode.Trim().ToUpperInvariant();
        c.ProcessCycle = req.ProcessCycle;
        c.SubProcess = req.SubProcess.Trim();
        c.RiskDescription = req.RiskDescription.Trim();
        c.RiskCategory = req.RiskCategory ?? "FinancialReporting";
        c.InherentRiskRating = req.InherentRiskRating;
        c.ControlActivity = req.ControlActivity.Trim();
        c.ControlNature = req.ControlNature ?? "Preventive";
        c.ControlFrequency = req.ControlFrequency ?? "Monthly";
        c.ControlType = req.ControlType ?? "Automated";
        c.ControlOwner = req.ControlOwner ?? "";
        c.Assertions = req.Assertions ?? "";

        c.WalkthroughPerformed = req.WalkthroughPerformed;
        c.WalkthroughDate = req.WalkthroughDate;
        c.WalkthroughReference = req.WalkthroughReference ?? "";
        c.DesignEffectiveness = req.DesignEffectiveness;
        c.DesignNotes = req.DesignNotes ?? "";

        c.TestingMethod = req.TestingMethod;
        c.SampleSize = req.SampleSize;
        c.ExceptionsCount = req.ExceptionsCount;
        c.OperatingEffectiveness = req.OperatingEffectiveness;
        c.TestingNotes = req.TestingNotes ?? "";
        c.TestedAt = req.TestedAt;
        c.TestedBy = req.TestedBy ?? "";
        c.UpdatedAt = DateTime.UtcNow;

        // Evaluate residual risk
        InternalAuditEngine.EvaluateResidualRisk(c);

        await _db.SaveChangesAsync(ct);

        // Update health score
        var allControls = await _db.RcmControlItems.Where(x => x.EngagementId == engagementId && x.FirmId == firmId).ToListAsync(ct);
        eng.HealthScorePercentage = InternalAuditEngine.CalculateHealthScore(allControls);
        await _db.SaveChangesAsync(ct);

        return ToControlDto(c);
    }

    public async Task<bool> DeleteControlAsync(Guid engagementId, Guid controlId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var c = await _db.RcmControlItems.FirstOrDefaultAsync(x => x.Id == controlId && x.EngagementId == engagementId && x.FirmId == firmId, ct);
        if (c is null) return false;

        _db.RcmControlItems.Remove(c);
        await _db.SaveChangesAsync(ct);

        var eng = await _db.InternalAuditEngagements.FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct);
        if (eng != null)
        {
            var remaining = await _db.RcmControlItems.Where(x => x.EngagementId == engagementId && x.FirmId == firmId).ToListAsync(ct);
            eng.HealthScorePercentage = InternalAuditEngine.CalculateHealthScore(remaining);
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    // -------------------------------------------------------------
    // Findings & Remediation
    // -------------------------------------------------------------

    public async Task<List<InternalAuditFindingDto>> ListFindingsAsync(Guid engagementId, string? severity, string? status, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.InternalAuditFindings.Where(x => x.EngagementId == engagementId && x.FirmId == firmId);

        if (!string.IsNullOrWhiteSpace(severity)) query = query.Where(x => x.Severity == severity);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.ImplementationStatus == status);

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToFindingDto).ToList();
    }

    public async Task<InternalAuditFindingDto> CreateOrUpdateFindingAsync(
        Guid engagementId,
        Guid? findingId,
        SaveInternalAuditFindingRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        InternalAuditFinding? f = null;

        if (findingId.HasValue && findingId.Value != Guid.Empty)
        {
            f = await _db.InternalAuditFindings.FirstOrDefaultAsync(x => x.Id == findingId.Value && x.EngagementId == engagementId && x.FirmId == firmId, ct);
            if (f is null) throw new KeyNotFoundException("Finding not found.");
        }
        else
        {
            var count = await _db.InternalAuditFindings.CountAsync(x => x.EngagementId == engagementId && x.FirmId == firmId, ct);
            var refNo = string.IsNullOrWhiteSpace(req.FindingReference)
                ? $"IA-FIND-{(count + 1):D3}"
                : req.FindingReference;

            f = new InternalAuditFinding
            {
                FirmId = firmId,
                EngagementId = engagementId,
                FindingReference = refNo,
                CreatedAt = DateTime.UtcNow
            };
            _db.InternalAuditFindings.Add(f);
        }

        f.ControlItemId = req.ControlItemId;
        f.ControlCode = req.ControlCode ?? "";
        f.Title = req.Title.Trim();
        f.ProcessCycle = req.ProcessCycle;
        f.Severity = req.Severity;
        f.DetailedObservation = req.DetailedObservation.Trim();
        f.RootCause = req.RootCause ?? "";
        f.EstimatedRiskAmount = req.EstimatedRiskAmount;
        f.IfcClassification = req.IfcClassification;
        f.AuditorRecommendation = req.AuditorRecommendation.Trim();
        f.ManagementActionPlan = req.ManagementActionPlan ?? "";
        f.ActionOwner = req.ActionOwner ?? "";
        f.TargetCompletionDate = req.TargetCompletionDate;
        if (!string.IsNullOrWhiteSpace(req.ImplementationStatus)) f.ImplementationStatus = req.ImplementationStatus;
        f.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToFindingDto(f);
    }

    public async Task<InternalAuditFindingDto> UpdateFindingStatusAsync(
        Guid engagementId,
        Guid findingId,
        UpdateFindingStatusRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var f = await _db.InternalAuditFindings.FirstOrDefaultAsync(x => x.Id == findingId && x.EngagementId == engagementId && x.FirmId == firmId, ct);
        if (f is null) throw new KeyNotFoundException("Finding not found.");

        f.ImplementationStatus = req.ImplementationStatus;
        if (!string.IsNullOrWhiteSpace(req.ManagementActionPlan)) f.ManagementActionPlan = req.ManagementActionPlan;
        if (req.TargetCompletionDate.HasValue) f.TargetCompletionDate = req.TargetCompletionDate.Value;
        f.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToFindingDto(f);
    }

    public async Task<bool> DeleteFindingAsync(Guid engagementId, Guid findingId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var f = await _db.InternalAuditFindings.FirstOrDefaultAsync(x => x.Id == findingId && x.EngagementId == engagementId && x.FirmId == firmId, ct);
        if (f is null) return false;

        _db.InternalAuditFindings.Remove(f);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Executive Report
    // -------------------------------------------------------------

    public async Task<InternalAuditEngagementDto> GenerateReportAsync(Guid engagementId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.InternalAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.Controls)
            .Include(x => x.Findings)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct);

        if (eng is null) throw new KeyNotFoundException("Engagement not found.");

        eng.HealthScorePercentage = InternalAuditEngine.CalculateHealthScore(eng.Controls);
        eng.GeneratedReportText = InternalAuditEngine.GenerateExecutiveReport(eng, eng.Client?.Name ?? "Client Company");
        eng.ReportDate = DateTime.UtcNow;
        eng.Status = "Reporting";
        eng.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToEngDto(eng);
    }

    private static InternalAuditEngagementDto ToEngDto(InternalAuditEngagement e)
    {
        return new InternalAuditEngagementDto(
            Id: e.Id,
            ClientId: e.ClientId,
            ClientName: e.Client?.Name,
            FinancialYear: e.FinancialYear,
            AuditPeriod: e.AuditPeriod,
            Title: e.Title,
            EngagementLeadName: e.EngagementLeadName,
            AuditScopeDescription: e.AuditScopeDescription,
            Status: e.Status,
            ReportDate: e.ReportDate,
            HealthScorePercentage: e.HealthScorePercentage,
            TotalControls: e.Controls.Count,
            EffectiveControls: e.Controls.Count(x => x.OperatingEffectiveness == "OperatingEffectively" && x.ExceptionsCount == 0),
            TotalFindings: e.Findings.Count,
            CriticalFindings: e.Findings.Count(x => x.Severity == "Critical"),
            GeneratedReportText: e.GeneratedReportText,
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt
        );
    }

    private static RcmControlItemDto ToControlDto(RcmControlItem c)
    {
        return new RcmControlItemDto(
            Id: c.Id,
            EngagementId: c.EngagementId,
            ControlCode: c.ControlCode,
            ProcessCycle: c.ProcessCycle,
            SubProcess: c.SubProcess,
            RiskDescription: c.RiskDescription,
            RiskCategory: c.RiskCategory,
            InherentRiskRating: c.InherentRiskRating,
            ControlActivity: c.ControlActivity,
            ControlNature: c.ControlNature,
            ControlFrequency: c.ControlFrequency,
            ControlType: c.ControlType,
            ControlOwner: c.ControlOwner,
            Assertions: c.Assertions,
            WalkthroughPerformed: c.WalkthroughPerformed,
            WalkthroughDate: c.WalkthroughDate,
            WalkthroughReference: c.WalkthroughReference,
            DesignEffectiveness: c.DesignEffectiveness,
            DesignNotes: c.DesignNotes,
            TestingMethod: c.TestingMethod,
            SampleSize: c.SampleSize,
            ExceptionsCount: c.ExceptionsCount,
            OperatingEffectiveness: c.OperatingEffectiveness,
            TestingNotes: c.TestingNotes,
            TestedAt: c.TestedAt,
            TestedBy: c.TestedBy,
            ResidualRiskRating: c.ResidualRiskRating,
            HasFinding: c.HasFinding,
            CreatedAt: c.CreatedAt,
            UpdatedAt: c.UpdatedAt
        );
    }

    private static InternalAuditFindingDto ToFindingDto(InternalAuditFinding f)
    {
        return new InternalAuditFindingDto(
            Id: f.Id,
            EngagementId: f.EngagementId,
            ControlItemId: f.ControlItemId,
            ControlCode: f.ControlCode,
            FindingReference: f.FindingReference,
            Title: f.Title,
            ProcessCycle: f.ProcessCycle,
            Severity: f.Severity,
            DetailedObservation: f.DetailedObservation,
            RootCause: f.RootCause,
            EstimatedRiskAmount: f.EstimatedRiskAmount,
            IfcClassification: f.IfcClassification,
            AuditorRecommendation: f.AuditorRecommendation,
            ManagementActionPlan: f.ManagementActionPlan,
            ActionOwner: f.ActionOwner,
            TargetCompletionDate: f.TargetCompletionDate,
            ImplementationStatus: f.ImplementationStatus,
            CreatedAt: f.CreatedAt,
            UpdatedAt: f.UpdatedAt
        );
    }
}
