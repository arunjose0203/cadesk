using System;
using System.Collections.Generic;
using CaDesk.Domain.AntiProfiteering;
using Xunit;

namespace CaDesk.UnitTests;

public class AntiProfiteeringUnitTests
{
    [Fact]
    public void EvaluateSku_FullPassThrough_IsCompliant()
    {
        var sku = new SkuProfiteeringItem
        {
            ProductCode = "SKU-ELEC-01",
            ProductName = "Inverter Refrigerator 260L",
            PreCutBasePrice = 20000m,
            OldGstRate = 28m,
            NewGstRate = 18m,
            PostCutActualBasePrice = 20000m, // Base price maintained
            QuantitySoldPostCut = 150m
        };

        AntiProfiteeringEngine.EvaluateSku(sku);

        Assert.Equal(25600m, sku.PreCutSellingPrice);       // 20000 * 1.28
        Assert.Equal(23600m, sku.CommensuratePriceRequired); // 20000 * 1.18
        Assert.Equal(23600m, sku.PostCutActualSellingPrice); // 20000 * 1.18
        Assert.Equal(0m, sku.BasePriceIncreasePercentage);
        Assert.Equal(0m, sku.ProfiteeringPerUnit);
        Assert.Equal(0m, sku.TotalProfiteeredAmount);
        Assert.Equal("Compliant", sku.ComplianceStatus);
    }

    [Fact]
    public void EvaluateSku_BasePriceHike_ZeroBenefitPassed_IsProfiteered()
    {
        var sku = new SkuProfiteeringItem
        {
            ProductCode = "SKU-CHOC-01",
            ProductName = "Dark Cocoa Bar 100g",
            PreCutBasePrice = 100m,
            OldGstRate = 28m, // PreCutSelling = 128
            NewGstRate = 18m, // Commensurate = 118
            PostCutActualBasePrice = 108.47m, // Hiked base price so selling price remains ~128
            QuantitySoldPostCut = 2000m
        };

        AntiProfiteeringEngine.EvaluateSku(sku);

        Assert.Equal(128m, sku.PreCutSellingPrice);
        Assert.Equal(118m, sku.CommensuratePriceRequired);
        Assert.Equal(127.99m, sku.PostCutActualSellingPrice); // 108.47 * 1.18
        Assert.Equal(8.47m, sku.BasePriceIncreasePercentage);
        Assert.Equal(9.99m, sku.ProfiteeringPerUnit); // 127.99 - 118
        Assert.Equal(19980m, sku.TotalProfiteeredAmount); // 9.99 * 2000
        Assert.Equal("Profiteered", sku.ComplianceStatus);
    }

    [Fact]
    public void EvaluateSku_PartialBenefitPassed_IsDeficient()
    {
        var sku = new SkuProfiteeringItem
        {
            ProductCode = "SKU-FOOT-01",
            ProductName = "Leather Formal Shoes",
            PreCutBasePrice = 2000m,
            OldGstRate = 18m, // PreCutSelling = 2360
            NewGstRate = 12m, // Commensurate = 2000 * 1.12 = 2240
            PostCutActualBasePrice = 2050m, // Mild base price hike
            QuantitySoldPostCut = 500m
        };

        AntiProfiteeringEngine.EvaluateSku(sku);

        Assert.Equal(2360m, sku.PreCutSellingPrice);
        Assert.Equal(2240m, sku.CommensuratePriceRequired);
        Assert.Equal(2296m, sku.PostCutActualSellingPrice); // 2050 * 1.12
        Assert.Equal(2.50m, sku.BasePriceIncreasePercentage);
        Assert.Equal(56m, sku.ProfiteeringPerUnit); // 2296 - 2240
        Assert.Equal(28000m, sku.TotalProfiteeredAmount); // 56 * 500
        Assert.Equal("Deficient", sku.ComplianceStatus);
    }

