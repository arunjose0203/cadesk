using System;
using System.Collections.Generic;

namespace CaDesk.Application.CostAudit;

public record CostAuditSummaryDto(
    int TotalEngagements,
    int ActiveAudits,
    int CertifiedAudits,
    int RegulatedSectorsCount,
    int NonRegulatedSectorsCount,
    int OverdueFilingsCount,
    decimal TotalAuditedProductionInr,
    decimal TotalCostProfitInr,
    decimal TotalUnreconciledDifferenceInr,
    decimal AverageRecordsComplianceScorePct);

public record CostAuditEngagementDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string EngagementReference,
    string FinancialYear,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string SectorType,
    string SectorCategory,
    decimal OverallTurnoverInr,
    decimal ProductTurnoverInr,
    decimal ExportTurnoverInr,
    bool IsExportExempt,
    bool IsSezExempt,
    bool IsCaptivePowerExempt,
    bool IsCostRecordsApplicable,
    bool IsCostAuditApplicable,
    string ApplicabilityReasoning,
    DateTime? BoardResolutionDate,
    DateTime? FyCommencementDate,
    bool AuditCommitteeRecommended,
    string CostAuditorName,
    string CostAuditorFirmName,
    string CostAuditorMembershipNo,
    string CostAuditorFirmRegNo,
    DateTime? Cra2FilingDueDate,
    string Cra2FilingStatus,
    string? Cra2Srn,
    DateTime? Cra2FiledDate,
    string AuditStatus,
    string AuditorOpinion,
    string? ReservationsQualifications,
    string? ObservationsSuggestions,
    string? Udin,
    DateTime? CertifiedAt,
    string? CertifiedBy,
    string? CostAuditReportText,
    DateTime? Cra3HandoverDate,
    DateTime? Cra4FilingDueDate,
    string Cra4FilingStatus,
    string? Cra4Srn,
    DateTime? Cra4FiledDate,
    bool Cra4XbrlValidated,
    int TotalProductsCount,
    decimal TotalProductionValueInr,
    decimal TotalCostOfSalesInr,
    decimal TotalSalesRealizationInr,
    decimal TotalCostProfitInr,
    decimal AuditedFinancialProfitInr,
    decimal ReconciledFinancialProfitInr,
    decimal NetReconciliationDifferenceInr,
    decimal RecordsComplianceScorePct,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<CostRecordHeadDto> RecordHeads,
    List<ProductCostStatementDto> ProductStatements,
    List<CostAuditReconciliationItemDto> ReconciliationItems);

