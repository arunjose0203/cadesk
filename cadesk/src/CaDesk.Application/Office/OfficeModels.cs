using CaDesk.Domain.Office;

namespace CaDesk.Application.Office;

// =========================================================================
// 1. Attendance Models
// =========================================================================

public sealed record AttendanceCheckInRequest(
    string? Notes = null,
    string? IpAddress = null
);

public sealed record AttendanceCheckOutRequest(
    string? Notes = null
);

public sealed record AttendanceRecordDto(
    Guid Id,
    Guid UserId,
    string UserName,
    DateOnly Date,
    DateTime CheckInTime,
    DateTime? CheckOutTime,
    string Status,
    string? Notes,
    string? IpAddress,
    double? WorkHours
);

public sealed record MonthlyAttendanceSummaryDto(
    Guid UserId,
    string UserName,
    int Month,
    int Year,
    int DaysPresent,
    int DaysHalfDay,
    int DaysOnDuty,
    int DaysAbsent,
    IReadOnlyList<AttendanceRecordDto> Records
);

// =========================================================================
// 2. Leave Models
// =========================================================================

public sealed record ApplyLeaveRequest(
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal DaysCount,
    string Reason
);

public sealed record LeaveBalanceDto(
    Guid Id,
    Guid UserId,
    string UserName,
    int Year,
    string LeaveType,
    decimal AllocatedDays,
    decimal UsedDays,
    decimal AvailableDays
);

public sealed record LeaveRequestDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal DaysCount,
    string Reason,
    string Status,
    Guid? ApproverId,
    string? ApproverName,
    DateTime? ApprovedAt,
    string? RejectionReason,
    DateTime CreatedAt
);

public sealed record LeaveApprovalRequest(
    string? Remarks = null
);

public sealed record LeaveRejectionRequest(
    string Reason
);

// =========================================================================
// 3. Office Expense Models
// =========================================================================

public sealed record SubmitExpenseRequest(
    DateOnly Date,
    string Category,
    decimal Amount,
    string Description,
    Guid? ClientId = null,
    Guid? TaskId = null,
    Guid? ReceiptDocumentId = null,
    bool IsReimbursableByClient = false
);

public sealed record OfficeExpenseDto(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid? ClientId,
    string? ClientName,
    Guid? TaskId,
    DateOnly Date,
    string Category,
    decimal Amount,
    string Description,
    Guid? ReceiptDocumentId,
    bool IsReimbursableByClient,
    bool IsClientBilled,
    Guid? BilledInvoiceId,
    string Status,
    Guid? ApprovedBy,
    string? ApproverName,
    DateTime? ApprovedAt,
    string? RejectionReason,
    DateTime CreatedAt
);

public sealed record ExpenseRejectionRequest(
    string Reason
);

public sealed record MonthlyExpenseReportDto(
    int Month,
    int Year,
    decimal TotalAmount,
    decimal ReimbursableAmount,
    decimal NonReimbursableAmount,
    IReadOnlyDictionary<string, decimal> CategoryBreakdown,
    IReadOnlyList<OfficeExpenseDto> Expenses
);

// =========================================================================
// 4. Inward / Outward Register Models
// =========================================================================

public sealed record CreateRegisterEntryRequest(
    string EntryType,
    DateOnly Date,
    Guid ClientId,
    string Description,
    string DeliveryMode,
    string? CourierName = null,
    string? TrackingNumber = null,
    string? SenderRecipientName = null,
    string? DscTokenRef = null,
    Guid? AcknowledgementFileId = null
);

public sealed record RegisterEntryDto(
    Guid Id,
    string EntryType,
    string EntryNumber,
    DateOnly Date,
    Guid ClientId,
    string ClientName,
    string Description,
    string DeliveryMode,
    string? CourierName,
    string? TrackingNumber,
    string? SenderRecipientName,
    Guid HandledByUserId,
    string HandledByUserName,
    string? DscTokenRef,
    string Status,
    Guid? AcknowledgementFileId,
    DateTime CreatedAt
);

public sealed record RegisterListQuery(
    string? EntryType = null,
    Guid? ClientId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Search = null,
    int Skip = 0,
    int Take = 50
);
