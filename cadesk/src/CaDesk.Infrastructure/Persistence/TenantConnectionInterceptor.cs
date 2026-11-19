using System.Data.Common;
using CaDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CaDesk.Infrastructure.Persistence;

/// <summary>
/// Lock 2 of 2: every time EF opens a connection, tell PostgreSQL which firm this is.
/// Row-level security policies read app.firm_id, so even a buggy query cannot see
/// another firm's rows. With no firm set, the value is '' and every policy returns nothing.
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = CreateCommand(connection);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = CreateCommand(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "select set_config('app.firm_id', @firm, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "firm";
        p.Value = tenant.HasTenant ? tenant.FirmId.ToString() : "";
        cmd.Parameters.Add(p);
        return cmd;
    }
}
