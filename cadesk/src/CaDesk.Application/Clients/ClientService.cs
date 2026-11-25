using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Clients;

public sealed record ClientDto(
    Guid Id, string Code, string Name, string? LegalName, string CountryCode, string EntityType,
    IReadOnlyDictionary<string, string> TaxIds, string? Email, string? Phone, bool IsActive, uint Version,
    Guid? GroupId = null, string? GroupName = null, Guid? AssignedUserId = null, string? AssignedUserName = null,
    IReadOnlyDictionary<string, string>? Custom = null, IReadOnlyList<string>? Warnings = null);

/// <summary>
/// Create / update a client. Code is optional: empty = next number from the firm's "client code format" setting.
/// On update, TaxIds and Custom left out (null) keep their current values; GroupId and AssignedUserId are replaced.
/// </summary>
public sealed record SaveClientRequest(
    string? Code, string Name, string? LegalName, string? CountryCode, string? EntityType,
    Dictionary<string, string>? TaxIds, string? Email, string? Phone, bool? IsActive, uint? Version,
    Guid? GroupId = null, Guid? AssignedUserId = null, Dictionary<string, string>? Custom = null);

public sealed record ClientListQuery(
    string? Search = null, Guid? GroupId = null, string? EntityType = null, Guid? AssignedUserId = null,
    bool? Active = null, string? TaxId = null, int Skip = 0, int Take = 50);

public sealed record ClientPage(IReadOnlyList<ClientDto> Items, int Total);

public sealed class ClientValidationException(IReadOnlyDictionary<string, string[]> errors)
    : RequestValidationException(errors);

/// <summary>
/// Client use cases. Tenant filtering is done by the DbContext (and RLS), never here.
/// Rules come from settings and platform data: entity types, required fields, code format, tax ID formats.
/// </summary>
public sealed class ClientService(IAppDbContext db, ISettings settings, TimeProvider clock, CaDesk.Application.Subscriptions.PlanAccessService plans)
{
    public const string NumberEntity = "client";

    // ------------------------------------------------------------ reads

