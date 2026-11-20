using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.CostAudit;

public static class CostAuditConstants
{
    // Sector Types (Rule 3 & Rule 4)
    public const string SectorRegulated = "Regulated"; // Table A
    public const string SectorNonRegulated = "NonRegulated"; // Table B

    // Table A Regulated Industry Categories
    public const string CatTelecommunications = "Telecommunication";
    public const string CatElectricity = "Electricity";
    public const string CatPetroleum = "Petroleum";
    public const string CatDrugsPharma = "DrugsAndPharma";
    public const string CatFertilizers = "Fertilizers";
    public const string CatSugar = "SugarAndAlcohol";

    // Table B Non-Regulated Industry Categories
    public const string CatCement = "Cement";
    public const string CatSteel = "Steel";
    public const string CatChemicals = "Chemicals";
    public const string CatTextiles = "Textiles";
    public const string CatCapitalGoods = "CapitalGoods";
    public const string CatAutomotive = "AutomotiveComponents";
    public const string CatPaper = "Paper";
    public const string CatMining = "MiningAndMetals";
    public const string CatOtherManufacturing = "OtherManufacturing";
    public const string CatOtherServices = "OtherServices";

    // Rule 3 Cost Records Turnover Threshold
    public const decimal Rule3RecordsTurnoverThreshold = 35_00_00_000m; // ₹35 Crore

    // Rule 4 Cost Audit Turnover Thresholds
    public const decimal RegulatedOverallTurnoverThreshold = 50_00_00_000m; // ₹50 Crore
    public const decimal RegulatedProductTurnoverThreshold = 25_00_00_000m; // ₹25 Crore

    public const decimal NonRegulatedOverallTurnoverThreshold = 100_00_00_000m; // ₹100 Crore
    public const decimal NonRegulatedProductTurnoverThreshold = 35_00_00_000m; // ₹35 Crore

    // Exemption Thresholds
    public const decimal ExportExemptionPct = 75.0m; // > 75% export turnover in foreign exchange

    // Engagement Statuses
    public const string StatusDraft = "Draft";
    public const string StatusInProgress = "AuditInProgress";
    public const string StatusUnderReview = "UnderReview";
    public const string StatusCertified = "Certified";
    public const string StatusFiledCra4 = "FiledCRA4";

    // Cost Auditor Opinions
    public const string OpinionUnmodified = "Unmodified";
    public const string OpinionQualified = "Qualified";
    public const string OpinionAdverse = "Adverse";
    public const string OpinionDisclaimer = "Disclaimer";

    // CRA-2 / CRA-4 Filing Statuses
    public const string FilingStatusPending = "Pending";
    public const string FilingStatusFiledOnTime = "FiledOnTime";
    public const string FilingStatusFiledDelayed = "FiledDelayed";
    public const string FilingStatusOverdue = "Overdue";

    // Cost Record Head Statuses
    public const string HeadCompliant = "Compliant";
    public const string HeadPartiallyCompliant = "PartiallyCompliant";
    public const string HeadNonCompliant = "NonCompliant";
    public const string HeadNotApplicable = "NotApplicable";

    // Reconciliation Item Types
    public const string RecAddIncomeNotCosted = "Add_IncomeNotCosted";
    public const string RecDeductExpenseNotCosted = "Deduct_ExpenseNotCosted";
    public const string RecAddCostExpenseOvercharged = "Add_CostExpenseOvercharged";
    public const string RecDeductFinancialExpenseOvercharged = "Deduct_FinancialExpenseOvercharged";
    public const string RecValuationDifference = "ValuationDifference";
}

