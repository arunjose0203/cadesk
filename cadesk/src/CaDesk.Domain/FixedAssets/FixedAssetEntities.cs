using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.FixedAssets;

public enum AssetClass
{
    Building = 1,
    PlantAndMachinery = 2,
    ComputersAndSoftware = 3,
    FurnitureAndFixtures = 4,
    OfficeEquipment = 5,
    Vehicles = 6,
    Intangibles = 7
}

public enum CompanyDepreciationMethod
{
    SLM = 1, // Straight Line Method
    WDV = 2  // Written Down Value
}

public enum ShiftType
{
    SingleShift = 1, // 1.0x standard rate
    DoubleShift = 2, // 1.5x standard rate (+50%)
    TripleShift = 3  // 2.0x standard rate (+100%)
}

public enum AssetStatus
{
    Active = 1,
    Disposed = 2,
    Scrapped = 3,
    WrittenOff = 4
}

public enum PhysicalVerificationStatus
{
    FoundAndGood = 1,
    Damaged = 2,
    Missing = 3,
    NeedsMaintenance = 4
}

public class FixedAsset : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; } = AssetClass.PlantAndMachinery;

    public DateOnly PurchaseDate { get; set; }
    public DateOnly CapitalizationDate { get; set; } // Put to use date

    public decimal PurchaseCost { get; set; }
    public decimal InwardFreight { get; set; }
    public decimal InstallationCost { get; set; }
    public decimal PreOperatingCost { get; set; }
    public decimal GstItcClaimed { get; set; }
    public decimal GrossCost { get; set; }

    public decimal SalvageValue { get; set; } // Default 5% under Companies Act Sch II
    public decimal UsefulLifeYears { get; set; } = 5;

    public CompanyDepreciationMethod CompanyDepreciationMethod { get; set; } = CompanyDepreciationMethod.SLM;
    public ShiftType ShiftType { get; set; } = ShiftType.SingleShift;

    public decimal IncomeTaxBlockRatePercent { get; set; } = 15;
    public bool IsEligibleForAdditionalDepreciation { get; set; }

    public string? Location { get; set; }
    public string? Department { get; set; }
    public string? CustodianName { get; set; }

    public AssetStatus Status { get; set; } = AssetStatus.Active;
    public DateOnly? DisposalDate { get; set; }
    public decimal? DisposalProceeds { get; set; }
    public string? DisposalRemarks { get; set; }

    public ICollection<AssetDepreciationEntry> DepreciationEntries { get; set; } = new List<AssetDepreciationEntry>();
    public ICollection<AssetPhysicalVerification> PhysicalVerifications { get; set; } = new List<AssetPhysicalVerification>();

    public void RecalculateGrossCost()
    {
        GrossCost = Math.Max(0m, PurchaseCost + InwardFreight + InstallationCost + PreOperatingCost - GstItcClaimed);
        if (SalvageValue == 0m && GrossCost > 0m)
        {
            SalvageValue = Math.Round(GrossCost * 0.05m, 2); // Default 5%
        }
    }
}

public class AssetDepreciationEntry : TenantEntity
{
    public Guid FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }

    public string FinancialYear { get; set; } = string.Empty; // e.g. 2024-25

    public decimal OpeningBookValue { get; set; }
    public decimal BookDepreciation { get; set; }
    public decimal ClosingBookValue { get; set; }

    public decimal OpeningTaxWdv { get; set; }
    public decimal TaxDepreciation { get; set; }
    public decimal ClosingTaxWdv { get; set; }

    public decimal TimingDifference { get; set; } // TaxDepreciation - BookDepreciation
    public decimal DeferredTaxImpact { get; set; } // TimingDifference * TaxRate (Positive = DTL, Negative = DTA)

    public int DaysPutToUseInYear { get; set; }
    public bool IsUnder180Days { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class AssetPhysicalVerification : TenantEntity
{
    public Guid FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }

    public DateOnly VerificationDate { get; set; }
    public string VerifiedBy { get; set; } = string.Empty;
    public PhysicalVerificationStatus Status { get; set; } = PhysicalVerificationStatus.FoundAndGood;
    public string? Remarks { get; set; }
    public string? LocationVerified { get; set; }
}

public static class DualDepreciationEngine
{
    public const decimal DefaultCorporateTaxRate = 0.2517m; // 25.168% Section 115BAA corporate tax rate

