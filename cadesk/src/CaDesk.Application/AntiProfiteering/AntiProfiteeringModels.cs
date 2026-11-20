using System;
using System.Collections.Generic;

namespace CaDesk.Application.AntiProfiteering;

public record GstRateNotificationDto(
    Guid Id,
    string NotificationNumber,
    DateTime NotificationDate,
    DateTime EffectiveDate,
    string HsnOrSacCode,
    string Description,
    decimal PreviousRatePercentage,
    decimal RevisedRatePercentage,
    decimal RateReductionPercentage,
    bool IsActive,
    string Notes,
    DateTime CreatedAt
);

public record AntiProfiteeringAssessmentDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string? ClientGstin,
    string AssessmentReference,
    string Title,
    string FinancialYear,
    DateTime AuditPeriodFrom,
    DateTime AuditPeriodTo,
    Guid? GstNotificationId,
    string NotificationReference,
    DateTime? RateCutEffectiveDate,
    string AuditStatus,
    string LeadAuditorName,
    string? Udin,
    DateTime? CertificationDate,
    int TotalSkusAudited,
    int ProfiteeredSkusCount,
    decimal TotalTurnoverPostCut,
    decimal TotalUndueProfiteeredAmount,
    decimal TotalCustomerRefundedAmount,
    decimal TotalCwfDepositedAmount,
    decimal CwfInterestPayableAmount,
    string? StatutoryReportText,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record SkuProfiteeringItemDto(
    Guid Id,
    Guid AssessmentId,
    string ProductCode,
    string HsnOrSacCode,
    string ProductName,
    string UnitOfMeasurement,
    decimal PreCutBasePrice,
    decimal OldGstRate,
    decimal PreCutSellingPrice,
    decimal NewGstRate,
    decimal CommensuratePriceRequired,
    decimal PostCutActualBasePrice,
    decimal PostCutActualSellingPrice,
    decimal BasePriceIncreasePercentage,
    decimal QuantitySoldPostCut,
    decimal ProfiteeringPerUnit,
    decimal TotalProfiteeredAmount,
    string ComplianceStatus,
    bool HasRawMaterialCostJustification,
    string JustificationRemarks,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ProfiteeringRefundRecordDto(
    Guid Id,
    Guid AssessmentId,
    string RecipientType,
    string CustomerNameOrEntity,
    string CustomerGstin,
    string InvoiceReference,
    decimal RefundAmount,
    string RefundMode,
    DateTime RefundOrDepositDate,
    string ChallanOrCinNumber,
    decimal InterestAmountPaid,
    string Notes,
    DateTime CreatedAt
);

public record AntiProfiteeringSummaryDto(
    int TotalAssessments,
    int TotalSkusAudited,
    int TotalProfiteeredSkus,
    decimal TotalTurnoverAudited,
    decimal TotalProfiteeredAmount,
    decimal TotalRefundedToCustomers,
    decimal TotalCwfDeposits,
    decimal TotalPendingRemediation
);

public record SaveRateNotificationRequest(
    string NotificationNumber,
    DateTime NotificationDate,
    DateTime EffectiveDate,
    string HsnOrSacCode,
    string Description,
    decimal PreviousRatePercentage,
    decimal RevisedRatePercentage,
    bool IsActive,
    string? Notes
);

public record SaveAssessmentRequest(
    Guid ClientId,
    string AssessmentReference,
    string Title,
    string FinancialYear,
    DateTime AuditPeriodFrom,
    DateTime AuditPeriodTo,
    Guid? GstNotificationId,
    string? NotificationReference,
    DateTime? RateCutEffectiveDate,
    string? LeadAuditorName
);

public record SaveSkuItemRequest(
    string ProductCode,
    string HsnOrSacCode,
    string ProductName,
    string? UnitOfMeasurement,
    decimal PreCutBasePrice,
    decimal OldGstRate,
    decimal NewGstRate,
    decimal PostCutActualBasePrice,
    decimal QuantitySoldPostCut,
    bool HasRawMaterialCostJustification,
    string? JustificationRemarks
);

public record SaveRefundRecordRequest(
    string RecipientType,
    string CustomerNameOrEntity,
    string? CustomerGstin,
    string? InvoiceReference,
    decimal RefundAmount,
    string? RefundMode,
    DateTime RefundOrDepositDate,
    string? ChallanOrCinNumber,
    decimal InterestAmountPaid,
    string? Notes
);

public record CertifyAssessmentRequest(
    string LeadAuditorName,
    string? Udin
);
