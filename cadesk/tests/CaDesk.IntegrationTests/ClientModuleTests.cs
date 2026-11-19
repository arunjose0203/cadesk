using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CaDesk.Application.Clients;
using CaDesk.Infrastructure.Configuration;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 5: client numbering, rules from settings, tax IDs, custom fields, contacts, groups, notes, import.</summary>
public class ClientModuleTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static SaveClientRequest New(string name, string? pan = "AAACA1234F", string? code = null, string type = "Company",
        Dictionary<string, string>? taxIds = null, Dictionary<string, string>? custom = null, Guid? groupId = null) =>
        new(code, name, null, null, type, taxIds ?? (pan is null ? null : new() { ["PAN"] = pan }),
            null, null, null, null, groupId, null, custom);

    private async Task<HttpClient> NewFirm(string name = "Client Co") =>
        api.WithToken((await api.SignUpAsync(name)).AccessToken);

    private static async Task<ClientDto> Create(HttpClient c, SaveClientRequest r)
    {
        var res = await c.PostAsJsonAsync("/api/clients", r);
        Assert.True(res.StatusCode == HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    private static async Task<Dictionary<string, string[]>> Errors(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private static Task<HttpResponseMessage> PutSetting(HttpClient c, string key, string json) =>
        c.PutAsJsonAsync($"/api/settings/{key}",
            new SaveSettingRequest(JsonDocument.Parse(json).RootElement.Clone(), null, null, null, "test"));

    // ------------------------------------------------------------ numbering

    [Fact]
    public async Task Codes_are_numbered_from_the_firm_format_and_typed_codes_are_skipped()
    {
        var c = await NewFirm();
        Assert.Equal("C0001", (await Create(c, New("One"))).Code);
        await Create(c, New("Typed", code: "C0002"));             // someone typed the next number by hand
        Assert.Equal("C0003", (await Create(c, New("Three"))).Code);

        Assert.Equal(HttpStatusCode.NoContent, (await PutSetting(c, "client.code_format", "\"KL/{FY}/{SEQ:3}\"")).StatusCode);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")).DateTime);
        Assert.Equal($"KL/{NumberFormat.FinancialYear(today, 4)}/001", (await Create(c, New("Yearly"))).Code);
    }

    [Fact]
    public async Task Parallel_creates_never_share_a_code()
    {
        var c = await NewFirm();
        var made = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Create(c, New($"P{i}"))));
        Assert.Equal(10, made.Select(m => m.Code).Distinct().Count());
    }

    // ------------------------------------------------------------ rules from settings and tax ID formats

    [Fact]
    public async Task Required_fields_entity_types_and_tax_id_formats_are_enforced()
    {
        var c = await NewFirm();

        Assert.Contains("taxIds.PAN", (await Errors(await c.PostAsJsonAsync("/api/clients", New("No PAN", pan: null)))).Keys);
        Assert.Contains("taxIds.PAN", (await Errors(await c.PostAsJsonAsync("/api/clients", New("Bad PAN", pan: "1234")))).Keys);
        Assert.Contains("entityType", (await Errors(await c.PostAsJsonAsync("/api/clients", New("Odd", type: "Spaceship")))).Keys);

        var badGstin = New("Bad GSTIN", taxIds: new() { ["PAN"] = "AAACA1234F", ["GSTIN"] = "32AAACA1234F1ZK" });
        Assert.Contains("taxIds.GSTIN", (await Errors(await c.PostAsJsonAsync("/api/clients", badGstin))).Keys);

        var otherPan = New("Wrong PAN", taxIds: new() { ["PAN"] = "BXMPM4521K", ["GSTIN"] = "32AAACA1234F1ZJ" });
        Assert.Contains("taxIds.GSTIN", (await Errors(await c.PostAsJsonAsync("/api/clients", otherPan))).Keys);

        // the firm adds email to required fields
        Assert.Equal(HttpStatusCode.NoContent, (await PutSetting(c, "client.required_fields", "[\"PAN\",\"email\"]")).StatusCode);
        Assert.Contains("email", (await Errors(await c.PostAsJsonAsync("/api/clients", New("No email")))).Keys);

        // spaces and lower case are cleaned up
        var ok = await Create(c, New("Fine") with { Email = "a@b.in", TaxIds = new() { ["PAN"] = "aaaca 1234 f" } });
        Assert.Equal("AAACA1234F", ok.TaxIds["PAN"]);
    }

    [Fact]
    public async Task Same_gstin_is_blocked_but_same_pan_only_warns()
    {
        var c = await NewFirm();
        var ids = new Dictionary<string, string> { ["PAN"] = "AAACB1234C", ["GSTIN"] = "32AAACB1234C1ZO" };
        var first = await Create(c, New("Branch Kochi", taxIds: ids));
        Assert.Empty(first.Warnings ?? []);

        Assert.Contains("taxIds.GSTIN", (await Errors(await c.PostAsJsonAsync("/api/clients", New("Copy", taxIds: ids)))).Keys);

        // another registration of the same business: same PAN, different GSTIN
        var second = await Create(c, New("Branch Kannur", taxIds: new() { ["PAN"] = "AAACB1234C", ["GSTIN"] = "32AAACB1234C2ZN" }));
        Assert.Contains(second.Warnings!, w => w.Contains(first.Code));

        // search by tax ID finds both
        var found = await c.GetFromJsonAsync<ClientPage>("/api/clients?taxId=aaacb1234c");
        Assert.Equal(2, found!.Total);
    }

    // ------------------------------------------------------------ custom fields

    [Fact]
    public async Task Custom_fields_are_validated_and_normalised()
    {
        var c = await NewFirm();
        var res = await c.PostAsJsonAsync("/api/custom-fields",
            new SaveCustomFieldRequest("plan", "Service plan", "dropdown", ["Basic", "Premium"], true, null, 1, true));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        await c.PostAsJsonAsync("/api/custom-fields",
            new SaveCustomFieldRequest("onboarded", "Onboarded on", "date", null, false, null, 2, true));

        Assert.Contains("custom.plan", (await Errors(await c.PostAsJsonAsync("/api/clients", New("No plan")))).Keys);
        Assert.Contains("custom.plan", (await Errors(await c.PostAsJsonAsync("/api/clients",
            New("Gold", custom: new() { ["plan"] = "Gold" })))).Keys);

        var ok = await Create(c, New("Premium client", custom: new() { ["plan"] = "premium", ["onboarded"] = "01/04/2025" }));
        Assert.Equal("Premium", ok.Custom!["plan"]);
        Assert.Equal("2025-04-01", ok.Custom["onboarded"]);

        // a bad key format is refused; the key and type of a field can't change later
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/custom-fields",
            new SaveCustomFieldRequest("Bad Key", "x", "text", null, false, null, 0, true))).StatusCode);
    }

    // ------------------------------------------------------------ contacts, groups, notes

    [Fact]
    public async Task First_contact_is_primary_and_only_one_primary_is_kept()
    {
        var c = await NewFirm();
        var client = await Create(c, New("Contacts Co"));
        var url = $"/api/clients/{client.Id}/contacts";

        var a = (await (await c.PostAsJsonAsync(url, new SaveContactRequest("Anita", "Director", "anita@x.in", null, null, null)))
            .Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.True(a.IsPrimary);

        var b = (await (await c.PostAsJsonAsync(url, new SaveContactRequest("Biju", "Accountant", null, "+91 98470 00000", true, null)))
            .Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.True(b.IsPrimary);

        var list = (await c.GetFromJsonAsync<List<ContactDto>>(url))!;
        Assert.Single(list, x => x.IsPrimary);
        Assert.Equal("Biju", list[0].Name);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync(url, new SaveContactRequest("No way to reach", null, null, null, null, null))).StatusCode);
    }

    [Fact]
    public async Task Groups_hold_clients_and_only_empty_groups_can_be_deleted()
    {
        var c = await NewFirm();
        var g = (await (await c.PostAsJsonAsync("/api/client-groups", new SaveGroupRequest("Menon family")))
            .Content.ReadFromJsonAsync<GroupDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/client-groups", new SaveGroupRequest("menon FAMILY"))).StatusCode);

        var client = await Create(c, New("Bindu Menon", pan: "BXMPM4521K", type: "Individual", groupId: g.Id));
        Assert.Equal("Menon family", client.GroupName);
        Assert.Equal(1, (await c.GetFromJsonAsync<List<GroupDto>>("/api/client-groups"))!.Single().ClientCount);

        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync($"/api/client-groups/{g.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/clients/{client.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/client-groups/{g.Id}")).StatusCode);
    }

    [Fact]
    public async Task Notes_show_their_author()
    {
        var c = await NewFirm();
        var client = await Create(c, New("Notes Co"));
        Assert.Equal(HttpStatusCode.Created,
            (await c.PostAsJsonAsync($"/api/clients/{client.Id}/notes", new SaveNoteRequest("Called about GST notice."))).StatusCode);
        var notes = (await c.GetFromJsonAsync<List<NoteDto>>($"/api/clients/{client.Id}/notes"))!;
        Assert.Equal("Test Partner", Assert.Single(notes).CreatedByName);
    }

    // ------------------------------------------------------------ firm isolation

    [Fact]
    public async Task Another_firm_cannot_reach_contacts_groups_or_fields()
    {
        var a = await NewFirm("Firm A2");
        var b = await NewFirm("Firm B2");
        var client = await Create(a, New("A's client"));
        var group = (await (await a.PostAsJsonAsync("/api/client-groups", new SaveGroupRequest("A group")))
            .Content.ReadFromJsonAsync<GroupDto>())!;
        await a.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldRequest("a_only", "A only", "text", null, true, null, 0, true));

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/clients/{client.Id}/contacts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/clients/{client.Id}/notes", new SaveNoteRequest("hi"))).StatusCode);
        Assert.Contains("groupId", (await Errors(await b.PostAsJsonAsync("/api/clients", New("B's", groupId: group.Id)))).Keys);
        Assert.Empty((await b.GetFromJsonAsync<List<GroupDto>>("/api/client-groups"))!);
        // A's required custom field doesn't apply to B
        await Create(b, New("B's client"));
        // and B numbers from 1
        Assert.Equal("C0002", (await Create(b, New("B's second"))).Code);
    }

    // ------------------------------------------------------------ import

    private static MultipartFormDataContent Csv(string text)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "clients.csv" } };
    }

    private const string ImportCsv =
        "Name,Entity type,PAN,GSTIN,Group,Email\r\n" +
        "Anand Textiles Pvt Ltd,Company,AAACA1234F,32AAACA1234F1ZJ,Anand group,accounts@anand.example\r\n" +
        "Anand Exports,Company,AAACB1234C,,Anand group,\r\n" +
        "Broken PAN,Company,12345,,,\r\n" +
        "Same GSTIN,Company,AAACA1234F,32AAACA1234F1ZJ,,\r\n" +
        ",Company,AAACA1234F,,,\r\n";

    [Fact]
    public async Task Import_preview_reports_row_errors_and_commit_saves_valid_rows_only_when_asked()
    {
        var c = await NewFirm();

        var preview = (await (await c.PostAsync("/api/clients/import/preview", Csv(ImportCsv)))
            .Content.ReadFromJsonAsync<ImportPreview>())!;
        Assert.Equal(5, preview.TotalRows);
        Assert.Equal(2, preview.ValidRows);
        Assert.Equal(new[] { "Anand group" }, preview.NewGroups);
        Assert.Equal(new[] { 4, 5, 6 }, preview.Rows.Where(r => !r.Ok).Select(r => r.Row));
        Assert.Contains(preview.Rows.Single(r => r.Row == 5).Errors, e => e.Contains("row 2"));
        Assert.Empty((await c.GetFromJsonAsync<ClientPage>("/api/clients"))!.Items);   // preview saves nothing

        // with errors and no skipInvalid: nothing is saved
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await c.PostAsync("/api/clients/import/commit", Csv(ImportCsv))).StatusCode);
        Assert.Equal(0, (await c.GetFromJsonAsync<ClientPage>("/api/clients"))!.Total);

        var done = (await (await c.PostAsync("/api/clients/import/commit?skipInvalid=true", Csv(ImportCsv)))
            .Content.ReadFromJsonAsync<ImportResult>())!;
        Assert.Equal(2, done.Created);
        var page = (await c.GetFromJsonAsync<ClientPage>("/api/clients"))!;
        Assert.Equal(new[] { "C0001", "C0002" }, page.Items.Select(i => i.Code).Order());
        Assert.All(page.Items, i => Assert.Equal("Anand group", i.GroupName));
    }

    [Fact]
    public async Task Import_template_lists_tax_ids_and_custom_fields()
    {
        var c = await NewFirm();
        await c.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldRequest("plan", "Service plan", "text", null, false, null, 0, true));
        var csv = await c.GetStringAsync("/api/clients/import/template?format=csv");
        Assert.StartsWith("Code,Name,Legal name,Country,Entity type,PAN,GSTIN,TAN,", csv.TrimStart('﻿'));
        Assert.Contains("Service plan", csv);

        var xlsx = await c.GetAsync("/api/clients/import/template");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
    }
}