    public static (DateOnly StartDate, DateOnly EndDate) ParseFinancialYear(string financialYear)
    {
        // Expected format: "2024-25" or "2024-2025"
        var parts = financialYear.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var startYear))
        {
            var curYear = DateTime.UtcNow.Month >= 4 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
            return (new DateOnly(curYear, 4, 1), new DateOnly(curYear + 1, 3, 31));
        }

        return (new DateOnly(startYear, 4, 1), new DateOnly(startYear + 1, 3, 31));
    }

    public static (decimal BookDepreciation, decimal ClosingBookValue, int ActiveDays) CalculateScheduleIIDepreciation(
        decimal grossCost,
        decimal salvageValue,
        decimal usefulLifeYears,
        CompanyDepreciationMethod method,
        ShiftType shiftType,
        decimal openingBookValue,
        DateOnly capitalizationDate,
        DateOnly? disposalDate,
        DateOnly fyStartDate,
        DateOnly fyEndDate)
    {
        if (openingBookValue <= salvageValue || usefulLifeYears <= 0m || grossCost <= 0m)
        {
            return (0m, openingBookValue, 0);
        }

        if (capitalizationDate > fyEndDate)
        {
            return (0m, openingBookValue, 0);
        }

        if (disposalDate.HasValue && disposalDate.Value < fyStartDate)
        {
            return (0m, openingBookValue, 0);
        }

        var activeStart = capitalizationDate > fyStartDate ? capitalizationDate : fyStartDate;
        var activeEnd = disposalDate.HasValue && disposalDate.Value < fyEndDate ? disposalDate.Value : fyEndDate;

        if (activeEnd < activeStart)
        {
            return (0m, openingBookValue, 0);
        }

        var activeDays = (activeEnd.DayNumber - activeStart.DayNumber) + 1;
        var totalDaysInFy = (fyEndDate.DayNumber - fyStartDate.DayNumber) + 1;

        decimal shiftMultiplier = shiftType switch
        {
            ShiftType.DoubleShift => 1.5m,
            ShiftType.TripleShift => 2.0m,
            _ => 1.0m
        };

        decimal fullYearDepr;

        if (method == CompanyDepreciationMethod.SLM)
        {
            var depreciableBase = Math.Max(0m, grossCost - salvageValue);
            var baseAnnual = depreciableBase / usefulLifeYears;
            fullYearDepr = baseAnnual * shiftMultiplier;
        }
        else // WDV
        {
            // Formula: Rate = 1 - (Salvage / Gross)^(1 / Life)
            if (salvageValue > 0m && salvageValue < grossCost)
            {
                double ratio = (double)(salvageValue / grossCost);
                double rate = 1.0 - Math.Pow(ratio, 1.0 / (double)usefulLifeYears);
                fullYearDepr = openingBookValue * (decimal)rate * shiftMultiplier;
            }
            else
            {
                var baseAnnual = grossCost / usefulLifeYears;
                fullYearDepr = baseAnnual * shiftMultiplier;
            }
        }

        var proratedDepr = Math.Round(fullYearDepr * ((decimal)activeDays / totalDaysInFy), 2);
        var maxAllowedDepr = Math.Max(0m, openingBookValue - salvageValue);
        var finalBookDepr = Math.Min(proratedDepr, maxAllowedDepr);
        var closingBookValue = Math.Round(openingBookValue - finalBookDepr, 2);

        return (finalBookDepr, closingBookValue, activeDays);
    }

    public static (decimal TaxDepreciation, decimal ClosingTaxWdv, bool IsUnder180Days, int DaysPutToUseInYear) CalculateSection32Depreciation(
        decimal grossCost,
        decimal incomeTaxBlockRatePercent,
        bool isEligibleForAdditionalDepreciation,
        decimal openingTaxWdv,
        DateOnly capitalizationDate,
        DateOnly? disposalDate,
        DateOnly fyStartDate,
        DateOnly fyEndDate)
    {
        if (openingTaxWdv <= 0m)
        {
            return (0m, 0m, false, 0);
        }

        if (capitalizationDate > fyEndDate)
        {
            return (0m, openingTaxWdv, false, 0);
        }

        if (disposalDate.HasValue && disposalDate.Value < fyStartDate)
        {
            return (0m, openingTaxWdv, false, 0);
        }

        bool isAcquiredInCurrentFy = capitalizationDate >= fyStartDate && capitalizationDate <= fyEndDate;
        int daysPutToUse = isAcquiredInCurrentFy
            ? (fyEndDate.DayNumber - capitalizationDate.DayNumber) + 1
            : (fyEndDate.DayNumber - fyStartDate.DayNumber) + 1;

        bool isUnder180Days = isAcquiredInCurrentFy && daysPutToUse < 180;
        decimal rateFactor = isUnder180Days ? 0.5m : 1.0m;

        decimal normalDepr = Math.Round(openingTaxWdv * (incomeTaxBlockRatePercent / 100m) * rateFactor, 2);

        decimal additionalDepr = 0m;
        if (isEligibleForAdditionalDepreciation && isAcquiredInCurrentFy)
        {
            // Section 32(1)(iia): 20% on actual cost if >= 180 days; 10% in Year 1 if < 180 days
            decimal addlRate = isUnder180Days ? 10.0m : 20.0m;
            additionalDepr = Math.Round(grossCost * (addlRate / 100m), 2);
        }

        decimal totalTaxDepr = Math.Min(openingTaxWdv, normalDepr + additionalDepr);
        decimal closingTaxWdv = Math.Round(openingTaxWdv - totalTaxDepr, 2);

        return (totalTaxDepr, closingTaxWdv, isUnder180Days, daysPutToUse);
    }

    public static (decimal TimingDifference, decimal DeferredTaxImpact) CalculateDeferredTax(
        decimal taxDepreciation,
        decimal bookDepreciation,
        decimal corporateTaxRate = DefaultCorporateTaxRate)
    {
        var timingDiff = Math.Round(taxDepreciation - bookDepreciation, 2);
        var deferredTax = Math.Round(timingDiff * corporateTaxRate, 2);
        return (timingDiff, deferredTax);
    }
}
