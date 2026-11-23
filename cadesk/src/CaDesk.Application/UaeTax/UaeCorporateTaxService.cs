using System.Text;
using System.Text.Json;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.UaeTax;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.UaeTax;

public class UaeCorporateTaxService : IUaeCorporateTaxService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public UaeCorporateTaxService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<UaeTaxProfileDto>> GetProfilesAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var query = _db.UaeTaxProfiles
            .AsNoTracking()
            .Include(p => p.Client)
            .Where(p => p.FirmId == _tenant.FirmId);

        if (clientId.HasValue && clientId.Value != Guid.Empty)
        {
            query = query.Where(p => p.ClientId == clientId.Value);
        }

        var list = await query.OrderBy(p => p.Client != null ? p.Client.Name : "").ToListAsync(ct);
        return list.Select(MapToProfileDto).ToList();
    }

    public async Task<UaeTaxProfileDto?> GetProfileByIdAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _db.UaeTaxProfiles
            .AsNoTracking()
            .Include(p => p.Client)
            .FirstOrDefaultAsync(p => p.Id == id && p.FirmId == _tenant.FirmId, ct);

        return profile is null ? null : MapToProfileDto(profile);
    }

    public async Task<UaeTaxProfileDto> CreateOrUpdateProfileAsync(CreateOrUpdateTaxProfileRequest req, CancellationToken ct = default)
    {
        var cleanTrn = (req.CorporateTaxTrn ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(cleanTrn) && !UaeTaxEngine.ValidateTrn(cleanTrn))
        {
            throw new ArgumentException("UAE Corporate Tax TRN must be 15 digits starting with 100 (e.g. 100123456789003).", nameof(req));
        }

        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == req.ClientId && c.FirmId == _tenant.FirmId, ct);

        if (client is null)
            throw new KeyNotFoundException($"Client {req.ClientId} not found.");

        var profile = await _db.UaeTaxProfiles
            .FirstOrDefaultAsync(p => p.ClientId == req.ClientId && p.FirmId == _tenant.FirmId, ct);

        if (profile is null)
        {
            profile = new UaeTaxProfile
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                CorporateTaxTrn = cleanTrn,
                Emirate = req.Emirate?.Trim() ?? "Dubai",
                FreeZoneName = req.FreeZoneName?.Trim() ?? "Mainland",
                Category = req.Category,
                FreeZoneRegime = req.FreeZoneRegime,
                FinancialYearStartMonth = req.FinancialYearStartMonth >= 1 && req.FinancialYearStartMonth <= 12 ? req.FinancialYearStartMonth : 1,
                IsMneGroupConstituent = req.IsMneGroupConstituent,
                AccountingStandard = req.AccountingStandard?.Trim() ?? "IFRS",
                AuditedFinancialsAvailable = req.AuditedFinancialsAvailable,
                CreatedAt = DateTime.UtcNow
            };
            _db.UaeTaxProfiles.Add(profile);
        }
        else
        {
            profile.CorporateTaxTrn = cleanTrn;
            profile.Emirate = req.Emirate?.Trim() ?? profile.Emirate;
            profile.FreeZoneName = req.FreeZoneName?.Trim() ?? profile.FreeZoneName;
            profile.Category = req.Category;
            profile.FreeZoneRegime = req.FreeZoneRegime;
            profile.FinancialYearStartMonth = req.FinancialYearStartMonth;
            profile.IsMneGroupConstituent = req.IsMneGroupConstituent;
            profile.AccountingStandard = req.AccountingStandard?.Trim() ?? profile.AccountingStandard;
            profile.AuditedFinancialsAvailable = req.AuditedFinancialsAvailable;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        profile.Client = client;

        return MapToProfileDto(profile);
    }

    public UaeTaxEngine.ComputationResult CalculateTax(CalculateUaeTaxRequest req)
    {
        return UaeTaxEngine.Compute(
            category: req.Category,
            grossRevenue: req.GrossRevenue,
            netAccountingProfit: req.NetAccountingProfitLoss,
            sbrElection: req.SmallBusinessReliefElection,
            priorYear1Revenue: req.PriorYear1Revenue,
            priorYear2Revenue: req.PriorYear2Revenue,
            isMneConstituent: req.IsMneConstituent,
            qualifyingRevenue: req.QualifyingRevenue,
            nonQualifyingRevenue: req.NonQualifyingRevenue,
            exemptIncome: req.ExemptIncome,
            unrealizedGainsLossesAdj: req.UnrealizedGainsLossesAdjustment,
            entertainmentExpense: req.EntertainmentExpenditureTotal,
            finesAndPenalties: req.FinesAndPenaltiesDisallowed,
            recoverableVat: req.RecoverableVatDisallowed,
            unapprovedDonations: req.DonationsToUnapprovedEntitiesDisallowed,
            netInterestExpense: req.NetInterestExpense,
            adjustedEbitda: req.AdjustedEbitda,
            broughtForwardLosses: req.BroughtForwardTaxLosses,
            foreignTaxCredit: req.ForeignTaxCreditReliefAed,
            exchangeRateToInr: req.ExchangeRateToInr
        );
    }

    public async Task<UaeCorporateTaxReturnDto> SaveReturnAsync(SaveUaeCorporateTaxReturnRequest req, CancellationToken ct = default)
    {
        var profile = await _db.UaeTaxProfiles
            .Include(p => p.Client)
            .FirstOrDefaultAsync(p => p.Id == req.TaxProfileId && p.FirmId == _tenant.FirmId, ct);

        if (profile is null)
            throw new KeyNotFoundException($"UAE Tax Profile {req.TaxProfileId} not found.");

        var count = await _db.UaeCorporateTaxReturns.CountAsync(r => r.FirmId == _tenant.FirmId, ct);
        var reference = $"CT-{req.ReturnYear}-{count + 1:D4}";

        var comp = UaeTaxEngine.Compute(
            category: profile.Category,
            grossRevenue: req.GrossRevenue,
            netAccountingProfit: req.NetAccountingProfitLoss,
            sbrElection: req.SmallBusinessReliefElection,
            priorYear1Revenue: req.PriorYear1Revenue,
            priorYear2Revenue: req.PriorYear2Revenue,
            isMneConstituent: profile.IsMneGroupConstituent,
            qualifyingRevenue: req.QualifyingRevenue,
            nonQualifyingRevenue: req.NonQualifyingRevenue,
            exemptIncome: req.ExemptIncome,
            unrealizedGainsLossesAdj: req.UnrealizedGainsLossesAdjustment,
            entertainmentExpense: req.EntertainmentExpenditureTotal,
            finesAndPenalties: req.FinesAndPenaltiesDisallowed,
            recoverableVat: req.RecoverableVatDisallowed,
            unapprovedDonations: req.DonationsToUnapprovedEntitiesDisallowed,
            netInterestExpense: req.NetInterestExpense,
            adjustedEbitda: req.AdjustedEbitda,
            broughtForwardLosses: req.BroughtForwardTaxLosses,
            foreignTaxCredit: req.ForeignTaxCreditReliefAed,
            exchangeRateToInr: req.ExchangeRateToInr
        );

        var dueDate = req.TaxPeriodEnd.AddMonths(9);

        var ret = new UaeCorporateTaxReturn
        {
            Id = Guid.NewGuid(),
            FirmId = _tenant.FirmId,
            TaxProfileId = profile.Id,
            ClientId = profile.ClientId,
            ReturnReference = reference,
            ReturnYear = req.ReturnYear,
            TaxPeriodStart = req.TaxPeriodStart,
            TaxPeriodEnd = req.TaxPeriodEnd,
            DueDate = dueDate,
            Status = ReturnFilingStatus.Draft,
            FunctionalCurrency = "AED",
            ExchangeRateToInr = req.ExchangeRateToInr > 0 ? req.ExchangeRateToInr : 22.85m,

            GrossRevenue = req.GrossRevenue,
            CostOfSales = req.CostOfSales,
            OperatingExpenses = req.OperatingExpenses,
            NetAccountingProfitLoss = req.NetAccountingProfitLoss,

            SmallBusinessReliefElection = req.SmallBusinessReliefElection,
            PriorYear1Revenue = req.PriorYear1Revenue,
            PriorYear2Revenue = req.PriorYear2Revenue,

            IsQfzp = profile.Category == UaeTaxpayerCategory.QualifyingFreeZonePerson,
            QualifyingRevenue = req.QualifyingRevenue,
            NonQualifyingRevenue = req.NonQualifyingRevenue,
            DeMinimisThreshold = comp.DeMinimisThresholdAed,
            DeMinimisSatisfied = comp.QfzpDeMinimisPassed,
            QualifyingIncome = comp.IncomeSubjectToZeroPctAed,
            TaxableNonQualifyingIncome = comp.IncomeSubjectToNinePctAed,

            ExemptIncome = req.ExemptIncome,
            UnrealizedGainsLossesAdjustment = req.UnrealizedGainsLossesAdjustment,
            EntertainmentExpenditureTotal = req.EntertainmentExpenditureTotal,
            EntertainmentExpenditureDisallowed = comp.DisallowedEntertainmentAed,
            FinesAndPenaltiesDisallowed = req.FinesAndPenaltiesDisallowed,
            RecoverableVatDisallowed = req.RecoverableVatDisallowed,
            DonationsToUnapprovedEntitiesDisallowed = req.DonationsToUnapprovedEntitiesDisallowed,

            NetInterestExpense = req.NetInterestExpense,
            AdjustedEbitda = req.AdjustedEbitda,
            InterestCapThreshold = Math.Max(UaeTaxEngine.InterestDeMinimisThresholdAed, Math.Max(0, req.AdjustedEbitda) * UaeTaxEngine.MaxInterestEbitdaRatio),
            DisallowedInterestExpense = comp.DisallowedInterestAed,

            BroughtForwardTaxLosses = req.BroughtForwardTaxLosses,
            TaxLossesUtilized = comp.TaxLossesUtilizedAed,
            CarriedForwardTaxLosses = comp.RemainingTaxLossesAed,

            AdjustedTaxableIncome = comp.AdjustedTaxableIncomeAed,
            TaxableIncomeSubjectToZeroPct = comp.IncomeSubjectToZeroPctAed,
            TaxableIncomeSubjectToNinePct = comp.IncomeSubjectToNinePctAed,
            CorporateTaxPayableAed = comp.GrossTaxPayableAed,
            ForeignTaxCreditReliefAed = req.ForeignTaxCreditReliefAed,
            NetTaxPayableAed = comp.NetTaxPayableAed,

            PreparedByUserId = _tenant.UserId,
            PreparedByName = "CA Staff",
            CreatedAt = DateTime.UtcNow
        };

        ret.SubmissionPayloadJson = GenerateEmaraTaxJson(ret, profile);

        _db.UaeCorporateTaxReturns.Add(ret);
        await _db.SaveChangesAsync(ct);

        ret.Client = profile.Client;
        ret.TaxProfile = profile;

        return MapToReturnDto(ret);
    }

    public async Task<UaeCorporateTaxReturnDto?> GetReturnByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ret = await _db.UaeCorporateTaxReturns
            .AsNoTracking()
            .Include(r => r.Client)
            .Include(r => r.TaxProfile)
            .FirstOrDefaultAsync(r => r.Id == id && r.FirmId == _tenant.FirmId && r.DeletedAt == null, ct);

        return ret is null ? null : MapToReturnDto(ret);
    }

    public async Task<List<UaeCorporateTaxReturnDto>> GetReturnsForProfileAsync(Guid profileId, CancellationToken ct = default)
    {
        var list = await _db.UaeCorporateTaxReturns
            .AsNoTracking()
            .Include(r => r.Client)
            .Include(r => r.TaxProfile)
            .Where(r => r.TaxProfileId == profileId && r.FirmId == _tenant.FirmId && r.DeletedAt == null)
            .OrderByDescending(r => r.ReturnYear)
            .ToListAsync(ct);

        return list.Select(MapToReturnDto).ToList();
    }

    public async Task<UaeCorporateTaxReturnDto> ApproveReturnAsync(Guid returnId, ApproveUaeReturnRequest req, CancellationToken ct = default)
    {
        var ret = await _db.UaeCorporateTaxReturns
            .Include(r => r.Client)
            .Include(r => r.TaxProfile)
            .FirstOrDefaultAsync(r => r.Id == returnId && r.FirmId == _tenant.FirmId && r.DeletedAt == null, ct);

        if (ret is null)
            throw new KeyNotFoundException($"UAE Corporate Tax Return {returnId} not found.");

        ret.Status = ReturnFilingStatus.PartnerApproved;
        ret.ApprovedByUserId = _tenant.UserId;
        ret.ApprovedByName = "Managing Partner";
        ret.ApprovedAt = DateTimeOffset.UtcNow;
        ret.PartnerNotes = req.PartnerNotes?.Trim();
        ret.FtaSubmissionReference = $"FTA-CT-{ret.ReturnYear}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        ret.UpdatedAt = DateTime.UtcNow;

        if (ret.TaxProfile is not null)
        {
            ret.SubmissionPayloadJson = GenerateEmaraTaxJson(ret, ret.TaxProfile);
        }

        await _db.SaveChangesAsync(ct);

        return MapToReturnDto(ret);
    }

    public async Task<EmaraTaxSubmissionExportDto> ExportEmaraTaxPackageAsync(Guid returnId, CancellationToken ct = default)
    {
        var ret = await _db.UaeCorporateTaxReturns
            .AsNoTracking()
            .Include(r => r.Client)
            .Include(r => r.TaxProfile)
            .FirstOrDefaultAsync(r => r.Id == returnId && r.FirmId == _tenant.FirmId && r.DeletedAt == null, ct);

        if (ret is null)
            throw new KeyNotFoundException($"UAE Corporate Tax Return {returnId} not found.");

        var clientName = ret.Client?.Name ?? "UAE Entity";
        var trn = ret.TaxProfile?.CorporateTaxTrn ?? "100000000000000";

        var json = ret.SubmissionPayloadJson ?? GenerateEmaraTaxJson(ret, ret.TaxProfile ?? new UaeTaxProfile());
        var xml = GenerateEmaraTaxXml(ret, clientName, trn);

        return new EmaraTaxSubmissionExportDto(
            ReturnReference: ret.ReturnReference,
            CorporateTaxTrn: trn,
            LegalEntityName: clientName,
            FormVersion: "Form CT01 v1.0",
            SubmissionXml: xml,
            SubmissionJson: json,
            TotalTaxPayableAed: ret.NetTaxPayableAed,
            TotalTaxPayableInr: ret.NetTaxPayableInr
        );
    }

    private static UaeTaxProfileDto MapToProfileDto(UaeTaxProfile p) => new(
        p.Id,
        p.ClientId,
        p.Client?.Name ?? "Client",
        p.Client?.Code ?? "",
        p.CorporateTaxTrn,
        p.Emirate,
        p.FreeZoneName,
        p.Category,
        p.FreeZoneRegime,
        p.FinancialYearStartMonth,
        p.IsMneGroupConstituent,
        p.AccountingStandard,
        p.AuditedFinancialsAvailable,
        p.CreatedAt
    );

    private static UaeCorporateTaxReturnDto MapToReturnDto(UaeCorporateTaxReturn r) => new(
        r.Id,
        r.TaxProfileId,
        r.ClientId,
        r.Client?.Name ?? "Client",
        r.TaxProfile?.CorporateTaxTrn ?? "",
        r.ReturnReference,
        r.ReturnYear,
        r.TaxPeriodStart,
        r.TaxPeriodEnd,
        r.DueDate,
        r.Status,
        r.FunctionalCurrency,
        r.ExchangeRateToInr,

        r.GrossRevenue,
        r.CostOfSales,
        r.GrossProfit,
        r.OperatingExpenses,
        r.NetAccountingProfitLoss,

        r.SmallBusinessReliefElection,
        r.GrossRevenue <= UaeTaxEngine.SbrRevenueCeilingAed && !r.IsQfzp && r.SmallBusinessReliefElection != SmallBusinessReliefStatus.NotEligible,
        r.PriorYear1Revenue,
        r.PriorYear2Revenue,

        r.IsQfzp,
        r.QualifyingRevenue,
        r.NonQualifyingRevenue,
        r.DeMinimisThreshold,
        r.DeMinimisSatisfied,
        r.QualifyingIncome,
        r.TaxableNonQualifyingIncome,

        r.ExemptIncome,
        r.UnrealizedGainsLossesAdjustment,
        r.EntertainmentExpenditureTotal,
        r.EntertainmentExpenditureDisallowed,
        r.FinesAndPenaltiesDisallowed,
        r.RecoverableVatDisallowed,
        r.DonationsToUnapprovedEntitiesDisallowed,

        r.NetInterestExpense,
        r.AdjustedEbitda,
        r.InterestCapThreshold,
        r.DisallowedInterestExpense,

        r.BroughtForwardTaxLosses,
        r.TaxLossesUtilized,
        r.CarriedForwardTaxLosses,

        r.AdjustedTaxableIncome,
        r.TaxableIncomeSubjectToZeroPct,
        r.TaxableIncomeSubjectToNinePct,
        r.CorporateTaxPayableAed,
        r.ForeignTaxCreditReliefAed,
        r.NetTaxPayableAed,
        r.NetTaxPayableInr,

        r.FtaSubmissionReference,
        r.PreparedByName,
        r.ApprovedByName,
        r.ApprovedAt,
        r.PartnerNotes,
        r.CreatedAt
    );

    private static string GenerateEmaraTaxJson(UaeCorporateTaxReturn r, UaeTaxProfile p)
    {
        var payload = new
        {
            declarationHeader = new
            {
                formType = "CT01",
                version = "1.0",
                taxYear = r.ReturnYear,
                trn = p.CorporateTaxTrn,
                emirate = p.Emirate,
                freeZoneName = p.FreeZoneName,
                taxpayerCategory = p.Category.ToString(),
                taxPeriodStart = r.TaxPeriodStart.ToString("yyyy-MM-dd"),
                taxPeriodEnd = r.TaxPeriodEnd.ToString("yyyy-MM-dd"),
                dueDate = r.DueDate.ToString("yyyy-MM-dd"),
                functionalCurrency = "AED"
            },
            accountingProfit = new
            {
                grossRevenue = r.GrossRevenue,
                costOfSales = r.CostOfSales,
                operatingExpenses = r.OperatingExpenses,
                netProfitLoss = r.NetAccountingProfitLoss
            },
            smallBusinessRelief = new
            {
                electionClaimed = r.SmallBusinessReliefElection == SmallBusinessReliefStatus.Elected,
                currentPeriodRevenue = r.GrossRevenue,
                priorYear1Revenue = r.PriorYear1Revenue,
                priorYear2Revenue = r.PriorYear2Revenue,
                reliefStatus = r.SmallBusinessReliefElection.ToString()
            },
            qfzpSchedule = new
            {
                isQfzp = r.IsQfzp,
                qualifyingRevenue = r.QualifyingRevenue,
                nonQualifyingRevenue = r.NonQualifyingRevenue,
                deMinimisThreshold = r.DeMinimisThreshold,
                deMinimisPassed = r.DeMinimisSatisfied,
                qualifyingIncomeAt0Pct = r.QualifyingIncome,
                nonQualifyingIncomeAt9Pct = r.TaxableNonQualifyingIncome
            },
            taxAdjustments = new
            {
                exemptIncomeArt22_23 = r.ExemptIncome,
                unrealizedGainsLossesArt20 = r.UnrealizedGainsLossesAdjustment,
                disallowedEntertainmentArt32 = r.EntertainmentExpenditureDisallowed,
                disallowedFinesPenaltiesArt33 = r.FinesAndPenaltiesDisallowed,
                disallowedRecoverableVatArt33 = r.RecoverableVatDisallowed,
                disallowedInterestArt30 = r.DisallowedInterestExpense
            },
            taxLossSchedule = new
            {
                broughtForwardLosses = r.BroughtForwardTaxLosses,
                utilizedLosses = r.TaxLossesUtilized,
                carriedForwardLosses = r.CarriedForwardTaxLosses
            },
            taxComputation = new
            {
                adjustedTaxableIncome = r.AdjustedTaxableIncome,
                taxableIncome0Pct = r.TaxableIncomeSubjectToZeroPct,
                taxableIncome9Pct = r.TaxableIncomeSubjectToNinePct,
                grossTaxPayable = r.CorporateTaxPayableAed,
                foreignTaxCredit = r.ForeignTaxCreditReliefAed,
                netTaxPayableAed = r.NetTaxPayableAed,
                exchangeRateToInr = r.ExchangeRateToInr,
                netTaxPayableInr = r.NetTaxPayableInr
            },
            attestation = new
            {
                approvedBy = r.ApprovedByName,
                approvedAt = r.ApprovedAt,
                ftaReference = r.FtaSubmissionReference
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GenerateEmaraTaxXml(UaeCorporateTaxReturn r, string legalName, string trn)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<EmaraTaxReturn xmlns=\"urn:fta:emaratax:ct01:v1\">");
        sb.AppendLine("  <Header>");
        sb.AppendLine($"    <ReturnReference>{r.ReturnReference}</ReturnReference>");
        sb.AppendLine($"    <TRN>{trn}</TRN>");
        sb.AppendLine($"    <LegalName>{System.Net.WebUtility.HtmlEncode(legalName)}</LegalName>");
        sb.AppendLine($"    <TaxPeriodStart>{r.TaxPeriodStart:yyyy-MM-dd}</TaxPeriodStart>");
        sb.AppendLine($"    <TaxPeriodEnd>{r.TaxPeriodEnd:yyyy-MM-dd}</TaxPeriodEnd>");
        sb.AppendLine($"    <DueDate>{r.DueDate:yyyy-MM-dd}</DueDate>");
        sb.AppendLine("    <Currency>AED</Currency>");
        sb.AppendLine("  </Header>");
        sb.AppendLine("  <Financials>");
        sb.AppendLine($"    <Revenue>{r.GrossRevenue:F2}</Revenue>");
        sb.AppendLine($"    <CostOfSales>{r.CostOfSales:F2}</CostOfSales>");
        sb.AppendLine($"    <NetAccountingProfit>{r.NetAccountingProfitLoss:F2}</NetAccountingProfit>");
        sb.AppendLine("  </Financials>");
        sb.AppendLine("  <ReliefsAndAdjustments>");
        sb.AppendLine($"    <SmallBusinessReliefElected>{(r.SmallBusinessReliefElection == SmallBusinessReliefStatus.Elected ? "true" : "false")}</SmallBusinessReliefElected>");
        sb.AppendLine($"    <IsQFZP>{(r.IsQfzp ? "true" : "false")}</IsQFZP>");
        sb.AppendLine($"    <QFZPDeMinimisSatisfied>{(r.DeMinimisSatisfied ? "true" : "false")}</QFZPDeMinimisSatisfied>");
        sb.AppendLine($"    <DisallowedInterestArticle30>{r.DisallowedInterestExpense:F2}</DisallowedInterestArticle30>");
        sb.AppendLine($"    <DisallowedEntertainmentArticle32>{r.EntertainmentExpenditureDisallowed:F2}</DisallowedEntertainmentArticle32>");
        sb.AppendLine($"    <FinesAndPenalties>{r.FinesAndPenaltiesDisallowed:F2}</FinesAndPenalties>");
        sb.AppendLine($"    <TaxLossesUtilized>{r.TaxLossesUtilized:F2}</TaxLossesUtilized>");
        sb.AppendLine("  </ReliefsAndAdjustments>");
        sb.AppendLine("  <TaxComputation>");
        sb.AppendLine($"    <AdjustedTaxableIncome>{r.AdjustedTaxableIncome:F2}</AdjustedTaxableIncome>");
        sb.AppendLine($"    <IncomeSubjectTo0Percent>{r.TaxableIncomeSubjectToZeroPct:F2}</IncomeSubjectTo0Percent>");
        sb.AppendLine($"    <IncomeSubjectTo9Percent>{r.TaxableIncomeSubjectToNinePct:F2}</IncomeSubjectTo9Percent>");
        sb.AppendLine($"    <NetCorporateTaxPayableAED>{r.NetTaxPayableAed:F2}</NetCorporateTaxPayableAED>");
        sb.AppendLine($"    <EquivalentTaxPayableINR>{r.NetTaxPayableInr:F2}</EquivalentTaxPayableINR>");
        sb.AppendLine("  </TaxComputation>");
        sb.AppendLine("</EmaraTaxReturn>");
        return sb.ToString();
    }
}
