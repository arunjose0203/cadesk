using CaDesk.Application.Abstractions;
using CaDesk.Domain.Engagements;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Engagements;

public sealed class EngagementService(IAppDbContext db, ITenantContext tenant) : IEngagementService
{
    public async Task<EngagementKycSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalLetters = await db.EngagementLetters.CountAsync(ct);
        var acceptedLetters = await db.EngagementLetters.CountAsync(l => l.Status == EngagementStatus.Accepted || l.Status == EngagementStatus.SignedDigitally, ct);
        var pendingLetters = await db.EngagementLetters.CountAsync(l => l.Status == EngagementStatus.Draft || l.Status == EngagementStatus.SentToClient, ct);
        var totalKyc = await db.ClientKycRecords.CountAsync(ct);
        var verifiedKyc = await db.ClientKycRecords.CountAsync(k => k.VerificationStatus == KycVerificationStatus.Verified, ct);
        var highRiskKyc = await db.ClientKycRecords.CountAsync(k => k.RiskRating == KycRiskRating.High || k.RiskRating == KycRiskRating.PoliticallyExposedPerson, ct);
        var totalContractedFees = await db.EngagementLetters.SumAsync(l => (decimal?)l.AgreedFeeAmount, ct) ?? 0m;

        return new EngagementKycSummaryDto(
            TotalLettersCount: totalLetters,
            AcceptedLettersCount: acceptedLetters,
            PendingLettersCount: pendingLetters,
            TotalKycRecordsCount: totalKyc,
            VerifiedKycCount: verifiedKyc,
            HighRiskKycCount: highRiskKyc,
            TotalContractedFeeValue: totalContractedFees
        );
    }

    public async Task<IReadOnlyList<EngagementLetterDto>> ListLettersAsync(
        Guid? clientId = null,
        string? type = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = db.EngagementLetters.Include(l => l.Client).AsQueryable();

        if (clientId.HasValue)
            q = q.Where(l => l.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(l => l.EngagementType == type.Trim());

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(l => l.Status == status.Trim());

        var items = await q.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);

        return items.Select(l => new EngagementLetterDto(
            Id: l.Id,
            ClientId: l.ClientId,
            ClientName: l.Client?.Name,
            LetterNumber: l.LetterNumber,
            EngagementType: l.EngagementType,
            FinancialYear: l.FinancialYear,
            ScopeOfWork: l.ScopeOfWork,
            ManagementResponsibilities: l.ManagementResponsibilities,
            AuditorResponsibilities: l.AuditorResponsibilities,
            FeeStructure: l.FeeStructure,
            AgreedFeeAmount: l.AgreedFeeAmount,
            OutOfPocketIncluded: l.OutOfPocketIncluded,
            Status: l.Status,
            SentDate: l.SentDate,
            AcceptedDate: l.AcceptedDate,
            ClientSignatoryName: l.ClientSignatoryName,
            ClientSignatoryDesignation: l.ClientSignatoryDesignation,
            AcceptanceIp: l.AcceptanceIp,
            LetterContent: l.LetterContent,
            CreatedAt: l.CreatedAt
        )).ToList();
    }

    public async Task<EngagementLetterDto?> GetLetterByIdAsync(Guid id, CancellationToken ct = default)
    {
        var l = await db.EngagementLetters
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (l is null) return null;

        return new EngagementLetterDto(
            Id: l.Id,
            ClientId: l.ClientId,
            ClientName: l.Client?.Name,
            LetterNumber: l.LetterNumber,
            EngagementType: l.EngagementType,
            FinancialYear: l.FinancialYear,
            ScopeOfWork: l.ScopeOfWork,
            ManagementResponsibilities: l.ManagementResponsibilities,
            AuditorResponsibilities: l.AuditorResponsibilities,
            FeeStructure: l.FeeStructure,
            AgreedFeeAmount: l.AgreedFeeAmount,
            OutOfPocketIncluded: l.OutOfPocketIncluded,
            Status: l.Status,
            SentDate: l.SentDate,
            AcceptedDate: l.AcceptedDate,
            ClientSignatoryName: l.ClientSignatoryName,
            ClientSignatoryDesignation: l.ClientSignatoryDesignation,
            AcceptanceIp: l.AcceptanceIp,
            LetterContent: l.LetterContent,
            CreatedAt: l.CreatedAt
        );
    }

    public async Task<EngagementLetterDto> CreateLetterAsync(CreateEngagementLetterRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {req.ClientId} not found.");

        var count = await db.EngagementLetters.CountAsync(l => l.FinancialYear == req.FinancialYear, ct);
        var letterNumber = $"EL/{req.FinancialYear}/{count + 1:D3}";

        var template = EngagementTemplateGenerator.GenerateTemplate(new GenerateTemplateRequest(
            EngagementType: req.EngagementType,
            ClientName: client.Name,
            FinancialYear: req.FinancialYear,
            FeeStructure: req.FeeStructure,
            AgreedFeeAmount: req.AgreedFeeAmount
        ));

        var entity = new EngagementLetter
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            LetterNumber = letterNumber,
            EngagementType = req.EngagementType ?? EngagementType.StatutoryAudit,
            FinancialYear = req.FinancialYear.Trim(),
            ScopeOfWork = string.IsNullOrWhiteSpace(req.ScopeOfWork) ? template.DefaultScope : req.ScopeOfWork.Trim(),
            ManagementResponsibilities = string.IsNullOrWhiteSpace(req.ManagementResponsibilities) ? template.DefaultManagementResponsibilities : req.ManagementResponsibilities.Trim(),
            AuditorResponsibilities = string.IsNullOrWhiteSpace(req.AuditorResponsibilities) ? template.DefaultAuditorResponsibilities : req.AuditorResponsibilities.Trim(),
            FeeStructure = req.FeeStructure ?? EngagementFeeStructure.FixedFee,
            AgreedFeeAmount = req.AgreedFeeAmount,
            OutOfPocketIncluded = req.OutOfPocketIncluded ?? false,
            Status = EngagementStatus.Draft,
            LetterContent = string.IsNullOrWhiteSpace(req.LetterContent) ? template.FullLetterContent : req.LetterContent.Trim()
        };

        db.EngagementLetters.Add(entity);
        await db.SaveChangesAsync(ct);

        return new EngagementLetterDto(
            Id: entity.Id,
            ClientId: entity.ClientId,
            ClientName: client.Name,
            LetterNumber: entity.LetterNumber,
            EngagementType: entity.EngagementType,
            FinancialYear: entity.FinancialYear,
            ScopeOfWork: entity.ScopeOfWork,
            ManagementResponsibilities: entity.ManagementResponsibilities,
            AuditorResponsibilities: entity.AuditorResponsibilities,
            FeeStructure: entity.FeeStructure,
            AgreedFeeAmount: entity.AgreedFeeAmount,
            OutOfPocketIncluded: entity.OutOfPocketIncluded,
            Status: entity.Status,
            SentDate: entity.SentDate,
            AcceptedDate: entity.AcceptedDate,
            ClientSignatoryName: entity.ClientSignatoryName,
            ClientSignatoryDesignation: entity.ClientSignatoryDesignation,
            AcceptanceIp: entity.AcceptanceIp,
            LetterContent: entity.LetterContent,
            CreatedAt: entity.CreatedAt
        );
    }

    public async Task<EngagementLetterDto> SendLetterAsync(Guid id, CancellationToken ct = default)
    {
        var letter = await db.EngagementLetters
            .Include(l => l.Client)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new InvalidOperationException($"Engagement letter {id} not found.");

        letter.Status = EngagementStatus.SentToClient;
        letter.SentDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new EngagementLetterDto(
            Id: letter.Id,
            ClientId: letter.ClientId,
            ClientName: letter.Client?.Name,
            LetterNumber: letter.LetterNumber,
            EngagementType: letter.EngagementType,
            FinancialYear: letter.FinancialYear,
            ScopeOfWork: letter.ScopeOfWork,
            ManagementResponsibilities: letter.ManagementResponsibilities,
            AuditorResponsibilities: letter.AuditorResponsibilities,
            FeeStructure: letter.FeeStructure,
            AgreedFeeAmount: letter.AgreedFeeAmount,
            OutOfPocketIncluded: letter.OutOfPocketIncluded,
            Status: letter.Status,
            SentDate: letter.SentDate,
            AcceptedDate: letter.AcceptedDate,
            ClientSignatoryName: letter.ClientSignatoryName,
            ClientSignatoryDesignation: letter.ClientSignatoryDesignation,
            AcceptanceIp: letter.AcceptanceIp,
            LetterContent: letter.LetterContent,
            CreatedAt: letter.CreatedAt
        );
    }

    public async Task<EngagementLetterDto> AcceptLetterAsync(Guid id, AcceptEngagementLetterRequest req, CancellationToken ct = default)
    {
        var letter = await db.EngagementLetters
            .Include(l => l.Client)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new InvalidOperationException($"Engagement letter {id} not found.");

        letter.Status = EngagementStatus.Accepted;
        letter.AcceptedDate = DateTime.UtcNow;
        letter.ClientSignatoryName = (req.SignatoryName ?? "Authorized Signatory").Trim();
        letter.ClientSignatoryDesignation = (req.SignatoryDesignation ?? "Director").Trim();
        letter.AcceptanceIp = (req.IpAddress ?? "127.0.0.1").Trim();

        await db.SaveChangesAsync(ct);

        return new EngagementLetterDto(
            Id: letter.Id,
            ClientId: letter.ClientId,
            ClientName: letter.Client?.Name,
            LetterNumber: letter.LetterNumber,
            EngagementType: letter.EngagementType,
            FinancialYear: letter.FinancialYear,
            ScopeOfWork: letter.ScopeOfWork,
            ManagementResponsibilities: letter.ManagementResponsibilities,
            AuditorResponsibilities: letter.AuditorResponsibilities,
            FeeStructure: letter.FeeStructure,
            AgreedFeeAmount: letter.AgreedFeeAmount,
            OutOfPocketIncluded: letter.OutOfPocketIncluded,
            Status: letter.Status,
            SentDate: letter.SentDate,
            AcceptedDate: letter.AcceptedDate,
            ClientSignatoryName: letter.ClientSignatoryName,
            ClientSignatoryDesignation: letter.ClientSignatoryDesignation,
            AcceptanceIp: letter.AcceptanceIp,
            LetterContent: letter.LetterContent,
            CreatedAt: letter.CreatedAt
        );
    }

    public async Task<IReadOnlyList<ClientKycRecordDto>> ListKycRecordsAsync(
        Guid? clientId = null,
        string? riskRating = null,
        CancellationToken ct = default)
    {
        var q = db.ClientKycRecords.Include(k => k.Client).AsQueryable();

        if (clientId.HasValue)
            q = q.Where(k => k.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(riskRating))
            q = q.Where(k => k.RiskRating == riskRating.Trim());

        var items = await q.OrderBy(k => k.NextReviewDueDate).ToListAsync(ct);

        return items.Select(k => new ClientKycRecordDto(
            Id: k.Id,
            ClientId: k.ClientId,
            ClientName: k.Client?.Name,
            RiskRating: k.RiskRating,
            VerificationStatus: k.VerificationStatus,
            ConstitutionDocumentType: k.ConstitutionDocumentType,
            ConstitutionDocumentNumber: k.ConstitutionDocumentNumber,
            PanVerified: k.PanVerified,
            GstinVerified: k.GstinVerified,
            AddressProofType: k.AddressProofType,
            AddressProofNumber: k.AddressProofNumber,
            UboName: k.UboName,
            UboShareholdingPercentage: k.UboShareholdingPercentage,
            UboPan: k.UboPan,
            SanctionsCheckCleared: k.SanctionsCheckCleared,
            AdverseMediaCheckCleared: k.AdverseMediaCheckCleared,
            RiskRationale: k.RiskRationale,
            LastReviewedDate: k.LastReviewedDate,
            NextReviewDueDate: k.NextReviewDueDate,
            CreatedAt: k.CreatedAt
        )).ToList();
    }

    public async Task<ClientKycRecordDto?> GetKycRecordByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        var k = await db.ClientKycRecords
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.ClientId == clientId, ct);

        if (k is null) return null;

        return new ClientKycRecordDto(
            Id: k.Id,
            ClientId: k.ClientId,
            ClientName: k.Client?.Name,
            RiskRating: k.RiskRating,
            VerificationStatus: k.VerificationStatus,
            ConstitutionDocumentType: k.ConstitutionDocumentType,
            ConstitutionDocumentNumber: k.ConstitutionDocumentNumber,
            PanVerified: k.PanVerified,
            GstinVerified: k.GstinVerified,
            AddressProofType: k.AddressProofType,
            AddressProofNumber: k.AddressProofNumber,
            UboName: k.UboName,
            UboShareholdingPercentage: k.UboShareholdingPercentage,
            UboPan: k.UboPan,
            SanctionsCheckCleared: k.SanctionsCheckCleared,
            AdverseMediaCheckCleared: k.AdverseMediaCheckCleared,
            RiskRationale: k.RiskRationale,
            LastReviewedDate: k.LastReviewedDate,
            NextReviewDueDate: k.NextReviewDueDate,
            CreatedAt: k.CreatedAt
        );
    }

    public async Task<ClientKycRecordDto> SaveKycRecordAsync(SaveClientKycRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {req.ClientId} not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nextDue = EngagementTemplateGenerator.CalculateNextKycReviewDueDate(req.RiskRating, today);

        var existing = await db.ClientKycRecords.FirstOrDefaultAsync(k => k.ClientId == req.ClientId, ct);

        if (existing is null)
        {
            existing = new ClientKycRecord
            {
                FirmId = tenant.FirmId,
                ClientId = req.ClientId,
                RiskRating = req.RiskRating ?? KycRiskRating.Low,
                VerificationStatus = req.VerificationStatus ?? KycVerificationStatus.PendingVerification,
                ConstitutionDocumentType = req.ConstitutionDocumentType ?? "CertificateOfIncorporation",
                ConstitutionDocumentNumber = req.ConstitutionDocumentNumber?.Trim(),
                PanVerified = req.PanVerified ?? false,
                GstinVerified = req.GstinVerified ?? false,
                AddressProofType = req.AddressProofType?.Trim(),
                AddressProofNumber = req.AddressProofNumber?.Trim(),
                UboName = req.UboName?.Trim(),
                UboShareholdingPercentage = req.UboShareholdingPercentage,
                UboPan = req.UboPan?.Trim().ToUpperInvariant(),
                SanctionsCheckCleared = req.SanctionsCheckCleared ?? true,
                AdverseMediaCheckCleared = req.AdverseMediaCheckCleared ?? true,
                RiskRationale = req.RiskRationale?.Trim(),
                LastReviewedDate = today,
                NextReviewDueDate = nextDue
            };
            db.ClientKycRecords.Add(existing);
        }
        else
        {
            existing.RiskRating = req.RiskRating ?? existing.RiskRating;
            existing.VerificationStatus = req.VerificationStatus ?? existing.VerificationStatus;
            existing.ConstitutionDocumentType = req.ConstitutionDocumentType ?? existing.ConstitutionDocumentType;
            existing.ConstitutionDocumentNumber = req.ConstitutionDocumentNumber?.Trim() ?? existing.ConstitutionDocumentNumber;
            existing.PanVerified = req.PanVerified ?? existing.PanVerified;
            existing.GstinVerified = req.GstinVerified ?? existing.GstinVerified;
            existing.AddressProofType = req.AddressProofType?.Trim() ?? existing.AddressProofType;
            existing.AddressProofNumber = req.AddressProofNumber?.Trim() ?? existing.AddressProofNumber;
            existing.UboName = req.UboName?.Trim() ?? existing.UboName;
            existing.UboShareholdingPercentage = req.UboShareholdingPercentage ?? existing.UboShareholdingPercentage;
            existing.UboPan = req.UboPan?.Trim().ToUpperInvariant() ?? existing.UboPan;
            existing.SanctionsCheckCleared = req.SanctionsCheckCleared ?? existing.SanctionsCheckCleared;
            existing.AdverseMediaCheckCleared = req.AdverseMediaCheckCleared ?? existing.AdverseMediaCheckCleared;
            existing.RiskRationale = req.RiskRationale?.Trim() ?? existing.RiskRationale;
            existing.LastReviewedDate = today;
            existing.NextReviewDueDate = nextDue;
        }

        await db.SaveChangesAsync(ct);

        return new ClientKycRecordDto(
            Id: existing.Id,
            ClientId: existing.ClientId,
            ClientName: client.Name,
            RiskRating: existing.RiskRating,
            VerificationStatus: existing.VerificationStatus,
            ConstitutionDocumentType: existing.ConstitutionDocumentType,
            ConstitutionDocumentNumber: existing.ConstitutionDocumentNumber,
            PanVerified: existing.PanVerified,
            GstinVerified: existing.GstinVerified,
            AddressProofType: existing.AddressProofType,
            AddressProofNumber: existing.AddressProofNumber,
            UboName: existing.UboName,
            UboShareholdingPercentage: existing.UboShareholdingPercentage,
            UboPan: existing.UboPan,
            SanctionsCheckCleared: existing.SanctionsCheckCleared,
            AdverseMediaCheckCleared: existing.AdverseMediaCheckCleared,
            RiskRationale: existing.RiskRationale,
            LastReviewedDate: existing.LastReviewedDate,
            NextReviewDueDate: existing.NextReviewDueDate,
            CreatedAt: existing.CreatedAt
        );
    }

    public GeneratedEngagementTemplate GenerateTemplate(GenerateTemplateRequest req) =>
        EngagementTemplateGenerator.GenerateTemplate(req);
}
