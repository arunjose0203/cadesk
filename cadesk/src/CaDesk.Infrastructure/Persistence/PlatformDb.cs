using Npgsql;

namespace CaDesk.Infrastructure.Persistence;

/// <summary>
/// Platform-level operations that are not inside one firm: creating a firm with its first user.
/// Uses the OWNER connection, so it is not limited by row-level security.
/// Keep this class small; never expose it to firm users directly.
/// </summary>
public sealed class PlatformDb(DatabaseOptions options)
{
    /// <summary>
    /// Creates the firm, its roles from the country's setup template, and the first user as Partner.
    /// All in one transaction (platform_create_firm in 0002_identity.sql).
    /// </summary>
    public async Task<(Guid FirmId, Guid UserId)> SignUpAsync(
        string firmName, string countryCode, string userName, string email, string passwordHash,
        string? setupTemplate = null, CancellationToken ct = default)
    {
        var firmId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var country = countryCode.Trim().ToUpperInvariant();

        await using var conn = new NpgsqlConnection(options.OwnerConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select platform_create_firm(@firm, @firmName, @country, @template, @user, @userName, @email::citext, @hash)", conn);
        cmd.Parameters.AddWithValue("firm", firmId);
        cmd.Parameters.AddWithValue("firmName", firmName.Trim());
        cmd.Parameters.AddWithValue("country", country);
        cmd.Parameters.AddWithValue("template", setupTemplate ?? DefaultTemplate(country));
        cmd.Parameters.AddWithValue("user", userId);
        cmd.Parameters.AddWithValue("userName", userName.Trim());
        cmd.Parameters.AddWithValue("email", email.Trim());
        cmd.Parameters.AddWithValue("hash", passwordHash);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidParameterValue)
        {
            throw new ArgumentException(ex.MessageText, nameof(countryCode), ex);
        }
        return (firmId, userId);
    }

    // More templates arrive with the country packs (UAE, Saudi Arabia ...).
    private static string DefaultTemplate(string country) => country switch
    {
        "IN" => "IN_CA_FIRM",
        _ => throw new ArgumentException($"Sign-up is not open for country '{country}' yet.", nameof(country)),
    };

    /// <summary>Firms that background jobs should work for (trial or active).</summary>
    public async Task<IReadOnlyList<Guid>> ActiveFirmIdsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(options.OwnerConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("select id from firms where status in ('trial','active') order by id", conn);
        var ids = new List<Guid>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) ids.Add(r.GetGuid(0));
        return ids;
    }
}
