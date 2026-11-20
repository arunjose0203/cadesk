using System;
using System.Collections.Generic;
using System.Linq;
using CaDesk.Domain.TransferPricing;
using Xunit;

namespace CaDesk.UnitTests;

public class TransferPricingUnitTests
{
    [Fact]
    public void CalculateArmLengthRange_SixOrMoreComparables_UsesRule10CAPercentileRange()
    {
        // 10 comparables sorted
        // Values: 10%, 12%, 14%, 15%, 16%, 18%, 20%, 22%, 24%, 26%
        var margins = new List<decimal> { 10m, 12m, 14m, 15m, 16m, 18m, 20m, 22m, 24m, 26m };

        var range = TransferPricingEngine.CalculateArmLengthRange(margins);

        Assert.True(range.UseRange);
        Assert.Equal(10, range.DataCount);
        // 10 * 0.35 = 3.5 -> 4th item = 15m
        Assert.Equal(15.00m, range.RangeMin);
        // 10 * 0.65 = 6.5 -> 7th item = 20m
        Assert.Equal(20.00m, range.RangeMax);
        // 10 * 0.50 = 5.0 (int) -> avg of 5th (16) and 6th (18) = 17m
        Assert.Equal(17.00m, range.MedianOrMean);
        Assert.Contains("Rule 10CA 35th percentile", range.Explanation);
    }

    [Fact]
    public void CalculateArmLengthRange_LessThanSixComparables_UsesArithmeticMeanWithTolerance()
    {
        // 4 comparables: 14%, 16%, 18%, 20%
        // Mean = 17%
        // +/- 3% tolerance = 17 * (1 - 0.03) = 16.49m to 17 * (1 + 0.03) = 17.51m
        var margins = new List<decimal> { 14m, 16m, 18m, 20m };

        var range = TransferPricingEngine.CalculateArmLengthRange(margins, tolerancePct: 3.0m);

        Assert.False(range.UseRange);
        Assert.Equal(4, range.DataCount);
        Assert.Equal(17.00m, range.MedianOrMean);
        Assert.Equal(16.49m, range.RangeMin);
        Assert.Equal(17.51m, range.RangeMax);
        Assert.Contains("Arithmetic Mean", range.Explanation);
    }

    [Fact]
    public void EvaluateTransaction_MarginWithinRange_IsAtArmLength_ZeroAdjustment()
    {
        var tx = new TransferPricingTransaction
        {
            ForeignCurrencyAmount = 1_000_000m,
            ExchangeRate = 83.50m,
            TestedPartyCostBase = 70_000_000m,
            TestedPartyMargin = 16.50m // Falls between 15% (35th) and 20% (65th)
        };

        var compMargins = new List<decimal> { 10m, 12m, 14m, 15m, 16m, 18m, 20m, 22m, 24m, 26m };

        TransferPricingEngine.EvaluateTransaction(tx, compMargins);

        Assert.Equal(83_500_000m, tx.InrValue);
        Assert.True(tx.IsWithinArmLength);
        Assert.Equal(0m, tx.AlpAdjustmentAmount);
        Assert.Equal(15.00m, tx.BenchmarkRangeMin);
        Assert.Equal(20.00m, tx.BenchmarkRangeMax);
        Assert.Equal(17.00m, tx.BenchmarkMedianOrMean);
    }

    [Fact]
    public void EvaluateTransaction_MarginBelowRange_CalculatesAlpAdjustmentToMedian()
    {
        // Tested party margin is 12% (below 35th percentile of 15%)
        // Median is 17%
        // Base is 10 Crore Total Cost
        // Required margin gap = 17% - 12% = 5%
        // Primary adjustment = 10,00,00,000 * 5% = 50,00,000
        var tx = new TransferPricingTransaction
        {
            ForeignCurrencyAmount = 1_500_000m,
            ExchangeRate = 83.00m,
            TestedPartyCostBase = 100_000_000m,
            TestedPartyMargin = 12.00m
        };

        var compMargins = new List<decimal> { 10m, 12m, 14m, 15m, 16m, 18m, 20m, 22m, 24m, 26m };

        TransferPricingEngine.EvaluateTransaction(tx, compMargins);

        Assert.False(tx.IsWithinArmLength);
        Assert.Equal(17.00m, tx.BenchmarkMedianOrMean);
        Assert.Equal(50_00_000m, tx.AlpAdjustmentAmount);
    }

    [Fact]
    public void EvaluateTransaction_MarginAboveRange_IsAtArmLength()
    {
        // Tested party earned 25% mark-up (more than 65th percentile of 20%)
        // Indian entity earned higher taxable profit in India -> No adverse adjustment
        var tx = new TransferPricingTransaction
        {
            ForeignCurrencyAmount = 500_000m,
            ExchangeRate = 83.00m,
            TestedPartyCostBase = 35_000_000m,
            TestedPartyMargin = 25.00m
        };

        var compMargins = new List<decimal> { 10m, 12m, 14m, 15m, 16m, 18m, 20m, 22m, 24m, 26m };

        TransferPricingEngine.EvaluateTransaction(tx, compMargins);

        Assert.True(tx.IsWithinArmLength);
        Assert.Equal(0m, tx.AlpAdjustmentAmount);
    }

