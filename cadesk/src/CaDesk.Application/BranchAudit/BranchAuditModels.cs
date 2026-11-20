using System;
using System.Collections.Generic;

namespace CaDesk.Application.BranchAudit;

public record BranchAuditOfficeDto(
    Guid Id,
    Guid EngagementId,
    string BranchCode,
    string BranchName,
    string BranchLocation,
    bool IsForeignBranch,
    string AuditorFirmName,
    string AuditorFirmRegistrationNumber,
    string AuditorName,
    string AuditorMembershipNumber,
    string AuditorEmail,
    string? AuditorPhone,
    bool HasActiveToken,
    DateTime? TokenExpiresAt,
    DateTime? LastSubmittedAt,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal NetProfit,
    string BranchReportType,
    bool HasQualifications,
    string? QualificationsSummary,
    string? BranchAuditReportRef,
    bool AccountingPoliciesConsistent,
    bool RelatedPartyTransactionsDisclosed,
    bool ContingentLiabilitiesReported,
    bool SubsequentEventsReported,
    bool FraudOrIrregularitiesReported,
    bool InternalControlsCommensurate,
    bool CaroApplicableToBranch,
    string? CaroReportSummary,
    string Status,
    string? PrincipalAuditorReviewNotes,
    Guid? ReviewedByUserId,
    string? ReviewedByUserName,
    DateTime? ReviewedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record BranchAuditEngagementDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string? ClientPan,
    Guid? AuditEngagementId,
    string? AuditEngagementTitle,
    string FinancialYear,
    string Title,
    decimal HeadOfficeAssets,
    decimal HeadOfficeLiabilities,
    decimal HeadOfficeRevenue,
    decimal HeadOfficeExpenses,
    decimal HeadOfficeNetProfit,
    decimal TotalConsolidatedAssets,
    decimal TotalConsolidatedLiabilities,
    decimal TotalConsolidatedRevenue,
    decimal TotalConsolidatedExpenses,
    decimal TotalConsolidatedNetProfit,
    int BranchesAuditedCount,
    int BranchAuditorsCount,
    decimal TotalBranchAssets,
    decimal BranchAssetsPercentage,
    decimal TotalBranchRevenue,
    decimal BranchRevenuePercentage,
    decimal TotalBranchNetProfit,
    decimal BranchNetProfitPercentage,
    string ConsolidationStatus,
    int BranchQualificationsCount,
    string? PrincipalAuditorNotes,
    string? GeneratedSa600ReportText,
    Guid? FinalizedByUserId,
    string? FinalizedByUserName,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<BranchAuditOfficeDto> Branches
);

public record SaveBranchEngagementRequest(
    Guid ClientId,
    Guid? AuditEngagementId,
    string FinancialYear,
    string Title,
    decimal HeadOfficeAssets,
    decimal HeadOfficeLiabilities,
    decimal HeadOfficeRevenue,
    decimal HeadOfficeExpenses,
    decimal HeadOfficeNetProfit,
    string? PrincipalAuditorNotes
);

public record SaveBranchOfficeRequest(
    string BranchCode,
    string BranchName,
    string BranchLocation,
    bool IsForeignBranch,
    string AuditorFirmName,
    string AuditorFirmRegistrationNumber,
    string AuditorName,
    string AuditorMembershipNumber,
    string AuditorEmail,
    string? AuditorPhone,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal NetProfit,
    string BranchReportType,
    bool HasQualifications,
    string? QualificationsSummary,
    string? BranchAuditReportRef,
    bool AccountingPoliciesConsistent,
    bool RelatedPartyTransactionsDisclosed,
    bool ContingentLiabilitiesReported,
    bool SubsequentEventsReported,
    bool FraudOrIrregularitiesReported,
    bool InternalControlsCommensurate,
    bool CaroApplicableToBranch,
    string? CaroReportSummary
);

public record SubmitBranchReturnsRequest(
    string Token,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal NetProfit,
    string BranchReportType,
    bool HasQualifications,
    string? QualificationsSummary,
    string? BranchAuditReportRef,
    bool AccountingPoliciesConsistent,
    bool RelatedPartyTransactionsDisclosed,
    bool ContingentLiabilitiesReported,
    bool SubsequentEventsReported,
    bool FraudOrIrregularitiesReported,
    bool InternalControlsCommensurate,
    bool CaroApplicableToBranch,
    string? CaroReportSummary
);

public record ReviewBranchOfficeRequest(
    string Decision, // "accept" or "reject"
    string? ReviewNotes
);

public record GenerateTokenResponse(
    Guid BranchId,
    string RawToken,
    DateTime ExpiresAt,
    string PortalUrl
);

public record BranchAuditSummaryDto(
    int TotalEngagements,
    int ConsolidatedCount,
    int FinalizedCount,
    int TotalBranches,
    int BranchesSubmitted,
    int BranchesAccepted,
    int TotalBranchQualifications,
    decimal TotalBranchAssetsAudited,
    decimal AverageBranchAssetsPercentage
);
