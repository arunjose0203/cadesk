using System.Text;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Office;
using CaDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Office;

/// <summary>
/// Sprint 64: Daily office operations engine for CA practices:
/// 1. Staff Attendance (Check-in/out, hours computation, monthly register, CSV export)
/// 2. Leave Management (Balance allocations, applications, manager approvals, auto-assignment suppression)
/// 3. Office Expenses & Client Reimbursables (Out-of-pocket expenses, receipts, client billing link, monthly reporting)
/// 4. Inward / Outward Physical Registers (Document & DSC custody tracking, printable registers)
/// </summary>
public sealed class OfficeService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock)
{
    private DateTime NowUtc => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(NowUtc);

    // =========================================================================
    // 1. Attendance Operations
    // =========================================================================

    public async Task<AttendanceRecordDto> CheckInAsync(Guid userId, AttendanceCheckInRequest req, CancellationToken ct = default)
    {
        var existing = await db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == Today, ct);

        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(req.Notes))
                existing.Notes = string.IsNullOrWhiteSpace(existing.Notes) ? req.Notes : $"{existing.Notes}; {req.Notes}";
            if (!string.IsNullOrWhiteSpace(req.IpAddress))
                existing.IpAddress = req.IpAddress;

            existing.UpdatedAt = NowUtc;
            existing.UpdatedBy = userId;
            await db.SaveChangesAsync(ct);
            return await MapAttendanceDtoAsync(existing, ct);
        }

        var record = new AttendanceRecord
        {
            FirmId = tenant.FirmId,
            UserId = userId,
            Date = Today,
            CheckInTime = NowUtc,
            Status = AttendanceStatus.Present,
            Notes = req.Notes,
            IpAddress = req.IpAddress,
            CreatedAt = NowUtc,
            CreatedBy = userId
        };

        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return await MapAttendanceDtoAsync(record, ct);
    }

    public async Task<AttendanceRecordDto> CheckOutAsync(Guid userId, AttendanceCheckOutRequest req, CancellationToken ct = default)
    {
        var record = await db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == Today, ct);

        if (record is null)
            throw new BusinessRuleException("No check-in record found for today. Please check in first.", 400);

        record.CheckOutTime = NowUtc;
        var durationHours = (record.CheckOutTime.Value - record.CheckInTime).TotalHours;

        // If less than 4 hours worked, classify as HalfDay
        if (durationHours < 4.0 && record.Status == AttendanceStatus.Present)
            record.Status = AttendanceStatus.HalfDay;

        if (!string.IsNullOrWhiteSpace(req.Notes))
            record.Notes = string.IsNullOrWhiteSpace(record.Notes) ? req.Notes : $"{record.Notes}; {req.Notes}";

        record.UpdatedAt = NowUtc;
        record.UpdatedBy = userId;
        await db.SaveChangesAsync(ct);
        return await MapAttendanceDtoAsync(record, ct);
    }

    public async Task<AttendanceRecordDto?> GetTodayAsync(Guid userId, CancellationToken ct = default)
    {
        var record = await db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == Today, ct);

        return record is null ? null : await MapAttendanceDtoAsync(record, ct);
    }

    public async Task<MonthlyAttendanceSummaryDto> GetMonthlyRegisterAsync(int month, int year, Guid? userId, CancellationToken ct = default)
    {
        var targetUser = userId ?? tenant.UserId ?? Guid.Empty;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetUser, ct);
        var userName = user?.Name ?? "Staff Member";

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.UserId == targetUser && a.Date >= startDate && a.Date <= endDate)
            .OrderBy(a => a.Date)
            .ToListAsync(ct);

        var dtos = records.Select(r => new AttendanceRecordDto(
            r.Id,
            r.UserId,
            userName,
            r.Date,
            r.CheckInTime,
            r.CheckOutTime,
            r.Status,
            r.Notes,
            r.IpAddress,
            r.CheckOutTime.HasValue ? Math.Round((r.CheckOutTime.Value - r.CheckInTime).TotalHours, 2) : null
        )).ToList();

        return new MonthlyAttendanceSummaryDto(
            UserId: targetUser,
            UserName: userName,
            Month: month,
            Year: year,
            DaysPresent: records.Count(r => r.Status == AttendanceStatus.Present),
            DaysHalfDay: records.Count(r => r.Status == AttendanceStatus.HalfDay),
            DaysOnDuty: records.Count(r => r.Status == AttendanceStatus.OnDuty),
            DaysAbsent: records.Count(r => r.Status == AttendanceStatus.Absent),
            Records: dtos
        );
    }

    public async Task<string> ExportAttendanceCsvAsync(int month, int year, CancellationToken ct = default)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var records = await (from a in db.AttendanceRecords.AsNoTracking()
                             join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                             where a.Date >= startDate && a.Date <= endDate
                             orderby a.Date, u.Name
                             select new { a, u.Name }).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Staff Name,Check In,Check Out,Work Hours,Status,Notes,IP Address");
        foreach (var r in records)
        {
            var hours = r.a.CheckOutTime.HasValue
                ? Math.Round((r.a.CheckOutTime.Value - r.a.CheckInTime).TotalHours, 2).ToString("F2")
                : "";
            var checkIn = r.a.CheckInTime.ToString("yyyy-MM-dd HH:mm:ss");
            var checkOut = r.a.CheckOutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            var notes = (r.a.Notes ?? "").Replace("\"", "\"\"");
            sb.AppendLine($"{r.a.Date:yyyy-MM-dd},\"{r.Name}\",{checkIn},{checkOut},{hours},{r.a.Status},\"{notes}\",{r.a.IpAddress ?? ""}");
        }

        return sb.ToString();
    }

    private async Task<AttendanceRecordDto> MapAttendanceDtoAsync(AttendanceRecord r, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        return new AttendanceRecordDto(
            r.Id,
            r.UserId,
            user?.Name ?? "Staff Member",
            r.Date,
            r.CheckInTime,
            r.CheckOutTime,
            r.Status,
            r.Notes,
            r.IpAddress,
            r.CheckOutTime.HasValue ? Math.Round((r.CheckOutTime.Value - r.CheckInTime).TotalHours, 2) : null
        );
    }

    // =========================================================================
    // 2. Leave Management Operations
    // =========================================================================

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(Guid userId, int? year = null, CancellationToken ct = default)
    {
        var y = year ?? Today.Year;
        var balances = await db.LeaveBalances
            .Where(b => b.UserId == userId && b.Year == y)
            .ToListAsync(ct);

        if (balances.Count == 0)
        {
            // Initialize default statutory allocations for CA firm staff
            balances =
            [
                new LeaveBalance { FirmId = tenant.FirmId, UserId = userId, Year = y, LeaveType = LeaveType.Casual, AllocatedDays = 12m, UsedDays = 0m },
                new LeaveBalance { FirmId = tenant.FirmId, UserId = userId, Year = y, LeaveType = LeaveType.Sick, AllocatedDays = 10m, UsedDays = 0m },
                new LeaveBalance { FirmId = tenant.FirmId, UserId = userId, Year = y, LeaveType = LeaveType.Earned, AllocatedDays = 15m, UsedDays = 0m },
                new LeaveBalance { FirmId = tenant.FirmId, UserId = userId, Year = y, LeaveType = LeaveType.Exam, AllocatedDays = 20m, UsedDays = 0m }
            ];
            db.LeaveBalances.AddRange(balances);
            await db.SaveChangesAsync(ct);
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        var uName = user?.Name ?? "Staff Member";

        return balances.Select(b => new LeaveBalanceDto(
            b.Id,
            b.UserId,
            uName,
            b.Year,
            b.LeaveType,
            b.AllocatedDays,
            b.UsedDays,
            Math.Max(0m, b.AllocatedDays - b.UsedDays)
        )).ToList();
    }

    public async Task<LeaveRequestDto> ApplyLeaveAsync(Guid userId, ApplyLeaveRequest req, CancellationToken ct = default)
    {
        if (req.EndDate < req.StartDate)
            throw new BusinessRuleException("End date cannot precede start date.", 400);

        if (req.DaysCount <= 0m)
            throw new BusinessRuleException("Leave duration must be greater than zero.", 400);

        var request = new LeaveRequest
        {
            FirmId = tenant.FirmId,
            UserId = userId,
            LeaveType = req.LeaveType,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            DaysCount = req.DaysCount,
            Reason = req.Reason.Trim(),
            Status = LeaveStatus.Pending,
            CreatedAt = NowUtc,
            CreatedBy = userId
        };

        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return new LeaveRequestDto(
            request.Id,
            request.UserId,
            user?.Name ?? "Staff Member",
            request.LeaveType,
            request.StartDate,
            request.EndDate,
            request.DaysCount,
            request.Reason,
            request.Status,
            null,
            null,
            null,
            null,
            request.CreatedAt
        );
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> ListRequestsAsync(Guid? userId = null, string? status = null, int? year = null, CancellationToken ct = default)
    {
        var q = db.LeaveRequests.AsNoTracking();

        if (userId.HasValue)
            q = q.Where(r => r.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status);

        if (year.HasValue)
            q = q.Where(r => r.StartDate.Year == year.Value || r.EndDate.Year == year.Value);

        var list = await q.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

        var userIds = list.Select(r => r.UserId).Concat(list.Where(r => r.ApproverId.HasValue).Select(r => r.ApproverId!.Value)).Distinct().ToList();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return list.Select(r => new LeaveRequestDto(
            r.Id,
            r.UserId,
            userNames.GetValueOrDefault(r.UserId, "Staff Member"),
            r.LeaveType,
            r.StartDate,
            r.EndDate,
            r.DaysCount,
            r.Reason,
            r.Status,
            r.ApproverId,
            r.ApproverId.HasValue ? userNames.GetValueOrDefault(r.ApproverId.Value) : null,
            r.ApprovedAt,
            r.RejectionReason,
            r.CreatedAt
        )).ToList();
    }

    public async Task<LeaveRequestDto> ApproveLeaveAsync(Guid requestId, Guid approverId, string? remarks = null, CancellationToken ct = default)
    {
        var req = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null) throw new BusinessRuleException("Leave request not found.", 404);

        if (req.Status != LeaveStatus.Pending)
            throw new BusinessRuleException($"Leave request cannot be approved from status '{req.Status}'.", 400);

        req.Status = LeaveStatus.Approved;
        req.ApproverId = approverId;
        req.ApprovedAt = NowUtc;
        req.UpdatedAt = NowUtc;
        req.UpdatedBy = approverId;

        // Deduct from user's leave balance
        var balance = await db.LeaveBalances
            .FirstOrDefaultAsync(b => b.UserId == req.UserId && b.Year == req.StartDate.Year && b.LeaveType == req.LeaveType, ct);

        if (balance is not null)
        {
            balance.UsedDays += req.DaysCount;
            balance.UpdatedAt = NowUtc;
        }

        // Add audit log entry
        db.AuditLogs.Add(new AuditLog
        {
            FirmId = tenant.FirmId,
            ActorId = approverId,
            ActorType = AuditActor.User,
            Entity = "leave_request",
            EntityId = req.Id.ToString(),
            Action = AuditAction.Approve,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { req.LeaveType, req.DaysCount, req.StartDate, req.EndDate, remarks }),
            At = NowUtc
        });

        await db.SaveChangesAsync(ct);

        var users = await db.Users.AsNoTracking()
            .Where(u => u.Id == req.UserId || u.Id == approverId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return new LeaveRequestDto(
            req.Id,
            req.UserId,
            users.GetValueOrDefault(req.UserId, "Staff Member"),
            req.LeaveType,
            req.StartDate,
            req.EndDate,
            req.DaysCount,
            req.Reason,
            req.Status,
            req.ApproverId,
            users.GetValueOrDefault(approverId),
            req.ApprovedAt,
            req.RejectionReason,
            req.CreatedAt
        );
    }

    public async Task<LeaveRequestDto> RejectLeaveAsync(Guid requestId, Guid approverId, string reason, CancellationToken ct = default)
    {
        var req = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null) throw new BusinessRuleException("Leave request not found.", 404);

        if (req.Status != LeaveStatus.Pending)
            throw new BusinessRuleException($"Leave request cannot be rejected from status '{req.Status}'.", 400);

        req.Status = LeaveStatus.Rejected;
        req.ApproverId = approverId;
        req.RejectionReason = reason.Trim();
        req.UpdatedAt = NowUtc;
        req.UpdatedBy = approverId;

        await db.SaveChangesAsync(ct);

        var users = await db.Users.AsNoTracking()
            .Where(u => u.Id == req.UserId || u.Id == approverId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return new LeaveRequestDto(
            req.Id,
            req.UserId,
            users.GetValueOrDefault(req.UserId, "Staff Member"),
            req.LeaveType,
            req.StartDate,
            req.EndDate,
            req.DaysCount,
            req.Reason,
            req.Status,
            req.ApproverId,
            users.GetValueOrDefault(approverId),
            req.ApprovedAt,
            req.RejectionReason,
            req.CreatedAt
        );
    }

    public async Task<bool> IsUserOnApprovedLeaveAsync(Guid userId, DateOnly date, CancellationToken ct = default)
    {
        return await db.LeaveRequests.AsNoTracking()
            .AnyAsync(r => r.UserId == userId && r.Status == LeaveStatus.Approved && r.StartDate <= date && r.EndDate >= date, ct);
    }

    public async Task<string> ExportLeaveRegisterCsvAsync(int year, CancellationToken ct = default)
    {
        var requests = await (from r in db.LeaveRequests.AsNoTracking()
                              join u in db.Users.AsNoTracking() on r.UserId equals u.Id
                              where r.StartDate.Year == year || r.EndDate.Year == year
                              orderby r.StartDate descending
                              select new { r, UserName = u.Name }).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Staff Name,Leave Type,Start Date,End Date,Days,Status,Reason,Approved At,Rejection Reason");
        foreach (var x in requests)
        {
            var reason = (x.r.Reason ?? "").Replace("\"", "\"\"");
            var rej = (x.r.RejectionReason ?? "").Replace("\"", "\"\"");
            sb.AppendLine($"\"{x.UserName}\",{x.r.LeaveType},{x.r.StartDate:yyyy-MM-dd},{x.r.EndDate:yyyy-MM-dd},{x.r.DaysCount},{x.r.Status},\"{reason}\",{x.r.ApprovedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""},\"{rej}\"");
        }
        return sb.ToString();
    }

    // =========================================================================
    // 3. Office Expenses & Client Reimbursables Operations
    // =========================================================================

    public async Task<OfficeExpenseDto> SubmitExpenseAsync(Guid userId, SubmitExpenseRequest req, CancellationToken ct = default)
    {
        if (req.Amount <= 0m)
            throw new BusinessRuleException("Expense amount must be greater than zero.", 400);

        if (req.Date > Today)
            throw new BusinessRuleException("Expense date cannot be in the future.", 400);

        var expense = new OfficeExpense
        {
            FirmId = tenant.FirmId,
            UserId = userId,
            ClientId = req.ClientId,
            TaskId = req.TaskId,
            Date = req.Date,
            Category = req.Category,
            Amount = req.Amount,
            Description = req.Description.Trim(),
            ReceiptDocumentId = req.ReceiptDocumentId,
            IsReimbursableByClient = req.IsReimbursableByClient,
            IsClientBilled = false,
            Status = ExpenseStatus.Submitted,
            CreatedAt = NowUtc,
            CreatedBy = userId
        };

        db.OfficeExpenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return await MapExpenseDtoAsync(expense, ct);
    }

    public async Task<IReadOnlyList<OfficeExpenseDto>> ListExpensesAsync(
        Guid? userId = null,
        Guid? clientId = null,
        string? status = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var q = db.OfficeExpenses.AsNoTracking();

        if (userId.HasValue) q = q.Where(e => e.UserId == userId.Value);
        if (clientId.HasValue) q = q.Where(e => e.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value);
        if (to.HasValue) q = q.Where(e => e.Date <= to.Value);

        var list = await q.OrderByDescending(e => e.Date).ToListAsync(ct);
        return await MapExpenseDtosAsync(list, ct);
    }

    public async Task<OfficeExpenseDto> ApproveExpenseAsync(Guid expenseId, Guid approverId, CancellationToken ct = default)
    {
        var expense = await db.OfficeExpenses.FirstOrDefaultAsync(e => e.Id == expenseId, ct);
        if (expense is null) throw new BusinessRuleException("Expense not found.", 404);

        expense.Status = ExpenseStatus.Approved;
        expense.ApprovedBy = approverId;
        expense.ApprovedAt = NowUtc;
        expense.UpdatedAt = NowUtc;
        expense.UpdatedBy = approverId;

        db.AuditLogs.Add(new AuditLog
        {
            FirmId = tenant.FirmId,
            ActorId = approverId,
            ActorType = AuditActor.User,
            Entity = "office_expense",
            EntityId = expense.Id.ToString(),
            Action = AuditAction.Approve,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { expense.Category, expense.Amount, expense.IsReimbursableByClient }),
            At = NowUtc
        });

        await db.SaveChangesAsync(ct);
        return await MapExpenseDtoAsync(expense, ct);
    }

    public async Task<OfficeExpenseDto> RejectExpenseAsync(Guid expenseId, Guid approverId, string reason, CancellationToken ct = default)
    {
        var expense = await db.OfficeExpenses.FirstOrDefaultAsync(e => e.Id == expenseId, ct);
        if (expense is null) throw new BusinessRuleException("Expense not found.", 404);

        expense.Status = ExpenseStatus.Rejected;
        expense.ApprovedBy = approverId;
        expense.RejectionReason = reason.Trim();
        expense.UpdatedAt = NowUtc;
        expense.UpdatedBy = approverId;

        await db.SaveChangesAsync(ct);
        return await MapExpenseDtoAsync(expense, ct);
    }

    public async Task<IReadOnlyList<OfficeExpenseDto>> GetUnbilledClientExpensesAsync(Guid clientId, CancellationToken ct = default)
    {
        var unbilled = await db.OfficeExpenses.AsNoTracking()
            .Where(e => e.ClientId == clientId && e.IsReimbursableByClient && !e.IsClientBilled && e.Status == ExpenseStatus.Approved)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        return await MapExpenseDtosAsync(unbilled, ct);
    }

    public async Task<MonthlyExpenseReportDto> GetMonthlyExpenseReportAsync(int month, int year, CancellationToken ct = default)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var expenses = await db.OfficeExpenses.AsNoTracking()
            .Where(e => e.Date >= startDate && e.Date <= endDate && e.Status == ExpenseStatus.Approved)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        var dtos = await MapExpenseDtosAsync(expenses, ct);

        var total = expenses.Sum(e => e.Amount);
        var reimbursable = expenses.Where(e => e.IsReimbursableByClient).Sum(e => e.Amount);
        var nonReimbursable = total - reimbursable;

        var breakdown = expenses.GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return new MonthlyExpenseReportDto(
            Month: month,
            Year: year,
            TotalAmount: total,
            ReimbursableAmount: reimbursable,
            NonReimbursableAmount: nonReimbursable,
            CategoryBreakdown: breakdown,
            Expenses: dtos
        );
    }

    private async Task<OfficeExpenseDto> MapExpenseDtoAsync(OfficeExpense e, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == e.UserId, ct);
        var client = e.ClientId.HasValue ? await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == e.ClientId.Value, ct) : null;
        var approver = e.ApprovedBy.HasValue ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == e.ApprovedBy.Value, ct) : null;

        return new OfficeExpenseDto(
            e.Id,
            e.UserId,
            user?.Name ?? "Staff Member",
            e.ClientId,
            client?.Name,
            e.TaskId,
            e.Date,
            e.Category,
            e.Amount,
            e.Description,
            e.ReceiptDocumentId,
            e.IsReimbursableByClient,
            e.IsClientBilled,
            e.BilledInvoiceId,
            e.Status,
            e.ApprovedBy,
            approver?.Name,
            e.ApprovedAt,
            e.RejectionReason,
            e.CreatedAt
        );
    }

    private async Task<IReadOnlyList<OfficeExpenseDto>> MapExpenseDtosAsync(List<OfficeExpense> list, CancellationToken ct)
    {
        var userIds = list.Select(e => e.UserId).Concat(list.Where(e => e.ApprovedBy.HasValue).Select(e => e.ApprovedBy!.Value)).Distinct().ToList();
        var clientIds = list.Where(e => e.ClientId.HasValue).Select(e => e.ClientId!.Value).Distinct().ToList();

        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return list.Select(e => new OfficeExpenseDto(
            e.Id,
            e.UserId,
            users.GetValueOrDefault(e.UserId, "Staff Member"),
            e.ClientId,
            e.ClientId.HasValue ? clients.GetValueOrDefault(e.ClientId.Value) : null,
            e.TaskId,
            e.Date,
            e.Category,
            e.Amount,
            e.Description,
            e.ReceiptDocumentId,
            e.IsReimbursableByClient,
            e.IsClientBilled,
            e.BilledInvoiceId,
            e.Status,
            e.ApprovedBy,
            e.ApprovedBy.HasValue ? users.GetValueOrDefault(e.ApprovedBy.Value) : null,
            e.ApprovedAt,
            e.RejectionReason,
            e.CreatedAt
        )).ToList();
    }

    // =========================================================================
    // 4. Inward / Outward Register Operations
    // =========================================================================

    public async Task<RegisterEntryDto> CreateRegisterEntryAsync(Guid handledByUserId, CreateRegisterEntryRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        // Sequence number calculation: INW/YYYY/0001 or OUT/YYYY/0001
        var prefix = req.EntryType == RegisterEntryType.Outward ? "OUT" : "INW";
        var year = req.Date.Year;
        var count = await db.InwardOutwardRegisters.CountAsync(r => r.EntryType == req.EntryType && r.Date.Year == year, ct);
        var entryNumber = $"{prefix}/{year}/{(count + 1):D4}";

        var entry = new InwardOutwardRegister
        {
            FirmId = tenant.FirmId,
            EntryType = req.EntryType,
            EntryNumber = entryNumber,
            Date = req.Date,
            ClientId = req.ClientId,
            Description = req.Description.Trim(),
            DeliveryMode = req.DeliveryMode,
            CourierName = req.CourierName?.Trim(),
            TrackingNumber = req.TrackingNumber?.Trim(),
            SenderRecipientName = req.SenderRecipientName?.Trim(),
            HandledByUserId = handledByUserId,
            DscTokenRef = req.DscTokenRef?.Trim(),
            Status = req.EntryType == RegisterEntryType.Outward ? RegisterStatus.Dispatched : RegisterStatus.Received,
            AcknowledgementFileId = req.AcknowledgementFileId,
            CreatedAt = NowUtc,
            CreatedBy = handledByUserId
        };

        db.InwardOutwardRegisters.Add(entry);
        await db.SaveChangesAsync(ct);

        var handledUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == handledByUserId, ct);
        return new RegisterEntryDto(
            entry.Id,
            entry.EntryType,
            entry.EntryNumber,
            entry.Date,
            entry.ClientId,
            client.Name,
            entry.Description,
            entry.DeliveryMode,
            entry.CourierName,
            entry.TrackingNumber,
            entry.SenderRecipientName,
            entry.HandledByUserId,
            handledUser?.Name ?? "Staff Member",
            entry.DscTokenRef,
            entry.Status,
            entry.AcknowledgementFileId,
            entry.CreatedAt
        );
    }

    public async Task<IReadOnlyList<RegisterEntryDto>> ListRegisterEntriesAsync(RegisterListQuery q, CancellationToken ct = default)
    {
        var query = db.InwardOutwardRegisters.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.EntryType))
            query = query.Where(r => r.EntryType == q.EntryType);

        if (q.ClientId.HasValue)
            query = query.Where(r => r.ClientId == q.ClientId.Value);

        if (q.FromDate.HasValue)
            query = query.Where(r => r.Date >= q.FromDate.Value);

        if (q.ToDate.HasValue)
            query = query.Where(r => r.Date <= q.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(r => r.Description.ToLower().Contains(s) ||
                                     r.EntryNumber.ToLower().Contains(s) ||
                                     (r.TrackingNumber != null && r.TrackingNumber.ToLower().Contains(s)) ||
                                     (r.DscTokenRef != null && r.DscTokenRef.ToLower().Contains(s)));
        }

        var list = await query.OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.EntryNumber)
            .Skip(q.Skip)
            .Take(q.Take)
            .ToListAsync(ct);

        var clientIds = list.Select(r => r.ClientId).Distinct().ToList();
        var userIds = list.Select(r => r.HandledByUserId).Distinct().ToList();

        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return list.Select(r => new RegisterEntryDto(
            r.Id,
            r.EntryType,
            r.EntryNumber,
            r.Date,
            r.ClientId,
            clients.GetValueOrDefault(r.ClientId, "Client"),
            r.Description,
            r.DeliveryMode,
            r.CourierName,
            r.TrackingNumber,
            r.SenderRecipientName,
            r.HandledByUserId,
            users.GetValueOrDefault(r.HandledByUserId, "Staff Member"),
            r.DscTokenRef,
            r.Status,
            r.AcknowledgementFileId,
            r.CreatedAt
        )).ToList();
    }

    public async Task<string> GeneratePrintableRegisterHtmlAsync(string? entryType = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == tenant.FirmId, ct);
        var firmName = firm?.Name ?? "Chartered Accountants";

        var list = await ListRegisterEntriesAsync(new RegisterListQuery(
            EntryType: entryType,
            FromDate: from,
            ToDate: to,
            Take: 500
        ), ct);

        var title = entryType == RegisterEntryType.Outward ? "OUTWARD REGISTER" :
                    entryType == RegisterEntryType.Inward ? "INWARD REGISTER" : "INWARD / OUTWARD REGISTER";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine($"  <title>{title} - {firmName}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; padding: 24px; margin: 0; }");
        sb.AppendLine("    .header { text-align: center; border-bottom: 2px solid #0f172a; padding-bottom: 16px; margin-bottom: 24px; }");
        sb.AppendLine("    .firm-name { font-size: 24px; font-weight: 700; color: #0f172a; }");
        sb.AppendLine("    .doc-title { font-size: 18px; font-weight: 600; color: #475569; margin-top: 4px; letter-spacing: 1px; }");
        sb.AppendLine("    .period { font-size: 13px; color: #64748b; margin-top: 4px; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; margin-top: 16px; font-size: 12px; }");
        sb.AppendLine("    th { background: #f1f5f9; color: #334155; font-weight: 600; text-align: left; padding: 10px 8px; border: 1px solid #cbd5e1; }");
        sb.AppendLine("    td { padding: 8px; border: 1px solid #e2e8f0; vertical-align: top; }");
        sb.AppendLine("    tr:nth-child(even) { background: #f8fafc; }");
        sb.AppendLine("    .badge { display: inline-block; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; }");
        sb.AppendLine("    .badge-inward { background: #e0f2fe; color: #0369a1; }");
        sb.AppendLine("    .badge-outward { background: #fef3c7; color: #92400e; }");
        sb.AppendLine("    .footer { margin-top: 32px; font-size: 11px; color: #94a3b8; text-align: right; }");
        sb.AppendLine("    @media print { body { padding: 0; } }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"header\">");
        sb.AppendLine($"    <div class=\"firm-name\">{firmName}</div>");
        sb.AppendLine($"    <div class=\"doc-title\">{title}</div>");
        var periodText = (from.HasValue && to.HasValue) ? $"For the period {from:dd-MMM-yyyy} to {to:dd-MMM-yyyy}" : $"Generated on {Today:dd-MMM-yyyy}";
        sb.AppendLine($"    <div class=\"period\">{periodText}</div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th style=\"width: 80px;\">Date</th>");
        sb.AppendLine("        <th style=\"width: 110px;\">Entry No</th>");
        sb.AppendLine("        <th style=\"width: 70px;\">Type</th>");
        sb.AppendLine("        <th>Client</th>");
        sb.AppendLine("        <th>Description & Particulars</th>");
        sb.AppendLine("        <th style=\"width: 80px;\">Mode</th>");
        sb.AppendLine("        <th style=\"width: 120px;\">Courier / Tracking</th>");
        sb.AppendLine("        <th style=\"width: 100px;\">DSC Token Ref</th>");
        sb.AppendLine("        <th style=\"width: 100px;\">Handled By</th>");
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");

        if (list.Count == 0)
        {
            sb.AppendLine("      <tr><td colspan=\"9\" style=\"text-align: center; color: #94a3b8; padding: 24px;\">No records found for the selected criteria.</td></tr>");
        }
        else
        {
            foreach (var item in list)
            {
                var badgeClass = item.EntryType == RegisterEntryType.Inward ? "badge-inward" : "badge-outward";
                var courierInfo = string.IsNullOrWhiteSpace(item.CourierName) ? (item.TrackingNumber ?? "-") : $"{item.CourierName} ({item.TrackingNumber ?? "-"})";
                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td>{item.Date:dd-MM-yyyy}</td>");
                sb.AppendLine($"        <td><strong>{item.EntryNumber}</strong></td>");
                sb.AppendLine($"        <td><span class=\"badge {badgeClass}\">{item.EntryType}</span></td>");
                sb.AppendLine($"        <td>{item.ClientName}</td>");
                sb.AppendLine($"        <td>{item.Description}</td>");
                sb.AppendLine($"        <td>{item.DeliveryMode}</td>");
                sb.AppendLine($"        <td>{courierInfo}</td>");
                sb.AppendLine($"        <td>{item.DscTokenRef ?? "-"}</td>");
                sb.AppendLine($"        <td>{item.HandledByUserName}</td>");
                sb.AppendLine("      </tr>");
            }
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine($"  <div class=\"footer\">Report generated by CaDesk Office Essentials &bull; Total Entries: {list.Count}</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
