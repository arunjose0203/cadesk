using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.AuditCommittee;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.AuditCommittee;

public class AuditCommitteeService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public AuditCommitteeService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<AuditCommitteeSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var engagements = await _db.AuditCommitteeEngagements
            .AsNoTracking()
            .Include(e => e.Deficiencies)
            .Include(e => e.SignificantMatters)
            .Include(e => e.UncorrectedMisstatements)
            .ToListAsync(ct);

        int totalEngagements = engagements.Count;
        int packGenerated = engagements.Count(e => e.Status == AuditCommitteeEngagementStatus.PackGenerated || e.Status == AuditCommitteeEngagementStatus.Presented);
        int signedOff = engagements.Count(e => e.Status == AuditCommitteeEngagementStatus.SignedOff);

        var allDefs = engagements.SelectMany(e => e.Deficiencies).ToList();
        int totalDefs = allDefs.Count;
        int significantDefs = allDefs.Count(d => d.Severity == DeficiencySeverity.SignificantDeficiency);
        decimal totalRisk = allDefs.Where(d => d.EstimatedRiskAmount.HasValue).Sum(d => d.EstimatedRiskAmount!.Value);

        var allMatters = engagements.SelectMany(e => e.SignificantMatters).ToList();
        int totalMatters = allMatters.Count;

        var allMisstatements = engagements.SelectMany(e => e.UncorrectedMisstatements).ToList();
        int totalMis = allMisstatements.Count;
        decimal totalMisValue = allMisstatements.Sum(m => m.MisstatementAmount);

        return new AuditCommitteeSummaryDto(
            TotalEngagements: totalEngagements,
            PackGeneratedCount: packGenerated,
            SignedOffCount: signedOff,
            TotalDeficiencies: totalDefs,
            TotalSignificantDeficiencies: significantDefs,
            TotalEstimatedRiskAmount: totalRisk,
            TotalSignificantMatters: totalMatters,
            TotalUncorrectedMisstatements: totalMis,
            TotalUncorrectedMisstatementValue: totalMisValue
        );
    }

    public async Task<List<AuditCommitteeEngagementDto>> ListEngagementsAsync(
        Guid? clientId = null,
        string? fy = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = _db.AuditCommitteeEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.SignedOffByUser)
            .Include(e => e.Deficiencies)
            .Include(e => e.SignificantMatters)
            .Include(e => e.UncorrectedMisstatements)
            .AsNoTracking()
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(e => e.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(fy)) q = q.Where(e => e.FinancialYear == fy);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);

        var list = await q.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);
        return list.Select(ToEngagementDto).ToList();
    }

    public async Task<AuditCommitteeEngagementDto> GetEngagementByIdAsync(Guid id, CancellationToken ct = default)
    {
        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.SignedOffByUser)
            .Include(e => e.Deficiencies)
            .Include(e => e.SignificantMatters)
            .Include(e => e.UncorrectedMisstatements)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (engagement == null)
            throw new BusinessRuleException("Audit committee engagement not found.", 404);

        return ToEngagementDto(engagement);
    }

    public async Task<AuditCommitteeEngagementDto> CreateOrUpdateEngagementAsync(
        Guid? id,
        SaveCommitteeEngagementRequest req,
        CancellationToken ct = default)
    {
        if (req.ClientId == Guid.Empty)
            throw new RequestValidationException(nameof(req.ClientId), "Client organization is required.");
        if (string.IsNullOrWhiteSpace(req.FinancialYear))
            throw new RequestValidationException(nameof(req.FinancialYear), "Financial year is required (e.g. 2024-25).");
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new RequestValidationException(nameof(req.Title), "Engagement title is required.");

        var clientExists = await _db.Clients.AnyAsync(c => c.Id == req.ClientId, ct);
        if (!clientExists)
            throw new BusinessRuleException("Specified client organization does not exist.", 404);

        AuditCommitteeEngagement engagement;
        if (id.HasValue && id.Value != Guid.Empty)
        {
            engagement = await _db.AuditCommitteeEngagements
                .Include(e => e.Deficiencies)
                .Include(e => e.SignificantMatters)
                .Include(e => e.UncorrectedMisstatements)
                .FirstOrDefaultAsync(e => e.Id == id.Value, ct)
                ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

            if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
                throw new BusinessRuleException("Cannot modify an engagement that has already been signed off.", 400);

            engagement.ClientId = req.ClientId;
            engagement.AuditEngagementId = req.AuditEngagementId;
            engagement.FinancialYear = req.FinancialYear.Trim();
            engagement.Title = req.Title.Trim();
            engagement.MeetingType = req.MeetingType.Trim();
            engagement.MeetingDate = req.MeetingDate;
            engagement.MeetingLocation = req.MeetingLocation?.Trim();
            engagement.CommitteeChairName = req.CommitteeChairName?.Trim() ?? "";
            engagement.LeadPartnerName = req.LeadPartnerName?.Trim() ?? "";
            engagement.MeetingMinutes = req.MeetingMinutes;
            engagement.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            engagement = new AuditCommitteeEngagement
            {
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                AuditEngagementId = req.AuditEngagementId,
                FinancialYear = req.FinancialYear.Trim(),
                Title = req.Title.Trim(),
                MeetingType = req.MeetingType.Trim(),
                MeetingDate = req.MeetingDate,
                MeetingLocation = req.MeetingLocation?.Trim(),
                CommitteeChairName = req.CommitteeChairName?.Trim() ?? "",
                LeadPartnerName = req.LeadPartnerName?.Trim() ?? "",
                MeetingMinutes = req.MeetingMinutes,
                Status = AuditCommitteeEngagementStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            _db.AuditCommitteeEngagements.Add(engagement);
        }

        await _db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(engagement.Id, ct);
    }

    public async Task DeleteEngagementAsync(Guid id, CancellationToken ct = default)
    {
        var engagement = await _db.AuditCommitteeEngagements
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Cannot delete a signed off engagement.", 400);

        _db.AuditCommitteeEngagements.Remove(engagement);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<InternalControlDeficiencyDto> AddOrUpdateDeficiencyAsync(
        Guid engagementId,
        Guid? deficiencyId,
        SaveDeficiencyRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.DeficiencyCode))
            throw new RequestValidationException(nameof(req.DeficiencyCode), "Deficiency code is required (e.g. ICD-01).");
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new RequestValidationException(nameof(req.Title), "Deficiency title is required.");

        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.Deficiencies)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Cannot modify deficiencies on a signed off engagement.", 400);

        InternalControlDeficiency def;
        if (deficiencyId.HasValue && deficiencyId.Value != Guid.Empty)
        {
            def = engagement.Deficiencies.FirstOrDefault(d => d.Id == deficiencyId.Value)
                ?? throw new BusinessRuleException("Internal control deficiency not found.", 404);

            def.DeficiencyCode = req.DeficiencyCode.Trim().ToUpperInvariant();
            def.Title = req.Title.Trim();
            def.ProcessArea = req.ProcessArea.Trim();
            def.DeficiencyType = req.DeficiencyType.Trim();
            def.Severity = req.Severity.Trim();
            def.Description = req.Description?.Trim() ?? "";
            def.PotentialFinancialEffect = req.PotentialFinancialEffect?.Trim() ?? "";
            def.EstimatedRiskAmount = req.EstimatedRiskAmount;
            def.AuditorRecommendation = req.AuditorRecommendation?.Trim() ?? "";
            def.ManagementResponse = req.ManagementResponse?.Trim();
            def.RemediationTargetDate = req.RemediationTargetDate;
            def.Status = req.Status.Trim();
            def.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            def = new InternalControlDeficiency
            {
                FirmId = _tenant.FirmId,
                CommitteeEngagementId = engagementId,
                DeficiencyCode = req.DeficiencyCode.Trim().ToUpperInvariant(),
                Title = req.Title.Trim(),
                ProcessArea = req.ProcessArea.Trim(),
                DeficiencyType = req.DeficiencyType.Trim(),
                Severity = req.Severity.Trim(),
                Description = req.Description?.Trim() ?? "",
                PotentialFinancialEffect = req.PotentialFinancialEffect?.Trim() ?? "",
                EstimatedRiskAmount = req.EstimatedRiskAmount,
                AuditorRecommendation = req.AuditorRecommendation?.Trim() ?? "",
                ManagementResponse = req.ManagementResponse?.Trim(),
                RemediationTargetDate = req.RemediationTargetDate,
                Status = req.Status.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.InternalControlDeficiencies.Add(def);
            engagement.Deficiencies.Add(def);
        }

        await _db.SaveChangesAsync(ct);
        return ToDeficiencyDto(def);
    }

    public async Task DeleteDeficiencyAsync(Guid engagementId, Guid deficiencyId, CancellationToken ct = default)
    {
        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.Deficiencies)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Cannot modify deficiencies on a signed off engagement.", 400);

        var def = engagement.Deficiencies.FirstOrDefault(d => d.Id == deficiencyId)
            ?? throw new BusinessRuleException("Internal control deficiency not found.", 404);

        _db.InternalControlDeficiencies.Remove(def);
        engagement.Deficiencies.Remove(def);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SignificantAuditMatterDto> AddOrUpdateSignificantMatterAsync(
        Guid engagementId,
        Guid? matterId,
        SaveSignificantMatterRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.MatterCode))
            throw new RequestValidationException(nameof(req.MatterCode), "Matter code is required (e.g. SAM-01).");
        if (string.IsNullOrWhiteSpace(req.Subject))
            throw new RequestValidationException(nameof(req.Subject), "Subject is required.");

        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.SignificantMatters)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Cannot modify matters on a signed off engagement.", 400);

        SignificantAuditMatter matter;
        if (matterId.HasValue && matterId.Value != Guid.Empty)
        {
            matter = engagement.SignificantMatters.FirstOrDefault(m => m.Id == matterId.Value)
                ?? throw new BusinessRuleException("Significant audit matter not found.", 404);

            matter.MatterCode = req.MatterCode.Trim().ToUpperInvariant();
            matter.Subject = req.Subject.Trim();
            matter.Category = req.Category.Trim();
            matter.Details = req.Details?.Trim() ?? "";
            matter.AuditorAssessment = req.AuditorAssessment?.Trim() ?? "";
            matter.AuditCommitteeFeedback = req.AuditCommitteeFeedback?.Trim();
            matter.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            matter = new SignificantAuditMatter
            {
                FirmId = _tenant.FirmId,
                CommitteeEngagementId = engagementId,
                MatterCode = req.MatterCode.Trim().ToUpperInvariant(),
                Subject = req.Subject.Trim(),
                Category = req.Category.Trim(),
                Details = req.Details?.Trim() ?? "",
                AuditorAssessment = req.AuditorAssessment?.Trim() ?? "",
                AuditCommitteeFeedback = req.AuditCommitteeFeedback?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.SignificantAuditMatters.Add(matter);
            engagement.SignificantMatters.Add(matter);
        }

        await _db.SaveChangesAsync(ct);
        return ToSignificantMatterDto(matter);
    }

    public async Task DeleteSignificantMatterAsync(Guid engagementId, Guid matterId, CancellationToken ct = default)
    {
        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.SignificantMatters)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Cannot modify matters on a signed off engagement.", 400);

        var matter = engagement.SignificantMatters.FirstOrDefault(m => m.Id == matterId)
            ?? throw new BusinessRuleException("Significant audit matter not found.", 404);

        _db.SignificantAuditMatters.Remove(matter);
        engagement.SignificantMatters.Remove(matter);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UncorrectedMisstatementDto> AddOrUpdateMisstatementAsync(
        Guid engagementId,
        Guid? misstatementId,
        SaveUncorrectedMisstatementRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.AccountDescription))
            throw new RequestValidationException(nameof(req.AccountDescription), "Account description is required.");

        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.UncorrectedMisstatements)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Cannot modify misstatements on a signed off engagement.", 400);

        UncorrectedMisstatement mis;
        if (misstatementId.HasValue && misstatementId.Value != Guid.Empty)
        {
            mis = engagement.UncorrectedMisstatements.FirstOrDefault(m => m.Id == misstatementId.Value)
                ?? throw new BusinessRuleException("Uncorrected misstatement not found.", 404);

            mis.AccountDescription = req.AccountDescription.Trim();
            mis.MisstatementAmount = req.MisstatementAmount;
            mis.Nature = req.Nature.Trim();
            mis.IsMaterial = req.IsMaterial;
            mis.ManagementJustificationForNonAdjustment = req.ManagementJustificationForNonAdjustment?.Trim();
            mis.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            mis = new UncorrectedMisstatement
            {
                FirmId = _tenant.FirmId,
                CommitteeEngagementId = engagementId,
                AccountDescription = req.AccountDescription.Trim(),
                MisstatementAmount = req.MisstatementAmount,
                Nature = req.Nature.Trim(),
                IsMaterial = req.IsMaterial,
                ManagementJustificationForNonAdjustment = req.ManagementJustificationForNonAdjustment?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.UncorrectedMisstatements.Add(mis);
            engagement.UncorrectedMisstatements.Add(mis);
        }

        await _db.SaveChangesAsync(ct);
        return ToMisstatementDto(mis);
    }

    public async Task DeleteMisstatementAsync(Guid engagementId, Guid misstatementId, CancellationToken ct = default)
    {
        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.UncorrectedMisstatements)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Cannot modify misstatements on a signed off engagement.", 400);

        var mis = engagement.UncorrectedMisstatements.FirstOrDefault(m => m.Id == misstatementId)
            ?? throw new BusinessRuleException("Uncorrected misstatement not found.", 404);

        _db.UncorrectedMisstatements.Remove(mis);
        engagement.UncorrectedMisstatements.Remove(mis);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuditCommitteeEngagementDto> GenerateGovernancePackAsync(Guid engagementId, CancellationToken ct = default)
    {
        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.SignedOffByUser)
            .Include(e => e.Deficiencies)
            .Include(e => e.SignificantMatters)
            .Include(e => e.UncorrectedMisstatements)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        engagement.GeneratedSa265LetterText = AuditCommitteeEngine.GenerateSa265DeficiencyLetter(engagement, engagement.Deficiencies);
        engagement.GeneratedGovernancePackText = AuditCommitteeEngine.GenerateSa260GovernancePack(
            engagement,
            engagement.Deficiencies,
            engagement.SignificantMatters,
            engagement.UncorrectedMisstatements);

        if (engagement.Status == AuditCommitteeEngagementStatus.Draft)
        {
            engagement.Status = AuditCommitteeEngagementStatus.PackGenerated;
        }

        engagement.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToEngagementDto(engagement);
    }

    public async Task<AuditCommitteeEngagementDto> SignOffEngagementAsync(
        Guid engagementId,
        SignOffCommitteeEngagementRequest req,
        Guid userId,
        CancellationToken ct = default)
    {
        var engagement = await _db.AuditCommitteeEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.SignedOffByUser)
            .Include(e => e.Deficiencies)
            .Include(e => e.SignificantMatters)
            .Include(e => e.UncorrectedMisstatements)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Audit committee engagement not found.", 404);

        if (engagement.Status == AuditCommitteeEngagementStatus.SignedOff)
            throw new BusinessRuleException("Audit committee engagement has already been signed off.", 400);

        if (string.IsNullOrWhiteSpace(engagement.GeneratedGovernancePackText) || string.IsNullOrWhiteSpace(engagement.GeneratedSa265LetterText))
        {
            engagement.GeneratedSa265LetterText = AuditCommitteeEngine.GenerateSa265DeficiencyLetter(engagement, engagement.Deficiencies);
            engagement.GeneratedGovernancePackText = AuditCommitteeEngine.GenerateSa260GovernancePack(
                engagement,
                engagement.Deficiencies,
                engagement.SignificantMatters,
                engagement.UncorrectedMisstatements);
        }

        if (!string.IsNullOrWhiteSpace(req.Minutes))
        {
            engagement.MeetingMinutes = req.Minutes.Trim();
        }

        engagement.Status = AuditCommitteeEngagementStatus.SignedOff;
        engagement.SignedOffByUserId = userId;
        engagement.SignedOffAt = DateTime.UtcNow;
        engagement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToEngagementDto(engagement);
    }

    private static AuditCommitteeEngagementDto ToEngagementDto(AuditCommitteeEngagement e)
    {
        var defs = e.Deficiencies?.Select(ToDeficiencyDto).ToList() ?? new();
        var matters = e.SignificantMatters?.Select(ToSignificantMatterDto).ToList() ?? new();
        var misstatements = e.UncorrectedMisstatements?.Select(ToMisstatementDto).ToList() ?? new();

        return new AuditCommitteeEngagementDto(
            Id: e.Id,
            ClientId: e.ClientId,
            ClientName: e.Client?.Name,
            ClientPan: e.Client != null && e.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : e.Client?.Code,
            AuditEngagementId: e.AuditEngagementId,
            AuditEngagementTitle: e.AuditEngagement?.Title,
            FinancialYear: e.FinancialYear,
            Title: e.Title,
            MeetingType: e.MeetingType,
            MeetingDate: e.MeetingDate,
            MeetingLocation: e.MeetingLocation,
            CommitteeChairName: e.CommitteeChairName,
            LeadPartnerName: e.LeadPartnerName,
            Status: e.Status,
            MeetingMinutes: e.MeetingMinutes,
            GeneratedGovernancePackText: e.GeneratedGovernancePackText,
            GeneratedSa265LetterText: e.GeneratedSa265LetterText,
            SignedOffByUserId: e.SignedOffByUserId,
            SignedOffByUserName: e.SignedOffByUser?.Name,
            SignedOffAt: e.SignedOffAt,
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt,
            TotalDeficienciesCount: defs.Count,
            SignificantDeficienciesCount: defs.Count(d => d.Severity == DeficiencySeverity.SignificantDeficiency),
            TotalEstimatedRiskAmount: defs.Where(d => d.EstimatedRiskAmount.HasValue).Sum(d => d.EstimatedRiskAmount!.Value),
            SignificantMattersCount: matters.Count,
            UncorrectedMisstatementsCount: misstatements.Count,
            TotalUncorrectedMisstatementAmount: misstatements.Sum(m => m.MisstatementAmount),
            Deficiencies: defs,
            SignificantMatters: matters,
            UncorrectedMisstatements: misstatements
        );
    }

    private static InternalControlDeficiencyDto ToDeficiencyDto(InternalControlDeficiency d) =>
        new(
            Id: d.Id,
            CommitteeEngagementId: d.CommitteeEngagementId,
            DeficiencyCode: d.DeficiencyCode,
            Title: d.Title,
            ProcessArea: d.ProcessArea,
            DeficiencyType: d.DeficiencyType,
            Severity: d.Severity,
            Description: d.Description,
            PotentialFinancialEffect: d.PotentialFinancialEffect,
            EstimatedRiskAmount: d.EstimatedRiskAmount,
            AuditorRecommendation: d.AuditorRecommendation,
            ManagementResponse: d.ManagementResponse,
            RemediationTargetDate: d.RemediationTargetDate,
            Status: d.Status,
            CreatedAt: d.CreatedAt,
            UpdatedAt: d.UpdatedAt
        );

    private static SignificantAuditMatterDto ToSignificantMatterDto(SignificantAuditMatter m) =>
        new(
            Id: m.Id,
            CommitteeEngagementId: m.CommitteeEngagementId,
            MatterCode: m.MatterCode,
            Subject: m.Subject,
            Category: m.Category,
            Details: m.Details,
            AuditorAssessment: m.AuditorAssessment,
            AuditCommitteeFeedback: m.AuditCommitteeFeedback,
            CreatedAt: m.CreatedAt,
            UpdatedAt: m.UpdatedAt
        );

    private static UncorrectedMisstatementDto ToMisstatementDto(UncorrectedMisstatement u) =>
        new(
            Id: u.Id,
            CommitteeEngagementId: u.CommitteeEngagementId,
            AccountDescription: u.AccountDescription,
            MisstatementAmount: u.MisstatementAmount,
            Nature: u.Nature,
            IsMaterial: u.IsMaterial,
            ManagementJustificationForNonAdjustment: u.ManagementJustificationForNonAdjustment,
            CreatedAt: u.CreatedAt,
            UpdatedAt: u.UpdatedAt
        );
}
