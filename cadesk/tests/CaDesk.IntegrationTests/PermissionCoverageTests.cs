using System.Reflection;
using CaDesk.Domain.Users;
using Npgsql;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Guards the permission plumbing that new desks keep forgetting: every permission an endpoint can require
/// (the Perms constants) must exist in the database, and every firm's built-in Partner role must hold all of
/// them. That includes firms signed up before the desk shipped, which a fresh-firm test never sees.
/// </summary>
public class PermissionCoverageTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly string[] CodePermissions = typeof(Perms)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Distinct()
        .Order()
        .ToArray();

    private async Task<List<string>> QueryAsync(string sql)
    {
        _ = api.Server; // applies migrations
        await using var conn = new NpgsqlConnection(ApiFactory.OwnerCs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await r.ReadAsync()) rows.Add(r.GetString(0));
        return rows;
    }

    [Fact]
    public async Task Every_permission_used_in_code_is_registered_in_the_database()
    {
        var registered = (await QueryAsync("select code from permissions")).ToHashSet();
        var missing = CodePermissions.Where(p => !registered.Contains(p)).ToList();
        Assert.True(missing.Count == 0, "Add these to a migration's permissions insert: " + string.Join(", ", missing));
    }

    [Fact]
    public async Task Every_firms_Partner_role_holds_every_permission()
    {
        var gaps = await QueryAsync("""
            select r.firm_id::text || ' lacks ' || p.code
            from roles r cross join permissions p
            where r.name = 'Partner' and r.is_system
              and not exists (select 1 from role_permissions rp where rp.role_id = r.id and rp.permission_code = p.code)
            order by 1 limit 20
            """);
        Assert.True(gaps.Count == 0,
            "A migration added permissions without granting them to existing Partner roles:\n" + string.Join("\n", gaps));
    }

    [Fact]
    public void Test_token_permission_list_matches_the_code()
    {
        // ApiFactory.ClientFor issues tokens with TestData.AllPermissions; a stale list silently skips new desks
        var missing = CodePermissions.Except(TestData.AllPermissions).ToList();
        Assert.True(missing.Count == 0, "Add to TestData.AllPermissions: " + string.Join(", ", missing));
    }
}
