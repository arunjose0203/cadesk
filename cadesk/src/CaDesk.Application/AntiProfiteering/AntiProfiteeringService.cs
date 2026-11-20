using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.AntiProfiteering;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.AntiProfiteering;

public class AntiProfiteeringService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AntiProfiteeringService(IAppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<AntiProfiteeringSummaryDto> GetSummaryAsync(Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var q = _db.AntiProfiteeringAssessments
            .Include(x => x.SkuItems)
            .Include(x => x.RefundRecords)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);

        var list = await q.ToListAsync(ct);

        int totalAssessments = list.Count;
        int totalSkus = list.Sum(x => x.TotalSkusAudited);
        int totalProfiteeredSkus = list.Sum(x => x.ProfiteeredSkusCount);
        decimal totalTurnover = list.Sum(x => x.TotalTurnoverPostCut);
        decimal totalProfiteered = list.Sum(x => x.TotalUndueProfiteeredAmount);
        decimal totalRefunded = list.Sum(x => x.TotalCustomerRefundedAmount);
        decimal totalCwf = list.Sum(x => x.TotalCwfDepositedAmount);
        decimal pendingRemediation = Math.Max(0m, totalProfiteered - totalRefunded - totalCwf);

        return new AntiProfiteeringSummaryDto(
            TotalAssessments: totalAssessments,
            TotalSkusAudited: totalSkus,
            TotalProfiteeredSkus: totalProfiteeredSkus,
            TotalTurnoverAudited: totalTurnover,
            TotalProfiteeredAmount: totalProfiteered,
            TotalRefundedToCustomers: totalRefunded,
            TotalCwfDeposits: totalCwf,
            TotalPendingRemediation: pendingRemediation
        );
    }

    // -------------------------------------------------------------
    // Rate Notifications
    // -------------------------------------------------------------

    public async Task<List<GstRateNotificationDto>> ListNotificationsAsync(bool? isActive, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var q = _db.GstRateNotifications.Where(x => x.FirmId == firmId);

        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);

        var list = await q.OrderByDescending(x => x.EffectiveDate).ToListAsync(ct);

        // If none exist, auto-seed standard baseline rate cuts
        if (list.Count == 0)
        {
            var baselines = AntiProfiteeringEngine.GenerateBaselineRateNotifications(firmId);
            _db.GstRateNotifications.AddRange(baselines);
            await _db.SaveChangesAsync(ct);
            list = await q.OrderByDescending(x => x.EffectiveDate).ToListAsync(ct);
        }

        return list.Select(ToNotificationDto).ToList();
    }

    public async Task<GstRateNotificationDto?> GetNotificationByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var item = await _db.GstRateNotifications.FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);
        return item is null ? null : ToNotificationDto(item);
    }

    public async Task<GstRateNotificationDto> CreateOrUpdateNotificationAsync(Guid? id, SaveRateNotificationRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        GstRateNotification? n = null;

        if (id.HasValue && id.Value != Guid.Empty)
        {
            n = await _db.GstRateNotifications.FirstOrDefaultAsync(x => x.Id == id.Value && x.FirmId == firmId, ct);
            if (n is null) throw new KeyNotFoundException("GST rate notification not found.");
        }
        else
        {
            n = new GstRateNotification
            {
                FirmId = firmId,
                CreatedAt = DateTime.UtcNow
            };
            _db.GstRateNotifications.Add(n);
        }

        n.NotificationNumber = req.NotificationNumber.Trim();
        n.NotificationDate = req.NotificationDate;
        n.EffectiveDate = req.EffectiveDate;
        n.HsnOrSacCode = req.HsnOrSacCode.Trim();
        n.Description = req.Description.Trim();
        n.PreviousRatePercentage = req.PreviousRatePercentage;
        n.RevisedRatePercentage = req.RevisedRatePercentage;
        n.RateReductionPercentage = req.PreviousRatePercentage - req.RevisedRatePercentage;
        n.IsActive = req.IsActive;
        n.Notes = req.Notes?.Trim() ?? "";

        await _db.SaveChangesAsync(ct);
        return ToNotificationDto(n);
    }

    public async Task<bool> DeleteNotificationAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var n = await _db.GstRateNotifications.FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);
        if (n is null) return false;

        _db.GstRateNotifications.Remove(n);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Assessments
    // -------------------------------------------------------------

    public async Task<List<AntiProfiteeringAssessmentDto>> ListAssessmentsAsync(Guid? clientId, string? financialYear, string? status, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var q = _db.AntiProfiteeringAssessments
            .Include(x => x.Client)
            .Include(x => x.GstNotification)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.AuditStatus == status);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToAssessmentDto).ToList();
    }

    public async Task<AntiProfiteeringAssessmentDto?> GetAssessmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var a = await _db.AntiProfiteeringAssessments
            .Include(x => x.Client)
            .Include(x => x.GstNotification)
            .Include(x => x.SkuItems)
            .Include(x => x.RefundRecords)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        return a is null ? null : ToAssessmentDto(a);
    }

    public async Task<AntiProfiteeringAssessmentDto> CreateOrUpdateAssessmentAsync(Guid? id, SaveAssessmentRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        AntiProfiteeringAssessment? a = null;

        if (id.HasValue && id.Value != Guid.Empty)
        {
            a = await _db.AntiProfiteeringAssessments
                .Include(x => x.Client)
                .Include(x => x.SkuItems)
                .Include(x => x.RefundRecords)
                .FirstOrDefaultAsync(x => x.Id == id.Value && x.FirmId == firmId, ct);

            if (a is null) throw new KeyNotFoundException("Anti-profiteering assessment not found.");
        }
        else
        {
            a = new AntiProfiteeringAssessment
            {
                FirmId = firmId,
                ClientId = req.ClientId,
                AssessmentReference = req.AssessmentReference.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.AntiProfiteeringAssessments.Add(a);
        }

        a.Title = req.Title.Trim();
        a.FinancialYear = req.FinancialYear.Trim();
        a.AuditPeriodFrom = req.AuditPeriodFrom;
        a.AuditPeriodTo = req.AuditPeriodTo;
        a.GstNotificationId = req.GstNotificationId;
        a.NotificationReference = req.NotificationReference?.Trim() ?? "";
        a.RateCutEffectiveDate = req.RateCutEffectiveDate;
        if (!string.IsNullOrWhiteSpace(req.LeadAuditorName)) a.LeadAuditorName = req.LeadAuditorName.Trim();
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await RecalculateAssessmentTotalsAsync(a.Id, ct);

        return (await GetAssessmentByIdAsync(a.Id, ct))!;
    }

    public async Task<bool> DeleteAssessmentAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var a = await _db.AntiProfiteeringAssessments.FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);
        if (a is null) return false;

        _db.AntiProfiteeringAssessments.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AntiProfiteeringAssessmentDto> CertifyAssessmentAsync(Guid id, CertifyAssessmentRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var a = await _db.AntiProfiteeringAssessments
            .Include(x => x.Client)
            .Include(x => x.SkuItems)
            .Include(x => x.RefundRecords)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (a is null) throw new KeyNotFoundException("Assessment not found.");

        a.LeadAuditorName = req.LeadAuditorName.Trim();
        a.Udin = string.IsNullOrWhiteSpace(req.Udin) ? $"24078512AAAAAP{DateTime.UtcNow:MMdd}" : req.Udin.Trim().ToUpperInvariant();
        a.CertificationDate = DateTime.UtcNow;
        a.AuditStatus = "Certified";
        a.UpdatedAt = DateTime.UtcNow;

        string clientName = a.Client?.Name ?? "Client Enterprise";
        string clientGstin = a.Client?.TaxIds.GetValueOrDefault("GSTIN") ?? "27AAACB1234F1Z5";
        a.StatutoryReportText = AntiProfiteeringEngine.GenerateStatutoryAuditReport(a, clientName, clientGstin);

        await _db.SaveChangesAsync(ct);
        return ToAssessmentDto(a);
    }

    // -------------------------------------------------------------
    // SKU Items
    // -------------------------------------------------------------

    public async Task<List<SkuProfiteeringItemDto>> ListSkuItemsAsync(Guid assessmentId, string? complianceStatus, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var q = _db.SkuProfiteeringItems.Where(x => x.AssessmentId == assessmentId && x.FirmId == firmId);

        if (!string.IsNullOrWhiteSpace(complianceStatus))
        {
            q = q.Where(x => x.ComplianceStatus == complianceStatus);
        }

        var list = await q.OrderBy(x => x.ProductCode).ToListAsync(ct);
        return list.Select(ToSkuDto).ToList();
    }

    public async Task<SkuProfiteeringItemDto> CreateOrUpdateSkuItemAsync(Guid assessmentId, Guid? skuId, SaveSkuItemRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var assessment = await _db.AntiProfiteeringAssessments.FirstOrDefaultAsync(x => x.Id == assessmentId && x.FirmId == firmId, ct);
        if (assessment is null) throw new KeyNotFoundException("Assessment not found.");

        SkuProfiteeringItem? item = null;
        if (skuId.HasValue && skuId.Value != Guid.Empty)
        {
            item = await _db.SkuProfiteeringItems.FirstOrDefaultAsync(x => x.Id == skuId.Value && x.AssessmentId == assessmentId && x.FirmId == firmId, ct);
            if (item is null) throw new KeyNotFoundException("SKU profiteering item not found.");
        }
        else
        {
            item = new SkuProfiteeringItem
            {
                FirmId = firmId,
                AssessmentId = assessmentId,
                CreatedAt = DateTime.UtcNow
            };
            _db.SkuProfiteeringItems.Add(item);
        }

        item.ProductCode = req.ProductCode.Trim().ToUpperInvariant();
        item.HsnOrSacCode = req.HsnOrSacCode.Trim();
        item.ProductName = req.ProductName.Trim();
        item.UnitOfMeasurement = string.IsNullOrWhiteSpace(req.UnitOfMeasurement) ? "PCS" : req.UnitOfMeasurement.Trim().ToUpperInvariant();
        item.PreCutBasePrice = req.PreCutBasePrice;
        item.OldGstRate = req.OldGstRate;
        item.NewGstRate = req.NewGstRate;
        item.PostCutActualBasePrice = req.PostCutActualBasePrice;
        item.QuantitySoldPostCut = req.QuantitySoldPostCut;
        item.HasRawMaterialCostJustification = req.HasRawMaterialCostJustification;
        item.JustificationRemarks = req.JustificationRemarks?.Trim() ?? "";
        item.UpdatedAt = DateTime.UtcNow;

        // Perform calculation
        AntiProfiteeringEngine.EvaluateSku(item);

        await _db.SaveChangesAsync(ct);
        await RecalculateAssessmentTotalsAsync(assessmentId, ct);

        return ToSkuDto(item);
    }

    public async Task<bool> DeleteSkuItemAsync(Guid assessmentId, Guid skuId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var item = await _db.SkuProfiteeringItems.FirstOrDefaultAsync(x => x.Id == skuId && x.AssessmentId == assessmentId && x.FirmId == firmId, ct);
        if (item is null) return false;

        _db.SkuProfiteeringItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        await RecalculateAssessmentTotalsAsync(assessmentId, ct);
        return true;
    }

    // -------------------------------------------------------------
    // Refund Records
    // -------------------------------------------------------------

    public async Task<List<ProfiteeringRefundRecordDto>> ListRefundRecordsAsync(Guid assessmentId, string? recipientType, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var q = _db.ProfiteeringRefundRecords.Where(x => x.AssessmentId == assessmentId && x.FirmId == firmId);

        if (!string.IsNullOrWhiteSpace(recipientType))
        {
            q = q.Where(x => x.RecipientType == recipientType);
        }

        var list = await q.OrderByDescending(x => x.RefundOrDepositDate).ToListAsync(ct);
        return list.Select(ToRefundDto).ToList();
    }

    public async Task<ProfiteeringRefundRecordDto> CreateOrUpdateRefundRecordAsync(Guid assessmentId, Guid? refundId, SaveRefundRecordRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var assessment = await _db.AntiProfiteeringAssessments.FirstOrDefaultAsync(x => x.Id == assessmentId && x.FirmId == firmId, ct);
        if (assessment is null) throw new KeyNotFoundException("Assessment not found.");

        ProfiteeringRefundRecord? r = null;
        if (refundId.HasValue && refundId.Value != Guid.Empty)
        {
            r = await _db.ProfiteeringRefundRecords.FirstOrDefaultAsync(x => x.Id == refundId.Value && x.AssessmentId == assessmentId && x.FirmId == firmId, ct);
            if (r is null) throw new KeyNotFoundException("Refund record not found.");
        }
        else
        {
            r = new ProfiteeringRefundRecord
            {
                FirmId = firmId,
                AssessmentId = assessmentId,
                CreatedAt = DateTime.UtcNow
            };
            _db.ProfiteeringRefundRecords.Add(r);
        }

        r.RecipientType = req.RecipientType;
        r.CustomerNameOrEntity = req.CustomerNameOrEntity.Trim();
        r.CustomerGstin = req.CustomerGstin?.Trim().ToUpperInvariant() ?? "";
        r.InvoiceReference = req.InvoiceReference?.Trim() ?? "";
        r.RefundAmount = req.RefundAmount;
        r.RefundMode = req.RefundMode ?? "CreditNote";
        r.RefundOrDepositDate = req.RefundOrDepositDate;
        r.ChallanOrCinNumber = req.ChallanOrCinNumber?.Trim() ?? "";
        r.InterestAmountPaid = req.InterestAmountPaid;
        r.Notes = req.Notes?.Trim() ?? "";

        await _db.SaveChangesAsync(ct);
        await RecalculateAssessmentTotalsAsync(assessmentId, ct);

        return ToRefundDto(r);
    }

    public async Task<bool> DeleteRefundRecordAsync(Guid assessmentId, Guid refundId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var r = await _db.ProfiteeringRefundRecords.FirstOrDefaultAsync(x => x.Id == refundId && x.AssessmentId == assessmentId && x.FirmId == firmId, ct);
        if (r is null) return false;

        _db.ProfiteeringRefundRecords.Remove(r);
        await _db.SaveChangesAsync(ct);
        await RecalculateAssessmentTotalsAsync(assessmentId, ct);
        return true;
    }

    // -------------------------------------------------------------
    // Recalculation Engine
    // -------------------------------------------------------------

    private async Task RecalculateAssessmentTotalsAsync(Guid assessmentId, CancellationToken ct)
    {
        var firmId = _tenantContext.FirmId;
        var a = await _db.AntiProfiteeringAssessments
            .Include(x => x.Client)
            .Include(x => x.SkuItems)
            .Include(x => x.RefundRecords)
            .FirstOrDefaultAsync(x => x.Id == assessmentId && x.FirmId == firmId, ct);

        if (a is null) return;

        a.TotalSkusAudited = a.SkuItems.Count;
        a.ProfiteeredSkusCount = a.SkuItems.Count(x => x.ComplianceStatus != "Compliant");
        a.TotalTurnoverPostCut = a.SkuItems.Sum(x => x.PostCutActualSellingPrice * x.QuantitySoldPostCut);
        a.TotalUndueProfiteeredAmount = a.SkuItems.Sum(x => x.TotalProfiteeredAmount);

        a.TotalCustomerRefundedAmount = a.RefundRecords
            .Where(x => x.RecipientType == "IdentifiedCustomer")
            .Sum(x => x.RefundAmount);

        a.TotalCwfDepositedAmount = a.RefundRecords
            .Where(x => x.RecipientType == "ConsumerWelfareFundCentral" || x.RecipientType == "ConsumerWelfareFundState")
            .Sum(x => x.RefundAmount);

        decimal unrefunded = Math.Max(0m, a.TotalUndueProfiteeredAmount - a.TotalCustomerRefundedAmount - a.TotalCwfDepositedAmount);
        var (_, _, interest) = AntiProfiteeringEngine.CalculateCwfSplit(unrefunded, 30);
        a.CwfInterestPayableAmount = interest;

        string clientName = a.Client?.Name ?? "Client Enterprise";
        string clientGstin = a.Client?.TaxIds.GetValueOrDefault("GSTIN") ?? "27AAACB1234F1Z5";
        a.StatutoryReportText = AntiProfiteeringEngine.GenerateStatutoryAuditReport(a, clientName, clientGstin);
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private static GstRateNotificationDto ToNotificationDto(GstRateNotification n)
    {
        return new GstRateNotificationDto(
            Id: n.Id,
            NotificationNumber: n.NotificationNumber,
            NotificationDate: n.NotificationDate,
            EffectiveDate: n.EffectiveDate,
            HsnOrSacCode: n.HsnOrSacCode,
            Description: n.Description,
            PreviousRatePercentage: n.PreviousRatePercentage,
            RevisedRatePercentage: n.RevisedRatePercentage,
            RateReductionPercentage: n.RateReductionPercentage,
            IsActive: n.IsActive,
            Notes: n.Notes,
            CreatedAt: n.CreatedAt
        );
    }

    private static AntiProfiteeringAssessmentDto ToAssessmentDto(AntiProfiteeringAssessment a)
    {
        return new AntiProfiteeringAssessmentDto(
            Id: a.Id,
            ClientId: a.ClientId,
            ClientName: a.Client?.Name,
            ClientGstin: a.Client?.TaxIds.GetValueOrDefault("GSTIN"),
            AssessmentReference: a.AssessmentReference,
            Title: a.Title,
            FinancialYear: a.FinancialYear,
            AuditPeriodFrom: a.AuditPeriodFrom,
            AuditPeriodTo: a.AuditPeriodTo,
            GstNotificationId: a.GstNotificationId,
            NotificationReference: a.NotificationReference,
            RateCutEffectiveDate: a.RateCutEffectiveDate,
            AuditStatus: a.AuditStatus,
            LeadAuditorName: a.LeadAuditorName,
            Udin: a.Udin,
            CertificationDate: a.CertificationDate,
            TotalSkusAudited: a.TotalSkusAudited,
            ProfiteeredSkusCount: a.ProfiteeredSkusCount,
            TotalTurnoverPostCut: a.TotalTurnoverPostCut,
            TotalUndueProfiteeredAmount: a.TotalUndueProfiteeredAmount,
            TotalCustomerRefundedAmount: a.TotalCustomerRefundedAmount,
            TotalCwfDepositedAmount: a.TotalCwfDepositedAmount,
            CwfInterestPayableAmount: a.CwfInterestPayableAmount,
            StatutoryReportText: a.StatutoryReportText,
            CreatedAt: a.CreatedAt,
            UpdatedAt: a.UpdatedAt
        );
    }

    private static SkuProfiteeringItemDto ToSkuDto(SkuProfiteeringItem s)
    {
        return new SkuProfiteeringItemDto(
            Id: s.Id,
            AssessmentId: s.AssessmentId,
            ProductCode: s.ProductCode,
            HsnOrSacCode: s.HsnOrSacCode,
            ProductName: s.ProductName,
            UnitOfMeasurement: s.UnitOfMeasurement,
            PreCutBasePrice: s.PreCutBasePrice,
            OldGstRate: s.OldGstRate,
            PreCutSellingPrice: s.PreCutSellingPrice,
            NewGstRate: s.NewGstRate,
            CommensuratePriceRequired: s.CommensuratePriceRequired,
            PostCutActualBasePrice: s.PostCutActualBasePrice,
            PostCutActualSellingPrice: s.PostCutActualSellingPrice,
            BasePriceIncreasePercentage: s.BasePriceIncreasePercentage,
            QuantitySoldPostCut: s.QuantitySoldPostCut,
            ProfiteeringPerUnit: s.ProfiteeringPerUnit,
            TotalProfiteeredAmount: s.TotalProfiteeredAmount,
            ComplianceStatus: s.ComplianceStatus,
            HasRawMaterialCostJustification: s.HasRawMaterialCostJustification,
            JustificationRemarks: s.JustificationRemarks,
            CreatedAt: s.CreatedAt,
            UpdatedAt: s.UpdatedAt
        );
    }

    private static ProfiteeringRefundRecordDto ToRefundDto(ProfiteeringRefundRecord r)
    {
        return new ProfiteeringRefundRecordDto(
            Id: r.Id,
            AssessmentId: r.AssessmentId,
            RecipientType: r.RecipientType,
            CustomerNameOrEntity: r.CustomerNameOrEntity,
            CustomerGstin: r.CustomerGstin,
            InvoiceReference: r.InvoiceReference,
            RefundAmount: r.RefundAmount,
            RefundMode: r.RefundMode,
            RefundOrDepositDate: r.RefundOrDepositDate,
            ChallanOrCinNumber: r.ChallanOrCinNumber,
            InterestAmountPaid: r.InterestAmountPaid,
            Notes: r.Notes,
            CreatedAt: r.CreatedAt
        );
    }
}
