using System.Text.RegularExpressions;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Clients;

public sealed record ContactDto(Guid Id, string Name, string? Role, string? Email, string? Phone, bool IsPrimary, bool ReceivesReminders);
public sealed record SaveContactRequest(string Name, string? Role, string? Email, string? Phone, bool? IsPrimary, bool? ReceivesReminders);

public sealed record NoteDto(Guid Id, string Body, Guid CreatedBy, string? CreatedByName, DateTime CreatedAt);
public sealed record SaveNoteRequest(string Body);

public sealed record GroupDto(Guid Id, string Name, int ClientCount);
public sealed record SaveGroupRequest(string Name);

public sealed record SaveCustomFieldRequest(string Key, string Label, string FieldType, List<string>? Options,
    bool? IsRequired, string? ValidationRegex, int? SortOrder, bool? IsActive);

/// <summary>Contacts, notes, groups and the firm's custom field definitions.</summary>
public sealed class ClientPartsService(IAppDbContext db, ITenantContext tenant)
{
    // ------------------------------------------------------------ contacts

    public async Task<IReadOnlyList<ContactDto>?> ListContactsAsync(Guid clientId, CancellationToken ct)
    {
        if (!await ClientExists(clientId, ct)) return null;
        return await db.ClientContacts.AsNoTracking().Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name)
            .Select(c => new ContactDto(c.Id, c.Name, c.Role, c.Email, c.Phone, c.IsPrimary, c.ReceivesReminders))
            .ToListAsync(ct);
    }

    public async Task<ContactDto?> SaveContactAsync(Guid clientId, Guid? contactId, SaveContactRequest r, CancellationToken ct)
    {
        if (!await ClientExists(clientId, ct)) return null;
        ValidateContact(r);

        ClientContact? c;
        if (contactId is { } id)
        {
            c = await db.ClientContacts.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
            if (c is null) return null;
        }
        else
        {
            c = new ClientContact { ClientId = clientId };
            db.ClientContacts.Add(c);
            // the first contact becomes primary unless told otherwise
            if (r.IsPrimary is null && !await db.ClientContacts.AnyAsync(x => x.ClientId == clientId, ct)) c.IsPrimary = true;
        }

        if (r.IsPrimary == false && c.IsPrimary && contactId is not null)
            throw new RequestValidationException("isPrimary", "Make another contact primary instead.");

        c.Name = r.Name.Trim();
        c.Role = Clean(r.Role);
        c.Email = Clean(r.Email);
        c.Phone = Clean(r.Phone);
        c.ReceivesReminders = r.ReceivesReminders ?? c.ReceivesReminders;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (r.IsPrimary == true && !c.IsPrimary)
        {
            // only one primary per client (unique index): clear the old one first
            var others = await db.ClientContacts.Where(x => x.ClientId == clientId && x.IsPrimary && x.Id != c.Id).ToListAsync(ct);
            foreach (var o in others) o.IsPrimary = false;
            await db.SaveChangesAsync(ct);
        }
        if (r.IsPrimary is { } p) c.IsPrimary = p;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new ContactDto(c.Id, c.Name, c.Role, c.Email, c.Phone, c.IsPrimary, c.ReceivesReminders);
    }

    public async Task<bool> DeleteContactAsync(Guid clientId, Guid contactId, CancellationToken ct)
    {
        var c = await db.ClientContacts.FirstOrDefaultAsync(x => x.Id == contactId && x.ClientId == clientId, ct);
        if (c is null) return false;
        db.ClientContacts.Remove(c);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidateContact(SaveContactRequest r)
    {
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200) e["name"] = ["Name is required (max 200 characters)."];
        if (Clean(r.Email) is { } em && (em.Length > 200 || !em.Contains('@') || em.Any(char.IsWhiteSpace))) e["email"] = ["Enter a valid email address."];
        if (Clean(r.Phone) is { Length: > 30 }) e["phone"] = ["Phone can be at most 30 characters."];
        if (Clean(r.Role) is { Length: > 100 }) e["role"] = ["Role can be at most 100 characters."];
        if (Clean(r.Email) is null && Clean(r.Phone) is null) e["email"] = ["Give an email or a phone number."];
        if (e.Count > 0) throw new RequestValidationException(e);
    }

    // ------------------------------------------------------------ notes

    public async Task<IReadOnlyList<NoteDto>?> ListNotesAsync(Guid clientId, CancellationToken ct)
    {
        if (!await ClientExists(clientId, ct)) return null;
        var notes = await db.ClientNotes.AsNoTracking().Where(n => n.ClientId == clientId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(200).ToListAsync(ct);
        var authors = notes.Select(n => n.CreatedBy).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => authors.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return notes.Select(n => new NoteDto(n.Id, n.Body, n.CreatedBy, names.GetValueOrDefault(n.CreatedBy), n.CreatedAt)).ToList();
    }

    public async Task<NoteDto?> AddNoteAsync(Guid clientId, SaveNoteRequest r, CancellationToken ct)
    {
        if (!await ClientExists(clientId, ct)) return null;
        var body = r.Body?.Trim() ?? "";
        if (body.Length is 0 or > 4000) throw new RequestValidationException("body", "Write a note (max 4000 characters).");
        var me = tenant.UserId ?? throw new BusinessRuleException("Only a signed-in user can add notes.", 403);
        var n = new ClientNote { ClientId = clientId, Body = body, CreatedBy = me };
        db.ClientNotes.Add(n);
        await db.SaveChangesAsync(ct);
        var name = await db.Users.Where(u => u.Id == me).Select(u => u.Name).FirstOrDefaultAsync(ct);
        return new NoteDto(n.Id, n.Body, n.CreatedBy, name, n.CreatedAt);
    }

    /// <summary>Notes are a record: only the author can remove their own note.</summary>
    public async Task<bool> DeleteNoteAsync(Guid clientId, Guid noteId, CancellationToken ct)
    {
        var n = await db.ClientNotes.FirstOrDefaultAsync(x => x.Id == noteId && x.ClientId == clientId, ct);
        if (n is null) return false;
        if (n.CreatedBy != tenant.UserId) throw new BusinessRuleException("Only the person who wrote a note can delete it.", 403);
        db.ClientNotes.Remove(n);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------------------------------------ groups

    public async Task<IReadOnlyList<GroupDto>> ListGroupsAsync(CancellationToken ct)
    {
        var counts = await db.Clients.AsNoTracking().Where(c => c.GroupId != null)
            .GroupBy(c => c.GroupId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var groups = await db.ClientGroups.AsNoTracking().OrderBy(g => g.Name).ToListAsync(ct);
        return groups.Select(g => new GroupDto(g.Id, g.Name, counts.GetValueOrDefault(g.Id))).ToList();
    }

    public async Task<GroupDto?> SaveGroupAsync(Guid? id, SaveGroupRequest r, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length is 0 or > 120) throw new RequestValidationException("name", "Group name is required (max 120 characters).");
        if (await db.ClientGroups.AnyAsync(g => g.Name.ToLower() == name.ToLower() && g.Id != id, ct))
            throw new RequestValidationException("name", "A group with this name already exists.");

        ClientGroup? g;
        if (id is { } gid)
        {
            g = await db.ClientGroups.FirstOrDefaultAsync(x => x.Id == gid, ct);
            if (g is null) return null;
        }
        else db.ClientGroups.Add(g = new ClientGroup());
        g.Name = name;
        await db.SaveChangesAsync(ct);
        return new GroupDto(g.Id, g.Name, await db.Clients.CountAsync(c => c.GroupId == g.Id, ct));
    }

    /// <summary>Only an empty group can be deleted (deleted clients are unlinked automatically).</summary>
    public async Task<bool> DeleteGroupAsync(Guid id, CancellationToken ct)
    {
        var g = await db.ClientGroups.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return false;
        if (await db.Clients.AnyAsync(c => c.GroupId == id, ct))
            throw new BusinessRuleException("Move the clients out of this group before deleting it.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var deleted = await db.Clients.IgnoreQueryFilters()
            .Where(c => c.GroupId == id && c.DeletedAt != null && c.FirmId == g.FirmId).ToListAsync(ct);
        foreach (var c in deleted) c.GroupId = null;
        await db.SaveChangesAsync(ct);   // unlink first: the foreign key must be clear before the delete
        db.ClientGroups.Remove(g);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    // ------------------------------------------------------------ custom fields

    public async Task<IReadOnlyList<CustomFieldDto>> ListCustomFieldsAsync(CancellationToken ct) =>
        (await db.CustomFieldDefinitions.AsNoTracking().Where(d => d.Entity == "client")
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Label).ToListAsync(ct))
        .Select(CustomFieldDto.From).ToList();

    /// <summary>Key and type are fixed once created (existing values depend on them); switch a field off instead.</summary>
    public async Task<CustomFieldDto?> SaveCustomFieldAsync(Guid? id, SaveCustomFieldRequest r, CancellationToken ct)
    {
        var e = new Dictionary<string, string[]>();
        var key = r.Key?.Trim() ?? "";
        var label = r.Label?.Trim() ?? "";
        var type = r.FieldType?.Trim().ToLowerInvariant() ?? "";
        var options = r.Options?.Select(o => o.Trim()).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (!Regex.IsMatch(key, "^[a-z][a-z0-9_]{0,39}$"))
            e["key"] = ["Key: lower-case letters, digits and _ , starting with a letter (max 40)."];
        if (label.Length is 0 or > 80) e["label"] = ["Label is required (max 80 characters)."];
        if (!CustomFieldTypes.All.Contains(type)) e["fieldType"] = [$"Type must be one of: {string.Join(", ", CustomFieldTypes.All)}."];
        if (type == CustomFieldTypes.Dropdown && (options is null || options.Count is 0 or > 100 || options.Any(o => o.Length > 100)))
            e["options"] = ["A dropdown needs 1-100 choices (max 100 characters each)."];
        if (!string.IsNullOrWhiteSpace(r.ValidationRegex))
        {
            if (type != CustomFieldTypes.Text) e["validationRegex"] = ["A format rule only applies to text fields."];
            else if (r.ValidationRegex.Length > 200) e["validationRegex"] = ["Format rule is too long."];
            else
                try { _ = new Regex(r.ValidationRegex, RegexOptions.None, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException) { e["validationRegex"] = ["This format rule isn't a valid regular expression."]; }
        }
        if (e.Count > 0) throw new RequestValidationException(e);

        CustomFieldDefinition? d;
        if (id is { } fid)
        {
            d = await db.CustomFieldDefinitions.FirstOrDefaultAsync(x => x.Id == fid, ct);
            if (d is null) return null;
            if (d.Key != key) throw new RequestValidationException("key", "The key can't be changed. Add a new field instead.");
            if (d.FieldType != type) throw new RequestValidationException("fieldType", "The type can't be changed. Add a new field instead.");
        }
        else
        {
            if (await db.CustomFieldDefinitions.AnyAsync(x => x.Entity == "client" && x.Key == key, ct))
                throw new RequestValidationException("key", "A field with this key already exists.");
            if (await db.CustomFieldDefinitions.CountAsync(x => x.Entity == "client", ct) >= 50)
                throw new BusinessRuleException("A firm can have at most 50 custom client fields.");
            db.CustomFieldDefinitions.Add(d = new CustomFieldDefinition { Entity = "client", Key = key, FieldType = type });
        }

        d.Label = label;
        d.Options = type == CustomFieldTypes.Dropdown ? options : null;
        d.IsRequired = r.IsRequired ?? d.IsRequired;
        d.ValidationRegex = string.IsNullOrWhiteSpace(r.ValidationRegex) ? null : r.ValidationRegex;
        d.SortOrder = r.SortOrder ?? d.SortOrder;
        d.IsActive = r.IsActive ?? d.IsActive;
        await db.SaveChangesAsync(ct);
        return CustomFieldDto.From(d);
    }

    // ------------------------------------------------------------ helpers

    private Task<bool> ClientExists(Guid id, CancellationToken ct) => db.Clients.AnyAsync(c => c.Id == id, ct);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
