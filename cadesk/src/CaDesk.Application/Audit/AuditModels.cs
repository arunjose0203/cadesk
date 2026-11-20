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
    DateOnly? AuditReportDate,
    DateOnly? ArchivalDeadline,
    bool IsArchived,
    DateTime? ArchivedAt,
    Guid? ArchivedBy,
    string? ArchivedByName,
    bool EqrRequired,
    Guid? EqrPartnerId,
    string? EqrPartnerName,
    string EqrStatus,
    string ArchivalStatus,
    int OpenReviewNotesCount,
    bool CanSignOff,
    string SignOffValidationMessage,
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
    int ReviewNotesCount,
    int OpenReviewNotesCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PreIssuanceChecklistItemDto(
    string Code,
    string Description,
    bool Required,
    bool IsVerified,
    string? Comments,
    DateTime? VerifiedAt
);

public record AuditEqrReviewDto(
    Guid Id,
    Guid EngagementId,
    string ReviewType,
    Guid EqrPartnerId,
    string? EqrPartnerName,
    string ReviewStatus,
    string? EvaluationMateriality,
    string? EvaluationSignificantRisks,
    string? EvaluationGoingConcern,
    string? EvaluationAuditOpinion,
    List<PreIssuanceChecklistItemDto> PreIssuanceChecklist,
    DateTime? EqrSignoffDate,
    string? EqrConclusionRemarks,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record AuditReviewNoteDto(
    Guid Id,
    Guid EngagementId,
    Guid? WorkingPaperId,
    string? WorkingPaperRef,
    string? WorkingPaperTitle,
    Guid RaisedByUserId,
    string? RaisedByName,
    string RaisedByRole,
    string Category,
    string Priority,
    string Query,
    string Status,
    string? Response,
    Guid? ResponseByUserId,
    string? ResponseByName,
    DateTime? ResponseAt,
    Guid? ClearedByUserId,
    string? ClearedByName,
    DateTime? ClearedAt,
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
    bool EqrRequired,
    Guid? EqrPartnerId,
    string? Notes
);

public record UpdateMaterialityRequest(
    string BenchmarkType,
    decimal BenchmarkAmount,
    decimal? OverallMaterialityPercent,
    decimal? PerformanceMaterialityPercent,
    decimal? TrivialThresholdPercent
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
    string Status,
    string? ReviewNotes
);

public record UpdateEngagementStatusRequest(
    string Status,
    string? AuditOpinion
);

public record SignOffAuditReportRequest(
    DateOnly AuditReportDate,
    string AuditOpinion,
    string? Notes
);

public record ArchiveEngagementRequest(
    string? Notes
);

public record CreateEqrReviewRequest(
    string ReviewType, // 'hot_review' or 'cold_review'
    Guid EqrPartnerId
);

public record UpdateEqrReviewRequest(
    string? EvaluationMateriality,
    string? EvaluationSignificantRisks,
    string? EvaluationGoingConcern,
    string? EvaluationAuditOpinion,
    List<PreIssuanceChecklistItemDto>? PreIssuanceChecklist,
    string? EqrConclusionRemarks
);

public record ClearEqrReviewRequest(
    string EqrConclusionRemarks,
    List<PreIssuanceChecklistItemDto> PreIssuanceChecklist
);

public record CreateReviewNoteRequest(
    Guid? WorkingPaperId,
    string RaisedByRole,
    string Category,
    string Priority,
    string Query
);

public record RespondReviewNoteRequest(
    string Response
);

public record ReopenReviewNoteRequest(
    string Remarks
);

public record AuditSummaryDto(
    int TotalEngagements,
    int PlanningCount,
    int FieldworkCount,
    int ReviewCount,
    int SignedOffCount,
    int ArchivedCount,
    int ArchivalDueSoonCount,
    int ArchivalOverdueCount,
    int EqrPendingCount,
    int TotalWorkingPapers,
    int OpenReviewNotesCount,
    int ExceptionsFlaggedCount
);
