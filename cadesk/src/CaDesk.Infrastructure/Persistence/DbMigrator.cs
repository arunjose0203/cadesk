using System.Reflection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CaDesk.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded SQL files in Persistence/Migrations in file-name order, once each.
/// Runs as the database OWNER role (the API itself runs as the restricted app role).
/// An advisory lock stops two API instances migrating at the same time.
/// </summary>
public sealed class DbMigrator(DatabaseOptions options, ILogger<DbMigrator> log)
{
    private const long LockKey = 7_270_001;

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(options.OwnerConnectionString);
        await conn.OpenAsync(ct);

        await Exec(conn, $"select pg_advisory_lock({LockKey})", ct);
        try
        {
            await Exec(conn, """
                create table if not exists schema_migrations (
                  id         text primary key,
                  applied_at timestamptz not null default now()
                )
                """, ct);

            var asm = typeof(DbMigrator).Assembly;
            var scripts = asm.GetManifestResourceNames()
                .Where(n => n.StartsWith("migrations/") && n.EndsWith(".sql"))
                .OrderBy(n => n, StringComparer.Ordinal);

            foreach (var name in scripts)
            {
                var id = name["migrations/".Length..];
                await using (var check = new NpgsqlCommand("select 1 from schema_migrations where id = @id", conn))
                {
                    check.Parameters.AddWithValue("id", id);
                    if (await check.ExecuteScalarAsync(ct) is not null) continue;
                }

                var sql = await ReadAsync(asm, name);
                await using var tx = await conn.BeginTransactionAsync(ct);
                await using (var run = new NpgsqlCommand(sql, conn, tx)) await run.ExecuteNonQueryAsync(ct);
                await using (var mark = new NpgsqlCommand("insert into schema_migrations (id) values (@id)", conn, tx))
                {
                    mark.Parameters.AddWithValue("id", id);
                    await mark.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                log.LogInformation("Applied migration {Migration}", id);
            }

            await EnsureAuditPartitionsAsync(conn, ct);
        }
        finally
        {
            await Exec(conn, $"select pg_advisory_unlock({LockKey})", CancellationToken.None);
        }
    }

    /// <summary>Monthly audit partitions for this month and the next three. Also run daily by the API.</summary>
    public async Task EnsureAuditPartitionsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(options.OwnerConnectionString);
        await conn.OpenAsync(ct);
        await EnsureAuditPartitionsAsync(conn, ct);
    }

    private static async Task EnsureAuditPartitionsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        await using var check = new NpgsqlCommand("select to_regproc('audit_ensure_partitions') is not null", conn);
        if (await check.ExecuteScalarAsync(ct) is true)
            await Exec(conn, "select audit_ensure_partitions(3)", ct);
    }

    private static async Task Exec(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<string> ReadAsync(Assembly asm, string name)
    {
        await using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return await r.ReadToEndAsync();
    }
}

public sealed class DatabaseOptions
{
    /// <summary>Restricted role used by the API; subject to row-level security.</summary>
    public required string AppConnectionString { get; init; }

    /// <summary>Owner role used only for migrations and platform operations (firm sign-up).</summary>
    public required string OwnerConnectionString { get; init; }

    public bool MigrateOnStartup { get; init; } = true;
}
