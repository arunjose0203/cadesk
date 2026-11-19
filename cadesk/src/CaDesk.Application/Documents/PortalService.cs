using System.Security.Cryptography;
using System.Text;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Documents;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Documents;

public sealed record UploadLinkDto(Guid Id, Guid ClientId, string? Message, string FolderName, string? Fy, Guid? TaskId, string? TaskTitle,
    int MaxFiles, int UploadedCount, DateTime ExpiresAt, DateTime? RevokedAt, DateTime? LastUsedAt, DateTime CreatedAt,
    string? CreatedByName, string Status);

public sealed record CreateUploadLinkRequest(string? Message = null, string? Folder = null, string? TypeCode = null, string? Fy = null,
    Guid? TaskId = null, int? Days = null, int? MaxFiles = null);

/// <summary>The token is shown once, here; only its hash is kept.</summary>
public sealed record UploadLinkCreated(UploadLinkDto Link, string Token);

public sealed record PortalFile(string Name, long SizeBytes, DateTime UploadedAt, string Status);
public sealed record PortalInfo(string FirmName, string ClientName, string? Message, string FolderName, string? Fy,
    IReadOnlyList<string> AllowedTypes, int MaxSizeMb, DateTime ExpiresAt, int Remaining, IReadOnlyList<PortalFile> Received);

/// <summary>Who an upload link is for. Set on the tenant as the "role" so the audit log shows a client did it.</summary>
public static class PortalActor
{
    public const string Role = "client_user";
}

