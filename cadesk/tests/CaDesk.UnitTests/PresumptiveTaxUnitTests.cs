using System;
using CaDesk.Domain.Presumptive;
using Xunit;

namespace CaDesk.UnitTests;

public class PresumptiveTaxUnitTests
{
    [Fact]
    public void Section44AD_StandardTurnoverLimit_AppliedWhenCashExceedsFivePercent()
    {
        // Gross turnover ₹2.10 Cr, Cash ₹20 Lakhs (approx 9.5% > 5%)
        decimal digital = 19_000_000m;
        decimal cash = 2_000_000m;
        decimal declaredProfit = 1_500_000m;

        var r = PresumptiveTaxEngine.CalculateSection44AD(
            digitalTurnover: digital,
            cashTurnover: cash,
            declaredProfit: declaredProfit
        );

        Assert.Equal(21_000_000m, r.GrossTurnover);
        Assert.False(r.IsDigitalThresholdEligible);
        Assert.Equal(PresumptiveTaxEngine.Sec44ADStandardLimit, r.TurnoverLimit); // ₹2 Cr
        Assert.False(r.IsTurnoverWithinLimit); // 2.10 Cr > 2.00 Cr
        Assert.True(r.IsAuditMandatory);
        Assert.Contains("exceeds the statutory presumptive ceiling", r.AuditReason);
    }

    [Fact]
    public void Section44AD_EnhancedTurnoverLimit_AppliedWhenCashBelowFivePercent()
    {
        // Gross turnover ₹2.50 Cr, Cash ₹5 Lakhs (2% <= 5%)
        decimal digital = 24_500_000m;
        decimal cash = 500_000m;
        decimal declaredProfit = 1_600_000m;

        var r = PresumptiveTaxEngine.CalculateSection44AD(
            digitalTurnover: digital,
            cashTurnover: cash,
            declaredProfit: declaredProfit
        );

        Assert.Equal(25_000_000m, r.GrossTurnover);
        Assert.True(r.IsDigitalThresholdEligible);
        Assert.Equal(PresumptiveTaxEngine.Sec44ADEnhancedLimit, r.TurnoverLimit); // ₹3 Cr
        Assert.True(r.IsTurnoverWithinLimit); // 2.50 Cr <= 3.00 Cr
        Assert.False(r.IsAuditMandatory);
    }

    [Fact]
    public void Section44AD_MinimumDeemedProfit_SplitSixPercentDigitalAndEightPercentCash()
    {
        // Digital ₹1.00 Cr, Cash ₹10 Lakhs
        decimal digital = 10_000_000m; // 6% = ₹6,00,000
        decimal cash = 1_000_000m;    // 8% = ₹80,000
        decimal declaredProfit = 680_000m;

        var r = PresumptiveTaxEngine.CalculateSection44AD(
            digitalTurnover: digital,
            cashTurnover: cash,
            declaredProfit: declaredProfit
        );

        Assert.Equal(680_000m, r.MinimumDeemedProfit);
        Assert.False(r.IsAuditMandatory);
    }

    [Fact]
    public void Section44ADA_CalculatesFiftyPercentMinimumProfit_AndTestsEnhancedLimit()
    {
        // Gross receipts ₹60 Lakhs, Digital ₹58 Lakhs, Cash ₹2 Lakhs (3.33% <= 5%)
        decimal digital = 5_800_000m;
        decimal cash = 200_000m;
        decimal declaredProfit = 3_200_000m;

        var r = PresumptiveTaxEngine.CalculateSection44ADA(
            digitalReceipts: digital,
            cashReceipts: cash,
            declaredProfit: declaredProfit
        );

        Assert.Equal(6_000_000m, r.GrossReceipts);
        Assert.True(r.IsDigitalThresholdEligible);
        Assert.Equal(PresumptiveTaxEngine.Sec44ADAEnhancedLimit, r.ReceiptsLimit); // ₹75 Lakhs
        Assert.True(r.IsWithinLimit);
        Assert.Equal(3_000_000m, r.MinimumDeemedProfit); // 50% of ₹60L
        Assert.False(r.IsAuditMandatory);
    }

