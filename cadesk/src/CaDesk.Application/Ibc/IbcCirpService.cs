using CaDesk.Application.Abstractions;
using CaDesk.Domain.Ibc;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Ibc;

public class IbcCirpService : IIbcCirpService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public IbcCirpService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<CirpCaseDto>> GetCasesAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var query = _db.CirpCases
            .Include(x => x.Client)
            .Include(x => x.CocMembers)
            .Include(x => x.Resolutions)
            .Include(x => x.PufeTransactions)
            .Include(x => x.Valuations)
            .AsQueryable();

        if (clientId.HasValue && clientId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return list.Select(x =>
        {
            var timeline = IbcCirpEngine.CalculateCirpTimelines(x.AdmissionDate, today);
            string pan = (x.Client?.TaxIds != null && x.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : (x.Client?.Code ?? "");

            return new CirpCaseDto(
                Id: x.Id,
                ClientId: x.ClientId,
                ClientName: x.Client?.Name ?? "Client",
                ClientPan: pan,
                CorporateDebtorName: x.CorporateDebtorName,
                Cin: x.Cin,
                NcltBench: x.NcltBench,
                CourtOrderNumber: x.CourtOrderNumber,
                AdmissionDate: x.AdmissionDate,
                IrpName: x.IrpName,
                IrpIpRegNumber: x.IrpIpRegNumber,
                RpName: x.RpName,
                RpIpRegNumber: x.RpIpRegNumber,
                MoratoriumSection14Declared: x.MoratoriumSection14Declared,
                ClaimsLastDate: x.ClaimsLastDate,
                Initial180DaysDeadline: x.Initial180DaysDeadline,
                Extended270DaysDeadline: x.Extended270DaysDeadline,
                Final330DaysLimit: x.Final330DaysLimit,
                DaysElapsed: timeline.DaysElapsed,
                DaysRemainingTo180: timeline.DaysRemainingTo180,
                DaysRemainingTo330: timeline.DaysRemainingTo330,
                Is180DaysExpired: timeline.Is180DaysExpired,
                Is330DaysCapBreached: timeline.Is330DaysCapBreached,
                TimelineStatusAlert: timeline.TimelineStatusAlert,
                Stage: x.Stage,
                TotalAdmittedFinancialDebt: x.TotalAdmittedFinancialDebt,
                TotalAdmittedOperationalDebt: x.TotalAdmittedOperationalDebt,
                StatusNote: x.StatusNote,
                MembersCount: x.CocMembers.Count,
                ResolutionsCount: x.Resolutions.Count,
                PufeCount: x.PufeTransactions.Count,
                ValuationsCount: x.Valuations.Count,
                CreatedAt: x.CreatedAt
            );
        }).ToList();
    }

    public async Task<CirpCaseDetailDto?> GetCaseByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await _db.CirpCases
            .Include(c => c.Client)
            .Include(c => c.CocMembers)
            .Include(c => c.Resolutions)
            .Include(c => c.PufeTransactions)
            .Include(c => c.Valuations)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (x == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var timeline = IbcCirpEngine.CalculateCirpTimelines(x.AdmissionDate, today);
        string pan = (x.Client?.TaxIds != null && x.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : (x.Client?.Code ?? "");

        var members = x.CocMembers.OrderByDescending(m => m.VotingSharePct).Select(m => new CocMemberDto(
            Id: m.Id,
            CirpCaseId: m.CirpCaseId,
            CreditorName: m.CreditorName,
            Category: m.Category,
            ClaimSubmittedAmount: m.ClaimSubmittedAmount,
            AdmittedClaimAmount: m.AdmittedClaimAmount,
            VotingSharePct: m.VotingSharePct,
            SecurityInterestDescription: m.SecurityInterestDescription,
            IsRelatedParty: m.IsRelatedParty,
            CreatedAt: m.CreatedAt
        )).ToList();

        var resolutions = x.Resolutions.OrderBy(r => r.MeetingNumber).ThenBy(r => r.ResolutionNumber).Select(r => new CocResolutionItemDto(
            Id: r.Id,
            CirpCaseId: r.CirpCaseId,
            MeetingNumber: r.MeetingNumber,
            ResolutionNumber: r.ResolutionNumber,
            ResolutionTitle: r.ResolutionTitle,
            StatutorySection: r.StatutorySection,
            RequiredThresholdPct: r.RequiredThresholdPct,
            VotingShareInFavourPct: r.VotingShareInFavourPct,
            VotingShareAgainstPct: r.VotingShareAgainstPct,
            VotingShareAbstainedPct: r.VotingShareAbstainedPct,
            Status: r.Status,
            CreatedAt: r.CreatedAt
        )).ToList();

        var pufe = x.PufeTransactions.OrderByDescending(t => t.TransactionDate).Select(t => new PufeTransactionItemDto(
            Id: t.Id,
            CirpCaseId: t.CirpCaseId,
            PufeType: t.PufeType,
            TransactionDate: t.TransactionDate,
            CounterpartyName: t.CounterpartyName,
            IsRelatedParty: t.IsRelatedParty,
            TransactionAmount: t.TransactionAmount,
            EstimatedAvoidanceRecoverableAmount: t.EstimatedAvoidanceRecoverableAmount,
            LookbackPeriodYears: t.LookbackPeriodYears,
            IsWithinLookbackPeriod: t.IsWithinLookbackPeriod,
            ForensicAuditorObservations: t.ForensicAuditorObservations,
            NcltApplicationFiled: t.NcltApplicationFiled,
            CreatedAt: t.CreatedAt
        )).ToList();

        var vals = x.Valuations.OrderBy(v => v.AssetClass).Select(v => new AssetValuationItemDto(
            Id: v.Id,
            CirpCaseId: v.CirpCaseId,
            AssetClass: v.AssetClass,
            AssetDescription: v.AssetDescription,
            Valuer1Name: v.Valuer1Name,
            Valuer1FairValue: v.Valuer1FairValue,
            Valuer1LiquidationValue: v.Valuer1LiquidationValue,
            Valuer2Name: v.Valuer2Name,
            Valuer2FairValue: v.Valuer2FairValue,
            Valuer2LiquidationValue: v.Valuer2LiquidationValue,
            AverageFairValue: v.AverageFairValue,
            AverageLiquidationValue: v.AverageLiquidationValue,
            ThirdValuerRequired: v.ThirdValuerRequired,
            CreatedAt: v.CreatedAt
        )).ToList();

        return new CirpCaseDetailDto(
            Id: x.Id,
            ClientId: x.ClientId,
            ClientName: x.Client?.Name ?? "Client",
            ClientPan: pan,
            CorporateDebtorName: x.CorporateDebtorName,
            Cin: x.Cin,
            NcltBench: x.NcltBench,
            CourtOrderNumber: x.CourtOrderNumber,
            AdmissionDate: x.AdmissionDate,
            IrpName: x.IrpName,
            IrpIpRegNumber: x.IrpIpRegNumber,
            RpName: x.RpName,
            RpIpRegNumber: x.RpIpRegNumber,
            MoratoriumSection14Declared: x.MoratoriumSection14Declared,
            ClaimsLastDate: x.ClaimsLastDate,
            Initial180DaysDeadline: x.Initial180DaysDeadline,
            Extended270DaysDeadline: x.Extended270DaysDeadline,
            Final330DaysLimit: x.Final330DaysLimit,
            DaysElapsed: timeline.DaysElapsed,
            DaysRemainingTo180: timeline.DaysRemainingTo180,
            DaysRemainingTo330: timeline.DaysRemainingTo330,
            Is180DaysExpired: timeline.Is180DaysExpired,
            Is330DaysCapBreached: timeline.Is330DaysCapBreached,
            TimelineStatusAlert: timeline.TimelineStatusAlert,
            Stage: x.Stage,
            TotalAdmittedFinancialDebt: x.TotalAdmittedFinancialDebt,
            TotalAdmittedOperationalDebt: x.TotalAdmittedOperationalDebt,
            StatusNote: x.StatusNote,
            CocMembers: members,
            Resolutions: resolutions,
            PufeTransactions: pufe,
            Valuations: vals,
            CreatedAt: x.CreatedAt
        );
    }

    public async Task<CirpCaseDetailDto> SaveCaseAsync(SaveCirpCaseRequest req, CancellationToken ct = default)
    {
        CirpCase? entity = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.CirpCases
                .Include(c => c.CocMembers)
                .Include(c => c.Resolutions)
                .Include(c => c.PufeTransactions)
                .Include(c => c.Valuations)
                .FirstOrDefaultAsync(c => c.Id == req.Id.Value, ct);
        }

        if (entity == null)
        {
            entity = new CirpCase
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                CreatedAt = DateTime.UtcNow
            };
            _db.CirpCases.Add(entity);
        }

        entity.CorporateDebtorName = req.CorporateDebtorName;
        entity.Cin = req.Cin;
        entity.NcltBench = req.NcltBench;
        entity.CourtOrderNumber = req.CourtOrderNumber;
        entity.AdmissionDate = req.AdmissionDate;
        entity.IrpName = req.IrpName;
        entity.IrpIpRegNumber = req.IrpIpRegNumber;
        entity.RpName = req.RpName;
        entity.RpIpRegNumber = req.RpIpRegNumber;
        entity.MoratoriumSection14Declared = req.MoratoriumSection14Declared;
        entity.Stage = req.Stage;
        entity.StatusNote = req.StatusNote;

        // Auto-calculate statutory milestones
        entity.ClaimsLastDate = req.AdmissionDate.AddDays(14);
        entity.Initial180DaysDeadline = req.AdmissionDate.AddDays(180);
        entity.Extended270DaysDeadline = req.AdmissionDate.AddDays(270);
        entity.Final330DaysLimit = req.AdmissionDate.AddDays(330);
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (await GetCaseByIdAsync(entity.Id, ct))!;
    }

    public async Task<bool> DeleteCaseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.CirpCases.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null) return false;

        _db.CirpCases.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CocMemberDto> SaveCocMemberAsync(SaveCocMemberRequest req, CancellationToken ct = default)
    {
        var cirpCase = await _db.CirpCases
            .Include(c => c.CocMembers)
            .FirstOrDefaultAsync(c => c.Id == req.CirpCaseId, ct);

        if (cirpCase == null)
            throw new InvalidOperationException($"CIRP Case {req.CirpCaseId} not found");

        CocMember? member = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            member = cirpCase.CocMembers.FirstOrDefault(m => m.Id == req.Id.Value);
        }

        if (member == null)
        {
            member = new CocMember
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                CirpCaseId = req.CirpCaseId,
                CreatedAt = DateTime.UtcNow
            };
            cirpCase.CocMembers.Add(member);
            _db.CocMembers.Add(member);
        }

        member.CreditorName = req.CreditorName;
        member.Category = req.Category;
        member.ClaimSubmittedAmount = req.ClaimSubmittedAmount;
        member.AdmittedClaimAmount = req.AdmittedClaimAmount;
        member.SecurityInterestDescription = req.SecurityInterestDescription;
        member.IsRelatedParty = req.IsRelatedParty;
        member.UpdatedAt = DateTime.UtcNow;

        // Recalculate CoC voting shares across all members
        IbcCirpEngine.CalculateCocVotingShares(cirpCase.CocMembers);

        // Update total debt figures on case
        cirpCase.TotalAdmittedFinancialDebt = cirpCase.CocMembers
            .Where(m => m.Category == CreditorCategory.SecuredFinancialCreditor || m.Category == CreditorCategory.UnsecuredFinancialCreditor)
            .Sum(m => m.AdmittedClaimAmount);

        cirpCase.TotalAdmittedOperationalDebt = cirpCase.CocMembers
            .Where(m => m.Category != CreditorCategory.SecuredFinancialCreditor && m.Category != CreditorCategory.UnsecuredFinancialCreditor)
            .Sum(m => m.AdmittedClaimAmount);

        await _db.SaveChangesAsync(ct);

        return new CocMemberDto(
            Id: member.Id,
            CirpCaseId: member.CirpCaseId,
            CreditorName: member.CreditorName,
            Category: member.Category,
            ClaimSubmittedAmount: member.ClaimSubmittedAmount,
            AdmittedClaimAmount: member.AdmittedClaimAmount,
            VotingSharePct: member.VotingSharePct,
            SecurityInterestDescription: member.SecurityInterestDescription,
            IsRelatedParty: member.IsRelatedParty,
            CreatedAt: member.CreatedAt
        );
    }

    public async Task<bool> DeleteCocMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        var member = await _db.CocMembers
            .Include(m => m.CirpCase)
            .ThenInclude(c => c!.CocMembers)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);

        if (member == null) return false;

        var cirpCase = member.CirpCase;
        _db.CocMembers.Remove(member);

        if (cirpCase != null)
        {
            cirpCase.CocMembers.Remove(member);
            IbcCirpEngine.CalculateCocVotingShares(cirpCase.CocMembers);

            cirpCase.TotalAdmittedFinancialDebt = cirpCase.CocMembers
                .Where(m => m.Category == CreditorCategory.SecuredFinancialCreditor || m.Category == CreditorCategory.UnsecuredFinancialCreditor)
                .Sum(m => m.AdmittedClaimAmount);

            cirpCase.TotalAdmittedOperationalDebt = cirpCase.CocMembers
                .Where(m => m.Category != CreditorCategory.SecuredFinancialCreditor && m.Category != CreditorCategory.UnsecuredFinancialCreditor)
                .Sum(m => m.AdmittedClaimAmount);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CocResolutionItemDto> SaveResolutionAsync(SaveCocResolutionRequest req, CancellationToken ct = default)
    {
        var cirpCase = await _db.CirpCases
            .Include(c => c.Resolutions)
            .FirstOrDefaultAsync(c => c.Id == req.CirpCaseId, ct);

        if (cirpCase == null)
            throw new InvalidOperationException($"CIRP Case {req.CirpCaseId} not found");

        CocResolutionItem? item = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            item = cirpCase.Resolutions.FirstOrDefault(r => r.Id == req.Id.Value);
        }

        if (item == null)
        {
            item = new CocResolutionItem
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                CirpCaseId = req.CirpCaseId,
                CreatedAt = DateTime.UtcNow
            };
            cirpCase.Resolutions.Add(item);
            _db.CocResolutions.Add(item);
        }

        item.MeetingNumber = req.MeetingNumber;
        item.ResolutionNumber = req.ResolutionNumber;
        item.ResolutionTitle = req.ResolutionTitle;
        item.StatutorySection = req.StatutorySection;
        item.RequiredThresholdPct = req.RequiredThresholdPct;
        item.VotingShareInFavourPct = req.VotingShareInFavourPct;
        item.VotingShareAgainstPct = req.VotingShareAgainstPct;
        item.VotingShareAbstainedPct = req.VotingShareAbstainedPct;
        item.Status = IbcCirpEngine.EvaluateResolution(req.RequiredThresholdPct, req.VotingShareInFavourPct);
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new CocResolutionItemDto(
            Id: item.Id,
            CirpCaseId: item.CirpCaseId,
            MeetingNumber: item.MeetingNumber,
            ResolutionNumber: item.ResolutionNumber,
            ResolutionTitle: item.ResolutionTitle,
            StatutorySection: item.StatutorySection,
            RequiredThresholdPct: item.RequiredThresholdPct,
            VotingShareInFavourPct: item.VotingShareInFavourPct,
            VotingShareAgainstPct: item.VotingShareAgainstPct,
            VotingShareAbstainedPct: item.VotingShareAbstainedPct,
            Status: item.Status,
            CreatedAt: item.CreatedAt
        );
    }

    public async Task<bool> DeleteResolutionAsync(Guid resId, CancellationToken ct = default)
    {
        var res = await _db.CocResolutions.FirstOrDefaultAsync(r => r.Id == resId, ct);
        if (res == null) return false;

        _db.CocResolutions.Remove(res);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PufeTransactionItemDto> SavePufeTransactionAsync(SavePufeTransactionRequest req, CancellationToken ct = default)
    {
        var cirpCase = await _db.CirpCases
            .Include(c => c.PufeTransactions)
            .FirstOrDefaultAsync(c => c.Id == req.CirpCaseId, ct);

        if (cirpCase == null)
            throw new InvalidOperationException($"CIRP Case {req.CirpCaseId} not found");

        PufeTransactionItem? item = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            item = cirpCase.PufeTransactions.FirstOrDefault(p => p.Id == req.Id.Value);
        }

        if (item == null)
        {
            item = new PufeTransactionItem
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                CirpCaseId = req.CirpCaseId,
                CreatedAt = DateTime.UtcNow
            };
            cirpCase.PufeTransactions.Add(item);
            _db.PufeTransactions.Add(item);
        }

        item.PufeType = req.PufeType;
        item.TransactionDate = req.TransactionDate;
        item.CounterpartyName = req.CounterpartyName;
        item.IsRelatedParty = req.IsRelatedParty;
        item.TransactionAmount = req.TransactionAmount;
        item.EstimatedAvoidanceRecoverableAmount = req.EstimatedAvoidanceRecoverableAmount;
        item.ForensicAuditorObservations = req.ForensicAuditorObservations;
        item.NcltApplicationFiled = req.NcltApplicationFiled;

        int lookbackYears = 1;
        if (req.PufeType == PufeType.ExtortionateCreditSec50) lookbackYears = 2;
        else if (req.IsRelatedParty) lookbackYears = 2;

        item.LookbackPeriodYears = lookbackYears;
        item.IsWithinLookbackPeriod = IbcCirpEngine.EvaluatePufeLookback(cirpCase.AdmissionDate, req.TransactionDate, req.IsRelatedParty, req.PufeType);
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new PufeTransactionItemDto(
            Id: item.Id,
            CirpCaseId: item.CirpCaseId,
            PufeType: item.PufeType,
            TransactionDate: item.TransactionDate,
            CounterpartyName: item.CounterpartyName,
            IsRelatedParty: item.IsRelatedParty,
            TransactionAmount: item.TransactionAmount,
            EstimatedAvoidanceRecoverableAmount: item.EstimatedAvoidanceRecoverableAmount,
            LookbackPeriodYears: item.LookbackPeriodYears,
            IsWithinLookbackPeriod: item.IsWithinLookbackPeriod,
            ForensicAuditorObservations: item.ForensicAuditorObservations,
            NcltApplicationFiled: item.NcltApplicationFiled,
            CreatedAt: item.CreatedAt
        );
    }

    public async Task<bool> DeletePufeTransactionAsync(Guid pufeId, CancellationToken ct = default)
    {
        var item = await _db.PufeTransactions.FirstOrDefaultAsync(p => p.Id == pufeId, ct);
        if (item == null) return false;

        _db.PufeTransactions.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AssetValuationItemDto> SaveValuationAsync(SaveAssetValuationRequest req, CancellationToken ct = default)
    {
        var cirpCase = await _db.CirpCases
            .Include(c => c.Valuations)
            .FirstOrDefaultAsync(c => c.Id == req.CirpCaseId, ct);

        if (cirpCase == null)
            throw new InvalidOperationException($"CIRP Case {req.CirpCaseId} not found");

        AssetValuationItem? item = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            item = cirpCase.Valuations.FirstOrDefault(v => v.Id == req.Id.Value);
        }

        if (item == null)
        {
            item = new AssetValuationItem
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                CirpCaseId = req.CirpCaseId,
                CreatedAt = DateTime.UtcNow
            };
            cirpCase.Valuations.Add(item);
            _db.AssetValuations.Add(item);
        }

        item.AssetClass = req.AssetClass;
        item.AssetDescription = req.AssetDescription;
        item.Valuer1Name = req.Valuer1Name;
        item.Valuer1FairValue = req.Valuer1FairValue;
        item.Valuer1LiquidationValue = req.Valuer1LiquidationValue;
        item.Valuer2Name = req.Valuer2Name;
        item.Valuer2FairValue = req.Valuer2FairValue;
        item.Valuer2LiquidationValue = req.Valuer2LiquidationValue;

        var (avgFair, avgLiq, thirdValuer) = IbcCirpEngine.ComputeValuation(
            req.Valuer1FairValue, req.Valuer1LiquidationValue,
            req.Valuer2FairValue, req.Valuer2LiquidationValue);

        item.AverageFairValue = avgFair;
        item.AverageLiquidationValue = avgLiq;
        item.ThirdValuerRequired = thirdValuer;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new AssetValuationItemDto(
            Id: item.Id,
            CirpCaseId: item.CirpCaseId,
            AssetClass: item.AssetClass,
            AssetDescription: item.AssetDescription,
            Valuer1Name: item.Valuer1Name,
            Valuer1FairValue: item.Valuer1FairValue,
            Valuer1LiquidationValue: item.Valuer1LiquidationValue,
            Valuer2Name: item.Valuer2Name,
            Valuer2FairValue: item.Valuer2FairValue,
            Valuer2LiquidationValue: item.Valuer2LiquidationValue,
            AverageFairValue: item.AverageFairValue,
            AverageLiquidationValue: item.AverageLiquidationValue,
            ThirdValuerRequired: item.ThirdValuerRequired,
            CreatedAt: item.CreatedAt
        );
    }

    public async Task<bool> DeleteValuationAsync(Guid valId, CancellationToken ct = default)
    {
        var item = await _db.AssetValuations.FirstOrDefaultAsync(v => v.Id == valId, ct);
        if (item == null) return false;

        _db.AssetValuations.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string> GenerateInformationMemorandumHtmlAsync(Guid caseId, CancellationToken ct = default)
    {
        var c = await _db.CirpCases
            .Include(x => x.Client)
            .Include(x => x.CocMembers)
            .Include(x => x.Valuations)
            .Include(x => x.PufeTransactions)
            .FirstOrDefaultAsync(x => x.Id == caseId, ct);

        if (c == null)
            throw new InvalidOperationException($"CIRP Case {caseId} not found");

        return IbcCirpEngine.GenerateInformationMemorandumHtml(c, c.CocMembers, c.Valuations, c.PufeTransactions);
    }

    public CirpTimelineCalculationDto CalculateTimelines(CalculateCirpTimelineRequest req)
    {
        var t = IbcCirpEngine.CalculateCirpTimelines(req.AdmissionDate, req.AsOfDate);
        return new CirpTimelineCalculationDto(
            AdmissionDate: t.AdmissionDate,
            ClaimsLastDate: t.ClaimsLastDate,
            Initial180Days: t.Initial180Days,
            Extended270Days: t.Extended270Days,
            Final330Days: t.Final330Days,
            DaysElapsed: t.DaysElapsed,
            DaysRemainingTo180: t.DaysRemainingTo180,
            DaysRemainingTo330: t.DaysRemainingTo330,
            Is180DaysExpired: t.Is180DaysExpired,
            Is330DaysCapBreached: t.Is330DaysCapBreached,
            TimelineStatusAlert: t.TimelineStatusAlert
        );
    }
}
