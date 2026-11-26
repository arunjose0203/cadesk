using CaDesk.Application.Abstractions;
using CaDesk.Domain.TaxNotices;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.TaxNotices;

public sealed class TaxNoticeService(IAppDbContext db, ITenantContext tenant) : ITaxNoticeService
{
    public async Task<List<TaxNoticeDto>> GetNoticesAsync(TaxAuthority? authority, NoticeStatus? status, Guid? clientId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = db.TaxNotices
            .AsNoTracking()
            .Include(n => n.Client)
            .Include(n => n.Hearings)
            .Include(n => n.Responses)
            .AsQueryable();

        if (authority.HasValue) q = q.Where(n => n.Authority == authority.Value);
        if (status.HasValue) q = q.Where(n => n.Status == status.Value);
        if (clientId.HasValue) q = q.Where(n => n.ClientId == clientId.Value);

        var list = await q.OrderBy(n => n.DueDate).ToListAsync(ct);

        return list.Select(n => MapToDto(n, today)).ToList();
    }

    public async Task<TaxNoticeDto?> GetNoticeByIdAsync(Guid id, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var notice = await db.TaxNotices
            .AsNoTracking()
            .Include(n => n.Client)
            .Include(n => n.Hearings)
            .Include(n => n.Responses)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

        return notice is null ? null : MapToDto(notice, today);
    }

