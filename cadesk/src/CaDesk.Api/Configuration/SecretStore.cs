using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Configuration;
using CaDesk.Infrastructure.Audit;
using CaDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Secret = CaDesk.Domain.Configuration.Secret;

namespace CaDesk.Api.Configuration;

public sealed record SecretInfo(string Name, DateTime CreatedAt, DateTime? RotatedAt);

/// <summary>
/// Firm secrets (API keys, passwords) encrypted with ASP.NET Data Protection before they reach the
/// database. Users can write and delete them, and see names and dates, but never read a value back.
/// </summary>
public sealed class SecretStore(AppDbContext db, IDataProtectionProvider dp, ITenantContext tenant, TimeProvider clock, AuditWriter audit) : ISecretStore
{
    public static readonly System.Text.RegularExpressions.Regex NameRule = new("^[a-z0-9_.-]{1,60}$");

    // Purpose includes the firm: ciphertext copied into another firm's row can't be decrypted there.
    private IDataProtector Protector => dp.CreateProtector("CaDesk.Secrets.v1", tenant.FirmId.ToString());

    public async Task SetAsync(string name, string plaintext, CancellationToken ct = default)
    {
        if (!NameRule.IsMatch(name)) throw new ArgumentException("Use 1-60 lowercase letters, digits, '.', '_' or '-'.", nameof(name));
        if (string.IsNullOrEmpty(plaintext) || plaintext.Length > 8000) throw new ArgumentException("Value must be 1-8000 characters.", nameof(plaintext));

        var cipher = Protector.Protect(plaintext);
        var row = await db.Secrets.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (row is null) db.Secrets.Add(new Secret { Name = name, Ciphertext = cipher });
        else
        {
            row.Ciphertext = cipher;
            row.RotatedAt = clock.GetUtcNow().UtcDateTime;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Server-side use only (e.g. calling an e-invoicing API). Never return this to a browser.</summary>
    public async Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        var row = await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Name == name, ct);
        return row is null ? null : Protector.Unprotect(row.Ciphertext);
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken ct = default)
    {
        // refuse while a setting still points at it
        var secretKeys = db.ConfigDefinitions.Where(d => d.ValueType == "secret_ref").Select(d => d.Key);
        var asJson = System.Text.Json.JsonSerializer.Serialize(name);
        var usedBy = await db.ConfigValues
            .Where(v => v.FirmId != null && secretKeys.Contains(v.Key) && v.Value == asJson)
            .Select(v => v.Key).FirstOrDefaultAsync(ct);
        if (usedBy is not null)
            throw new BusinessRuleException($"The setting '{usedBy}' still uses this secret. Change that setting first.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);   // delete and its audit row together
        var deleted = await db.Secrets.Where(s => s.Name == name).ExecuteDeleteAsync(ct) > 0;
        if (!deleted) return false;
        await audit.RecordAsync("secrets", name, AuditAction.Delete, new { Name = name }, null, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<bool> ExistsAsync(string name, CancellationToken ct = default) =>
        db.Secrets.AnyAsync(s => s.Name == name, ct);

    public async Task<IReadOnlyList<SecretInfo>> ListAsync(CancellationToken ct) =>
        await db.Secrets.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SecretInfo(s.Name, s.CreatedAt, s.RotatedAt)).ToListAsync(ct);
}
