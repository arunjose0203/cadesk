using System;
using System.Collections.Generic;

namespace CaDesk.Application.InternalAudit;

public record InternalAuditEngagementDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string FinancialYear,
    string AuditPeriod,
    string Title,
    string EngagementLeadName,
    string AuditScopeDescription,
    string Status,
    DateTime? ReportDate,
    decimal HealthScorePercentage,
    int TotalControls,
    int EffectiveControls,
    int TotalFindings,
    int CriticalFindings,
    string? GeneratedReportText,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record RcmControlItemDto(
    Guid Id,
    Guid EngagementId,
    string ControlCode,
    string ProcessCycle,
    string SubProcess,
    string RiskDescription,
    string RiskCategory,
    string InherentRiskRating,
    string ControlActivity,
    string ControlNature,
    string ControlFrequency,
    string ControlType,
    string ControlOwner,
    string Assertions,
    bool WalkthroughPerformed,
    DateTime? WalkthroughDate,
    string WalkthroughReference,
    string DesignEffectiveness,
    string DesignNotes,
    string TestingMethod,
    int SampleSize,
    int ExceptionsCount,
    string OperatingEffectiveness,
    string TestingNotes,
    DateTime? TestedAt,
    string TestedBy,
    string ResidualRiskRating,
    bool HasFinding,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record InternalAuditFindingDto(
    Guid Id,
    Guid EngagementId,
    Guid? ControlItemId,
    string ControlCode,
    string FindingReference,
    string Title,
    string ProcessCycle,
    string Severity,
    string DetailedObservation,
    string RootCause,
    decimal? EstimatedRiskAmount,
    string IfcClassification,
    string AuditorRecommendation,
    string ManagementActionPlan,
    string ActionOwner,
    DateTime? TargetCompletionDate,
    string ImplementationStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record InternalAuditSummaryDto(
    int TotalEngagements,
    int TotalControlsTested,
    decimal OverallHealthScore,
    int TotalDeficiencies,
    int CriticalDeficiencies,
    int MaterialWeaknesses,
    decimal TotalFinancialExposure,
    int OpenActionsCount
);

public record SaveInternalAuditEngagementRequest(
    Guid ClientId,
    string FinancialYear,
    string AuditPeriod,
    string Title,
    string? EngagementLeadName,
    string? AuditScopeDescription,
    string? Status
);

public record SaveRcmControlItemRequest(
    string ControlCode,
    string ProcessCycle,
    string SubProcess,
    string RiskDescription,
    string? RiskCategory,
    string InherentRiskRating,
    string ControlActivity,
    string? ControlNature,
    string? ControlFrequency,
    string? ControlType,
    string? ControlOwner,
    string? Assertions,
    bool WalkthroughPerformed,
    DateTime? WalkthroughDate,
    string? WalkthroughReference,
    string DesignEffectiveness,
    string? DesignNotes,
    string TestingMethod,
    int SampleSize,
    int ExceptionsCount,
    string OperatingEffectiveness,
    string? TestingNotes,
    DateTime? TestedAt,
    string? TestedBy
);

public record SaveInternalAuditFindingRequest(
    Guid? ControlItemId,
    string? ControlCode,
    string? FindingReference,
    string Title,
    string ProcessCycle,
    string Severity,
    string DetailedObservation,
    string? RootCause,
    decimal? EstimatedRiskAmount,
    string IfcClassification,
    string AuditorRecommendation,
    string? ManagementActionPlan,
    string? ActionOwner,
    DateTime? TargetCompletionDate,
    string? ImplementationStatus
);

public record UpdateFindingStatusRequest(
    string ImplementationStatus,
    string? ManagementActionPlan,
    DateTime? TargetCompletionDate
);
