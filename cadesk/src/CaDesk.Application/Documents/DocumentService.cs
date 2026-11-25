using System.Security.Cryptography;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Documents;
using CaDesk.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Documents;

public sealed record DocumentDto(Guid Id, Guid ClientId, string? Folder, string? TypeCode, string FolderName, string? Fy, string? PeriodKey,
    string Name, string FileName, string Extension, long SizeBytes, string ScanStatus, string? ScanDetail, string Source,
    string? UploadedByName, DateTime UploadedAt, string? Notes, bool LegalHold, DateTime? DeletedAt, DateTime? PurgedAt,
    string? PurgeReason, bool CanDownload, int TaskCount, uint Version);

public sealed record LinkedTask(Guid Id, string Title, string Status);
public sealed record DocumentDetail(DocumentDto Document, IReadOnlyList<LinkedTask> Tasks);

public sealed record DocumentQuery(string? Fy = null, string? Folder = null, string? TypeCode = null, string? Search = null,
    bool Trash = false, int Skip = 0, int Take = 100);
public sealed record DocumentPage(IReadOnlyList<DocumentDto> Items, int Total);

public sealed record FolderNode(string? Folder, string? TypeCode, string Name, int Count);
/// <summary>A year in the client's vault (Fy null = the permanent folder).</summary>
public sealed record YearNode(string? Fy, string Label, int Count, IReadOnlyList<FolderNode> Folders);
public sealed record FolderChoice(string? Folder, string? TypeCode, string Name, bool PerYear);
public sealed record VaultInfo(IReadOnlyList<YearNode> Years, int TrashCount, IReadOnlyList<FolderChoice> Folders,
    IReadOnlyList<string> YearChoices, string CurrentFy, IReadOnlyList<string> AllowedTypes, int MaxSizeMb,
    bool ScanEnabled, bool ClientUploadEnabled);

/// <summary>Where a file goes. Neither folder nor filing given = "Other"; a task's deadline fills in the filing and year.</summary>
public sealed record UploadRequest(string? Folder = null, string? TypeCode = null, string? Fy = null, string? Name = null,
    string? Notes = null, Guid? TaskId = null);
public sealed record UpdateDocumentRequest(string? Name, string? Folder, string? TypeCode, string? Fy, string? Notes, uint? Version);

public sealed record DownloadInfo(Stream Content, string FileName, string ContentType);

public sealed record RetentionItem(Guid Id, Guid ClientId, string ClientCode, string ClientName, string Name, string FolderName,
    string? Fy, DateOnly KeepUntil, long SizeBytes, bool ClientDeleted);
public sealed record RetentionPage(int RetentionYears, IReadOnlyList<RetentionItem> Items, int Total);
public sealed record MaintenanceResult(int Scanned, int Infected, int TrashPurged);

