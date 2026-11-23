namespace CaDesk.Application.PartnerCompanion;

public sealed record PartnerPulseSummaryDto(
    decimal TodayCollections,
    decimal MonthCollections,
    decimal UnbilledWipAmount,
    decimal BillableHoursThisWeek,
    int ActiveStaffCount,
    int CriticalDeadlinesCount,
    int PendingAuditSignoffsCount,
    int PendingTimesheetApprovalsCount,
    int PendingInvoiceApprovalsCount,
    int UnresolvedForensicAlertsCount
);

public sealed record PendingAuditItemDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string FinancialYear,
    string AuditType,
    decimal OverallMateriality,
    string AuditOpinion,
    int CompletedSchedulesCount,
    int TotalSchedulesCount,
    string Status,
    DateOnly? PlannedEndDate
);

public sealed record PendingTimesheetItemDto(
    Guid Id,
    string StaffName,
    string ClientName,
    DateOnly Date,
    decimal Hours,
    decimal HourlyRate,
    decimal Amount,
    string Description
);

public sealed record PendingInvoiceItemDto(
    Guid Id,
    string InvoiceNumber,
    string ClientName,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    string Status
);

public sealed record ForensicAlertItemDto(
    Guid Id,
    string ClientName,
    string AnomalyType,
    string RiskLevel,
    decimal Amount,
    decimal ExposureAmount,
    string Counterparty,
    string Narration,
    string StatutorySection,
    string Status,
    DateOnly TransactionDate
);

public sealed record DeadlineCountdownItemDto(
    Guid Id,
    string Title,
    DateOnly DueDate,
    int DaysRemaining,
    string Category,
    string Status
);

public sealed record PartnerMobileSecurityDto(
    bool HasQuickPin,
    bool BiometricEnabled,
    int AutoLockMinutes
);

public sealed record PartnerActionCenterResponse(
    PartnerPulseSummaryDto Pulse,
    IReadOnlyList<PendingAuditItemDto> PendingAudits,
    IReadOnlyList<PendingTimesheetItemDto> PendingTimesheets,
    IReadOnlyList<PendingInvoiceItemDto> PendingInvoices,
    IReadOnlyList<ForensicAlertItemDto> ForensicAlerts,
    IReadOnlyList<DeadlineCountdownItemDto> Deadlines,
    PartnerMobileSecurityDto Security
);

public sealed record QuickSignAuditRequest(
    string? PartnerMembershipNo,
    string? Udin,
    string? Remarks
);

public sealed record QuickApproveTimesheetsRequest(
    List<Guid> TimeEntryIds
);

public sealed record QuickApproveInvoiceRequest(
    Guid InvoiceId
);

public sealed record TriageForensicAlertRequest(
    string Status,
    string? AuditorNotes
);

public sealed record SetupQuickPinRequest(
    string Pin,
    bool BiometricEnabled = false
);

public sealed record VerifyQuickPinRequest(
    string Pin
);

public sealed record QuickPinVerificationResult(
    bool Success,
    string? Message = null
);
