using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.TaxAudit;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.TaxAudit;

public class Form3CdService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public Form3CdService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Form3CdSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var assessments = await _db.Form3CdAssessments
            .AsNoTracking()
            .ToListAsync(ct);

        int total = assessments.Count;
        int mandatory = assessments.Count(a => a.IsTaxAuditMandatory);
        int exempt = assessments.Count(a => !a.IsTaxAuditMandatory);
        int finalized = assessments.Count(a => a.AssessmentStatus == Form3CdStatus.Finalized);

        decimal totalDisallowances = assessments.Sum(a => a.TotalNetIncomeAdjustment);
        decimal total43Bh = assessments.Sum(a => a.TotalDisallowanceSec43Bh);
        decimal total269 = assessments.Sum(a => a.TotalSec269Violations);

        return new Form3CdSummaryDto(
            TotalAssessments: total,
            MandatoryCount: mandatory,
            ExemptCount: exempt,
            FinalizedCount: finalized,
            TotalNetDisallowances: totalDisallowances,
            Total43BhMsmeDisallowances: total43Bh,
            Total269Violations: total269
        );
    }

    public List<Form3CdClauseTemplate> GetStandardClausesTemplate()
    {
        return Form3CdEngine.GetStandardClausesTemplate();
    }

    public Form3CdApplicabilityEvaluation EvaluateApplicability(EvaluateApplicabilityRequest req)
    {
        return Form3CdEngine.EvaluateApplicability(
            entityType: req.EntityType,
            isBusiness: req.IsBusiness,
            turnoverOrGrossReceipts: req.TurnoverOrGrossReceipts,
            cashReceipts: req.CashReceipts,
            totalPayments: req.TotalPayments,
            cashPayments: req.CashPayments,
            isAuditedUnderOtherLaw: req.IsAuditedUnderOtherLaw,
            declaresLowerPresumptiveProfit: req.DeclaresLowerPresumptiveProfit,
            is44AdLockout: req.Is44AdLockout
        );
    }

    public async Task<List<Form3CdAssessmentDto>> ListAssessmentsAsync(
        Guid? clientId = null,
        string? fy = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = _db.Form3CdAssessments
            .Include(a => a.Client)
            .Include(a => a.AuditEngagement)
            .Include(a => a.FinalizedByUser)
            .Include(a => a.ClauseResponses)
            .AsNoTracking()
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(a => a.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(fy)) q = q.Where(a => a.FinancialYear == fy);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.AssessmentStatus == status);

        var list = await q.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<Form3CdAssessmentDto> GetAssessmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var assessment = await _db.Form3CdAssessments
            .Include(a => a.Client)
            .Include(a => a.AuditEngagement)
            .Include(a => a.FinalizedByUser)
            .Include(a => a.ClauseResponses)
                .ThenInclude(c => c.VerifiedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Form 3CD tax audit assessment {id} not found.");

        return ToDto(assessment);
    }

    public async Task<Form3CdAssessmentDto> CreateOrUpdateAssessmentAsync(
        Guid? id,
        SaveForm3CdAssessmentRequest req,
        CancellationToken ct = default)
    {
        Form3CdAssessment? assessment = null;
        bool isNew = !id.HasValue;

        if (id.HasValue)
        {
            assessment = await _db.Form3CdAssessments
                .Include(a => a.ClauseResponses)
                .FirstOrDefaultAsync(a => a.Id == id.Value, ct);

            if (assessment == null)
                throw new KeyNotFoundException($"Form 3CD assessment {id.Value} not found.");

            if (assessment.AssessmentStatus == Form3CdStatus.Finalized)
                throw new InvalidOperationException("Cannot modify a finalized Form 3CD tax audit assessment.");
        }
        else
        {
            assessment = new Form3CdAssessment
            {
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId
            };
            _db.Form3CdAssessments.Add(assessment);
        }

        // Evaluate applicability
        var eval = Form3CdEngine.EvaluateApplicability(
            entityType: req.Status,
            isBusiness: req.IsBusiness,
            turnoverOrGrossReceipts: req.TotalTurnoverOrGrossReceipts,
            cashReceipts: req.TotalCashReceipts,
            totalPayments: req.TotalPayments,
            cashPayments: req.TotalCashPayments,
            isAuditedUnderOtherLaw: req.IsAuditedUnderOtherLaw,
            declaresLowerPresumptiveProfit: req.DeclaresLowerPresumptiveProfit,
            is44AdLockout: req.Is44AdLockout
        );

        assessment.ClientId = req.ClientId;
        assessment.AuditEngagementId = req.AuditEngagementId;
        assessment.FinancialYear = req.FinancialYear;
        assessment.AssessmentYear = req.AssessmentYear;
        assessment.AuditType = !string.IsNullOrWhiteSpace(req.AuditType) ? req.AuditType : eval.RecommendedAuditType;
        assessment.Section44AbClause = !string.IsNullOrWhiteSpace(req.Section44AbClause) ? req.Section44AbClause : eval.ApplicableClause;
        assessment.TotalTurnoverOrGrossReceipts = req.TotalTurnoverOrGrossReceipts;
        assessment.TotalCashReceipts = req.TotalCashReceipts;
        assessment.TotalPayments = req.TotalPayments;
        assessment.TotalCashPayments = req.TotalCashPayments;
        assessment.CashReceiptPercentage = eval.CashReceiptPercent;
        assessment.CashPaymentPercentage = eval.CashPaymentPercent;
        assessment.IsTaxAuditMandatory = eval.IsMandatory;
        assessment.ApplicabilityReason = eval.Rationale;

        assessment.AssesseeName = req.AssesseeName;
        assessment.Pan = req.Pan;
        assessment.Address = req.Address;
        assessment.Gstin = req.Gstin;
        assessment.Status = req.Status;
        assessment.MethodOfAccounting = req.MethodOfAccounting;
        assessment.OptedSpecialTaxRegime = req.OptedSpecialTaxRegime;
        if (!string.IsNullOrWhiteSpace(req.AuditorObservations))
        {
            assessment.AuditorObservations = req.AuditorObservations;
        }
        assessment.UpdatedAt = DateTime.UtcNow;

        // If new, seed standard clauses
        if (isNew)
        {
            var templates = Form3CdEngine.GetStandardClausesTemplate();
            foreach (var tmpl in templates)
            {
                var clause = new Form3CdClauseResponse
                {
                    FirmId = _tenant.FirmId,
                    AssessmentId = assessment.Id,
                    ClauseNumber = tmpl.ClauseNumber,
                    ClauseTitle = tmpl.ClauseTitle,
                    Category = tmpl.Category,
                    Status = Form3CdClauseStatus.Clean,
                    ReportedAmount = 0m,
                    AuditorRemarks = $"Verified particulars against books and records. Compliant as per {tmpl.ClauseNumber} requirements.",
                    HasDiscrepancy = false
                };
                assessment.ClauseResponses.Add(clause);
                _db.Form3CdClauseResponses.Add(clause);
            }
        }

        Form3CdEngine.ComputeAssessmentTotals(assessment, assessment.ClauseResponses);
        assessment.GeneratedReportText = Form3CdEngine.GenerateTaxAuditReportDraft(assessment, assessment.ClauseResponses);

        await _db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(assessment.Id, ct);
    }

    public async Task<Form3CdAssessmentDto> UpdateClauseResponseAsync(
        Guid assessmentId,
        Guid clauseId,
        UpdateForm3CdClauseRequest req,
        CancellationToken ct = default)
    {
        var assessment = await _db.Form3CdAssessments
            .Include(a => a.ClauseResponses)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Form 3CD assessment {assessmentId} not found.");

        if (assessment.AssessmentStatus == Form3CdStatus.Finalized)
            throw new InvalidOperationException("Cannot modify clauses of a finalized Form 3CD tax audit assessment.");

        var clause = assessment.ClauseResponses.FirstOrDefault(c => c.Id == clauseId);
        if (clause == null)
            throw new KeyNotFoundException($"Clause response {clauseId} not found in assessment {assessmentId}.");

        clause.Status = req.Status;
        clause.ReportedAmount = req.ReportedAmount;
        clause.AuditorRemarks = req.AuditorRemarks;
        clause.HasDiscrepancy = req.HasDiscrepancy;
        clause.ParticularsJson = req.ParticularsJson;
        clause.WorkpaperRef = req.WorkpaperRef;
        clause.VerifiedByUserId = _tenant.UserId;
        clause.VerifiedAt = DateTime.UtcNow;
        clause.UpdatedAt = DateTime.UtcNow;

        if (assessment.AssessmentStatus == Form3CdStatus.Draft)
        {
            assessment.AssessmentStatus = Form3CdStatus.InReview;
        }

        Form3CdEngine.ComputeAssessmentTotals(assessment, assessment.ClauseResponses);
        assessment.GeneratedReportText = Form3CdEngine.GenerateTaxAuditReportDraft(assessment, assessment.ClauseResponses);
        assessment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(assessmentId, ct);
    }

    public async Task<string> GenerateReportTextAsync(Guid id, CancellationToken ct = default)
    {
        var assessment = await _db.Form3CdAssessments
            .Include(a => a.ClauseResponses)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Form 3CD assessment {id} not found.");

        Form3CdEngine.ComputeAssessmentTotals(assessment, assessment.ClauseResponses);
        var reportText = Form3CdEngine.GenerateTaxAuditReportDraft(assessment, assessment.ClauseResponses);
        assessment.GeneratedReportText = reportText;
        assessment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return reportText;
    }

    public async Task<Form3CdAssessmentDto> FinalizeAssessmentAsync(
        Guid id,
        string? auditorObservations,
        CancellationToken ct = default)
    {
        var assessment = await _db.Form3CdAssessments
            .Include(a => a.ClauseResponses)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assessment == null)
            throw new KeyNotFoundException($"Form 3CD assessment {id} not found.");

        if (assessment.AssessmentStatus == Form3CdStatus.Finalized)
            throw new InvalidOperationException("Assessment is already finalized.");

        if (!string.IsNullOrWhiteSpace(auditorObservations))
        {
            assessment.AuditorObservations = auditorObservations;
        }

        assessment.AssessmentStatus = Form3CdStatus.Finalized;
        assessment.FinalizedByUserId = _tenant.UserId;
        assessment.FinalizedAt = DateTime.UtcNow;
        assessment.UpdatedAt = DateTime.UtcNow;

        Form3CdEngine.ComputeAssessmentTotals(assessment, assessment.ClauseResponses);
        assessment.GeneratedReportText = Form3CdEngine.GenerateTaxAuditReportDraft(assessment, assessment.ClauseResponses);

        await _db.SaveChangesAsync(ct);
        return await GetAssessmentByIdAsync(id, ct);
    }

    private static Form3CdAssessmentDto ToDto(Form3CdAssessment a)
    {
        return new Form3CdAssessmentDto(
            Id: a.Id,
            ClientId: a.ClientId,
            ClientName: a.Client?.Name,
            AuditEngagementId: a.AuditEngagementId,
            AuditEngagementTitle: a.AuditEngagement?.Title,
            FinancialYear: a.FinancialYear,
            AssessmentYear: a.AssessmentYear,
            AuditType: a.AuditType,
            Section44AbClause: a.Section44AbClause,
            TotalTurnoverOrGrossReceipts: a.TotalTurnoverOrGrossReceipts,
            TotalCashReceipts: a.TotalCashReceipts,
            TotalPayments: a.TotalPayments,
            TotalCashPayments: a.TotalCashPayments,
            CashReceiptPercentage: a.CashReceiptPercentage,
            CashPaymentPercentage: a.CashPaymentPercentage,
            IsTaxAuditMandatory: a.IsTaxAuditMandatory,
            ApplicabilityReason: a.ApplicabilityReason,
            AssesseeName: a.AssesseeName,
            Pan: a.Pan,
            Address: a.Address,
            Gstin: a.Gstin,
            Status: a.Status,
            MethodOfAccounting: a.MethodOfAccounting,
            OptedSpecialTaxRegime: a.OptedSpecialTaxRegime,
            TotalDisallowanceSec40a: a.TotalDisallowanceSec40a,
            TotalDisallowanceSec40A3: a.TotalDisallowanceSec40A3,
            TotalDisallowanceSec43B: a.TotalDisallowanceSec43B,
            TotalDisallowanceSec43Bh: a.TotalDisallowanceSec43Bh,
            TotalDisallowanceSec361va: a.TotalDisallowanceSec361va,
            TotalSec269Violations: a.TotalSec269Violations,
            TotalIcdsNetAdjustment: a.TotalIcdsNetAdjustment,
            TotalNetIncomeAdjustment: a.TotalNetIncomeAdjustment,
            AssessmentStatus: a.AssessmentStatus,
            QualificationsCount: a.QualificationsCount,
            DiscrepanciesCount: a.DiscrepanciesCount,
            AuditorObservations: a.AuditorObservations,
            GeneratedReportText: a.GeneratedReportText,
            FinalizedByUserId: a.FinalizedByUserId,
            FinalizedByName: a.FinalizedByUser?.Name,
            FinalizedAt: a.FinalizedAt,
            CreatedAt: a.CreatedAt,
            UpdatedAt: a.UpdatedAt,
            ClauseResponses: a.ClauseResponses
                .OrderBy(c => c.ClauseNumber)
                .Select(c => new Form3CdClauseResponseDto(
                    Id: c.Id,
                    AssessmentId: c.AssessmentId,
                    ClauseNumber: c.ClauseNumber,
                    ClauseTitle: c.ClauseTitle,
                    Category: c.Category,
                    Status: c.Status,
                    ReportedAmount: c.ReportedAmount,
                    AuditorRemarks: c.AuditorRemarks,
                    HasDiscrepancy: c.HasDiscrepancy,
                    ParticularsJson: c.ParticularsJson,
                    WorkpaperRef: c.WorkpaperRef,
                    VerifiedByUserId: c.VerifiedByUserId,
                    VerifiedByName: c.VerifiedByUser?.Name,
                    VerifiedAt: c.VerifiedAt,
                    UpdatedAt: c.UpdatedAt
                )).ToList()
        );
    }
}
