using CaDesk.Domain.Common;

namespace CaDesk.Domain.Tasks;

/// <summary>A piece of work: made from a deadline (one task per deadline) or added by hand. Named WorkTask to avoid System.Threading.Tasks.Task.</summary>
public class WorkTask : TenantEntity
{
    public Guid? ClientId { get; set; }
    public Guid? DeadlineId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = WorkTaskStatus.Open;
    public string Priority { get; set; } = TaskPriority.Normal;
    public DateOnly? DueDate { get; set; }
    public Guid? AssignedUserId { get; set; }
    /// <summary>A named reviewer; null = anyone with tasks.review.</summary>
    public Guid? ReviewerUserId { get; set; }
    public bool ReviewRequired { get; set; }
    public Guid? SubmittedBy { get; set; }
    /// <summary>Deadline tasks: filing date and acknowledgement recorded on the deadline when the work is approved.</summary>
    public DateOnly? FiledOn { get; set; }
    public string? AckNo { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedBy { get; set; }
}

public class TaskChecklistItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid TaskId { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = "";
    public bool IsDone { get; set; }
    public Guid? DoneBy { get; set; }
    public DateTime? DoneAt { get; set; }
}

/// <summary>A comment, or a history line written by the system (kind status / review).</summary>
public class TaskComment : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid TaskId { get; set; }
    public string Kind { get; set; } = TaskCommentKind.Comment;
    public string Body { get; set; } = "";
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Checklist for a compliance type: platform default (FirmId null) or the firm's own version.</summary>
public class TaskTemplate : Entity
{
    public Guid? FirmId { get; set; }
    public string? TypeCode { get; set; }
    public string Name { get; set; } = "";
    public List<string> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class WorkTaskStatus
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string InReview = "in_review";
    public const string ChangesRequested = "changes_requested";
    public const string Done = "done";
    public const string Cancelled = "cancelled";
    public static bool IsActive(string s) => s is not (Done or Cancelled);
}

public static class TaskPriority
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public static readonly string[] All = [Low, Normal, High];
}

public static class TaskCommentKind
{
    public const string Comment = "comment";
    public const string Status = "status";
    public const string Review = "review";
}
