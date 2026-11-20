using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.SecretarialAudit;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.SecretarialAudit;

public class SecretarialAuditService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SecretarialAuditService(IAppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<SecretarialAuditSummaryDto> GetSummaryAsync(
        Guid? clientId,
        string? financialYear,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var q = _db.SecretarialAuditEngagements
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);

        var list = await q.ToListAsync(ct);

        int total = list.Count;
        int active = list.Count(x => x.AuditStatus != SecretarialAuditConstants.StatusCertified);
        int certified = list.Count(x => x.AuditStatus == SecretarialAuditConstants.StatusCertified);
        int mandatoryApplicable = list.Count(x => x.IsApplicable);
        int totalQualifications = list.Sum(x => x.QualificationsCount);
        int delayedFilings = list.Sum(x => x.FilingsDelayedCount);
        int meetingIrregularities = list.Sum(x => x.MeetingDeficienciesCount);
        decimal avgScore = total > 0 ? Math.Round(list.Average(x => x.OverallComplianceScore), 2) : 100m;

        return new SecretarialAuditSummaryDto(
            total,
            active,
            certified,
            mandatoryApplicable,
            totalQualifications,
            delayedFilings,
            meetingIrregularities,
            avgScore);
    }

    public ApplicabilityCheckResult CheckApplicability(ApplicabilityCheckRequest req)
    {
        var (isApplicable, reason, thresholds) = SecretarialAuditEngine.EvaluateApplicability(
            req.CompanyType,
            req.PaidUpShareCapital,
            req.Turnover,
            req.OutstandingBorrowings);

        return new ApplicabilityCheckResult(isApplicable, reason, thresholds);
    }

    // -------------------------------------------------------------
    // Engagements Management
    // -------------------------------------------------------------
    public async Task<List<SecretarialAuditEngagementDto>> ListEngagementsAsync(
        Guid? clientId,
        string? financialYear,
        string? status,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var q = _db.SecretarialAuditEngagements
            .Include(x => x.Client)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.AuditStatus == status);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToEngagementDto).ToList();
    }

    public async Task<SecretarialAuditEngagementDto?> GetEngagementByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var item = await _db.SecretarialAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        return item is null ? null : ToEngagementDto(item);
    }

    public async Task<SecretarialAuditEngagementDto> CreateOrUpdateEngagementAsync(
        Guid? id,
        SaveEngagementRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId && c.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Client not found or access denied.");

        SecretarialAuditEngagement eng;
        bool isNew = !id.HasValue;

        if (isNew)
        {
            eng = new SecretarialAuditEngagement
            {
                FirmId = firmId,
                ClientId = req.ClientId,
                AuditStatus = SecretarialAuditConstants.StatusDraft,
                CreatedAt = DateTime.UtcNow
            };
            _db.SecretarialAuditEngagements.Add(eng);
        }
        else
        {
            eng = await _db.SecretarialAuditEngagements
                .Include(x => x.ChecklistItems)
                .Include(x => x.McaFilings)
                .Include(x => x.MeetingAudits)
                .FirstOrDefaultAsync(x => x.Id == id!.Value && x.FirmId == firmId, ct)
                ?? throw new InvalidOperationException("Engagement not found.");
        }

        eng.ClientId = req.ClientId;
        eng.EngagementReference = req.EngagementReference;
        eng.Title = req.Title;
        eng.FinancialYear = req.FinancialYear;
        eng.AuditPeriodStart = req.AuditPeriodStart;
        eng.AuditPeriodEnd = req.AuditPeriodEnd;
        eng.Cin = req.Cin;
        eng.CompanyType = req.CompanyType;
        eng.PaidUpShareCapital = req.PaidUpShareCapital;
        eng.Turnover = req.Turnover;
        eng.OutstandingBorrowings = req.OutstandingBorrowings;

        // Evaluate applicability
        var (isApplicable, reason, _) = SecretarialAuditEngine.EvaluateApplicability(
            req.CompanyType,
            req.PaidUpShareCapital,
            req.Turnover,
            req.OutstandingBorrowings);
        eng.IsApplicable = isApplicable;
        eng.ApplicabilityReason = reason;

        // Auditor particulars
        eng.AuditorName = req.AuditorName;
        eng.AuditorFirmName = req.AuditorFirmName;
        eng.MembershipType = req.MembershipType;
        eng.MembershipNumber = req.MembershipNumber;
        eng.CertificateOfPracticeNumber = req.CertificateOfPracticeNumber;
        eng.PeerReviewCertificateNumber = req.PeerReviewCertificateNumber;
        eng.UpdatedAt = DateTime.UtcNow;

        // If new, seed baseline checklist
        if (isNew)
        {
            bool isListed = string.Equals(req.CompanyType, SecretarialAuditConstants.CompanyTypeListedPublic, StringComparison.OrdinalIgnoreCase);
            var checklist = SecretarialAuditEngine.SeedBaselineChecklist(eng.Id, firmId, isListed);
            eng.ChecklistItems.AddRange(checklist);
            _db.SecretarialChecklistItems.AddRange(checklist);
        }

        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(eng, client.LegalName ?? client.Name, "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        eng.Client = client;

        return ToEngagementDto(eng);
    }

    public async Task<bool> DeleteEngagementAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (eng is null) return false;

        _db.SecretarialAuditEngagements.Remove(eng);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SecretarialAuditEngagementDto> CertifyEngagementAsync(
        Guid id,
        CertifyEngagementRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Engagement not found.");

        if (string.IsNullOrWhiteSpace(req.Udin) || req.Udin.Trim().Length < 17)
        {
            throw new ArgumentException("A valid ICSI/ICAI UDIN of at least 17 characters is required for certification.");
        }

        eng.Udin = req.Udin.Trim().ToUpperInvariant();
        eng.CertifiedAt = DateTime.UtcNow;
        eng.CertifiedBy = req.CertifiedBy;
        eng.AuditStatus = SecretarialAuditConstants.StatusCertified;
        eng.UpdatedAt = DateTime.UtcNow;

        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.LegalName ?? eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return ToEngagementDto(eng);
    }

    public async Task<string> GenerateMR3ReportAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.Client)
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Engagement not found.");

        SecretarialAuditEngine.RecalculateSummary(eng);
        var report = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.LegalName ?? eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        eng.FormMR3ReportText = report;
        eng.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return report;
    }

    // -------------------------------------------------------------
    // Checklist Items
    // -------------------------------------------------------------
    public async Task<List<SecretarialChecklistItemDto>> ListChecklistItemsAsync(
        Guid engagementId,
        string? category,
        string? status,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var q = _db.SecretarialChecklistItems
            .Where(x => x.EngagementId == engagementId && x.FirmId == firmId);

        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(x => x.StatuteCategory == category);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.ComplianceStatus == status);

        var list = await q.OrderBy(x => x.StatuteCategory).ThenBy(x => x.ClauseCode).ToListAsync(ct);
        return list.Select(ToChecklistItemDto).ToList();
    }

    public async Task<SecretarialChecklistItemDto> CreateOrUpdateChecklistItemAsync(
        Guid engagementId,
        Guid? itemId,
        SaveChecklistItemRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Engagement not found.");

        SecretarialChecklistItem item;
        if (!itemId.HasValue)
        {
            item = new SecretarialChecklistItem
            {
                FirmId = firmId,
                EngagementId = engagementId,
                CreatedAt = DateTime.UtcNow
            };
            eng.ChecklistItems.Add(item);
            _db.SecretarialChecklistItems.Add(item);
        }
        else
        {
            item = eng.ChecklistItems.FirstOrDefault(x => x.Id == itemId.Value)
                ?? throw new InvalidOperationException("Checklist item not found.");
        }

        item.StatuteCategory = req.StatuteCategory;
        item.ClauseCode = req.ClauseCode;
        item.SubjectArea = req.SubjectArea;
        item.StatutoryRequirement = req.StatutoryRequirement;
        item.ComplianceStatus = req.ComplianceStatus;
        item.AuditorRemarks = req.AuditorRemarks;
        item.IsQualification = req.IsQualification;
        item.DeviationRisk = req.DeviationRisk;
        item.TargetCompletionDate = req.TargetCompletionDate;
        item.ActionOwner = req.ActionOwner;
        item.UpdatedAt = DateTime.UtcNow;

        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return ToChecklistItemDto(item);
    }

    public async Task<bool> DeleteChecklistItemAsync(Guid engagementId, Guid itemId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct);

        if (eng is null) return false;

        var item = eng.ChecklistItems.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return false;

        eng.ChecklistItems.Remove(item);
        _db.SecretarialChecklistItems.Remove(item);

        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> BatchUpdateChecklistAsync(
        Guid engagementId,
        BatchUpdateChecklistRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Engagement not found.");

        int updated = 0;
        foreach (var patch in req.Items)
        {
            var item = eng.ChecklistItems.FirstOrDefault(x => x.Id == patch.Id);
            if (item != null)
            {
                item.ComplianceStatus = patch.ComplianceStatus;
                item.AuditorRemarks = patch.AuditorRemarks;
                item.IsQualification = patch.IsQualification;
                item.DeviationRisk = patch.DeviationRisk;
                item.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return updated;
    }

    // -------------------------------------------------------------
    // MCA Form Filings
    // -------------------------------------------------------------
    public async Task<List<McaFormFilingAuditDto>> ListMcaFilingsAsync(Guid engagementId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var list = await _db.McaFormFilingAudits
            .Where(x => x.EngagementId == engagementId && x.FirmId == firmId)
            .OrderBy(x => x.EventDate)
            .ToListAsync(ct);

        return list.Select(ToMcaFilingDto).ToList();
    }

    public async Task<McaFormFilingAuditDto> CreateOrUpdateMcaFilingAsync(
        Guid engagementId,
        Guid? filingId,
        SaveMcaFilingRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Engagement not found.");

        McaFormFilingAudit item;
        if (!filingId.HasValue)
        {
            item = new McaFormFilingAudit
            {
                FirmId = firmId,
                EngagementId = engagementId,
                CreatedAt = DateTime.UtcNow
            };
            eng.McaFilings.Add(item);
            _db.McaFormFilingAudits.Add(item);
        }
        else
        {
            item = eng.McaFilings.FirstOrDefault(x => x.Id == filingId.Value)
                ?? throw new InvalidOperationException("Filing audit record not found.");
        }

        item.FormName = req.FormName;
        item.Description = req.Description;
        item.EventDate = req.EventDate;
        item.StatutoryDaysAllowed = req.StatutoryDaysAllowed;
        item.ActualFilingDate = req.ActualFilingDate;
        item.Srn = req.Srn;
        item.AdditionalFeePaid = req.AdditionalFeePaid;
        item.AuditorRemarks = req.AuditorRemarks;

        SecretarialAuditEngine.EvaluateMcaFiling(item);
        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return ToMcaFilingDto(item);
    }

    public async Task<bool> DeleteMcaFilingAsync(Guid engagementId, Guid filingId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct);

        if (eng is null) return false;

        var item = eng.McaFilings.FirstOrDefault(x => x.Id == filingId);
        if (item is null) return false;

        eng.McaFilings.Remove(item);
        _db.McaFormFilingAudits.Remove(item);

        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Board & Committee Meeting Audits
    // -------------------------------------------------------------
    public async Task<List<BoardMeetingAuditRecordDto>> ListMeetingAuditsAsync(Guid engagementId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var list = await _db.BoardMeetingAuditRecords
            .Where(x => x.EngagementId == engagementId && x.FirmId == firmId)
            .OrderBy(x => x.MeetingDate)
            .ToListAsync(ct);

        return list.Select(ToMeetingAuditDto).ToList();
    }

    public async Task<BoardMeetingAuditRecordDto> CreateOrUpdateMeetingAuditAsync(
        Guid engagementId,
        Guid? meetingId,
        SaveBoardMeetingAuditRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Engagement not found.");

        BoardMeetingAuditRecord item;
        if (!meetingId.HasValue)
        {
            item = new BoardMeetingAuditRecord
            {
                FirmId = firmId,
                EngagementId = engagementId,
                CreatedAt = DateTime.UtcNow
            };
            eng.MeetingAudits.Add(item);
            _db.BoardMeetingAuditRecords.Add(item);
        }
        else
        {
            item = eng.MeetingAudits.FirstOrDefault(x => x.Id == meetingId.Value)
                ?? throw new InvalidOperationException("Meeting audit record not found.");
        }

        item.MeetingType = req.MeetingType;
        item.MeetingDate = req.MeetingDate;
        item.PreviousMeetingDate = req.PreviousMeetingDate;
        item.NoticeDispatchDate = req.NoticeDispatchDate;
        item.TotalMembersCount = req.TotalMembersCount;
        item.AttendedMembersCount = req.AttendedMembersCount;
        item.IndependentDirectorsAttended = req.IndependentDirectorsAttended;
        item.SecretarialStandardComplied = req.SecretarialStandardComplied;
        item.AuditorRemarks = req.AuditorRemarks;

        SecretarialAuditEngine.EvaluateMeetingCompliance(item);
        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return ToMeetingAuditDto(item);
    }

    public async Task<bool> DeleteMeetingAuditAsync(Guid engagementId, Guid meetingId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var eng = await _db.SecretarialAuditEngagements
            .Include(x => x.ChecklistItems)
            .Include(x => x.McaFilings)
            .Include(x => x.MeetingAudits)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == engagementId && x.FirmId == firmId, ct);

        if (eng is null) return false;

        var item = eng.MeetingAudits.FirstOrDefault(x => x.Id == meetingId);
        if (item is null) return false;

        eng.MeetingAudits.Remove(item);
        _db.BoardMeetingAuditRecords.Remove(item);

        SecretarialAuditEngine.RecalculateSummary(eng);
        eng.FormMR3ReportText = SecretarialAuditEngine.GenerateFormMR3(
            eng,
            eng.Client?.Name ?? "The Company",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Helper Mappers
    // -------------------------------------------------------------
    private static SecretarialAuditEngagementDto ToEngagementDto(SecretarialAuditEngagement x) => new(
        x.Id,
        x.ClientId,
        x.Client?.Name ?? "",
        x.EngagementReference,
        x.Title,
        x.FinancialYear,
        x.AuditPeriodStart,
        x.AuditPeriodEnd,
        x.Cin,
        x.CompanyType,
        x.PaidUpShareCapital,
        x.Turnover,
        x.OutstandingBorrowings,
        x.IsApplicable,
        x.ApplicabilityReason,
        x.AuditStatus,
        x.AuditorName,
        x.AuditorFirmName,
        x.MembershipType,
        x.MembershipNumber,
        x.CertificateOfPracticeNumber,
        x.PeerReviewCertificateNumber,
        x.Udin,
        x.CertifiedAt,
        x.CertifiedBy,
        x.TotalChecklistCount,
        x.CompliedCount,
        x.NotCompliedCount,
        x.PartiallyCompliedCount,
        x.NotApplicableCount,
        x.QualificationsCount,
        x.OverallComplianceScore,
        x.TotalFilingsAudited,
        x.FilingsDelayedCount,
        x.TotalMeetingsAudited,
        x.MeetingDeficienciesCount,
        x.FormMR3ReportText,
        x.CreatedAt,
        x.UpdatedAt);

    private static SecretarialChecklistItemDto ToChecklistItemDto(SecretarialChecklistItem x) => new(
        x.Id,
        x.EngagementId,
        x.StatuteCategory,
        x.ClauseCode,
        x.SubjectArea,
        x.StatutoryRequirement,
        x.ComplianceStatus,
        x.AuditorRemarks,
        x.IsQualification,
        x.DeviationRisk,
        x.TargetCompletionDate,
        x.ActionOwner,
        x.CreatedAt,
        x.UpdatedAt);

    private static McaFormFilingAuditDto ToMcaFilingDto(McaFormFilingAudit x) => new(
        x.Id,
        x.EngagementId,
        x.FormName,
        x.Description,
        x.EventDate,
        x.StatutoryDaysAllowed,
        x.StatutoryDueDate,
        x.ActualFilingDate,
        x.Srn,
        x.FilingStatus,
        x.DelayDays,
        x.AdditionalFeePaid,
        x.AuditorRemarks,
        x.CreatedAt);

    private static BoardMeetingAuditRecordDto ToMeetingAuditDto(BoardMeetingAuditRecord x) => new(
        x.Id,
        x.EngagementId,
        x.MeetingType,
        x.MeetingDate,
        x.PreviousMeetingDate,
        x.GapFromPreviousMeetingDays,
        x.IsGapCompliant,
        x.NoticeDispatchDate,
        x.NoticeDaysGiven,
        x.IsNoticeCompliant,
        x.TotalMembersCount,
        x.AttendedMembersCount,
        x.QuorumRequiredCount,
        x.IsQuorumCompliant,
        x.IndependentDirectorsAttended,
        x.SecretarialStandardComplied,
        x.AuditorRemarks,
        x.CreatedAt);
}