    public async Task<ClientPage> ListAsync(ClientListQuery f, CancellationToken ct)
    {
        var q = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(s) || c.Code.ToLower().Contains(s) ||
                             (c.LegalName != null && c.LegalName.ToLower().Contains(s)));
        }
        if (f.GroupId is { } g) q = q.Where(c => c.GroupId == g);
        if (!string.IsNullOrWhiteSpace(f.EntityType)) q = q.Where(c => c.EntityType == f.EntityType);
        if (f.AssignedUserId is { } u) q = q.Where(c => c.AssignedUserId == u);
        if (f.Active is { } a) q = q.Where(c => c.IsActive == a);
        if (!string.IsNullOrWhiteSpace(f.TaxId))
        {
            var ids = await ClientIdsWithAnyTaxIdAsync(TaxIdRules.Normalise(f.TaxId), ct);
            q = q.Where(c => ids.Contains(c.Id));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Skip(Math.Max(0, f.Skip)).Take(Math.Clamp(f.Take, 1, 200)).ToListAsync(ct);
        return new ClientPage(await ToDtosAsync(rows, ct), total);
    }

    public async Task<ClientDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : (await ToDtosAsync([c], ct))[0];
    }

    // ------------------------------------------------------------ writes

    public async Task<ClientDto> CreateAsync(SaveClientRequest r, CancellationToken ct)
    {
        var rules = await LoadRulesAsync(ct);
        var prepared = await PrepareAsync(r, existing: null, rules, ct);
        if (prepared.Errors.Count > 0) throw new ClientValidationException(prepared.Errors);
        await plans.EnsureClientRoomAsync(1, ct);

        var c = new Client();
        prepared.ApplyTo(c);
        c.Code = prepared.Code ?? await NextFreeCodeAsync(rules, reserved: null, ct);
        db.Clients.Add(c);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([c], ct))[0] with { Warnings = prepared.Warnings };
    }

    public async Task<ClientDto?> UpdateAsync(Guid id, SaveClientRequest r, CancellationToken ct)
    {
        var c = await db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        if (r.Version is { } v && v != c.Version) throw new DbUpdateConcurrencyException("Client was changed by someone else.");

        var rules = await LoadRulesAsync(ct);
        var prepared = await PrepareAsync(r, c, rules, ct);
        if (prepared.Errors.Count > 0) throw new ClientValidationException(prepared.Errors);

        prepared.ApplyTo(c);
        if (prepared.Code is { } code) c.Code = code;
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([c], ct))[0] with { Warnings = prepared.Warnings };
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct)
    {
        var c = await db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.DeletedAt = clock.GetUtcNow().UtcDateTime;
        c.IsActive = false;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>What the client form needs: entity types, required fields, tax ID types, custom fields.</summary>
    public async Task<ClientFormInfo> FormInfoAsync(CancellationToken ct)
    {
        var rules = await LoadRulesAsync(ct);
        var types = await rules.TaxTypesAsync(rules.FirmCountry, ct);
        return new ClientFormInfo(rules.FirmCountry, rules.EntityTypes, rules.Required.Order().ToList(), rules.CodeFormat,
            NumberFormat.Format(rules.CodeFormat, 1, rules.Today, rules.FyStartMonth),
            types.Select(TaxIdTypeDto.From).ToList(),
            rules.CustomFields.Where(d => d.IsActive).OrderBy(d => d.SortOrder).ThenBy(d => d.Label)
                .Select(CustomFieldDto.From).ToList());
    }

    // ------------------------------------------------------------ shared with import

    /// <summary>Everything validation needs, loaded once (import validates thousands of rows with one load).</summary>
    internal async Task<ClientRulesSnapshot> LoadRulesAsync(CancellationToken ct)
    {
        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(ct)
                   ?? throw new BusinessRuleException("Firm not found.", 404);
        var tzName = await settings.GetAsync(Keys.TimeZone, ct: ct);
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(tzName, out var found) ? found : TimeZoneInfo.Utc;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).DateTime);

        return new ClientRulesSnapshot(db)
        {
            FirmCountry = firm.CountryCode.Trim(),
            EntityTypes = await settings.GetAsync(Keys.ClientEntityTypes, ct: ct),
            Required = (await settings.GetAsync(Keys.ClientRequiredFields, ct: ct)).ToHashSet(StringComparer.OrdinalIgnoreCase),
            CodeFormat = await settings.GetAsync(Keys.ClientCodeFormat, ct: ct),
            FyStartMonth = await settings.GetAsync(Keys.FiscalYearStartMonth, ct: ct),
            Today = today,
            CustomFields = await db.CustomFieldDefinitions.AsNoTracking().Where(d => d.Entity == "client").ToListAsync(ct),
        };
    }

    /// <summary>
    /// Validates and normalises one request against the rules. Does not save or take a number.
    /// <paramref name="skipGroupCheck"/>: import passes groups by name and creates missing ones itself.
    /// </summary>
    internal async Task<PreparedClient> PrepareAsync(SaveClientRequest r, Client? existing, ClientRulesSnapshot rules,
        CancellationToken ct, bool skipGroupCheck = false)
    {
        var errors = new Dictionary<string, string[]>();
        var warnings = new List<string>();

        // code: optional; when given it must be free in this firm
        string? code = string.IsNullOrWhiteSpace(r.Code) ? null : r.Code.Trim();
        if (code is { Length: > 30 }) errors["code"] = ["Code can be at most 30 characters."];
        else if (code is not null && code != existing?.Code && await CodeTakenAsync(code, ct))
            errors["code"] = ["A client with this code already exists."];

        var name = r.Name?.Trim() ?? "";
        if (name.Length is 0 or > 200) errors["name"] = ["Name is required (max 200 characters)."];
        var legal = string.IsNullOrWhiteSpace(r.LegalName) ? null : r.LegalName.Trim();
        if (legal is { Length: > 200 }) errors["legalName"] = ["Legal name can be at most 200 characters."];

        var country = (r.CountryCode ?? existing?.CountryCode ?? rules.FirmCountry).Trim().ToUpperInvariant();
        if (country.Length != 2) errors["countryCode"] = ["Use a 2-letter country code."];
        else if (!await rules.CountryExistsAsync(country, ct)) errors["countryCode"] = [$"{country} is not a country we support."];

        var entityType = string.IsNullOrWhiteSpace(r.EntityType) ? existing?.EntityType : r.EntityType.Trim();
        var matchedType = rules.EntityTypes.FirstOrDefault(t => string.Equals(t, entityType, StringComparison.OrdinalIgnoreCase));
        if (entityType is null) errors["entityType"] = [$"Choose an entity type: {string.Join(", ", rules.EntityTypes)}."];
        else if (matchedType is null && entityType != existing?.EntityType)
            errors["entityType"] = [$"'{entityType}' isn't one of your entity types: {string.Join(", ", rules.EntityTypes)}."];
        entityType = matchedType ?? entityType;

        var email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim();
        if (email is not null && (email.Length > 200 || !LooksLikeEmail(email))) errors["email"] = ["Enter a valid email address."];
        var phone = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();
        if (phone is not null && (phone.Length > 30 || phone.Any(ch => !(char.IsDigit(ch) || ch is '+' or ' ' or '-' or '(' or ')'))))
            errors["phone"] = ["Enter a valid phone number."];

        // tax IDs: formats for the client's country
        var taxTypes = country.Length == 2 ? await rules.TaxTypesAsync(country, ct) : [];
        var rawTax = r.TaxIds ?? existing?.TaxIds ?? new Dictionary<string, string>();
        var taxIds = rawTax.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key.Trim().ToUpperInvariant(), kv => TaxIdRules.Normalise(kv.Value));
        foreach (var (k, v) in TaxIdRules.Validate(taxIds, taxTypes)) errors[k] = v;

        // required fields from settings; a tax ID is only required where the client's country uses it
        foreach (var req in rules.Required)
        {
            if (req.Equals("email", StringComparison.OrdinalIgnoreCase) && email is null) errors.TryAdd("email", ["Email is required."]);
            else if (req.Equals("phone", StringComparison.OrdinalIgnoreCase) && phone is null) errors.TryAdd("phone", ["Phone is required."]);
            else if (taxTypes.FirstOrDefault(t => t.Code.Equals(req, StringComparison.OrdinalIgnoreCase)) is { } tt &&
                     !taxIds.ContainsKey(tt.Code))
                errors.TryAdd($"taxIds.{tt.Code}", [$"{tt.Label} is required."]);
        }

        // duplicates within the firm: unique IDs (GSTIN) block, others (PAN) warn
        foreach (var (k, v) in taxIds)
        {
            if (errors.ContainsKey($"taxIds.{k}")) continue;
            if (existing is not null && existing.TaxIds.TryGetValue(k, out var old) && old == v) continue;
            var other = await ClientsWithTaxIdAsync(k, v, existing?.Id, ct);
            if (other.Count == 0) continue;
            var who = string.Join(", ", other.Select(o => $"{o.Code} {o.Name}"));
            if (taxTypes.FirstOrDefault(t => t.Code == k)?.IsUnique == true)
                errors[$"taxIds.{k}"] = [$"This {k} is already used by {who}."];
            else
                warnings.Add($"{k} {v} is also used by {who}.");
        }

        // custom fields
        // left out on update = keep as is (a required field added later doesn't block other edits)
        Dictionary<string, string> custom;
        if (existing is not null && r.Custom is null) custom = new Dictionary<string, string>(existing.Custom);
        else
        {
            (custom, var customErrors) = CustomFieldRules.Validate(r.Custom, rules.CustomFields);
            foreach (var (k, v) in customErrors) errors[k] = v;
        }
        if (existing is not null)   // keep values of fields the firm has switched off
            foreach (var d in rules.CustomFields.Where(d => !d.IsActive))
                if (existing.Custom.TryGetValue(d.Key, out var keep)) custom[d.Key] = keep;

        if (!skipGroupCheck && r.GroupId is { } gid && !await db.ClientGroups.AnyAsync(g => g.Id == gid, ct))
            errors["groupId"] = ["Group not found."];
        if (r.AssignedUserId is { } uid && uid != existing?.AssignedUserId &&
            !await db.Users.AnyAsync(u => u.Id == uid && u.IsActive, ct))
            errors["assignedUserId"] = ["Assigned user not found or inactive."];

        return new PreparedClient(errors, warnings)
        {
            Code = code, Name = name, LegalName = legal, CountryCode = country, EntityType = entityType ?? "",
            TaxIds = taxIds, Email = email, Phone = phone, Custom = custom,
            GroupId = r.GroupId, AssignedUserId = r.AssignedUserId, IsActive = r.IsActive ?? existing?.IsActive ?? true,
        };
    }

    /// <summary>Next client code from the firm's format. Skips numbers already taken by hand-typed codes.</summary>
    internal async Task<string> NextFreeCodeAsync(ClientRulesSnapshot rules, ISet<string>? reserved, CancellationToken ct)
    {
        var period = NumberFormat.Period(rules.CodeFormat, rules.Today, rules.FyStartMonth);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var seq = (await db.Database
                .SqlQuery<long>($"select next_number({NumberEntity}, {period}) as \"Value\"")
                .ToListAsync(ct))[0];
            var code = NumberFormat.Format(rules.CodeFormat, seq, rules.Today, rules.FyStartMonth);
            if (reserved?.Contains(code) == true) continue;
            if (!await CodeTakenAsync(code, ct)) return code;
        }
        throw new BusinessRuleException("Couldn't find a free client code. Check the client code format in Settings.");
    }

    // ------------------------------------------------------------ helpers

    private async Task<List<(Guid Id, string Code, string Name)>> ClientsWithTaxIdAsync(
        string code, string value, Guid? exclude, CancellationToken ct)
    {
        // jsonb lookup; firm_id filter mirrors the EF filter (RLS enforces it too). Deleted clients don't count.
        var ids = await db.Database.SqlQuery<Guid>($"""
            select id as "Value" from clients
             where firm_id = app_current_firm() and deleted_at is null and tax_ids ->> {code} = {value}
            """).ToListAsync(ct);
        if (exclude is { } ex) ids.Remove(ex);
        if (ids.Count == 0) return [];
        return (await db.Clients.AsNoTracking().Where(c => ids.Contains(c.Id)).OrderBy(c => c.Code)
                .Select(c => new { c.Id, c.Code, c.Name }).Take(3).ToListAsync(ct))
            .Select(x => (x.Id, x.Code, x.Name)).ToList();
    }

    /// <summary>Codes stay taken after a soft delete (the unique index covers deleted rows too).</summary>
    private async Task<bool> CodeTakenAsync(string code, CancellationToken ct) =>
        (await db.Database.SqlQuery<int>($"""
            select count(*)::int as "Value" from clients where firm_id = app_current_firm() and code = {code}
            """).ToListAsync(ct))[0] > 0;

    private async Task<List<Guid>> ClientIdsWithAnyTaxIdAsync(string value, CancellationToken ct) =>
        await db.Database.SqlQuery<Guid>($"""
            select c.id as "Value" from clients c
             where c.firm_id = app_current_firm() and c.deleted_at is null
               and exists (select 1 from jsonb_each_text(c.tax_ids) t where t.value = {value})
            """).ToListAsync(ct);

    private async Task<List<ClientDto>> ToDtosAsync(IReadOnlyList<Client> rows, CancellationToken ct)
    {
        var groupIds = rows.Where(r => r.GroupId != null).Select(r => r.GroupId!.Value).Distinct().ToList();
        var userIds = rows.Where(r => r.AssignedUserId != null).Select(r => r.AssignedUserId!.Value).Distinct().ToList();
        var groups = groupIds.Count == 0 ? new Dictionary<Guid, string>()
            : await db.ClientGroups.AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var users = userIds.Count == 0 ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return rows.Select(c => new ClientDto(
            c.Id, c.Code, c.Name, c.LegalName, c.CountryCode.Trim(), c.EntityType, c.TaxIds, c.Email, c.Phone, c.IsActive, c.Version,
            c.GroupId, c.GroupId is { } g && groups.TryGetValue(g, out var gn) ? gn : null,
            c.AssignedUserId, c.AssignedUserId is { } u && users.TryGetValue(u, out var un) ? un : null,
            c.Custom)).ToList();
    }

    private static bool LooksLikeEmail(string s)
    {
        var at = s.IndexOf('@');
        return at > 0 && at == s.LastIndexOf('@') && s.IndexOf('.', at) > at + 1 && !s.EndsWith('.') && !s.Any(char.IsWhiteSpace);
    }
}

