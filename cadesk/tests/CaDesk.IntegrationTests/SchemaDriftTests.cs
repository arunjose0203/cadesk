using CaDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Guards against drift between the EF model (C# entities) and the SQL migrations:
/// every table and column EF reads or writes must exist in the migrated database.
/// Without this, a renamed property or a navigation with no explicit foreign key
/// (EF then invents a shadow "xxx_user_id" column) only shows up as a 500 at run time.
/// </summary>
public class SchemaDriftTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Every_mapped_table_and_column_exists_in_the_database()
    {
        _ = api.Server; // applies migrations

        var dbColumns = new HashSet<(string Table, string Column)>();
        await using (var conn = new NpgsqlConnection(ApiFactory.OwnerCs))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "select table_name, column_name from information_schema.columns where table_schema = 'public'", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) dbColumns.Add((r.GetString(0), r.GetString(1)));
        }
        var dbTables = dbColumns.Select(c => c.Table).ToHashSet();

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var problems = new List<string>();
        foreach (var et in db.Model.GetEntityTypes())
        {
            var table = et.GetTableName();
            if (table is null || et.IsOwned()) continue;
            if (!dbTables.Contains(table))
            {
                problems.Add($"{et.ClrType.Name}: table '{table}' is missing");
                continue;
            }
            var store = StoreObjectIdentifier.Table(table, et.GetSchema());
            foreach (var p in et.GetProperties())
            {
                var column = p.GetColumnName(store);
                // xmin is a PostgreSQL system column (optimistic concurrency); information_schema doesn't list it
                if (column is null or "xmin") continue;
                if (!dbColumns.Contains((table, column)))
                    problems.Add($"{et.ClrType.Name}.{p.Name}{(p.IsShadowProperty() ? " (shadow)" : "")}: column '{table}.{column}' is missing");
            }
        }

        Assert.True(problems.Count == 0, "EF model and database disagree:\n" + string.Join("\n", problems.Order()));
    }
}
