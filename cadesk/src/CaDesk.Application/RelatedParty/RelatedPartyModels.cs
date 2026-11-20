using System;
using System.Collections.Generic;

namespace CaDesk.Application.RelatedParty;

public record RelatedPartyEntityDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string Name,
    string RelationshipType,
    string Pan,
    string Cin,
    string DirectorOrKmpName,
    string NatureOfInterest,
    bool IsActive,
    int TransactionCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record RelatedPartyTransactionDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    Guid RelatedPartyId,
    string? RelatedPartyName,
    string? RelationshipType,
    string FinancialYear,
    DateTime TransactionDate,
    string ContractReference,
    string TransactionType,
    string Description,
    decimal TransactionAmount,
    string Currency,
    decimal AnnualTurnoverBenchmark,
    decimal NetWorthBenchmark,
    decimal ThresholdLimitAmount,
    decimal ThresholdPercentage,
    bool ExceedsStatutoryThreshold,
    bool IsOrdinaryCourseOfBusiness,
    bool IsArmLength,
    string ArmLengthMethod,
    decimal BenchmarkArmLengthPrice,
    decimal PriceVariancePercentage,
    string AuditCommitteeApprovalStatus,
    DateTime? AuditCommitteeMeetingDate,
    Guid? OmnibusApprovalId,
    DateTime? BoardResolutionDate,
    DateTime? ShareholdersResolutionDate,
    bool RequiresShareholderApproval,
    bool Section40A2bRisk,
    DateTime? Mbp4EnteredDate,
    decimal OutstandingBalanceAtYearEnd,
    string BalanceType,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record RelatedPartyOmnibusApprovalDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string FinancialYear,
    DateTime AuditCommitteeMeetingDate,
    string Title,
    decimal MaximumApprovedLimit,
    decimal UtilizedAmount,
    string ApprovalCriteria,
    DateTime ValidityPeriodTo,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record RelatedPartySummaryDto(
    int TotalParties,
    int TotalTransactions,
    decimal TotalVolumeInr,
    decimal ArmLengthComplianceRate,
    int SpecialResolutionRequiredCount,
    int AuditCommitteeApprovedCount,
    int Sec40A2bRiskCount,
    decimal TotalYearEndBalancesInr
);

public record SaveRelatedPartyEntityRequest(
    Guid ClientId,
    string Name,
    string RelationshipType,
    string? Pan,
    string? Cin,
    string? DirectorOrKmpName,
    string? NatureOfInterest,
    bool IsActive
);

public record SaveRelatedPartyTransactionRequest(
    Guid ClientId,
    Guid RelatedPartyId,
    string FinancialYear,
    DateTime TransactionDate,
    string? ContractReference,
    string TransactionType,
    string? Description,
    decimal TransactionAmount,
    string? Currency,
    decimal AnnualTurnoverBenchmark,
    decimal NetWorthBenchmark,
    bool IsOrdinaryCourseOfBusiness,
    string? ArmLengthMethod,
    decimal BenchmarkArmLengthPrice,
    string? AuditCommitteeApprovalStatus,
    DateTime? AuditCommitteeMeetingDate,
    Guid? OmnibusApprovalId,
    DateTime? BoardResolutionDate,
    DateTime? ShareholdersResolutionDate,
    DateTime? Mbp4EnteredDate,
    decimal OutstandingBalanceAtYearEnd,
    string? BalanceType
);

public record SaveOmnibusApprovalRequest(
    Guid ClientId,
    string FinancialYear,
    DateTime AuditCommitteeMeetingDate,
    string Title,
    decimal MaximumApprovedLimit,
    string? ApprovalCriteria,
    DateTime ValidityPeriodTo
);

public record ApproveTransactionRequest(
    string ApprovalStatus, // Approved, OmnibusApproved
    DateTime? MeetingDate,
    Guid? OmnibusApprovalId,
    DateTime? BoardDate,
    DateTime? ShareholdersDate
);

public record RelatedPartyReportsDto(
    string Mbp4RegisterText,
    string As18DisclosureNoteText
);
