using CaDesk.Domain.Common;

namespace CaDesk.Domain.Documents;

/// <summary>
/// One file in a client's vault. The bytes are in the file store under <see cref="StorageKey"/>;
/// the row stays as a record after the file is removed (trash emptied, retention, virus found).
/// </summary>
public class Document : TenantEntity
{
    public Guid ClientId { get; set; }
    /// <summary>A general folder (bank, notices ...) — or null when the document is in a filing's folder.</summary>
    public string? Folder { get; set; }
    /// <summary>The filing's folder (compliance type code, e.g. GSTR3B_M).</summary>
    public string? TypeCode { get; set; }
    /// <summary>Financial year label (2026-27); null only in the permanent folder.</summary>
    public string? Fy { get; set; }
    public string? PeriodKey { get; set; }
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Extension { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string? StorageKey { get; set; }
    public string ScanStatus { get; set; } = DocumentScanStatus.Pending;
    public string? ScanDetail { get; set; }
    public DateTime? ScannedAt { get; set; }
    public int ScanAttempts { get; set; }
    public string Source { get; set; } = DocumentSource.Staff;
    public Guid? UploadLinkId { get; set; }
    public string? Notes { get; set; }
    /// <summary>Retention counts from this date (end of the year or period); null = permanent folder.</summary>
    public DateOnly? RetentionFrom { get; set; }
    public bool LegalHold { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime? PurgedAt { get; set; }
    public string? PurgeReason { get; set; }

    /// <summary>The file can be handed out: present, not in the trash, and not found (or waiting) to be unsafe.</summary>
    public bool CanDownload => StorageKey is not null && PurgedAt is null && DeletedAt is null &&
                               ScanStatus is DocumentScanStatus.Clean or DocumentScanStatus.NotScanned;
}

/// <summary>A general folder (platform data). Filing folders come from compliance types.</summary>
public class DocumentFolder
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool PerYear { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A link a client uses to send files without an account. Only the token's hash is stored.</summary>
public class DocumentUploadLink : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string TokenHash { get; set; } = "";
    public string? Message { get; set; }
    public string? Folder { get; set; }
    public string? TypeCode { get; set; }
    public string? Fy { get; set; }
    public string? PeriodKey { get; set; }
    public Guid? TaskId { get; set; }
    public int MaxFiles { get; set; } = 20;
    public int UploadedCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A document a task uses.</summary>
public class TaskDocument : ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid TaskId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid? LinkedBy { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

public static class DocumentScanStatus
{
    /// <summary>Not scanned yet (scanner busy or unreachable): the file can't be downloaded until it is.</summary>
    public const string Pending = "pending";
    public const string Clean = "clean";
    /// <summary>A virus was found: the file was removed.</summary>
    public const string Infected = "infected";
    /// <summary>Scanning is switched off on this server.</summary>
    public const string NotScanned = "not_scanned";
}

public static class DocumentSource
{
    public const string Staff = "staff";
    public const string Portal = "portal";
}

public static class PurgeReasons
{
    public const string Trash = "trash";
    public const string Retention = "retention";
    public const string Infected = "infected";
}
