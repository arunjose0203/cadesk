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
            CreatedBy = currentUserId
        };

        db.AuditEngagements.Add(eng);

        // Seed standard ICAI working paper templates
        var templates = GetDefaultWorkingPapers(eng.Id, tenant.FirmId, currentUserId);
        db.AuditWorkingPapers.AddRange(templates);

        await db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(eng.Id, ct);
    }

    public async Task<AuditEngagementDto> GetEngagementByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await db.AuditEngagements
            .Include(x => x.Client)
            .Include(x => x.LeadPartner)
            .Include(x => x.Manager)
            .Include(x => x.WorkingPapers)
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
            .Include(x => x.WorkingPapers)
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

    public async Task<bool> DeleteEngagementAsync(Guid id, CancellationToken ct = default)
    {
        var e = await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;

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
        var wp = await db.AuditWorkingPapers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (wp is null) throw new BusinessRuleException("Working paper not found.", 404);

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
        var wp = await db.AuditWorkingPapers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (wp is null) throw new BusinessRuleException("Working paper not found.", 404);

        if (!WorkingPaperStatus.All.Contains(req.Status))
            throw new RequestValidationException(nameof(req.Status), $"Invalid status '{req.Status}'.");

        wp.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.ReviewNotes)) wp.ReviewNotes = req.ReviewNotes.Trim();

        if (req.Status is WorkingPaperStatus.Reviewed or WorkingPaperStatus.PartnerApproved)
        {
            wp.ReviewerId = currentUserId;
            wp.ReviewedAt = DateTime.UtcNow;
        }

        wp.UpdatedAt = DateTime.UtcNow;
        wp.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return await GetWorkingPaperByIdAsync(id, ct);
    }

    public async Task<bool> DeleteWorkingPaperAsync(Guid id, CancellationToken ct = default)
    {
        var wp = await db.AuditWorkingPapers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (wp is null) return false;

        db.AuditWorkingPapers.Remove(wp);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // =========================================================================
    // Summary KPI
    // =========================================================================

    public async Task<AuditSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var engagements = await db.AuditEngagements.AsNoTracking().ToListAsync(ct);
        var workingPapers = await db.AuditWorkingPapers.AsNoTracking().ToListAsync(ct);

        var totalEng = engagements.Count;
        var planning = engagements.Count(x => x.Status == EngagementStatus.Planning);
        var fieldwork = engagements.Count(x => x.Status == EngagementStatus.Fieldwork);
        var review = engagements.Count(x => x.Status == EngagementStatus.Review);
        var signedOff = engagements.Count(x => x.Status == EngagementStatus.PartnerSignoff);

        var totalWps = workingPapers.Count;
        var exceptionsCount = workingPapers.Count(x => x.Conclusion is AuditConclusion.QualifiedFinding or AuditConclusion.MaterialMisstatement);

        return new AuditSummaryDto(totalEng, planning, fieldwork, review, signedOff, totalWps, exceptionsCount);
    }

    // =========================================================================
    // Helpers & ICAI Template Seeding
    // =========================================================================

    private static List<AuditWorkingPaper> GetDefaultWorkingPapers(Guid engId, Guid firmId, Guid currentUserId) =>
    [
        new()
        {
            FirmId = firmId,
            EngagementId = engId,
            RefCode = "WP/BNK/01",
            Area = "Cash & Bank Balances",
            Title = "Bank Balances Confirmation & Reconciliation (BRS)",
            Assertion = AuditAssertion.Existence,
            Objective = "Verify existence and accuracy of bank balances via third-party direct confirmations and review of outstanding reconciling items.",
            ProceduresPerformed = "Obtained bank statements and BRS for all operative accounts. Verified subsequent clearance of uncredited cheques and unpresented cheques.",
            SamplingMethod = SamplingMethod.HundredPercent,
            Conclusion = AuditConclusion.PendingEvidence,
            Status = WorkingPaperStatus.Draft,
            PreparerId = currentUserId,
            PreparedAt = DateTime.UtcNow
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engId,
            RefCode = "WP/REV/01",
            Area = "Revenue & Receivables",
            Title = "Revenue Cut-off & Substantive Sampling (GSTR-1 vs Financials)",
            Assertion = AuditAssertion.Completeness,
            Objective = "Ensure revenue is recognized in correct accounting period and sales register reconciles with GST portal returns.",
            ProceduresPerformed = "Tested sales entries 5 days before and 5 days after FY closure against lorry receipts (e-way bills) and delivery notes.",
            SamplingMethod = SamplingMethod.Stratified,
            Conclusion = AuditConclusion.PendingEvidence,
            Status = WorkingPaperStatus.Draft,
            PreparerId = currentUserId,
            PreparedAt = DateTime.UtcNow
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engId,
            RefCode = "WP/PUR/01",
            Area = "Purchases & Trade Payables",
            Title = "Purchase Register vs GSTR-2B ITC Matching & MSME Search",
            Assertion = AuditAssertion.Accuracy,
            Objective = "Verify input tax credit genuineness and compliance with MSMED Act 45-day payment statutory deadlines.",
            ProceduresPerformed = "Reconciled purchase ledger with GSTR-2B. Cross-checked MSME UDYAM registration status of active vendors.",
            SamplingMethod = SamplingMethod.MonetaryUnitSampling,
            Conclusion = AuditConclusion.PendingEvidence,
            Status = WorkingPaperStatus.Draft,
            PreparerId = currentUserId,
            PreparedAt = DateTime.UtcNow
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engId,
            RefCode = "WP/AST/01",
            Area = "Fixed Assets & Depreciation",
            Title = "Fixed Asset Additions & Schedule II Useful Life Verification",
            Assertion = AuditAssertion.Valuation,
            Objective = "Verify asset additions during the year have proper invoices, title deeds, and depreciation charged per Companies Act Schedule II.",
            ProceduresPerformed = "Vouched 100% of capital asset additions above performance materiality. Recalculated SLM/WDV depreciation rates.",
            SamplingMethod = SamplingMethod.RiskTargeted,
            Conclusion = AuditConclusion.PendingEvidence,
            Status = WorkingPaperStatus.Draft,
            PreparerId = currentUserId,
            PreparedAt = DateTime.UtcNow
        },
        new()
        {
            FirmId = firmId,
            EngagementId = engId,
            RefCode = "WP/TAX/01",
            Area = "Statutory Dues & Compliances",
            Title = "TDS, GST & PF/ESI Payment Challan Verification",
            Assertion = AuditAssertion.Completeness,
            Objective = "Ensure timely deposit of statutory dues to prevent disallowance under Section 43B and Section 36(1)(va) of Income Tax Act.",
            ProceduresPerformed = "Verified monthly challans with due dates and audited Form 26AS/AIS for corresponding credits.",
            SamplingMethod = SamplingMethod.HundredPercent,
            Conclusion = AuditConclusion.PendingEvidence,
            Status = WorkingPaperStatus.Draft,
            PreparerId = currentUserId,
            PreparedAt = DateTime.UtcNow
        }
    ];

    private static AuditEngagementDto ToEngagementDto(AuditEngagement e)
    {
        var totalWps = e.WorkingPapers?.Count ?? 0;
        var reviewedWps = e.WorkingPapers?.Count(x => x.Status is WorkingPaperStatus.Reviewed or WorkingPaperStatus.PartnerApproved) ?? 0;

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
            totalWps,
            reviewedWps,
            e.CreatedAt,
            e.UpdatedAt
        );
    }

    private static AuditWorkingPaperDto ToWorkingPaperDto(AuditWorkingPaper w)
    {
        var attachments = JsonSerializer.Deserialize<List<string>>(w.Attachments, JsonOpts) ?? [];

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
            w.CreatedAt,
            w.UpdatedAt
        );
    }
}
