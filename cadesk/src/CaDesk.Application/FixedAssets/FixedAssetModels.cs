using CaDesk.Domain.FixedAssets;

namespace CaDesk.Application.FixedAssets;

public record FixedAssetDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string AssetTag,
    string Name,
    AssetClass AssetClass,
    DateOnly PurchaseDate,
    DateOnly CapitalizationDate,
    decimal PurchaseCost,
    decimal InwardFreight,
    decimal InstallationCost,
    decimal PreOperatingCost,
    decimal GstItcClaimed,
    decimal GrossCost,
    decimal SalvageValue,
    decimal UsefulLifeYears,
    CompanyDepreciationMethod CompanyDepreciationMethod,
    ShiftType ShiftType,
    decimal IncomeTaxBlockRatePercent,
    bool IsEligibleForAdditionalDepreciation,
    string? Location,
    string? Department,
    string? CustodianName,
    AssetStatus Status,
    DateOnly? DisposalDate,
    decimal? DisposalProceeds,
    string? DisposalRemarks,
    decimal LatestBookValue,
    decimal LatestTaxWdv,
    DateTime CreatedAt
);

public record AssetDepreciationEntryDto(
    Guid Id,
    Guid FixedAssetId,
    string AssetTag,
    string AssetName,
    AssetClass AssetClass,
    string FinancialYear,
    decimal OpeningBookValue,
    decimal BookDepreciation,
    decimal ClosingBookValue,
    decimal OpeningTaxWdv,
    decimal TaxDepreciation,
    decimal ClosingTaxWdv,
    decimal TimingDifference,
    decimal DeferredTaxImpact,
    int DaysPutToUseInYear,
    bool IsUnder180Days,
    DateTime CalculatedAt
);

public record AssetPhysicalVerificationDto(
    Guid Id,
    Guid FixedAssetId,
    string AssetTag,
    string AssetName,
    DateOnly VerificationDate,
    string VerifiedBy,
    PhysicalVerificationStatus Status,
    string? Remarks,
    string? LocationVerified,
    DateTime CreatedAt
);

public record FixedAssetSummaryDto(
    int TotalAssets,
    int ActiveAssets,
    int DisposedAssets,
    decimal TotalGrossBlock,
    decimal TotalBookDepreciation,
    decimal TotalNetBookValue,
    decimal TotalTaxDepreciation,
    decimal TotalTaxWdv,
    decimal TotalTimingDifference,
    decimal NetDeferredTaxLiability,
    int TotalVerifications,
    int VerifiedInLast12Months,
    decimal VerificationCompliancePercent
);

public record SaveFixedAssetRequest(
    Guid ClientId,
    string AssetTag,
    string Name,
    AssetClass AssetClass,
    DateOnly PurchaseDate,
    DateOnly CapitalizationDate,
    decimal PurchaseCost,
    decimal InwardFreight,
    decimal InstallationCost,
    decimal PreOperatingCost,
    decimal GstItcClaimed,
    decimal? SalvageValue,
    decimal UsefulLifeYears,
    CompanyDepreciationMethod CompanyDepreciationMethod,
    ShiftType ShiftType,
    decimal IncomeTaxBlockRatePercent,
    bool IsEligibleForAdditionalDepreciation,
    string? Location,
    string? Department,
    string? CustodianName
);

public record DisposeAssetRequest(
    DateOnly DisposalDate,
    decimal DisposalProceeds,
    string? Remarks
);

public record RunDualDepreciationRequest(
    Guid ClientId,
    string FinancialYear,
    Guid? AssetId = null,
    decimal? CorporateTaxRate = null
);

public record RecordPhysicalVerificationRequest(
    DateOnly VerificationDate,
    string VerifiedBy,
    PhysicalVerificationStatus Status,
    string? Remarks,
    string? LocationVerified
);
