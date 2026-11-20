using CaDesk.Application.Abstractions;
using CaDesk.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Billing;

public class TimesheetService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<List<TimeEntryDto>> GetTimeEntriesAsync(
        Guid? userId = null,
        Guid? clientId = null,
        Guid? taskId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool? unbilledOnly = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var q = db.TimeEntries
            .Include(x => x.Client)
            .Include(x => x.Task)
            .Include(x => x.User)
            .AsNoTracking();

        if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (taskId.HasValue) q = q.Where(x => x.TaskId == taskId.Value);
        if (from.HasValue) q = q.Where(x => x.Date >= from.Value);
        if (to.HasValue) q = q.Where(x => x.Date <= to.Value);
        if (unbilledOnly == true) q = q.Where(x => x.Billable && !x.IsBilled);

        var list = await q
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<TimesheetSummaryDto> GetSummaryAsync(
        Guid? userId = null,
        Guid? clientId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var q = db.TimeEntries.AsNoTracking();
        if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (from.HasValue) q = q.Where(x => x.Date >= from.Value);
        if (to.HasValue) q = q.Where(x => x.Date <= to.Value);

        var entries = await q.ToListAsync(ct);

        var totalHours = entries.Sum(x => x.Hours);
        var billableHours = entries.Where(x => x.Billable).Sum(x => x.Hours);
        var nonBillableHours = totalHours - billableHours;
        var unbilledEntries = entries.Where(x => x.Billable && !x.IsBilled).ToList();
        var unbilledHours = unbilledEntries.Sum(x => x.Hours);
        var estimatedValue = unbilledEntries.Sum(x => x.Hours * x.HourlyRate);

        return new TimesheetSummaryDto(
            totalHours,
            billableHours,
            nonBillableHours,
            unbilledHours,
            estimatedValue
        );
    }

    public async Task<TimeEntryDto> CreateAsync(CreateTimeEntryRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        if (req.Hours <= 0 || req.Hours > 24)
            throw new RequestValidationException(nameof(req.Hours), "Hours must be greater than 0 and at most 24.");

        if (string.IsNullOrWhiteSpace(req.Description))
            throw new RequestValidationException(nameof(req.Description), "Description is required.");

        var clientExists = await db.Clients.AnyAsync(x => x.Id == req.ClientId, ct);
        if (!clientExists)
            throw new BusinessRuleException("Client not found.", 404);

        if (req.TaskId.HasValue)
        {
            var taskExists = await db.Tasks.AnyAsync(x => x.Id == req.TaskId.Value, ct);
            if (!taskExists)
                throw new BusinessRuleException("Task not found.", 404);
        }

        var entry = new TimeEntry
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            TaskId = req.TaskId,
            UserId = currentUserId,
            Date = req.Date,
            Hours = Math.Round(req.Hours, 2),
            HourlyRate = Math.Max(0, req.HourlyRate ?? 1500m),
            Description = req.Description.Trim(),
            Billable = req.Billable ?? true,
            IsBilled = false
        };

        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(entry.Id, ct);
    }

    public async Task<TimeEntryDto> UpdateAsync(Guid id, UpdateTimeEntryRequest req, Guid currentUserId, bool canManage, CancellationToken ct = default)
    {
        var entry = await db.TimeEntries
            .Include(x => x.Client)
            .Include(x => x.Task)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entry is null) throw new BusinessRuleException("Time entry not found.", 404);

        if (!canManage && entry.UserId != currentUserId)
            throw new BusinessRuleException("You can only edit your own timesheets.", 403);

        if (entry.IsBilled)
            throw new BusinessRuleException("Billed time entries cannot be edited.", 409);

        if (req.Hours <= 0 || req.Hours > 24)
            throw new RequestValidationException(nameof(req.Hours), "Hours must be between 0 and 24.");

        if (string.IsNullOrWhiteSpace(req.Description))
            throw new RequestValidationException(nameof(req.Description), "Description is required.");

        entry.Date = req.Date;
        entry.Hours = Math.Round(req.Hours, 2);
        if (req.HourlyRate.HasValue) entry.HourlyRate = Math.Max(0, req.HourlyRate.Value);
        entry.Description = req.Description.Trim();
        if (req.Billable.HasValue) entry.Billable = req.Billable.Value;

        await db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, bool canManage, CancellationToken ct = default)
    {
        var entry = await db.TimeEntries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null) return;

        if (!canManage && entry.UserId != currentUserId)
            throw new BusinessRuleException("You can only delete your own timesheets.", 403);

        if (entry.IsBilled)
            throw new BusinessRuleException("Billed time entries cannot be deleted.", 409);

        db.TimeEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    private async Task<TimeEntryDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.TimeEntries
            .Include(x => x.Client)
            .Include(x => x.Task)
            .Include(x => x.User)
            .AsNoTracking()
            .FirstAsync(x => x.Id == id, ct);

        return ToDto(entry);
    }

    private static TimeEntryDto ToDto(TimeEntry x) => new(
        x.Id,
        x.ClientId,
        x.Client?.Name,
        x.TaskId,
        x.Task?.Title,
        x.UserId,
        x.User?.Name,
        x.Date,
        x.Hours,
        x.HourlyRate,
        x.Description,
        x.Billable,
        x.IsBilled,
        x.InvoiceItemId,
        x.CreatedAt
    );
}