    [Fact]
    public void RecalculateStudySummary_AggregatesInternationalAndSdtVolumesAccurately()
    {
        var study = new TransferPricingStudy
        {
            AssociatedEnterprises = new List<AssociatedEnterprise>
            {
                new() { EnterpriseName = "Alpha Inc USA" },
                new() { EnterpriseName = "Beta Ltd UK" }
            },
            Transactions = new List<TransferPricingTransaction>
            {
                new()
                {
                    TransactionCategory = TransferPricingConstants.CatInternational,
                    InrValue = 50_000_000m,
                    IsWithinArmLength = true,
                    AlpAdjustmentAmount = 0m
                },
                new()
                {
                    TransactionCategory = TransferPricingConstants.CatInternational,
                    InrValue = 30_000_000m,
                    IsWithinArmLength = false,
                    AlpAdjustmentAmount = 4_500_000m
                },
                new()
                {
                    TransactionCategory = TransferPricingConstants.CatSpecifiedDomestic,
                    InrValue = 250_000_000m,
                    IsWithinArmLength = true,
                    AlpAdjustmentAmount = 0m
                }
            }
        };

        TransferPricingEngine.RecalculateStudySummary(study);

        Assert.Equal(2, study.TotalAssociatedEnterprisesCount);
        Assert.Equal(2, study.TotalInternationalTransactionsCount);
        Assert.Equal(80_000_000m, study.TotalInternationalInrValue);
        Assert.Equal(1, study.TotalSdtTransactionsCount);
        Assert.Equal(250_000_000m, study.TotalSdtInrValue);
        Assert.Equal(1, study.TotalAlpAdjustmentsCount);
        Assert.Equal(4_500_000m, study.TotalAlpAdjustmentAmount);
        Assert.Equal(2, study.TransactionsWithinToleranceCount);
        Assert.Equal(1, study.TransactionsOutsideToleranceCount);
    }

    [Fact]
    public void GenerateForm3CEB_GeneratesStandardStatutoryReportWithClauses()
    {
        var ae = new AssociatedEnterprise
        {
            EnterpriseName = "Microsoft Corporation USA",
            CountryOfResidence = "USA",
            TaxIdentificationNumber = "US-987654321"
        };

        var study = new TransferPricingStudy
        {
            StudyReference = "TP-2024-MSFT-01",
            FinancialYear = "2024-25",
            AssessmentYear = "2025-26",
            AccountantName = "CA Vikram Singhania",
            AccountantFirmName = "Singhania & Associates LLP",
            MembershipNumber = "045678",
            FirmRegistrationNumber = "012345N",
            Udin = "25045678B000123456",
            AssociatedEnterprises = new List<AssociatedEnterprise> { ae },
            Transactions = new List<TransferPricingTransaction>
            {
                new()
                {
                    AssociatedEnterprise = ae,
                    TransactionCategory = TransferPricingConstants.CatInternational,
                    ClauseNumber = 13,
                    DescriptionOfTransaction = "Provision of Software Development & Engineering Services",
                    CurrencyCode = "USD",
                    ForeignCurrencyAmount = 1_000_000m,
                    ExchangeRate = 83.50m,
                    InrValue = 83_500_000m,
                    MostAppropriateMethod = TransferPricingConstants.MethodTnmm,
                    ProfitLevelIndicator = TransferPricingConstants.PliOpTc,
                    TestedPartyMargin = 12.00m,
                    BenchmarkRangeMin = 15.00m,
                    BenchmarkRangeMax = 20.00m,
                    BenchmarkMedianOrMean = 17.00m,
                    IsWithinArmLength = false,
                    AlpAdjustmentAmount = 3_500_000m
                }
            }
        };

        TransferPricingEngine.RecalculateStudySummary(study);

        var report = TransferPricingEngine.GenerateForm3CEB(
            study,
            "Tech Innovations India Private Limited",
            "AABCT1234F",
            "Cyber City, Gurugram, Haryana");

        Assert.Contains("FORM NO. 3CEB", report);
        Assert.Contains("Tech Innovations India Private Limited", report);
        Assert.Contains("AABCT1234F", report);
        Assert.Contains("Microsoft Corporation USA", report);
        Assert.Contains("Provision of Software Development & Engineering Services", report);
        Assert.Contains("Clause 13", report);
        Assert.Contains("Primary TP Adjustment", report);
        Assert.Contains(3_500_000m.ToString("N2"), report);
        Assert.Contains("25045678B000123456", report);
        Assert.Contains("CA Vikram Singhania", report);
    }
}