/// <summary>
/// Upload links: staff make a link for a client (optionally for one filing or task); the client opens it without an account
/// and sends files. A link works until it expires, is revoked, or has received its maximum number of files, and only while
/// the firm allows client uploads for that client (doc.client_upload_enabled). Every file goes through the same checks
/// as a staff upload.
/// </summary>
public sealed class PortalService(IAppDbContext db, ISettings settings, ITenantContext tenant, ITenantSetter setTenant,
    TimeProvider clock, DocumentService documents)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------ staff side

    public async Task<IReadOnlyList<UploadLinkDto>?> ListLinksAsync(Guid clientId, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct)) return null;
        var links = await db.DocumentUploadLinks.AsNoTracking().Where(l => l.ClientId == clientId)
            .OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync(ct);
        return await ToDtosAsync(links, ct);
    }

    public async Task<UploadLinkCreated?> CreateLinkAsync(Guid clientId, CreateUploadLinkRequest r, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return null;
        if (!client.IsActive) throw new BusinessRuleException("This client is inactive.");
        if (!await settings.GetAsync(Keys.DocClientUploadEnabled, clientId, ct: ct))
            throw new BusinessRuleException("Client uploads are switched off for this client (setting \"Clients can upload\").");
        if (r.Message is { Length: > 500 }) throw new RequestValidationException("message", "The message can be at most 500 characters.");
        var days = r.Days ?? await settings.GetAsync(Keys.DocUploadLinkDays, ct: ct);
        if (days is < 1 or > 90) throw new RequestValidationException("days", "A link can work for 1 to 90 days.");
        var maxFiles = r.MaxFiles ?? 20;
        if (maxFiles is < 1 or > 100) throw new RequestValidationException("maxFiles", "Allow 1 to 100 files.");

        string? folder = r.Folder, typeCode = r.TypeCode, fy = r.Fy, periodKey = null;
        if (r.TaskId is { } taskId)
        {
            var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
                ?? throw new RequestValidationException("taskId", "Task not found.");
            if (task.ClientId != clientId) throw new RequestValidationException("taskId", "This task is for another client.");
            if (!WorkTaskStatus.IsActive(task.Status)) throw new BusinessRuleException("This task is closed.");
            if (task.DeadlineId is { } did && folder is null && typeCode is null &&
                await db.Deadlines.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did, ct) is { } d)
            {
                typeCode = d.TypeCode;
                periodKey = d.PeriodKey;
                fy ??= DocumentRules.FyOf(d.PeriodEnd, await settings.GetAsync(Keys.FiscalYearStartMonth, ct: ct));
            }
        }
        // the same checks as an upload, so a bad folder or year is refused now rather than when the client uses the link
        await documents.CheckPlaceAsync(folder, typeCode, fy, ct);

        var token = NewToken();
        var link = new DocumentUploadLink
        {
            ClientId = clientId, TokenHash = Hash(token), Message = string.IsNullOrWhiteSpace(r.Message) ? null : r.Message.Trim(),
            Folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim(),
            TypeCode = string.IsNullOrWhiteSpace(typeCode) ? null : typeCode.Trim(),
            Fy = string.IsNullOrWhiteSpace(fy) ? null : fy.Trim(), PeriodKey = periodKey, TaskId = r.TaskId,
            MaxFiles = maxFiles, ExpiresAt = Now.AddDays(days), CreatedBy = tenant.UserId, CreatedAt = Now,
        };
        db.DocumentUploadLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return new UploadLinkCreated((await ToDtosAsync([link], ct))[0], token);
    }

    public async Task<bool> RevokeLinkAsync(Guid id, CancellationToken ct)
    {
        var link = await db.DocumentUploadLinks.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (link is null) return false;
        link.RevokedAt ??= Now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------------------------------------ client side (no account; the link is the key)

    public async Task<PortalInfo> InfoAsync(string token, CancellationToken ct)
    {
        var link = await OpenLinkAsync(token, ct);
        var client = await db.Clients.AsNoTracking().FirstAsync(c => c.Id == link.ClientId, ct);
        var firm = await db.Firms.AsNoTracking().FirstAsync(ct);
        var names = await documents.FolderNamesAsync(ct);
        var received = await db.Documents.AsNoTracking().Where(d => d.UploadLinkId == link.Id).OrderBy(d => d.CreatedAt)
            .Select(d => new PortalFile(d.FileName, d.SizeBytes, d.CreatedAt,
                d.ScanStatus == DocumentScanStatus.Infected ? "removed: virus found" : "received"))
            .ToListAsync(ct);
        return new PortalInfo(firm.Name, client.Name, link.Message, PlaceName(names, link), link.Fy,
            await settings.GetAsync(Keys.DocAllowedTypes, link.ClientId, ct: ct), await settings.GetAsync(Keys.DocMaxSizeMb, link.ClientId, ct: ct),
            link.ExpiresAt, Math.Max(0, link.MaxFiles - link.UploadedCount), received);
    }

    public async Task<PortalFile> UploadAsync(string token, UploadFile file, CancellationToken ct)
    {
        var link = await OpenLinkAsync(token, ct);

        // claim a place first (atomic), so parallel uploads can't go over the link's limit
        var claimed = await db.DocumentUploadLinks
            .Where(l => l.Id == link.Id && l.UploadedCount < l.MaxFiles && l.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.UploadedCount, l => l.UploadedCount + 1).SetProperty(l => l.LastUsedAt, Now), ct);
        if (claimed == 0) throw new BusinessRuleException("This link has received all the files it allows. Ask for a new link.", 410);

        try
        {
            // only link the task while it's still open; otherwise the file just goes to the folder
            Guid? taskId = link.TaskId is { } tid && await db.Tasks.AnyAsync(t => t.Id == tid &&
                t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled, ct) ? tid : null;
            var doc = await documents.StoreAsync(link.ClientId,
                new UploadRequest(link.Folder, link.TypeCode, link.Fy, null, null, taskId), file, DocumentSource.Portal, link.Id, ct)
                ?? throw new BusinessRuleException("This link is no longer valid.", 410);
            return new PortalFile(doc.FileName, doc.SizeBytes, doc.UploadedAt, "received");
        }
        catch
        {
            await db.DocumentUploadLinks.Where(l => l.Id == link.Id && l.UploadedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.UploadedCount, l => l.UploadedCount - 1), CancellationToken.None);
            throw;
        }
    }

    /// <summary>Finds the link by its token's hash, then works inside that firm, under both isolation locks, as the client.</summary>
    private async Task<DocumentUploadLink> OpenLinkAsync(string token, CancellationToken ct)
    {
        const string invalid = "This upload link is not valid or has expired. Ask your accountant for a new one.";
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200) throw new BusinessRuleException(invalid, 410);
        var hash = Hash(token);
        var found = (await db.Database.SqlQuery<LinkLookup>($"""
            select id as "Id", firm_id as "FirmId", client_id as "ClientId", expires_at as "ExpiresAt", revoked_at as "RevokedAt"
            from portal_find_upload_link({hash})
            """).ToListAsync(ct)).FirstOrDefault();
        if (found is null || found.RevokedAt is not null || found.ExpiresAt <= Now) throw new BusinessRuleException(invalid, 410);

        setTenant.Set(found.FirmId, null, PortalActor.Role);
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(ct);
        if (firm is null || firm.Status is not (FirmStatus.Trial or FirmStatus.Active)) throw new BusinessRuleException(invalid, 410);
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == found.ClientId, ct);
        if (client is null || !client.IsActive) throw new BusinessRuleException(invalid, 410);
        if (!await settings.GetAsync(Keys.DocClientUploadEnabled, found.ClientId, ct: ct))
            throw new BusinessRuleException("Uploads are switched off. Please contact your accountant.", 403);
        return await db.DocumentUploadLinks.AsNoTracking().FirstAsync(l => l.Id == found.Id, ct);
    }

    /// <summary>Row from portal_find_upload_link (a class with setters, as SqlQuery needs).</summary>
    public sealed class LinkLookup
    {
        public Guid Id { get; set; }
        public Guid FirmId { get; set; }
        public Guid ClientId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

    private static string PlaceName(DocumentService.FolderNames names, DocumentUploadLink l) =>
        l.Folder is null && l.TypeCode is null ? names.Name("other", null) : names.Name(l.Folder, l.TypeCode);

    private async Task<List<UploadLinkDto>> ToDtosAsync(IReadOnlyList<DocumentUploadLink> links, CancellationToken ct)
    {
        var names = await documents.FolderNamesAsync(ct);
        var userIds = links.Where(l => l.CreatedBy != null).Select(l => l.CreatedBy!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var taskIds = links.Where(l => l.TaskId != null).Select(l => l.TaskId!.Value).Distinct().ToList();
        var tasks = await db.Tasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        var now = Now;
        return links.Select(l => new UploadLinkDto(l.Id, l.ClientId, l.Message, PlaceName(names, l), l.Fy, l.TaskId,
            l.TaskId is { } t ? tasks.GetValueOrDefault(t) : null, l.MaxFiles, l.UploadedCount, l.ExpiresAt, l.RevokedAt, l.LastUsedAt,
            l.CreatedAt, l.CreatedBy is { } u ? users.GetValueOrDefault(u) : null,
            l.RevokedAt is not null ? "revoked" : l.ExpiresAt <= now ? "expired" : l.UploadedCount >= l.MaxFiles ? "full" : "active")).ToList();
    }

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