public class CostAuditEngagement : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string EngagementReference { get; set; } = "";
    public string FinancialYear { get; set; } = ""; // e.g. "2024-25"
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // Sector & Applicability
    public string SectorType { get; set; } = CostAuditConstants.SectorRegulated;
    public string SectorCategory { get; set; } = CostAuditConstants.CatDrugsPharma;
    public decimal OverallTurnoverInr { get; set; }
    public decimal ProductTurnoverInr { get; set; }
    public decimal ExportTurnoverInr { get; set; }

    public bool IsExportExempt { get; set; }
    public bool IsSezExempt { get; set; }
    public bool IsCaptivePowerExempt { get; set; }

    public bool IsCostRecordsApplicable { get; set; } // Rule 3
    public bool IsCostAuditApplicable { get; set; }   // Rule 4
    public string ApplicabilityReasoning { get; set; } = "";

    // Form CRA-2 (Auditor Appointment) tracking
    public DateTime? BoardResolutionDate { get; set; }
    public DateTime? FyCommencementDate { get; set; }
    public bool AuditCommitteeRecommended { get; set; } = true;
    public string CostAuditorName { get; set; } = "";
    public string CostAuditorFirmName { get; set; } = "";
    public string CostAuditorMembershipNo { get; set; } = "";
    public string CostAuditorFirmRegNo { get; set; } = "";
    public DateTime? Cra2FilingDueDate { get; set; }
    public string Cra2FilingStatus { get; set; } = CostAuditConstants.FilingStatusPending;
    public string? Cra2Srn { get; set; }
    public DateTime? Cra2FiledDate { get; set; }

    // Form CRA-3 (Cost Audit Report) state
    public string AuditStatus { get; set; } = CostAuditConstants.StatusDraft;
    public string AuditorOpinion { get; set; } = CostAuditConstants.OpinionUnmodified;
    public string? ReservationsQualifications { get; set; }
    public string? ObservationsSuggestions { get; set; }

    // Certification & UDIN (18 alphanumeric characters)
    public string? Udin { get; set; }
    public DateTime? CertifiedAt { get; set; }
    public string? CertifiedBy { get; set; }
    public string? CostAuditReportText { get; set; }

    // Form CRA-4 (Filing with Central Government) tracking
    public DateTime? Cra3HandoverDate { get; set; } // Date of receipt by Board of Directors
    public DateTime? Cra4FilingDueDate { get; set; } // Handover + 30 days
    public string Cra4FilingStatus { get; set; } = CostAuditConstants.FilingStatusPending;
    public string? Cra4Srn { get; set; }
    public DateTime? Cra4FiledDate { get; set; }
    public bool Cra4XbrlValidated { get; set; }

    // Aggregate Analytics & Reconciliation Summary
    public int TotalProductsCount { get; set; }
    public decimal TotalProductionValueInr { get; set; }
    public decimal TotalCostOfSalesInr { get; set; }
    public decimal TotalSalesRealizationInr { get; set; }
    public decimal TotalCostProfitInr { get; set; }
    public decimal AuditedFinancialProfitInr { get; set; }
    public decimal ReconciledFinancialProfitInr { get; set; }
    public decimal NetReconciliationDifferenceInr { get; set; } // Reconciled - Audited (ideal: 0)
    public decimal RecordsComplianceScorePct { get; set; } = 100m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation collections
    public List<CostRecordHead> RecordHeads { get; set; } = [];
    public List<ProductCostStatement> ProductStatements { get; set; } = [];
    public List<CostAuditReconciliationItem> ReconciliationItems { get; set; } = [];
}