    [Fact]
    public void CalculateCwfSplit_FiftyFiftySplit_WithStatutory18PercentInterest()
    {
        decimal unrefunded = 100000m;
        int delayDays = 60;

        var (central, state, interest) = AntiProfiteeringEngine.CalculateCwfSplit(unrefunded, delayDays);

        Assert.Equal(50000m, central);
        Assert.Equal(50000m, state);
        // 100,000 * 0.18 * (60 / 365) = 2958.90
        Assert.Equal(2958.90m, interest);
    }

    [Fact]
    public void CalculateCwfSplit_ZeroAmount_ReturnsZero()
    {
        var (central, state, interest) = AntiProfiteeringEngine.CalculateCwfSplit(0m, 120);
        Assert.Equal(0m, central);
        Assert.Equal(0m, state);
        Assert.Equal(0m, interest);
    }

    [Fact]
    public void GenerateBaselineRateNotifications_GeneratesStandardLandmarkNotifications()
    {
        var firmId = Guid.NewGuid();
        var baselines = AntiProfiteeringEngine.GenerateBaselineRateNotifications(firmId);

        Assert.True(baselines.Count >= 5);
        Assert.Contains(baselines, b => b.NotificationNumber.Contains("41/2017") && b.HsnOrSacCode == "9963");
        Assert.Contains(baselines, b => b.NotificationNumber.Contains("18/2018") && b.HsnOrSacCode == "8418");
        Assert.Contains(baselines, b => b.NotificationNumber.Contains("14/2019") && b.HsnOrSacCode == "8703");
    }

    [Fact]
    public void GenerateStatutoryAuditReport_ContainsSection171AndClientDetails()
    {
        var assessment = new AntiProfiteeringAssessment
        {
            Id = Guid.NewGuid(),
            AssessmentReference = "APA-2024-001",
            FinancialYear = "2024-25",
            AuditPeriodFrom = new DateTime(2024, 8, 1),
            AuditPeriodTo = new DateTime(2024, 10, 31),
            NotificationReference = "18/2018-Central Tax (Rate)",
            RateCutEffectiveDate = new DateTime(2018, 7, 27),
            LeadAuditorName = "CA Vikram Joshi, FCA, DISA",
            Udin = "24078512AAAAAP9981",
            TotalSkusAudited = 2,
            ProfiteeredSkusCount = 1,
            TotalTurnoverPostCut = 5000000m,
            TotalUndueProfiteeredAmount = 150000m,
            TotalCustomerRefundedAmount = 50000m,
            TotalCwfDepositedAmount = 0m,
            SkuItems = new List<SkuProfiteeringItem>
            {
                new()
                {
                    ProductCode = "SKU-REF-01",
                    ProductName = "Single Door Refrigerator",
                    PreCutBasePrice = 12000m,
                    OldGstRate = 28m,
                    CommensuratePriceRequired = 14160m,
                    PostCutActualSellingPrice = 14160m,
                    TotalProfiteeredAmount = 0m,
                    ComplianceStatus = "Compliant"
                },
                new()
                {
                    ProductCode = "SKU-REF-02",
                    ProductName = "Double Door Frost Free",
                    PreCutBasePrice = 25000m,
                    OldGstRate = 28m,
                    CommensuratePriceRequired = 29500m,
                    PostCutActualSellingPrice = 30500m,
                    TotalProfiteeredAmount = 150000m,
                    ComplianceStatus = "Profiteered"
                }
            }
        };

        var report = AntiProfiteeringEngine.GenerateStatutoryAuditReport(assessment, "Voltas Home Appliances Limited", "27AAACV9876F1Z2");

        Assert.Contains("Voltas Home Appliances Limited", report);
        Assert.Contains("27AAACV9876F1Z2", report);
        Assert.Contains("Section 171", report);
        Assert.Contains("Rule 122-137", report);
        Assert.Contains("24078512AAAAAP9981", report);
        Assert.Contains("ADVERSE FINDING / QUALIFICATION", report);
        Assert.Contains("Consumer Welfare Funds", report);
    }
}
