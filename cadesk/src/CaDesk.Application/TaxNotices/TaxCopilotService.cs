using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.TaxNotices;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.TaxNotices;

public class TaxCopilotService : ITaxCopilotService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    private static readonly Regex UdinRegex = new(@"^[0-9]{2}[0-9]{6}[A-Z0-9]{4}[A-Z0-9]{6}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TaxCopilotService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ParsedNoticeResultDto> ParseNoticeAsync(ParseNoticeRequest req, CancellationToken ct = default)
    {
        var parsed = NoticeOcrParser.Parse(req.RawText);

        string? clientPanOrGstin = parsed.PanOrGstin;
        if (req.ClientId.HasValue && req.ClientId.Value != Guid.Empty)
        {
            var client = await _db.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == req.ClientId.Value && c.FirmId == _tenant.FirmId, ct);
            if (client is not null && string.IsNullOrEmpty(clientPanOrGstin))
            {
                clientPanOrGstin = client.TaxIds.GetValueOrDefault("PAN") ?? client.TaxIds.GetValueOrDefault("GSTIN") ?? client.Code;
            }
        }

        // Match relevant precedents from knowledge base
        var matchedPrecedents = await GetRecommendedPrecedentsInternalAsync(parsed.Authority, parsed.Section, parsed.ExtractedIssue, ct);

        return new ParsedNoticeResultDto(
            parsed.Authority,
            parsed.Category,
            parsed.Section,
            parsed.Din,
            parsed.DinStatus,
            clientPanOrGstin,
            parsed.AssessmentYear,
            parsed.FinancialYear,
            parsed.IssueDate,
            parsed.DueDate,
            parsed.DemandAmount,
            parsed.OfficerJurisdiction,
            parsed.ExtractedIssue,
            parsed.SummaryOfAllegations,
            parsed.RecommendedPreliminaryObjections,
            parsed.RecommendedGrounds,
            matchedPrecedents,
            parsed.ConfidenceScore
        );
    }

    public async Task<List<StatutoryPrecedentDto>> GetPrecedentsAsync(
        TaxAuthority? authority,
        PrecedentCategory? category,
        string? search,
        CancellationToken ct = default)
    {
        var query = _db.StatutoryPrecedents
            .AsNoTracking()
            .Where(p => p.FirmId == null || p.FirmId == _tenant.FirmId);

        if (authority.HasValue)
        {
            query = query.Where(p => p.Authority == authority.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.CaseTitle.ToLower().Contains(s) ||
                p.Citation.ToLower().Contains(s) ||
                p.ApplicableSections.ToLower().Contains(s) ||
                p.KeyRatioDecidendi.ToLower().Contains(s) ||
                p.Keywords.ToLower().Contains(s));
        }

        var list = await query
            .OrderByDescending(p => p.IsLandmark)
            .ThenByDescending(p => p.JudgmentYear)
            .ToListAsync(ct);

        return list.Select(MapToPrecedentDto).ToList();
    }

    public async Task<StatutoryPrecedentDto> CreateCustomPrecedentAsync(CreatePrecedentRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.CaseTitle))
            throw new ArgumentException("Case Title is required.", nameof(req));
        if (string.IsNullOrWhiteSpace(req.Citation))
            throw new ArgumentException("Citation is required.", nameof(req));

        var precedent = new StatutoryPrecedent
        {
            Id = Guid.NewGuid(),
            FirmId = _tenant.FirmId,
            Authority = req.Authority,
            Category = req.Category,
            CaseTitle = req.CaseTitle.Trim(),
            Citation = req.Citation.Trim(),
            CourtBench = req.CourtBench?.Trim() ?? "Tribunal / High Court",
            JudgmentYear = req.JudgmentYear > 1900 ? req.JudgmentYear : DateTime.UtcNow.Year,
            ApplicableSections = req.ApplicableSections?.Trim() ?? "",
            KeyRatioDecidendi = req.KeyRatioDecidendi?.Trim() ?? "",
            DefenseGroundTemplate = req.DefenseGroundTemplate?.Trim() ?? "",
            IsLandmark = false,
            Keywords = req.Keywords?.Trim() ?? "",
            CreatedAt = DateTime.UtcNow
        };

        _db.StatutoryPrecedents.Add(precedent);
        await _db.SaveChangesAsync(ct);

        return MapToPrecedentDto(precedent);
    }

    public async Task<TaxDefenseDraftDto> CreateDefenseDraftAsync(CreateDefenseDraftRequest req, CancellationToken ct = default)
    {
        var notice = await _db.TaxNotices
            .Include(n => n.Client)
            .FirstOrDefaultAsync(n => n.Id == req.NoticeId && n.FirmId == _tenant.FirmId, ct);

        if (notice is null)
            throw new KeyNotFoundException($"Tax Notice {req.NoticeId} not found.");

        var count = await _db.TaxDefenseDrafts.CountAsync(d => d.FirmId == _tenant.FirmId, ct);
        var draftNumber = $"DEF-{DateTime.UtcNow.Year}-{count + 1:D4}";

        var clientName = notice.Client?.Name ?? "Client";
        var clientPanOrGstin = notice.Client?.TaxIds.GetValueOrDefault("PAN") ?? notice.Client?.TaxIds.GetValueOrDefault("GSTIN") ?? notice.Client?.Code ?? "";

        var title = !string.IsNullOrWhiteSpace(req.Title)
            ? req.Title.Trim()
            : (notice.Authority == TaxAuthority.IncomeTax
                ? $"Written Submissions in response to Notice under {notice.Section}"
                : $"Detailed Written Reply to Show Cause Notice under {notice.Section}");

        var subject = !string.IsNullOrWhiteSpace(req.Subject)
            ? req.Subject.Trim()
            : $"In the matter of {clientName} ({clientPanOrGstin}) for {notice.FinancialYear} (AY {notice.AssessmentYear ?? "N/A"}) - Response to {notice.Section} Notice dated {notice.IssueDate:dd/MM/yyyy}";

        var addressedTo = !string.IsNullOrWhiteSpace(req.AddressedTo)
            ? req.AddressedTo.Trim()
            : (notice.OfficerJurisdiction ?? (notice.Authority == TaxAuthority.IncomeTax ? "National Faceless Assessment Centre (NFAC), Delhi" : "Assistant Commissioner of State / Central Tax"));

        var preliminaryObjections = req.PreliminaryObjections ?? new List<string>();
        if (preliminaryObjections.Count == 0)
        {
            if (notice.Authority == TaxAuthority.IncomeTax)
            {
                if (notice.Section.Contains("148"))
                {
                    preliminaryObjections.Add("Reassessment time-barred under Section 149: Impugned proceedings initiated beyond 3-year limit without tangible asset evidence exceeding Rs. 50 Lakhs (Rajeev Bansal SC).");
                    preliminaryObjections.Add("Defective jurisdiction for want of valid sanction under Section 151 from the specified superior authority.");
                    preliminaryObjections.Add("Denial of Natural Justice: Underlying reports, statements, and reasons recorded were not provided prior to notice issuance.");
                }
                else if (notice.Section.Contains("143(1)"))
                {
                    preliminaryObjections.Add("Section 143(1)(a) does not permit mechanical disallowance of statutory claims without prior notice to the assessee.");
                }
                else if (notice.Section.Contains("270A"))
                {
                    preliminaryObjections.Add("Show cause notice is invalid for ambiguity as it fails to specify the exact charge of 'under-reporting' vs 'misreporting' of income.");
                }
                else
                {
                    preliminaryObjections.Add("Statutory Right to Personal Hearing under Section 144B(6): Mandatory requirement of personal hearing through video conference prior to any adverse order.");
                }
            }
            else
            {
                preliminaryObjections.Add("Mandatory Personal Hearing under Section 75(4) of CGST Act, 2017 before any adverse decision or tax demand is finalized.");
                preliminaryObjections.Add("Show Cause Notice barred by limitation under Section 73(10) from the statutory due date of the annual return.");
            }
        }

        var factualMatrix = req.FactualMatrix ?? new List<FactualItem>
        {
            new() { ItemNo = 1, EventDate = notice.IssueDate.ToString("dd/MM/yyyy"), Description = $"Receipt of notice under {notice.Section} alleging tax discrepancy.", AnnexureRef = "Annexure A-1: Copy of Notice" },
            new() { ItemNo = 2, EventDate = DateTime.UtcNow.ToString("dd/MM/yyyy"), Description = "Verification of regular audited books of account and statutory returns filed by assessee.", AnnexureRef = "Annexure A-2: ITR / GSTR-3B Acknowledgement" },
            new() { ItemNo = 3, EventDate = DateTime.UtcNow.ToString("dd/MM/yyyy"), Description = "Compilation of third-party ledger confirmations, bank remittances, and counter-party KYC.", AnnexureRef = "Annexure A-3: Bank Statement & Confirmations" }
        };

        var meritGrounds = req.MeritGrounds ?? new List<MeritGroundItem>
        {
            new() { GroundNo = 1, Title = "Full Disclosure and Genuine Character of Transactions", Argument = "The entire receipts and expenses form part of the regular books of accounts audited by statutory auditors. All payments were made/received strictly through account payee banking channels.", RelevantSection = notice.Section },
            new() { GroundNo = 2, Title = "No Loss of Revenue or Evasion Intent", Argument = "The assessee acted in bona fide compliance with all applicable legal provisions. No contumacious conduct or fraudulent intent can be attributed.", RelevantSection = notice.Section }
        };

        var citedPrecedents = req.CitedPrecedents ?? new List<CitedPrecedentItem>();
        if (citedPrecedents.Count == 0)
        {
            var recommended = await GetRecommendedPrecedentsInternalAsync(notice.Authority, notice.Section, notice.Subject, ct);
            foreach (var rec in recommended.Take(3))
            {
                citedPrecedents.Add(new CitedPrecedentItem
                {
                    PrecedentId = rec.Id,
                    CaseTitle = rec.CaseTitle,
                    Citation = rec.Citation,
                    CourtBench = rec.CourtBench,
                    RelevantHolding = rec.KeyRatioDecidendi
                });
            }
        }

        var prayer = !string.IsNullOrWhiteSpace(req.PrayerForRelief)
            ? req.PrayerForRelief.Trim()
            : (notice.Authority == TaxAuthority.IncomeTax
                ? "In light of the aforesaid factual submissions and judicial authorities, it is most respectfully prayed that:\n1. The preliminary objections be upheld and the impugned notice / proceedings be dropped in toto.\n2. The return of income as filed by the assessee be accepted without any adverse addition or disallowance.\n3. An opportunity of personal hearing via Video Conference under Section 144B(6) be granted before taking any adverse view."
                : "In view of the above facts, statutory provisions, and binding judicial precedents, it is prayed that:\n1. The proposed demand of tax, interest, and penalty under the Show Cause Notice be dropped.\n2. The Input Tax Credit claimed in Form GSTR-3B be verified and allowed in full.\n3. A mandatory personal hearing under Section 75(4) of the CGST Act be granted prior to passing any order.");

        var annexures = req.AnnexureIndex ?? new List<AnnexureItem>
        {
            new() { AnnexureNo = "A-1", Title = $"Copy of Notice under {notice.Section}", DocumentType = "Official Notice", PageCount = 2 },
            new() { AnnexureNo = "A-2", Title = "Audited Financial Statements & Tax Audit Report", DocumentType = "Financial Statements", PageCount = 18 },
            new() { AnnexureNo = "A-3", Title = "Bank Statements evidencing payments through banking channels", DocumentType = "Bank Statement", PageCount = 6 }
        };

        var draft = new TaxDefenseDraft
        {
            Id = Guid.NewGuid(),
            FirmId = _tenant.FirmId,
            NoticeId = notice.Id,
            ClientId = notice.ClientId,
            DraftNumber = draftNumber,
            Title = title,
            Subject = subject,
            AddressedTo = addressedTo,
            ClientName = clientName,
            ClientPanOrGstin = clientPanOrGstin,
            AssessmentYear = notice.AssessmentYear ?? "",
            FinancialYear = notice.FinancialYear,
            NoticeRefNumber = notice.NoticeNumber,
            NoticeDin = notice.Din ?? "N/A",
            NoticeIssueDate = notice.IssueDate,
            NoticeDueDate = notice.DueDate,
            PrayerForRelief = prayer,
            Status = DefenseDraftStatus.Draft,
            PreparedByUserId = _tenant.UserId,
            PreparedByName = "CA Staff",
            CreatedAt = DateTime.UtcNow
        };

        draft.SetPreliminaryObjections(preliminaryObjections);
        draft.SetFactualMatrix(factualMatrix);
        draft.SetMeritGrounds(meritGrounds);
        draft.SetCitedPrecedents(citedPrecedents);
        draft.SetAnnexureIndex(annexures);

        draft.GeneratedHtml = GenerateDefenseHtml(draft);

        _db.TaxDefenseDrafts.Add(draft);

        // Update notice status
        if (notice.Status == NoticeStatus.Received)
        {
            notice.Status = NoticeStatus.InReview;
        }

        await _db.SaveChangesAsync(ct);

        return MapToDraftDto(draft);
    }

    public async Task<TaxDefenseDraftDto?> GetDefenseDraftByIdAsync(Guid id, CancellationToken ct = default)
    {
        var draft = await _db.TaxDefenseDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.FirmId == _tenant.FirmId && d.DeletedAt == null, ct);

        return draft is null ? null : MapToDraftDto(draft);
    }

    public async Task<List<TaxDefenseDraftDto>> GetDefenseDraftsForNoticeAsync(Guid noticeId, CancellationToken ct = default)
    {
        var list = await _db.TaxDefenseDrafts
            .AsNoTracking()
            .Where(d => d.NoticeId == noticeId && d.FirmId == _tenant.FirmId && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return list.Select(MapToDraftDto).ToList();
    }

    public async Task<TaxDefenseDraftDto> UpdateDefenseDraftAsync(Guid id, UpdateDefenseDraftRequest req, CancellationToken ct = default)
    {
        var draft = await _db.TaxDefenseDrafts
            .FirstOrDefaultAsync(d => d.Id == id && d.FirmId == _tenant.FirmId && d.DeletedAt == null, ct);

        if (draft is null)
            throw new KeyNotFoundException($"Defense Draft {id} not found.");

        if (draft.Status == DefenseDraftStatus.Finalized)
            throw new InvalidOperationException("Finalized defense drafts cannot be edited.");

        draft.Title = req.Title.Trim();
        draft.Subject = req.Subject.Trim();
        draft.AddressedTo = req.AddressedTo.Trim();
        draft.PrayerForRelief = req.PrayerForRelief.Trim();

        draft.SetPreliminaryObjections(req.PreliminaryObjections);
        draft.SetFactualMatrix(req.FactualMatrix);
        draft.SetMeritGrounds(req.MeritGrounds);
        draft.SetCitedPrecedents(req.CitedPrecedents);
        draft.SetAnnexureIndex(req.AnnexureIndex);

        draft.GeneratedHtml = GenerateDefenseHtml(draft);
        draft.UpdatedAt = DateTime.UtcNow;
        draft.UpdatedBy = _tenant.UserId;

        await _db.SaveChangesAsync(ct);

        return MapToDraftDto(draft);
    }

    public async Task<TaxDefenseDraftDto> ApproveDefenseDraftAsync(Guid id, ApproveDefenseDraftRequest req, CancellationToken ct = default)
    {
        var draft = await _db.TaxDefenseDrafts
            .Include(d => d.Notice)
            .FirstOrDefaultAsync(d => d.Id == id && d.FirmId == _tenant.FirmId && d.DeletedAt == null, ct);

        if (draft is null)
            throw new KeyNotFoundException($"Defense Draft {id} not found.");

        if (!string.IsNullOrWhiteSpace(req.PartnerUdin))
        {
            var cleanUdin = req.PartnerUdin.Trim();
            if (!UdinRegex.IsMatch(cleanUdin))
            {
                throw new ArgumentException("UDIN must be an 18-character alphanumeric string (e.g. 24104523ABCD123456).", nameof(req));
            }
            draft.PartnerUdin = cleanUdin;
        }

        draft.Status = DefenseDraftStatus.PartnerApproved;
        draft.ApprovedByUserId = _tenant.UserId;
        draft.ApprovedByName = "Managing Partner";
        draft.ApprovedAt = DateTimeOffset.UtcNow;
        draft.UpdatedAt = DateTime.UtcNow;
        draft.UpdatedBy = _tenant.UserId;

        // Re-generate HTML with approval badge and UDIN
        draft.GeneratedHtml = GenerateDefenseHtml(draft);

        if (draft.Notice is not null)
        {
            draft.Notice.Status = NoticeStatus.PartnerApproved;
            draft.Notice.UpdatedAt = DateTime.UtcNow;
            draft.Notice.UpdatedBy = _tenant.UserId;
        }

        await _db.SaveChangesAsync(ct);

        return MapToDraftDto(draft);
    }

    public async Task<string> ExportDefenseDraftHtmlAsync(Guid id, CancellationToken ct = default)
    {
        var draft = await _db.TaxDefenseDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.FirmId == _tenant.FirmId && d.DeletedAt == null, ct);

        if (draft is null)
            throw new KeyNotFoundException($"Defense Draft {id} not found.");

        return draft.GeneratedHtml ?? GenerateDefenseHtml(draft);
    }

    private async Task<List<StatutoryPrecedentDto>> GetRecommendedPrecedentsInternalAsync(
        TaxAuthority authority,
        string section,
        string? issue,
        CancellationToken ct)
    {
        var query = _db.StatutoryPrecedents
            .AsNoTracking()
            .Where(p => (p.FirmId == null || p.FirmId == _tenant.FirmId) && p.Authority == authority);

        var list = await query.ToListAsync(ct);

        var secNormalized = section.ToLowerInvariant();
        var issueNormalized = (issue ?? "").ToLowerInvariant();

        var scored = list.Select(p =>
        {
            var score = 0;
            var appSec = p.ApplicableSections.ToLowerInvariant();
            var kw = p.Keywords.ToLowerInvariant();

            if (secNormalized.Contains("148") && appSec.Contains("148")) score += 40;
            if (secNormalized.Contains("143(1)") && appSec.Contains("143(1)")) score += 40;
            if (secNormalized.Contains("270a") && appSec.Contains("270a")) score += 40;
            if ((secNormalized.Contains("drc") || secNormalized.Contains("88d") || secNormalized.Contains("73")) && (appSec.Contains("16(2)(c)") || appSec.Contains("88d") || appSec.Contains("73"))) score += 40;

            if (issueNormalized.Contains("cash") && kw.Contains("cash")) score += 20;
            if (issueNormalized.Contains("itc") && kw.Contains("itc")) score += 25;
            if (issueNormalized.Contains("tds") && kw.Contains("tds")) score += 25;
            if (issueNormalized.Contains("limitation") && kw.Contains("limitation")) score += 25;
            if (issueNormalized.Contains("hearing") && kw.Contains("natural justice")) score += 20;

            if (p.IsLandmark) score += 10;

            return new { Precedent = p, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .ThenByDescending(x => x.Precedent.JudgmentYear)
        .Take(5)
        .Select(x => MapToPrecedentDto(x.Precedent))
        .ToList();

        return scored;
    }

    private static StatutoryPrecedentDto MapToPrecedentDto(StatutoryPrecedent p) => new(
        p.Id,
        p.FirmId,
        p.Authority,
        p.Category,
        p.CaseTitle,
        p.Citation,
        p.CourtBench,
        p.JudgmentYear,
        p.ApplicableSections,
        p.KeyRatioDecidendi,
        p.DefenseGroundTemplate,
        p.IsLandmark,
        p.Keywords
    );

    private static TaxDefenseDraftDto MapToDraftDto(TaxDefenseDraft d) => new(
        d.Id,
        d.NoticeId,
        d.ClientId,
        d.ClientName,
        d.DraftNumber,
        d.Title,
        d.Subject,
        d.AddressedTo,
        d.ClientPanOrGstin,
        d.AssessmentYear,
        d.FinancialYear,
        d.NoticeRefNumber,
        d.NoticeDin,
        d.NoticeIssueDate,
        d.NoticeDueDate,
        d.GetPreliminaryObjections(),
        d.GetFactualMatrix(),
        d.GetMeritGrounds(),
        d.GetCitedPrecedents(),
        d.PrayerForRelief,
        d.GetAnnexureIndex(),
        d.Status,
        d.PreparedByUserId,
        d.PreparedByName,
        d.ApprovedByUserId,
        d.ApprovedByName,
        d.ApprovedAt,
        d.PartnerUdin,
        d.GeneratedHtml,
        d.CreatedAt
    );

    private static string GenerateDefenseHtml(TaxDefenseDraft d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(d.DraftNumber)} - {WebUtility.HtmlEncode(d.Title)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(@"
          body { font-family: 'Times New Roman', Times, serif; font-size: 14pt; line-height: 1.6; color: #111; margin: 0; padding: 2.5cm; }
          .header-box { text-align: center; border-bottom: 2px solid #000; padding-bottom: 12px; margin-bottom: 24px; }
          .firm-title { font-size: 18pt; font-weight: bold; text-transform: uppercase; letter-spacing: 1px; }
          .firm-sub { font-size: 11pt; color: #444; font-style: italic; }
          .draft-meta { display: flex; justify-content: space-between; margin-bottom: 20px; font-size: 12pt; }
          .addressed-to { margin-bottom: 20px; }
          .subject-box { background: #f4f4f4; padding: 10px 14px; border-left: 4px solid #333; margin: 20px 0; font-weight: bold; }
          .section-heading { font-size: 15pt; font-weight: bold; text-decoration: underline; margin-top: 28px; margin-bottom: 12px; }
          .objection-item { margin-bottom: 12px; text-align: justify; }
          table { width: 100%; border-collapse: collapse; margin: 16px 0; font-size: 12pt; }
          th, td { border: 1px solid #666; padding: 8px 10px; text-align: left; }
          th { background: #eaeaea; font-weight: bold; }
          .precedent-card { border: 1px solid #aaa; padding: 12px; margin: 12px 0; background: #fafafa; border-radius: 4px; }
          .case-title { font-weight: bold; font-size: 13pt; color: #1a365d; }
          .citation { font-style: italic; color: #555; }
          .ratio-box { margin-top: 6px; font-size: 12pt; text-align: justify; }
          .prayer-box { border: 1px dashed #333; padding: 14px; background: #fdfdfd; margin-top: 20px; white-space: pre-wrap; font-size: 12pt; }
          .signature-box { margin-top: 40px; float: right; width: 280px; text-align: left; }
          .udin-badge { border: 2px solid #047857; color: #047857; padding: 4px 10px; font-family: monospace; font-weight: bold; display: inline-block; margin-top: 8px; border-radius: 4px; }
          .clear { clear: both; }
          @media print {
            body { padding: 0; margin: 1.5cm; }
            .no-print { display: none; }
          }
        ");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("<div class=\"header-box\">");
        sb.AppendLine("<div class=\"firm-title\">FORMAL STATUTORY WRITTEN SUBMISSION</div>");
        sb.AppendLine("<div class=\"firm-sub\">Prepared via CA Desk &bull; Legal Defense &amp; Statutory Citation Copilot</div>");
        sb.AppendLine("</div>");

        // Meta
        sb.AppendLine("<div class=\"draft-meta\">");
        sb.AppendLine($"<div><strong>Draft Ref:</strong> {WebUtility.HtmlEncode(d.DraftNumber)}<br/><strong>Notice DIN:</strong> {WebUtility.HtmlEncode(d.NoticeDin)}</div>");
        sb.AppendLine($"<div style=\"text-align:right;\"><strong>Date:</strong> {DateTime.UtcNow:dd MMMM yyyy}<br/><strong>Notice Ref:</strong> {WebUtility.HtmlEncode(d.NoticeRefNumber)}</div>");
        sb.AppendLine("</div>");

        // Addressed To
        sb.AppendLine("<div class=\"addressed-to\">");
        sb.AppendLine("To,<br/>");
        sb.AppendLine($"<strong>{WebUtility.HtmlEncode(d.AddressedTo)}</strong><br/>");
        sb.AppendLine("Income Tax Department / Goods and Services Tax Network<br/>");
        sb.AppendLine("Government of India");
        sb.AppendLine("</div>");

        // Subject Box
        sb.AppendLine("<div class=\"subject-box\">");
        sb.AppendLine($"<strong>SUBJECT:</strong> {WebUtility.HtmlEncode(d.Subject)}<br/>");
        sb.AppendLine($"<span style=\"font-size:11pt; font-weight:normal;\"><strong>Assessee:</strong> {WebUtility.HtmlEncode(d.ClientName)} | <strong>PAN/GSTIN:</strong> {WebUtility.HtmlEncode(d.ClientPanOrGstin)} | <strong>FY:</strong> {WebUtility.HtmlEncode(d.FinancialYear)} | <strong>AY:</strong> {WebUtility.HtmlEncode(d.AssessmentYear)}</span>");
        sb.AppendLine("</div>");

        sb.AppendLine("<p>Respected Sir/Madam,</p>");
        sb.AppendLine("<p>In response to the captioned notice, the assessee above-named respectfully submits the following preliminary objections, factual matrix, legal submissions, and judicial authorities for your kind and favorable consideration:</p>");

        // 1. Preliminary Objections
        var objections = d.GetPreliminaryObjections();
        if (objections.Count > 0)
        {
            sb.AppendLine("<div class=\"section-heading\">I. PRELIMINARY OBJECTIONS TO JURISDICTION &amp; VALIDITY</div>");
            for (int i = 0; i < objections.Count; i++)
            {
                sb.AppendLine($"<div class=\"objection-item\"><strong>{i + 1}.</strong> {WebUtility.HtmlEncode(objections[i])}</div>");
            }
        }

        // 2. Factual Matrix
        var facts = d.GetFactualMatrix();
        if (facts.Count > 0)
        {
            sb.AppendLine("<div class=\"section-heading\">II. FACTUAL CHRONOLOGY &amp; PRIMARY MATRIX</div>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th style=\"width:8%;\">S.No.</th><th style=\"width:18%;\">Event Date</th><th>Description of Fact / Transaction</th><th style=\"width:24%;\">Supporting Evidence</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var f in facts)
            {
                sb.AppendLine($"<tr><td>{f.ItemNo}</td><td>{WebUtility.HtmlEncode(f.EventDate)}</td><td>{WebUtility.HtmlEncode(f.Description)}</td><td>{WebUtility.HtmlEncode(f.AnnexureRef)}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // 3. Submissions on Merits
        var grounds = d.GetMeritGrounds();
        if (grounds.Count > 0)
        {
            sb.AppendLine("<div class=\"section-heading\">III. DETAILED SUBMISSIONS &amp; REBUTTAL ON MERITS</div>");
            foreach (var g in grounds)
            {
                sb.AppendLine($"<div style=\"margin-bottom:18px;\">");
                sb.AppendLine($"<div style=\"font-weight:bold; font-size:13pt;\">Ground {g.GroundNo}: {WebUtility.HtmlEncode(g.Title)} (Ref: {WebUtility.HtmlEncode(g.RelevantSection)})</div>");
                sb.AppendLine($"<div style=\"text-align:justify; margin-top:4px;\">{WebUtility.HtmlEncode(g.Argument)}</div>");
                sb.AppendLine("</div>");
            }
        }

        // 4. Judicial Authorities & Precedents
        var precedents = d.GetCitedPrecedents();
        if (precedents.Count > 0)
        {
            sb.AppendLine("<div class=\"section-heading\">IV. JUDICIAL AUTHORITIES &amp; BINDING PRECEDENTS ANNEXED</div>");
            foreach (var p in precedents)
            {
                sb.AppendLine("<div class=\"precedent-card\">");
                sb.AppendLine($"<div class=\"case-title\">&bull; {WebUtility.HtmlEncode(p.CaseTitle)}</div>");
                sb.AppendLine($"<div class=\"citation\">Citation: <strong>{WebUtility.HtmlEncode(p.Citation)}</strong> | Forum: {WebUtility.HtmlEncode(p.CourtBench)}</div>");
                sb.AppendLine($"<div class=\"ratio-box\"><strong>Key Principle / Ratio Decidendi:</strong> {WebUtility.HtmlEncode(p.RelevantHolding)}</div>");
                sb.AppendLine("</div>");
            }
        }

        // 5. Prayer for Relief
        sb.AppendLine("<div class=\"section-heading\">V. PRAYER FOR RELIEF</div>");
        sb.AppendLine($"<div class=\"prayer-box\">{WebUtility.HtmlEncode(d.PrayerForRelief)}</div>");

        // 6. Annexures
        var annexures = d.GetAnnexureIndex();
        if (annexures.Count > 0)
        {
            sb.AppendLine("<div class=\"section-heading\">VI. INDEX OF ANNEXURES &amp; SUBSTANTIATING DOCUMENTS</div>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th style=\"width:15%;\">Annexure No.</th><th>Document Description</th><th style=\"width:22%;\">Type</th><th style=\"width:12%;\">Pages</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var a in annexures)
            {
                sb.AppendLine($"<tr><td><strong>{WebUtility.HtmlEncode(a.AnnexureNo)}</strong></td><td>{WebUtility.HtmlEncode(a.Title)}</td><td>{WebUtility.HtmlEncode(a.DocumentType)}</td><td>{a.PageCount}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // Signature & Approval Block
        sb.AppendLine("<div class=\"signature-box\">");
        sb.AppendLine("<p>Yours faithfully,<br/><strong>For and on behalf of Assessee:</strong></p>");
        sb.AppendLine("<div style=\"height: 50px;\"></div>");
        sb.AppendLine($"<div><strong>{WebUtility.HtmlEncode(d.ClientName)}</strong></div>");
        sb.AppendLine($"<div style=\"font-size:11pt; color:#555;\">Authorized Signatory / Tax Representative</div>");

        if (!string.IsNullOrWhiteSpace(d.PartnerUdin))
        {
            sb.AppendLine($"<div class=\"udin-badge\">ICAI UDIN: {WebUtility.HtmlEncode(d.PartnerUdin)}</div>");
            sb.AppendLine($"<div style=\"font-size:10pt; color:#047857;\">Certified by {WebUtility.HtmlEncode(d.ApprovedByName ?? "Partner")} on {d.ApprovedAt:dd/MM/yyyy HH:mm}</div>");
        }
        else if (d.Status == DefenseDraftStatus.PartnerApproved)
        {
            sb.AppendLine("<div style=\"color:#047857; font-weight:bold; margin-top:8px;\">&#10004; Partner Reviewed &amp; Approved</div>");
        }

        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"clear\"></div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
