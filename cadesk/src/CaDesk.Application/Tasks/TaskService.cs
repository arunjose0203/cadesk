using CaDesk.Application.Abstractions;
using CaDesk.Application.Compliance;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Tasks;
using CaDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Tasks;

public sealed record TaskQuery(string? Assignee = null, bool ToReview = false, Guid? ClientId = null, string? Status = null,
    DateOnly? DueTo = null, string? Search = null, int Skip = 0, int Take = 100);

public sealed record TaskListItem(Guid Id, string Title, Guid? ClientId, string? ClientCode, string? ClientName, Guid? DeadlineId,
    string Status, string Priority, DateOnly? DueDate, bool IsOverdue, Guid? AssignedUserId, string? AssignedUserName,
    Guid? ReviewerUserId, string? ReviewerName, bool ReviewRequired, int ChecklistDone, int ChecklistTotal, uint Version);

public sealed record TaskPage(IReadOnlyList<TaskListItem> Items, int Total);

public sealed record ChecklistItemDto(Guid Id, int Position, string Text, bool IsDone, string? DoneByName, DateTime? DoneAt);
public sealed record CommentDto(Guid Id, string Kind, string Body, Guid? CreatedBy, string? CreatedByName, DateTime CreatedAt);
public sealed record DeadlineRef(Guid Id, string TypeCode, string PeriodKey, string PeriodLabel, DateOnly DueDate, DateOnly StatutoryDue, string Status);

public sealed record TaskDetail(TaskListItem Task, string? Description, Guid? SubmittedBy, string? SubmittedByName,
    DateOnly? FiledOn, string? AckNo, DateTime? CompletedAt, string? CompletedByName, DeadlineRef? Deadline,
    IReadOnlyList<ChecklistItemDto> Checklist, IReadOnlyList<CommentDto> Comments);

public sealed record SaveTaskRequest(string Title, string? Description, Guid? ClientId, DateOnly? DueDate, string? Priority,
    Guid? AssignedUserId, Guid? ReviewerUserId, bool? ReviewRequired, List<string>? Checklist, uint? Version);

/// <summary>Finish (or submit for review). Deadline tasks record the filing date and acknowledgement.</summary>
public sealed record CompleteTaskRequest(DateOnly? FiledOn, string? AckNo, string? Comment, uint? Version);
public sealed record ReviewTaskRequest(bool Approve, string? Comment, uint? Version);

public sealed record WorkloadRow(Guid? UserId, string Name, int Open, int Overdue, int DueThisWeek, int WaitingReview, int ToReview, string? LeaveStatus = null);

public sealed record SaveTemplateRequest(string Name, List<string> Items);
public sealed record TemplateDto(Guid Id, string? TypeCode, string Name, IReadOnlyList<string> Items, bool FirmOwn);

