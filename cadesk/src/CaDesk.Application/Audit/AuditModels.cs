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
    bool EqrRequired = false,
    Guid? EqrPartnerId = null,
    string? Notes = null
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

public record LeadScheduleDto(
    Guid Id,
    Guid EngagementId,
    string LeadCode,
    string LeadTitle,
    string ScheduleCategory,
    decimal UnadjustedBookBalance,
    decimal AuditAdjustmentsDebit,
    decimal AuditAdjustmentsCredit,
    decimal AuditedBalance,
    decimal PriorYearBalance,
    decimal VarianceAmount,
    decimal VariancePercentage,
    bool IsMaterialVariance,
    string? AuditProceduresSummary,
    string? ExceptionsNoted,
    string Status,
    Guid? SignedOffByUserId,
    string? SignedOffByName,
    DateTime? SignedOffAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpdateLeadScheduleRequest(
    decimal AuditAdjustmentsDebit,
    decimal AuditAdjustmentsCredit,
    decimal PriorYearBalance,
    string? AuditProceduresSummary,
    string? ExceptionsNoted,
    string Status,
    bool SignOff
);

public record GenerateLeadSchedulesRequest(
    Guid? TrialBalanceImportId
);

public record DossierIndexSectionDto(
    string IndexCode,
    string SectionTitle,
    string SaStandard,
    bool Mandatory,
    string Status, // Complete, InProgress, Pending
    int ItemsCount,
    string SummaryNotes
);

public record AuditDossierDto(
    Guid Id,
    Guid EngagementId,
    string DossierType,
    int Version,
    string Status,
    string IntegritySha256,
    bool NfraInspectionReady,
    int TotalWorkingPapersIncluded,
    int LeadSchedulesCount,
    int TotalExceptionsNoted,
    int TotalOpenNotes,
    string? PartnerSignoffRemarks,
    List<DossierIndexSectionDto> IndexSections,
    DateTime CompiledAt,
    Guid? CompiledBy,
    string? CompiledByName,
    DateTime? SealedAt,
    Guid? SealedBy,
    string? SealedByName,
    DateTime CreatedAt
);

public record CompileDossierRequest(
    string DossierType = "COMBINED_PEER_REVIEW",
    string? Notes = null
);

public record SealDossierRequest(
    string PartnerSignoffRemarks
);

public record DossierExportHtmlDto(
    string HtmlContent,
    string IntegritySha256,
    DateTime GeneratedAt
);

// =========================================================================
// Sprint 45: SA 505 External Confirmation DTOs
// =========================================================================

public record ConfirmationCampaignDto(
    Guid Id,
    Guid AuditEngagementId,
    string Title,
    string? Description,
    DateOnly AsOfDate,
    DateOnly CutoffDate,
    string Status,
    int TotalRequestsCount,
    int ConfirmedAgreedCount,
    int DisputedCount,
    int PendingCount,
    decimal TotalBookBalanceAmount,
    decimal TotalConfirmedAmount,
    decimal VarianceAmountTotal,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ExternalConfirmationRequestDto(
    Guid Id,
    Guid CampaignId,
    Guid AuditEngagementId,
    string LeadScheduleRef,
    string PartyType,
    string PartyName,
    string PartyContactEmail,
    string? PartyContactPhone,
    string? PartyPanGstin,
    string ConfirmationType,
    decimal BookBalanceAmount,
    string Currency,
    DateOnly AsOfDate,
    string SecureAccessToken,
    DateTime TokenExpiresAt,
    string Status,
    DateTime? SentAt,
    DateTime? RespondedAt,
    decimal? ConfirmedBalanceAmount,
    decimal? VarianceAmount,
    string DisputeReason,
    string? ThirdPartyRemarks,
    string? ThirdPartySignatoryName,
    string? ThirdPartyDesignation,
    string? AlternativeProceduresPerformed,
    string AuditorConclusion,
    decimal? ProposedAdjustmentAmount,
    string? AuditorNotes,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    DateTime CreatedAt
);

public record CreateConfirmationCampaignRequest(
    string Title,
    string? Description,
    DateOnly AsOfDate,
    DateOnly CutoffDate
);

public record CreateConfirmationRequest(
    string LeadScheduleRef,
    string PartyType,
    string PartyName,
    string PartyContactEmail,
    string? PartyContactPhone,
    string? PartyPanGstin,
    string ConfirmationType,
    decimal BookBalanceAmount,
    DateOnly AsOfDate
);

public record AutoPopulateConfirmationsRequest(
    Guid? TrialBalanceImportId,
    decimal MinimumDebtorBalance = 100_000m,
    decimal MinimumCreditorBalance = 100_000m,
    int TopNCount = 20
);

public record SubmitConfirmationResponseRequest(
    decimal ConfirmedBalanceAmount,
    string DisputeReason,
    string? ThirdPartyRemarks,
    string? ThirdPartySignatoryName,
    string? ThirdPartyDesignation
);

public record UpdateConfirmationAuditorEvaluationRequest(
    string Status,
    string? AlternativeProceduresPerformed,
    string AuditorConclusion,
    decimal? ProposedAdjustmentAmount,
    string? AuditorNotes
);

public record PublicConfirmationDetailsDto(
    string ClientName,
    string AuditorFirmName,
    string PartyName,
    string PartyType,
    string ConfirmationType,
    decimal? BookBalanceAmount,
    DateOnly AsOfDate,
    bool IsExpired,
    bool AlreadyResponded,
    string CurrentStatus
);

public record ConfirmationSummaryExportHtmlDto(
    string HtmlContent,
    DateTime GeneratedAt
);

// =========================================================================
// Sprint 46: 1-Click ICAI Independent Auditor's Report & Notes Generator DTOs
// =========================================================================

public record KeyAuditMatterDto(
    Guid Id,
    Guid ReportId,
    string Category,
    string Title,
    string DescriptionOfMatter,
    string WhyConsideredSignificant,
    string HowAddressedInAudit,
    string? KeyDocumentationRef
);

public record IndependentAuditorReportDto(
    Guid Id,
    Guid AuditEngagementId,
    string ReportTitle,
    string OpinionType,
    DateOnly ReportDate,
    string PlaceOfSignature,
    string AuditorFirmName,
    string FirmRegistrationNumber,
    string PartnerName,
    string PartnerMembershipNumber,
    string? Udin,
    string OpinionParagraph,
    string BasisForOpinionParagraph,
    string? MaterialUncertaintyGoingConcernParagraph,
    string? EmphasisOfMatterParagraph,
    string? OtherMatterParagraph,
    bool IncludeKeyAuditMatters,
    string? KeyAuditMattersIntro,
    string ResponsibilitiesOfManagementParagraph,
    string AuditorResponsibilitiesParagraph,
    bool Clause143_3_a_InformationObtained,
    bool Clause143_3_b_ProperBooksKept,
    bool Clause143_3_c_AgreementWithBooks,
    bool Clause143_3_d_ComplianceWithAS,
    string? Clause143_3_e_AdverseFunctioningRemarks,
    bool Clause143_3_f_DirectorsDisqualified,
    string? Clause143_3_g_MaintenanceAccountsQualifications,
    bool Clause143_3_h_InternalFinancialControlsAnnexure,
    string Rule11_a_PendingLitigations,
    string Rule11_b_ForeseeableLosses,
    string Rule11_c_IepfTransfers,
    string Rule11_e_IntermediaryFunds,
    string Rule11_f_DividendCompliance,
    string Rule11_g_AuditTrailStatus,
    string Rule11_g_AuditTrailRemarks,
    bool HasCaro2020Annexure,
    string Status,
    List<KeyAuditMatterDto> KeyAuditMatters,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record GenerateAuditorReportRequest(
    string OpinionType = "Unmodified",
    string PlaceOfSignature = "Mumbai",
    string? QualificationDetails = null,
    bool IncludeKeyAuditMatters = true,
    string? EmphasisOfMatter = null,
    string? MaterialUncertaintyGoingConcern = null
);

public record UpdateAuditorReportRequest(
    string OpinionType,
    string PlaceOfSignature,
    string OpinionParagraph,
    string BasisForOpinionParagraph,
    string? MaterialUncertaintyGoingConcernParagraph,
    string? EmphasisOfMatterParagraph,
    string? OtherMatterParagraph,
    bool IncludeKeyAuditMatters,
    string? KeyAuditMattersIntro,
    string ResponsibilitiesOfManagementParagraph,
    string AuditorResponsibilitiesParagraph,
    string Rule11_g_AuditTrailStatus,
    string Rule11_g_AuditTrailRemarks,
    List<KeyAuditMatterDto>? KeyAuditMatters
);

public record IssueReportWithUdinRequest(
    string Udin,
    DateOnly ReportDate
);

public record AuditorReportHtmlExportDto(
    string HtmlContent,
    string OpinionType,
    string? Udin,
    DateTime GeneratedAt
);

public record NotesToAccountsDto(
    Guid Id,
    Guid AuditEngagementId,
    string FinancialYear,
    string Note1CorporateInformation,
    string Note2AccountingPolicies,
    string BalanceSheetNotesJson,
    string ProfitAndLossNotesJson,
    string RegulatoryRatiosJson,
    string AdditionalRegulatoryDisclosures,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record GenerateNotesToAccountsRequest(
    Guid? TrialBalanceImportId = null
);

public record UpdateNotesToAccountsRequest(
    string Note1CorporateInformation,
    string Note2AccountingPolicies,
    string AdditionalRegulatoryDisclosures,
    string Status
);

public record NotesToAccountsHtmlExportDto(
    string HtmlContent,
    DateTime GeneratedAt
);


