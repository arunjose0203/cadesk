using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaDesk.Api.Auth;
using CaDesk.Api.Endpoints;
using CaDesk.Api.Users;
using CaDesk.Application.Clients;
using CaDesk.Application.Compliance;
using CaDesk.Application.Tasks;
using CaDesk.Infrastructure.Configuration;

namespace CaDesk.IntegrationTests;

/// <summary>Sprint 7: tasks from deadlines, checklists, review with separation of duties, ad-hoc tasks, workload, isolation.</summary>
public class TaskTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> GstCompany = new() { ["PAN"] = "AAACA1234F", ["GSTIN"] = "32AAACA1234F1ZJ" };

    private sealed record Firm(HttpClient Partner, Guid PartnerId);

    private async Task<Firm> NewFirm()
    {
        var u = await api.SignUpAsync("Task Co");
        return new Firm(api.WithToken(u.AccessToken), u.UserId);
    }

    /// <summary>Invites and signs in a second user with the Manager role (can review).</summary>
    private async Task<(HttpClient Client, Guid Id)> AddManager(HttpClient partner)
    {
        var role = (await partner.GetFromJsonAsync<List<RoleItem>>("/api/roles"))!.Single(r => r.Name == "Manager").Id;
        var invite = (await (await partner.PostAsJsonAsync("/api/invitations",
            new InviteRequest($"{Guid.NewGuid():N}@mgr.example", null, role))).Content.ReadFromJsonAsync<InviteCreated>())!;
        var res = await api.Raw().PostAsJsonAsync("/api/auth/accept-invite", new AcceptInviteRequest(invite.InviteToken, "Meena Manager", "Manager-password-77"));
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResult>())!;
        return (api.WithToken(auth.AccessToken!), auth.User!.Id);
    }

    private static async Task<ClientDto> NewClient(HttpClient c, Guid? assignedTo = null)
    {
        var res = await c.PostAsJsonAsync("/api/clients",
            new SaveClientRequest(null, "GST client", null, null, "Company", GstCompany, null, null, null, null, AssignedUserId: assignedTo));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    private static async Task<List<TaskListItem>> Tasks(HttpClient c, string query) =>
        (await c.GetFromJsonAsync<TaskPage>($"/api/tasks?{query}"))!.Items.ToList();

    private static async Task<TaskDetail> Detail(HttpClient c, Guid id) => (await c.GetFromJsonAsync<TaskDetail>($"/api/tasks/{id}"))!;

    private static async Task<TaskDetail> Ok(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<TaskDetail>())!;
    }

    private static async Task TickAll(HttpClient c, TaskDetail t)
    {
        foreach (var i in t.Checklist.Where(i => !i.IsDone))
            await Ok(await c.PutAsJsonAsync($"/api/tasks/{t.Task.Id}/checklist/{i.Id}", new ChecklistItemRequest(true, null)));
    }

    private async Task<TaskListItem> FirstGstTask(HttpClient c, Guid clientId) =>
        (await Tasks(c, $"clientId={clientId}&assignee=all")).Where(t => t.Title.StartsWith("GSTR-3B")).OrderBy(t => t.DueDate).First();

    [Fact]
    public async Task Close_deadlines_get_a_task_with_checklist_for_the_clients_staff()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner, f.PartnerId);

        var mine = await Tasks(f.Partner, "assignee=me");
        var t = mine.Where(x => x.ClientId == client.Id && x.Title.StartsWith("GSTR-3B (monthly) · ")).OrderBy(x => x.DueDate).First();
        Assert.True(t.ReviewRequired);                 // firm default
        Assert.Equal(6, t.ChecklistTotal);             // GSTR-3B default checklist
        Assert.NotNull(t.DeadlineId);
        // nothing far in the future yet (default: tasks 30 days before the due date)
        Assert.DoesNotContain(await Tasks(f.Partner, "assignee=all&status=all"), x => x.DueDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(31)));
    }

    [Fact]
    public async Task Review_flow_needs_a_second_person_and_files_the_deadline_on_approval()
    {
        var f = await NewFirm();
        var (manager, managerId) = await AddManager(f.Partner);
        var client = await NewClient(f.Partner, f.PartnerId);
        var item = await FirstGstTask(f.Partner, client.Id);
        var id = item.Id;

        // can't finish with unticked steps
        Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.PostAsJsonAsync($"/api/tasks/{id}/complete",
            new CompleteTaskRequest(null, "AA3210261234567", null, null))).StatusCode);
        await TickAll(f.Partner, await Detail(f.Partner, id));

        var submitted = await Ok(await f.Partner.PostAsJsonAsync($"/api/tasks/{id}/complete",
            new CompleteTaskRequest(null, "AA3210261234567", "Filed after client OK", null)));
        Assert.Equal("in_review", submitted.Task.Status);
        Assert.NotEqual("filed", submitted.Deadline!.Status);

        // filing straight on the deadline would skip the review
        Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.PutAsJsonAsync($"/api/deadlines/{item.DeadlineId}",
            new UpdateDeadlineRequest("filed", null, null, null, null, null))).StatusCode);

        // the person who did the work can't approve it while someone else can
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Partner.PostAsJsonAsync($"/api/tasks/{id}/review",
            new ReviewTaskRequest(true, null, null))).StatusCode);
        Assert.Contains(await Tasks(manager, "toReview=true&assignee=all"), x => x.Id == id);

        // sending back needs a reason
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync($"/api/tasks/{id}/review",
            new ReviewTaskRequest(false, null, null))).StatusCode);
        var back = await Ok(await manager.PostAsJsonAsync($"/api/tasks/{id}/review", new ReviewTaskRequest(false, "ITC for Oct is missing one bill", null)));
        Assert.Equal("changes_requested", back.Task.Status);

        await Ok(await f.Partner.PostAsJsonAsync($"/api/tasks/{id}/complete", new CompleteTaskRequest(null, "AA3210261234567", null, null)));
        var done = await Ok(await manager.PostAsJsonAsync($"/api/tasks/{id}/review", new ReviewTaskRequest(true, "Looks right", null)));
        Assert.Equal("done", done.Task.Status);
        Assert.Equal("filed", done.Deadline!.Status);
        Assert.Contains(done.Comments, c => c.Kind == "review" && c.Body.StartsWith("Sent back"));
        Assert.Contains(done.Comments, c => c.Kind == "review" && c.Body.StartsWith("Approved"));

        var deadline = (await f.Partner.GetFromJsonAsync<DeadlinePage>($"/api/deadlines?clientId={client.Id}&status=filed"))!.Items.Single();
        Assert.Equal("AA3210261234567", deadline.AckNo);
        _ = managerId;
    }

    [Fact]
    public async Task A_far_deadline_cant_be_filed_around_review_and_its_task_can_be_made_early()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner, f.PartnerId);
        var far = (await f.Partner.GetFromJsonAsync<DeadlinePage>($"/api/deadlines?clientId={client.Id}&take=500"))!.Items
            .Where(d => d.TypeCode == "ITR_AUDIT").OrderBy(d => d.DueDate).Last();   // FY 2026-27, due Oct 2027: no task yet; period started

        Assert.Equal(HttpStatusCode.Conflict, (await f.Partner.PutAsJsonAsync($"/api/deadlines/{far.Id}",
            new UpdateDeadlineRequest("filed", null, null, null, null, null))).StatusCode);

        var made = await f.Partner.PostAsync($"/api/deadlines/{far.Id}/task", null);
        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        var again = await f.Partner.PostAsync($"/api/deadlines/{far.Id}/task", null);   // same task, not a second one
        Assert.Equal(await made.Content.ReadAsStringAsync(), await again.Content.ReadAsStringAsync());

        // review can't be switched off on a filing's task
        var taskId = (await made.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("taskId").GetGuid();
        var t = await Detail(f.Partner, taskId);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Partner.PutAsJsonAsync($"/api/tasks/{taskId}", new SaveTaskRequest(
            t.Task.Title, null, client.Id, t.Task.DueDate, null, f.PartnerId, null, false, null, null))).StatusCode);
    }

    [Fact]
    public async Task A_sole_reviewer_can_approve_their_own_work()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner, f.PartnerId);
        var id = (await FirstGstTask(f.Partner, client.Id)).Id;
        await TickAll(f.Partner, await Detail(f.Partner, id));
        await Ok(await f.Partner.PostAsJsonAsync($"/api/tasks/{id}/complete", new CompleteTaskRequest(null, null, null, null)));
        Assert.Equal("done", (await Ok(await f.Partner.PostAsJsonAsync($"/api/tasks/{id}/review", new ReviewTaskRequest(true, null, null)))).Task.Status);
    }

    [Fact]
    public async Task Ad_hoc_tasks_with_their_own_checklist()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner);
        var created = await f.Partner.PostAsJsonAsync("/api/tasks", new SaveTaskRequest("Reply to GST notice", "ASMT-10 for FY 2024-25",
            client.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), "high", f.PartnerId, null, false,
            ["Read the notice", "Collect reconciliation", "Draft reply"], null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var t = (await created.Content.ReadFromJsonAsync<TaskDetail>())!;
        Assert.Equal(3, t.Checklist.Count);
        Assert.Null(t.Deadline);

        // a step that doesn't apply can be removed; then tick the rest and finish (no review on this task)
        await Ok(await f.Partner.DeleteAsync($"/api/tasks/{t.Task.Id}/checklist/{t.Checklist[1].Id}"));
        await TickAll(f.Partner, await Detail(f.Partner, t.Task.Id));
        var done = await Ok(await f.Partner.PostAsJsonAsync($"/api/tasks/{t.Task.Id}/complete", new CompleteTaskRequest(null, null, "Reply uploaded", null)));
        Assert.Equal("done", done.Task.Status);

        Assert.Equal(HttpStatusCode.BadRequest, (await f.Partner.PostAsJsonAsync("/api/tasks",
            new SaveTaskRequest("", null, null, null, "urgent", null, null, null, null, null))).StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_deadline_task_marks_the_deadline_not_needed()
    {
        var f = await NewFirm();
        var client = await NewClient(f.Partner, f.PartnerId);
        var item = await FirstGstTask(f.Partner, client.Id);
        var cancelled = await Ok(await f.Partner.PostAsJsonAsync($"/api/tasks/{item.Id}/cancel", new CancelTaskRequest("Nil return filed by client", null)));
        Assert.Equal("cancelled", cancelled.Task.Status);
        Assert.Equal("not_applicable", cancelled.Deadline!.Status);
    }

    [Fact]
    public async Task Workload_counts_each_persons_open_work()
    {
        var f = await NewFirm();
        await NewClient(f.Partner, f.PartnerId);
        var rows = (await f.Partner.GetFromJsonAsync<List<WorkloadRow>>("/api/tasks/workload"))!;
        var me = rows.Single(r => r.UserId == f.PartnerId);
        Assert.True(me.Open >= 2);                     // at least GSTR-1 and GSTR-3B for the month
        Assert.True(me.Overdue + me.DueThisWeek <= me.Open);
    }

    [Fact]
    public async Task A_firm_checklist_replaces_the_default_for_new_tasks()
    {
        var f = await NewFirm();
        var saved = await f.Partner.PutAsJsonAsync("/api/task-templates/GSTR3B_M",
            new SaveTemplateRequest("Our GSTR-3B", ["Download 2B", "Prepare working", "Partner approval", "File"]));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var client = await NewClient(f.Partner, f.PartnerId);
        Assert.Equal(4, (await FirstGstTask(f.Partner, client.Id)).ChecklistTotal);
        Assert.Contains(await f.Partner.GetFromJsonAsync<List<TemplateDto>>("/api/task-templates") ?? [], x => x.TypeCode == "GSTR3B_M" && x.FirmOwn);
    }

    [Fact]
    public async Task Another_firm_cannot_see_or_touch_tasks()
    {
        var a = await NewFirm();
        var b = await NewFirm();
        var client = await NewClient(a.Partner, a.PartnerId);
        var id = (await FirstGstTask(a.Partner, client.Id)).Id;

        Assert.Empty(await Tasks(b.Partner, "assignee=all&status=all"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.Partner.GetAsync($"/api/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Partner.PostAsJsonAsync($"/api/tasks/{id}/comments", new CommentRequest("hi"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Partner.PostAsJsonAsync("/api/tasks",
            new SaveTaskRequest("Sneaky", null, client.Id, null, null, null, null, null, null, null))).StatusCode);
    }
}