/// <summary>Rules for one firm at one moment. Tax ID types and countries are looked up once per country.</summary>
public sealed class ClientRulesSnapshot(IAppDbContext db)
{
    private readonly Dictionary<string, IReadOnlyCollection<TaxIdType>> _taxTypes = new();
    private readonly Dictionary<string, bool> _countries = new();

    public required string FirmCountry { get; init; }
    public required string[] EntityTypes { get; init; }
    public required HashSet<string> Required { get; init; }
    public required string CodeFormat { get; init; }
    public required int FyStartMonth { get; init; }
    public required DateOnly Today { get; init; }
    public required List<CustomFieldDefinition> CustomFields { get; init; }

    public async Task<IReadOnlyCollection<TaxIdType>> TaxTypesAsync(string country, CancellationToken ct)
    {
        if (!_taxTypes.TryGetValue(country, out var list))
            _taxTypes[country] = list = await db.TaxIdTypes.AsNoTracking()
                .Where(t => t.CountryCode == country).OrderBy(t => t.SortOrder).ToListAsync(ct);
        return list;
    }

    public async Task<bool> CountryExistsAsync(string country, CancellationToken ct)
    {
        if (!_countries.TryGetValue(country, out var ok))
            _countries[country] = ok = (await db.Database
                .SqlQuery<int>($"select count(*)::int as \"Value\" from countries where code = {country}")
                .ToListAsync(ct))[0] > 0;
        return ok;
    }
}

