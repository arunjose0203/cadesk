using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Documents;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Tasks;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Office;

public static class AttendanceStatus
{
    public const string Present = "Present";
    public const string HalfDay = "HalfDay";
    public const string OnDuty = "OnDuty";
    public const string Absent = "Absent";
}

public static class LeaveType
{
    public const string Casual = "Casual";
    public const string Sick = "Sick";
    public const string Earned = "Earned";
    public const string Exam = "Exam";
}

public static class LeaveStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class ExpenseCategory
{
    public const string Travel = "Travel";
    public const string RocFees = "RocFees";
    public const string StampPaper = "StampPaper";
    public const string Courier = "Courier";
    public const string Printing = "Printing";
    public const string Meals = "Meals";
    public const string Other = "Other";
}

public static class ExpenseStatus
{
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Reimbursed = "Reimbursed";
}

public static class RegisterEntryType
{
    public const string Inward = "Inward";
    public const string Outward = "Outward";
}

public static class DeliveryMode
{
    public const string Hand = "Hand";
    public const string Courier = "Courier";
    public const string Email = "Email";
    public const string SpeedPost = "SpeedPost";
    public const string RegisteredPost = "RegisteredPost";
}

public static class RegisterStatus
{
    public const string Received = "Received";
    public const string Dispatched = "Dispatched";
    public const string Acknowledged = "Acknowledged";
    public const string Returned = "Returned";
}

/// <summary>Daily staff attendance check-in / check-out with client office notes.</summary>
public class AttendanceRecord : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string Status { get; set; } = AttendanceStatus.Present;
    public string? Notes { get; set; }
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public User? User { get; set; }
}

/// <summary>Annual leave allocation and consumption balance per user.</summary>
public class LeaveBalance : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid UserId { get; set; }
    public int Year { get; set; }
    public string LeaveType { get; set; } = Office.LeaveType.Casual;
    public decimal AllocatedDays { get; set; } = 12.00m;
    public decimal UsedDays { get; set; } = 0.00m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Firm? Firm { get; set; }
    public User? User { get; set; }
}

/// <summary>Leave application submitted by staff, approved/rejected by manager/partner.</summary>
public class LeaveRequest : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid UserId { get; set; }
    public string LeaveType { get; set; } = Office.LeaveType.Casual;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal DaysCount { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = LeaveStatus.Pending;
    public Guid? ApproverId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public User? User { get; set; }
    public User? Approver { get; set; }
}

/// <summary>Staff out-of-pocket expenses, client reimbursables, and receipt vault links.</summary>
public class OfficeExpense : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? TaskId { get; set; }
    public DateOnly Date { get; set; }
    public string Category { get; set; } = ExpenseCategory.Other;
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public Guid? ReceiptDocumentId { get; set; }
    public bool IsReimbursableByClient { get; set; }
    public bool IsClientBilled { get; set; }
    public Guid? BilledInvoiceId { get; set; }
    public string Status { get; set; } = ExpenseStatus.Submitted;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public User? User { get; set; }
    public Client? Client { get; set; }
    public WorkTask? Task { get; set; }
    public Document? ReceiptDocument { get; set; }
}

/// <summary>Physical document, book, and DSC token movement register.</summary>
public class InwardOutwardRegister : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public string EntryType { get; set; } = RegisterEntryType.Inward;
    public string EntryNumber { get; set; } = "";
    public DateOnly Date { get; set; }
    public Guid ClientId { get; set; }
    public string Description { get; set; } = "";
    public string DeliveryMode { get; set; } = Office.DeliveryMode.Hand;
    public string? CourierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? SenderRecipientName { get; set; }
    public Guid HandledByUserId { get; set; }
    public string? DscTokenRef { get; set; }
    public string Status { get; set; } = RegisterStatus.Received;
    public Guid? AcknowledgementFileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public User? HandledByUser { get; set; }
    public Document? AcknowledgementFile { get; set; }
}
