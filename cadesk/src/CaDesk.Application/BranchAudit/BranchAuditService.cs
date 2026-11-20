using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.BranchAudit;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.BranchAudit;

public class BranchAuditService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public BranchAuditService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<BranchAuditSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var engagements = await _db.BranchAuditEngagements
            .AsNoTracking()
            .Include(e => e.Branches)
            .ToListAsync(ct);

        int totalEngagements = engagements.Count;
        int consolidated = engagements.Count(e => e.ConsolidationStatus == BranchAuditEngagementStatus.Consolidated);
        int finalized = engagements.Count(e => e.ConsolidationStatus == BranchAuditEngagementStatus.Finalized);

        var allBranches = engagements.SelectMany(e => e.Branches).ToList();
        int totalBranches = allBranches.Count;
        int submitted = allBranches.Count(b => b.Status == BranchOfficeStatus.SubmittedByBranch || b.Status == BranchOfficeStatus.UnderReview);
        int accepted = allBranches.Count(b => b.Status == BranchOfficeStatus.Accepted);
        int totalQualifications = allBranches.Count(b => b.HasQualifications || b.BranchReportType != BranchReportType.Clean);

        decimal totalAssetsAudited = allBranches.Where(b => b.Status == BranchOfficeStatus.SubmittedByBranch || b.Status == BranchOfficeStatus.Accepted).Sum(b => b.TotalAssets);
        decimal avgAssetsPct = engagements.Count > 0 ? Math.Round(engagements.Average(e => e.BranchAssetsPercentage), 2) : 0m;

        return new BranchAuditSummaryDto(
            TotalEngagements: totalEngagements,
            ConsolidatedCount: consolidated,
            FinalizedCount: finalized,
            TotalBranches: totalBranches,
            BranchesSubmitted: submitted,
            BranchesAccepted: accepted,
            TotalBranchQualifications: totalQualifications,
            TotalBranchAssetsAudited: totalAssetsAudited,
            AverageBranchAssetsPercentage: avgAssetsPct
        );
    }

    public async Task<List<BranchAuditEngagementDto>> ListEngagementsAsync(
        Guid? clientId = null,
        string? fy = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = _db.BranchAuditEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.FinalizedByUser)
            .Include(e => e.Branches)
                .ThenInclude(b => b.ReviewedByUser)
            .AsNoTracking()
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(e => e.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(fy)) q = q.Where(e => e.FinancialYear == fy);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.ConsolidationStatus == status);

        var list = await q.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);
        return list.Select(ToEngagementDto).ToList();
    }

    public async Task<BranchAuditEngagementDto> GetEngagementByIdAsync(Guid id, CancellationToken ct = default)
    {
        var engagement = await _db.BranchAuditEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.FinalizedByUser)
            .Include(e => e.Branches)
                .ThenInclude(b => b.ReviewedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (engagement == null)
            throw new BusinessRuleException("Branch audit engagement not found.", 404);

        return ToEngagementDto(engagement);
    }

    public async Task<BranchAuditEngagementDto> CreateOrUpdateEngagementAsync(
        Guid? id,
        SaveBranchEngagementRequest req,
        CancellationToken ct = default)
    {
        if (req.ClientId == Guid.Empty)
            throw new RequestValidationException(nameof(req.ClientId), "Client is required.");
        if (string.IsNullOrWhiteSpace(req.FinancialYear))
            throw new RequestValidationException(nameof(req.FinancialYear), "Financial year is required (e.g. 2024-25).");
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new RequestValidationException(nameof(req.Title), "Engagement title is required.");

        var clientExists = await _db.Clients.AnyAsync(c => c.Id == req.ClientId, ct);
        if (!clientExists)
            throw new BusinessRuleException("Specified client organization does not exist.", 404);

        BranchAuditEngagement engagement;
        if (id.HasValue && id.Value != Guid.Empty)
        {
            engagement = await _db.BranchAuditEngagements
                .Include(e => e.Branches)
                .FirstOrDefaultAsync(e => e.Id == id.Value, ct)
                ?? throw new BusinessRuleException("Branch audit engagement not found.", 404);

            if (engagement.ConsolidationStatus == BranchAuditEngagementStatus.Finalized)
                throw new BusinessRuleException("Cannot modify an engagement that has already been finalized.", 400);

            engagement.ClientId = req.ClientId;
            engagement.AuditEngagementId = req.AuditEngagementId;
            engagement.FinancialYear = req.FinancialYear.Trim();
            engagement.Title = req.Title.Trim();
            engagement.HeadOfficeAssets = req.HeadOfficeAssets;
            engagement.HeadOfficeLiabilities = req.HeadOfficeLiabilities;
            engagement.HeadOfficeRevenue = req.HeadOfficeRevenue;
            engagement.HeadOfficeExpenses = req.HeadOfficeExpenses;
            engagement.HeadOfficeNetProfit = req.HeadOfficeNetProfit;
            engagement.PrincipalAuditorNotes = req.PrincipalAuditorNotes;
            engagement.UpdatedAt = DateTime.UtcNow;

            // Recompute totals
            Sa600Engine.ComputeConsolidationTotals(engagement, engagement.Branches);
            if (engagement.ConsolidationStatus == BranchAuditEngagementStatus.Consolidated)
            {
                engagement.GeneratedSa600ReportText = Sa600Engine.GenerateSa600OtherMatterParagraph(engagement, engagement.Branches);
            }
        }
        else
        {
            engagement = new BranchAuditEngagement
            {
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                AuditEngagementId = req.AuditEngagementId,
                FinancialYear = req.FinancialYear.Trim(),
                Title = req.Title.Trim(),
                HeadOfficeAssets = req.HeadOfficeAssets,
                HeadOfficeLiabilities = req.HeadOfficeLiabilities,
                HeadOfficeRevenue = req.HeadOfficeRevenue,
                HeadOfficeExpenses = req.HeadOfficeExpenses,
                HeadOfficeNetProfit = req.HeadOfficeNetProfit,
                PrincipalAuditorNotes = req.PrincipalAuditorNotes,
                ConsolidationStatus = BranchAuditEngagementStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            Sa600Engine.ComputeConsolidationTotals(engagement, Array.Empty<BranchAuditOffice>());
            _db.BranchAuditEngagements.Add(engagement);
        }

        await _db.SaveChangesAsync(ct);
        return await GetEngagementByIdAsync(engagement.Id, ct);
    }

    public async Task DeleteEngagementAsync(Guid id, CancellationToken ct = default)
    {
        var engagement = await _db.BranchAuditEngagements
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new BusinessRuleException("Branch audit engagement not found.", 404);

        if (engagement.ConsolidationStatus == BranchAuditEngagementStatus.Finalized)
            throw new BusinessRuleException("Cannot delete a finalized engagement.", 400);

        _db.BranchAuditEngagements.Remove(engagement);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BranchAuditOfficeDto> AddOrUpdateBranchOfficeAsync(
        Guid engagementId,
        Guid? branchId,
        SaveBranchOfficeRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.BranchCode))
            throw new RequestValidationException(nameof(req.BranchCode), "Branch code is required (e.g. BR-01).");
        if (string.IsNullOrWhiteSpace(req.BranchName))
            throw new RequestValidationException(nameof(req.BranchName), "Branch name is required.");

        var engagement = await _db.BranchAuditEngagements
            .Include(e => e.Branches)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Branch audit engagement not found.", 404);

        if (engagement.ConsolidationStatus == BranchAuditEngagementStatus.Finalized)
            throw new BusinessRuleException("Cannot modify branches on a finalized engagement.", 400);

        BranchAuditOffice office;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            office = engagement.Branches.FirstOrDefault(b => b.Id == branchId.Value)
                ?? throw new BusinessRuleException("Branch office not found.", 404);

            office.BranchCode = req.BranchCode.Trim().ToUpperInvariant();
            office.BranchName = req.BranchName.Trim();
            office.BranchLocation = req.BranchLocation?.Trim() ?? "";
            office.IsForeignBranch = req.IsForeignBranch;
            office.AuditorFirmName = req.AuditorFirmName?.Trim() ?? "";
            office.AuditorFirmRegistrationNumber = req.AuditorFirmRegistrationNumber?.Trim() ?? "";
            office.AuditorName = req.AuditorName?.Trim() ?? "";
            office.AuditorMembershipNumber = req.AuditorMembershipNumber?.Trim() ?? "";
            office.AuditorEmail = req.AuditorEmail?.Trim() ?? "";
            office.AuditorPhone = req.AuditorPhone?.Trim();

            office.TotalAssets = req.TotalAssets;
            office.TotalLiabilities = req.TotalLiabilities;
            office.TotalRevenue = req.TotalRevenue;
            office.TotalExpenses = req.TotalExpenses;
            office.NetProfit = req.NetProfit;
            office.BranchReportType = req.BranchReportType;
            office.HasQualifications = req.HasQualifications;
            office.QualificationsSummary = req.QualificationsSummary;
            office.BranchAuditReportRef = req.BranchAuditReportRef;
            office.AccountingPoliciesConsistent = req.AccountingPoliciesConsistent;
            office.RelatedPartyTransactionsDisclosed = req.RelatedPartyTransactionsDisclosed;
            office.ContingentLiabilitiesReported = req.ContingentLiabilitiesReported;
            office.SubsequentEventsReported = req.SubsequentEventsReported;
            office.FraudOrIrregularitiesReported = req.FraudOrIrregularitiesReported;
            office.InternalControlsCommensurate = req.InternalControlsCommensurate;
            office.CaroApplicableToBranch = req.CaroApplicableToBranch;
            office.CaroReportSummary = req.CaroReportSummary;
            office.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            office = new BranchAuditOffice
            {
                FirmId = _tenant.FirmId,
                EngagementId = engagementId,
                BranchCode = req.BranchCode.Trim().ToUpperInvariant(),
                BranchName = req.BranchName.Trim(),
                BranchLocation = req.BranchLocation?.Trim() ?? "",
                IsForeignBranch = req.IsForeignBranch,
                AuditorFirmName = req.AuditorFirmName?.Trim() ?? "",
                AuditorFirmRegistrationNumber = req.AuditorFirmRegistrationNumber?.Trim() ?? "",
                AuditorName = req.AuditorName?.Trim() ?? "",
                AuditorMembershipNumber = req.AuditorMembershipNumber?.Trim() ?? "",
                AuditorEmail = req.AuditorEmail?.Trim() ?? "",
                AuditorPhone = req.AuditorPhone?.Trim(),
                TotalAssets = req.TotalAssets,
                TotalLiabilities = req.TotalLiabilities,
                TotalRevenue = req.TotalRevenue,
                TotalExpenses = req.TotalExpenses,
                NetProfit = req.NetProfit,
                BranchReportType = req.BranchReportType,
                HasQualifications = req.HasQualifications,
                QualificationsSummary = req.QualificationsSummary,
                BranchAuditReportRef = req.BranchAuditReportRef,
                AccountingPoliciesConsistent = req.AccountingPoliciesConsistent,
                RelatedPartyTransactionsDisclosed = req.RelatedPartyTransactionsDisclosed,
                ContingentLiabilitiesReported = req.ContingentLiabilitiesReported,
                SubsequentEventsReported = req.SubsequentEventsReported,
                FraudOrIrregularitiesReported = req.FraudOrIrregularitiesReported,
                InternalControlsCommensurate = req.InternalControlsCommensurate,
                CaroApplicableToBranch = req.CaroApplicableToBranch,
                CaroReportSummary = req.CaroReportSummary,
                Status = BranchOfficeStatus.Assigned,
                CreatedAt = DateTime.UtcNow
            };
            _db.BranchAuditOffices.Add(office);
            engagement.Branches.Add(office);
        }

        Sa600Engine.ComputeConsolidationTotals(engagement, engagement.Branches);
        await _db.SaveChangesAsync(ct);

        return ToBranchDto(office);
    }

    public async Task DeleteBranchOfficeAsync(Guid engagementId, Guid branchId, CancellationToken ct = default)
    {
        var engagement = await _db.BranchAuditEngagements
            .Include(e => e.Branches)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Branch audit engagement not found.", 404);

        if (engagement.ConsolidationStatus == BranchAuditEngagementStatus.Finalized)
            throw new BusinessRuleException("Cannot modify branches on a finalized engagement.", 400);

        var office = engagement.Branches.FirstOrDefault(b => b.Id == branchId)
            ?? throw new BusinessRuleException("Branch office not found.", 404);

        _db.BranchAuditOffices.Remove(office);
        engagement.Branches.Remove(office);

        Sa600Engine.ComputeConsolidationTotals(engagement, engagement.Branches);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<GenerateTokenResponse> GenerateBranchAccessTokenAsync(
        Guid engagementId,
        Guid branchId,
        CancellationToken ct = default)
    {
        var office = await _db.BranchAuditOffices
            .FirstOrDefaultAsync(b => b.EngagementId == engagementId && b.Id == branchId, ct)
            ?? throw new BusinessRuleException("Branch office not found.", 404);

        var rawToken = Sa600Engine.GenerateBranchAccessToken();
        office.AccessTokenHash = Sa600Engine.HashToken(rawToken);
        office.TokenExpiresAt = DateTime.UtcNow.AddDays(30);

        if (office.Status == BranchOfficeStatus.Assigned)
        {
            office.Status = BranchOfficeStatus.PackageSent;
        }

        office.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new GenerateTokenResponse(
            BranchId: office.Id,
            RawToken: rawToken,
            ExpiresAt: office.TokenExpiresAt.Value,
            PortalUrl: $"/branch-portal?token={rawToken}"
        );
    }

    public async Task<BranchAuditOfficeDto> GetBranchPortalInfoByTokenAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new RequestValidationException(nameof(rawToken), "Access token is required.");

        var tokenHash = Sa600Engine.HashToken(rawToken.Trim());

        var office = await _db.BranchAuditOffices
            .IgnoreQueryFilters()
            .Include(b => b.ReviewedByUser)
            .FirstOrDefaultAsync(b => b.AccessTokenHash == tokenHash, ct);

        if (office == null)
            throw new BusinessRuleException("Invalid branch auditor access token.", 401);

        if (office.TokenExpiresAt.HasValue && office.TokenExpiresAt.Value < DateTime.UtcNow)
            throw new BusinessRuleException("This branch auditor access token has expired.", 403);

        return ToBranchDto(office);
    }

    public async Task<BranchAuditOfficeDto> SubmitBranchReturnsExternalAsync(
        SubmitBranchReturnsRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            throw new RequestValidationException(nameof(req.Token), "Access token is required.");

        var tokenHash = Sa600Engine.HashToken(req.Token.Trim());

        var office = await _db.BranchAuditOffices
            .IgnoreQueryFilters()
            .Include(b => b.Engagement)
            .FirstOrDefaultAsync(b => b.AccessTokenHash == tokenHash, ct);

        if (office == null)
            throw new BusinessRuleException("Invalid branch auditor access token.", 401);

        if (office.TokenExpiresAt.HasValue && office.TokenExpiresAt.Value < DateTime.UtcNow)
            throw new BusinessRuleException("This branch auditor access token has expired.", 403);

        // Set tenant context if not set so RLS & audit stamping match
        if (_tenant is ITenantSetter setter && !_tenant.HasTenant)
        {
            setter.Set(office.FirmId, null, "BranchAuditor");
        }

        office.TotalAssets = req.TotalAssets;
        office.TotalLiabilities = req.TotalLiabilities;
        office.TotalRevenue = req.TotalRevenue;
        office.TotalExpenses = req.TotalExpenses;
        office.NetProfit = req.NetProfit;
        office.BranchReportType = req.BranchReportType;
        office.HasQualifications = req.HasQualifications;
        office.QualificationsSummary = req.QualificationsSummary;
        office.BranchAuditReportRef = req.BranchAuditReportRef;
        office.AccountingPoliciesConsistent = req.AccountingPoliciesConsistent;
        office.RelatedPartyTransactionsDisclosed = req.RelatedPartyTransactionsDisclosed;
        office.ContingentLiabilitiesReported = req.ContingentLiabilitiesReported;
        office.SubsequentEventsReported = req.SubsequentEventsReported;
        office.FraudOrIrregularitiesReported = req.FraudOrIrregularitiesReported;
        office.InternalControlsCommensurate = req.InternalControlsCommensurate;
        office.CaroApplicableToBranch = req.CaroApplicableToBranch;
        office.CaroReportSummary = req.CaroReportSummary;
        office.Status = BranchOfficeStatus.SubmittedByBranch;
        office.LastSubmittedAt = DateTime.UtcNow;
        office.UpdatedAt = DateTime.UtcNow;

        if (office.Engagement != null)
        {
            if (office.Engagement.ConsolidationStatus == BranchAuditEngagementStatus.Draft)
            {
                office.Engagement.ConsolidationStatus = BranchAuditEngagementStatus.BranchesInProgress;
            }

            // Reload all branches for the engagement to recompute
            var branches = await _db.BranchAuditOffices
                .IgnoreQueryFilters()
                .Where(b => b.EngagementId == office.EngagementId)
                .ToListAsync(ct);

            Sa600Engine.ComputeConsolidationTotals(office.Engagement, branches);
        }

        await _db.SaveChangesAsync(ct);
        return ToBranchDto(office);
    }

    public async Task<BranchAuditOfficeDto> ReviewBranchOfficeAsync(
        Guid engagementId,
        Guid branchId,
        ReviewBranchOfficeRequest req,
        Guid reviewerUserId,
        CancellationToken ct = default)
    {
        var office = await _db.BranchAuditOffices
            .Include(b => b.Engagement)
            .FirstOrDefaultAsync(b => b.EngagementId == engagementId && b.Id == branchId, ct)
            ?? throw new BusinessRuleException("Branch office not found.", 404);

        if (req.Decision.Equals("accept", StringComparison.OrdinalIgnoreCase))
        {
            office.Status = BranchOfficeStatus.Accepted;
        }
        else if (req.Decision.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            office.Status = BranchOfficeStatus.Rejected;
        }
        else
        {
            throw new RequestValidationException(nameof(req.Decision), "Decision must be 'accept' or 'reject'.");
        }

        office.PrincipalAuditorReviewNotes = req.ReviewNotes;
        office.ReviewedByUserId = reviewerUserId;
        office.ReviewedAt = DateTime.UtcNow;
        office.UpdatedAt = DateTime.UtcNow;

        if (office.Engagement != null)
        {
            var branches = await _db.BranchAuditOffices
                .Where(b => b.EngagementId == engagementId)
                .ToListAsync(ct);

            Sa600Engine.ComputeConsolidationTotals(office.Engagement, branches);
        }

        await _db.SaveChangesAsync(ct);
        return ToBranchDto(office);
    }

    public async Task<BranchAuditEngagementDto> ConsolidateEngagementAsync(Guid engagementId, CancellationToken ct = default)
    {
        var engagement = await _db.BranchAuditEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.FinalizedByUser)
            .Include(e => e.Branches)
                .ThenInclude(b => b.ReviewedByUser)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Branch audit engagement not found.", 404);

        if (engagement.ConsolidationStatus == BranchAuditEngagementStatus.Finalized)
            throw new BusinessRuleException("Cannot re-consolidate a finalized engagement.", 400);

        Sa600Engine.ComputeConsolidationTotals(engagement, engagement.Branches);
        engagement.GeneratedSa600ReportText = Sa600Engine.GenerateSa600OtherMatterParagraph(engagement, engagement.Branches);
        engagement.ConsolidationStatus = BranchAuditEngagementStatus.Consolidated;
        engagement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToEngagementDto(engagement);
    }

    public async Task<BranchAuditEngagementDto> FinalizeEngagementAsync(
        Guid engagementId,
        Guid userId,
        CancellationToken ct = default)
    {
        var engagement = await _db.BranchAuditEngagements
            .Include(e => e.Client)
            .Include(e => e.AuditEngagement)
            .Include(e => e.FinalizedByUser)
            .Include(e => e.Branches)
                .ThenInclude(b => b.ReviewedByUser)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct)
            ?? throw new BusinessRuleException("Branch audit engagement not found.", 404);

        if (engagement.ConsolidationStatus == BranchAuditEngagementStatus.Finalized)
            throw new BusinessRuleException("Engagement is already finalized.", 400);

        // Check if any branches are in submitted or under review state (pending auditor review)
        bool hasPendingReview = engagement.Branches.Any(b => b.Status == BranchOfficeStatus.SubmittedByBranch || b.Status == BranchOfficeStatus.UnderReview);
        if (hasPendingReview)
            throw new BusinessRuleException("Cannot finalize engagement: one or more branches are awaiting principal auditor review.", 400);

        Sa600Engine.ComputeConsolidationTotals(engagement, engagement.Branches);
        engagement.GeneratedSa600ReportText = Sa600Engine.GenerateSa600OtherMatterParagraph(engagement, engagement.Branches);
        engagement.ConsolidationStatus = BranchAuditEngagementStatus.Finalized;
        engagement.FinalizedByUserId = userId;
        engagement.FinalizedAt = DateTime.UtcNow;
        engagement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToEngagementDto(engagement);
    }

    private static BranchAuditEngagementDto ToEngagementDto(BranchAuditEngagement e) =>
        new(
            Id: e.Id,
            ClientId: e.ClientId,
            ClientName: e.Client?.Name,
            ClientPan: e.Client != null && e.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : e.Client?.Code,
            AuditEngagementId: e.AuditEngagementId,
            AuditEngagementTitle: e.AuditEngagement?.Title,
            FinancialYear: e.FinancialYear,
            Title: e.Title,
            HeadOfficeAssets: e.HeadOfficeAssets,
            HeadOfficeLiabilities: e.HeadOfficeLiabilities,
            HeadOfficeRevenue: e.HeadOfficeRevenue,
            HeadOfficeExpenses: e.HeadOfficeExpenses,
            HeadOfficeNetProfit: e.HeadOfficeNetProfit,
            TotalConsolidatedAssets: e.TotalConsolidatedAssets,
            TotalConsolidatedLiabilities: e.TotalConsolidatedLiabilities,
            TotalConsolidatedRevenue: e.TotalConsolidatedRevenue,
            TotalConsolidatedExpenses: e.TotalConsolidatedExpenses,
            TotalConsolidatedNetProfit: e.TotalConsolidatedNetProfit,
            BranchesAuditedCount: e.BranchesAuditedCount,
            BranchAuditorsCount: e.BranchAuditorsCount,
            TotalBranchAssets: e.TotalBranchAssets,
            BranchAssetsPercentage: e.BranchAssetsPercentage,
            TotalBranchRevenue: e.TotalBranchRevenue,
            BranchRevenuePercentage: e.BranchRevenuePercentage,
            TotalBranchNetProfit: e.TotalBranchNetProfit,
            BranchNetProfitPercentage: e.BranchNetProfitPercentage,
            ConsolidationStatus: e.ConsolidationStatus,
            BranchQualificationsCount: e.BranchQualificationsCount,
            PrincipalAuditorNotes: e.PrincipalAuditorNotes,
            GeneratedSa600ReportText: e.GeneratedSa600ReportText,
            FinalizedByUserId: e.FinalizedByUserId,
            FinalizedByUserName: e.FinalizedByUser?.Name,
            FinalizedAt: e.FinalizedAt,
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt,
            Branches: e.Branches?.Select(ToBranchDto).ToList() ?? new()
        );

    private static BranchAuditOfficeDto ToBranchDto(BranchAuditOffice b) =>
        new(
            Id: b.Id,
            EngagementId: b.EngagementId,
            BranchCode: b.BranchCode,
            BranchName: b.BranchName,
            BranchLocation: b.BranchLocation,
            IsForeignBranch: b.IsForeignBranch,
            AuditorFirmName: b.AuditorFirmName,
            AuditorFirmRegistrationNumber: b.AuditorFirmRegistrationNumber,
            AuditorName: b.AuditorName,
            AuditorMembershipNumber: b.AuditorMembershipNumber,
            AuditorEmail: b.AuditorEmail,
            AuditorPhone: b.AuditorPhone,
            HasActiveToken: !string.IsNullOrWhiteSpace(b.AccessTokenHash) && b.TokenExpiresAt.HasValue && b.TokenExpiresAt.Value > DateTime.UtcNow,
            TokenExpiresAt: b.TokenExpiresAt,
            LastSubmittedAt: b.LastSubmittedAt,
            TotalAssets: b.TotalAssets,
            TotalLiabilities: b.TotalLiabilities,
            TotalRevenue: b.TotalRevenue,
            TotalExpenses: b.TotalExpenses,
            NetProfit: b.NetProfit,
            BranchReportType: b.BranchReportType,
            HasQualifications: b.HasQualifications,
            QualificationsSummary: b.QualificationsSummary,
            BranchAuditReportRef: b.BranchAuditReportRef,
            AccountingPoliciesConsistent: b.AccountingPoliciesConsistent,
            RelatedPartyTransactionsDisclosed: b.RelatedPartyTransactionsDisclosed,
            ContingentLiabilitiesReported: b.ContingentLiabilitiesReported,
            SubsequentEventsReported: b.SubsequentEventsReported,
            FraudOrIrregularitiesReported: b.FraudOrIrregularitiesReported,
            InternalControlsCommensurate: b.InternalControlsCommensurate,
            CaroApplicableToBranch: b.CaroApplicableToBranch,
            CaroReportSummary: b.CaroReportSummary,
            Status: b.Status,
            PrincipalAuditorReviewNotes: b.PrincipalAuditorReviewNotes,
            ReviewedByUserId: b.ReviewedByUserId,
            ReviewedByUserName: b.ReviewedByUser?.Name,
            ReviewedAt: b.ReviewedAt,
            CreatedAt: b.CreatedAt,
            UpdatedAt: b.UpdatedAt
        );
}
