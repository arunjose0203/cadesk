using CaDesk.Application.Abstractions;
using CaDesk.Domain.Legal;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Legal;

public sealed record LegalDocumentDto(string Document, string Title, string Version, string Url, bool Accepted, DateTime? AcceptedAt);
public sealed record LegalStatusDto(IReadOnlyList<LegalDocumentDto> Documents, IReadOnlyList<string> Pending);
public sealed record AcceptLegalRequest(IReadOnlyList<string> Documents);

/// <summary>
/// Terms, privacy notice and data processing terms (Sprint 71). Every user accepts the terms and the privacy
/// notice; the data processing terms are accepted once per firm, by someone who manages the firm's settings.
/// </summary>
public sealed class LegalService(IAppDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public static IReadOnlyList<LegalDocumentDto> Published() =>
        LegalDocuments.Current.Select(kv => new LegalDocumentDto(kv.Key, LegalDocuments.Titles[kv.Key], kv.Value,
            LegalDocuments.Paths[kv.Key], false, null)).ToList();

    public async Task<LegalStatusDto> StatusAsync(bool managesFirm, CancellationToken ct)
    {
        var userId = tenant.UserId ?? throw new BusinessRuleException("Sign in again.", 401);
        var mine = await db.LegalAcceptances.AsNoTracking()
            .Where(a => a.UserId == userId || a.Document == LegalDocuments.Dpa)
            .Select(a => new { a.UserId, a.Document, a.Version, a.AcceptedAt })
            .ToListAsync(ct);

        var docs = new List<LegalDocumentDto>();
        var pending = new List<string>();
        foreach (var (doc, version) in LegalDocuments.Current)
        {
            // the DPA binds the firm: any acceptance of the current version counts
            var hit = mine.Where(a => a.Document == doc && a.Version == version && (doc == LegalDocuments.Dpa || a.UserId == userId))
                .OrderBy(a => a.AcceptedAt).FirstOrDefault();
            docs.Add(new LegalDocumentDto(doc, LegalDocuments.Titles[doc], version, LegalDocuments.Paths[doc], hit is not null, hit?.AcceptedAt));
            if (hit is null && (doc != LegalDocuments.Dpa || managesFirm)) pending.Add(doc);
        }
        return new LegalStatusDto(docs, pending);
    }

    public async Task<LegalStatusDto> AcceptAsync(AcceptLegalRequest r, bool managesFirm, string? ip, string? userAgent, CancellationToken ct)
    {
        var userId = tenant.UserId ?? throw new BusinessRuleException("Sign in again.", 401);
        var wanted = (r.Documents ?? Array.Empty<string>()).Select(d => d.Trim().ToLowerInvariant()).Distinct().ToList();
        if (wanted.Count == 0) throw new RequestValidationException("documents", "Choose what you accept.");
        foreach (var d in wanted)
        {
            if (!LegalDocuments.Current.ContainsKey(d)) throw new RequestValidationException("documents", $"Unknown document: {d}.");
            if (d == LegalDocuments.Dpa && !managesFirm)
                throw new BusinessRuleException("The data processing terms are accepted for the firm by someone who manages its settings.", 403);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var d in wanted)
        {
            var version = LegalDocuments.Current[d];
            if (await db.LegalAcceptances.AnyAsync(a => a.UserId == userId && a.Document == d && a.Version == version, ct)) continue;
            db.LegalAcceptances.Add(new LegalAcceptance
            {
                FirmId = tenant.FirmId, UserId = userId, Document = d, Version = version, AcceptedAt = now,
                IpAddress = ip is { Length: > 64 } ? ip[..64] : ip,
                UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent,
            });
        }
        await db.SaveChangesAsync(ct);
        return await StatusAsync(managesFirm, ct);
    }
}
