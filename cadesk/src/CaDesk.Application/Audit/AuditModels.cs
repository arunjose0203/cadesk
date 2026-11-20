namespace CaDesk.Application.Audit;

public record AuditEngagementDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string FinancialYear,
    string AuditType,
    string Title,
    Guid? LeadPartnerId,
    string? LeadPartnerName,
    Guid? ManagerId,
    string? ManagerName,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string Status,
    string BenchmarkType,
    decimal BenchmarkAmount,
    decimal OverallMateriality,
    decimal PerformanceMateriality,
    decimal TrivialThreshold,
    string? AuditOpinion,
    string? Notes,
    int TotalWorkingPapers,
    int ReviewedWorkingPapers,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record AuditWorkingPaperDto(
    Guid Id,
    Guid EngagementId,
    string RefCode,
    string Area,
    string Title,
    string Assertion,
    string Objective,
    string ProceduresPerformed,
    string SamplingMethod,
    decimal PopulationValue,
    decimal SampleValue,
    int ItemsTestedCount,
    string? ExceptionsNoted,
    string Conclusion,
    string Status,
    Guid? PreparerId,
    string? PreparerName,
    DateTime? PreparedAt,
    Guid? ReviewerId,
    string? ReviewerName,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    List<string> Attachments,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateAuditEngagementRequest(
    Guid ClientId,
    string FinancialYear,
    string? AuditType,
    string Title,
    Guid? LeadPartnerId,
    Guid? ManagerId,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string? BenchmarkType,
    decimal BenchmarkAmount,
    string? Notes
);

public record UpdateMaterialityRequest(
    string BenchmarkType,
    decimal BenchmarkAmount,
    decimal? OverallMaterialityPercent, // e.g. 1% of revenue or 5% of PBT
    decimal? PerformanceMaterialityPercent, // e.g. 75%
    decimal? TrivialThresholdPercent // e.g. 5%
);

public record SaveWorkingPaperRequest(
    string RefCode,
    string Area,
    string Title,
    string? Assertion,
    string Objective,
    string? ProceduresPerformed,
    string? SamplingMethod,
    decimal PopulationValue,
    decimal SampleValue,
    int ItemsTestedCount,
    string? ExceptionsNoted,
    string? Conclusion
);

public record ReviewWorkingPaperRequest(
    string Status, // 'draft', 'ready_for_review', 'reviewed', 'partner_approved'
    string? ReviewNotes
);

public record UpdateEngagementStatusRequest(
    string Status, // 'planning', 'fieldwork', 'review', 'partner_signoff', 'archived'
    string? AuditOpinion
);

public record AuditSummaryDto(
    int TotalEngagements,
    int PlanningCount,
    int FieldworkCount,
    int ReviewCount,
    int SignedOffCount,
    int TotalWorkingPapers,
    int ExceptionsFlaggedCount
);
