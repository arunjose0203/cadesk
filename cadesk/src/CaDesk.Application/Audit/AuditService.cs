using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Audit;

public class AuditService(IAppDbContext db, ITenantContext tenant)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // =========================================================================
    // Materiality Calculation (ICAI SA 320 / SA 450)
    // =========================================================================

    public static (decimal Overall, decimal Performance, decimal Trivial) CalculateMateriality(
        string benchmarkType,
        decimal benchmarkAmount,
        decimal? customOverallPercent = null,
        decimal? customPerfPercent = null,
        decimal? customTrivialPercent = null)
    {
        if (benchmarkAmount <= 0) return (0, 0, 0);

        decimal defaultOverallPct = benchmarkType.ToLowerInvariant() switch
        {
            BenchmarkType.ProfitBeforeTax => 0.05m, // 5% of PBT
            BenchmarkType.Revenue => 0.01m,          // 1% of Gross Turnover
            BenchmarkType.TotalAssets => 0.01m,      // 1% of Total Assets
            BenchmarkType.Equity => 0.02m,           // 2% of Net Worth / Equity
            _ => 0.01m
        };

        var overallPct = (customOverallPercent ?? (defaultOverallPct * 100m)) / 100m;
        var perfPct = (customPerfPercent ?? 75m) / 100m;
        var trivialPct = (customTrivialPercent ?? 5m) / 100m;

        var overall = Math.Round(benchmarkAmount * overallPct, 2);
        var performance = Math.Round(overall * perfPct, 2);
        var trivial = Math.Round(overall * trivialPct, 2);

        return (overall, performance, trivial);
    }

    private static void AssertNotArchived(AuditEngagement eng)
    {
        if (eng.IsArchived)
        {
            throw new InvalidOperationException("Audit engagement file is locked and archived under ICAI SQC 1. Modifications are strictly prohibited.");
        }
    }

    // =========================================================================
    // Engagement Management
    // =========================================================================

    public async Task<AuditEngagementDto> CreateEngagementAsync(
        CreateAuditEngagementRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Trim().Length < 3)
            throw new RequestValidationException(nameof(req.Title), "Title must be at least 3 characters.");

        var auditType = req.AuditType ?? AuditType.Statutory;
        if (!AuditType.All.Contains(auditType))
            throw new RequestValidationException(nameof(req.AuditType), $"Invalid audit type '{auditType}'.");

        var benchmarkType = req.BenchmarkType ?? BenchmarkType.Revenue;
        var (overall, perf, trivial) = CalculateMateriality(benchmarkType, req.BenchmarkAmount);

        var eng = new AuditEngagement
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            FinancialYear = req.FinancialYear,
            AuditType = auditType,
            Title = req.Title.Trim(),
            LeadPartnerId = req.LeadPartnerId,
            ManagerId = req.ManagerId,
            PlannedStartDate = req.PlannedStartDate,
            PlannedEndDate = req.PlannedEndDate,
            Status = EngagementStatus.Planning,
            BenchmarkType = benchmarkType,
            BenchmarkAmount = req.BenchmarkAmount,
            OverallMateriality = overall,
            PerformanceMateriality = perf,
            TrivialThreshold = trivial,
            Notes = req.Notes?.Trim(),
            EqrRequired = req.EqrRequired,
            EqrPartnerId = req.EqrPartnerId,
            EqrStatus = req.EqrRequired ? EqrReviewStatus.Assigned : EqrReviewStatus.NotApplicable,
            CreatedBy = currentUserId
        };

        db.AuditEngagements.Add(eng);

        // Seed standard ICAI working paper templates
        var templates = GetDefaultWorkingPapers(eng.Id, tenant.FirmId, currentUserId);
        db.AuditWorkingPapers.AddRange(templates);

        // If EQR required and EQR Partner selected, seed initial EQR review with standard checklist
        if (req.EqrRequired && req.EqrPartnerId.HasValue)
        {
            var standardChecklist = AuditQualityEngine.GetStandardPreIssuanceChecklist()
                .Select(c => new PreIssuanceChecklistItemDto(c.Code, c.Description, c.Required, false, null, null))
                .ToList();

            var eqr = new AuditEqrReview
            {
                FirmId = tenant.FirmId,
                EngagementId = eng.Id,
                ReviewType = EqrReviewType.HotReview,
                EqrPartnerId = req.EqrPartnerId.Value,
                ReviewStatus = EqrReviewStatus.Assigned,
                PreIssuanceChecklist = JsonSerializer.Serialize(standardChecklist, JsonOpts),
                CreatedBy = currentUserId
            };
            db.AuditEqrReviews.Add(eqr);
        }

        await db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(eng.Id, ct);
    }

    public async Task<AuditEngagementDto> GetEngagementByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await db.AuditEngagements
            .Include(x => x.Client)
            .Include(x => x.LeadPartner)
            .Include(x => x.Manager)
            .Include(x => x.EqrPartner)
            .Include(x => x.ArchivedByUser)
            .Include(x => x.WorkingPapers)
            .Include(x => x.ReviewNotes)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (e is null) throw new BusinessRuleException("Audit engagement not found.", 404);

        return ToEngagementDto(e);
    }

    public async Task<List<AuditEngagementDto>> ListEngagementsAsync(
        Guid? clientId = null,
        string? financialYear = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = db.AuditEngagements
            .Include(x => x.Client)
            .Include(x => x.LeadPartner)
            .Include(x => x.Manager)
            .Include(x => x.EqrPartner)
            .Include(x => x.ArchivedByUser)
            .Include(x => x.WorkingPapers)
            .Include(x => x.ReviewNotes)
            .AsNoTracking();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear) && financialYear != "all") q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(x => x.Status == status);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToEngagementDto).ToList();
    }

    public async Task<AuditEngagementDto> UpdateMaterialityAsync(
        Guid id,
        UpdateMaterialityRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var e = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) throw new BusinessRuleException("Audit engagement not found.", 404);
        AssertNotArchived(e);

        var (overall, perf, trivial) = CalculateMateriality(
            req.BenchmarkType,
            req.BenchmarkAmount,
            req.OverallMaterialityPercent,
            req.PerformanceMaterialityPercent,
            req.TrivialThresholdPercent
        );

        e.BenchmarkType = req.BenchmarkType;
        e.BenchmarkAmount = req.BenchmarkAmount;
        e.OverallMateriality = overall;
        e.PerformanceMateriality = perf;
        e.TrivialThreshold = trivial;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(id, ct);
    }

    public async Task<AuditEngagementDto> UpdateStatusAsync(
        Guid id,
        UpdateEngagementStatusRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var e = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) throw new BusinessRuleException("Audit engagement not found.", 404);
        AssertNotArchived(e);

        if (!EngagementStatus.All.Contains(req.Status))
            throw new RequestValidationException(nameof(req.Status), $"Invalid status '{req.Status}'.");

        if (!string.IsNullOrWhiteSpace(req.AuditOpinion) && !AuditOpinion.All.Contains(req.AuditOpinion))
            throw new RequestValidationException(nameof(req.AuditOpinion), $"Invalid audit opinion '{req.AuditOpinion}'.");

        e.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.AuditOpinion))
            e.AuditOpinion = req.AuditOpinion;

        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(id, ct);
    }

    public async Task<AuditEngagementDto> SignOffAuditAsync(
        Guid id,
        SignOffAuditReportRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var e = await db.AuditEngagements
            .Include(x => x.ReviewNotes)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (e is null) throw new BusinessRuleException("Audit engagement not found.", 404);
        AssertNotArchived(e);

        var openNotesCount = e.ReviewNotes.Count(n => n.Status is ReviewNoteStatus.Open or ReviewNoteStatus.Reopened);
        var (allowed, reason) = AuditQualityEngine.CanSignOffAudit(openNotesCount, e.EqrRequired, e.EqrStatus);
        if (!allowed)
        {
            throw new InvalidOperationException(reason);
        }

        e.AuditReportDate = req.AuditReportDate;
        e.ArchivalDeadline = AuditQualityEngine.CalculateArchivalDeadline(req.AuditReportDate);
        e.AuditOpinion = req.AuditOpinion;
        e.Status = EngagementStatus.PartnerSignoff;
        if (!string.IsNullOrWhiteSpace(req.Notes))
        {
            e.Notes = string.IsNullOrWhiteSpace(e.Notes) ? req.Notes : $"{e.Notes}\n[Sign-off Notes]: {req.Notes}";
        }

        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(id, ct);
    }

    public async Task<AuditEngagementDto> ArchiveEngagementAsync(
        Guid id,
        ArchiveEngagementRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var e = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) throw new BusinessRuleException("Audit engagement not found.", 404);
        if (e.IsArchived) throw new InvalidOperationException("Audit engagement is already archived and locked.");

        e.IsArchived = true;
        e.ArchivedAt = DateTime.UtcNow;
        e.ArchivedBy = currentUserId;
        e.Status = EngagementStatus.Archived;

        if (!string.IsNullOrWhiteSpace(req.Notes))
        {
            e.Notes = string.IsNullOrWhiteSpace(e.Notes) ? req.Notes : $"{e.Notes}\n[Archival Notes]: {req.Notes}";
        }

        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(id, ct);
    }

    public async Task<bool> DeleteEngagementAsync(Guid id, CancellationToken ct = default)
    {
        var e = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        AssertNotArchived(e);

        db.AuditEngagements.Remove(e);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // =========================================================================
    // Working Papers Management
    // =========================================================================

    public async Task<List<AuditWorkingPaperDto>> ListWorkingPapersAsync(
        Guid engagementId,
        string? area = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = db.AuditWorkingPapers
            .Include(x => x.Preparer)
            .Include(x => x.Reviewer)
            .Include(x => x.ReviewNotesList)
            .AsNoTracking()
            .Where(x => x.EngagementId == engagementId);

        if (!string.IsNullOrWhiteSpace(area) && area != "all") q = q.Where(x => x.Area == area);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(x => x.Status == status);

        var list = await q.OrderBy(x => x.RefCode).ToListAsync(ct);
        return list.Select(ToWorkingPaperDto).ToList();
    }

    public async Task<AuditWorkingPaperDto> GetWorkingPaperByIdAsync(Guid id, CancellationToken ct = default)
    {
        var wp = await db.AuditWorkingPapers
            .Include(x => x.Preparer)
            .Include(x => x.Reviewer)
            .Include(x => x.ReviewNotesList)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (wp is null) throw new BusinessRuleException("Working paper not found.", 404);

        return ToWorkingPaperDto(wp);
    }

    public async Task<AuditWorkingPaperDto> CreateWorkingPaperAsync(
        Guid engagementId,
        SaveWorkingPaperRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var eng = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == engagementId, ct);
        if (eng is null) throw new BusinessRuleException("Audit engagement not found.", 404);
        AssertNotArchived(eng);

        if (string.IsNullOrWhiteSpace(req.RefCode))
            throw new RequestValidationException(nameof(req.RefCode), "Reference code is required (e.g. WP/BNK/01).");
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new RequestValidationException(nameof(req.Title), "Title is required.");

        var wp = new AuditWorkingPaper
        {
            FirmId = tenant.FirmId,
            EngagementId = engagementId,
            RefCode = req.RefCode.Trim().ToUpperInvariant(),
            Area = req.Area.Trim(),
            Title = req.Title.Trim(),
            Assertion = req.Assertion ?? AuditAssertion.Existence,
            Objective = req.Objective.Trim(),
            ProceduresPerformed = req.ProceduresPerformed?.Trim() ?? "",
            SamplingMethod = req.SamplingMethod ?? SamplingMethod.Haphazard,
            PopulationValue = req.PopulationValue,
            SampleValue = req.SampleValue,
            ItemsTestedCount = req.ItemsTestedCount,
            ExceptionsNoted = req.ExceptionsNoted?.Trim(),
            Conclusion = req.Conclusion ?? AuditConclusion.PendingEvidence,
            Status = WorkingPaperStatus.Draft,
            PreparerId = currentUserId,
            PreparedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };

        db.AuditWorkingPapers.Add(wp);
        await db.SaveChangesAsync(ct);

        return await GetWorkingPaperByIdAsync(wp.Id, ct);
    }

    public async Task<AuditWorkingPaperDto> UpdateWorkingPaperAsync(
        Guid id,
        SaveWorkingPaperRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var wp = await db.AuditWorkingPapers
            .Include(x => x.Engagement)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (wp is null) throw new BusinessRuleException("Working paper not found.", 404);
        if (wp.Engagement != null) AssertNotArchived(wp.Engagement);

        wp.RefCode = req.RefCode.Trim().ToUpperInvariant();
        wp.Area = req.Area.Trim();
        wp.Title = req.Title.Trim();
        if (!string.IsNullOrWhiteSpace(req.Assertion)) wp.Assertion = req.Assertion;
        wp.Objective = req.Objective.Trim();
        wp.ProceduresPerformed = req.ProceduresPerformed?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(req.SamplingMethod)) wp.SamplingMethod = req.SamplingMethod;
        wp.PopulationValue = req.PopulationValue;
        wp.SampleValue = req.SampleValue;
        wp.ItemsTestedCount = req.ItemsTestedCount;
        wp.ExceptionsNoted = req.ExceptionsNoted?.Trim();
        if (!string.IsNullOrWhiteSpace(req.Conclusion)) wp.Conclusion = req.Conclusion;

        wp.UpdatedAt = DateTime.UtcNow;
        wp.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return await GetWorkingPaperByIdAsync(id, ct);
    }

    public async Task<AuditWorkingPaperDto> ReviewWorkingPaperAsync(
        Guid id,
        ReviewWorkingPaperRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var wp = await db.AuditWorkingPapers
            .Include(x => x.Engagement)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (wp is null) throw new BusinessRuleException("Working paper not found.", 404);
        if (wp.Engagement != null) AssertNotArchived(wp.Engagement);

        if (!WorkingPaperStatus.All.Contains(req.Status))
            throw new RequestValidationException(nameof(req.Status), $"Invalid working paper status '{req.Status}'.");

        wp.Status = req.Status;
        wp.ReviewerId = currentUserId;
        wp.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(req.ReviewNotes))
            wp.ReviewNotes = req.ReviewNotes.Trim();

        wp.UpdatedAt = DateTime.UtcNow;
        wp.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return await GetWorkingPaperByIdAsync(id, ct);
    }

    public async Task<bool> DeleteWorkingPaperAsync(Guid id, CancellationToken ct = default)
    {
        var wp = await db.AuditWorkingPapers
            .Include(x => x.Engagement)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (wp is null) return false;
        if (wp.Engagement != null) AssertNotArchived(wp.Engagement);

        db.AuditWorkingPapers.Remove(wp);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // =========================================================================
    // Engagement Quality Review (EQR) (ICAI SQC 1 / SA 220)
    // =========================================================================

    public async Task<AuditEqrReviewDto?> GetEqrReviewAsync(Guid engagementId, CancellationToken ct = default)
    {
        var eqr = await db.AuditEqrReviews
            .Include(x => x.EqrPartner)
            .AsNoTracking()
            .Where(x => x.EngagementId == engagementId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return eqr != null ? ToEqrDto(eqr) : null;
    }

    public async Task<AuditEqrReviewDto> AssignEqrReviewAsync(
        Guid engagementId,
        CreateEqrReviewRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var eng = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == engagementId, ct);
        if (eng is null) throw new BusinessRuleException("Audit engagement not found.", 404);
        AssertNotArchived(eng);

        var eqrPartner = await db.Users.FirstOrDefaultAsync(u => u.Id == req.EqrPartnerId, ct);
        if (eqrPartner is null) throw new BusinessRuleException("Selected EQR partner not found.", 404);

        eng.EqrRequired = true;
        eng.EqrPartnerId = req.EqrPartnerId;
        eng.EqrStatus = EqrReviewStatus.Assigned;
        eng.UpdatedAt = DateTime.UtcNow;
        eng.UpdatedBy = currentUserId;

        var standardChecklist = AuditQualityEngine.GetStandardPreIssuanceChecklist()
            .Select(c => new PreIssuanceChecklistItemDto(c.Code, c.Description, c.Required, false, null, null))
            .ToList();

        var eqr = new AuditEqrReview
        {
            FirmId = tenant.FirmId,
            EngagementId = engagementId,
            ReviewType = req.ReviewType ?? EqrReviewType.HotReview,
            EqrPartnerId = req.EqrPartnerId,
            ReviewStatus = EqrReviewStatus.Assigned,
            PreIssuanceChecklist = JsonSerializer.Serialize(standardChecklist, JsonOpts),
            CreatedBy = currentUserId
        };

        db.AuditEqrReviews.Add(eqr);
        await db.SaveChangesAsync(ct);

        return ToEqrDto(eqr, eqrPartner.Name);
    }

    public async Task<AuditEqrReviewDto> UpdateEqrReviewAsync(
        Guid id,
        UpdateEqrReviewRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var eqr = await db.AuditEqrReviews
            .Include(x => x.Engagement)
            .Include(x => x.EqrPartner)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (eqr is null) throw new BusinessRuleException("EQR review not found.", 404);
        if (eqr.Engagement != null) AssertNotArchived(eqr.Engagement);

        if (req.EvaluationMateriality != null) eqr.EvaluationMateriality = req.EvaluationMateriality;
        if (req.EvaluationSignificantRisks != null) eqr.EvaluationSignificantRisks = req.EvaluationSignificantRisks;
        if (req.EvaluationGoingConcern != null) eqr.EvaluationGoingConcern = req.EvaluationGoingConcern;
        if (req.EvaluationAuditOpinion != null) eqr.EvaluationAuditOpinion = req.EvaluationAuditOpinion;
        if (req.EqrConclusionRemarks != null) eqr.EqrConclusionRemarks = req.EqrConclusionRemarks;
        if (req.PreIssuanceChecklist != null) eqr.PreIssuanceChecklist = JsonSerializer.Serialize(req.PreIssuanceChecklist, JsonOpts);

        eqr.ReviewStatus = EqrReviewStatus.InProgress;
        eqr.UpdatedAt = DateTime.UtcNow;
        eqr.UpdatedBy = currentUserId;

        if (eqr.Engagement != null)
        {
            eqr.Engagement.EqrStatus = EqrReviewStatus.InProgress;
        }

        await db.SaveChangesAsync(ct);
        return ToEqrDto(eqr);
    }

    public async Task<AuditEqrReviewDto> ClearEqrReviewAsync(
        Guid id,
        ClearEqrReviewRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var eqr = await db.AuditEqrReviews
            .Include(x => x.Engagement)
            .Include(x => x.EqrPartner)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (eqr is null) throw new BusinessRuleException("EQR review not found.", 404);
        if (eqr.Engagement != null) AssertNotArchived(eqr.Engagement);

        // Verify mandatory checklist items
        if (req.PreIssuanceChecklist.Any(c => c.Required && !c.IsVerified))
        {
            throw new InvalidOperationException("All mandatory pre-issuance quality checklist items must be verified before granting EQR clearance.");
        }

        eqr.PreIssuanceChecklist = JsonSerializer.Serialize(req.PreIssuanceChecklist, JsonOpts);
        eqr.EqrConclusionRemarks = req.EqrConclusionRemarks;
        eqr.EqrSignoffDate = DateTime.UtcNow;
        eqr.ReviewStatus = EqrReviewStatus.ClearanceGranted;
        eqr.UpdatedAt = DateTime.UtcNow;
        eqr.UpdatedBy = currentUserId;

        if (eqr.Engagement != null)
        {
            eqr.Engagement.EqrStatus = EqrReviewStatus.ClearanceGranted;
        }

        await db.SaveChangesAsync(ct);
        return ToEqrDto(eqr);
    }

    // =========================================================================
    // Review Notes & Query Resolution Workflow
    // =========================================================================

    public async Task<List<AuditReviewNoteDto>> ListReviewNotesAsync(
        Guid engagementId,
        Guid? workingPaperId = null,
        string? status = null,
        string? priority = null,
        CancellationToken ct = default)
    {
        var q = db.AuditReviewNotes
            .Include(x => x.WorkingPaper)
            .Include(x => x.RaisedByUser)
            .Include(x => x.ResponseByUser)
            .Include(x => x.ClearedByUser)
            .AsNoTracking()
            .Where(x => x.EngagementId == engagementId);

        if (workingPaperId.HasValue) q = q.Where(x => x.WorkingPaperId == workingPaperId.Value);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(priority) && priority != "all") q = q.Where(x => x.Priority == priority);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(n => ToReviewNoteDto(n, n.WorkingPaper?.RefCode, n.WorkingPaper?.Title, n.RaisedByUser?.Name, n.ResponseByUser?.Name, n.ClearedByUser?.Name)).ToList();
    }

    public async Task<AuditReviewNoteDto> AddReviewNoteAsync(
        Guid engagementId,
        CreateReviewNoteRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var eng = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == engagementId, ct);
        if (eng is null) throw new BusinessRuleException("Audit engagement not found.", 404);
        AssertNotArchived(eng);

        if (string.IsNullOrWhiteSpace(req.Query))
            throw new RequestValidationException(nameof(req.Query), "Review query description is required.");

        var note = new AuditReviewNote
        {
            FirmId = tenant.FirmId,
            EngagementId = engagementId,
            WorkingPaperId = req.WorkingPaperId,
            RaisedByUserId = currentUserId,
            RaisedByRole = req.RaisedByRole ?? ReviewNoteRole.Manager,
            Category = req.Category ?? ReviewNoteCategory.DocumentationGap,
            Priority = req.Priority ?? ReviewNotePriority.Medium,
            Query = req.Query.Trim(),
            Status = ReviewNoteStatus.Open,
            CreatedBy = currentUserId
        };

        db.AuditReviewNotes.Add(note);
        await db.SaveChangesAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
        var wp = req.WorkingPaperId.HasValue ? await db.AuditWorkingPapers.FirstOrDefaultAsync(w => w.Id == req.WorkingPaperId.Value, ct) : null;

        return ToReviewNoteDto(note, wp?.RefCode, wp?.Title, user?.Name);
    }

    public async Task<AuditReviewNoteDto> RespondReviewNoteAsync(
        Guid noteId,
        RespondReviewNoteRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var note = await db.AuditReviewNotes
            .Include(x => x.Engagement)
            .Include(x => x.WorkingPaper)
            .Include(x => x.RaisedByUser)
            .FirstOrDefaultAsync(x => x.Id == noteId, ct);

        if (note is null) throw new BusinessRuleException("Review note not found.", 404);
        if (note.Engagement != null) AssertNotArchived(note.Engagement);

        note.Response = req.Response.Trim();
        note.ResponseByUserId = currentUserId;
        note.ResponseAt = DateTime.UtcNow;
        note.Status = ReviewNoteStatus.ResponseProvided;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);

        var responder = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
        return ToReviewNoteDto(note, note.WorkingPaper?.RefCode, note.WorkingPaper?.Title, note.RaisedByUser?.Name, responder?.Name);
    }

    public async Task<AuditReviewNoteDto> ClearReviewNoteAsync(
        Guid noteId,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var note = await db.AuditReviewNotes
            .Include(x => x.Engagement)
            .Include(x => x.WorkingPaper)
            .Include(x => x.RaisedByUser)
            .Include(x => x.ResponseByUser)
            .FirstOrDefaultAsync(x => x.Id == noteId, ct);

        if (note is null) throw new BusinessRuleException("Review note not found.", 404);
        if (note.Engagement != null) AssertNotArchived(note.Engagement);

        note.ClearedByUserId = currentUserId;
        note.ClearedAt = DateTime.UtcNow;
        note.Status = ReviewNoteStatus.Cleared;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);

        var clearer = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
        return ToReviewNoteDto(note, note.WorkingPaper?.RefCode, note.WorkingPaper?.Title, note.RaisedByUser?.Name, note.ResponseByUser?.Name, clearer?.Name);
    }

    public async Task<AuditReviewNoteDto> ReopenReviewNoteAsync(
        Guid noteId,
        ReopenReviewNoteRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var note = await db.AuditReviewNotes
            .Include(x => x.Engagement)
            .Include(x => x.WorkingPaper)
            .Include(x => x.RaisedByUser)
            .Include(x => x.ResponseByUser)
            .FirstOrDefaultAsync(x => x.Id == noteId, ct);

        if (note is null) throw new BusinessRuleException("Review note not found.", 404);
        if (note.Engagement != null) AssertNotArchived(note.Engagement);

        note.Status = ReviewNoteStatus.Reopened;
        if (!string.IsNullOrWhiteSpace(req.Remarks))
        {
            note.Query = $"{note.Query}\n[Reopened Note]: {req.Remarks.Trim()}";
        }
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return ToReviewNoteDto(note, note.WorkingPaper?.RefCode, note.WorkingPaper?.Title, note.RaisedByUser?.Name, note.ResponseByUser?.Name);
    }

    // =========================================================================
    // Summary
    // =========================================================================

    public async Task<AuditSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var engagements = await db.AuditEngagements
            .Include(x => x.WorkingPapers)
            .Include(x => x.ReviewNotes)
            .AsNoTracking()
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var total = engagements.Count;
        var planning = engagements.Count(x => x.Status == EngagementStatus.Planning);
        var fieldwork = engagements.Count(x => x.Status == EngagementStatus.Fieldwork);
        var review = engagements.Count(x => x.Status == EngagementStatus.Review);
        var signedOff = engagements.Count(x => x.Status == EngagementStatus.PartnerSignoff);
        var archived = engagements.Count(x => x.IsArchived || x.Status == EngagementStatus.Archived);

        var dueSoon = engagements.Count(x => !x.IsArchived && x.ArchivalDeadline.HasValue &&
            (x.ArchivalDeadline.Value.DayNumber - today.DayNumber) is >= 0 and <= 15);

        var overdue = engagements.Count(x => !x.IsArchived && x.ArchivalDeadline.HasValue &&
            (x.ArchivalDeadline.Value.DayNumber - today.DayNumber) < 0);

        var eqrPending = engagements.Count(x => x.EqrRequired && x.EqrStatus != EqrReviewStatus.ClearanceGranted);

        var allWps = engagements.SelectMany(x => x.WorkingPapers).ToList();
        var totalWps = allWps.Count;
        var exceptions = allWps.Count(x => !string.IsNullOrWhiteSpace(x.ExceptionsNoted));

        var openNotes = engagements.SelectMany(x => x.ReviewNotes)
            .Count(n => n.Status is ReviewNoteStatus.Open or ReviewNoteStatus.Reopened);

        return new AuditSummaryDto(
            total,
            planning,
            fieldwork,
            review,
            signedOff,
            archived,
            dueSoon,
            overdue,
            eqrPending,
            totalWps,
            openNotes,
            exceptions
        );
    }

    public static List<PreIssuanceChecklistItemDto> GetChecklistTemplate()
    {
        return AuditQualityEngine.GetStandardPreIssuanceChecklist()
            .Select(c => new PreIssuanceChecklistItemDto(c.Code, c.Description, c.Required, false, null, null))
            .ToList();
    }

    // =========================================================================
    // Default Working Papers Seeder
    // =========================================================================

    private static List<AuditWorkingPaper> GetDefaultWorkingPapers(Guid engagementId, Guid firmId, Guid userId) =>
    [
        new()
        {
            FirmId = firmId,
            EngagementId = engagementId,
            RefCode = "WP-REV-01",
            Area = "Revenue & Sales",
            Title = "Cut-off and Analytical Review of Sales",
            Assertion = AuditAssertion.Completeness,
            Objective = "Test sales invoices around year-end to verify cut-off and compare revenue trends.",
            SamplingMethod = SamplingMethod.RiskTargeted,
            Conclusion = AuditConclusion.PendingEvidence,
            CreatedBy = userId
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engagementId,
            RefCode = "WP-REC-01",
            Area = "Trade Receivables",
            Title = "External Confirmation & Aging Analysis",
            Assertion = AuditAssertion.Existence,
            Objective = "Circularize SA 505 external balance confirmations and evaluate ECL provision.",
            SamplingMethod = SamplingMethod.MonetaryUnitSampling,
            Conclusion = AuditConclusion.PendingEvidence,
            CreatedBy = userId
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engagementId,
            RefCode = "WP-BNK-01",
            Area = "Bank & Cash",
            Title = "Bank Reconciliation & Direct Confirmations",
            Assertion = AuditAssertion.Existence,
            Objective = "Obtain independent bank balance certificates and verify BRS reconciling items.",
            SamplingMethod = SamplingMethod.HundredPercent,
            Conclusion = AuditConclusion.PendingEvidence,
            CreatedBy = userId
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engagementId,
            RefCode = "WP-FA-01",
            Area = "Fixed Assets",
            Title = "Physical Verification & Title Deeds Audit",
            Assertion = AuditAssertion.RightsObligations,
            Objective = "Verify FAR reconciliation, physical verification reports, and CARO 2020 clause (i) title deed records.",
            SamplingMethod = SamplingMethod.Stratified,
            Conclusion = AuditConclusion.PendingEvidence,
            CreatedBy = userId
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engagementId,
            RefCode = "WP-INV-01",
            Area = "Inventory",
            Title = "Physical Inventory Count & NRV Testing",
            Assertion = AuditAssertion.Valuation,
            Objective = "Attend year-end physical inventory count under SA 501 and evaluate lower of cost and NRV.",
            SamplingMethod = SamplingMethod.RiskTargeted,
            Conclusion = AuditConclusion.PendingEvidence,
            CreatedBy = userId
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engagementId,
            RefCode = "WP-STAT-01",
            Area = "Statutory Dues",
            Title = "Timely Deposit of PF, ESIC, GST, and TDS",
            Assertion = AuditAssertion.Completeness,
            Objective = "Verify challans and dates of payment to assess CARO 2020 clause (vii) undisputed dues.",
            SamplingMethod = SamplingMethod.HundredPercent,
            Conclusion = AuditConclusion.PendingEvidence,
            CreatedBy = userId
        }
    ];

    private static AuditEngagementDto ToEngagementDto(AuditEngagement e)
    {
        var totalWps = e.WorkingPapers?.Count ?? 0;
        var reviewedWps = e.WorkingPapers?.Count(x => x.Status is WorkingPaperStatus.Reviewed or WorkingPaperStatus.PartnerApproved) ?? 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var archivalStatus = AuditQualityEngine.GetArchivalStatus(e.IsArchived, e.AuditReportDate, e.ArchivalDeadline, today);
        var openNotes = e.ReviewNotes?.Count(n => n.Status is ReviewNoteStatus.Open or ReviewNoteStatus.Reopened) ?? 0;
        var (canSignOff, validationMsg) = AuditQualityEngine.CanSignOffAudit(openNotes, e.EqrRequired, e.EqrStatus);

        return new AuditEngagementDto(
            e.Id,
            e.ClientId,
            e.Client?.Name,
            e.FinancialYear,
            e.AuditType,
            e.Title,
            e.LeadPartnerId,
            e.LeadPartner?.Name,
            e.ManagerId,
            e.Manager?.Name,
            e.PlannedStartDate,
            e.PlannedEndDate,
            e.Status,
            e.BenchmarkType,
            e.BenchmarkAmount,
            e.OverallMateriality,
            e.PerformanceMateriality,
            e.TrivialThreshold,
            e.AuditOpinion,
            e.Notes,
            e.AuditReportDate,
            e.ArchivalDeadline,
            e.IsArchived,
            e.ArchivedAt,
            e.ArchivedBy,
            e.ArchivedByUser?.Name,
            e.EqrRequired,
            e.EqrPartnerId,
            e.EqrPartner?.Name,
            e.EqrStatus,
            archivalStatus,
            openNotes,
            canSignOff,
            validationMsg,
            totalWps,
            reviewedWps,
            e.CreatedAt,
            e.UpdatedAt
        );
    }

    private static AuditWorkingPaperDto ToWorkingPaperDto(AuditWorkingPaper w)
    {
        var attachments = JsonSerializer.Deserialize<List<string>>(w.Attachments, JsonOpts) ?? [];
        var totalNotes = w.ReviewNotesList?.Count ?? 0;
        var openNotes = w.ReviewNotesList?.Count(n => n.Status is ReviewNoteStatus.Open or ReviewNoteStatus.Reopened) ?? 0;

        return new AuditWorkingPaperDto(
            w.Id,
            w.EngagementId,
            w.RefCode,
            w.Area,
            w.Title,
            w.Assertion,
            w.Objective,
            w.ProceduresPerformed,
            w.SamplingMethod,
            w.PopulationValue,
            w.SampleValue,
            w.ItemsTestedCount,
            w.ExceptionsNoted,
            w.Conclusion,
            w.Status,
            w.PreparerId,
            w.Preparer?.Name,
            w.PreparedAt,
            w.ReviewerId,
            w.Reviewer?.Name,
            w.ReviewedAt,
            w.ReviewNotes,
            attachments,
            totalNotes,
            openNotes,
            w.CreatedAt,
            w.UpdatedAt
        );
    }

    private static AuditEqrReviewDto ToEqrDto(AuditEqrReview eqr, string? partnerName = null)
    {
        var checklist = JsonSerializer.Deserialize<List<PreIssuanceChecklistItemDto>>(eqr.PreIssuanceChecklist, JsonOpts) ?? [];

        return new AuditEqrReviewDto(
            eqr.Id,
            eqr.EngagementId,
            eqr.ReviewType,
            eqr.EqrPartnerId,
            partnerName ?? eqr.EqrPartner?.Name,
            eqr.ReviewStatus,
            eqr.EvaluationMateriality,
            eqr.EvaluationSignificantRisks,
            eqr.EvaluationGoingConcern,
            eqr.EvaluationAuditOpinion,
            checklist,
            eqr.EqrSignoffDate,
            eqr.EqrConclusionRemarks,
            eqr.CreatedAt,
            eqr.UpdatedAt
        );
    }

    private static AuditReviewNoteDto ToReviewNoteDto(
        AuditReviewNote n,
        string? wpRef = null,
        string? wpTitle = null,
        string? raisedByName = null,
        string? responseByName = null,
        string? clearedByName = null)
    {
        return new AuditReviewNoteDto(
            n.Id,
            n.EngagementId,
            n.WorkingPaperId,
            wpRef ?? n.WorkingPaper?.RefCode,
            wpTitle ?? n.WorkingPaper?.Title,
            n.RaisedByUserId,
            raisedByName ?? n.RaisedByUser?.Name,
            n.RaisedByRole,
            n.Category,
            n.Priority,
            n.Query,
            n.Status,
            n.Response,
            n.ResponseByUserId,
            responseByName ?? n.ResponseByUser?.Name,
            n.ResponseAt,
            n.ClearedByUserId,
            clearedByName ?? n.ClearedByUser?.Name,
            n.ClearedAt,
            n.CreatedAt,
            n.UpdatedAt
        );
    }
}
