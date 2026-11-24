using CaDesk.Application.Abstractions;
using CaDesk.Domain.TaxLitigation;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.TaxLitigation;

public class TaxLitigationService : ITaxLitigationService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public TaxLitigationService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<TaxAppealDto>> GetAppealsAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var query = _db.TaxAppeals
            .Include(x => x.Client)
            .Include(x => x.Grounds)
            .Include(x => x.PaperBookDocuments)
            .Include(x => x.Citations)
            .AsQueryable();

        if (clientId.HasValue && clientId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return list.Select(x =>
        {
            var lim = TaxAppealStatutoryCalculator.CalculateLimitation(x.Forum, x.DateOfServiceOfOrder, today);
            string pan = (x.Client?.TaxIds != null && x.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : (x.Client?.Code ?? "");
            return new TaxAppealDto(
                Id: x.Id,
                ClientId: x.ClientId,
                ClientName: x.Client?.Name ?? "Client",
                ClientPan: pan,
                TaxNoticeId: x.TaxNoticeId,
                NoticeReferenceNumber: x.NoticeReferenceNumber,
                Forum: x.Forum,
                AppealNumber: x.AppealNumber,
                AssessmentYear: x.AssessmentYear,
                FinancialYear: x.FinancialYear,
                AssessingOfficerWardOrCircle: x.AssessingOfficerWardOrCircle,
                ImpugnedOrderSection: x.ImpugnedOrderSection,
                ImpugnedOrderNumber: x.ImpugnedOrderNumber,
                ImpugnedOrderDate: x.ImpugnedOrderDate,
                DateOfServiceOfOrder: x.DateOfServiceOfOrder,
                StatutoryFilingDeadline: lim.DeadlineDate,
                DaysRemaining: lim.DaysRemaining,
                DelayCondonationRequired: lim.DelayCondonationRequired,
                DaysOfDelay: lim.DaysOfDelay,
                FilingFeeAmount: x.FilingFeeAmount,
                AssessedTotalIncome: x.AssessedTotalIncome,
                ReturnedTotalIncome: x.ReturnedTotalIncome,
                TotalDisputedAdditions: x.TotalDisputedAdditions,
                DisputedTaxDemand: x.DisputedTaxDemand,
                FilingDate: x.FilingDate,
                DinOrAckNumber: x.DinOrAckNumber,
                Status: x.Status,
                PartnerMembershipNumber: x.PartnerMembershipNumber,
                PartnerCertifiedByName: x.PartnerCertifiedByName,
                PartnerCertifiedAt: x.PartnerCertifiedAt,
                GroundsCount: x.Grounds.Count,
                DocumentsCount: x.PaperBookDocuments.Count,
                CitationsCount: x.Citations.Count,
                CreatedAt: x.CreatedAt
            );
        }).ToList();
    }

    public async Task<TaxAppealDetailDto?> GetAppealByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await _db.TaxAppeals
            .Include(a => a.Client)
            .Include(a => a.Grounds)
            .Include(a => a.PaperBookDocuments)
            .Include(a => a.Citations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (x is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lim = TaxAppealStatutoryCalculator.CalculateLimitation(x.Forum, x.DateOfServiceOfOrder, today);

        return ToDetailDto(x, lim.DaysRemaining, lim.DelayCondonationRequired, lim.DaysOfDelay);
    }

    public async Task<TaxAppealDetailDto> SaveAppealAsync(CreateOrUpdateTaxAppealRequest req, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lim = TaxAppealStatutoryCalculator.CalculateLimitation(req.Forum, req.DateOfServiceOfOrder, today);
        decimal fee = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(req.Forum, req.AssessedTotalIncome);

        TaxAppeal entity;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.TaxAppeals
                .Include(a => a.Client)
                .Include(a => a.Grounds)
                .Include(a => a.PaperBookDocuments)
                .Include(a => a.Citations)
                .FirstOrDefaultAsync(a => a.Id == req.Id.Value, ct)
                ?? throw new KeyNotFoundException("Tax Appeal not found");
        }
        else
        {
            entity = new TaxAppeal
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                Status = AppealStatus.Drafting
            };
            _db.TaxAppeals.Add(entity);
        }

        entity.TaxNoticeId = req.TaxNoticeId;
        entity.NoticeReferenceNumber = req.NoticeReferenceNumber;
        entity.Forum = req.Forum;
        entity.AppealNumber = req.AppealNumber;
        entity.AssessmentYear = req.AssessmentYear;
        entity.FinancialYear = req.FinancialYear;
        entity.AssessingOfficerWardOrCircle = req.AssessingOfficerWardOrCircle;
        entity.ImpugnedOrderSection = req.ImpugnedOrderSection;
        entity.ImpugnedOrderNumber = req.ImpugnedOrderNumber;
        entity.ImpugnedOrderDate = req.ImpugnedOrderDate;
        entity.DateOfServiceOfOrder = req.DateOfServiceOfOrder;
        entity.StatutoryFilingDeadline = lim.DeadlineDate;
        entity.FilingFeeAmount = fee;
        entity.AssessedTotalIncome = req.AssessedTotalIncome;
        entity.ReturnedTotalIncome = req.ReturnedTotalIncome;
        entity.TotalDisputedAdditions = req.TotalDisputedAdditions;
        entity.DisputedTaxDemand = req.DisputedTaxDemand;
        entity.DelayCondonationRequired = lim.DelayCondonationRequired;
        entity.DaysOfDelay = lim.DaysOfDelay;
        entity.CondonationReason = req.CondonationReason;
        entity.StatementOfFacts = req.StatementOfFacts;
        entity.ReliefSought = req.ReliefSought;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        entity.Client = client;
        return ToDetailDto(entity, lim.DaysRemaining, lim.DelayCondonationRequired, lim.DaysOfDelay);
    }

    public async Task<bool> DeleteAppealAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.TaxAppeals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return false;

        _db.TaxAppeals.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AppealGroundItemDto> SaveGroundAsync(SaveAppealGroundRequest req, CancellationToken ct = default)
    {
        var appeal = await _db.TaxAppeals.FirstOrDefaultAsync(x => x.Id == req.TaxAppealId, ct)
            ?? throw new KeyNotFoundException("Tax Appeal not found");

        AppealGroundItem entity;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.AppealGrounds.FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct)
                ?? throw new KeyNotFoundException("Ground of Appeal not found");
        }
        else
        {
            entity = new AppealGroundItem
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                TaxAppealId = req.TaxAppealId
            };
            _db.AppealGrounds.Add(entity);
        }

        entity.GroundNumber = req.GroundNumber;
        entity.GroundType = req.GroundType;
        entity.Title = req.Title;
        entity.GroundText = req.GroundText;
        entity.StatutorySection = req.StatutorySection;
        entity.DisputedAdditionAmount = req.DisputedAdditionAmount;
        entity.IsWithoutPrejudice = req.IsWithoutPrejudice;
        entity.IsAlternativeGround = req.IsAlternativeGround;
        entity.OrderIndex = req.OrderIndex;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new AppealGroundItemDto(
            Id: entity.Id,
            TaxAppealId: entity.TaxAppealId,
            GroundNumber: entity.GroundNumber,
            GroundType: entity.GroundType,
            Title: entity.Title,
            GroundText: entity.GroundText,
            StatutorySection: entity.StatutorySection,
            DisputedAdditionAmount: entity.DisputedAdditionAmount,
            IsWithoutPrejudice: entity.IsWithoutPrejudice,
            IsAlternativeGround: entity.IsAlternativeGround,
            OrderIndex: entity.OrderIndex
        );
    }

    public async Task<bool> DeleteGroundAsync(Guid groundId, CancellationToken ct = default)
    {
        var entity = await _db.AppealGrounds.FirstOrDefaultAsync(x => x.Id == groundId, ct);
        if (entity is null) return false;

        _db.AppealGrounds.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<PaperBookDocumentDto>> SavePaperBookDocumentAsync(SavePaperBookDocRequest req, CancellationToken ct = default)
    {
        var appeal = await _db.TaxAppeals.Include(a => a.PaperBookDocuments).FirstOrDefaultAsync(x => x.Id == req.TaxAppealId, ct)
            ?? throw new KeyNotFoundException("Tax Appeal not found");

        PaperBookDocument entity;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = appeal.PaperBookDocuments.FirstOrDefault(x => x.Id == req.Id.Value)
                ?? throw new KeyNotFoundException("Paper Book document not found");
        }
        else
        {
            entity = new PaperBookDocument
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                TaxAppealId = req.TaxAppealId
            };
            appeal.PaperBookDocuments.Add(entity);
            _db.PaperBookDocuments.Add(entity);
        }

        entity.ItemNumber = req.ItemNumber;
        entity.DocumentDate = req.DocumentDate;
        entity.DescriptionOfDocument = req.DescriptionOfDocument;
        entity.Relevance = req.Relevance;
        entity.EvidenceType = req.EvidenceType;
        entity.LowerAuthorityFilingDate = req.LowerAuthorityFilingDate;
        entity.IsCertified = req.IsCertified;
        entity.OrderIndex = req.OrderIndex;

        // Set rough page range based on page count before re-pagination
        int pageCount = Math.Max(1, req.PageCount);
        entity.PageNumberFrom = 1;
        entity.PageNumberTo = pageCount;
        entity.UpdatedAt = DateTime.UtcNow;

        // Re-paginate all documents sequentially
        ItatRule18PaperBookEngine.RepaginateDocuments(appeal.PaperBookDocuments);

        await _db.SaveChangesAsync(ct);

        return appeal.PaperBookDocuments.OrderBy(x => x.ItemNumber).Select(ToPaperBookDto).ToList();
    }

    public async Task<List<PaperBookDocumentDto>> DeletePaperBookDocumentAsync(Guid docId, CancellationToken ct = default)
    {
        var entity = await _db.PaperBookDocuments.FirstOrDefaultAsync(x => x.Id == docId, ct);
        if (entity is null) return new List<PaperBookDocumentDto>();

        var appealId = entity.TaxAppealId;
        _db.PaperBookDocuments.Remove(entity);
        await _db.SaveChangesAsync(ct);

        var remaining = await _db.PaperBookDocuments.Where(x => x.TaxAppealId == appealId).ToListAsync(ct);
        ItatRule18PaperBookEngine.RepaginateDocuments(remaining);
        await _db.SaveChangesAsync(ct);

        return remaining.OrderBy(x => x.ItemNumber).Select(ToPaperBookDto).ToList();
    }

    public async Task<AppealJudicialCitationDto> AddCitationAsync(AddJudicialCitationRequest req, CancellationToken ct = default)
    {
        var appeal = await _db.TaxAppeals.FirstOrDefaultAsync(x => x.Id == req.TaxAppealId, ct)
            ?? throw new KeyNotFoundException("Tax Appeal not found");

        var entity = new AppealJudicialCitation
        {
            Id = Guid.NewGuid(),
            FirmId = _tenant.FirmId,
            TaxAppealId = req.TaxAppealId,
            PrecedentId = req.PrecedentId,
            CaseTitle = req.CaseTitle,
            Citation = req.Citation,
            CourtOrTribunal = req.CourtOrTribunal,
            JudgmentYear = req.JudgmentYear,
            StatutorySection = req.StatutorySection,
            KeyRatioProposition = req.KeyRatioProposition,
            ParagraphReference = req.ParagraphReference,
            RelevanceToGround = req.RelevanceToGround
        };

        _db.AppealJudicialCitations.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new AppealJudicialCitationDto(
            Id: entity.Id,
            TaxAppealId: entity.TaxAppealId,
            PrecedentId: entity.PrecedentId,
            CaseTitle: entity.CaseTitle,
            Citation: entity.Citation,
            CourtOrTribunal: entity.CourtOrTribunal,
            JudgmentYear: entity.JudgmentYear,
            StatutorySection: entity.StatutorySection,
            KeyRatioProposition: entity.KeyRatioProposition,
            ParagraphReference: entity.ParagraphReference,
            RelevanceToGround: entity.RelevanceToGround
        );
    }

    public async Task<bool> DeleteCitationAsync(Guid citationId, CancellationToken ct = default)
    {
        var entity = await _db.AppealJudicialCitations.FirstOrDefaultAsync(x => x.Id == citationId, ct);
        if (entity is null) return false;

        _db.AppealJudicialCitations.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TaxAppealDetailDto> CertifyAppealAsync(Guid appealId, CertifyAppealRequest req, CancellationToken ct = default)
    {
        var appeal = await _db.TaxAppeals
            .Include(a => a.Client)
            .Include(a => a.Grounds)
            .Include(a => a.PaperBookDocuments)
            .Include(a => a.Citations)
            .FirstOrDefaultAsync(a => a.Id == appealId, ct)
            ?? throw new KeyNotFoundException("Tax Appeal not found");

        appeal.Status = AppealStatus.Certified;
        appeal.PartnerMembershipNumber = req.PartnerMembershipNumber;
        appeal.PartnerCertifiedByName = req.PartnerName;
        appeal.PartnerCertifiedAt = DateTimeOffset.UtcNow;
        if (req.FilingDate.HasValue) appeal.FilingDate = req.FilingDate.Value;
        if (!string.IsNullOrWhiteSpace(req.DinOrAckNumber)) appeal.DinOrAckNumber = req.DinOrAckNumber;
        appeal.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lim = TaxAppealStatutoryCalculator.CalculateLimitation(appeal.Forum, appeal.DateOfServiceOfOrder, today);

        return ToDetailDto(appeal, lim.DaysRemaining, lim.DelayCondonationRequired, lim.DaysOfDelay);
    }

    public async Task<string> GenerateAppealDossierHtmlAsync(Guid appealId, CancellationToken ct = default)
    {
        var appeal = await _db.TaxAppeals
            .Include(a => a.Client)
            .Include(a => a.Grounds)
            .Include(a => a.PaperBookDocuments)
            .Include(a => a.Citations)
            .FirstOrDefaultAsync(a => a.Id == appealId, ct)
            ?? throw new KeyNotFoundException("Tax Appeal not found");

        return ItatRule18PaperBookEngine.CompileAppealDossierHtml(
            appeal,
            appeal.Grounds,
            appeal.PaperBookDocuments,
            appeal.Citations
        );
    }

    public FilingFeeResultDto CalculateFilingFee(CalculateFilingFeeRequest req)
    {
        decimal fee = TaxAppealStatutoryCalculator.CalculateStatutoryFilingFee(req.Forum, req.AssessedTotalIncome, req.IsOtherMatter);
        string legalBasis = req.Forum switch
        {
            AppealForum.CitAppeals => "Section 249(1) of the Income Tax Act, 1961 (₹250 / ₹500 / ₹1,000)",
            AppealForum.Itat => "Section 253(6) of the Income Tax Act, 1961 (₹500 / ₹1,500 / 1% up to ₹10,000 max)",
            AppealForum.HighCourt => "Section 260A of the Income Tax Act, 1961 r.w. Court Fees Act",
            _ => "Supreme Court Rules, 2013"
        };

        return new FilingFeeResultDto(
            Forum: req.Forum,
            AssessedTotalIncome: req.AssessedTotalIncome,
            StatutoryFilingFee: fee,
            LegalBasis: legalBasis
        );
    }

    private static TaxAppealDetailDto ToDetailDto(TaxAppeal a, int daysRemaining, bool condonationRequired, int daysOfDelay)
    {
        string pan = (a.Client?.TaxIds != null && a.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : (a.Client?.Code ?? "");
        return new TaxAppealDetailDto(
            Id: a.Id,
            ClientId: a.ClientId,
            ClientName: a.Client?.Name ?? "Client",
            ClientPan: pan,
            TaxNoticeId: a.TaxNoticeId,
            NoticeReferenceNumber: a.NoticeReferenceNumber,
            Forum: a.Forum,
            AppealNumber: a.AppealNumber,
            AssessmentYear: a.AssessmentYear,
            FinancialYear: a.FinancialYear,
            AssessingOfficerWardOrCircle: a.AssessingOfficerWardOrCircle,
            ImpugnedOrderSection: a.ImpugnedOrderSection,
            ImpugnedOrderNumber: a.ImpugnedOrderNumber,
            ImpugnedOrderDate: a.ImpugnedOrderDate,
            DateOfServiceOfOrder: a.DateOfServiceOfOrder,
            StatutoryFilingDeadline: a.StatutoryFilingDeadline,
            DaysRemaining: daysRemaining,
            DelayCondonationRequired: condonationRequired,
            DaysOfDelay: daysOfDelay,
            CondonationReason: a.CondonationReason,
            FilingFeeAmount: a.FilingFeeAmount,
            AssessedTotalIncome: a.AssessedTotalIncome,
            ReturnedTotalIncome: a.ReturnedTotalIncome,
            TotalDisputedAdditions: a.TotalDisputedAdditions,
            DisputedTaxDemand: a.DisputedTaxDemand,
            FilingDate: a.FilingDate,
            DinOrAckNumber: a.DinOrAckNumber,
            Status: a.Status,
            StatementOfFacts: a.StatementOfFacts,
            ReliefSought: a.ReliefSought,
            PartnerMembershipNumber: a.PartnerMembershipNumber,
            PartnerCertifiedByName: a.PartnerCertifiedByName,
            PartnerCertifiedAt: a.PartnerCertifiedAt,
            Grounds: a.Grounds.OrderBy(g => g.OrderIndex).ThenBy(g => g.GroundNumber).Select(g => new AppealGroundItemDto(
                Id: g.Id,
                TaxAppealId: g.TaxAppealId,
                GroundNumber: g.GroundNumber,
                GroundType: g.GroundType,
                Title: g.Title,
                GroundText: g.GroundText,
                StatutorySection: g.StatutorySection,
                DisputedAdditionAmount: g.DisputedAdditionAmount,
                IsWithoutPrejudice: g.IsWithoutPrejudice,
                IsAlternativeGround: g.IsAlternativeGround,
                OrderIndex: g.OrderIndex
            )).ToList(),
            PaperBookDocuments: a.PaperBookDocuments.OrderBy(d => d.ItemNumber).Select(ToPaperBookDto).ToList(),
            Citations: a.Citations.Select(c => new AppealJudicialCitationDto(
                Id: c.Id,
                TaxAppealId: c.TaxAppealId,
                PrecedentId: c.PrecedentId,
                CaseTitle: c.CaseTitle,
                Citation: c.Citation,
                CourtOrTribunal: c.CourtOrTribunal,
                JudgmentYear: c.JudgmentYear,
                StatutorySection: c.StatutorySection,
                KeyRatioProposition: c.KeyRatioProposition,
                ParagraphReference: c.ParagraphReference,
                RelevanceToGround: c.RelevanceToGround
            )).ToList(),
            CreatedAt: a.CreatedAt
        );
    }

    private static PaperBookDocumentDto ToPaperBookDto(PaperBookDocument d)
    {
        return new PaperBookDocumentDto(
            Id: d.Id,
            TaxAppealId: d.TaxAppealId,
            ItemNumber: d.ItemNumber,
            DocumentDate: d.DocumentDate,
            DescriptionOfDocument: d.DescriptionOfDocument,
            Relevance: d.Relevance,
            EvidenceType: d.EvidenceType,
            LowerAuthorityFilingDate: d.LowerAuthorityFilingDate,
            PageNumberFrom: d.PageNumberFrom,
            PageNumberTo: d.PageNumberTo,
            TotalPages: d.TotalPages,
            IsCertified: d.IsCertified,
            OrderIndex: d.OrderIndex
        );
    }
}
