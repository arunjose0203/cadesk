using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CaDesk.Api.Auth;
using CaDesk.Application.Clients;
using CaDesk.Application.Documents;
using CaDesk.Application.Tasks;
using CaDesk.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 9: the document vault — folders by year and filing, file checks from settings, virus scan, trash and retention,
/// client upload links, links to tasks, and isolation between firms. The virus scanner is <see cref="TestVirusScanner"/>.
/// </summary>
public class DocumentTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> GstCompany = new() { ["PAN"] = "AAACA1234F", ["GSTIN"] = "32AAACA1234F1ZJ" };

    private sealed record Firm(HttpClient Partner, SignedUp User);

    private async Task<Firm> NewFirm()
    {
        var u = await api.SignUpAsync("Vault Co");
        return new Firm(api.WithToken(u.AccessToken), u);
    }

    private static async Task<ClientDto> NewClient(HttpClient c, string name = "Anand Textiles", Dictionary<string, string>? taxIds = null)
    {
        var res = await c.PostAsJsonAsync("/api/clients",
            new SaveClientRequest(null, name, null, null, "Company", taxIds ?? GstCompany, "accounts@anand.example", null, null, null));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    private static byte[] Pdf(string text = "") => Encoding.ASCII.GetBytes($"%PDF-1.4\n% {Guid.NewGuid()} {text}\n%%EOF\n");

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, Dictionary<string, string>? fields = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        foreach (var (k, v) in fields ?? new Dictionary<string, string>()) form.Add(new StringContent(v), k);
        return form;
    }

    private static Task<HttpResponseMessage> Upload(HttpClient c, Guid clientId, byte[] bytes, string fileName, Dictionary<string, string>? fields = null) =>
        c.PostAsync($"/api/clients/{clientId}/documents", Form(bytes, fileName, fields));

    private static async Task<DocumentDto> Uploaded(HttpClient c, Guid clientId, byte[] bytes, string fileName, Dictionary<string, string>? fields = null)
    {
        var res = await Upload(c, clientId, bytes, fileName, fields);
        Assert.True(res.StatusCode == HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    private static Task<HttpResponseMessage> Set(HttpClient c, string key, string json, string? scope = null, Guid? scopeId = null) =>
        c.PutAsJsonAsync($"/api/settings/{key}", new SaveSettingRequest(JsonDocument.Parse(json).RootElement.Clone(), scope, scopeId, null, "test"));

    private static async Task<DocumentDto> Doc(HttpClient c, Guid id) =>
        (await c.GetFromJsonAsync<DocumentDetail>($"/api/documents/{id}"))!.Document;

    private static async Task Sql(string sql, Guid id)
    {
        await using var conn = new NpgsqlConnection(ApiFactory.OwnerCs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------ folders, download, audit

    [Fact]
    public async Task A_staff_upload_lands_in_its_year_and_folder_and_downloads_unchanged()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var bytes = Pdf("bank statement");

        var doc = await Uploaded(f.Partner, client.Id, bytes, @"C:\Users\me\Desktop\HDFC April.pdf",
            new() { ["folder"] = "bank", ["fy"] = "2025-26", ["notes"] = "Main account" });
        Assert.Equal("HDFC April.pdf", doc.FileName);                // no folder part from the uploader's machine
        Assert.Equal("HDFC April", doc.Name);
        Assert.Equal("2025-26", doc.Fy);
        Assert.Equal("Bank statements", doc.FolderName);
        Assert.Equal("clean", doc.ScanStatus);
        Assert.True(doc.CanDownload);
        Assert.Equal("Test Partner", doc.UploadedByName);

        var permanent = await Uploaded(f.Partner, client.Id, Pdf("pan card"), "PAN.pdf", new() { ["folder"] = "permanent", ["fy"] = "2025-26" });
        Assert.Null(permanent.Fy);                                     // the permanent folder has no year

        var vault = (await f.Partner.GetFromJsonAsync<VaultInfo>($"/api/clients/{client.Id}/documents/vault"))!;
        Assert.Null(vault.Years[0].Fy);                                // permanent first
        var year = vault.Years.Single(y => y.Fy == "2025-26");
        Assert.Equal(1, year.Folders.Single(x => x.Folder == "bank").Count);
        Assert.Contains(vault.Folders, x => x.TypeCode == "GSTR3B_M");  // filing folders for the client's country
        Assert.Contains("pdf", vault.AllowedTypes);
        Assert.True(vault.ScanEnabled);

        var page = (await f.Partner.GetFromJsonAsync<DocumentPage>($"/api/clients/{client.Id}/documents?fy=2025-26&folder=bank"))!;
        Assert.Equal(doc.Id, Assert.Single(page.Items).Id);
        Assert.Single((await f.Partner.GetFromJsonAsync<DocumentPage>($"/api/clients/{client.Id}/documents?fy=permanent"))!.Items);

        var res = await f.Partner.GetAsync($"/api/documents/{doc.Id}/download");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(bytes, await res.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", res.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());

        var audit = await f.Partner.GetStringAsync($"/api/audit-logs?entity=documents&entityId={doc.Id}&action=download");
        Assert.Contains(doc.Id.ToString(), audit);
    }

    [Fact]
    public async Task Moving_and_renaming_keeps_the_year_rules()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var doc = await Uploaded(f.Partner, client.Id, Pdf(), "notice.pdf");
        Assert.Equal("Other", doc.FolderName);                         // no folder given

        var moved = await f.Partner.PutAsJsonAsync($"/api/documents/{doc.Id}",
            new UpdateDocumentRequest("GST notice ASMT-10", "notices", null, "2024-25", null, doc.Version));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var d = (await moved.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(("GST notice ASMT-10", "notices", "2024-25"), (d.Name, d.Folder, d.Fy));

        Assert.Equal(HttpStatusCode.BadRequest, (await f.Partner.PutAsJsonAsync($"/api/documents/{doc.Id}",
            new UpdateDocumentRequest(null, null, null, "2024", null, null))).StatusCode);            // April-March firm: 2024-25
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Partner.PutAsJsonAsync($"/api/documents/{doc.Id}",
            new UpdateDocumentRequest(null, "nope", null, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.PutAsJsonAsync($"/api/documents/{doc.Id}",
            new UpdateDocumentRequest("Again", null, null, null, null, doc.Version))).StatusCode);      // stale version
    }

    // ------------------------------------------------------------ file checks from settings

    [Fact]
    public async Task Type_content_and_size_follow_the_firm_settings()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);

        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(f.Partner, client.Id, Encoding.ASCII.GetBytes("MZ..."), "setup.exe")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(f.Partner, client.Id, Pdf(), "no-extension")).StatusCode);

        var fake = await Upload(f.Partner, client.Id, Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>"), "invoice.pdf");
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.Contains("not a PDF", await fake.Content.ReadAsStringAsync());

        var csv = Encoding.UTF8.GetBytes($"date,amount\n2026-04-01,{Random.Shared.Next()}\n");
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(f.Partner, client.Id, csv, "ledger.csv")).StatusCode);   // csv not allowed by default
        Assert.Equal(HttpStatusCode.NoContent, (await Set(f.Partner, "doc.allowed_types", "[\"pdf\",\"csv\",\"xlsx\"]")).StatusCode);
        await Uploaded(f.Partner, client.Id, csv, "ledger.csv");

        Assert.Equal(HttpStatusCode.Created, (await Upload(f.Partner, client.Id, Office("xl/workbook.xml"), "sales.xlsx")).StatusCode);
        var macros = await Upload(f.Partner, client.Id, Office("xl/workbook.xml", "xl/vbaProject.bin"), "macro.xlsx");
        Assert.Equal(HttpStatusCode.BadRequest, macros.StatusCode);
        Assert.Contains("macros", await macros.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(f.Partner, client.Id, Office("word/document.xml"), "renamed.xlsx")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Set(f.Partner, "doc.max_size_mb", "1")).StatusCode);
        var big = Pdf(new string('x', 1_200_000));
        var tooBig = await Upload(f.Partner, client.Id, big, "scan.pdf");
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.Contains("larger than 1 MB", await tooBig.Content.ReadAsStringAsync());
    }

    private static byte[] Office(params string[] parts)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var part in parts.Prepend("[Content_Types].xml"))
            {
                using var w = new StreamWriter(zip.CreateEntry(part).Open());
                w.Write($"<x>{Guid.NewGuid()}</x>");
            }
        return ms.ToArray();
    }

    [Fact]
    public async Task The_same_file_twice_is_refused_but_another_client_may_have_it()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var other = await NewClient(f.Partner, "Bharat Traders", new() { ["PAN"] = "AAACB1234F" });
        var bytes = Pdf();
        await Uploaded(f.Partner, client.Id, bytes, "Form 16.pdf");
        var again = await Upload(f.Partner, client.Id, bytes, "copy.pdf");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("Form 16", await again.Content.ReadAsStringAsync());
        await Uploaded(f.Partner, other.Id, bytes, "Form 16.pdf");
    }

    // ------------------------------------------------------------ virus scan

    [Fact]
    public async Task Viruses_are_refused_and_files_waiting_for_a_scan_cannot_be_downloaded()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);

        var infected = await Upload(f.Partner, client.Id, Pdf(TestVirusScanner.Eicar), "invoice.pdf");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, infected.StatusCode);
        Assert.Contains("virus", await infected.Content.ReadAsStringAsync());
        Assert.Equal(0, (await f.Partner.GetFromJsonAsync<DocumentPage>($"/api/clients/{client.Id}/documents"))!.Total);

        TestVirusScanner.Down = true;
        DocumentDto waiting;
        try
        {
            waiting = await Uploaded(f.Partner, client.Id, Pdf(), "purchases.pdf");
            Assert.Equal("pending", waiting.ScanStatus);
            Assert.False(waiting.CanDownload);
            Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.GetAsync($"/api/documents/{waiting.Id}/download")).StatusCode);
        }
        finally { TestVirusScanner.Down = false; }

        var run = await f.Partner.PostAsync("/api/documents/maintenance", null);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        Assert.Equal("clean", (await Doc(f.Partner, waiting.Id)).ScanStatus);
        Assert.Equal(HttpStatusCode.OK, (await f.Partner.GetAsync($"/api/documents/{waiting.Id}/download")).StatusCode);
    }

    [Fact]
    public async Task A_waiting_file_found_infected_later_is_removed_and_shown_as_such()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        TestVirusScanner.Down = true;
        DocumentDto doc;
        try { doc = await Uploaded(f.Partner, client.Id, Pdf(TestVirusScanner.Eicar), "bad.pdf"); }
        finally { TestVirusScanner.Down = false; }

        var r = (await (await f.Partner.PostAsync("/api/documents/maintenance", null)).Content.ReadFromJsonAsync<MaintenanceResult>())!;
        Assert.True(r.Infected >= 1);
        var d = await Doc(f.Partner, doc.Id);
        Assert.Equal(("infected", "infected"), (d.ScanStatus, d.PurgeReason));
        Assert.Equal(HttpStatusCode.Gone, (await f.Partner.GetAsync($"/api/documents/{doc.Id}/download")).StatusCode);
        Assert.Contains((await f.Partner.GetFromJsonAsync<DocumentPage>($"/api/clients/{client.Id}/documents"))!.Items, x => x.Id == doc.Id);
    }

    // ------------------------------------------------------------ trash and retention

    [Fact]
    public async Task Deleted_files_go_to_the_trash_and_are_removed_after_the_trash_days()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var doc = await Uploaded(f.Partner, client.Id, Pdf(), "draft.pdf");

        Assert.Equal(HttpStatusCode.NoContent, (await f.Partner.DeleteAsync($"/api/documents/{doc.Id}")).StatusCode);
        Assert.Equal(0, (await f.Partner.GetFromJsonAsync<DocumentPage>($"/api/clients/{client.Id}/documents"))!.Total);
        Assert.Single((await f.Partner.GetFromJsonAsync<DocumentPage>($"/api/clients/{client.Id}/documents?trash=true"))!.Items);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.GetAsync($"/api/documents/{doc.Id}/download")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await f.Partner.PostAsync($"/api/documents/{doc.Id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Partner.GetAsync($"/api/documents/{doc.Id}/download")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await f.Partner.DeleteAsync($"/api/documents/{doc.Id}")).StatusCode);
        await f.Partner.PostAsync("/api/documents/maintenance", null);
        Assert.Null((await Doc(f.Partner, doc.Id)).PurgedAt);                                     // not yet: 30 days

        await Sql("update documents set deleted_at = now() - interval '31 days' where id = @id", doc.Id);
        var r = (await (await f.Partner.PostAsync("/api/documents/maintenance", null)).Content.ReadFromJsonAsync<MaintenanceResult>())!;
        Assert.Equal(1, r.TrashPurged);
        var purged = await Doc(f.Partner, doc.Id);
        Assert.Equal("trash", purged.PurgeReason);                                                // the row stays as a record
        Assert.Equal(HttpStatusCode.Gone, (await f.Partner.PostAsync($"/api/documents/{doc.Id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await f.Partner.GetAsync($"/api/documents/{doc.Id}/download")).StatusCode);
    }

    [Fact]
    public async Task Files_past_retention_are_listed_for_a_person_to_remove_and_legal_hold_protects_them()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var old = await Uploaded(f.Partner, client.Id, Pdf(), "ITR 2010.pdf", new() { ["folder"] = "financials", ["fy"] = "2010-11" });
        var held = await Uploaded(f.Partner, client.Id, Pdf(), "Assessment 2010.pdf", new() { ["folder"] = "notices", ["fy"] = "2010-11" });
        var recent = await Uploaded(f.Partner, client.Id, Pdf(), "This year.pdf", new() { ["folder"] = "bank" });
        Assert.Equal(HttpStatusCode.OK, (await f.Partner.PutAsJsonAsync($"/api/documents/{held.Id}/hold", new { hold = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.DeleteAsync($"/api/documents/{held.Id}")).StatusCode);

        var due = (await f.Partner.GetFromJsonAsync<RetentionPage>("/api/documents/retention"))!;
        Assert.Equal(8, due.RetentionYears);
        var item = Assert.Single(due.Items);
        Assert.Equal(old.Id, item.Id);
        Assert.Equal(new DateOnly(2019, 3, 31), item.KeepUntil);                                  // FY 2010-11 ends 31 Mar 2011, + 8 years

        var purge = await f.Partner.PostAsJsonAsync("/api/documents/retention/purge", new { ids = new[] { old.Id, held.Id, recent.Id } });
        Assert.Equal(1, (await purge.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("removed").GetInt32());   // only the one really due
        Assert.Equal(HttpStatusCode.Gone, (await f.Partner.GetAsync($"/api/documents/{old.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Partner.GetAsync($"/api/documents/{held.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Partner.GetAsync($"/api/documents/{recent.Id}/download")).StatusCode);
    }

    [Fact]
    public async Task Staff_who_can_upload_cannot_delete_or_hold()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var doc = await Uploaded(f.Partner, client.Id, Pdf(), "kyc.pdf");
        var article = api.WithToken(api.Services.GetRequiredService<TokenIssuer>().Issue(f.User.UserId, f.User.FirmId, ["Article"],
            ["clients.read", "documents.read", "documents.upload", "tasks.read"]));

        await Uploaded(article, client.Id, Pdf(), "article upload.pdf");
        Assert.Equal(HttpStatusCode.OK, (await article.GetAsync($"/api/documents/{doc.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await article.DeleteAsync($"/api/documents/{doc.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await article.PutAsJsonAsync($"/api/documents/{doc.Id}/hold", new { hold = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await article.GetAsync("/api/documents/retention")).StatusCode);
    }

    // ------------------------------------------------------------ isolation

    [Fact]
    public async Task Another_firm_cannot_see_list_or_download_documents()
    {
        var a = await NewFirm();
        var b = await NewFirm();
        var client = await NewClient(a.Partner);
        var doc = await Uploaded(a.Partner, client.Id, Pdf(), "secret.pdf");
        var link = (await (await a.Partner.PostAsJsonAsync($"/api/clients/{client.Id}/upload-links", new CreateUploadLinkRequest()))
            .Content.ReadFromJsonAsync<UploadLinkCreated>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await b.Partner.GetAsync($"/api/documents/{doc.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Partner.GetAsync($"/api/documents/{doc.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Partner.GetAsync($"/api/clients/{client.Id}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Partner.DeleteAsync($"/api/documents/{doc.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(b.Partner, client.Id, Pdf(), "x.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Partner.PostAsync($"/api/upload-links/{link.Link.Id}/revoke", null)).StatusCode);
        Assert.DoesNotContain(doc.Id.ToString(), await b.Partner.GetStringAsync("/api/documents/retention"));
    }

    // ------------------------------------------------------------ client upload links

    [Fact]
    public async Task A_client_uploads_with_a_link_and_no_account_until_it_is_full_or_revoked()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var created = await f.Partner.PostAsJsonAsync($"/api/clients/{client.Id}/upload-links",
            new CreateUploadLinkRequest("Please send your bank statements for April", "bank", null, "2026-27", null, 7, 2));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var link = (await created.Content.ReadFromJsonAsync<UploadLinkCreated>())!;
        Assert.True(link.Token.Length >= 40);
        Assert.Equal("active", link.Link.Status);

        var anon = api.Raw();
        var info = (await anon.GetFromJsonAsync<PortalInfo>($"/api/portal/uploads/{link.Token}"))!;
        Assert.Equal("Anand Textiles", info.ClientName);
        Assert.Equal("Please send your bank statements for April", info.Message);
        Assert.Equal(2, info.Remaining);

        var sent = await anon.PostAsync($"/api/portal/uploads/{link.Token}", Form(Pdf(), "April.pdf"));
        Assert.True(sent.StatusCode == HttpStatusCode.OK, await sent.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync($"/api/portal/uploads/{link.Token}",
            Form(Encoding.ASCII.GetBytes("<html/>"), "May.pdf"))).StatusCode);                    // same checks as staff uploads
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsync($"/api/portal/uploads/{link.Token}", Form(Pdf(), "May.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await anon.PostAsync($"/api/portal/uploads/{link.Token}", Form(Pdf(), "June.pdf"))).StatusCode);

        var docs = (await f.Partner.GetFromJsonAsync<DocumentPage>($"/api/clients/{client.Id}/documents?fy=2026-27&folder=bank"))!.Items;
        Assert.Equal(2, docs.Count);
        Assert.All(docs, d => Assert.Equal(("portal", "Client (upload link)"), (d.Source, d.UploadedByName)));
        Assert.Equal(2, (await anon.GetFromJsonAsync<PortalInfo>($"/api/portal/uploads/{link.Token}"))!.Received.Count);

        var audit = await f.Partner.GetStringAsync($"/api/audit-logs?entity=documents&entityId={docs[0].Id}");
        Assert.Contains("client_user", audit);                                                     // the audit log shows the client did it

        var second = (await (await f.Partner.PostAsJsonAsync($"/api/clients/{client.Id}/upload-links", new CreateUploadLinkRequest()))
            .Content.ReadFromJsonAsync<UploadLinkCreated>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await f.Partner.PostAsync($"/api/upload-links/{second.Link.Id}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await anon.GetAsync($"/api/portal/uploads/{second.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await anon.GetAsync($"/api/portal/uploads/not-a-real-token")).StatusCode);
        Assert.Equal("revoked", (await f.Partner.GetFromJsonAsync<List<UploadLinkDto>>($"/api/clients/{client.Id}/upload-links"))!
            .Single(l => l.Id == second.Link.Id).Status);
    }

    [Fact]
    public async Task Upload_links_follow_the_client_setting_and_expire()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var link = (await (await f.Partner.PostAsJsonAsync($"/api/clients/{client.Id}/upload-links", new CreateUploadLinkRequest()))
            .Content.ReadFromJsonAsync<UploadLinkCreated>())!;
        var anon = api.Raw();

        Assert.Equal(HttpStatusCode.NoContent, (await Set(f.Partner, "doc.client_upload_enabled", "false", "client", client.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await anon.GetAsync($"/api/portal/uploads/{link.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.PostAsJsonAsync($"/api/clients/{client.Id}/upload-links",
            new CreateUploadLinkRequest())).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await f.Partner.DeleteAsync($"/api/settings/doc.client_upload_enabled?scope=client&scopeId={client.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/portal/uploads/{link.Token}")).StatusCode);

        await Sql("update document_upload_links set expires_at = now() - interval '1 minute' where id = @id", link.Link.Id);
        Assert.Equal(HttpStatusCode.Gone, (await anon.GetAsync($"/api/portal/uploads/{link.Token}")).StatusCode);
    }

    // ------------------------------------------------------------ tasks

    [Fact]
    public async Task Documents_link_to_tasks_and_a_deadline_task_files_them_under_its_filing_and_year()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var other = await NewClient(f.Partner, "Bharat Traders", new() { ["PAN"] = "AAACB1234F" });

        var deadline = (await f.Partner.GetFromJsonAsync<JsonElement>($"/api/deadlines?clientId={client.Id}&take=500"))
            .GetProperty("items").EnumerateArray().First(d => d.GetProperty("typeCode").GetString() == "GSTR3B_M");
        var made = await f.Partner.PostAsync($"/api/deadlines/{deadline.GetProperty("id").GetGuid()}/task", null);
        var taskId = (await made.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("taskId").GetGuid();

        var up = await f.Partner.PostAsync($"/api/tasks/{taskId}/documents/upload", Form(Pdf(), "purchase register.pdf"));
        Assert.True(up.StatusCode == HttpStatusCode.Created, await up.Content.ReadAsStringAsync());
        var doc = (await up.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal("GSTR3B_M", doc.TypeCode);
        Assert.Equal(deadline.GetProperty("periodKey").GetString(), doc.PeriodKey);
        Assert.Equal(1, doc.TaskCount);

        // a document already in the vault can be linked too — but only the task's own client's
        var bank = await Uploaded(f.Partner, client.Id, Pdf(), "bank.pdf", new() { ["folder"] = "bank" });
        var foreign = await Uploaded(f.Partner, other.Id, Pdf(), "other.pdf");
        var linked = await f.Partner.PostAsJsonAsync($"/api/tasks/{taskId}/documents", new { documentId = bank.Id });
        Assert.Equal(2, (await linked.Content.ReadFromJsonAsync<List<DocumentDto>>())!.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Partner.PostAsJsonAsync($"/api/tasks/{taskId}/documents", new { documentId = foreign.Id })).StatusCode);

        var detail = (await f.Partner.GetFromJsonAsync<DocumentDetail>($"/api/documents/{bank.Id}"))!;
        Assert.Equal(taskId, Assert.Single(detail.Tasks).Id);
        var history = (await f.Partner.GetFromJsonAsync<TaskDetail>($"/api/tasks/{taskId}"))!.Comments.Select(c => c.Body).ToList();
        Assert.Contains("Document added: purchase register", history);
        Assert.Contains("Document linked: bank", history);

        var after = await f.Partner.DeleteAsync($"/api/tasks/{taskId}/documents/{bank.Id}");
        Assert.Single((await after.Content.ReadFromJsonAsync<List<DocumentDto>>())!);

        // a client's upload link for the task links what they send
        var link = (await (await f.Partner.PostAsJsonAsync($"/api/clients/{client.Id}/upload-links",
            new CreateUploadLinkRequest(TaskId: taskId))).Content.ReadFromJsonAsync<UploadLinkCreated>())!;
        Assert.Equal(HttpStatusCode.OK, (await api.Raw().PostAsync($"/api/portal/uploads/{link.Token}", Form(Pdf(), "invoices.pdf"))).StatusCode);
        var docs = (await f.Partner.GetFromJsonAsync<List<DocumentDto>>($"/api/tasks/{taskId}/documents"))!;
        Assert.Equal(2, docs.Count);
        Assert.Contains(docs, d => d.Source == "portal" && d.TypeCode == "GSTR3B_M");
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Partner.PostAsJsonAsync($"/api/clients/{other.Id}/upload-links",
            new CreateUploadLinkRequest(TaskId: taskId))).StatusCode);                              // task of another client
    }
}
