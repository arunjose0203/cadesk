using System;
using System.Collections.Generic;
using CaDesk.Domain.CostAudit;
using Xunit;

namespace CaDesk.UnitTests;

public class CostAuditUnitTests
{
    [Fact]
    public void EvaluateApplicability_Rule3_Below35Cr_RecordsNotApplicable()
    {
        // ₹30 Crore overall turnover (< ₹35 Cr)
        var result = CostAuditEngine.EvaluateApplicability(
            CostAuditConstants.SectorRegulated,
            overallTurnoverInr: 30_00_00_000m,
            productTurnoverInr: 15_00_00_000m,
            exportTurnoverInr: 5_00_00_000m,
            isSezUnit: false,
            isCaptivePower: false);

        Assert.False(result.IsCostRecordsApplicable);
        Assert.False(result.IsCostAuditApplicable);
        Assert.False(result.IsExportExempt);
        Assert.Contains("Rule 3 Not Applicable", result.Reasoning);
    }

    [Fact]
    public void EvaluateApplicability_Rule4_RegulatedSector_PassesBothThresholds()
    {
        // Regulated sector (Table A): Overall >= 50 Cr, Product >= 25 Cr
        var result = CostAuditEngine.EvaluateApplicability(
            CostAuditConstants.SectorRegulated,
            overallTurnoverInr: 65_00_00_000m,
            productTurnoverInr: 30_00_00_000m,
            exportTurnoverInr: 10_00_00_000m,
            isSezUnit: false,
            isCaptivePower: false);

        Assert.True(result.IsCostRecordsApplicable);
        Assert.True(result.IsCostAuditApplicable);
        Assert.False(result.IsExportExempt);
        Assert.Contains("Table A (Regulated Sector)", result.Reasoning);
        Assert.Contains("Mandatory Cost Audit required", result.Reasoning);
    }

    [Fact]
    public void EvaluateApplicability_Rule4_RegulatedSector_ProductTurnoverBelow25Cr_AuditExempt()
    {
        // Regulated sector (Table A): Overall 60 Cr (>= 50 Cr), but Product is 20 Cr (< 25 Cr)
        var result = CostAuditEngine.EvaluateApplicability(
            CostAuditConstants.SectorRegulated,
            overallTurnoverInr: 60_00_00_000m,
            productTurnoverInr: 20_00_00_000m,
            exportTurnoverInr: 5_00_00_000m,
            isSezUnit: false,
            isCaptivePower: false);

        Assert.True(result.IsCostRecordsApplicable); // Overall >= 35 Cr
        Assert.False(result.IsCostAuditApplicable);  // Product < 25 Cr
        Assert.Contains("FAIL", result.Reasoning);
        Assert.Contains("Exempt from mandatory Cost Audit", result.Reasoning);
    }

    [Fact]
    public void EvaluateApplicability_Rule4_NonRegulatedSector_PassesBothThresholds()
    {
        // Non-regulated sector (Table B): Overall >= 100 Cr, Product >= 35 Cr
        var result = CostAuditEngine.EvaluateApplicability(
            CostAuditConstants.SectorNonRegulated,
            overallTurnoverInr: 120_00_00_000m,
            productTurnoverInr: 45_00_00_000m,
            exportTurnoverInr: 15_00_00_000m,
            isSezUnit: false,
            isCaptivePower: false);

        Assert.True(result.IsCostRecordsApplicable);
        Assert.True(result.IsCostAuditApplicable);
        Assert.False(result.IsExportExempt);
        Assert.Contains("Table B (Non-Regulated Sector)", result.Reasoning);
        Assert.Contains("Mandatory Cost Audit required", result.Reasoning);
    }

    [Fact]
    public void EvaluateApplicability_Rule4_ExportExemption_Over75Percent_AuditExempt()
    {
        // Overall: 100 Cr, Export: 80 Cr (80% > 75%)
        var result = CostAuditEngine.EvaluateApplicability(
            CostAuditConstants.SectorRegulated,
            overallTurnoverInr: 100_00_00_000m,
            productTurnoverInr: 50_00_00_000m,
            exportTurnoverInr: 80_00_00_000m,
            isSezUnit: false,
            isCaptivePower: false);

        Assert.True(result.IsCostRecordsApplicable);
        Assert.False(result.IsCostAuditApplicable); // Exempt due to Rule 4(3)(i)
        Assert.True(result.IsExportExempt);
        Assert.Contains("Rule 4 Exemption (Rule 4(3)(i))", result.Reasoning);
    }

