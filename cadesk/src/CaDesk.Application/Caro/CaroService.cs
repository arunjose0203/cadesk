using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Caro;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Caro;

public class CaroService(IAppDbContext db)
{
    public async Task<List<CaroAssessmentDto>> ListAssessmentsAsync(
        Guid? clientId = null,
        string? fy = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = db.CaroAssessments
            .Include(x => x.Client)
            .Include(x => x.Engagement)
            .Include(x => x.FinalizedByUser)
            .Include(x => x.ClauseResponses)
            .AsNoTracking()
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(fy) && fy != "all") q = q.Where(x => x.FinancialYear == fy);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(x => x.Status == status);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToAssessmentDto).ToList();
    }

    public async Task<CaroAssessmentDto> GetAssessmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var a = await db.CaroAssessments
            .Include(x => x.Client)
            .Include(x => x.Engagement)
            .Include(x => x.FinalizedByUser)
            .Include(x => x.ClauseResponses)
                .ThenInclude(c => c.VerifiedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (a is null) throw new BusinessRuleException("CARO assessment not found.", 404);
        return ToAssessmentDto(a);
    }

    public async Task<CaroAssessmentDto> CreateAssessmentAsync(
        SaveCaroAssessmentRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            req.CompanyType,
            req.IsHoldingOrSubsidiaryOfPublic,
            req.PaidUpCapitalAndReserves,
            req.TotalBorrowings,
            req.TotalRevenue
        );

        var assessment = new CaroAssessment
        {
            Id = Guid.NewGuid(),
            ClientId = req.ClientId,
            EngagementId = req.EngagementId,
            FinancialYear = req.FinancialYear,
            CompanyType = req.CompanyType,
            IsHoldingOrSubsidiaryOfPublic = req.IsHoldingOrSubsidiaryOfPublic,
            PaidUpCapitalAndReserves = req.PaidUpCapitalAndReserves,
            TotalBorrowings = req.TotalBorrowings,
            TotalRevenue = req.TotalRevenue,
            IsCaroApplicable = isApplicable,
            ExemptionReason = reason,
            AuditorConclusion = req.AuditorConclusion,
            Status = CaroAssessmentStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        // Populate standard 21 clause response templates
        var templates = CaroEngine.GetStandard21ClauseTemplates();
        foreach (var t in templates)
        {
            assessment.ClauseResponses.Add(new CaroClauseResponse
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessment.Id,
                ClauseNumber = t.ClauseNumber,
                ClauseTitle = t.Title,
                Category = t.Category,
                Status = isApplicable ? CaroClauseStatus.Compliant : CaroClauseStatus.NotApplicable,
                AuditorRemark = isApplicable ? t.StandardRemark : $"Clause 3({t.ClauseNumber}) not applicable.",
                HasDiscrepancy = false
            });
        }

        assessment.GeneratedAnnexureText = CaroEngine.GenerateAnnexureDraft(assessment, assessment.ClauseResponses);

        db.CaroAssessments.Add(assessment);
        await db.SaveChangesAsync(ct);

        return await GetAssessmentByIdAsync(assessment.Id, ct);
    }

    public async Task<CaroAssessmentDto> UpdateAssessmentAsync(
        Guid id,
        SaveCaroAssessmentRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var a = await db.CaroAssessments
            .Include(x => x.Client)
            .Include(x => x.ClauseResponses)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (a is null) throw new BusinessRuleException("CARO assessment not found.", 404);
        if (a.Status == CaroAssessmentStatus.Finalized)
            throw new BusinessRuleException("Finalized CARO assessment cannot be edited. Please reopen first.", 400);

        var (isApplicable, reason) = CaroEngine.EvaluateApplicability(
            req.CompanyType,
            req.IsHoldingOrSubsidiaryOfPublic,
            req.PaidUpCapitalAndReserves,
            req.TotalBorrowings,
            req.TotalRevenue
        );

        a.EngagementId = req.EngagementId;
        a.FinancialYear = req.FinancialYear;
        a.CompanyType = req.CompanyType;
        a.IsHoldingOrSubsidiaryOfPublic = req.IsHoldingOrSubsidiaryOfPublic;
        a.PaidUpCapitalAndReserves = req.PaidUpCapitalAndReserves;
        a.TotalBorrowings = req.TotalBorrowings;
        a.TotalRevenue = req.TotalRevenue;
        a.IsCaroApplicable = isApplicable;
        a.ExemptionReason = reason;
        a.AuditorConclusion = req.AuditorConclusion;
        a.UpdatedAt = DateTime.UtcNow;

        a.QualificationsCount = a.ClauseResponses.Count(c => c.Status == CaroClauseStatus.Qualified);
        a.AdverseRemarksCount = a.ClauseResponses.Count(c => c.Status == CaroClauseStatus.Adverse);
        a.GeneratedAnnexureText = CaroEngine.GenerateAnnexureDraft(a, a.ClauseResponses);

        await db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(a.Id, ct);
    }

    public async Task<CaroAssessmentDto> UpdateClauseResponseAsync(
        Guid assessmentId,
        Guid clauseId,
        UpdateClauseResponseRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var a = await db.CaroAssessments
            .Include(x => x.Client)
            .Include(x => x.ClauseResponses)
            .FirstOrDefaultAsync(x => x.Id == assessmentId, ct);

        if (a is null) throw new BusinessRuleException("CARO assessment not found.", 404);
        if (a.Status == CaroAssessmentStatus.Finalized)
            throw new BusinessRuleException("Finalized CARO assessment cannot be edited.", 400);

        var clause = a.ClauseResponses.FirstOrDefault(c => c.Id == clauseId);
        if (clause is null) throw new BusinessRuleException("CARO clause not found.", 404);

        clause.Status = req.Status;
        clause.AuditorRemark = req.AuditorRemark;
        clause.HasDiscrepancy = req.HasDiscrepancy;
        clause.DiscrepancyAmount = req.DiscrepancyAmount;
        clause.SupportingWpsRef = req.SupportingWpsRef;
        clause.VerifiedByUserId = currentUserId;
        clause.VerifiedAt = DateTime.UtcNow;
        clause.UpdatedAt = DateTime.UtcNow;

        a.QualificationsCount = a.ClauseResponses.Count(c => c.Status == CaroClauseStatus.Qualified);
        a.AdverseRemarksCount = a.ClauseResponses.Count(c => c.Status == CaroClauseStatus.Adverse);
        a.GeneratedAnnexureText = CaroEngine.GenerateAnnexureDraft(a, a.ClauseResponses);
        a.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(a.Id, ct);
    }

    public async Task<string> GenerateAnnexureAsync(Guid assessmentId, CancellationToken ct = default)
    {
        var a = await db.CaroAssessments
            .Include(x => x.Client)
            .Include(x => x.ClauseResponses)
            .FirstOrDefaultAsync(x => x.Id == assessmentId, ct);

        if (a is null) throw new BusinessRuleException("CARO assessment not found.", 404);

        var draft = CaroEngine.GenerateAnnexureDraft(a, a.ClauseResponses);
        a.GeneratedAnnexureText = draft;
        a.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task<CaroAssessmentDto> FinalizeAssessmentAsync(
        Guid assessmentId,
        string? conclusion,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var a = await db.CaroAssessments
            .Include(x => x.Client)
            .Include(x => x.ClauseResponses)
            .FirstOrDefaultAsync(x => x.Id == assessmentId, ct);

        if (a is null) throw new BusinessRuleException("CARO assessment not found.", 404);

        a.Status = CaroAssessmentStatus.Finalized;
        a.AuditorConclusion = conclusion ?? a.AuditorConclusion;
        a.FinalizedByUserId = currentUserId;
        a.FinalizedAt = DateTime.UtcNow;
        a.QualificationsCount = a.ClauseResponses.Count(c => c.Status == CaroClauseStatus.Qualified);
        a.AdverseRemarksCount = a.ClauseResponses.Count(c => c.Status == CaroClauseStatus.Adverse);
        a.GeneratedAnnexureText = CaroEngine.GenerateAnnexureDraft(a, a.ClauseResponses);
        a.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(a.Id, ct);
    }

    public async Task<CaroSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var list = await db.CaroAssessments
            .Include(x => x.ClauseResponses)
            .AsNoTracking()
            .ToListAsync(ct);

        var total = list.Count;
        var applicable = list.Count(x => x.IsCaroApplicable);
        var exempt = list.Count(x => !x.IsCaroApplicable);
        var finalized = list.Count(x => x.Status == CaroAssessmentStatus.Finalized);
        var totalQual = list.Sum(x => x.QualificationsCount);
        var totalAdv = list.Sum(x => x.AdverseRemarksCount);

        return new CaroSummaryDto(total, applicable, exempt, finalized, totalQual, totalAdv);
    }

    public List<CaroClauseTemplateDto> GetClauseTemplates()
    {
        return CaroEngine.GetStandard21ClauseTemplates()
            .Select(t => new CaroClauseTemplateDto(t.ClauseNumber, t.Title, t.Category, t.StandardRemark))
            .ToList();
    }

    private static CaroAssessmentDto ToAssessmentDto(CaroAssessment a)
    {
        var clauses = a.ClauseResponses?
            .OrderBy(x => x.ClauseNumber)
            .Select(c => new CaroClauseResponseDto(
                c.Id,
                c.AssessmentId,
                c.ClauseNumber,
                c.ClauseTitle,
                c.Category,
                c.Status,
                c.AuditorRemark,
                c.HasDiscrepancy,
                c.DiscrepancyAmount,
                c.SupportingWpsRef,
                c.VerifiedByUserId,
                c.VerifiedByUser?.Name,
                c.VerifiedAt,
                c.UpdatedAt
            )).ToList() ?? [];

        return new CaroAssessmentDto(
            a.Id,
            a.ClientId,
            a.Client?.Name,
            a.EngagementId,
            a.Engagement?.Title,
            a.FinancialYear,
            a.CompanyType,
            a.IsHoldingOrSubsidiaryOfPublic,
            a.PaidUpCapitalAndReserves,
            a.TotalBorrowings,
            a.TotalRevenue,
            a.IsCaroApplicable,
            a.ExemptionReason,
            a.Status,
            a.QualificationsCount,
            a.AdverseRemarksCount,
            a.GeneratedAnnexureText,
            a.AuditorConclusion,
            a.FinalizedByUserId,
            a.FinalizedByUser?.Name,
            a.FinalizedAt,
            a.CreatedAt,
            a.UpdatedAt,
            clauses
        );
    }
}
