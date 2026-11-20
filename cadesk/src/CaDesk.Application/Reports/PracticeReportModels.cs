namespace CaDesk.Application.Reports;

public sealed record PracticeExecutiveSummaryDto(
    string FinancialYear,
    decimal TotalBilledYtd,
    decimal TotalCollectedYtd,
    decimal TotalOutstandingReceivables,
    decimal OverallRealizationRatePct,
    decimal TotalBillableHoursLogged,
    decimal TotalNonBillableHoursLogged,
    decimal StaffUtilizationPct,
    int TotalDeadlinesCount,
    int FiledOnTimeCount,
    int FiledLateCount,
    int OverdueCount,
    decimal SlaCompliancePct,
    int ActiveClientsCount,
    decimal AverageRevenuePerClient,
    PracticeTargetDto? Targets);

public sealed record ClientProfitabilityDto(
    Guid ClientId,
    string ClientName,
    string? EntityType,
    decimal TotalInvoiced,
    decimal TotalPaid,
    decimal BalanceDue,
    decimal TotalHoursLogged,
    decimal EffectiveHourlyRate,
    string ProfitabilityRating);

public sealed record StaffUtilizationDto(
    Guid UserId,
    string StaffName,
    string RoleName,
    decimal BillableHours,
    decimal NonBillableHours,
    decimal TotalHours,
    decimal UtilizationPct,
    decimal BilledValueGenerated);

public sealed record ReceivablesAgingDto(
    decimal TotalReceivables,
    decimal Current0To30Days,
    decimal Bucket31To60Days,
    decimal Bucket61To90Days,
    decimal BucketAbove90Days,
    List<AgedInvoiceItemDto> AgedInvoices);

public sealed record AgedInvoiceItemDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid ClientId,
    string ClientName,
    DateOnly IssueDate,
    DateOnly DueDate,
    int AgeDays,
    decimal TotalAmount,
    decimal BalanceDue,
    string AgingBucket);

public sealed record ComplianceSlaReportDto(
    string FinancialYear,
    int TotalStatutoryFilings,
    int FiledOnTime,
    int FiledLate,
    int CurrentlyOverdue,
    decimal OverallSlaPercentage,
    List<ComplianceTypeSlaDto> CategoryBreakdown);

public sealed record ComplianceTypeSlaDto(
    string Category,
    int TotalCount,
    int FiledOnTime,
    int FiledLate,
    int Overdue,
    decimal SlaPercentage);

public sealed record RevenueBreakdownDto(
    string ServiceCategory,
    decimal Amount,
    decimal PercentageOfTotal);

public sealed record PracticeTargetDto(
    Guid Id,
    string FinancialYear,
    decimal TargetAnnualRevenue,
    decimal TargetBillableHours,
    decimal TargetRecoveryPercentage,
    decimal TargetComplianceSlaPct,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SetPracticeTargetRequest(
    string FinancialYear,
    decimal TargetAnnualRevenue,
    decimal TargetBillableHours,
    decimal TargetRecoveryPercentage,
    decimal TargetComplianceSlaPct);