    public async Task<TaxNoticeDto> CreateNoticeAsync(CreateTaxNoticeRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client '{req.ClientId}' not found.");

        string? assignedName = null;
        if (req.AssignedToUserId.HasValue)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.AssignedToUserId.Value, ct);
            assignedName = user?.Name;
        }

        var dinStatus = DinValidator.EvaluateDin(req.Din, req.Authority, req.IssueDate);

        var notice = new TaxNotice
        {
            ClientId = req.ClientId,
            Authority = req.Authority,
            Category = req.Category,
            Section = req.Section.Trim(),
            NoticeNumber = req.NoticeNumber.Trim(),
            Din = string.IsNullOrWhiteSpace(req.Din) ? null : req.Din.Trim(),
            DinStatus = dinStatus,
            AssessmentYear = req.AssessmentYear?.Trim(),
            FinancialYear = req.FinancialYear.Trim(),
            IssueDate = req.IssueDate,
            DueDate = req.DueDate,
            DemandAmount = req.DemandAmount,
            RefundDisallowed = req.RefundDisallowed,
            Status = NoticeStatus.Received,
            Subject = req.Subject.Trim(),
            Description = req.Description?.Trim(),
            AssignedToUserId = req.AssignedToUserId,
            AssignedToName = assignedName,
            OfficerName = req.OfficerName?.Trim(),
            OfficerJurisdiction = req.OfficerJurisdiction?.Trim()
        };

        db.TaxNotices.Add(notice);

        // If a demand is raised in the notice, also register the tax demand for appeal countdown
        if (req.DemandAmount > 0)
        {
            var appealDueDate = AppealDeadlineCalculator.CalculateAppealDueDate(req.Authority, req.IssueDate);
            var demand = new TaxDemand
            {
                ClientId = req.ClientId,
                Notice = notice,
                AssessmentYear = req.AssessmentYear?.Trim(),
                FinancialYear = req.FinancialYear.Trim(),
                Section = req.Section.Trim(),
                OriginalDemand = req.DemandAmount,
                DemandDate = req.IssueDate,
                AppealDueDate = appealDueDate,
                AppealStatus = AppealStatus.NotAppealed,
                Remarks = $"Auto-generated from Notice {req.NoticeNumber}"
            };
            db.TaxDemands.Add(demand);
        }

        await db.SaveChangesAsync(ct);
        notice.Client = client;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return MapToDto(notice, today);
    }

    public async Task<TaxNoticeDto> UpdateNoticeAsync(Guid id, UpdateTaxNoticeRequest req, CancellationToken ct = default)
    {
        var notice = await db.TaxNotices
            .Include(n => n.Client)
            .Include(n => n.Hearings)
            .Include(n => n.Responses)
            .FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new KeyNotFoundException($"Tax notice '{id}' not found.");

        notice.Status = req.Status;
        notice.DueDate = req.DueDate;
        notice.DemandAmount = req.DemandAmount;
        notice.RefundDisallowed = req.RefundDisallowed;
        notice.Subject = req.Subject.Trim();
        notice.Description = req.Description?.Trim();
        notice.OfficerName = req.OfficerName?.Trim();
        notice.OfficerJurisdiction = req.OfficerJurisdiction?.Trim();

        if (req.Din != notice.Din)
        {
            notice.Din = string.IsNullOrWhiteSpace(req.Din) ? null : req.Din.Trim();
            notice.DinStatus = DinValidator.EvaluateDin(notice.Din, notice.Authority, notice.IssueDate);
        }

        if (req.AssignedToUserId != notice.AssignedToUserId)
        {
            notice.AssignedToUserId = req.AssignedToUserId;
            if (req.AssignedToUserId.HasValue)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.AssignedToUserId.Value, ct);
                notice.AssignedToName = user?.Name;
            }
            else
            {
                notice.AssignedToName = null;
            }
        }

        await db.SaveChangesAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return MapToDto(notice, today);
    }

    public async Task<TaxNoticeDto> VerifyDinAsync(Guid noticeId, CancellationToken ct = default)
    {
        var notice = await db.TaxNotices
            .Include(n => n.Client)
            .Include(n => n.Hearings)
            .Include(n => n.Responses)
            .FirstOrDefaultAsync(n => n.Id == noticeId, ct)
            ?? throw new KeyNotFoundException($"Tax notice '{noticeId}' not found.");

        notice.DinStatus = DinValidator.EvaluateDin(notice.Din, notice.Authority, notice.IssueDate);
        await db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return MapToDto(notice, today);
    }

    public async Task<List<NoticeHearingDto>> GetHearingsAsync(Guid? noticeId, HearingStatus? status, CancellationToken ct = default)
    {
        var q = db.NoticeHearings
            .AsNoTracking()
            .Include(h => h.Notice!).ThenInclude(n => n.Client)
            .AsQueryable();

        if (noticeId.HasValue) q = q.Where(h => h.NoticeId == noticeId.Value);
        if (status.HasValue) q = q.Where(h => h.Status == status.Value);

        var list = await q.OrderBy(h => h.HearingDate).ToListAsync(ct);

        return list.Select(h => new NoticeHearingDto(
            h.Id,
            h.NoticeId,
            h.Notice?.Section ?? "",
            h.Notice?.NoticeNumber ?? "",
            h.Notice?.Client?.Name ?? "",
            h.HearingDate,
            h.HearingType,
            h.OfficerName,
            h.OfficerDesignation,
            h.MeetingLink,
            h.Status,
            h.AdjournmentReason,
            h.SummaryNotes,
            h.AttendedByUserId,
            h.AttendedByName,
            h.CreatedAt
        )).ToList();
    }

    public async Task<NoticeHearingDto> AddHearingAsync(Guid noticeId, CreateHearingRequest req, CancellationToken ct = default)
    {
        var notice = await db.TaxNotices
            .Include(n => n.Client)
            .FirstOrDefaultAsync(n => n.Id == noticeId, ct)
            ?? throw new KeyNotFoundException($"Tax notice '{noticeId}' not found.");

        var hearing = new NoticeHearing
        {
            NoticeId = noticeId,
            HearingDate = req.HearingDate,
            HearingType = req.HearingType,
            OfficerName = req.OfficerName?.Trim(),
            OfficerDesignation = req.OfficerDesignation?.Trim(),
            MeetingLink = req.MeetingLink?.Trim(),
            Status = HearingStatus.Scheduled,
            SummaryNotes = req.SummaryNotes?.Trim()
        };

        db.NoticeHearings.Add(hearing);

        if (notice.Status == NoticeStatus.Received)
        {
            notice.Status = NoticeStatus.InReview;
        }

        await db.SaveChangesAsync(ct);

        return new NoticeHearingDto(
            hearing.Id,
            hearing.NoticeId,
            notice.Section,
            notice.NoticeNumber,
            notice.Client?.Name ?? "",
            hearing.HearingDate,
            hearing.HearingType,
            hearing.OfficerName,
            hearing.OfficerDesignation,
            hearing.MeetingLink,
            hearing.Status,
            hearing.AdjournmentReason,
            hearing.SummaryNotes,
            hearing.AttendedByUserId,
            hearing.AttendedByName,
            hearing.CreatedAt
        );
    }

    public async Task<NoticeHearingDto> UpdateHearingStatusAsync(Guid hearingId, UpdateHearingStatusRequest req, CancellationToken ct = default)
    {
        var hearing = await db.NoticeHearings
            .Include(h => h.Notice!).ThenInclude(n => n.Client)
            .FirstOrDefaultAsync(h => h.Id == hearingId, ct)
            ?? throw new KeyNotFoundException($"Hearing '{hearingId}' not found.");

        hearing.Status = req.Status;
        hearing.AdjournmentReason = req.AdjournmentReason?.Trim();
        if (!string.IsNullOrWhiteSpace(req.SummaryNotes))
        {
            hearing.SummaryNotes = req.SummaryNotes.Trim();
        }

        if (req.AttendedByUserId.HasValue)
        {
            hearing.AttendedByUserId = req.AttendedByUserId;
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.AttendedByUserId.Value, ct);
            hearing.AttendedByName = user?.Name;
        }

        await db.SaveChangesAsync(ct);

        return new NoticeHearingDto(
            hearing.Id,
            hearing.NoticeId,
            hearing.Notice?.Section ?? "",
            hearing.Notice?.NoticeNumber ?? "",
            hearing.Notice?.Client?.Name ?? "",
            hearing.HearingDate,
            hearing.HearingType,
            hearing.OfficerName,
            hearing.OfficerDesignation,
            hearing.MeetingLink,
            hearing.Status,
            hearing.AdjournmentReason,
            hearing.SummaryNotes,
            hearing.AttendedByUserId,
            hearing.AttendedByName,
            hearing.CreatedAt
        );
    }

    public async Task<List<NoticeResponseDto>> GetResponsesAsync(Guid noticeId, CancellationToken ct = default)
    {
        var list = await db.NoticeResponses
            .AsNoTracking()
            .Where(r => r.NoticeId == noticeId)
            .OrderByDescending(r => r.ResponseVersion)
            .ToListAsync(ct);

        return list.Select(r => new NoticeResponseDto(
            r.Id,
            r.NoticeId,
            r.ResponseVersion,
            r.SubmissionTitle,
            r.SummaryOfSubmissions,
            r.LegalGrounds,
            r.Status,
            r.PreparedByUserId,
            r.PreparedByName,
            r.ApprovedByUserId,
            r.ApprovedByName,
            r.ApprovedAt,
            r.PortalAckNumber,
            r.SubmittedAt,
            r.AttachmentNames,
            r.CreatedAt
        )).ToList();
    }

    public async Task<NoticeResponseDto> DraftResponseAsync(Guid noticeId, DraftResponseRequest req, CancellationToken ct = default)
    {
        var notice = await db.TaxNotices
            .FirstOrDefaultAsync(n => n.Id == noticeId, ct)
            ?? throw new KeyNotFoundException($"Tax notice '{noticeId}' not found.");

        var existingCount = await db.NoticeResponses.CountAsync(r => r.NoticeId == noticeId, ct);
        string? prepName = null;
        if (tenant.HasTenant && tenant.UserId != Guid.Empty)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == tenant.UserId, ct);
            prepName = user?.Name;
        }

        var response = new NoticeResponse
        {
            NoticeId = noticeId,
            ResponseVersion = existingCount + 1,
            SubmissionTitle = req.SubmissionTitle.Trim(),
            SummaryOfSubmissions = req.SummaryOfSubmissions.Trim(),
            LegalGrounds = req.LegalGrounds?.Trim(),
            AttachmentNames = req.AttachmentNames?.Trim(),
            Status = ResponseStatus.Draft,
            PreparedByUserId = tenant.HasTenant ? tenant.UserId : null,
            PreparedByName = prepName
        };

        db.NoticeResponses.Add(response);
        notice.Status = NoticeStatus.DraftPrepared;

        await db.SaveChangesAsync(ct);

        return new NoticeResponseDto(
            response.Id,
            response.NoticeId,
            response.ResponseVersion,
            response.SubmissionTitle,
            response.SummaryOfSubmissions,
            response.LegalGrounds,
            response.Status,
            response.PreparedByUserId,
            response.PreparedByName,
            response.ApprovedByUserId,
            response.ApprovedByName,
            response.ApprovedAt,
            response.PortalAckNumber,
            response.SubmittedAt,
            response.AttachmentNames,
            response.CreatedAt
        );
    }

    public async Task<NoticeResponseDto> ApproveResponseAsync(Guid responseId, CancellationToken ct = default)
    {
        var response = await db.NoticeResponses
            .Include(r => r.Notice)
            .FirstOrDefaultAsync(r => r.Id == responseId, ct)
            ?? throw new KeyNotFoundException($"Response '{responseId}' not found.");

        string? approverName = null;
        if (tenant.HasTenant && tenant.UserId != Guid.Empty)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == tenant.UserId, ct);
            approverName = user?.Name;
        }

        response.Status = ResponseStatus.PartnerApproved;
        response.ApprovedByUserId = tenant.HasTenant ? tenant.UserId : null;
        response.ApprovedByName = approverName;
        response.ApprovedAt = DateTimeOffset.UtcNow;

        if (response.Notice != null)
        {
            response.Notice.Status = NoticeStatus.PartnerApproved;
        }

        await db.SaveChangesAsync(ct);

        return new NoticeResponseDto(
            response.Id,
            response.NoticeId,
            response.ResponseVersion,
            response.SubmissionTitle,
            response.SummaryOfSubmissions,
            response.LegalGrounds,
            response.Status,
            response.PreparedByUserId,
            response.PreparedByName,
            response.ApprovedByUserId,
            response.ApprovedByName,
            response.ApprovedAt,
            response.PortalAckNumber,
            response.SubmittedAt,
            response.AttachmentNames,
            response.CreatedAt
        );
    }

    public async Task<NoticeResponseDto> RecordSubmissionAsync(Guid responseId, RecordSubmissionRequest req, CancellationToken ct = default)
    {
        var response = await db.NoticeResponses
            .Include(r => r.Notice)
            .FirstOrDefaultAsync(r => r.Id == responseId, ct)
            ?? throw new KeyNotFoundException($"Response '{responseId}' not found.");

        response.Status = ResponseStatus.UploadedToPortal;
        response.PortalAckNumber = req.PortalAckNumber.Trim();
        response.SubmittedAt = req.SubmittedAt ?? DateTimeOffset.UtcNow;

        if (response.Notice != null)
        {
            response.Notice.Status = NoticeStatus.Submitted;
        }

        await db.SaveChangesAsync(ct);

        return new NoticeResponseDto(
            response.Id,
            response.NoticeId,
            response.ResponseVersion,
            response.SubmissionTitle,
            response.SummaryOfSubmissions,
            response.LegalGrounds,
            response.Status,
            response.PreparedByUserId,
            response.PreparedByName,
            response.ApprovedByUserId,
            response.ApprovedByName,
            response.ApprovedAt,
            response.PortalAckNumber,
            response.SubmittedAt,
            response.AttachmentNames,
            response.CreatedAt
        );
    }

    public async Task<List<TaxDemandDto>> GetDemandsAsync(Guid? clientId, bool? overdueOnly, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = db.TaxDemands
            .AsNoTracking()
            .Include(d => d.Client)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(d => d.ClientId == clientId.Value);

        var list = await q.OrderBy(d => d.AppealDueDate).ToListAsync(ct);

        if (overdueOnly == true)
        {
            list = list.Where(d => d.DaysUntilAppealDue(today) < 0 && d.OutstandingAmount > 0).ToList();
        }

        return list.Select(d => new TaxDemandDto(
            d.Id,
            d.ClientId,
            d.Client?.Name ?? "",
            d.Client?.Code ?? "",
            d.NoticeId,
            d.AssessmentYear,
            d.FinancialYear,
            d.Section,
            d.OriginalDemand,
            d.StayedAmount,
            d.PaidAmount,
            d.AdjustedRefund,
            d.OutstandingAmount,
            d.DemandDate,
            d.AppealDueDate,
            d.DaysUntilAppealDue(today),
            d.AppealStatus,
            d.AppealAckNumber,
            d.Remarks
        )).ToList();
    }

    public async Task<TaxDemandDto> CreateDemandAsync(CreateTaxDemandRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client '{req.ClientId}' not found.");

        var appealDue = req.AppealDueDate ?? AppealDeadlineCalculator.CalculateAppealDueDate(TaxAuthority.IncomeTax, req.DemandDate);

        var demand = new TaxDemand
        {
            ClientId = req.ClientId,
            NoticeId = req.NoticeId,
            AssessmentYear = req.AssessmentYear?.Trim(),
            FinancialYear = req.FinancialYear.Trim(),
            Section = req.Section.Trim(),
            OriginalDemand = req.OriginalDemand,
            DemandDate = req.DemandDate,
            AppealDueDate = appealDue,
            AppealStatus = AppealStatus.NotAppealed,
            Remarks = req.Remarks?.Trim()
        };

        db.TaxDemands.Add(demand);
        await db.SaveChangesAsync(ct);
        demand.Client = client;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new TaxDemandDto(
            demand.Id,
            demand.ClientId,
            client.Name,
            client.Code,
            demand.NoticeId,
            demand.AssessmentYear,
            demand.FinancialYear,
            demand.Section,
            demand.OriginalDemand,
            demand.StayedAmount,
            demand.PaidAmount,
            demand.AdjustedRefund,
            demand.OutstandingAmount,
            demand.DemandDate,
            demand.AppealDueDate,
            demand.DaysUntilAppealDue(today),
            demand.AppealStatus,
            demand.AppealAckNumber,
            demand.Remarks
        );
    }

    public async Task<TaxDemandDto> RecordDemandPaymentAsync(Guid demandId, RecordDemandPaymentRequest req, CancellationToken ct = default)
    {
        var demand = await db.TaxDemands
            .Include(d => d.Client)
            .FirstOrDefaultAsync(d => d.Id == demandId, ct)
            ?? throw new KeyNotFoundException($"Tax demand '{demandId}' not found.");

        if (req.IsRefundAdjustment)
        {
            demand.AdjustedRefund += req.Amount;
        }
        else
        {
            demand.PaidAmount += req.Amount;
        }

        if (demand.OutstandingAmount == 0)
        {
            demand.AppealStatus = AppealStatus.Settled;
        }

        if (!string.IsNullOrWhiteSpace(req.Remarks))
        {
            demand.Remarks = string.IsNullOrWhiteSpace(demand.Remarks)
                ? req.Remarks.Trim()
                : $"{demand.Remarks}; {req.Remarks.Trim()}";
        }

        await db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new TaxDemandDto(
            demand.Id,
            demand.ClientId,
            demand.Client?.Name ?? "",
            demand.Client?.Code ?? "",
            demand.NoticeId,
            demand.AssessmentYear,
            demand.FinancialYear,
            demand.Section,
            demand.OriginalDemand,
            demand.StayedAmount,
            demand.PaidAmount,
            demand.AdjustedRefund,
            demand.OutstandingAmount,
            demand.DemandDate,
            demand.AppealDueDate,
            demand.DaysUntilAppealDue(today),
            demand.AppealStatus,
            demand.AppealAckNumber,
            demand.Remarks
        );
    }

    public async Task<TaxDemandDto> UpdateAppealStatusAsync(Guid demandId, UpdateAppealRequest req, CancellationToken ct = default)
    {
        var demand = await db.TaxDemands
            .Include(d => d.Client)
            .FirstOrDefaultAsync(d => d.Id == demandId, ct)
            ?? throw new KeyNotFoundException($"Tax demand '{demandId}' not found.");

        demand.AppealStatus = req.Status;
        if (!string.IsNullOrWhiteSpace(req.AppealAckNumber))
        {
            demand.AppealAckNumber = req.AppealAckNumber.Trim();
        }
        if (req.StayedAmount.HasValue)
        {
            demand.StayedAmount = req.StayedAmount.Value;
        }
        if (!string.IsNullOrWhiteSpace(req.Remarks))
        {
            demand.Remarks = string.IsNullOrWhiteSpace(demand.Remarks)
                ? req.Remarks.Trim()
                : $"{demand.Remarks}; {req.Remarks.Trim()}";
        }

        await db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new TaxDemandDto(
            demand.Id,
            demand.ClientId,
            demand.Client?.Name ?? "",
            demand.Client?.Code ?? "",
            demand.NoticeId,
            demand.AssessmentYear,
            demand.FinancialYear,
            demand.Section,
            demand.OriginalDemand,
            demand.StayedAmount,
            demand.PaidAmount,
            demand.AdjustedRefund,
            demand.OutstandingAmount,
            demand.DemandDate,
            demand.AppealDueDate,
            demand.DaysUntilAppealDue(today),
            demand.AppealStatus,
            demand.AppealAckNumber,
            demand.Remarks
        );
    }

    public async Task<NoticeDashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeNotices = await db.TaxNotices
            .AsNoTracking()
            .Where(n => n.Status != NoticeStatus.DisposedOrClosed)
            .ToListAsync(ct);

        var totalActive = activeNotices.Count;
        var incomeTax = activeNotices.Count(n => n.Authority == TaxAuthority.IncomeTax);
        var gst = activeNotices.Count(n => n.Authority == TaxAuthority.Gst);
        var invalidDin = activeNotices.Count(n => n.DinStatus == DinStatus.Invalid);

        var pendingHearings = await db.NoticeHearings
            .AsNoTracking()
            .CountAsync(h => h.Status == HearingStatus.Scheduled, ct);

        var pendingApprovals = await db.NoticeResponses
            .AsNoTracking()
            .CountAsync(r => r.Status == ResponseStatus.Draft || r.Status == ResponseStatus.UnderReview, ct);

        var demands = await db.TaxDemands
            .AsNoTracking()
            .ToListAsync(ct);

        var totalDemand = demands.Sum(d => d.OriginalDemand);
        var totalOutstanding = demands.Sum(d => d.OutstandingAmount);
        var appealsDueWithin30 = demands.Count(d =>
            d.OutstandingAmount > 0 &&
            d.DaysUntilAppealDue(today) >= 0 &&
            d.DaysUntilAppealDue(today) <= 30 &&
            d.AppealStatus == AppealStatus.NotAppealed);

        return new NoticeDashboardSummaryDto(
            totalActive,
            incomeTax,
            gst,
            pendingHearings,
            pendingApprovals,
            totalDemand,
            totalOutstanding,
            appealsDueWithin30,
            invalidDin
        );
    }

    private static TaxNoticeDto MapToDto(TaxNotice n, DateOnly today) =>
        new(
            n.Id,
            n.ClientId,
            n.Client?.Name ?? "",
            n.Client?.Code ?? "",
            n.Authority,
            n.Category,
            n.Section,
            n.NoticeNumber,
            n.Din,
            n.DinStatus,
            n.AssessmentYear,
            n.FinancialYear,
            n.IssueDate,
            n.DueDate,
            n.DueDate.DayNumber - today.DayNumber,
            n.DemandAmount,
            n.RefundDisallowed,
            n.Status,
            n.Subject,
            n.Description,
            n.AssignedToUserId,
            n.AssignedToName,
            n.OfficerName,
            n.OfficerJurisdiction,
            n.Hearings?.Count ?? 0,
            n.Responses?.Count ?? 0,
            n.CreatedAt
        );
}