/// <summary>
/// Tasks: one per deadline (made when the deadline comes within task.create_ahead_days) plus ad-hoc work.
/// Flow: open → in_progress → (in_review → done | changes_requested) or done when no review is needed.
/// A deadline is marked filed only when its task is done, so review can't be skipped.
/// </summary>
public sealed class TaskService(IAppDbContext db, ISettings settings, ITenantContext tenant, TimeProvider clock,
    CaDesk.Application.Notifications.NotificationService notify)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private Guid Me => tenant.UserId ?? throw new BusinessRuleException("Only a signed-in user can do this.", 403);

    // ------------------------------------------------------------ made from deadlines (called by DeadlineService)

    /// <summary>Creates tasks for open deadlines that are close, keeps their dates in step. Saves nothing itself.</summary>
    public async Task<int> SyncFromDeadlinesAsync(DateOnly today, CancellationToken ct)
    {
        var ahead = await settings.GetAsync(Keys.TaskCreateAheadDays, ct: ct);
        var rule = await settings.GetAsync(Keys.TaskAutoAssignRule, ct: ct);
        var horizon = today.AddDays(ahead);

        var due = await (from d in db.Deadlines
                         join c in db.Clients on d.ClientId equals c.Id
                         where (d.Status == DeadlineStatus.Pending || d.Status == DeadlineStatus.InProgress) && d.DueDate <= horizon
                         select new { d, c.AssignedUserId }).ToListAsync(ct);
        var ids = due.Select(x => x.d.Id).ToList();
        var existing = await db.Tasks.Where(t => t.DeadlineId != null && ids.Contains(t.DeadlineId.Value)).ToDictionaryAsync(t => t.DeadlineId!.Value, ct);

        // keep dates and the review rule in step (holiday / extension moved the deadline; firm turned review on)
        var reviewByClient = new Dictionary<Guid, bool>();
        async Task<bool> ReviewFor(Guid clientId)
        {
            if (!reviewByClient.TryGetValue(clientId, out var v))
                reviewByClient[clientId] = v = await settings.GetAsync(Keys.TaskReviewRequired, clientId, ct: ct);
            return v;
        }
        foreach (var x in due)
        {
            if (!existing.TryGetValue(x.d.Id, out var t) || !WorkTaskStatus.IsActive(t.Status)) continue;
            if (t.DueDate != x.d.InternalDue) t.DueDate = x.d.InternalDue;
            if (t.Status != WorkTaskStatus.InReview)
            {
                var rr = await ReviewFor(x.d.ClientId);
                if (t.ReviewRequired != rr) t.ReviewRequired = rr;
            }
        }

        var missing = due.Where(x => !existing.ContainsKey(x.d.Id)).ToList();
        if (missing.Count == 0) return 0;

        var types = await db.ComplianceTypes.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.Name, ct);
        var templates = await TemplatesByTypeAsync(ct);
        var load = rule == "round_robin" ? await OpenLoadAsync(ct) : null;

        var onLeaveUserIds = (await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Status == Domain.Office.LeaveStatus.Approved && r.StartDate <= today && r.EndDate >= today)
            .Select(r => r.UserId)
            .ToListAsync(ct)).ToHashSet();

        foreach (var x in missing)
        {
            var d = x.d;
            var reviewRequired = await ReviewFor(d.ClientId);
            Guid? candidate = rule switch
            {
                "unassigned" => null,
                "round_robin" => PickLeastLoaded(load!),
                _ => d.AssignedUserId ?? x.AssignedUserId,
            };
            Guid? assignee = candidate.HasValue && onLeaveUserIds.Contains(candidate.Value) ? null : candidate;
            var task = new WorkTask
            {
                ClientId = d.ClientId, DeadlineId = d.Id,
                Title = $"{types.GetValueOrDefault(d.TypeCode, d.TypeCode)} · {DeadlineRules.Label(d.PeriodKey)}",
                DueDate = d.InternalDue, AssignedUserId = assignee, ReviewRequired = reviewRequired,
                Status = d.Status == DeadlineStatus.InProgress ? WorkTaskStatus.InProgress : WorkTaskStatus.Open,
            };
            db.Tasks.Add(task);
            AddChecklist(task, templates.GetValueOrDefault(d.TypeCode) ?? []);
            History(task, $"Created from the deadline due {d.DueDate:dd MMM yyyy}.", system: true);
        }
        return missing.Count;
    }

    /// <summary>Makes the task for one deadline now (before the usual create-ahead window). Returns its id.</summary>
    public async Task<Guid?> EnsureTaskForDeadlineAsync(Guid deadlineId, CancellationToken ct)
    {
        var d = await db.Deadlines.FirstOrDefaultAsync(x => x.Id == deadlineId, ct);
        if (d is null) return null;
        if (await db.Tasks.Where(t => t.DeadlineId == deadlineId).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct) is { } have) return have;
        if (!DeadlineStatus.IsOpen(d.Status)) throw new BusinessRuleException("This deadline is closed.");
        var types = await db.ComplianceTypes.AsNoTracking().Where(t => t.Code == d.TypeCode).Select(t => t.Name).FirstOrDefaultAsync(ct);
        var templates = await TemplatesByTypeAsync(ct);
        var clientAssignee = await db.Clients.Where(c => c.Id == d.ClientId).Select(c => c.AssignedUserId).FirstOrDefaultAsync(ct);
        var task = new WorkTask
        {
            ClientId = d.ClientId, DeadlineId = d.Id, Title = $"{types ?? d.TypeCode} · {DeadlineRules.Label(d.PeriodKey)}",
            DueDate = d.InternalDue, AssignedUserId = d.AssignedUserId ?? clientAssignee,
            ReviewRequired = await settings.GetAsync(Keys.TaskReviewRequired, d.ClientId, ct: ct),
            Status = d.Status == DeadlineStatus.InProgress ? WorkTaskStatus.InProgress : WorkTaskStatus.Open,
        };
        db.Tasks.Add(task);
        AddChecklist(task, templates.GetValueOrDefault(d.TypeCode) ?? []);
        History(task, "Created from the deadline.");
        await db.SaveChangesAsync(ct);
        return task.Id;
    }

    /// <summary>A deadline may be removed by the generator only if its task hasn't been worked on.</summary>
    public async Task<HashSet<Guid>> DeadlinesWithWorkAsync(IReadOnlyCollection<Guid> deadlineIds, CancellationToken ct)
    {
        var ids = deadlineIds.ToList();
        var worked = await db.Tasks.Where(t => t.DeadlineId != null && ids.Contains(t.DeadlineId.Value) &&
                (t.Status != WorkTaskStatus.Open ||
                 db.TaskChecklistItems.Any(i => i.TaskId == t.Id && i.IsDone) ||
                 db.TaskComments.Any(c => c.TaskId == t.Id && c.Kind == TaskCommentKind.Comment)))
            .Select(t => t.DeadlineId!.Value).ToListAsync(ct);
        return worked.ToHashSet();
    }

    /// <summary>
    /// Called before a person changes a deadline's status directly. Keeps the task in step and stops a reviewed
    /// filing from being marked filed around its review.
    /// </summary>
    public async Task OnDeadlineStatusChangingAsync(Deadline d, string newStatus, CancellationToken ct)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.DeadlineId == d.Id, ct);
        if (t is null && newStatus == DeadlineStatus.Filed && newStatus != d.Status &&
            await settings.GetAsync(Keys.TaskReviewRequired, d.ClientId, ct: ct))
            throw new BusinessRuleException("Your firm reviews filings first. Create the task for this deadline, finish it and submit it for review.");
        if (t is null || newStatus == d.Status) return;

        if (newStatus == DeadlineStatus.Filed && t.ReviewRequired && t.Status != WorkTaskStatus.Done)
            throw new BusinessRuleException("This filing needs review. Finish its task and submit it for review instead.");

        switch (newStatus)
        {
            case DeadlineStatus.Filed when t.Status != WorkTaskStatus.Done:
                t.Status = WorkTaskStatus.Done; t.CompletedAt = Now; t.CompletedBy = tenant.UserId;
                History(t, "Marked filed on the deadline.");
                break;
            case DeadlineStatus.NotApplicable when WorkTaskStatus.IsActive(t.Status):
                t.Status = WorkTaskStatus.Cancelled;
                History(t, "Deadline marked not needed.");
                break;
            case DeadlineStatus.Pending or DeadlineStatus.InProgress when !WorkTaskStatus.IsActive(t.Status):
                t.Status = WorkTaskStatus.InProgress; t.CompletedAt = null; t.CompletedBy = null;
                History(t, "Deadline reopened.");
                break;
            case DeadlineStatus.InProgress when t.Status == WorkTaskStatus.Open:
                t.Status = WorkTaskStatus.InProgress;
                break;
        }
    }

    // ------------------------------------------------------------ reading

    public async Task<TaskPage> ListAsync(TaskQuery q, CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var rows = db.Tasks.AsNoTracking();
        rows = q.Assignee switch
        {
            null or "" or "all" => rows,
            "me" => rows.Where(t => t.AssignedUserId == tenant.UserId),
            "unassigned" => rows.Where(t => t.AssignedUserId == null),
            var s when Guid.TryParse(s, out var uid) => rows.Where(t => t.AssignedUserId == uid),
            _ => throw new RequestValidationException("assignee", "Use me, unassigned, all or a user id."),
        };
        if (q.ToReview)
        {
            var me = tenant.UserId;
            var sole = (await UsersWithPermissionAsync(Perms.TasksReview, ct)).All(u => u == me);
            rows = rows.Where(t => t.Status == WorkTaskStatus.InReview &&
                                   (t.ReviewerUserId == null || t.ReviewerUserId == me || t.ReviewerUserId == t.SubmittedBy) &&
                                   (sole || t.SubmittedBy != me));
        }
        if (q.ClientId is { } cid) rows = rows.Where(t => t.ClientId == cid);
        rows = q.Status switch
        {
            null or "" or "active" => rows.Where(t => t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled),
            "overdue" => rows.Where(t => t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled && t.DueDate < today),
            "all" => rows,
            _ => rows.Where(t => t.Status == q.Status),
        };
        if (q.DueTo is { } to) rows = rows.Where(t => t.DueDate <= to);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            rows = rows.Where(t => t.Title.ToLower().Contains(s) ||
                                   db.Clients.Any(c => c.Id == t.ClientId && (c.Name.ToLower().Contains(s) || c.Code.ToLower().Contains(s))));
        }

        var total = await rows.CountAsync(ct);
        var page = await rows.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority == TaskPriority.High).ThenBy(t => t.Title)
            .Skip(Math.Max(0, q.Skip)).Take(Math.Clamp(q.Take, 1, 500)).ToListAsync(ct);
        return new TaskPage(await ToItemsAsync(page, today, ct), total);
    }

    public async Task<TaskDetail?> GetAsync(Guid id, CancellationToken ct)
    {
        var t = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;
        var today = await TodayAsync(ct);
        var item = (await ToItemsAsync([t], today, ct))[0];

        var checklist = await db.TaskChecklistItems.AsNoTracking().Where(i => i.TaskId == id).OrderBy(i => i.Position).ToListAsync(ct);
        var comments = await db.TaskComments.AsNoTracking().Where(c => c.TaskId == id).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(ct);
        var people = checklist.Select(i => i.DoneBy).Concat(comments.Select(c => c.CreatedBy))
            .Append(t.SubmittedBy).Append(t.CompletedBy).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => people.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        string? N(Guid? g) => g is { } v ? names.GetValueOrDefault(v) : null;

        DeadlineRef? deadline = null;
        if (t.DeadlineId is { } did)
            deadline = await db.Deadlines.AsNoTracking().Where(d => d.Id == did)
                .Select(d => new DeadlineRef(d.Id, d.TypeCode, d.PeriodKey, d.PeriodKey, d.DueDate, d.StatutoryDue, d.Status))
                .FirstOrDefaultAsync(ct);
        if (deadline is not null) deadline = deadline with { PeriodLabel = DeadlineRules.Label(deadline.PeriodKey) };

        return new TaskDetail(item, t.Description, t.SubmittedBy, N(t.SubmittedBy), t.FiledOn, t.AckNo, t.CompletedAt, N(t.CompletedBy),
            deadline,
            checklist.Select(i => new ChecklistItemDto(i.Id, i.Position, i.Text, i.IsDone, N(i.DoneBy), i.DoneAt)).ToList(),
            comments.Select(c => new CommentDto(c.Id, c.Kind, c.Body, c.CreatedBy, c.CreatedBy is null ? "System" : N(c.CreatedBy), c.CreatedAt)).ToList());
    }

    /// <summary>Per person: open work, overdue, due in 7 days, waiting for review, and reviews they can do.</summary>
    public async Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        var week = today.AddDays(7);
        var active = db.Tasks.AsNoTracking().Where(t => t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled);
        var counts = await active.GroupBy(t => t.AssignedUserId).Select(g => new
        {
            UserId = g.Key,
            Open = g.Count(),
            Overdue = g.Count(t => t.DueDate < today),
            Week = g.Count(t => t.DueDate >= today && t.DueDate <= week),
            Waiting = g.Count(t => t.Status == WorkTaskStatus.InReview),
        }).ToListAsync(ct);
        var named = await active.Where(t => t.Status == WorkTaskStatus.InReview && t.ReviewerUserId != null)
            .GroupBy(t => t.ReviewerUserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var anyReviewer = await active.CountAsync(t => t.Status == WorkTaskStatus.InReview && t.ReviewerUserId == null, ct);
        var reviewers = (await UsersWithPermissionAsync(Perms.TasksReview, ct)).ToHashSet();

        var users = await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).Select(u => new { u.Id, u.Name }).ToListAsync(ct);
        var activeLeaves = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Status == Domain.Office.LeaveStatus.Approved && r.StartDate <= today && r.EndDate >= today)
            .ToDictionaryAsync(r => r.UserId, r => $"On Leave ({r.LeaveType})", ct);

        var rows = users.Select(u =>
        {
            var c = counts.FirstOrDefault(x => x.UserId == u.Id);
            return new WorkloadRow(u.Id, u.Name, c?.Open ?? 0, c?.Overdue ?? 0, c?.Week ?? 0, c?.Waiting ?? 0,
                named.GetValueOrDefault(u.Id) + (reviewers.Contains(u.Id) ? anyReviewer : 0),
                activeLeaves.GetValueOrDefault(u.Id));
        }).ToList();
        var none = counts.FirstOrDefault(x => x.UserId == null);
        if (none is not null) rows.Add(new WorkloadRow(null, "Unassigned", none.Open, none.Overdue, none.Week, none.Waiting, 0));
        return rows;
    }

    // ------------------------------------------------------------ changing

    public async Task<TaskDetail> CreateAsync(SaveTaskRequest r, CancellationToken ct)
    {
        await ValidateAsync(r, null, ct);
        var t = new WorkTask
        {
            ClientId = r.ClientId,
            ReviewRequired = r.ReviewRequired ?? await settings.GetAsync(Keys.TaskReviewRequired, r.ClientId, ct: ct),
        };
        Apply(t, r);
        db.Tasks.Add(t);
        AddChecklist(t, r.Checklist ?? []);
        History(t, "Task created.");
        await db.SaveChangesAsync(ct);
        return (await GetAsync(t.Id, ct))!;
    }

    public async Task<TaskDetail?> UpdateAsync(Guid id, SaveTaskRequest r, CancellationToken ct)
    {
        var t = await LoadAsync(id, r.Version, ct);
        if (t is null) return null;
        await ValidateAsync(r, t, ct);
        if (r.AssignedUserId != t.AssignedUserId) History(t, $"Assigned to {await NameAsync(r.AssignedUserId, ct) ?? "nobody"}.");
        Apply(t, r);
        // review can't be switched off to skip it: deadline tasks follow the firm setting; others need a reviewer to change it
        if (r.ReviewRequired is { } rr && rr != t.ReviewRequired && t.Status != WorkTaskStatus.InReview)
        {
            if (t.DeadlineId is not null)
                throw new RequestValidationException("reviewRequired", "Review for filings follows your firm's setting.");
            if (!rr && !(await UsersWithPermissionAsync(Perms.TasksReview, ct)).Contains(Me))
                throw new BusinessRuleException("Only a reviewer can turn review off.", 403);
            t.ReviewRequired = rr;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDetail?> StartAsync(Guid id, uint? version, CancellationToken ct)
    {
        var t = await LoadAsync(id, version, ct);
        if (t is null) return null;
        if (t.Status is not (WorkTaskStatus.Open or WorkTaskStatus.ChangesRequested))
            throw new BusinessRuleException("Only an open or sent-back task can be started.");
        t.Status = WorkTaskStatus.InProgress;
        t.AssignedUserId ??= Me;
        await SetDeadlineStatusAsync(t, DeadlineStatus.InProgress, ct);
        History(t, "Started.");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Done, or "submit for review" when review is required. Every checklist item must be ticked.</summary>
    public async Task<TaskDetail?> CompleteAsync(Guid id, CompleteTaskRequest r, CancellationToken ct)
    {
        var t = await LoadAsync(id, r.Version, ct);
        if (t is null) return null;
        if (t.Status is not (WorkTaskStatus.Open or WorkTaskStatus.InProgress or WorkTaskStatus.ChangesRequested))
            throw new BusinessRuleException("This task is not open for completion.");
        if (await db.TaskChecklistItems.AnyAsync(i => i.TaskId == id && !i.IsDone, ct))
            throw new BusinessRuleException("Tick every checklist item first (remove items that don't apply).");

        if (t.DeadlineId is not null)
        {
            var today = await TodayAsync(ct);
            var filedOn = r.FiledOn ?? today;
            var d = await db.Deadlines.AsNoTracking().FirstAsync(x => x.Id == t.DeadlineId, ct);
            if (filedOn > today || filedOn < d.PeriodStart)
                throw new RequestValidationException("filedOn", "Filed date can't be in the future or before the period starts.");
            if (r.AckNo is { Length: > 100 }) throw new RequestValidationException("ackNo", "Acknowledgement number is too long.");
            t.FiledOn = filedOn;
            t.AckNo = string.IsNullOrWhiteSpace(r.AckNo) ? null : r.AckNo.Trim();
        }
        if (!string.IsNullOrWhiteSpace(r.Comment)) Comment(t, r.Comment);

        t.SubmittedBy = Me;
        if (t.ReviewRequired)
        {
            t.Status = WorkTaskStatus.InReview;
            History(t, "Submitted for review.");
            if (t.ReviewerUserId is { } reviewer && reviewer != Me)
                await notify.TaskEventAsync(CaDesk.Domain.Notifications.NotificationKeys.ReviewRequested, t, reviewer, null, ct);
        }
        else await FinishAsync(t, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDetail?> ReviewAsync(Guid id, ReviewTaskRequest r, CancellationToken ct)
    {
        var t = await LoadAsync(id, r.Version, ct);
        if (t is null) return null;
        if (t.Status != WorkTaskStatus.InReview) throw new BusinessRuleException("This task is not waiting for review.");
        // a named reviewer reviews; if they submitted the work themselves, any other reviewer may
        if (t.ReviewerUserId is { } named && named != Me && named != t.SubmittedBy)
            throw new BusinessRuleException("Another person is named as the reviewer of this task.", 403);
        if (t.SubmittedBy == Me && (await UsersWithPermissionAsync(Perms.TasksReview, ct)).Any(u => u != Me))
            throw new BusinessRuleException("Someone else must review your own work.", 403);

        if (r.Approve)
        {
            History(t, "Approved." + (string.IsNullOrWhiteSpace(r.Comment) ? "" : $" {r.Comment.Trim()}"), TaskCommentKind.Review);
            await FinishAsync(t, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(r.Comment)) throw new RequestValidationException("comment", "Say what needs to change.");
            t.Status = WorkTaskStatus.ChangesRequested;
            History(t, $"Sent back: {r.Comment.Trim()}", TaskCommentKind.Review);
            var worker = t.SubmittedBy ?? t.AssignedUserId;
            if (worker is { } w && w != Me)
                await notify.TaskEventAsync(CaDesk.Domain.Notifications.NotificationKeys.TaskSentBack, t, w, r.Comment.Trim(), ct);
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDetail?> ReopenAsync(Guid id, uint? version, CancellationToken ct)
    {
        var t = await LoadAsync(id, version, ct);
        if (t is null) return null;
        if (WorkTaskStatus.IsActive(t.Status)) throw new BusinessRuleException("This task is already open.");
        t.Status = WorkTaskStatus.InProgress;
        t.CompletedAt = null; t.CompletedBy = null;
        await SetDeadlineStatusAsync(t, DeadlineStatus.InProgress, ct);
        History(t, "Reopened.");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Not needed: an ad-hoc task is cancelled; a deadline task also marks its deadline "not needed".</summary>
    public async Task<TaskDetail?> CancelAsync(Guid id, string? reason, uint? version, CancellationToken ct)
    {
        var t = await LoadAsync(id, version, ct);
        if (t is null) return null;
        if (!WorkTaskStatus.IsActive(t.Status)) throw new BusinessRuleException("This task is already closed.");
        t.Status = WorkTaskStatus.Cancelled;
        await SetDeadlineStatusAsync(t, DeadlineStatus.NotApplicable, ct);
        History(t, "Cancelled." + (string.IsNullOrWhiteSpace(reason) ? "" : $" {reason.Trim()}"));
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---- checklist and comments

    public async Task<TaskDetail?> SetItemAsync(Guid taskId, Guid itemId, bool done, CancellationToken ct)
    {
        var t = await LoadAsync(taskId, null, ct);
        if (t is null) return null;
        EnsureEditable(t);
        var i = await db.TaskChecklistItems.FirstOrDefaultAsync(x => x.Id == itemId && x.TaskId == taskId, ct);
        if (i is null) return null;
        i.IsDone = done;
        i.DoneBy = done ? Me : null;
        i.DoneAt = done ? Now : null;
        if (done && t.Status == WorkTaskStatus.Open)
        {
            t.Status = WorkTaskStatus.InProgress;
            await SetDeadlineStatusAsync(t, DeadlineStatus.InProgress, ct);
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(taskId, ct);
    }

    public async Task<TaskDetail?> AddItemAsync(Guid taskId, string text, CancellationToken ct)
    {
        var t = await LoadAsync(taskId, null, ct);
        if (t is null) return null;
        EnsureEditable(t);
        text = text?.Trim() ?? "";
        if (text.Length is 0 or > 300) throw new RequestValidationException("text", "Write the step (max 300 characters).");
        var count = await db.TaskChecklistItems.CountAsync(i => i.TaskId == taskId, ct);
        if (count >= 40) throw new BusinessRuleException("A checklist can have at most 40 steps.");
        var next = count == 0 ? 0 : await db.TaskChecklistItems.Where(i => i.TaskId == taskId).MaxAsync(i => i.Position, ct) + 1;
        db.TaskChecklistItems.Add(new TaskChecklistItem { TaskId = taskId, Position = next, Text = text });
        await db.SaveChangesAsync(ct);
        return await GetAsync(taskId, ct);
    }

    public async Task<TaskDetail?> RemoveItemAsync(Guid taskId, Guid itemId, CancellationToken ct)
    {
        var t = await LoadAsync(taskId, null, ct);
        if (t is null) return null;
        EnsureEditable(t);
        var i = await db.TaskChecklistItems.FirstOrDefaultAsync(x => x.Id == itemId && x.TaskId == taskId, ct);
        if (i is null) return null;
        db.TaskChecklistItems.Remove(i);
        History(t, $"Removed step: {i.Text}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(taskId, ct);
    }

    public async Task<TaskDetail?> AddCommentAsync(Guid taskId, string body, CancellationToken ct)
    {
        var t = await LoadAsync(taskId, null, ct);
        if (t is null) return null;
        body = body?.Trim() ?? "";
        if (body.Length is 0 or > 4000) throw new RequestValidationException("body", "Write a comment (max 4000 characters).");
        Comment(t, body);
        await db.SaveChangesAsync(ct);
        return await GetAsync(taskId, ct);
    }

    // ---- checklist templates

    public async Task<IReadOnlyList<TemplateDto>> TemplatesAsync(CancellationToken ct)
    {
        var all = await db.TaskTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        var firmTypes = all.Where(t => t.FirmId != null && t.TypeCode != null).Select(t => t.TypeCode).ToHashSet();
        return all.Where(t => t.FirmId != null || t.TypeCode == null || !firmTypes.Contains(t.TypeCode))
            .Select(t => new TemplateDto(t.Id, t.TypeCode, t.Name, t.Items, t.FirmId != null)).ToList();
    }

    /// <summary>Saves the firm's own checklist for a compliance type (replacing the platform default for the firm).</summary>
    public async Task<TemplateDto> SaveTemplateAsync(string typeCode, SaveTemplateRequest r, CancellationToken ct)
    {
        if (!await db.ComplianceTypes.AnyAsync(t => t.Code == typeCode, ct)) throw new BusinessRuleException("Unknown compliance type.", 404);
        var items = (r.Items ?? []).Select(i => i.Trim()).Where(i => i.Length > 0).ToList();
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 120) throw new RequestValidationException("name", "Name is required (max 120).");
        if (items.Count > 40 || items.Any(i => i.Length > 300)) throw new RequestValidationException("items", "Up to 40 steps, 300 characters each.");

        var row = await db.TaskTemplates.FirstOrDefaultAsync(t => t.FirmId != null && t.TypeCode == typeCode, ct);
        if (row is null) db.TaskTemplates.Add(row = new TaskTemplate { FirmId = tenant.FirmId, TypeCode = typeCode });
        row.Name = r.Name.Trim();
        row.Items = items;
        await db.SaveChangesAsync(ct);
        return new TemplateDto(row.Id, row.TypeCode, row.Name, row.Items, true);
    }

    /// <summary>Back to the platform's default checklist.</summary>
    public async Task<bool> ResetTemplateAsync(string typeCode, CancellationToken ct)
    {
        var row = await db.TaskTemplates.FirstOrDefaultAsync(t => t.FirmId != null && t.TypeCode == typeCode, ct);
        if (row is null) return false;
        db.TaskTemplates.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------------------------------------ helpers

    /// <summary>Changing a deadline through its task needs the same right as changing it directly.</summary>
    private async Task EnsureCanChangeDeadlineAsync(WorkTask t, CancellationToken ct)
    {
        if (t.DeadlineId is null) return;
        if (!(await UsersWithPermissionAsync(Perms.ComplianceManage, ct)).Contains(Me))
            throw new BusinessRuleException("You need permission to change deadlines (compliance.manage) to do this.", 403);
    }

    private async Task FinishAsync(WorkTask t, CancellationToken ct)
    {
        await EnsureCanChangeDeadlineAsync(t, ct);
        t.Status = WorkTaskStatus.Done;
        t.CompletedAt = Now;
        t.CompletedBy = Me;
        if (t.DeadlineId is { } did)
        {
            var d = await db.Deadlines.FirstAsync(x => x.Id == did, ct);
            d.Status = DeadlineStatus.Filed;
            d.FiledOn = t.FiledOn ?? await TodayAsync(ct);
            d.AckNo = t.AckNo;
            History(t, $"Deadline marked filed on {d.FiledOn:dd MMM yyyy}{(t.AckNo is null ? "" : $" ({t.AckNo})")}.");
        }
        else History(t, "Done.");
    }

    private async Task SetDeadlineStatusAsync(WorkTask t, string status, CancellationToken ct)
    {
        if (t.DeadlineId is not { } did) return;
        var d = await db.Deadlines.FirstAsync(x => x.Id == did, ct);
        if (d.Status == status) return;
        await EnsureCanChangeDeadlineAsync(t, ct);
        d.Status = status;
        if (status != DeadlineStatus.Filed) { d.FiledOn = null; d.AckNo = null; }
    }

    private async Task<WorkTask?> LoadAsync(Guid id, uint? version, CancellationToken ct)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is not null && version is { } v && v != t.Version) throw new DbUpdateConcurrencyException("Task was changed by someone else.");
        return t;
    }

    private static void EnsureEditable(WorkTask t)
    {
        if (!WorkTaskStatus.IsActive(t.Status) || t.Status == WorkTaskStatus.InReview)
            throw new BusinessRuleException("The checklist can't change while the task is in review or closed.");
    }

    private async Task ValidateAsync(SaveTaskRequest r, WorkTask? existing, CancellationToken ct)
    {
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 200) e["title"] = ["Title is required (max 200 characters)."];
        if (r.Description is { Length: > 4000 }) e["description"] = ["Description can be at most 4000 characters."];
        if (r.Priority is not null && !TaskPriority.All.Contains(r.Priority)) e["priority"] = ["Priority: low, normal or high."];
        if (existing is null && r.ClientId is { } cid && !await db.Clients.AnyAsync(c => c.Id == cid, ct)) e["clientId"] = ["Client not found."];
        if (existing is not null && existing.DeadlineId is not null && r.ClientId is { } c2 && c2 != existing.ClientId)
            e["clientId"] = ["A deadline's task stays with its client."];
        if (r.AssignedUserId is { } a && a != existing?.AssignedUserId && !await db.Users.AnyAsync(u => u.Id == a && u.IsActive, ct))
            e["assignedUserId"] = ["Assigned user not found or inactive."];
        if (r.ReviewerUserId is { } same && same == (r.AssignedUserId ?? existing?.AssignedUserId))
            e["reviewerUserId"] = ["The reviewer must be someone other than the person doing the work."];
        if (r.ReviewerUserId is { } rv && rv != existing?.ReviewerUserId &&
            !(await UsersWithPermissionAsync(Perms.TasksReview, ct)).Contains(rv))
            e["reviewerUserId"] = ["The reviewer must be an active user who can review work."];
        if (r.Checklist is { Count: > 40 } || r.Checklist?.Any(i => i.Length > 300) == true) e["checklist"] = ["Up to 40 steps, 300 characters each."];
        if (e.Count > 0) throw new RequestValidationException(e);
    }

    private static void Apply(WorkTask t, SaveTaskRequest r)
    {
        t.Title = r.Title.Trim();
        t.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        t.DueDate = r.DueDate;
        t.Priority = r.Priority ?? t.Priority;
        t.AssignedUserId = r.AssignedUserId;
        t.ReviewerUserId = r.ReviewerUserId;
    }

    private void AddChecklist(WorkTask t, IEnumerable<string> items)
    {
        var pos = 0;
        foreach (var text in items.Select(i => i.Trim()).Where(i => i.Length > 0).Take(40))
            db.TaskChecklistItems.Add(new TaskChecklistItem { TaskId = t.Id, Position = pos++, Text = text });
    }

    private void History(WorkTask t, string body, string kind = TaskCommentKind.Status, bool system = false) =>
        db.TaskComments.Add(new TaskComment { TaskId = t.Id, Kind = kind, Body = body, CreatedBy = system ? null : tenant.UserId, CreatedAt = Now });

    private void Comment(WorkTask t, string body) =>
        db.TaskComments.Add(new TaskComment { TaskId = t.Id, Kind = TaskCommentKind.Comment, Body = body.Trim(), CreatedBy = Me, CreatedAt = Now });

    private async Task<Dictionary<string, List<string>>> TemplatesByTypeAsync(CancellationToken ct)
    {
        var rows = await db.TaskTemplates.AsNoTracking().Where(t => t.TypeCode != null).ToListAsync(ct);
        // the firm's own checklist wins over the platform default
        return rows.GroupBy(t => t.TypeCode!).ToDictionary(g => g.Key, g => (g.FirstOrDefault(t => t.FirmId != null) ?? g.First()).Items);
    }

    private async Task<Dictionary<Guid, int>> OpenLoadAsync(CancellationToken ct)
    {
        var workers = await UsersWithPermissionAsync(Perms.TasksManage, ct);
        var open = await db.Tasks.Where(t => t.AssignedUserId != null && t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled)
            .GroupBy(t => t.AssignedUserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return workers.ToDictionary(u => u, u => open.GetValueOrDefault(u));
    }

    private static Guid? PickLeastLoaded(Dictionary<Guid, int> load)
    {
        if (load.Count == 0) return null;
        var pick = load.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        load[pick]++;
        return pick;
    }

    private async Task<List<Guid>> UsersWithPermissionAsync(string permission, CancellationToken ct) =>
        await (from u in db.Users
               join ur in db.UserRoles on u.Id equals ur.UserId
               join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
               where u.IsActive && rp.PermissionCode == permission
               select u.Id).Distinct().ToListAsync(ct);

    private async Task<string?> NameAsync(Guid? id, CancellationToken ct) =>
        id is { } v ? await db.Users.Where(u => u.Id == v).Select(u => u.Name).FirstOrDefaultAsync(ct) : null;

    private async Task<List<TaskListItem>> ToItemsAsync(IReadOnlyList<WorkTask> page, DateOnly today, CancellationToken ct)
    {
        var ids = page.Select(t => t.Id).ToList();
        var progress = await db.TaskChecklistItems.AsNoTracking().Where(i => ids.Contains(i.TaskId))
            .GroupBy(i => i.TaskId).Select(g => new { g.Key, Done = g.Count(i => i.IsDone), Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, ct);
        var clientIds = page.Where(t => t.ClientId != null).Select(t => t.ClientId!.Value).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Code, c.Name }, ct);
        var userIds = page.SelectMany(t => new[] { t.AssignedUserId, t.ReviewerUserId }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return page.Select(t =>
        {
            progress.TryGetValue(t.Id, out var p);
            var c = t.ClientId is { } cid ? clients.GetValueOrDefault(cid) : null;
            return new TaskListItem(t.Id, t.Title, t.ClientId, c?.Code, c?.Name, t.DeadlineId, t.Status, t.Priority, t.DueDate,
                WorkTaskStatus.IsActive(t.Status) && t.DueDate < today,
                t.AssignedUserId, t.AssignedUserId is { } a ? users.GetValueOrDefault(a) : null,
                t.ReviewerUserId, t.ReviewerUserId is { } r ? users.GetValueOrDefault(r) : null,
                t.ReviewRequired, p?.Done ?? 0, p?.Total ?? 0, t.Version);
        }).ToList();
    }

    private async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        var tzName = await settings.GetAsync(Keys.TimeZone, ct: ct);
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(tzName, out var found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).DateTime);
    }
}
