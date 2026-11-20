using System;
using System.Collections.Generic;

namespace CaDesk.Application.TransferPricing;

public record TransferPricingSummaryDto(
    int TotalStudies,
    int ActiveStudies,
    int CertifiedStudies,
    int TotalAssociatedEnterprises,
    int TotalInternationalTransactions,
    decimal TotalInternationalVolumeInr,
    int TotalSdtTransactions,
    decimal TotalSdtVolumeInr,
    int TotalAlpAdjustmentsCount,
    decimal TotalAlpAdjustmentAmount);

public record TransferPricingStudyDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string StudyReference,
    string Title,
    string FinancialYear,
    string AssessmentYear,
    DateTime AuditPeriodStart,
    DateTime AuditPeriodEnd,
    bool Rule10DDocumentationMaintained,
    string Rule10DDocumentationPath,
    string StudyStatus,
    string AccountantName,
    string AccountantFirmName,
    string MembershipNumber,
    string FirmRegistrationNumber,
    string? Udin,
    DateTime? CertifiedAt,
    string? CertifiedBy,
    int TotalAssociatedEnterprisesCount,
    int TotalInternationalTransactionsCount,
    decimal TotalInternationalInrValue,
    int TotalSdtTransactionsCount,
    decimal TotalSdtInrValue,
    int TotalAlpAdjustmentsCount,
    decimal TotalAlpAdjustmentAmount,
    int TransactionsWithinToleranceCount,
    int TransactionsOutsideToleranceCount,
    string? Form3CebReportText,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AssociatedEnterpriseDto(
    Guid Id,
    Guid StudyId,
    string EnterpriseName,
    string CountryOfResidence,
    string CountryCode,
    string TaxIdentificationNumber,
    string RegisteredAddress,
    string RelationshipSection,
    decimal OwnershipPercentage,
    string BriefActivityDescription,
    DateTime CreatedAt);

public record TransferPricingTransactionDto(
    Guid Id,
    Guid StudyId,
    Guid? AssociatedEnterpriseId,
    string? AssociatedEnterpriseName,
    string? AssociatedEnterpriseCountry,
    string TransactionCategory,
    int ClauseNumber,
    string DescriptionOfTransaction,
    string CurrencyCode,
    decimal ForeignCurrencyAmount,
    decimal ExchangeRate,
    decimal InrValue,
    string MostAppropriateMethod,
    string ProfitLevelIndicator,
    decimal TestedPartyCostBase,
    decimal TestedPartyMargin,
    int ComparablesCount,
    decimal BenchmarkRangeMin,
    decimal BenchmarkRangeMax,
    decimal BenchmarkMedianOrMean,
    decimal ToleranceBandPercentage,
    bool IsWithinArmLength,
    decimal AlpAdjustmentAmount,
    string MethodSelectionRationale,
    string DocumentationReference,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ComparableBenchmarkDto(
    Guid Id,
    Guid StudyId,
    Guid? TransactionId,
    string ComparableCompanyName,
    string CinOrIdentifier,
    string FinancialYear,
    decimal OperatingRevenue,
    decimal OperatingCost,
    decimal OperatingProfit,
    decimal CalculatedMarginPercentage,
    decimal Weight,
    bool IsAccepted,
    string? RejectionReason,
    DateTime CreatedAt);

public record SaveStudyRequest(
    Guid ClientId,
    string StudyReference,
    string Title,
    string FinancialYear,
    string AssessmentYear,
    DateTime AuditPeriodStart,
    DateTime AuditPeriodEnd,
    bool Rule10DDocumentationMaintained,
    string Rule10DDocumentationPath,
    string AccountantName,
    string AccountantFirmName,
    string MembershipNumber,
    string FirmRegistrationNumber);

public record CertifyStudyRequest(
    string Udin,
    string CertifiedBy);

public record SaveEnterpriseRequest(
    string EnterpriseName,
    string CountryOfResidence,
    string CountryCode,
    string TaxIdentificationNumber,
    string RegisteredAddress,
    string RelationshipSection,
    decimal OwnershipPercentage,
    string BriefActivityDescription);

public record SaveTransactionRequest(
    Guid? AssociatedEnterpriseId,
    string TransactionCategory,
    int ClauseNumber,
    string DescriptionOfTransaction,
    string CurrencyCode,
    decimal ForeignCurrencyAmount,
    decimal ExchangeRate,
    string MostAppropriateMethod,
    string ProfitLevelIndicator,
    decimal TestedPartyCostBase,
    decimal TestedPartyMargin,
    decimal ToleranceBandPercentage,
    string MethodSelectionRationale,
    string DocumentationReference);

public record SaveComparableRequest(
    Guid? TransactionId,
    string ComparableCompanyName,
    string CinOrIdentifier,
    string FinancialYear,
    decimal OperatingRevenue,
    decimal OperatingCost,
    decimal OperatingProfit,
    decimal Weight,
    bool IsAccepted,
    string? RejectionReason);

public record BenchmarkCalculationRequest(
    List<decimal> ComparableMargins,
    decimal ToleranceBandPercentage);

public record BenchmarkCalculationResult(
    bool UseRange,
    int DataCount,
    decimal RangeMin,
    decimal RangeMax,
    decimal MedianOrMean,
    string Explanation);
