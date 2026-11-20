using System.Text.RegularExpressions;
using CaDesk.Domain.Mca;

namespace CaDesk.Application.Mca;

public static class McaFeeCalculator
{
    private static readonly Regex CinRegex = new(@"^[LU][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}$", RegexOptions.Compiled);
    private static readonly Regex DinRegex = new(@"^[0-9]{8}$", RegexOptions.Compiled);

    public static bool IsValidCin(string? cin) =>
        !string.IsNullOrWhiteSpace(cin) && CinRegex.IsMatch(cin.Trim().ToUpperInvariant());

    public static bool IsValidDin(string? din) =>
        !string.IsNullOrWhiteSpace(din) && DinRegex.IsMatch(din.Trim());

    public static decimal DetermineStandardNormalFee(string formType, decimal paidUpCapital)
    {
        if (formType.StartsWith("DIR-3_KYC", StringComparison.OrdinalIgnoreCase))
            return 0m;

        if (paidUpCapital < 100_000m) return 200m;
        if (paidUpCapital < 500_000m) return 300m;
        if (paidUpCapital < 2_500_000m) return 400m;
        if (paidUpCapital < 10_000_000m) return 500m;
        return 600m;
    }

    public static McaLateFeeCalculationResult CalculateLateFee(
        string formType,
        DateOnly dueDate,
        DateOnly filingDate,
        decimal? paidUpCapital = null)
    {
        var capital = paidUpCapital ?? 100_000m;
        var normalFee = DetermineStandardNormalFee(formType, capital);

        if (filingDate <= dueDate)
        {
            return new McaLateFeeCalculationResult(
                FormType: formType,
                DueDate: dueDate,
                FilingDate: filingDate,
                DaysDelayed: 0,
                NormalFee: normalFee,
                LateFee: 0m,
                TotalFee: normalFee,
                LegalProvisionDescription: "Filed on or before statutory due date. No additional / late fee applicable."
            );
        }

        var daysDelayed = filingDate.DayNumber - dueDate.DayNumber;

        // DIR-3 KYC Annual Filing: 30th Sept due date; flat ₹5,000 late fee post due date
        if (formType.StartsWith("DIR-3_KYC", StringComparison.OrdinalIgnoreCase))
        {
            var kycLateFee = 5000m;
            return new McaLateFeeCalculationResult(
                FormType: formType,
                DueDate: dueDate,
                FilingDate: filingDate,
                DaysDelayed: daysDelayed,
                NormalFee: 0m,
                LateFee: kycLateFee,
                TotalFee: kycLateFee,
                LegalProvisionDescription: "DIR-3 KYC late filing: Flat statutory additional fee of ₹5,000 per DIN under Rule 12A of Companies (Appointment and Qualification of Directors) Rules, 2014."
            );
        }

        // Annual Filings (AOC-4 & MGT-7): Section 403 second proviso: ₹100 per day of delay without upper ceiling
        if (formType is McaFormType.Aoc4 or McaFormType.Aoc4Xbrl or McaFormType.Aoc4Cfs or McaFormType.Mgt7 or McaFormType.Mgt7A)
        {
            var lateFee = daysDelayed * 100m;
            return new McaLateFeeCalculationResult(
                FormType: formType,
                DueDate: dueDate,
                FilingDate: filingDate,
                DaysDelayed: daysDelayed,
                NormalFee: normalFee,
                LateFee: lateFee,
                TotalFee: normalFee + lateFee,
                LegalProvisionDescription: $"Section 403(1) second proviso: Additional fee of ₹100 per day of delay ({daysDelayed} days × ₹100) applicable for annual financial and secretarial returns."
            );
        }

        // Event-based forms (DIR-12, MGT-14, ADT-1, DPT-3, MSME-1, CHG-1 etc.) slab-based additional fee
        decimal multiplier;
        string slabText;
        if (daysDelayed <= 30)
        {
            multiplier = 2m;
            slabText = "Up to 30 days delay: 2 times of normal filing fee";
        }
        else if (daysDelayed <= 60)
        {
            multiplier = 4m;
            slabText = "More than 30 days and up to 60 days delay: 4 times of normal filing fee";
        }
        else if (daysDelayed <= 90)
        {
            multiplier = 6m;
            slabText = "More than 60 days and up to 90 days delay: 6 times of normal filing fee";
        }
        else if (daysDelayed <= 180)
        {
            multiplier = 10m;
            slabText = "More than 90 days and up to 180 days delay: 10 times of normal filing fee";
        }
        else
        {
            multiplier = 12m;
            slabText = "Beyond 180 days delay: 12 times of normal filing fee";
        }

        var additionalFee = normalFee * multiplier;
        return new McaLateFeeCalculationResult(
            FormType: formType,
            DueDate: dueDate,
            FilingDate: filingDate,
            DaysDelayed: daysDelayed,
            NormalFee: normalFee,
            LateFee: additionalFee,
            TotalFee: normalFee + additionalFee,
            LegalProvisionDescription: $"Companies (Registration Offices and Fees) Rules, 2014 - Table of Additional Fees: {slabText} ({daysDelayed} days delay)."
        );
    }
}
