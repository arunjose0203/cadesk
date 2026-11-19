using System.Text;
using CaDesk.Api.Auth;
using CaDesk.Api.Clients;
using CaDesk.Application.Clients;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Compliance;
using CaDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/clients").WithTags("Clients");

        // ---- clients

        g.MapGet("/", async (string? search, Guid? groupId, string? entityType, Guid? assignedUserId, bool? active,
                string? taxId, int? skip, int? take, ClientService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(
                new ClientListQuery(search, groupId, entityType, assignedUserId, active, taxId, skip ?? 0, take ?? 50), ct)))
            .RequirePermission(Perms.ClientsRead);

        g.MapGet("/form", async (ClientService svc, CancellationToken ct) => Results.Ok(await svc.FormInfoAsync(ct)))
            .RequirePermission(Perms.ClientsRead);

        g.MapGet("/{id:guid}", async (Guid id, ClientService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } c ? Results.Ok(c) : Results.NotFound())
            .RequirePermission(Perms.ClientsRead);

        // a client's deadlines follow its details (GSTIN added → GST returns appear)
        g.MapPost("/", async (SaveClientRequest req, ClientService svc, DeadlineService deadlines, ILogger<ClientService> log, CancellationToken ct) =>
        {
            var c = await svc.CreateAsync(req, ct);
            await RefreshDeadlines(deadlines, [c.Id], log, ct);
            return Results.Created($"/api/clients/{c.Id}", c);
        }).RequirePermission(Perms.ClientsWrite);

        g.MapPut("/{id:guid}", async (Guid id, SaveClientRequest req, ClientService svc, DeadlineService deadlines, ILogger<ClientService> log, CancellationToken ct) =>
        {
            if (await svc.UpdateAsync(id, req, ct) is not { } c) return Results.NotFound();
            await RefreshDeadlines(deadlines, [c.Id], log, ct);
            return Results.Ok(c);
        }).RequirePermission(Perms.ClientsWrite);

        g.MapDelete("/{id:guid}", async (Guid id, ClientService svc, CancellationToken ct) =>
            await svc.SoftDeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.ClientsDelete);

        // ---- contacts

        g.MapGet("/{id:guid}/contacts", async (Guid id, ClientPartsService svc, CancellationToken ct) =>
            await svc.ListContactsAsync(id, ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequirePermission(Perms.ClientsRead);

        g.MapPost("/{id:guid}/contacts", async (Guid id, SaveContactRequest r, ClientPartsService svc, CancellationToken ct) =>
            await svc.SaveContactAsync(id, null, r, ct) is { } c ? Results.Created($"/api/clients/{id}/contacts/{c.Id}", c) : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);

        g.MapPut("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, SaveContactRequest r, ClientPartsService svc, CancellationToken ct) =>
            await svc.SaveContactAsync(id, contactId, r, ct) is { } c ? Results.Ok(c) : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);

        g.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, ClientPartsService svc, CancellationToken ct) =>
            await svc.DeleteContactAsync(id, contactId, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);

        // ---- notes

        g.MapGet("/{id:guid}/notes", async (Guid id, ClientPartsService svc, CancellationToken ct) =>
            await svc.ListNotesAsync(id, ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequirePermission(Perms.ClientsRead);

        g.MapPost("/{id:guid}/notes", async (Guid id, SaveNoteRequest r, ClientPartsService svc, CancellationToken ct) =>
            await svc.AddNoteAsync(id, r, ct) is { } n ? Results.Created($"/api/clients/{id}/notes/{n.Id}", n) : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);

        g.MapDelete("/{id:guid}/notes/{noteId:guid}", async (Guid id, Guid noteId, ClientPartsService svc, CancellationToken ct) =>
            await svc.DeleteNoteAsync(id, noteId, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);

        // ---- import (.xlsx / .csv)

        g.MapGet("/import/template", async (string? format, ClientImportService imp, ClientService svc, CancellationToken ct) =>
        {
            var headers = await imp.TemplateHeadersAsync(ct);
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ClientImportService.TemplateCsv(headers))).ToArray(),
                    "text/csv", "client-import-template.csv");
            return Results.File(SpreadsheetFiles.TemplateXlsx(headers, await svc.FormInfoAsync(ct)),
                SpreadsheetFiles.XlsxType, "client-import-template.xlsx");
        }).RequirePermission(Perms.ClientsWrite);

        g.MapPost("/import/preview", async (IFormFile? file, ClientImportService imp, CancellationToken ct) =>
            Results.Ok(await imp.PreviewAsync(await SpreadsheetFiles.ReadAsync(file, ct), ct)))
            .RequirePermission(Perms.ClientsWrite)
            .DisableAntiforgery()   // bearer-token API: no cookies to forge
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(SpreadsheetFiles.MaxBytes + 64 * 1024));

        g.MapPost("/import/commit", async (IFormFile? file, bool? skipInvalid, ClientImportService imp, DeadlineService deadlines,
                ILogger<ClientService> log, CancellationToken ct) =>
            {
                var result = await imp.CommitAsync(await SpreadsheetFiles.ReadAsync(file, ct), skipInvalid ?? false, ct);
                if (result.Created > 0) await RefreshDeadlines(deadlines, null, log, ct);
                return result.Created == 0 && result.Preview.ErrorRows > 0 ? Results.UnprocessableEntity(result) : Results.Ok(result);
            })
            .RequirePermission(Perms.ClientsWrite)
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(SpreadsheetFiles.MaxBytes + 64 * 1024));

        // ---- groups

        var groups = app.MapGroup("/api/client-groups").WithTags("Clients");
        groups.MapGet("/", async (ClientPartsService svc, CancellationToken ct) => Results.Ok(await svc.ListGroupsAsync(ct)))
            .RequirePermission(Perms.ClientsRead);
        groups.MapPost("/", async (SaveGroupRequest r, ClientPartsService svc, CancellationToken ct) =>
            await svc.SaveGroupAsync(null, r, ct) is { } x ? Results.Created($"/api/client-groups/{x.Id}", x) : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);
        groups.MapPut("/{id:guid}", async (Guid id, SaveGroupRequest r, ClientPartsService svc, CancellationToken ct) =>
            await svc.SaveGroupAsync(id, r, ct) is { } x ? Results.Ok(x) : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);
        groups.MapDelete("/{id:guid}", async (Guid id, ClientPartsService svc, CancellationToken ct) =>
            await svc.DeleteGroupAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequirePermission(Perms.ClientsWrite);

        // ---- custom field definitions (firm admin) and tax ID formats (platform data)

        var fields = app.MapGroup("/api/custom-fields").WithTags("Clients");
        fields.MapGet("/", async (ClientPartsService svc, CancellationToken ct) => Results.Ok(await svc.ListCustomFieldsAsync(ct)))
            .RequirePermission(Perms.ClientsRead);
        fields.MapPost("/", async (SaveCustomFieldRequest r, ClientPartsService svc, CancellationToken ct) =>
            await svc.SaveCustomFieldAsync(null, r, ct) is { } x ? Results.Created($"/api/custom-fields/{x.Id}", x) : Results.NotFound())
            .RequirePermission(Perms.SettingsManage);
        fields.MapPut("/{id:guid}", async (Guid id, SaveCustomFieldRequest r, ClientPartsService svc, CancellationToken ct) =>
            await svc.SaveCustomFieldAsync(id, r, ct) is { } x ? Results.Ok(x) : Results.NotFound())
            .RequirePermission(Perms.SettingsManage);

        app.MapGet("/api/tax-id-types", async (string? country, IAppDbContext db, CancellationToken ct) =>
        {
            var q = db.TaxIdTypes.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(country)) q = q.Where(t => t.CountryCode == country.Trim().ToUpper());
            var rows = await q.OrderBy(t => t.CountryCode).ThenBy(t => t.SortOrder).ToListAsync(ct);
            return Results.Ok(rows.Select(TaxIdTypeDto.From));
        }).WithTags("Clients").RequirePermission(Perms.ClientsRead);

        return app;
    }

    /// <summary>The client is already saved; if deadlines fail here the daily job catches up, so don't fail the request.</summary>
    private static async Task RefreshDeadlines(DeadlineService deadlines, IReadOnlyCollection<Guid>? ids, ILogger log, CancellationToken ct)
    {
        try { await deadlines.GenerateAsync(ids, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Deadline refresh after client change failed"); }
    }
}