    [Fact]
    public void EvaluateApplicability_Rule4_SezUnit_AuditExempt()
    {
        // SEZ unit exempt under Rule 4(3)(ii)
        var result = CostAuditEngine.EvaluateApplicability(
            CostAuditConstants.SectorNonRegulated,
            overallTurnoverInr: 200_00_00_000m,
            productTurnoverInr: 100_00_00_000m,
            exportTurnoverInr: 20_00_00_000m,
            isSezUnit: true,
            isCaptivePower: false);

        Assert.True(result.IsCostRecordsApplicable);
        Assert.False(result.IsCostAuditApplicable);
        Assert.Contains("Rule 4 Exemption (Rule 4(3)(ii))", result.Reasoning);
    }

    [Fact]
    public void CalculateCra2DueDates_SelectsEarlierOf30DaysOr180Days()
    {
        var fyStart = new DateTime(2024, 4, 1);
        // Board meeting on 15 May 2024 -> 30 days is 14 June 2024, 180 days is 28 Sept 2024
        var boardMeeting = new DateTime(2024, 5, 15);

        var (dueDate, _, _) = CostAuditEngine.CalculateCra2DueDates(boardMeeting, fyStart, null);

        Assert.Equal(boardMeeting.AddDays(30), dueDate);
    }

    [Fact]
    public void CalculateCra4DueDates_Calculates30DaysFromCra3Handover()
    {
        var cra3Handover = new DateTime(2024, 9, 1);
        var (dueDate, status, _) = CostAuditEngine.CalculateCra4DueDates(cra3Handover, null);

        Assert.Equal(cra3Handover.AddDays(30), dueDate);
    }

    [Fact]
    public void CalculateProductCostStatement_CalculatesCostSheetAndMarginsCorrectly()
    {
        var statement = new ProductCostStatement
        {
            ProductCode = "PHARMA-001",
            ProductName = "Paracetamol 500mg Tablets",
            UnitOfMeasurement = "MT",
            InstalledCapacity = 1000m,
            AvailableCapacity = 800m,
            ActualProduction = 600m, // 600 / 800 = 75%
            DirectMaterialsCost = 30_00_000m,
            DirectLaborCost = 10_00_000m,
            UtilitiesCost = 5_00_000m,
            OtherDirectExpenses = 1_00_000m,
            WorksOverheads = 8_00_000m,
            QualityControlCost = 2_00_000m,
            RndExpenses = 1_50_000m,
            AdministrationOverheads = 3_50_000m,
            OpeningWipCost = 2_00_000m,
            ClosingWipCost = 1_00_000m, // net WIP = +1,00,000
            OpeningFinishedGoodsCost = 4_00_000m,
            ClosingFinishedGoodsCost = 2_00_000m, // net FG = +2,00,000
            SellingAndDistributionOverheads = 6_00_000m,
            NetSalesRealization = 80_00_000m
        };

        CostAuditEngine.CalculateProductCostStatement(statement);

        // Prime Cost = 30L + 10L + 5L + 1L = 46L
        Assert.Equal(46_00_000m, statement.PrimeCost);

        // Works Cost = 46L + 8L = 54L
        Assert.Equal(54_00_000m, statement.WorksCost);

        // Cost of Production = 54L + 2L + 1.5L + 3.5L + (2L - 1L) = 62L
        Assert.Equal(62_00_000m, statement.CostOfProduction);

        // Cost of Sales = 62L + (4L - 2L) + 6L = 70L
        Assert.Equal(70_00_000m, statement.CostOfSales);

        // Margin = 80L - 70L = 10L
        Assert.Equal(10_00_000m, statement.ProductMarginInr);

        // Margin Pct = (10L / 80L) * 100 = 12.5%
        Assert.Equal(12.5m, statement.ProductMarginPct);

        // Capacity utilization = (600 / 800) * 100 = 75.0%
        Assert.Equal(75.0m, statement.CapacityUtilizationPct);

        // Unit Cost of Sales = 70L / 600 = 11666.67
        Assert.Equal(Math.Round(70_00_000m / 600m, 2), statement.UnitCostOfSales);
    }

