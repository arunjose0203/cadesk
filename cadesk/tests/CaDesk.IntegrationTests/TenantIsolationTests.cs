using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using Npgsql;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 1 "done when": Firm A can never read or change Firm B's data,
/// through the API (lock 1, EF filter) or with direct SQL (lock 2, row-level security).
/// </summary>
public class TenantIsolationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static SaveClientRequest NewClient(string name) => new(
        Code: $"C-{Guid.NewGuid():N}"[..12], Name: name, LegalName: null, CountryCode: "IN",
        EntityType: "Individual", TaxIds: new() { ["PAN"] = "ABCDE1234F" },
        Email: null, Phone: null, IsActive: true, Version: null);

    private async Task<ClientDto> CreateAsFirmA(string name = "Client of A")
    {
        var res = await api.ClientFor(api.FirmA.FirmId).PostAsJsonAsync("/api/clients", NewClient(name));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var res = await api.CreateClient().GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Token_without_firm_is_forbidden()
    {
        var res = await api.ClientFor(firmId: null).GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Firm_A_sees_its_own_client()
    {
        var created = await CreateAsFirmA();
        var a = api.ClientFor(api.FirmA.FirmId);

        var list = (await a.GetFromJsonAsync<ClientPage>("/api/clients?take=200"))!.Items;
        Assert.Contains(list!, c => c.Id == created.Id);
        Assert.Equal("ABCDE1234F", created.TaxIds["PAN"]);
    }

    [Fact]
    public async Task Firm_B_cannot_list_get_update_or_delete_firm_A_client()
    {
        var created = await CreateAsFirmA();
        var b = api.ClientFor(api.FirmB.FirmId);

        var list = (await b.GetFromJsonAsync<ClientPage>("/api/clients?take=200"))!.Items;
        Assert.DoesNotContain(list!, c => c.Id == created.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/clients/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PutAsJsonAsync($"/api/clients/{created.Id}", NewClient("hacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/clients/{created.Id}")).StatusCode);

        // and Firm A's client is unchanged
        var again = await api.ClientFor(api.FirmA.FirmId).GetFromJsonAsync<ClientDto>($"/api/clients/{created.Id}");
        Assert.Equal("Client of A", again!.Name);
    }

    [Fact]
    public async Task Same_client_code_is_allowed_in_different_firms_but_not_twice_in_one()
    {
        var req = NewClient("Shared code");
        var a = api.ClientFor(api.FirmA.FirmId);
        Assert.Equal(HttpStatusCode.Created, (await a.PostAsJsonAsync("/api/clients", req)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/clients", req)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await api.ClientFor(api.FirmB.FirmId).PostAsJsonAsync("/api/clients", req)).StatusCode);
    }

    [Fact]
    public async Task Deleted_client_disappears_from_lists()
    {
        var created = await CreateAsFirmA("To delete");
        var a = api.ClientFor(api.FirmA.FirmId);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/clients/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/clients/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Row_level_security_blocks_direct_sql_across_firms()
    {
        var created = await CreateAsFirmA();

        await using var conn = new NpgsqlConnection(ApiFactory.AppCs); // the restricted app role
        await conn.OpenAsync();

        async Task SetFirm(string value)
        {
            await using var set = new NpgsqlCommand("select set_config('app.firm_id', @f, false)", conn);
            set.Parameters.AddWithValue("f", value);
            await set.ExecuteNonQueryAsync();
        }

        async Task<long> CountClient()
        {
            await using var cmd = new NpgsqlCommand("select count(*) from clients where id = @id", conn);
            cmd.Parameters.AddWithValue("id", created.Id);
            return (long)(await cmd.ExecuteScalarAsync())!;
        }

        // no firm set: nothing visible
        await SetFirm("");
        Assert.Equal(0, await CountClient());

        // as Firm B: Firm A's row is invisible even without any WHERE on firm_id
        await SetFirm(api.FirmB.FirmId.ToString());
        Assert.Equal(0, await CountClient());

        // as Firm B: inserting a row that claims to belong to Firm A is refused by PostgreSQL
        await using (var insert = new NpgsqlCommand("""
            insert into clients (id, firm_id, code, name, country_code, entity_type)
            values (gen_random_uuid(), @a, 'HACK', 'hack', 'IN', 'Individual')
            """, conn))
        {
            insert.Parameters.AddWithValue("a", api.FirmA.FirmId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState); // insufficient_privilege / RLS violation
        }

        // as Firm A: visible
        await SetFirm(api.FirmA.FirmId.ToString());
        Assert.Equal(1, await CountClient());
    }
}
