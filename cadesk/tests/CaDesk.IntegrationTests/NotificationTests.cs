using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Notifications;
using CaDesk.Infrastructure.Configuration;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 8: reminders queued from deadlines and tasks, never twice, quiet hours, opt-out, templates, retries, isolation.
/// Channels run in "Log" mode in tests: nothing leaves the machine; addresses containing "fail" fail.
/// </summary>
public class NotificationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> GstCompany = new() { ["PAN"] = "AAACA1234F", ["GSTIN"] = "32AAACA1234F1ZJ" };

    private static Task<HttpResponseMessage> Set(HttpClient c, string key, string json) =>
        c.PutAsJsonAsync($"/api/settings/{key}", new SaveSettingRequest(JsonDocument.Parse(json).RootElement.Clone(), null, null, null, "test"));

    /// <summary>A firm with reminders for every deadline in the next 60 days and no quiet hours, so tests don't depend on the date or time.</summary>
    private async Task<HttpClient> NewFirm() => (await NewFirmWithUser()).Client;

    private async Task<(HttpClient Client, Guid UserId)> NewFirmWithUser()
    {
        var u = await api.SignUpAsync("Remind Co");
        var c = api.WithToken(u.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await Set(c, "reminder.schedule_days", "[60]")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Set(c, "reminder.quiet_start", "\"00:00\"")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Set(c, "reminder.quiet_end", "\"00:00\"")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Set(c, "reminder.staff_digest_time", "\"00:00\"")).StatusCode);
        return (c, u.UserId);
    }

    private static async Task<ClientDto> NewClient(HttpClient c, string? email = "accounts@anand.example", Guid? assignedTo = null, Dictionary<string, string>? taxIds = null)
    {
        var res = await c.PostAsJsonAsync("/api/clients",
            new SaveClientRequest(null, "Anand Textiles", null, null, "Company", taxIds ?? GstCompany, email, null, null, null, AssignedUserId: assignedTo));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    private static async Task<RunResult> Run(HttpClient c)
    {
        var res = await c.PostAsync("/api/reminders/run", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<RunResult>())!;
    }

    private static async Task<List<NotificationDto>> Log(HttpClient c, string query = "") =>
        (await c.GetFromJsonAsync<NotificationPage>($"/api/notifications?take=200{query}"))!.Items.ToList();

    [Fact]
    public async Task Clients_get_one_reminder_per_deadline_and_never_twice()
    {
        var c = await NewFirm();
        var client = await NewClient(c);

        var first = await Run(c);
        Assert.True(first.Queued > 0);
        var log = await Log(c, $"&clientId={client.Id}");
        var reminders = log.Where(n => n.TemplateKey == "client.deadline_reminder").ToList();
        Assert.NotEmpty(reminders);
        Assert.All(reminders, n => Assert.Equal("sent", n.Status));
        Assert.All(reminders, n => Assert.Equal("accounts@anand.example", n.ToAddress));
        Assert.Equal(reminders.Count, reminders.Select(n => n.RelatedId).Distinct().Count());     // one per deadline
        Assert.Contains("Anand Textiles", reminders[0].Body);
        Assert.DoesNotContain("{{", reminders[0].Body);

        var again = await Run(c);
        Assert.Equal(0, again.Queued);
        Assert.Equal(log.Count, (await Log(c, $"&clientId={client.Id}")).Count);
    }

    [Fact]
    public async Task Contacts_who_opt_out_get_nothing_and_whatsapp_waits_for_set_up()
    {
        var c = await NewFirm();
        Assert.Equal(HttpStatusCode.NoContent, (await Set(c, "reminder.channels", "[\"email\",\"whatsapp\"]")).StatusCode);
        var client = await NewClient(c, email: null);
        await c.PostAsJsonAsync($"/api/clients/{client.Id}/contacts", new SaveContactRequest("Suresh", "Director", "suresh@anand.example", "098470 12345", true, true));
        await c.PostAsJsonAsync($"/api/clients/{client.Id}/contacts", new SaveContactRequest("Meera", "Accountant", "meera@anand.example", null, false, false));

        await Run(c);
        var log = await Log(c, $"&clientId={client.Id}");
        Assert.DoesNotContain(log, n => n.ToAddress == "meera@anand.example");
        Assert.Contains(log, n => n.Channel == "email" && n.ToAddress == "suresh@anand.example" && n.Status == "sent");
        // WhatsApp number normalised; not set up for this firm yet, so skipped with a reason
        var wa = log.Where(n => n.Channel == "whatsapp").ToList();
        Assert.NotEmpty(wa);
        Assert.All(wa, n => Assert.Equal("919847012345", n.ToAddress));
        Assert.All(wa, n => Assert.Equal("skipped", n.Status));
        Assert.Contains("not set up", wa[0].LastError);
    }

    [Fact]
    public async Task Quiet_hours_hold_messages_and_the_client_switch_turns_reminders_off()
    {
        var c = await NewFirm();
        Assert.Equal(HttpStatusCode.NoContent, (await Set(c, "reminder.quiet_end", "\"23:59\"")).StatusCode);   // quiet almost all day
        var client = await NewClient(c);
        await Run(c);
        var held = (await Log(c, $"&clientId={client.Id}")).Where(n => n.TemplateKey.StartsWith("client.")).ToList();
        Assert.NotEmpty(held);
        Assert.All(held, n => Assert.Equal("queued", n.Status));
        Assert.All(held, n => Assert.True(n.ScheduledAt > DateTime.UtcNow));

        var other = await NewClient(c, "other@client.example", taxIds: new() { ["PAN"] = "AAACB1234F" });
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync("/api/settings/reminder.client_enabled",
            new SaveSettingRequest(JsonDocument.Parse("false").RootElement.Clone(), "client", other.Id, null, "test"))).StatusCode);
        await Run(c);
        Assert.DoesNotContain(await Log(c, $"&clientId={other.Id}"), n => n.TemplateKey.StartsWith("client."));   // partner escalations still go out
    }

    [Fact]
    public async Task Failed_messages_retry_with_backoff_and_can_be_sent_again()
    {
        var c = await NewFirm();
        var client = await NewClient(c, "will-fail@client.example");
        await Run(c);
        var n = (await Log(c, $"&clientId={client.Id}")).First(x => x.TemplateKey == "client.deadline_reminder");
        Assert.Equal("queued", n.Status);                 // retry later
        Assert.Equal(1, n.Attempts);
        Assert.NotNull(n.LastError);
        Assert.True(n.ScheduledAt > DateTime.UtcNow);

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/notifications/{n.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/notifications/{n.Id}/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/notifications/{n.Id}/retry", null)).StatusCode);   // already queued
    }

    [Fact]
    public async Task Firm_templates_are_used_and_unknown_placeholders_refused()
    {
        var c = await NewFirm();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/notification-templates/client.deadline_reminder/email",
            new SaveMessageTemplateRequest("Hi {{contact}}", "Pay {{amount}}", null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync("/api/notification-templates/client.deadline_reminder/email",
            new SaveMessageTemplateRequest("Reminder: {{compliance}}", "Namaskaram {{contact}}, {{compliance}} for {{period}} is due {{days_left}}.", null, null))).StatusCode);

        var preview = (await (await c.PostAsJsonAsync("/api/notification-templates/client.deadline_reminder/preview",
            new PreviewRequest("Reminder: {{compliance}}", "Namaskaram {{contact}}"))).Content.ReadFromJsonAsync<PreviewResult>())!;
        Assert.Equal("Namaskaram Suresh Anand", preview.Body);

        var client = await NewClient(c);
        await Run(c);
        var n = (await Log(c, $"&clientId={client.Id}")).First(x => x.TemplateKey == "client.deadline_reminder");
        Assert.StartsWith("Namaskaram Anand Textiles", n.Body);
        Assert.StartsWith("Reminder: ", n.Subject);
    }

    [Fact]
    public async Task Staff_get_a_daily_summary_once_a_day()
    {
        var (c, me) = await NewFirmWithUser();
        await NewClient(c, assignedTo: me);          // its tasks are mine
        await Run(c);
        var digests = (await Log(c)).Where(n => n.TemplateKey == "staff.daily_digest").ToList();
        Assert.Single(digests);
        Assert.Contains("GSTR", digests[0].Body);
        await Run(c);
        Assert.Single((await Log(c)).Where(n => n.TemplateKey == "staff.daily_digest"));
    }

    [Fact]
    public async Task Another_firm_sees_none_of_it()
    {
        var a = await NewFirm();
        var b = await NewFirm();
        var client = await NewClient(a);
        await Run(a);
        var one = (await Log(a)).First();

        Assert.Empty(await Log(b));
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/notifications/{one.Id}/retry", null)).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<NotificationPage>($"/api/clients/{client.Id}/notifications"))!.Items);

        // the client's own history shows only what the client received, not staff emails about it
        var history = (await a.GetFromJsonAsync<NotificationPage>($"/api/clients/{client.Id}/notifications"))!.Items;
        Assert.All(history, n => Assert.StartsWith("client", n.RecipientType));
    }
}