public record CostRecordHeadDto(
    Guid Id,
    Guid EngagementId,
    string HeadCode,
    string HeadName,
    string RuleReference,
    string Status,
    string RecordMaintenanceSystem,
    string? VerificationNotes,
    string? DeficiencyOrAnomaly,
    string? CorrectiveActionRequired,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProductCostStatementDto(
    Guid Id,
    Guid EngagementId,
    string ProductCode,
    string ProductName,
    string CtaHeading,
    string UnitOfMeasurement,
    decimal InstalledCapacity,
    decimal AvailableCapacity,
    decimal ActualProduction,
    decimal CapacityUtilizationPct,
    decimal DirectMaterialsCost,
    decimal DirectLaborCost,
    decimal UtilitiesCost,
    decimal OtherDirectExpenses,
    decimal PrimeCost,
    decimal WorksOverheads,
    decimal WorksCost,
    decimal QualityControlCost,
    decimal RndExpenses,
    decimal AdministrationOverheads,
    decimal OpeningWipCost,
    decimal ClosingWipCost,
    decimal CostOfProduction,
    decimal OpeningFinishedGoodsCost,
    decimal ClosingFinishedGoodsCost,
    decimal SellingAndDistributionOverheads,
    decimal CostOfSales,
    decimal NetSalesRealization,
    decimal ProductMarginInr,
    decimal ProductMarginPct,
    decimal UnitCostOfSales,
    decimal UnitSalesRealization,
    decimal UnitMarginInr,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CostAuditReconciliationItemDto(
    Guid Id,
    Guid EngagementId,
    string ItemType,
    string Description,
    decimal AmountInr,
    string Impact,
    string? FinancialScheduleRef,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ApplicabilityCheckRequest(
    string SectorType,
    decimal OverallTurnoverInr,
    decimal ProductTurnoverInr,
    decimal ExportTurnoverInr,
    bool IsSezUnit,
    bool IsCaptivePower);

public record ApplicabilityCheckResult(
    bool IsCostRecordsApplicable,
    bool IsCostAuditApplicable,
    bool IsExportExempt,
    string Reasoning);

public record MaterialVarianceRequest(
    decimal StandardQuantity,
    decimal ActualQuantity,
    decimal StandardPrice,
    decimal ActualPrice);

public record MaterialVarianceResult(
    decimal PriceVariance,
    decimal UsageVariance,
    decimal TotalCostVariance);

public record CreateCostAuditEngagementRequest(
    Guid ClientId,
    string EngagementReference,
    string FinancialYear,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string SectorType,
    string SectorCategory,
    decimal OverallTurnoverInr,
    decimal ProductTurnoverInr,
    decimal ExportTurnoverInr,
    bool IsSezExempt,
    bool IsCaptivePowerExempt,
    DateTime? BoardResolutionDate,
    DateTime? FyCommencementDate,
    bool AuditCommitteeRecommended,
    string CostAuditorName,
    string CostAuditorFirmName,
    string CostAuditorMembershipNo,
    string CostAuditorFirmRegNo,
    decimal AuditedFinancialProfitInr,
    bool SeedDefaultHeads = true);

public record UpdateCostAuditEngagementRequest(
    string EngagementReference,
    string FinancialYear,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string SectorType,
    string SectorCategory,
    decimal OverallTurnoverInr,
    decimal ProductTurnoverInr,
    decimal ExportTurnoverInr,
    bool IsSezExempt,
    bool IsCaptivePowerExempt,
    DateTime? BoardResolutionDate,
    DateTime? FyCommencementDate,
    bool AuditCommitteeRecommended,
    string CostAuditorName,
    string CostAuditorFirmName,
    string CostAuditorMembershipNo,
    string CostAuditorFirmRegNo,
    decimal AuditedFinancialProfitInr,
    string AuditorOpinion,
    string? ReservationsQualifications,
    string? ObservationsSuggestions,
    string AuditStatus);

public record CreateProductCostStatementRequest(
    string ProductCode,
    string ProductName,
    string CtaHeading,
    string UnitOfMeasurement,
    decimal InstalledCapacity,
    decimal AvailableCapacity,
    decimal ActualProduction,
    decimal DirectMaterialsCost,
    decimal DirectLaborCost,
    decimal UtilitiesCost,
    decimal OtherDirectExpenses,
    decimal WorksOverheads,
    decimal QualityControlCost,
    decimal RndExpenses,
    decimal AdministrationOverheads,
    decimal OpeningWipCost,
    decimal ClosingWipCost,
    decimal OpeningFinishedGoodsCost,
    decimal ClosingFinishedGoodsCost,
    decimal SellingAndDistributionOverheads,
    decimal NetSalesRealization);

public record UpdateProductCostStatementRequest(
    string ProductCode,
    string ProductName,
    string CtaHeading,
    string UnitOfMeasurement,
    decimal InstalledCapacity,
    decimal AvailableCapacity,
    decimal ActualProduction,
    decimal DirectMaterialsCost,
    decimal DirectLaborCost,
    decimal UtilitiesCost,
    decimal OtherDirectExpenses,
    decimal WorksOverheads,
    decimal QualityControlCost,
    decimal RndExpenses,
    decimal AdministrationOverheads,
    decimal OpeningWipCost,
    decimal ClosingWipCost,
    decimal OpeningFinishedGoodsCost,
    decimal ClosingFinishedGoodsCost,
    decimal SellingAndDistributionOverheads,
    decimal NetSalesRealization);

public record CreateReconciliationItemRequest(
    string ItemType,
    string Description,
    decimal AmountInr,
    string Impact,
    string? FinancialScheduleRef,
    string? Notes);

public record UpdateReconciliationItemRequest(
    string ItemType,
    string Description,
    decimal AmountInr,
    string Impact,
    string? FinancialScheduleRef,
    string? Notes);

public record UpdateRecordHeadRequest(
    string Status,
    string RecordMaintenanceSystem,
    string? VerificationNotes,
    string? DeficiencyOrAnomaly,
    string? CorrectiveActionRequired);

public record RecordCra2FilingRequest(
    string Cra2Srn,
    DateTime Cra2FiledDate,
    string Cra2FilingStatus);

public record RecordCra4FilingRequest(
    string Cra4Srn,
    DateTime Cra4FiledDate,
    string Cra4FilingStatus,
    bool Cra4XbrlValidated);

public record CertifyEngagementRequest(
    string Udin,
    string CertifiedBy);
