using CaDesk.Application.Abstractions;
using CaDesk.Domain.FixedAssets;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.FixedAssets;

public class FixedAssetService(IAppDbContext db) : IFixedAssetService
{
    public async Task<FixedAssetSummaryDto> GetSummaryAsync(Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var qAssets = db.FixedAssets.AsNoTracking();
        var qEntries = db.AssetDepreciationEntries.AsNoTracking();
        var qVerifications = db.AssetPhysicalVerifications.AsNoTracking();

        if (clientId.HasValue)
        {
            qAssets = qAssets.Where(a => a.ClientId == clientId.Value);
            qEntries = qEntries.Where(e => e.FixedAsset!.ClientId == clientId.Value);
            qVerifications = qVerifications.Where(v => v.FixedAsset!.ClientId == clientId.Value);
        }

        var assets = await qAssets.ToListAsync(ct);
        var totalAssets = assets.Count;
        var activeAssets = assets.Count(a => a.Status == AssetStatus.Active);
        var disposedAssets = assets.Count(a => a.Status == AssetStatus.Disposed);
        var totalGrossBlock = assets.Sum(a => a.GrossCost);

        if (!string.IsNullOrWhiteSpace(financialYear))
        {
            qEntries = qEntries.Where(e => e.FinancialYear == financialYear);
        }

        var entries = await qEntries.ToListAsync(ct);
        var totalBookDepr = entries.Sum(e => e.BookDepreciation);
        var totalTaxDepr = entries.Sum(e => e.TaxDepreciation);
        var totalTimingDiff = entries.Sum(e => e.TimingDifference);
        var netDtl = entries.Sum(e => e.DeferredTaxImpact);

        var totalNetBookValue = entries.Count > 0 ? entries.Sum(e => e.ClosingBookValue) : totalGrossBlock;
        var totalTaxWdv = entries.Count > 0 ? entries.Sum(e => e.ClosingTaxWdv) : totalGrossBlock;

        var verifications = await qVerifications.ToListAsync(ct);
        var totalVerifications = verifications.Count;
        var twelveMonthsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12));
        var verifiedRecent = verifications.Count(v => v.VerificationDate >= twelveMonthsAgo);
        var compliancePct = totalAssets > 0 ? Math.Round((decimal)verifiedRecent / totalAssets * 100m, 1) : 100m;

        return new FixedAssetSummaryDto(
            totalAssets,
            activeAssets,
            disposedAssets,
            totalGrossBlock,
            totalBookDepr,
            totalNetBookValue,
            totalTaxDepr,
            totalTaxWdv,
            totalTimingDiff,
            netDtl,
            totalVerifications,
            verifiedRecent,
            compliancePct
        );
    }

    public async Task<IReadOnlyList<FixedAssetDto>> GetAssetsAsync(Guid? clientId, AssetClass? assetClass, AssetStatus? status, CancellationToken ct = default)
    {
        var q = db.FixedAssets
            .AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.DepreciationEntries)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(a => a.ClientId == clientId.Value);
        if (assetClass.HasValue) q = q.Where(a => a.AssetClass == assetClass.Value);
        if (status.HasValue) q = q.Where(a => a.Status == status.Value);

        var list = await q.OrderBy(a => a.AssetTag).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<FixedAssetDto?> GetAssetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.FixedAssets
            .AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.DepreciationEntries)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return item is null ? null : ToDto(item);
    }

    public async Task<FixedAssetDto> CreateAssetAsync(SaveFixedAssetRequest req, CancellationToken ct = default)
    {
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            ClientId = req.ClientId,
            AssetTag = req.AssetTag.Trim(),
            Name = req.Name.Trim(),
            AssetClass = req.AssetClass,
            PurchaseDate = req.PurchaseDate,
            CapitalizationDate = req.CapitalizationDate,
            PurchaseCost = req.PurchaseCost,
            InwardFreight = req.InwardFreight,
            InstallationCost = req.InstallationCost,
            PreOperatingCost = req.PreOperatingCost,
            GstItcClaimed = req.GstItcClaimed,
            UsefulLifeYears = req.UsefulLifeYears > 0 ? req.UsefulLifeYears : 5,
            CompanyDepreciationMethod = req.CompanyDepreciationMethod,
            ShiftType = req.ShiftType,
            IncomeTaxBlockRatePercent = req.IncomeTaxBlockRatePercent > 0 ? req.IncomeTaxBlockRatePercent : 15,
            IsEligibleForAdditionalDepreciation = req.IsEligibleForAdditionalDepreciation,
            Location = req.Location?.Trim(),
            Department = req.Department?.Trim(),
            CustodianName = req.CustodianName?.Trim(),
            Status = AssetStatus.Active
        };

        if (req.SalvageValue.HasValue)
        {
            asset.SalvageValue = req.SalvageValue.Value;
        }

        asset.RecalculateGrossCost();

        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync(ct);

        return (await GetAssetByIdAsync(asset.Id, ct))!;
    }

    public async Task<FixedAssetDto> UpdateAssetAsync(Guid id, SaveFixedAssetRequest req, CancellationToken ct = default)
    {
        var asset = await db.FixedAssets.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"Fixed asset {id} not found.");

        asset.AssetTag = req.AssetTag.Trim();
        asset.Name = req.Name.Trim();
        asset.AssetClass = req.AssetClass;
        asset.PurchaseDate = req.PurchaseDate;
        asset.CapitalizationDate = req.CapitalizationDate;
        asset.PurchaseCost = req.PurchaseCost;
        asset.InwardFreight = req.InwardFreight;
        asset.InstallationCost = req.InstallationCost;
        asset.PreOperatingCost = req.PreOperatingCost;
        asset.GstItcClaimed = req.GstItcClaimed;
        asset.UsefulLifeYears = req.UsefulLifeYears;
        asset.CompanyDepreciationMethod = req.CompanyDepreciationMethod;
        asset.ShiftType = req.ShiftType;
        asset.IncomeTaxBlockRatePercent = req.IncomeTaxBlockRatePercent;
        asset.IsEligibleForAdditionalDepreciation = req.IsEligibleForAdditionalDepreciation;
        asset.Location = req.Location?.Trim();
        asset.Department = req.Department?.Trim();
        asset.CustodianName = req.CustodianName?.Trim();

        if (req.SalvageValue.HasValue)
        {
            asset.SalvageValue = req.SalvageValue.Value;
        }

        asset.RecalculateGrossCost();

        await db.SaveChangesAsync(ct);
        return (await GetAssetByIdAsync(asset.Id, ct))!;
    }

    public async Task<FixedAssetDto> DisposeAssetAsync(Guid id, DisposeAssetRequest req, CancellationToken ct = default)
    {
        var asset = await db.FixedAssets.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"Fixed asset {id} not found.");

        asset.Status = AssetStatus.Disposed;
        asset.DisposalDate = req.DisposalDate;
        asset.DisposalProceeds = req.DisposalProceeds;
        asset.DisposalRemarks = req.Remarks?.Trim();

        await db.SaveChangesAsync(ct);
        return (await GetAssetByIdAsync(asset.Id, ct))!;
    }

    public async Task DeleteAssetAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await db.FixedAssets.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (asset is not null)
        {
            db.FixedAssets.Remove(asset);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<AssetDepreciationEntryDto>> RunDepreciationAsync(RunDualDepreciationRequest req, CancellationToken ct = default)
    {
        var (fyStart, fyEnd) = DualDepreciationEngine.ParseFinancialYear(req.FinancialYear);
        var corporateTaxRate = req.CorporateTaxRate ?? DualDepreciationEngine.DefaultCorporateTaxRate;

        var qAssets = db.FixedAssets
            .Include(a => a.Client)
            .Include(a => a.DepreciationEntries)
            .Where(a => a.ClientId == req.ClientId);

        if (req.AssetId.HasValue)
        {
            qAssets = qAssets.Where(a => a.Id == req.AssetId.Value);
        }

        var assets = await qAssets.ToListAsync(ct);
        var results = new List<AssetDepreciationEntry>();

        foreach (var asset in assets)
        {
            // Skip assets capitalized after this FY
            if (asset.CapitalizationDate > fyEnd) continue;

            // Skip assets disposed before this FY
            if (asset.DisposalDate.HasValue && asset.DisposalDate.Value < fyStart) continue;

            // Find prior FY entry or start from GrossCost
            var priorEntries = asset.DepreciationEntries
                .Where(e => e.FinancialYear != req.FinancialYear)
                .OrderByDescending(e => e.FinancialYear)
                .ToList();

            decimal openingBook = priorEntries.FirstOrDefault()?.ClosingBookValue ?? asset.GrossCost;
            decimal openingTax = priorEntries.FirstOrDefault()?.ClosingTaxWdv ?? asset.GrossCost;

            var (bookDepr, closingBook, activeDays) = DualDepreciationEngine.CalculateScheduleIIDepreciation(
                asset.GrossCost,
                asset.SalvageValue,
                asset.UsefulLifeYears,
                asset.CompanyDepreciationMethod,
                asset.ShiftType,
                openingBook,
                asset.CapitalizationDate,
                asset.DisposalDate,
                fyStart,
                fyEnd
            );

            var (taxDepr, closingTax, isUnder180, daysPutToUse) = DualDepreciationEngine.CalculateSection32Depreciation(
                asset.GrossCost,
                asset.IncomeTaxBlockRatePercent,
                asset.IsEligibleForAdditionalDepreciation,
                openingTax,
                asset.CapitalizationDate,
                asset.DisposalDate,
                fyStart,
                fyEnd
            );

            var (timingDiff, deferredTax) = DualDepreciationEngine.CalculateDeferredTax(
                taxDepr,
                bookDepr,
                corporateTaxRate
            );

            var existing = await db.AssetDepreciationEntries
                .FirstOrDefaultAsync(e => e.FixedAssetId == asset.Id && e.FinancialYear == req.FinancialYear, ct);

            if (existing is not null)
            {
                existing.OpeningBookValue = openingBook;
                existing.BookDepreciation = bookDepr;
                existing.ClosingBookValue = closingBook;
                existing.OpeningTaxWdv = openingTax;
                existing.TaxDepreciation = taxDepr;
                existing.ClosingTaxWdv = closingTax;
                existing.TimingDifference = timingDiff;
                existing.DeferredTaxImpact = deferredTax;
                existing.DaysPutToUseInYear = daysPutToUse;
                existing.IsUnder180Days = isUnder180;
                existing.CalculatedAt = DateTime.UtcNow;
                results.Add(existing);
            }
            else
            {
                var entry = new AssetDepreciationEntry
                {
                    Id = Guid.NewGuid(),
                    FixedAssetId = asset.Id,
                    FinancialYear = req.FinancialYear,
                    OpeningBookValue = openingBook,
                    BookDepreciation = bookDepr,
                    ClosingBookValue = closingBook,
                    OpeningTaxWdv = openingTax,
                    TaxDepreciation = taxDepr,
                    ClosingTaxWdv = closingTax,
                    TimingDifference = timingDiff,
                    DeferredTaxImpact = deferredTax,
                    DaysPutToUseInYear = daysPutToUse,
                    IsUnder180Days = isUnder180,
                    CalculatedAt = DateTime.UtcNow
                };
                db.AssetDepreciationEntries.Add(entry);
                results.Add(entry);
            }
        }

        await db.SaveChangesAsync(ct);

        return await GetDepreciationEntriesAsync(req.AssetId, req.ClientId, req.FinancialYear, ct);
    }

    public async Task<IReadOnlyList<AssetDepreciationEntryDto>> GetDepreciationEntriesAsync(
        Guid? assetId, Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var q = db.AssetDepreciationEntries
            .AsNoTracking()
            .Include(e => e.FixedAsset)
            .AsQueryable();

        if (assetId.HasValue) q = q.Where(e => e.FixedAssetId == assetId.Value);
        if (clientId.HasValue) q = q.Where(e => e.FixedAsset!.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(e => e.FinancialYear == financialYear);

        var list = await q.OrderBy(e => e.FixedAsset!.AssetTag).ThenByDescending(e => e.FinancialYear).ToListAsync(ct);

        return list.Select(e => new AssetDepreciationEntryDto(
            e.Id,
            e.FixedAssetId,
            e.FixedAsset?.AssetTag ?? string.Empty,
            e.FixedAsset?.Name ?? string.Empty,
            e.FixedAsset?.AssetClass ?? AssetClass.PlantAndMachinery,
            e.FinancialYear,
            e.OpeningBookValue,
            e.BookDepreciation,
            e.ClosingBookValue,
            e.OpeningTaxWdv,
            e.TaxDepreciation,
            e.ClosingTaxWdv,
            e.TimingDifference,
            e.DeferredTaxImpact,
            e.DaysPutToUseInYear,
            e.IsUnder180Days,
            e.CalculatedAt
        )).ToList();
    }

    public async Task<AssetPhysicalVerificationDto> RecordVerificationAsync(
        Guid assetId, RecordPhysicalVerificationRequest req, CancellationToken ct = default)
    {
        var asset = await db.FixedAssets.FirstOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new KeyNotFoundException($"Fixed asset {assetId} not found.");

        var verification = new AssetPhysicalVerification
        {
            Id = Guid.NewGuid(),
            FixedAssetId = asset.Id,
            VerificationDate = req.VerificationDate,
            VerifiedBy = req.VerifiedBy.Trim(),
            Status = req.Status,
            Remarks = req.Remarks?.Trim(),
            LocationVerified = req.LocationVerified?.Trim()
        };

        // If asset is reported missing or damaged, reflect in asset status if appropriate
        if (req.Status == PhysicalVerificationStatus.Missing)
        {
            asset.Status = AssetStatus.WrittenOff;
        }

        db.AssetPhysicalVerifications.Add(verification);
        await db.SaveChangesAsync(ct);

        return new AssetPhysicalVerificationDto(
            verification.Id,
            verification.FixedAssetId,
            asset.AssetTag,
            asset.Name,
            verification.VerificationDate,
            verification.VerifiedBy,
            verification.Status,
            verification.Remarks,
            verification.LocationVerified,
            verification.CreatedAt
        );
    }

    public async Task<IReadOnlyList<AssetPhysicalVerificationDto>> GetVerificationsAsync(
        Guid? assetId, Guid? clientId, CancellationToken ct = default)
    {
        var q = db.AssetPhysicalVerifications
            .AsNoTracking()
            .Include(v => v.FixedAsset)
            .AsQueryable();

        if (assetId.HasValue) q = q.Where(v => v.FixedAssetId == assetId.Value);
        if (clientId.HasValue) q = q.Where(v => v.FixedAsset!.ClientId == clientId.Value);

        var list = await q.OrderByDescending(v => v.VerificationDate).ToListAsync(ct);

        return list.Select(v => new AssetPhysicalVerificationDto(
            v.Id,
            v.FixedAssetId,
            v.FixedAsset?.AssetTag ?? string.Empty,
            v.FixedAsset?.Name ?? string.Empty,
            v.VerificationDate,
            v.VerifiedBy,
            v.Status,
            v.Remarks,
            v.LocationVerified,
            v.CreatedAt
        )).ToList();
    }

    private static FixedAssetDto ToDto(FixedAsset a)
    {
        var latestEntry = a.DepreciationEntries.OrderByDescending(e => e.FinancialYear).FirstOrDefault();
        var latestBook = latestEntry?.ClosingBookValue ?? a.GrossCost;
        var latestTax = latestEntry?.ClosingTaxWdv ?? a.GrossCost;

        return new FixedAssetDto(
            a.Id,
            a.ClientId,
            a.Client?.Name ?? string.Empty,
            a.Client?.Code ?? string.Empty,
            a.AssetTag,
            a.Name,
            a.AssetClass,
            a.PurchaseDate,
            a.CapitalizationDate,
            a.PurchaseCost,
            a.InwardFreight,
            a.InstallationCost,
            a.PreOperatingCost,
            a.GstItcClaimed,
            a.GrossCost,
            a.SalvageValue,
            a.UsefulLifeYears,
            a.CompanyDepreciationMethod,
            a.ShiftType,
            a.IncomeTaxBlockRatePercent,
            a.IsEligibleForAdditionalDepreciation,
            a.Location,
            a.Department,
            a.CustodianName,
            a.Status,
            a.DisposalDate,
            a.DisposalProceeds,
            a.DisposalRemarks,
            latestBook,
            latestTax,
            a.CreatedAt
        );
    }
}