    [Fact]
    public void CalculateMaterialVariance_CalculatesPriceUsageAndTotalCostVariances()
    {
        // Standard: 100 kg @ ₹50 = ₹5,000
        // Actual: 110 kg @ ₹48 = ₹5,280
        // Price Variance: Actual Qty * (Std Price - Actual Price) = 110 * (50 - 48) = +220 (Favorable)
        // Usage Variance: Std Price * (Std Qty - Actual Qty) = 50 * (100 - 110) = -500 (Adverse)
        // Total Variance: 5000 - 5280 = -280 (Adverse)
        var (priceVar, usageVar, totalVar) = CostAuditEngine.CalculateMaterialVariance(
            standardQuantity: 100m,
            actualQuantity: 110m,
            standardPrice: 50m,
            actualPrice: 48m);

        Assert.Equal(220m, priceVar);
        Assert.Equal(-500m, usageVar);
        Assert.Equal(-280m, totalVar);
        Assert.Equal(priceVar + usageVar, totalVar);
    }

    [Fact]
    public void RecalculateReconciliation_ReconcilesCostProfitToFinancialProfit()
    {
        decimal costProfit = 50_00_000m;
        decimal auditedFinancialProfit = 55_00_000m;

        var items = new List<CostAuditReconciliationItem>
        {
            new() { ItemType = CostAuditConstants.RecAddIncomeNotCosted, Description = "Dividend Income", AmountInr = 10_00_000m, Impact = "Positive" },
            new() { ItemType = CostAuditConstants.RecDeductExpenseNotCosted, Description = "Penalties and Fines", AmountInr = 5_00_000m, Impact = "Negative" }
        };

        var (reconciled, additions, deductions, diff) = CostAuditEngine.RecalculateReconciliation(
            costProfit,
            auditedFinancialProfit,
            items);

        // Reconciled = 50L + 10L - 5L = 55L
        Assert.Equal(55_00_000m, reconciled);
        Assert.Equal(10_00_000m, additions);
        Assert.Equal(5_00_000m, deductions);
        Assert.Equal(0m, diff); // Perfectly reconciled
    }

    [Fact]
    public void GenerateFormCra3Report_GeneratesStatutoryReportWithAnnexures()
    {
        var engagement = new CostAuditEngagement
        {
            EngagementReference = "CA-2024-25-001",
            FinancialYear = "2024-25",
            PeriodStart = new DateTime(2024, 4, 1),
            PeriodEnd = new DateTime(2025, 3, 31),
            SectorType = CostAuditConstants.SectorRegulated,
            SectorCategory = CostAuditConstants.CatDrugsPharma,
            OverallTurnoverInr = 120_00_00_000m,
            ProductTurnoverInr = 60_00_00_000m,
            CostAuditorName = "R. K. Sharma",
            CostAuditorFirmName = "Sharma & Associates, Cost Accountants",
            CostAuditorMembershipNo = "FCMA 12345",
            CostAuditorFirmRegNo = "FRN 000123",
            AuditorOpinion = CostAuditConstants.OpinionUnmodified,
            Udin = "24123456AAAAAA1234",
            CertifiedAt = new DateTime(2025, 8, 15)
        };

        var statement = new ProductCostStatement
        {
            ProductCode = "P001",
            ProductName = "Formulation A",
            CtaHeading = "3004",
            ActualProduction = 1000m,
            UnitOfMeasurement = "KG",
            CostOfProduction = 50_00_000m,
            CostOfSales = 55_00_000m,
            NetSalesRealization = 70_00_000m
        };
        CostAuditEngine.CalculateProductCostStatement(statement);
        engagement.ProductStatements.Add(statement);

        CostAuditEngine.RecalculateEngagementSummary(engagement);

        var report = CostAuditEngine.GenerateFormCra3Report(engagement, null);

        Assert.Contains("FORM NO. CRA-3", report);
        Assert.Contains("COST AUDIT REPORT", report);
        Assert.Contains("Sharma & Associates, Cost Accountants", report);
        Assert.Contains("PART A: GENERAL INFORMATION & STATUTORY COMPLIANCE", report);
        Assert.Contains("PART B: MANUFACTURING SECTOR - PRODUCT-WISE COST STATEMENTS", report);
        Assert.Contains("PART D: PROFIT RECONCILIATION WITH FINANCIAL ACCOUNTS", report);
        Assert.Contains("24123456AAAAAA1234", report);
    }
}