public class CostRecordHead : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public CostAuditEngagement? Engagement { get; set; }

    public string HeadCode { get; set; } = ""; // e.g. "REC_01_MATERIALS"
    public string HeadName { get; set; } = "";
    public string RuleReference { get; set; } = "CRA-1 Clause";
    public string Status { get; set; } = CostAuditConstants.HeadCompliant;
    public string RecordMaintenanceSystem { get; set; } = "ERP Cost Ledger";
    public string? VerificationNotes { get; set; }
    public string? DeficiencyOrAnomaly { get; set; }
    public string? CorrectiveActionRequired { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class ProductCostStatement : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public CostAuditEngagement? Engagement { get; set; }

    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string CtaHeading { get; set; } = ""; // Customs/Excise/HSN tariff chapter heading
    public string UnitOfMeasurement { get; set; } = "MT"; // MT, KG, Litres, Units, etc.

    // Capacity & Production quantitative details
    public decimal InstalledCapacity { get; set; }
    public decimal AvailableCapacity { get; set; }
    public decimal ActualProduction { get; set; }
    public decimal CapacityUtilizationPct { get; set; }

    // Direct Elements of Cost
    public decimal DirectMaterialsCost { get; set; }
    public decimal DirectLaborCost { get; set; }
    public decimal UtilitiesCost { get; set; } // Power, fuel, water, steam
    public decimal OtherDirectExpenses { get; set; }
    public decimal PrimeCost { get; set; }

    // Overheads & Manufacturing Cost
    public decimal WorksOverheads { get; set; }
    public decimal WorksCost { get; set; }
    public decimal QualityControlCost { get; set; }
    public decimal RndExpenses { get; set; }
    public decimal AdministrationOverheads { get; set; }

    // Work-in-Progress Adjustments
    public decimal OpeningWipCost { get; set; }
    public decimal ClosingWipCost { get; set; }
    public decimal CostOfProduction { get; set; }

    // Finished Goods & Selling Overheads
    public decimal OpeningFinishedGoodsCost { get; set; }
    public decimal ClosingFinishedGoodsCost { get; set; }
    public decimal SellingAndDistributionOverheads { get; set; }
    public decimal CostOfSales { get; set; }

    // Sales Realization & Margins
    public decimal NetSalesRealization { get; set; }
    public decimal ProductMarginInr { get; set; } // NetSalesRealization - CostOfSales
    public decimal ProductMarginPct { get; set; }

    // Per Unit Statistics
    public decimal UnitCostOfSales { get; set; }
    public decimal UnitSalesRealization { get; set; }
    public decimal UnitMarginInr { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class CostAuditReconciliationItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public CostAuditEngagement? Engagement { get; set; }

    public string ItemType { get; set; } = CostAuditConstants.RecAddIncomeNotCosted;
    public string Description { get; set; } = "";
    public decimal AmountInr { get; set; }
    public string Impact { get; set; } = "Positive"; // "Positive" (+ to profit) or "Negative" (- to profit)
    public string? FinancialScheduleRef { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public static class CostAuditEngine
{
    /// <summary>
    /// Evaluates Rule 3 (Cost Records) and Rule 4 (Cost Audit) applicability under the Companies (Cost Records and Audit) Rules, 2014.
    /// </summary>
    public static (bool IsCostRecordsApplicable, bool IsCostAuditApplicable, bool IsExportExempt, string Reasoning) EvaluateApplicability(
        string sectorType,
        decimal overallTurnoverInr,
        decimal productTurnoverInr,
        decimal exportTurnoverInr,
        bool isSezUnit,
        bool isCaptivePower)
    {
        var sb = new StringBuilder();

        // Rule 3: Maintenance of Cost Records
        bool isRecordsApplicable = overallTurnoverInr >= CostAuditConstants.Rule3RecordsTurnoverThreshold;
        if (isRecordsApplicable)
        {
            sb.AppendLine($"Rule 3 Applicable: Overall turnover of ₹{overallTurnoverInr:N0} meets or exceeds statutory threshold of ₹35 Crore.");
        }
        else
        {
            sb.AppendLine($"Rule 3 Not Applicable: Overall turnover of ₹{overallTurnoverInr:N0} is below the ₹35 Crore threshold.");
        }

        // Rule 4(3) Exemptions:
        // (i) Revenue from exports in foreign exchange exceeds 75% of total revenue;
        // (ii) Operating from a Special Economic Zone (SEZ);
        // (iii) Engaged in electricity generation for captive consumption through a captive generating plant.
        decimal exportRatioPct = overallTurnoverInr > 0 ? Math.Round((exportTurnoverInr / overallTurnoverInr) * 100m, 2) : 0m;
        bool isExportExempt = exportRatioPct > CostAuditConstants.ExportExemptionPct;

        if (isExportExempt)
        {
            sb.AppendLine($"Rule 4 Exemption (Rule 4(3)(i)): Export revenue of ₹{exportTurnoverInr:N0} represents {exportRatioPct}% (> 75%) of total revenue in foreign exchange.");
            return (isRecordsApplicable, false, true, sb.ToString().TrimEnd());
        }

        if (isSezUnit)
        {
            sb.AppendLine("Rule 4 Exemption (Rule 4(3)(ii)): Company operations are conducted from a Special Economic Zone (SEZ).");
            return (isRecordsApplicable, false, false, sb.ToString().TrimEnd());
        }

        if (isCaptivePower && sectorType == CostAuditConstants.SectorRegulated)
        {
            sb.AppendLine("Rule 4 Exemption (Rule 4(3)(iii)): Engaged in generation of electricity solely for captive consumption.");
            return (isRecordsApplicable, false, false, sb.ToString().TrimEnd());
        }

        // Rule 4: Cost Audit Applicability
        bool isAuditApplicable;
        if (sectorType == CostAuditConstants.SectorRegulated)
        {
            bool meetsOverall = overallTurnoverInr >= CostAuditConstants.RegulatedOverallTurnoverThreshold;
            bool meetsProduct = productTurnoverInr >= CostAuditConstants.RegulatedProductTurnoverThreshold;
            isAuditApplicable = meetsOverall && meetsProduct;

            sb.AppendLine($"Table A (Regulated Sector): Overall Turnover ₹{overallTurnoverInr:N0} (Min: ₹50 Cr -> {(meetsOverall ? "PASS" : "FAIL")}) and Product Turnover ₹{productTurnoverInr:N0} (Min: ₹25 Cr -> {(meetsProduct ? "PASS" : "FAIL")}).");
        }
        else
        {
            bool meetsOverall = overallTurnoverInr >= CostAuditConstants.NonRegulatedOverallTurnoverThreshold;
            bool meetsProduct = productTurnoverInr >= CostAuditConstants.NonRegulatedProductTurnoverThreshold;
            isAuditApplicable = meetsOverall && meetsProduct;

            sb.AppendLine($"Table B (Non-Regulated Sector): Overall Turnover ₹{overallTurnoverInr:N0} (Min: ₹100 Cr -> {(meetsOverall ? "PASS" : "FAIL")}) and Product Turnover ₹{productTurnoverInr:N0} (Min: ₹35 Cr -> {(meetsProduct ? "PASS" : "FAIL")}).");
        }

        sb.AppendLine(isAuditApplicable
            ? "Mandatory Cost Audit required under Section 148 read with Rule 4."
            : "Exempt from mandatory Cost Audit as turnover criteria are not satisfied.");

        return (isRecordsApplicable, isAuditApplicable, isExportExempt, sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Calculates the statutory filing due date for Form CRA-2 (Intimation of Cost Auditor Appointment).
    /// Rule 6(2): Within 30 days of Board Meeting OR within 180 days of FY commencement, whichever is earlier.
    /// </summary>
    public static (DateTime DueDate, string Status, int DaysRemainingOrOverdue) CalculateCra2DueDates(
        DateTime? boardResolutionDate,
        DateTime? fyCommencementDate,
        DateTime? filedDate)
    {
        var fyStart = fyCommencementDate ?? DateTime.UtcNow.Date;
        var limit180 = fyStart.AddDays(180);

        DateTime dueDate = limit180;
        if (boardResolutionDate.HasValue)
        {
            var limit30 = boardResolutionDate.Value.AddDays(30);
            dueDate = limit30 < limit180 ? limit30 : limit180;
        }

        var today = DateTime.UtcNow.Date;
        if (filedDate.HasValue)
        {
            bool onTime = filedDate.Value.Date <= dueDate.Date;
            int diff = (int)(dueDate.Date - filedDate.Value.Date).TotalDays;
            return (dueDate, onTime ? CostAuditConstants.FilingStatusFiledOnTime : CostAuditConstants.FilingStatusFiledDelayed, diff);
        }

        if (today > dueDate.Date)
        {
            int overdueDays = (int)(today - dueDate.Date).TotalDays;
            return (dueDate, CostAuditConstants.FilingStatusOverdue, -overdueDays);
        }

        int remainingDays = (int)(dueDate.Date - today).TotalDays;
        return (dueDate, CostAuditConstants.FilingStatusPending, remainingDays);
    }

    /// <summary>
    /// Calculates the statutory filing due date for Form CRA-4 (Filing Cost Audit Report with MCA).
    /// Rule 6(6): Within 30 days from the date of receipt of a copy of the Cost Audit Report by the company.
    /// </summary>
    public static (DateTime DueDate, string Status, int DaysRemainingOrOverdue) CalculateCra4DueDates(
        DateTime? cra3HandoverDate,
        DateTime? filedDate)
    {
        var handover = cra3HandoverDate ?? DateTime.UtcNow.Date;
        var dueDate = handover.AddDays(30);

        var today = DateTime.UtcNow.Date;
        if (filedDate.HasValue)
        {
            bool onTime = filedDate.Value.Date <= dueDate.Date;
            int diff = (int)(dueDate.Date - filedDate.Value.Date).TotalDays;
            return (dueDate, onTime ? CostAuditConstants.FilingStatusFiledOnTime : CostAuditConstants.FilingStatusFiledDelayed, diff);
        }

        if (today > dueDate.Date)
        {
            int overdueDays = (int)(today - dueDate.Date).TotalDays;
            return (dueDate, CostAuditConstants.FilingStatusOverdue, -overdueDays);
        }

        int remainingDays = (int)(dueDate.Date - today).TotalDays;
        return (dueDate, CostAuditConstants.FilingStatusPending, remainingDays);
    }

    /// <summary>
    /// Recalculates cost sheet totals, margins, and per-unit metrics for a product.
    /// </summary>
    public static void CalculateProductCostStatement(ProductCostStatement statement)
    {
        // Capacity utilization
        if (statement.AvailableCapacity > 0)
        {
            statement.CapacityUtilizationPct = Math.Round((statement.ActualProduction / statement.AvailableCapacity) * 100m, 2);
        }
        else
        {
            statement.CapacityUtilizationPct = 0m;
        }

        // Prime Cost = Direct Materials + Direct Labor + Utilities + Other Direct
        statement.PrimeCost = statement.DirectMaterialsCost
                              + statement.DirectLaborCost
                              + statement.UtilitiesCost
                              + statement.OtherDirectExpenses;

        // Works Cost = Prime Cost + Works Overheads
        statement.WorksCost = statement.PrimeCost + statement.WorksOverheads;

        // Cost of Production = Works Cost + Quality Control + R&D + Admin Overheads + (Opening WIP - Closing WIP)
        decimal netWipAdjustment = statement.OpeningWipCost - statement.ClosingWipCost;
        statement.CostOfProduction = statement.WorksCost
                                    + statement.QualityControlCost
                                    + statement.RndExpenses
                                    + statement.AdministrationOverheads
                                    + netWipAdjustment;

        // Cost of Sales = Cost of Production + (Opening FG - Closing FG) + Selling & Distribution Overheads
        decimal netFgAdjustment = statement.OpeningFinishedGoodsCost - statement.ClosingFinishedGoodsCost;
        statement.CostOfSales = statement.CostOfProduction
                                + netFgAdjustment
                                + statement.SellingAndDistributionOverheads;

        // Margins
        statement.ProductMarginInr = statement.NetSalesRealization - statement.CostOfSales;
        if (statement.NetSalesRealization != 0)
        {
            statement.ProductMarginPct = Math.Round((statement.ProductMarginInr / statement.NetSalesRealization) * 100m, 2);
        }
        else
        {
            statement.ProductMarginPct = 0m;
        }

        // Per Unit figures
        if (statement.ActualProduction > 0)
        {
            statement.UnitCostOfSales = Math.Round(statement.CostOfSales / statement.ActualProduction, 2);
            statement.UnitSalesRealization = Math.Round(statement.NetSalesRealization / statement.ActualProduction, 2);
            statement.UnitMarginInr = Math.Round(statement.ProductMarginInr / statement.ActualProduction, 2);
        }
        else
        {
            statement.UnitCostOfSales = 0m;
            statement.UnitSalesRealization = 0m;
            statement.UnitMarginInr = 0m;
        }
    }

    /// <summary>
    /// Computes Material Variances (Price, Usage, and Total Cost Variance).
    /// </summary>
    public static (decimal PriceVariance, decimal UsageVariance, decimal TotalCostVariance) CalculateMaterialVariance(
        decimal standardQuantity,
        decimal actualQuantity,
        decimal standardPrice,
        decimal actualPrice)
    {
        // Material Price Variance = Actual Quantity * (Standard Price - Actual Price)
        // Favorable if Standard Price > Actual Price (positive)
        decimal priceVar = actualQuantity * (standardPrice - actualPrice);

        // Material Usage Variance = Standard Price * (Standard Quantity - Actual Quantity)
        // Favorable if Standard Quantity > Actual Quantity (positive)
        decimal usageVar = standardPrice * (standardQuantity - actualQuantity);

        // Total Cost Variance = (Standard Quantity * Standard Price) - (Actual Quantity * Actual Price)
        decimal totalVar = (standardQuantity * standardPrice) - (actualQuantity * actualPrice);

        return (priceVar, usageVar, totalVar);
    }

    /// <summary>
    /// Recalculates Profit Reconciliation between Cost Records and Financial Accounts (Form CRA-3 Part D).
    /// Cost Profit + Additions (Incomes not costed, etc.) - Deductions (Expenses not costed, etc.) = Reconciled Profit.
    /// </summary>
    public static (decimal ReconciledProfit, decimal TotalAdditions, decimal TotalDeductions, decimal DifferenceFromAudited) RecalculateReconciliation(
        decimal startingCostProfit,
        decimal auditedFinancialProfit,
        IEnumerable<CostAuditReconciliationItem> items)
    {
        decimal totalAdditions = 0m;
        decimal totalDeductions = 0m;

        foreach (var item in items)
        {
            if (item.Impact.Equals("Positive", StringComparison.OrdinalIgnoreCase) ||
                item.ItemType.StartsWith("Add_", StringComparison.OrdinalIgnoreCase))
            {
                totalAdditions += item.AmountInr;
            }
            else
            {
                totalDeductions += item.AmountInr;
            }
        }

        decimal reconciledProfit = startingCostProfit + totalAdditions - totalDeductions;
        decimal diff = reconciledProfit - auditedFinancialProfit;

        return (reconciledProfit, totalAdditions, totalDeductions, diff);
    }

    /// <summary>
    /// Recalculates engagement summary aggregates, compliance score, and reconciliation difference.
    /// </summary>
    public static void RecalculateEngagementSummary(CostAuditEngagement engagement)
    {
        // 1. Recalculate product statements
        engagement.TotalProductsCount = engagement.ProductStatements.Count;
        engagement.TotalProductionValueInr = engagement.ProductStatements.Sum(p => p.CostOfProduction);
        engagement.TotalCostOfSalesInr = engagement.ProductStatements.Sum(p => p.CostOfSales);
        engagement.TotalSalesRealizationInr = engagement.ProductStatements.Sum(p => p.NetSalesRealization);
        engagement.TotalCostProfitInr = engagement.TotalSalesRealizationInr - engagement.TotalCostOfSalesInr;

        // 2. Recalculate CRA-1 Compliance Score
        if (engagement.RecordHeads.Count > 0)
        {
            int applicableCount = engagement.RecordHeads.Count(h => h.Status != CostAuditConstants.HeadNotApplicable);
            if (applicableCount > 0)
            {
                decimal compliantPoints = engagement.RecordHeads.Count(h => h.Status == CostAuditConstants.HeadCompliant) * 1.0m;
                decimal partialPoints = engagement.RecordHeads.Count(h => h.Status == CostAuditConstants.HeadPartiallyCompliant) * 0.5m;
                engagement.RecordsComplianceScorePct = Math.Round(((compliantPoints + partialPoints) / applicableCount) * 100m, 2);
            }
            else
            {
                engagement.RecordsComplianceScorePct = 100m;
            }
        }

        // 3. Recalculate Reconciliation
        var (reconciled, _, _, diff) = RecalculateReconciliation(
            engagement.TotalCostProfitInr,
            engagement.AuditedFinancialProfitInr,
            engagement.ReconciliationItems);

        engagement.ReconciledFinancialProfitInr = reconciled;
        engagement.NetReconciliationDifferenceInr = diff;

        // 4. Update CRA-2 / CRA-4 due dates & status
        var (cra2Due, cra2Status, _) = CalculateCra2DueDates(
            engagement.BoardResolutionDate,
            engagement.FyCommencementDate,
            engagement.Cra2FiledDate);
        engagement.Cra2FilingDueDate = cra2Due;
        if (string.IsNullOrWhiteSpace(engagement.Cra2Srn))
        {
            engagement.Cra2FilingStatus = cra2Status;
        }

        if (engagement.Cra3HandoverDate.HasValue)
        {
            var (cra4Due, cra4Status, _) = CalculateCra4DueDates(
                engagement.Cra3HandoverDate,
                engagement.Cra4FiledDate);
            engagement.Cra4FilingDueDate = cra4Due;
            if (string.IsNullOrWhiteSpace(engagement.Cra4Srn))
            {
                engagement.Cra4FilingStatus = cra4Status;
            }
        }
    }

    /// <summary>
    /// Returns the standard 30 Form CRA-1 Cost Records Heads for seeding.
    /// </summary>
    public static List<CostRecordHead> GetStandardCostRecordHeads(Guid firmId, Guid engagementId)
    {
        var standardHeads = new (string Code, string Name, string RuleRef)[]
        {
            ("MAT_01", "Direct Materials Cost, Store Ledger & Scrap Accounting", "Rule 3 / CRA-1 Clause 1"),
            ("LAB_02", "Salaries, Wages, Overtime & Idle Time Allocation", "Rule 3 / CRA-1 Clause 2"),
            ("UTL_03", "Utilities (Power, Fuel, Water & Steam Accounting)", "Rule 3 / CRA-1 Clause 3"),
            ("SRV_04", "Service Department Cost Allocation & Apportionment", "Rule 3 / CRA-1 Clause 4"),
            ("DEP_05", "Depreciation, Amortization & Plant Utilization Register", "Rule 3 / CRA-1 Clause 5"),
            ("OVH_06", "Factory, Administrative & Selling Overheads Absorption", "Rule 3 / CRA-1 Clause 6"),
            ("RND_07", "Research & Development Costs & Amortization", "Rule 3 / CRA-1 Clause 7"),
            ("ROY_08", "Royalties, Licensing & Technical Know-how Fees", "Rule 3 / CRA-1 Clause 8"),
            ("QCT_09", "Quality Control, Testing & Certification Records", "Rule 3 / CRA-1 Clause 9"),
            ("POL_10", "Pollution Control & Environmental Compliance Costs", "Rule 3 / CRA-1 Clause 10"),
            ("ABN_11", "Abnormal, Idle Time & Non-Recurring Cost Exclusions", "Rule 3 / CRA-1 Clause 11"),
            ("PCK_12", "Primary & Secondary Packing Material Cost Accounts", "Rule 3 / CRA-1 Clause 12"),
            ("BYP_13", "By-Products, Joint-Products & Credit from Scrap", "Rule 3 / CRA-1 Clause 13"),
            ("WIP_14", "Work-in-Progress & Finished Goods Inventory Valuation", "Rule 3 / CRA-1 Clause 14"),
            ("CST_15", "Quantitative Details, Installed & Available Capacity", "Rule 3 / CRA-1 Clause 15"),
            ("REC_16", "Reconciliation of Cost and Financial Accounts (P&L)", "Rule 3 / CRA-1 Clause 16"),
            ("RPT_17", "Related Party Transactions at Cost or Arm's Length", "Rule 3 / CRA-1 Clause 17"),
            ("INT_18", "Finance Costs & Borrowing Costs Allocation", "Rule 3 / CRA-1 Clause 18"),
            ("EXP_19", "Export Incentives & Duty Drawback Realizations", "Rule 3 / CRA-1 Clause 19"),
            ("JOB_20", "Job Work, Sub-contracting & Toll Processing Charges", "Rule 3 / CRA-1 Clause 20"),
            ("ICT_21", "Internal Consumption of Manufactured Goods & Transfer", "Rule 3 / CRA-1 Clause 21"),
            ("CAP_22", "Capital Work-in-Progress & Self-Constructed Assets", "Rule 3 / CRA-1 Clause 22"),
            ("EMP_23", "Employee Benefits & Gratuity Allocation to Cost Centers", "Rule 3 / CRA-1 Clause 23"),
            ("LOG_24", "Logistics, Freight Inwards & Freight Outwards Costs", "Rule 3 / CRA-1 Clause 24"),
            ("INS_25", "Insurance Premium & Risk Coverage Allocation", "Rule 3 / CRA-1 Clause 25"),
            ("REP_26", "Repairs, Maintenance & Spares Consumption Accounts", "Rule 3 / CRA-1 Clause 26"),
            ("TAX_27", "Input Tax Credit Adjustments & Non-Creditable Taxes", "Rule 3 / CRA-1 Clause 27"),
            ("CON_28", "Cost Accounting Policies & CAS Consistency Verification", "Rule 3 / CRA-1 Clause 28"),
            ("VAR_29", "Standard Costing & Variance Analysis Records", "Rule 3 / CRA-1 Clause 29"),
            ("SYS_30", "Audit Trail & Electronic Cost Accounting System Controls", "Rule 3 / CRA-1 Clause 30")
        };

        var list = new List<CostRecordHead>();
        foreach (var h in standardHeads)
        {
            list.Add(new CostRecordHead
            {
                Id = Guid.NewGuid(),
                FirmId = firmId,
                EngagementId = engagementId,
                HeadCode = h.Code,
                HeadName = h.Name,
                RuleReference = h.RuleRef,
                Status = CostAuditConstants.HeadCompliant,
                RecordMaintenanceSystem = "ERP Cost Ledger / Cost Centers",
                CreatedAt = DateTime.UtcNow
            });
        }
        return list;
    }

    /// <summary>
    /// Generates the complete statutory Cost Audit Report in Form CRA-3 pursuant to Section 148(6) and Rule 6(4).
    /// </summary>
    public static string GenerateFormCra3Report(CostAuditEngagement engagement, Client? client)
    {
        var sb = new StringBuilder();
        var clientName = client?.Name ?? "Client Company Limited";
        var cin = client != null && client.TaxIds.TryGetValue("CIN", out var cVal) && !string.IsNullOrWhiteSpace(cVal)
            ? cVal
            : (client != null && client.TaxIds.TryGetValue("PAN", out var pVal) && !string.IsNullOrWhiteSpace(pVal) ? pVal : "U00000XX0000PTC000000");

        sb.AppendLine("# FORM NO. CRA-3");
        sb.AppendLine("### [Pursuant to Section 148(6) of the Companies Act, 2013 and Rule 6(4) of the Companies (Cost Records and Audit) Rules, 2014]");
        sb.AppendLine("## COST AUDIT REPORT");
        sb.AppendLine();
        sb.AppendLine($"**To the Board of Directors of {clientName}**");
        sb.AppendLine();
        sb.AppendLine("### 1. Cost Auditor's Report");
        sb.AppendLine($"We, **{engagement.CostAuditorFirmName}**, Cost Accountants, having been appointed as Cost Auditors of **{clientName}** (CIN: `{cin}`) under Section 148 of the Companies Act, 2013, have audited the cost records maintained by the Company for the year ended **{engagement.PeriodEnd:dd MMMM yyyy}** for products/services covered under the Companies (Cost Records and Audit) Rules, 2014.");
        sb.AppendLine();

        sb.AppendLine("### 2. Opinion");
        if (engagement.AuditorOpinion == CostAuditConstants.OpinionUnmodified)
        {
            sb.AppendLine("In our opinion and to the best of our information and according to the explanations given to us, the said cost records and statements give a true and fair view of the cost of production, cost of sales, and margin for the products under audit in accordance with the Cost Accounting Standards (CAS) issued by the Institute of Cost Accountants of India.");
        }
        else if (engagement.AuditorOpinion == CostAuditConstants.OpinionQualified)
        {
            sb.AppendLine("In our opinion and to the best of our information and according to the explanations given to us, **subject to the qualifications/observations noted below**, the cost records give a true and fair view of the cost of production and sales.");
        }
        else
        {
            sb.AppendLine($"In our opinion, for the reasons stated below, the cost records **do not present a true and fair view** ({engagement.AuditorOpinion}).");
        }
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(engagement.ReservationsQualifications))
        {
            sb.AppendLine("### 3. Reservations, Qualifications and Adverse Remarks");
            sb.AppendLine(engagement.ReservationsQualifications);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(engagement.ObservationsSuggestions))
        {
            sb.AppendLine("### 4. Observations & Suggestions for Management Action");
            sb.AppendLine(engagement.ObservationsSuggestions);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("## ANNEXURE TO THE COST AUDIT REPORT");
        sb.AppendLine();
        sb.AppendLine("### PART A: GENERAL INFORMATION & STATUTORY COMPLIANCE");
        sb.AppendLine($"1. **Name of Company**: {clientName}");
        sb.AppendLine($"2. **Financial Year**: {engagement.FinancialYear} (From {engagement.PeriodStart:dd-MM-yyyy} to {engagement.PeriodEnd:dd-MM-yyyy})");
        sb.AppendLine($"3. **Sector Classification**: {engagement.SectorType} ({engagement.SectorCategory})");
        sb.AppendLine($"4. **Total Company Turnover**: ₹{engagement.OverallTurnoverInr:N2}");
        sb.AppendLine($"5. **Audited Product Turnover**: ₹{engagement.ProductTurnoverInr:N2}");
        sb.AppendLine($"6. **Form CRA-2 Filing SRN**: `{engagement.Cra2Srn ?? "Pending / Filed"}` (Filed on: {engagement.Cra2FiledDate?.ToString("dd-MM-yyyy") ?? "N/A"})");
        sb.AppendLine($"7. **Form CRA-1 Records Compliance Score**: {engagement.RecordsComplianceScorePct:F1}%");
        sb.AppendLine();

        sb.AppendLine("### PART B: MANUFACTURING SECTOR - PRODUCT-WISE COST STATEMENTS");
        sb.AppendLine("| Product Code | Product Name | CTA Heading | Actual Prod. | Capacity Util. % | Cost of Prod. (₹) | Cost of Sales (₹) | Sales Realization (₹) | Margin (₹) | Margin % |");
        sb.AppendLine("|:---|:---|:---|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var p in engagement.ProductStatements)
        {
            sb.AppendLine($"| `{p.ProductCode}` | {p.ProductName} | `{p.CtaHeading}` | {p.ActualProduction:N0} {p.UnitOfMeasurement} | {p.CapacityUtilizationPct:F1}% | ₹{p.CostOfProduction:N2} | ₹{p.CostOfSales:N2} | ₹{p.NetSalesRealization:N2} | ₹{p.ProductMarginInr:N2} | {p.ProductMarginPct:F1}% |");
        }
        sb.AppendLine($"| **TOTAL** | **{engagement.TotalProductsCount} Products** | | | | **₹{engagement.TotalProductionValueInr:N2}** | **₹{engagement.TotalCostOfSalesInr:N2}** | **₹{engagement.TotalSalesRealizationInr:N2}** | **₹{engagement.TotalCostProfitInr:N2}** | |");
        sb.AppendLine();

        sb.AppendLine("### PART D: PROFIT RECONCILIATION WITH FINANCIAL ACCOUNTS (P&L)");
        sb.AppendLine($"1. **Profit as per Cost Accounts (Cost Profit)**: **₹{engagement.TotalCostProfitInr:N2}**");
        sb.AppendLine();
        sb.AppendLine("2. **Add: Incomes / Differences not credited in Cost Accounts**:");
        var additions = engagement.ReconciliationItems.Where(i => i.Impact.Equals("Positive", StringComparison.OrdinalIgnoreCase) || i.ItemType.StartsWith("Add_", StringComparison.OrdinalIgnoreCase)).ToList();
        if (additions.Count == 0)
        {
            sb.AppendLine("   - None reported.");
        }
        else
        {
            foreach (var a in additions)
            {
                sb.AppendLine($"   - {a.Description}: ₹{a.AmountInr:N2} (Ref: {a.FinancialScheduleRef ?? "P&L"})");
            }
        }
        sb.AppendLine();

        sb.AppendLine("3. **Less: Expenses / Differences not debited in Cost Accounts**:");
        var deductions = engagement.ReconciliationItems.Where(i => !i.Impact.Equals("Positive", StringComparison.OrdinalIgnoreCase) && !i.ItemType.StartsWith("Add_", StringComparison.OrdinalIgnoreCase)).ToList();
        if (deductions.Count == 0)
        {
            sb.AppendLine("   - None reported.");
        }
        else
        {
            foreach (var d in deductions)
            {
                sb.AppendLine($"   - {d.Description}: ₹{d.AmountInr:N2} (Ref: {d.FinancialScheduleRef ?? "P&L"})");
            }
        }
        sb.AppendLine();

        sb.AppendLine($"4. **Reconciled Profit as per Cost Audit**: **₹{engagement.ReconciledFinancialProfitInr:N2}**");
        sb.AppendLine($"5. **Audited Profit as per Financial Accounts (P&L)**: **₹{engagement.AuditedFinancialProfitInr:N2}**");
        sb.AppendLine($"6. **Unreconciled Difference**: **₹{engagement.NetReconciliationDifferenceInr:N2}** (Status: {(Math.Abs(engagement.NetReconciliationDifferenceInr) < 1m ? "Fully Reconciled" : "Discrepancy Pending Resolution")})");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine("### STATUTORY CERTIFICATION & UDIN");
        sb.AppendLine($"**Cost Auditor**: {engagement.CostAuditorName}");
        sb.AppendLine($"**Firm Name**: {engagement.CostAuditorFirmName}");
        sb.AppendLine($"**Membership Number**: {engagement.CostAuditorMembershipNo}");
        sb.AppendLine($"**Firm Registration Number (FRN)**: {engagement.CostAuditorFirmRegNo}");
        sb.AppendLine($"**Unique Document Identification Number (UDIN)**: `{engagement.Udin ?? "PENDING_CERTIFICATION"}`");
        sb.AppendLine($"**Date of Signing**: {engagement.CertifiedAt?.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture) ?? DateTime.UtcNow.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"**Place**: India");

        return sb.ToString();
    }
}