/// <summary>A validated, normalised client ready to apply. Errors empty = OK to save.</summary>
public sealed class PreparedClient(Dictionary<string, string[]> errors, List<string> warnings)
{
    public Dictionary<string, string[]> Errors { get; } = errors;
    public List<string> Warnings { get; } = warnings;
    public string? Code { get; init; }
    public string Name { get; init; } = "";
    public string? LegalName { get; init; }
    public string CountryCode { get; init; } = "";
    public string EntityType { get; init; } = "";
    public Dictionary<string, string> TaxIds { get; init; } = new();
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public Dictionary<string, string> Custom { get; init; } = new();
    public Guid? GroupId { get; set; }
    public Guid? AssignedUserId { get; init; }
    public bool IsActive { get; init; }

    public void ApplyTo(Client c)
    {
        c.Name = Name;
        c.LegalName = LegalName;
        c.CountryCode = CountryCode;
        c.EntityType = EntityType;
        c.TaxIds = TaxIds;
        c.Email = Email;
        c.Phone = Phone;
        c.Custom = Custom;
        c.GroupId = GroupId;
        c.AssignedUserId = AssignedUserId;
        c.IsActive = IsActive;
    }
}

public sealed record TaxIdTypeDto(string CountryCode, string Code, string Label, string? Example, bool IsUnique)
{
    public static TaxIdTypeDto From(TaxIdType t) => new(t.CountryCode.Trim(), t.Code, t.Label, t.Example, t.IsUnique);
}

public sealed record CustomFieldDto(Guid Id, string Key, string Label, string FieldType, IReadOnlyList<string>? Options,
    bool IsRequired, string? ValidationRegex, int SortOrder, bool IsActive)
{
    public static CustomFieldDto From(CustomFieldDefinition d) =>
        new(d.Id, d.Key, d.Label, d.FieldType, d.Options, d.IsRequired, d.ValidationRegex, d.SortOrder, d.IsActive);
}

public sealed record ClientFormInfo(string FirmCountry, IReadOnlyList<string> EntityTypes, IReadOnlyList<string> RequiredFields,
    string CodeFormat, string NextCodeExample, IReadOnlyList<TaxIdTypeDto> TaxIdTypes, IReadOnlyList<CustomFieldDto> CustomFields);
