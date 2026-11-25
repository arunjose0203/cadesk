using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Documents;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Endpoints;

public sealed record LinkDocumentRequest(Guid DocumentId);
public sealed record HoldRequest(bool Hold);
public sealed record PurgeRequest(List<Guid> Ids);
/// <summary>A file the web app generated (GST return JSON or CSV working), sent as text.</summary>
public sealed record SaveGeneratedDocumentRequest(string FileName, string Content, string? Folder = null, string? TypeCode = null,
    string? Fy = null, string? Name = null, string? Notes = null, string? ContentBase64 = null);

public static class DocumentEndpoints
{
    /// <summary>Largest request an upload may be: the highest doc.max_size_mb (50) plus room for the form fields.</summary>
    public const long MaxUploadRequestBytes = 50L * 1024 * 1024 + 256 * 1024;
    public const string PortalRateLimit = "portal";

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- a client's vault

        var c = app.MapGroup("/api/clients/{clientId:guid}").WithTags("Documents");

        c.MapGet("/documents/vault", async (Guid clientId, DocumentService svc, CancellationToken ct) =>
            await svc.VaultAsync(clientId, ct) is { } v ? Results.Ok(v) : Results.NotFound())
            .RequirePermission(Perms.DocumentsRead);

        // fy: a year (2026-27) or "permanent"; trash=true lists the trash
        c.MapGet("/documents", async (Guid clientId, string? fy, string? folder, string? typeCode, string? search, bool? trash,
                int? skip, int? take, DocumentService svc, CancellationToken ct) =>
            await svc.ListAsync(clientId, new DocumentQuery(fy, folder, typeCode, search, trash ?? false, skip ?? 0, take ?? 100), ct) is { } p
                ? Results.Ok(p) : Results.NotFound())
            .RequirePermission(Perms.DocumentsRead);