/// <summary>
/// The document vault: per-client folders by year and filing. Every file is checked (type from settings and from its content,
/// size from settings), scanned for viruses, stored outside the database and kept until retention ends.
/// Downloads are recorded in the audit log. Deleting moves a file to the trash; the trash is emptied after doc.trash_days.
/// Files past retention are listed for a person to remove — never removed automatically.
/// </summary>
public sealed class DocumentService(IAppDbContext db, ISettings settings, ITenantContext tenant, TimeProvider clock,
    IFileStore store, IVirusScanner scanner, IAuditRecorder audit)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------ reading

    public async Task<VaultInfo?> VaultAsync(Guid clientId, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => new { c.CountryCode }).FirstOrDefaultAsync(ct);
        if (client is null) return null;
        var names = await FolderNamesAsync(ct);
        var groups = await Visible(db.Documents.AsNoTracking().Where(d => d.ClientId == clientId))
            .GroupBy(d => new { d.Fy, d.Folder, d.TypeCode })
            .Select(g => new { g.Key.Fy, g.Key.Folder, g.Key.TypeCode, Count = g.Count() })
            .ToListAsync(ct);
        var trash = await db.Documents.CountAsync(d => d.ClientId == clientId && d.DeletedAt != null && d.PurgedAt == null, ct);

        var years = groups.GroupBy(g => g.Fy)
            .OrderBy(g => g.Key != null).ThenByDescending(g => g.Key, StringComparer.Ordinal)
            .Select(y => new YearNode(y.Key, y.Key is null ? "Permanent" : $"FY {y.Key}", y.Sum(x => x.Count),
                y.Select(x => new FolderNode(x.Folder, x.TypeCode, names.Name(x.Folder, x.TypeCode), x.Count))
                    .OrderBy(f => f.TypeCode is not null).ThenBy(f => names.Order(f.Folder, f.TypeCode)).ToList()))
            .ToList();

        var choices = names.Folders.Select(f => new FolderChoice(f.Code, null, f.Name, f.PerYear)).ToList();
        var used = groups.Where(g => g.TypeCode != null).Select(g => g.TypeCode!).Distinct().ToList();
        choices.AddRange(await db.ComplianceTypes.AsNoTracking()
            .Where(t => (t.CountryCode == client.CountryCode && t.IsActive) || used.Contains(t.Code))
            .OrderBy(t => t.SortOrder)
            .Select(t => new FolderChoice(null, t.Code, t.Name, true)).ToListAsync(ct));

        var fyStart = await settings.GetAsync(Keys.FiscalYearStartMonth, ct: ct);
        var today = await TodayAsync(ct);
        return new VaultInfo(years, trash, choices, DocumentRules.RecentYears(today, fyStart), DocumentRules.FyOf(today, fyStart),
            await settings.GetAsync(Keys.DocAllowedTypes, clientId, ct: ct), await settings.GetAsync(Keys.DocMaxSizeMb, clientId, ct: ct),
            scanner.Enabled, await settings.GetAsync(Keys.DocClientUploadEnabled, clientId, ct: ct));
    }

    public async Task<DocumentPage?> ListAsync(Guid clientId, DocumentQuery q, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct)) return null;
        var rows = db.Documents.AsNoTracking().Where(d => d.ClientId == clientId);
        if (q.Trash) rows = rows.Where(d => d.DeletedAt != null && d.PurgedAt == null);
        else
        {
            rows = Visible(rows);
            if (q.Fy == "permanent") rows = rows.Where(d => d.Fy == null);
            else if (!string.IsNullOrWhiteSpace(q.Fy)) rows = rows.Where(d => d.Fy == q.Fy);
            if (!string.IsNullOrWhiteSpace(q.Folder)) rows = rows.Where(d => d.Folder == q.Folder);
            if (!string.IsNullOrWhiteSpace(q.TypeCode)) rows = rows.Where(d => d.TypeCode == q.TypeCode);
        }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            rows = rows.Where(d => d.Name.ToLower().Contains(s) || d.FileName.ToLower().Contains(s));
        }
        var total = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id)
            .Skip(Math.Max(0, q.Skip)).Take(Math.Clamp(q.Take, 1, 500)).ToListAsync(ct);
        return new DocumentPage(await ToDtosAsync(page, ct), total);
    }

    public async Task<DocumentDetail?> GetAsync(Guid id, CancellationToken ct)
    {
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        var tasks = await (from l in db.TaskDocuments
                           join t in db.Tasks on l.TaskId equals t.Id
                           where l.DocumentId == id
                           orderby l.LinkedAt
                           select new LinkedTask(t.Id, t.Title, t.Status)).ToListAsync(ct);
        return new DocumentDetail((await ToDtosAsync([d], ct))[0], tasks);
    }

    /// <summary>Documents a task uses, for the task screen.</summary>
    public async Task<IReadOnlyList<DocumentDto>> ForTaskAsync(Guid taskId, CancellationToken ct)
    {
        var docs = await (from l in db.TaskDocuments
                          join d in db.Documents on l.DocumentId equals d.Id
                          where l.TaskId == taskId
                          orderby l.LinkedAt
                          select d).AsNoTracking().ToListAsync(ct);
        return await ToDtosAsync(docs, ct);
    }

    /// <summary>The file, for a download. Recorded in the audit log.</summary>
    public async Task<DownloadInfo?> OpenAsync(Guid id, CancellationToken ct)
    {
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        if (d.PurgedAt is not null || d.StorageKey is null)
            throw new BusinessRuleException(d.ScanStatus == DocumentScanStatus.Infected
                ? "A virus was found in this file, so it was removed." : "This file has been removed.", 410);
        if (d.DeletedAt is not null) throw new BusinessRuleException("This file is in the trash. Restore it first.");
        if (d.ScanStatus == DocumentScanStatus.Pending)
            throw new BusinessRuleException("This file hasn't been checked for viruses yet. Try again in a few minutes.");
        if (!d.CanDownload) throw new BusinessRuleException("This file can't be downloaded.");

        var stream = await store.OpenAsync(d.StorageKey, ct)
            ?? throw new BusinessRuleException("The file is missing from storage. Tell your administrator.", 410);
        await audit.RecordAsync("documents", d.Id.ToString(), AuditAction.Download, null, new { d.Name, d.FileName, d.ClientId }, ct);
        return new DownloadInfo(stream, d.FileName, FileTypes.ContentTypeOf(d.Extension));
    }

    // ------------------------------------------------------------ uploading

    /// <summary>Staff upload (or, through <see cref="PortalService"/>, a client's upload with a link).</summary>
    public async Task<DocumentDto?> UploadAsync(Guid clientId, UploadRequest r, UploadFile file, CancellationToken ct) =>
        await StoreAsync(clientId, r, file, DocumentSource.Staff, null, ct);

    /// <summary>
    /// A file CaDesk produced itself, such as a GSTR-1 JSON or a GSTR-3B/GSTR-9 working (Sprint 66).
    /// Only .json and .csv are accepted here, whatever doc.allowed_types says, because the content is
    /// generated by the app rather than chosen by a user; it is still content-checked, size-limited,
    /// virus-scanned and de-duplicated like any upload.
    /// </summary>
    public async Task<DocumentDto?> SaveGeneratedAsync(Guid clientId, UploadRequest r, UploadFile file, CancellationToken ct) =>
        await StoreAsync(clientId, r, file, DocumentSource.Staff, null, ct, generated: true);

    internal async Task<DocumentDto?> StoreAsync(Guid clientId, UploadRequest r, UploadFile file, string source, Guid? linkId, CancellationToken ct,
        bool generated = false)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct)) return null;
        if (r.Notes is { Length: > 1000 }) throw new RequestValidationException("notes", "Notes can be at most 1000 characters.");
        var safeName = DocumentRules.SafeFileName(file.FileName);
        var ext = await CheckFileAsync(clientId, safeName, file, ct, generated);

        // where it goes: a task's deadline fills in the filing, period and year
        WorkTask? task = null;
        Deadline? deadline = null;
        if (r.TaskId is { } taskId)
        {
            task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
                ?? throw new RequestValidationException("taskId", "Task not found.");
            if (task.ClientId is { } tc && tc != clientId) throw new RequestValidationException("taskId", "This task is for another client.");
            if (!WorkTaskStatus.IsActive(task.Status)) throw new BusinessRuleException("This task is closed; reopen it to add documents.");
            if (task.DeadlineId is { } did) deadline = await db.Deadlines.AsNoTracking().FirstOrDefaultAsync(x => x.Id == did, ct);
        }
        var place = await PlaceAsync(r.Folder, r.TypeCode, r.Fy, deadline, ct);

        string hash;
        await using (var s = file.Open()) hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(s, ct));
        var same = await db.Documents.AsNoTracking()
            .Where(d => d.ClientId == clientId && d.Sha256 == hash && d.DeletedAt == null && d.PurgedAt == null)
            .Select(d => new { d.Name, d.Fy }).FirstOrDefaultAsync(ct);
        if (same is not null)
            throw new BusinessRuleException($"This file is already in the vault as \"{same.Name}\"{(same.Fy is null ? "" : $" (FY {same.Fy})")}.");

        // scan before storing; if the scanner can't answer now, store it as "pending" and the job scans it later
        var status = DocumentScanStatus.NotScanned;
        string? detail = null;
        DateTime? scannedAt = null;
        if (scanner.Enabled)
        {
            ScanResult result;
            await using (var s = file.Open()) result = await scanner.ScanAsync(s, ct);
            switch (result.Outcome)
            {
                case ScanResult.Infected:
                    throw new BusinessRuleException($"A virus was found in this file ({result.Detail}). It was not saved.", 422);
                case ScanResult.Clean:
                    status = DocumentScanStatus.Clean; scannedAt = Now;
                    break;
                default:
                    status = DocumentScanStatus.Pending; detail = Trim(result.Detail, 300);
                    break;
            }
        }

        var doc = new Document
        {
            ClientId = clientId, Folder = place.Folder, TypeCode = place.TypeCode, Fy = place.Fy,
            PeriodKey = deadline?.PeriodKey,
            Name = DocumentRules.DisplayName(r.Name, safeName), FileName = safeName, Extension = ext,
            ContentType = FileTypes.ContentTypeOf(ext), SizeBytes = file.Length, Sha256 = hash,
            ScanStatus = status, ScanDetail = detail, ScannedAt = scannedAt, ScanAttempts = scanner.Enabled ? 1 : 0,
            Source = source, UploadLinkId = linkId, Notes = Trim(r.Notes, 1000), RetentionFrom = place.RetentionFrom,
        };
        doc.StorageKey = DocumentRules.StorageKey(tenant.FirmId, doc.Id, Now);

        await using (var s = file.Open()) await store.SaveAsync(doc.StorageKey, s, ct);
        try
        {
            db.Documents.Add(doc);
            if (task is not null)
            {
                db.TaskDocuments.Add(new TaskDocument { TaskId = task.Id, DocumentId = doc.Id, LinkedBy = tenant.UserId, LinkedAt = Now });
                TaskHistory(task.Id, source == DocumentSource.Portal ? $"Client uploaded: {doc.Name}" : $"Document added: {doc.Name}");
            }
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await store.DeleteAsync(doc.StorageKey, CancellationToken.None);   // no orphan files
            throw;
        }
        return (await ToDtosAsync([doc], ct))[0];
    }

    /// <summary>Type allowed by settings, content matching the extension, size within the limit. Returns the extension.</summary>
    private async Task<string> CheckFileAsync(Guid clientId, string safeName, UploadFile file, CancellationToken ct, bool generated = false)
    {
        var allowed = generated
            ? new[] { "json", "csv", "pdf" }
            : await settings.GetAsync(Keys.DocAllowedTypes, clientId, ct: ct);
        var maxMb = await settings.GetAsync(Keys.DocMaxSizeMb, clientId, ct: ct);
        var ext = FileTypes.ExtensionOf(safeName);
        var code = FileTypes.CodeOf(ext);
        if (code is null || !allowed.Contains(code))
            throw new RequestValidationException("file", $"Only these file types can be uploaded: {string.Join(", ", allowed)}.");
        if (file.Length <= 0) throw new RequestValidationException("file", "The file is empty.");
        if (file.Length > maxMb * 1024L * 1024L)
            throw new RequestValidationException("file", $"The file is larger than {maxMb} MB.");
        await using var s = file.Open();
        if (FileTypes.CheckContent(ext, s) is { } problem) throw new RequestValidationException("file", problem);
        return ext;
    }

    private sealed record Place(string? Folder, string? TypeCode, string? Fy, DateOnly? RetentionFrom);

    /// <summary>Checks the folder or filing and the year; works out when retention starts.</summary>
    private async Task<Place> PlaceAsync(string? folder, string? typeCode, string? fy, Deadline? deadline, CancellationToken ct)
    {
        folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();
        typeCode = string.IsNullOrWhiteSpace(typeCode) ? null : typeCode.Trim();
        fy = string.IsNullOrWhiteSpace(fy) ? null : fy.Trim();
        if (folder is not null && typeCode is not null)
            throw new RequestValidationException("folder", "Choose a folder or a filing, not both.");
        if (folder is null && typeCode is null)
        {
            if (deadline is not null) typeCode = deadline.TypeCode;
            else folder = "other";
        }

        var perYear = true;
        if (folder is not null)
        {
            var f = await db.DocumentFolders.AsNoTracking().FirstOrDefaultAsync(x => x.Code == folder, ct)
                ?? throw new RequestValidationException("folder", "Unknown folder.");
            perYear = f.PerYear;
        }
        else if (!await db.ComplianceTypes.AnyAsync(t => t.Code == typeCode, ct))
            throw new RequestValidationException("typeCode", "Unknown filing.");

        if (!perYear) return new Place(folder, null, null, null);

        var fyStart = await settings.GetAsync(Keys.FiscalYearStartMonth, ct: ct);
        fy ??= DocumentRules.FyOf(deadline?.PeriodEnd ?? await TodayAsync(ct), fyStart);
        if (!DocumentRules.IsValidFy(fy, fyStart))
            throw new RequestValidationException("fy", fyStart == 1 ? "Year must look like 2026." : "Year must look like 2026-27.");
        return new Place(folder, typeCode, fy, DocumentRules.FyEnd(fy, fyStart));
    }

    /// <summary>Refuses an unknown folder or filing, or a year in the wrong style (used before making an upload link).</summary>
    internal async Task CheckPlaceAsync(string? folder, string? typeCode, string? fy, CancellationToken ct) =>
        await PlaceAsync(folder, typeCode, fy, null, ct);

    // ------------------------------------------------------------ changing

    /// <summary>Rename, move to another folder or year, change notes.</summary>
    public async Task<DocumentDto?> UpdateAsync(Guid id, UpdateDocumentRequest r, CancellationToken ct)
    {
        var d = await LoadAsync(id, r.Version, ct);
        if (d is null) return null;
        if (d.PurgedAt is not null) throw new BusinessRuleException("This file has been removed.", 410);
        if (r.Name is not null)
        {
            var name = r.Name.Trim();
            if (name.Length is 0 or > DocumentRules.MaxNameLength) throw new RequestValidationException("name", "Name is required (max 200 characters).");
            d.Name = name;
        }
        if (r.Notes is not null)
        {
            if (r.Notes.Length > 1000) throw new RequestValidationException("notes", "Notes can be at most 1000 characters.");
            d.Notes = Trim(r.Notes, 1000);
        }
        if (r.Folder is not null || r.TypeCode is not null || r.Fy is not null)
        {
            var place = await PlaceAsync(r.Folder ?? (r.TypeCode is null ? d.Folder : null), r.TypeCode ?? (r.Folder is null ? d.TypeCode : null),
                r.Fy ?? d.Fy, null, ct);
            d.Folder = place.Folder; d.TypeCode = place.TypeCode; d.Fy = place.Fy; d.RetentionFrom = place.RetentionFrom;
        }
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([d], ct))[0];
    }

    /// <summary>A document on legal hold is never deleted or removed for retention.</summary>
    public async Task<DocumentDto?> SetHoldAsync(Guid id, bool hold, CancellationToken ct)
    {
        var d = await LoadAsync(id, null, ct);
        if (d is null) return null;
        if (d.PurgedAt is not null) throw new BusinessRuleException("This file has been removed.", 410);
        d.LegalHold = hold;
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([d], ct))[0];
    }

    /// <summary>Moves to the trash. The file is removed after doc.trash_days unless restored.</summary>
    public async Task<bool> TrashAsync(Guid id, CancellationToken ct)
    {
        var d = await LoadAsync(id, null, ct);
        if (d is null) return false;
        if (d.LegalHold) throw new BusinessRuleException("This document is on legal hold and can't be deleted.");
        if (d.DeletedAt is not null || d.PurgedAt is not null) return true;
        d.DeletedAt = Now;
        d.DeletedBy = tenant.UserId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DocumentDto?> RestoreAsync(Guid id, CancellationToken ct)
    {
        var d = await LoadAsync(id, null, ct);
        if (d is null) return null;
        if (d.PurgedAt is not null) throw new BusinessRuleException("The trash was emptied; this file can't be restored.", 410);
        d.DeletedAt = null;
        d.DeletedBy = null;
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([d], ct))[0];
    }

    /// <summary>Tries the virus scan again (the scanner was down when it was uploaded).</summary>
    public async Task<DocumentDto?> RescanAsync(Guid id, CancellationToken ct)
    {
        var d = await LoadAsync(id, null, ct);
        if (d is null) return null;
        if (d.ScanStatus != DocumentScanStatus.Pending) return (await ToDtosAsync([d], ct))[0];
        await ScanStoredAsync(d, ct);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([d], ct))[0];
    }

    // ------------------------------------------------------------ tasks

    public async Task<IReadOnlyList<DocumentDto>?> LinkToTaskAsync(Guid taskId, Guid documentId, CancellationToken ct)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, ct);
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct);
        if (t is null || d is null) return null;
        if (!WorkTaskStatus.IsActive(t.Status)) throw new BusinessRuleException("This task is closed; reopen it to add documents.");
        if (t.ClientId is { } c && c != d.ClientId) throw new RequestValidationException("documentId", "This document belongs to another client.");
        if (d.DeletedAt is not null || d.PurgedAt is not null) throw new BusinessRuleException("This document is deleted.");
        if (!await db.TaskDocuments.AnyAsync(x => x.TaskId == taskId && x.DocumentId == documentId, ct))
        {
            db.TaskDocuments.Add(new TaskDocument { TaskId = taskId, DocumentId = documentId, LinkedBy = tenant.UserId, LinkedAt = Now });
            TaskHistory(taskId, $"Document linked: {d.Name}");
            await db.SaveChangesAsync(ct);
        }
        return await ForTaskAsync(taskId, ct);
    }

    public async Task<IReadOnlyList<DocumentDto>?> UnlinkFromTaskAsync(Guid taskId, Guid documentId, CancellationToken ct)
    {
        var t = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, ct);
        var link = await db.TaskDocuments.FirstOrDefaultAsync(x => x.TaskId == taskId && x.DocumentId == documentId, ct);
        if (t is null || link is null) return null;
        if (!WorkTaskStatus.IsActive(t.Status) || t.Status == WorkTaskStatus.InReview)
            throw new BusinessRuleException("Documents can't be removed while the task is in review or closed.");
        var name = await db.Documents.Where(x => x.Id == documentId).Select(x => x.Name).FirstAsync(ct);
        db.TaskDocuments.Remove(link);
        TaskHistory(taskId, $"Document unlinked: {name}");
        await db.SaveChangesAsync(ct);
        return await ForTaskAsync(taskId, ct);
    }

    private void TaskHistory(Guid taskId, string body) =>
        db.TaskComments.Add(new TaskComment { TaskId = taskId, Kind = TaskCommentKind.Status, Body = body, CreatedBy = tenant.UserId, CreatedAt = Now });

    // ------------------------------------------------------------ retention and the job

    /// <summary>Documents kept longer than doc.retention_years (from the end of their year; permanent ones from the client's deletion).</summary>
    public async Task<RetentionPage> PastRetentionAsync(int take, CancellationToken ct)
    {
        var (years, query) = await PastRetentionQueryAsync(ct);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Doc.RetentionFrom).ThenBy(x => x.Doc.Id).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct);
        var names = await FolderNamesAsync(ct);
        return new RetentionPage(years, rows.Select(x => new RetentionItem(x.Doc.Id, x.Doc.ClientId, x.ClientCode, x.ClientName, x.Doc.Name,
            names.Name(x.Doc.Folder, x.Doc.TypeCode), x.Doc.Fy,
            (x.Doc.RetentionFrom ?? DateOnly.FromDateTime(x.ClientDeletedAt!.Value)).AddYears(years), x.Doc.SizeBytes,
            x.ClientDeletedAt != null)).ToList(), total);
    }

    /// <summary>Removes the files of the given documents if they are still past retention (and not on hold). Rows stay as a record.</summary>
    public async Task<int> PurgePastRetentionAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count is 0 or > 1000) throw new RequestValidationException("ids", "Choose between 1 and 1000 documents.");
        var (_, query) = await PastRetentionQueryAsync(ct);
        var list = ids.ToList();
        var dueIds = await query.Where(x => list.Contains(x.Doc.Id)).Select(x => x.Doc.Id).ToListAsync(ct);
        var docs = await db.Documents.Where(d => dueIds.Contains(d.Id)).ToListAsync(ct);
        foreach (var d in docs) await RemoveFileAsync(d, PurgeReasons.Retention, ct);
        await db.SaveChangesAsync(ct);
        return docs.Count;
    }

    private sealed class DueRow
    {
        public Document Doc { get; init; } = null!;
        public string ClientCode { get; init; } = "";
        public string ClientName { get; init; } = "";
        public DateTime? ClientDeletedAt { get; init; }
    }

    private async Task<(int Years, IQueryable<DueRow> Query)> PastRetentionQueryAsync(CancellationToken ct)
    {
        var years = await settings.GetAsync(Keys.DocRetentionYears, ct: ct);
        var cutoff = (await TodayAsync(ct)).AddYears(-years);
        var cutoffAt = cutoff.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var firmId = tenant.FirmId;
        // deleted clients too (their permanent documents count from the deletion); RLS still limits rows to this firm
        var clients = db.Clients.IgnoreQueryFilters().Where(c => c.FirmId == firmId);
        var q = from d in db.Documents
                join c in clients on d.ClientId equals c.Id
                where d.PurgedAt == null && !d.LegalHold &&
                      ((d.RetentionFrom != null && d.RetentionFrom < cutoff) ||
                       (d.RetentionFrom == null && c.DeletedAt != null && c.DeletedAt < cutoffAt))
                select new DueRow { Doc = d, ClientCode = c.Code, ClientName = c.Name, ClientDeletedAt = c.DeletedAt };
        return (years, q);
    }

    /// <summary>For the background job: scans files that are waiting, empties the trash after doc.trash_days.</summary>
    public async Task<MaintenanceResult> MaintainAsync(CancellationToken ct)
    {
        var scanned = 0;
        var infected = 0;
        var pending = await db.Documents
            .Where(d => d.ScanStatus == DocumentScanStatus.Pending && d.PurgedAt == null && d.StorageKey != null && d.ScanAttempts < 200)
            .OrderBy(d => d.CreatedAt).Take(50).ToListAsync(ct);
        foreach (var d in pending)
        {
            var scannerDown = await ScanStoredAsync(d, ct);
            if (d.ScanStatus != DocumentScanStatus.Pending) scanned++;
            if (d.ScanStatus == DocumentScanStatus.Infected) infected++;
            await db.SaveChangesAsync(ct);
            if (scannerDown) break;   // try the rest on the next run
        }

        var trashDays = await settings.GetAsync(Keys.DocTrashDays, ct: ct);
        var before = Now.AddDays(-trashDays);
        var old = await db.Documents.Where(d => d.DeletedAt != null && d.DeletedAt < before && d.PurgedAt == null && !d.LegalHold)
            .OrderBy(d => d.DeletedAt).Take(500).ToListAsync(ct);
        foreach (var d in old) await RemoveFileAsync(d, PurgeReasons.Trash, ct);
        if (old.Count > 0) await db.SaveChangesAsync(ct);
        return new MaintenanceResult(scanned, infected, old.Count);
    }

    /// <summary>Scans a stored file. Returns true when the scanner couldn't answer.</summary>
    private async Task<bool> ScanStoredAsync(Document d, CancellationToken ct)
    {
        if (!scanner.Enabled)
        {
            d.ScanStatus = DocumentScanStatus.NotScanned;
            return false;
        }
        d.ScanAttempts++;
        if (d.StorageKey is null) return false;
        await using var s = await store.OpenAsync(d.StorageKey, ct);
        if (s is null)
        {
            d.ScanDetail = "The file is missing from storage.";
            return false;
        }
        var r = await scanner.ScanAsync(s, ct);
        switch (r.Outcome)
        {
            case ScanResult.Clean:
                d.ScanStatus = DocumentScanStatus.Clean; d.ScanDetail = null; d.ScannedAt = Now;
                break;
            case ScanResult.Infected:
                d.ScanStatus = DocumentScanStatus.Infected; d.ScanDetail = Trim(r.Detail, 300); d.ScannedAt = Now;
                await s.DisposeAsync();
                await RemoveFileAsync(d, PurgeReasons.Infected, ct);
                break;
            default:
                d.ScanDetail = Trim(r.Detail, 300) ?? "The virus scanner did not answer.";
                return true;
        }
        return false;
    }

    private async Task RemoveFileAsync(Document d, string reason, CancellationToken ct)
    {
        if (d.StorageKey is { } key) await store.DeleteAsync(key, ct);
        d.StorageKey = null;
        d.PurgedAt = Now;
        d.PurgeReason = reason;
    }

    // ------------------------------------------------------------ helpers

    /// <summary>In the vault: not in the trash and still there — or removed because of a virus, so people see why.</summary>
    private static IQueryable<Document> Visible(IQueryable<Document> q) =>
        q.Where(d => d.DeletedAt == null && (d.PurgedAt == null || d.PurgeReason == PurgeReasons.Infected));

    private async Task<Document?> LoadAsync(Guid id, uint? version, CancellationToken ct)
    {
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is not null && version is { } v && v != d.Version) throw new DbUpdateConcurrencyException("Document was changed by someone else.");
        return d;
    }

    internal sealed class FolderNames(List<DocumentFolder> folders, Dictionary<string, (string Name, int Order)> types)
    {
        public IReadOnlyList<DocumentFolder> Folders => folders;
        public string Name(string? folder, string? typeCode) =>
            folder is not null ? folders.FirstOrDefault(f => f.Code == folder)?.Name ?? folder
            : typeCode is not null && types.TryGetValue(typeCode, out var t) ? t.Name : typeCode ?? "";
        public int Order(string? folder, string? typeCode) =>
            folder is not null ? folders.FirstOrDefault(f => f.Code == folder)?.SortOrder ?? 999
            : typeCode is not null && types.TryGetValue(typeCode, out var t) ? t.Order : 999;
    }

    internal async Task<FolderNames> FolderNamesAsync(CancellationToken ct)
    {
        var folders = await db.DocumentFolders.AsNoTracking().OrderBy(f => f.SortOrder).ToListAsync(ct);
        var types = await db.ComplianceTypes.AsNoTracking().Select(t => new { t.Code, t.Name, t.SortOrder })
            .ToDictionaryAsync(t => t.Code, t => (t.Name, t.SortOrder), ct);
        return new FolderNames(folders, types);
    }

    private async Task<List<DocumentDto>> ToDtosAsync(IReadOnlyList<Document> docs, CancellationToken ct)
    {
        if (docs.Count == 0) return [];
        var names = await FolderNamesAsync(ct);
        var userIds = docs.Where(d => d.CreatedBy != null).Select(d => d.CreatedBy!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var ids = docs.Select(d => d.Id).ToList();
        var taskCounts = await db.TaskDocuments.Where(l => ids.Contains(l.DocumentId)).GroupBy(l => l.DocumentId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return docs.Select(d => new DocumentDto(d.Id, d.ClientId, d.Folder, d.TypeCode, names.Name(d.Folder, d.TypeCode), d.Fy, d.PeriodKey,
            d.Name, d.FileName, d.Extension, d.SizeBytes, d.ScanStatus, d.ScanDetail, d.Source,
            d.Source == DocumentSource.Portal ? "Client (upload link)" : d.CreatedBy is { } u ? users.GetValueOrDefault(u) : null,
            d.CreatedAt, d.Notes, d.LegalHold, d.DeletedAt, d.PurgedAt, d.PurgeReason, d.CanDownload,
            taskCounts.GetValueOrDefault(d.Id), d.Version)).ToList();
    }

    private static string? Trim(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }

    internal async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        var tzName = await settings.GetAsync(Keys.TimeZone, ct: ct);
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(tzName, out var found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).DateTime);
    }
}