    [Fact]
    public void Section44AE_HeavyAndLightVehicles_DeemedProfitCalculation()
    {
        // Heavy vehicle: 18 MT, 8 months -> ₹1,000/ton/month = ₹18,000 * 8 = ₹1,44,000
        var heavy = PresumptiveTaxEngine.CalculateSection44AeVehicle(
            vehicleType: VehicleType.Heavy,
            grossVehicleWeightTons: 18m,
            monthsOwned: 8
        );

        Assert.Equal(18_000m, heavy.MonthlyRate);
        Assert.Equal(144_000m, heavy.DeemedAnnualProfit);

        // Light vehicle: Other, 12 months -> ₹7,500/month = ₹7,500 * 12 = ₹90,000
        var light = PresumptiveTaxEngine.CalculateSection44AeVehicle(
            vehicleType: VehicleType.Other,
            grossVehicleWeightTons: 7.5m,
            monthsOwned: 12
        );

        Assert.Equal(7500m, light.MonthlyRate);
        Assert.Equal(90_000m, light.DeemedAnnualProfit);
    }

    [Fact]
    public void Section44AD_Lockout_FiveConsecutiveYearsPenalty()
    {
        // Opted out in AY 2021-22 -> Lockout expires in AY 2026-27 (5 years)
        var (isActive1, expiryAy1) = PresumptiveTaxEngine.EvaluateLockout(
            optedOutPriorYear: true,
            optOutAy: "2021-22",
            currentAy: "2024-25"
        );
        Assert.True(isActive1);
        Assert.Equal("2026-27", expiryAy1);

        // In AY 2028-29, lockout should have expired
        var (isActive2, expiryAy2) = PresumptiveTaxEngine.EvaluateLockout(
            optedOutPriorYear: true,
            optOutAy: "2021-22",
            currentAy: "2028-29"
        );
        Assert.False(isActive2);
        Assert.Equal("2026-27", expiryAy2);
    }

    [Fact]
    public void Section44AB_MandatoryAudit_TriggeredWhenDeclaredProfitBelowDeemed()
    {
        // 44AD with Digital ₹1 Cr (min deemed profit = ₹6 Lakhs), but declared ₹4 Lakhs
        var r = PresumptiveTaxEngine.CalculateSection44AD(
            digitalTurnover: 10_000_000m,
            cashTurnover: 0m,
            declaredProfit: 400_000m
        );

        Assert.Equal(600_000m, r.MinimumDeemedProfit);
        Assert.True(r.IsAuditMandatory);
        Assert.Contains("lower than the statutory minimum deemed profit", r.AuditReason);
    }

    [Fact]
    public void ValidateMinimalFinancials_BalanceSheetDifference_Detected()
    {
        // Assets: Debtors 5L + Stock 3L + Cash 50K + Bank 1.5L = 10L
        // Liab: Creditors 4L + Loans 1L + Capital 5L = 10L
        var balanced = PresumptiveTaxEngine.ValidateMinimalFinancials(
            sundryDebtors: 500_000m,
            sundryCreditors: 400_000m,
            stockInTrade: 300_000m,
            cashBalance: 50_000m,
            bankBalance: 150_000m,
            fixedAssets: 0m,
            otherAssets: 0m,
            partnerOrProprietorCapital: 500_000m,
            loansAndBorrowings: 100_000m
        );

        Assert.Equal(1_000_000m, balanced.TotalAssets);
        Assert.Equal(1_000_000m, balanced.TotalLiabilitiesAndCapital);
        Assert.Equal(0m, balanced.Difference);
        Assert.True(balanced.IsBalanced);
        Assert.Empty(balanced.ValidationAlerts);

        // When Creditors is ₹2 Lakhs, variance is ₹2 Lakhs
        var imbalanced = PresumptiveTaxEngine.ValidateMinimalFinancials(
            sundryDebtors: 500_000m,
            sundryCreditors: 200_000m,
            stockInTrade: 300_000m,
            cashBalance: 50_000m,
            bankBalance: 150_000m,
            fixedAssets: 0m,
            otherAssets: 0m,
            partnerOrProprietorCapital: 500_000m,
            loansAndBorrowings: 100_000m
        );

        Assert.False(imbalanced.IsBalanced);
        Assert.Equal(200_000m, imbalanced.Difference);
        Assert.Contains(imbalanced.ValidationAlerts, a => a.Contains("Balance Sheet variance"));
    }
}