        // multipart: file + folder | typeCode, fy, name, notes, taskId
        c.MapPost("/documents", async (Guid clientId, HttpRequest req, DocumentService svc, CancellationToken ct) =>
            {
                var (file, form) = await ReadUploadAsync(req, ct);
                var r = new UploadRequest(form("folder"), form("typeCode"), form("fy"), form("name"), form("notes"),
                    Guid.TryParse(form("taskId"), out var tid) ? tid : null);
                return await svc.UploadAsync(clientId, r, file, ct) is { } d
                    ? Results.Created($"/api/documents/{d.Id}", d) : Results.NotFound();
            })
            .RequirePermission(Perms.DocumentsUpload)
            .DisableAntiforgery()   // bearer-token API: no cookies to forge
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes));

        // a file the app generated (Sprint 66: GSTR-1 JSON, GSTR-3B / GSTR-9 working); .json or .csv only
        c.MapPost("/documents/generated", async (Guid clientId, SaveGeneratedDocumentRequest r, DocumentService svc, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(r.FileName)) throw new RequestValidationException("fileName", "A file name is required.");
                // text files (json, csv) come as Content; binary ones (pdf) as ContentBase64
                byte[] bytes;
                if (!string.IsNullOrEmpty(r.ContentBase64))
                {
                    if (r.ContentBase64.Length > MaxUploadRequestBytes / 3 * 4 + 4) throw new RequestValidationException("contentBase64", "The file is too large.");
                    try { bytes = Convert.FromBase64String(r.ContentBase64); }
                    catch (FormatException) { throw new RequestValidationException("contentBase64", "The file content is not valid base64."); }
                }
                else if (!string.IsNullOrEmpty(r.Content)) bytes = System.Text.Encoding.UTF8.GetBytes(r.Content);
                else throw new RequestValidationException("content", "The file is empty.");
                if (bytes.Length == 0) throw new RequestValidationException("content", "The file is empty.");
                if (bytes.LongLength > MaxUploadRequestBytes) throw new RequestValidationException("content", "The file is too large.");
                var file = new UploadFile(r.FileName, bytes.LongLength, () => new MemoryStream(bytes, writable: false));
                var req = new UploadRequest(r.Folder, r.TypeCode, r.Fy, r.Name, r.Notes, null);
                return await svc.SaveGeneratedAsync(clientId, req, file, ct) is { } d
                    ? Results.Created($"/api/documents/{d.Id}", d) : Results.NotFound();
            })
            .RequirePermission(Perms.DocumentsUpload)
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes));

        // ---- client upload links (made by staff)

        c.MapGet("/upload-links", async (Guid clientId, PortalService svc, CancellationToken ct) =>
            await svc.ListLinksAsync(clientId, ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequirePermission(Perms.DocumentsUpload);
        c.MapPost("/upload-links", async (Guid clientId, CreateUploadLinkRequest r, PortalService svc, CancellationToken ct) =>
            await svc.CreateLinkAsync(clientId, r, ct) is { } x ? Results.Created($"/api/upload-links/{x.Link.Id}", x) : Results.NotFound())
            .RequirePermission(Perms.DocumentsUpload);
        app.MapPost("/api/upload-links/{id:guid}/revoke", async (Guid id, PortalService svc, CancellationToken ct) =>
            await svc.RevokeLinkAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .WithTags("Documents").RequirePermission(Perms.DocumentsUpload);

        // ---- one document

        var d = app.MapGroup("/api/documents").WithTags("Documents");

        d.MapGet("/folders", async (IAppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.DocumentFolders.AsNoTracking().OrderBy(f => f.SortOrder).ToListAsync(ct)))
            .RequirePermission(Perms.DocumentsRead);

        d.MapGet("/{id:guid}", async (Guid id, DocumentService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } x ? Results.Ok(x) : Results.NotFound())
            .RequirePermission(Perms.DocumentsRead);

        d.MapGet("/{id:guid}/download", async (Guid id, DocumentService svc, HttpResponse res, CancellationToken ct) =>
            {
                if (await svc.OpenAsync(id, ct) is not { } f) return Results.NotFound();
                // never shown inside the app's pages: always a download, never sniffed or run as a page
                res.Headers.XContentTypeOptions = "nosniff";
                res.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
                res.Headers.CacheControl = "private, no-store";
                return Results.File(f.Content, f.ContentType, f.FileName);
            })
            .RequirePermission(Perms.DocumentsRead);

        d.MapPut("/{id:guid}", async (Guid id, UpdateDocumentRequest r, DocumentService svc, CancellationToken ct) =>
            await svc.UpdateAsync(id, r, ct) is { } x ? Results.Ok(x) : Results.NotFound())
            .RequirePermission(Perms.DocumentsUpload);

        d.MapPut("/{id:guid}/hold", async (Guid id, HoldRequest r, DocumentService svc, CancellationToken ct) =>
            await svc.SetHoldAsync(id, r.Hold, ct) is { } x ? Results.Ok(x) : Results.NotFound())
            .RequirePermission(Perms.DocumentsDelete);

        d.MapDelete("/{id:guid}", async (Guid id, DocumentService svc, CancellationToken ct) =>
            await svc.TrashAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.DocumentsDelete);

        d.MapPost("/{id:guid}/restore", async (Guid id, DocumentService svc, CancellationToken ct) =>
            await svc.RestoreAsync(id, ct) is { } x ? Results.Ok(x) : Results.NotFound())
            .RequirePermission(Perms.DocumentsDelete);

        d.MapPost("/{id:guid}/rescan", async (Guid id, DocumentService svc, CancellationToken ct) =>
            await svc.RescanAsync(id, ct) is { } x ? Results.Ok(x) : Results.NotFound())
            .RequirePermission(Perms.DocumentsUpload);

        // ---- retention: files kept longer than doc.retention_years, for a person to remove

        d.MapGet("/retention", async (int? take, DocumentService svc, CancellationToken ct) =>
            Results.Ok(await svc.PastRetentionAsync(take ?? 200, ct)))
            .RequirePermission(Perms.DocumentsDelete);
        d.MapPost("/retention/purge", async (PurgeRequest r, DocumentService svc, CancellationToken ct) =>
            Results.Ok(new { removed = await svc.PurgePastRetentionAsync(r.Ids ?? [], ct) }))
            .RequirePermission(Perms.DocumentsDelete);

        // what the 10-minute job does (scan waiting files, empty the trash), now
        d.MapPost("/maintenance", async (DocumentService svc, CancellationToken ct) => Results.Ok(await svc.MaintainAsync(ct)))
            .RequirePermission(Perms.SettingsManage);

        // ---- documents a task uses

        var t = app.MapGroup("/api/tasks/{taskId:guid}/documents").WithTags("Documents");
        t.MapGet("/", async (Guid taskId, DocumentService svc, IAppDbContext db, CancellationToken ct) =>
            await db.Tasks.AnyAsync(x => x.Id == taskId, ct) ? Results.Ok(await svc.ForTaskAsync(taskId, ct)) : Results.NotFound())
            .RequirePermission(Perms.TasksRead).RequirePermission(Perms.DocumentsRead);
        t.MapPost("/", async (Guid taskId, LinkDocumentRequest r, DocumentService svc, CancellationToken ct) =>
            await svc.LinkToTaskAsync(taskId, r.DocumentId, ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequirePermission(Perms.TasksRead).RequirePermission(Perms.DocumentsUpload);
        t.MapDelete("/{documentId:guid}", async (Guid taskId, Guid documentId, DocumentService svc, CancellationToken ct) =>
            await svc.UnlinkFromTaskAsync(taskId, documentId, ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequirePermission(Perms.TasksRead).RequirePermission(Perms.DocumentsUpload);
        // upload straight into the task's client folder (its filing and year when it's a deadline's task)
        t.MapPost("/upload", async (Guid taskId, HttpRequest req, DocumentService svc, IAppDbContext db, CancellationToken ct) =>
            {
                var task = await db.Tasks.AsNoTracking().Where(x => x.Id == taskId).Select(x => new { x.ClientId }).FirstOrDefaultAsync(ct);
                if (task is null) return Results.NotFound();
                if (task.ClientId is not { } clientId)
                    throw new RequestValidationException("taskId", "Choose a client for this task before adding documents.");
                var (file, form) = await ReadUploadAsync(req, ct);
                var r = new UploadRequest(form("folder"), form("typeCode"), form("fy"), form("name"), form("notes"), taskId);
                return await svc.UploadAsync(clientId, r, file, ct) is { } x ? Results.Created($"/api/documents/{x.Id}", x) : Results.NotFound();
            })
            .RequirePermission(Perms.TasksRead).RequirePermission(Perms.DocumentsUpload)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes));

        // ---- the client's side of an upload link: no account, the link is the key

        var p = app.MapGroup("/api/portal/uploads/{token}").WithTags("Client uploads").AllowAnonymous().RequireRateLimiting(PortalRateLimit);
        p.MapGet("/", async (string token, PortalService svc, CancellationToken ct) => Results.Ok(await svc.InfoAsync(token, ct)));
        p.MapPost("/", async (string token, HttpRequest req, PortalService svc, CancellationToken ct) =>
            {
                var (file, _) = await ReadUploadAsync(req, ct);
                return Results.Ok(await svc.UploadAsync(token, file, ct));
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes));

        return app;
    }

    /// <summary>One file (field "file") and the form's text fields.</summary>
    private static async Task<(UploadFile File, Func<string, string?> Field)> ReadUploadAsync(HttpRequest req, CancellationToken ct)
    {
        if (!req.HasFormContentType) throw new RequestValidationException("file", "Send the file as multipart/form-data.");
        var form = await req.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? (form.Files.Count == 1 ? form.Files[0] : null);
        if (file is null) throw new RequestValidationException("file", "Choose a file to upload.");
        return (new UploadFile(file.FileName, file.Length, file.OpenReadStream),
            name => form.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v.ToString()) ? v.ToString() : null);
    }
}
