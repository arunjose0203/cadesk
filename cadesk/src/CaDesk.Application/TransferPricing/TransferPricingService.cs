using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.TransferPricing;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.TransferPricing;

public class TransferPricingService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TransferPricingService(IAppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TransferPricingSummaryDto> GetSummaryAsync(
        Guid? clientId,
        string? financialYear,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var q = _db.TransferPricingStudies
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);

        var list = await q.ToListAsync(ct);

        int total = list.Count;
        int active = list.Count(x => x.StudyStatus != TransferPricingConstants.StatusCertified);
        int certified = list.Count(x => x.StudyStatus == TransferPricingConstants.StatusCertified);
        int totalAes = list.Sum(x => x.TotalAssociatedEnterprisesCount);
        int totalIntlTxs = list.Sum(x => x.TotalInternationalTransactionsCount);
        decimal totalIntlVol = list.Sum(x => x.TotalInternationalInrValue);
        int totalSdtTxs = list.Sum(x => x.TotalSdtTransactionsCount);
        decimal totalSdtVol = list.Sum(x => x.TotalSdtInrValue);
        int totalAdjustmentsCount = list.Sum(x => x.TotalAlpAdjustmentsCount);
        decimal totalAdjustmentAmount = list.Sum(x => x.TotalAlpAdjustmentAmount);

        return new TransferPricingSummaryDto(
            total,
            active,
            certified,
            totalAes,
            totalIntlTxs,
            totalIntlVol,
            totalSdtTxs,
            totalSdtVol,
            totalAdjustmentsCount,
            totalAdjustmentAmount);
    }

    public BenchmarkCalculationResult CalculateBenchmark(BenchmarkCalculationRequest req)
    {
        var res = TransferPricingEngine.CalculateArmLengthRange(req.ComparableMargins, req.ToleranceBandPercentage);
        return new BenchmarkCalculationResult(
            res.UseRange,
            res.DataCount,
            res.RangeMin,
            res.RangeMax,
            res.MedianOrMean,
            res.Explanation);
    }

    // -------------------------------------------------------------
    // Study Management
    // -------------------------------------------------------------
    public async Task<List<TransferPricingStudyDto>> ListStudiesAsync(
        Guid? clientId,
        string? financialYear,
        string? status,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var q = _db.TransferPricingStudies
            .Include(x => x.Client)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.StudyStatus == status);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToStudyDto).ToList();
    }

    public async Task<TransferPricingStudyDto?> GetStudyByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var item = await _db.TransferPricingStudies
            .Include(x => x.Client)
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
                .ThenInclude(t => t.AssociatedEnterprise)
            .Include(x => x.Comparables)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        return item is null ? null : ToStudyDto(item);
    }

    public async Task<TransferPricingStudyDto> CreateOrUpdateStudyAsync(
        Guid? id,
        SaveStudyRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId && c.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Client not found or access denied.");

        TransferPricingStudy study;
        bool isNew = !id.HasValue;

        if (isNew)
        {
            study = new TransferPricingStudy
            {
                FirmId = firmId,
                ClientId = req.ClientId,
                StudyStatus = TransferPricingConstants.StatusDraft,
                CreatedAt = DateTime.UtcNow
            };
            _db.TransferPricingStudies.Add(study);
        }
        else
        {
            study = await _db.TransferPricingStudies
                .Include(x => x.AssociatedEnterprises)
                .Include(x => x.Transactions)
                .Include(x => x.Comparables)
                .FirstOrDefaultAsync(x => x.Id == id!.Value && x.FirmId == firmId, ct)
                ?? throw new InvalidOperationException("Transfer pricing study not found.");
        }

        study.ClientId = req.ClientId;
        study.StudyReference = req.StudyReference;
        study.Title = req.Title;
        study.FinancialYear = req.FinancialYear;
        study.AssessmentYear = req.AssessmentYear;
        study.AuditPeriodStart = req.AuditPeriodStart;
        study.AuditPeriodEnd = req.AuditPeriodEnd;
        study.Rule10DDocumentationMaintained = req.Rule10DDocumentationMaintained;
        study.Rule10DDocumentationPath = req.Rule10DDocumentationPath;

        study.AccountantName = req.AccountantName;
        study.AccountantFirmName = req.AccountantFirmName;
        study.MembershipNumber = req.MembershipNumber;
        study.FirmRegistrationNumber = req.FirmRegistrationNumber;
        study.UpdatedAt = DateTime.UtcNow;

        TransferPricingEngine.RecalculateStudySummary(study);
        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            client.LegalName ?? client.Name,
            client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        study.Client = client;

        return ToStudyDto(study);
    }

    public async Task<bool> DeleteStudyAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (study is null) return false;

        _db.TransferPricingStudies.Remove(study);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TransferPricingStudyDto> CertifyStudyAsync(
        Guid id,
        CertifyStudyRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.Client)
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
                .ThenInclude(t => t.AssociatedEnterprise)
            .Include(x => x.Comparables)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Study not found.");

        if (string.IsNullOrWhiteSpace(req.Udin) || req.Udin.Trim().Length < 18)
        {
            throw new ArgumentException("A valid 18-character ICAI UDIN is required for certification of Form 3CEB.");
        }

        study.Udin = req.Udin.Trim().ToUpperInvariant();
        study.CertifiedAt = DateTime.UtcNow;
        study.CertifiedBy = req.CertifiedBy;
        study.StudyStatus = TransferPricingConstants.StatusCertified;
        study.UpdatedAt = DateTime.UtcNow;

        TransferPricingEngine.RecalculateStudySummary(study);
        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return ToStudyDto(study);
    }

    public async Task<string> GenerateForm3CEBReportAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.Client)
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
                .ThenInclude(t => t.AssociatedEnterprise)
            .Include(x => x.Comparables)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Study not found.");

        TransferPricingEngine.RecalculateStudySummary(study);
        var report = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        study.Form3CebReportText = report;
        study.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return report;
    }

    // -------------------------------------------------------------
    // Associated Enterprises
    // -------------------------------------------------------------
    public async Task<List<AssociatedEnterpriseDto>> ListEnterprisesAsync(Guid studyId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var list = await _db.AssociatedEnterprises
            .Where(x => x.StudyId == studyId && x.FirmId == firmId)
            .OrderBy(x => x.EnterpriseName)
            .ToListAsync(ct);

        return list.Select(ToEnterpriseDto).ToList();
    }

    public async Task<AssociatedEnterpriseDto> CreateOrUpdateEnterpriseAsync(
        Guid studyId,
        Guid? aeId,
        SaveEnterpriseRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == studyId && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Study not found.");

        AssociatedEnterprise item;
        if (!aeId.HasValue)
        {
            item = new AssociatedEnterprise
            {
                FirmId = firmId,
                StudyId = studyId,
                CreatedAt = DateTime.UtcNow
            };
            study.AssociatedEnterprises.Add(item);
            _db.AssociatedEnterprises.Add(item);
        }
        else
        {
            item = study.AssociatedEnterprises.FirstOrDefault(x => x.Id == aeId.Value)
                ?? throw new InvalidOperationException("Associated Enterprise not found.");
        }

        item.EnterpriseName = req.EnterpriseName;
        item.CountryOfResidence = req.CountryOfResidence;
        item.CountryCode = req.CountryCode;
        item.TaxIdentificationNumber = req.TaxIdentificationNumber;
        item.RegisteredAddress = req.RegisteredAddress;
        item.RelationshipSection = req.RelationshipSection;
        item.OwnershipPercentage = req.OwnershipPercentage;
        item.BriefActivityDescription = req.BriefActivityDescription;

        TransferPricingEngine.RecalculateStudySummary(study);
        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return ToEnterpriseDto(item);
    }

    public async Task<bool> DeleteEnterpriseAsync(Guid studyId, Guid aeId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == studyId && x.FirmId == firmId, ct);

        if (study is null) return false;

        var item = study.AssociatedEnterprises.FirstOrDefault(x => x.Id == aeId);
        if (item is null) return false;

        study.AssociatedEnterprises.Remove(item);
        _db.AssociatedEnterprises.Remove(item);

        TransferPricingEngine.RecalculateStudySummary(study);
        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Transfer Pricing Transactions
    // -------------------------------------------------------------
    public async Task<List<TransferPricingTransactionDto>> ListTransactionsAsync(Guid studyId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var list = await _db.TransferPricingTransactions
            .Include(x => x.AssociatedEnterprise)
            .Where(x => x.StudyId == studyId && x.FirmId == firmId)
            .OrderBy(x => x.ClauseNumber)
            .ToListAsync(ct);

        return list.Select(ToTransactionDto).ToList();
    }

    public async Task<TransferPricingTransactionDto> CreateOrUpdateTransactionAsync(
        Guid studyId,
        Guid? txId,
        SaveTransactionRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
                .ThenInclude(t => t.AssociatedEnterprise)
            .Include(x => x.Comparables)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == studyId && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Study not found.");

        TransferPricingTransaction item;
        if (!txId.HasValue)
        {
            item = new TransferPricingTransaction
            {
                FirmId = firmId,
                StudyId = studyId,
                CreatedAt = DateTime.UtcNow
            };
            study.Transactions.Add(item);
            _db.TransferPricingTransactions.Add(item);
        }
        else
        {
            item = study.Transactions.FirstOrDefault(x => x.Id == txId.Value)
                ?? throw new InvalidOperationException("Transaction not found.");
        }

        item.AssociatedEnterpriseId = req.AssociatedEnterpriseId;
        item.TransactionCategory = req.TransactionCategory;
        item.ClauseNumber = req.ClauseNumber;
        item.DescriptionOfTransaction = req.DescriptionOfTransaction;
        item.CurrencyCode = req.CurrencyCode;
        item.ForeignCurrencyAmount = req.ForeignCurrencyAmount;
        item.ExchangeRate = req.ExchangeRate > 0 ? req.ExchangeRate : 1.0m;
        item.MostAppropriateMethod = req.MostAppropriateMethod;
        item.ProfitLevelIndicator = req.ProfitLevelIndicator;
        item.TestedPartyCostBase = req.TestedPartyCostBase;
        item.TestedPartyMargin = req.TestedPartyMargin;
        item.ToleranceBandPercentage = req.ToleranceBandPercentage > 0 ? req.ToleranceBandPercentage : 3.0m;
        item.MethodSelectionRationale = req.MethodSelectionRationale;
        item.DocumentationReference = req.DocumentationReference;
        item.UpdatedAt = DateTime.UtcNow;

        // Fetch accepted comparables linked to this transaction or study
        var compMargins = study.Comparables
            .Where(c => c.IsAccepted && (c.TransactionId == item.Id || c.TransactionId == null))
            .Select(c => c.CalculatedMarginPercentage)
            .ToList();

        TransferPricingEngine.EvaluateTransaction(item, compMargins);
        TransferPricingEngine.RecalculateStudySummary(study);

        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        item.AssociatedEnterprise = study.AssociatedEnterprises.FirstOrDefault(x => x.Id == item.AssociatedEnterpriseId);

        return ToTransactionDto(item);
    }

    public async Task<bool> DeleteTransactionAsync(Guid studyId, Guid txId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == studyId && x.FirmId == firmId, ct);

        if (study is null) return false;

        var item = study.Transactions.FirstOrDefault(x => x.Id == txId);
        if (item is null) return false;

        study.Transactions.Remove(item);
        _db.TransferPricingTransactions.Remove(item);

        TransferPricingEngine.RecalculateStudySummary(study);
        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Comparable Benchmarks
    // -------------------------------------------------------------
    public async Task<List<ComparableBenchmarkDto>> ListComparablesAsync(Guid studyId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var list = await _db.ComparableBenchmarks
            .Where(x => x.StudyId == studyId && x.FirmId == firmId)
            .OrderBy(x => x.ComparableCompanyName)
            .ToListAsync(ct);

        return list.Select(ToComparableDto).ToList();
    }

    public async Task<ComparableBenchmarkDto> CreateOrUpdateComparableAsync(
        Guid studyId,
        Guid? compId,
        SaveComparableRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
            .Include(x => x.Comparables)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == studyId && x.FirmId == firmId, ct)
            ?? throw new InvalidOperationException("Study not found.");

        ComparableBenchmark item;
        if (!compId.HasValue)
        {
            item = new ComparableBenchmark
            {
                FirmId = firmId,
                StudyId = studyId,
                CreatedAt = DateTime.UtcNow
            };
            study.Comparables.Add(item);
            _db.ComparableBenchmarks.Add(item);
        }
        else
        {
            item = study.Comparables.FirstOrDefault(x => x.Id == compId.Value)
                ?? throw new InvalidOperationException("Comparable benchmark record not found.");
        }

        item.TransactionId = req.TransactionId;
        item.ComparableCompanyName = req.ComparableCompanyName;
        item.CinOrIdentifier = req.CinOrIdentifier;
        item.FinancialYear = req.FinancialYear;
        item.OperatingRevenue = req.OperatingRevenue;
        item.OperatingCost = req.OperatingCost;
        item.OperatingProfit = req.OperatingProfit;
        item.Weight = req.Weight > 0 ? req.Weight : 1.0m;
        item.IsAccepted = req.IsAccepted;
        item.RejectionReason = req.RejectionReason;

        // Auto calculate margin percentage (OP / TC)
        if (item.OperatingCost > 0)
        {
            item.CalculatedMarginPercentage = Math.Round((item.OperatingProfit / item.OperatingCost) * 100m, 2);
        }
        else if (item.OperatingRevenue > 0)
        {
            item.CalculatedMarginPercentage = Math.Round((item.OperatingProfit / item.OperatingRevenue) * 100m, 2);
        }
        else
        {
            item.CalculatedMarginPercentage = 0m;
        }

        // Re-evaluate affected transactions
        var compMargins = study.Comparables
            .Where(c => c.IsAccepted)
            .Select(c => c.CalculatedMarginPercentage)
            .ToList();

        foreach (var tx in study.Transactions)
        {
            TransferPricingEngine.EvaluateTransaction(tx, compMargins);
        }

        TransferPricingEngine.RecalculateStudySummary(study);
        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return ToComparableDto(item);
    }

    public async Task<bool> DeleteComparableAsync(Guid studyId, Guid compId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var study = await _db.TransferPricingStudies
            .Include(x => x.AssociatedEnterprises)
            .Include(x => x.Transactions)
            .Include(x => x.Comparables)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == studyId && x.FirmId == firmId, ct);

        if (study is null) return false;

        var item = study.Comparables.FirstOrDefault(x => x.Id == compId);
        if (item is null) return false;

        study.Comparables.Remove(item);
        _db.ComparableBenchmarks.Remove(item);

        var compMargins = study.Comparables
            .Where(c => c.IsAccepted)
            .Select(c => c.CalculatedMarginPercentage)
            .ToList();

        foreach (var tx in study.Transactions)
        {
            TransferPricingEngine.EvaluateTransaction(tx, compMargins);
        }

        TransferPricingEngine.RecalculateStudySummary(study);
        study.Form3CebReportText = TransferPricingEngine.GenerateForm3CEB(
            study,
            study.Client?.LegalName ?? study.Client?.Name ?? "The Assessee",
            study.Client != null && study.Client.TaxIds.TryGetValue("PAN", out var pan) ? pan : "PAN NOT PROVIDED",
            "Registered Office Address");

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Helper Mappers
    // -------------------------------------------------------------
    private static TransferPricingStudyDto ToStudyDto(TransferPricingStudy x) => new(
        x.Id,
        x.ClientId,
        x.Client?.Name ?? "",
        x.StudyReference,
        x.Title,
        x.FinancialYear,
        x.AssessmentYear,
        x.AuditPeriodStart,
        x.AuditPeriodEnd,
        x.Rule10DDocumentationMaintained,
        x.Rule10DDocumentationPath,
        x.StudyStatus,
        x.AccountantName,
        x.AccountantFirmName,
        x.MembershipNumber,
        x.FirmRegistrationNumber,
        x.Udin,
        x.CertifiedAt,
        x.CertifiedBy,
        x.TotalAssociatedEnterprisesCount,
        x.TotalInternationalTransactionsCount,
        x.TotalInternationalInrValue,
        x.TotalSdtTransactionsCount,
        x.TotalSdtInrValue,
        x.TotalAlpAdjustmentsCount,
        x.TotalAlpAdjustmentAmount,
        x.TransactionsWithinToleranceCount,
        x.TransactionsOutsideToleranceCount,
        x.Form3CebReportText,
        x.CreatedAt,
        x.UpdatedAt);

    private static AssociatedEnterpriseDto ToEnterpriseDto(AssociatedEnterprise x) => new(
        x.Id,
        x.StudyId,
        x.EnterpriseName,
        x.CountryOfResidence,
        x.CountryCode,
        x.TaxIdentificationNumber,
        x.RegisteredAddress,
        x.RelationshipSection,
        x.OwnershipPercentage,
        x.BriefActivityDescription,
        x.CreatedAt);

    private static TransferPricingTransactionDto ToTransactionDto(TransferPricingTransaction x) => new(
        x.Id,
        x.StudyId,
        x.AssociatedEnterpriseId,
        x.AssociatedEnterprise?.EnterpriseName,
        x.AssociatedEnterprise?.CountryOfResidence,
        x.TransactionCategory,
        x.ClauseNumber,
        x.DescriptionOfTransaction,
        x.CurrencyCode,
        x.ForeignCurrencyAmount,
        x.ExchangeRate,
        x.InrValue,
        x.MostAppropriateMethod,
        x.ProfitLevelIndicator,
        x.TestedPartyCostBase,
        x.TestedPartyMargin,
        x.ComparablesCount,
        x.BenchmarkRangeMin,
        x.BenchmarkRangeMax,
        x.BenchmarkMedianOrMean,
        x.ToleranceBandPercentage,
        x.IsWithinArmLength,
        x.AlpAdjustmentAmount,
        x.MethodSelectionRationale,
        x.DocumentationReference,
        x.CreatedAt,
        x.UpdatedAt);

    private static ComparableBenchmarkDto ToComparableDto(ComparableBenchmark x) => new(
        x.Id,
        x.StudyId,
        x.TransactionId,
        x.ComparableCompanyName,
        x.CinOrIdentifier,
        x.FinancialYear,
        x.OperatingRevenue,
        x.OperatingCost,
        x.OperatingProfit,
        x.CalculatedMarginPercentage,
        x.Weight,
        x.IsAccepted,
        x.RejectionReason,
        x.CreatedAt);
}
