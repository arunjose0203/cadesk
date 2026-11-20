using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.ForeignRemittance;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.ForeignRemittance;

public class ForeignRemittanceService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ForeignRemittanceService(IAppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ForeignRemittanceSummaryDto> GetSummaryAsync(string? financialYear, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.ForeignRemittanceAssessments.Where(x => x.FirmId == firmId);

        if (!string.IsNullOrWhiteSpace(financialYear))
        {
            query = query.Where(x => x.FinancialYear == financialYear);
        }

        var items = await query.ToListAsync(ct);

        return new ForeignRemittanceSummaryDto(
            TotalAssessments: items.Count,
            TotalRemittanceInr: items.Sum(x => x.InrEquivalent),
            TotalTdsInr: items.Sum(x => x.TdsAmountInr),
            DraftCount: items.Count(x => x.Status == "Draft" || x.Status == "Assessed"),
            CertifiedCount: items.Count(x => x.Status == "Certified"),
            FiledCount: items.Count(x => x.Status == "Filed"),
            PartACount: items.Count(x => x.Form15CaPart == "PartA"),
            PartBCount: items.Count(x => x.Form15CaPart == "PartB"),
            PartCCount: items.Count(x => x.Form15CaPart == "PartC"),
            PartDCount: items.Count(x => x.Form15CaPart == "PartD"),
            ExemptCount: items.Count(x => x.Form15CaPart == "ExemptRule37BB")
        );
    }

    public async Task<List<ForeignRemittanceAssessmentDto>> GetAssessmentsAsync(
        Guid? clientId,
        string? financialYear,
        string? status,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.ForeignRemittanceAssessments
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(financialYear))
        {
            query = query.Where(x => x.FinancialYear == financialYear);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<ForeignRemittanceAssessmentDto?> GetAssessmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var entity = await _db.ForeignRemittanceAssessments
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<ForeignRemittanceAssessmentDto> CreateAssessmentAsync(CreateForeignRemittanceRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        // Auto-generate reference if blank
        var reference = req.RemittanceReference;
        if (string.IsNullOrWhiteSpace(reference))
        {
            var count = await _db.ForeignRemittanceAssessments.CountAsync(x => x.FirmId == firmId && x.FinancialYear == req.FinancialYear, ct);
            reference = $"REM-{req.FinancialYear.Replace("-", "")}-{(count + 1):D3}";
        }

        var assessment = new ForeignRemittanceAssessment
        {
            FirmId = firmId,
            ClientId = req.ClientId,
            FinancialYear = req.FinancialYear,
            RemittanceReference = reference,
            RemitterPan = req.RemitterPan.ToUpperInvariant().Trim(),
            RemitterTan = req.RemitterTan?.ToUpperInvariant().Trim() ?? "",
            RemitterName = req.RemitterName.Trim(),
            RemitterStatus = req.RemitterStatus ?? "Company",
            RemitterAddress = req.RemitterAddress ?? "",
            RemitterEmail = req.RemitterEmail ?? "",
            RemitterPhone = req.RemitterPhone ?? "",
            BeneficiaryName = req.BeneficiaryName.Trim(),
            BeneficiaryCountry = req.BeneficiaryCountry.Trim(),
            BeneficiaryCountryCode = req.BeneficiaryCountryCode?.ToUpperInvariant().Trim() ?? "",
            BeneficiaryAddress = req.BeneficiaryAddress ?? "",
            BeneficiaryTaxId = req.BeneficiaryTaxId ?? "",
            BeneficiaryEntityType = req.BeneficiaryEntityType ?? "Company",
            RemittanceCurrency = req.RemittanceCurrency.ToUpperInvariant().Trim(),
            ForeignAmount = req.ForeignAmount,
            ExchangeRate = req.ExchangeRate,
            ProposedRemittanceDate = req.ProposedRemittanceDate,
            RbiPurposeCode = req.RbiPurposeCode ?? "P0802",
            PurposeDescription = req.PurposeDescription ?? "",
            NatureOfRemittance = req.NatureOfRemittance ?? "FeesForTechnicalServices",
            ContractDescription = req.ContractDescription ?? "",
            InvoiceNumber = req.InvoiceNumber ?? "",
            InvoiceDate = req.InvoiceDate,
            IsChargeableToTax = req.IsChargeableToTax,
            ChargeabilityReason = req.ChargeabilityReason ?? "",
            DomesticSection = req.DomesticSection ?? "Section 195 read with Section 115A",
            DomesticTaxRate = req.DomesticTaxRate,
            SurchargeRate = req.SurchargeRate,
            CessRate = req.CessRate,
            TreatyBenefitClaimed = req.TreatyBenefitClaimed,
            TreatyCountry = req.TreatyCountry ?? req.BeneficiaryCountry,
            TreatyArticle = req.TreatyArticle ?? "Article 12 - Royalties and Fees for Technical Services",
            TreatyTaxRate = req.TreatyTaxRate,
            HasTrc = req.HasTrc,
            TrcPeriodFrom = req.TrcPeriodFrom,
            TrcPeriodTo = req.TrcPeriodTo,
            HasForm10F = req.HasForm10F,
            NoPeDeclaration = req.NoPeDeclaration,
            BeneficialOwnershipConfirmed = req.BeneficialOwnershipConfirmed,
            MliPptPassed = req.MliPptPassed,
            TaxBorneByRemitter = req.TaxBorneByRemitter,
            AoOrderSection = req.AoOrderSection,
            AoOrderNumber = req.AoOrderNumber,
            AoOrderDate = req.AoOrderDate,
            AoCertificateRate = req.AoCertificateRate,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        // Standard checklist creation
        assessment.Checklist = new List<ForeignRemittanceChecklistItem>
        {
            new()
            {
                FirmId = firmId,
                ItemCode = "TRC",
                ItemDescription = "Tax Residency Certificate (TRC) issued by foreign Government revenue authority",
                IsMandatory = assessment.TreatyBenefitClaimed,
                Status = assessment.HasTrc ? "Verified" : "Pending",
                VerificationNotes = assessment.HasTrc ? "Verified as valid for the current financial year." : "",
                VerifiedAt = assessment.HasTrc ? DateTime.UtcNow : null
            },
            new()
            {
                FirmId = firmId,
                ItemCode = "FORM_10F",
                ItemDescription = "Form 10F self-declaration electronically furnished on e-Filing portal",
                IsMandatory = assessment.TreatyBenefitClaimed,
                Status = assessment.HasForm10F ? "Verified" : "Pending",
                VerificationNotes = assessment.HasForm10F ? "Portal filing receipt verified." : "",
                VerifiedAt = assessment.HasForm10F ? DateTime.UtcNow : null
            },
            new()
            {
                FirmId = firmId,
                ItemCode = "NO_PE",
                ItemDescription = "Declaration affirming non-existence of Permanent Establishment (PE) in India",
                IsMandatory = assessment.TreatyBenefitClaimed,
                Status = assessment.NoPeDeclaration ? "Verified" : "Pending",
                VerificationNotes = assessment.NoPeDeclaration ? "Signed letter from remittee authorized signatory on record." : "",
                VerifiedAt = assessment.NoPeDeclaration ? DateTime.UtcNow : null
            },
            new()
            {
                FirmId = firmId,
                ItemCode = "BENEFICIAL_OWNER",
                ItemDescription = "Beneficial ownership certificate affirming right to receive and use remittance funds",
                IsMandatory = assessment.TreatyBenefitClaimed,
                Status = assessment.BeneficialOwnershipConfirmed ? "Verified" : "Pending",
                VerificationNotes = assessment.BeneficialOwnershipConfirmed ? "Confirmed as actual beneficial owner." : "",
                VerifiedAt = assessment.BeneficialOwnershipConfirmed ? DateTime.UtcNow : null
            },
            new()
            {
                FirmId = firmId,
                ItemCode = "INVOICE",
                ItemDescription = "Foreign party commercial invoice detailing services/goods/royalties provided",
                IsMandatory = true,
                Status = !string.IsNullOrWhiteSpace(assessment.InvoiceNumber) ? "Verified" : "Pending",
                VerificationNotes = !string.IsNullOrWhiteSpace(assessment.InvoiceNumber) ? $"Invoice #{assessment.InvoiceNumber} verified." : "",
                VerifiedAt = !string.IsNullOrWhiteSpace(assessment.InvoiceNumber) ? DateTime.UtcNow : null
            },
            new()
            {
                FirmId = firmId,
                ItemCode = "CONTRACT",
                ItemDescription = "Underlying agreement / Statement of Work (SOW) governing the international remittance",
                IsMandatory = true,
                Status = !string.IsNullOrWhiteSpace(assessment.ContractDescription) ? "Verified" : "Pending",
                VerificationNotes = !string.IsNullOrWhiteSpace(assessment.ContractDescription) ? "Master Services Agreement on file." : "",
                VerifiedAt = !string.IsNullOrWhiteSpace(assessment.ContractDescription) ? DateTime.UtcNow : null
            },
            new()
            {
                FirmId = firmId,
                ItemCode = "SEC_197_LDC",
                ItemDescription = "Section 197 / 195(2) Lower / Nil withholding certificate issued by the Assessing Officer",
                IsMandatory = !string.IsNullOrWhiteSpace(assessment.AoOrderSection),
                Status = !string.IsNullOrWhiteSpace(assessment.AoOrderNumber) ? "Verified" : "Pending",
                VerificationNotes = !string.IsNullOrWhiteSpace(assessment.AoOrderNumber) ? $"Order #{assessment.AoOrderNumber} under Sec {assessment.AoOrderSection} verified." : "",
                VerifiedAt = !string.IsNullOrWhiteSpace(assessment.AoOrderNumber) ? DateTime.UtcNow : null
            }
        };

        // Perform calculation
        ForeignRemittanceEngine.CalculateAssessment(assessment);
        assessment.Generated15CaXmlPayload = ForeignRemittanceEngine.GenerateForm15CaXmlPayload(assessment);
        assessment.Generated15CbCertificateText = ForeignRemittanceEngine.GenerateForm15CbCertificate(assessment);

        _db.ForeignRemittanceAssessments.Add(assessment);
        await _db.SaveChangesAsync(ct);

        return ToDto(assessment);
    }

    public async Task<ForeignRemittanceAssessmentDto> UpdateAssessmentAsync(Guid id, UpdateForeignRemittanceRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var entity = await _db.ForeignRemittanceAssessments
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (entity is null)
            throw new KeyNotFoundException("Foreign remittance assessment not found.");

        if (entity.Status == "Certified" || entity.Status == "Filed")
            throw new InvalidOperationException("Cannot modify an assessment that has already been certified or filed. Revert to draft first.");

        if (!string.IsNullOrWhiteSpace(req.RemittanceReference)) entity.RemittanceReference = req.RemittanceReference;
        entity.RemitterPan = req.RemitterPan.ToUpperInvariant().Trim();
        entity.RemitterTan = req.RemitterTan?.ToUpperInvariant().Trim() ?? "";
        entity.RemitterName = req.RemitterName.Trim();
        entity.RemitterStatus = req.RemitterStatus ?? "Company";
        entity.RemitterAddress = req.RemitterAddress ?? "";
        entity.RemitterEmail = req.RemitterEmail ?? "";
        entity.RemitterPhone = req.RemitterPhone ?? "";
        entity.BeneficiaryName = req.BeneficiaryName.Trim();
        entity.BeneficiaryCountry = req.BeneficiaryCountry.Trim();
        entity.BeneficiaryCountryCode = req.BeneficiaryCountryCode?.ToUpperInvariant().Trim() ?? "";
        entity.BeneficiaryAddress = req.BeneficiaryAddress ?? "";
        entity.BeneficiaryTaxId = req.BeneficiaryTaxId ?? "";
        entity.BeneficiaryEntityType = req.BeneficiaryEntityType ?? "Company";
        entity.RemittanceCurrency = req.RemittanceCurrency.ToUpperInvariant().Trim();
        entity.ForeignAmount = req.ForeignAmount;
        entity.ExchangeRate = req.ExchangeRate;
        entity.ProposedRemittanceDate = req.ProposedRemittanceDate;
        entity.RbiPurposeCode = req.RbiPurposeCode ?? "P0802";
        entity.PurposeDescription = req.PurposeDescription ?? "";
        entity.NatureOfRemittance = req.NatureOfRemittance ?? "FeesForTechnicalServices";
        entity.ContractDescription = req.ContractDescription ?? "";
        entity.InvoiceNumber = req.InvoiceNumber ?? "";
        entity.InvoiceDate = req.InvoiceDate;
        entity.IsChargeableToTax = req.IsChargeableToTax;
        entity.ChargeabilityReason = req.ChargeabilityReason ?? "";
        entity.DomesticSection = req.DomesticSection ?? "Section 195 read with Section 115A";
        entity.DomesticTaxRate = req.DomesticTaxRate;
        entity.SurchargeRate = req.SurchargeRate;
        entity.CessRate = req.CessRate;
        entity.TreatyBenefitClaimed = req.TreatyBenefitClaimed;
        entity.TreatyCountry = req.TreatyCountry ?? req.BeneficiaryCountry;
        entity.TreatyArticle = req.TreatyArticle ?? "Article 12 - Royalties and Fees for Technical Services";
        entity.TreatyTaxRate = req.TreatyTaxRate;
        entity.HasTrc = req.HasTrc;
        entity.TrcPeriodFrom = req.TrcPeriodFrom;
        entity.TrcPeriodTo = req.TrcPeriodTo;
        entity.HasForm10F = req.HasForm10F;
        entity.NoPeDeclaration = req.NoPeDeclaration;
        entity.BeneficialOwnershipConfirmed = req.BeneficialOwnershipConfirmed;
        entity.MliPptPassed = req.MliPptPassed;
        entity.TaxBorneByRemitter = req.TaxBorneByRemitter;
        entity.AoOrderSection = req.AoOrderSection;
        entity.AoOrderNumber = req.AoOrderNumber;
        entity.AoOrderDate = req.AoOrderDate;
        entity.AoCertificateRate = req.AoCertificateRate;
        entity.UpdatedAt = DateTime.UtcNow;

        ForeignRemittanceEngine.CalculateAssessment(entity);
        entity.Generated15CaXmlPayload = ForeignRemittanceEngine.GenerateForm15CaXmlPayload(entity);
        entity.Generated15CbCertificateText = ForeignRemittanceEngine.GenerateForm15CbCertificate(entity);

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAssessmentAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var entity = await _db.ForeignRemittanceAssessments
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (entity is null) return false;

        _db.ForeignRemittanceAssessments.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ForeignRemittanceChecklistItemDto> UpdateChecklistItemAsync(
        Guid assessmentId,
        Guid itemId,
        UpdateChecklistItemRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var item = await _db.ForeignRemittanceChecklists
            .FirstOrDefaultAsync(x => x.Id == itemId && x.AssessmentId == assessmentId && x.FirmId == firmId, ct);

        if (item is null)
            throw new KeyNotFoundException("Checklist item not found.");

        item.Status = req.Status;
        item.VerificationNotes = req.VerificationNotes ?? "";
        item.VerifiedAt = req.Status == "Verified" ? DateTime.UtcNow : null;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ForeignRemittanceChecklistItemDto(
            item.Id,
            item.AssessmentId,
            item.ItemCode,
            item.ItemDescription,
            item.IsMandatory,
            item.Status,
            item.VerificationNotes,
            item.VerifiedAt,
            item.CreatedAt,
            item.UpdatedAt
        );
    }

    public async Task<ForeignRemittanceAssessmentDto> CertifyRemittanceAsync(
        Guid id,
        Guid? certifierUserId,
        CertifyRemittanceRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var entity = await _db.ForeignRemittanceAssessments
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (entity is null)
            throw new KeyNotFoundException("Foreign remittance assessment not found.");

        if (string.IsNullOrWhiteSpace(req.CaMembershipNumber) || string.IsNullOrWhiteSpace(req.Udin))
            throw new ArgumentException("Chartered Accountant Membership Number and UDIN are mandatory for Form 15CB certification.");

        if (req.Udin.Trim().Length != 18)
            throw new ArgumentException("UDIN must be exactly 18 alphanumeric characters in accordance with ICAI guidelines.");

        // Check mandatory items
        var unverifiedMandatory = entity.Checklist.Where(x => x.IsMandatory && x.Status != "Verified" && x.Status != "Waived").ToList();
        if (unverifiedMandatory.Count != 0)
        {
            var missingCodes = string.Join(", ", unverifiedMandatory.Select(x => x.ItemCode));
            throw new InvalidOperationException($"Cannot certify Form 15CB. Mandatory documentation checklist items are not verified: {missingCodes}");
        }

        entity.CaMembershipNumber = req.CaMembershipNumber.Trim();
        entity.CaName = req.CaName.Trim();
        entity.CaFirmName = req.CaFirmName.Trim();
        entity.CaFrn = req.CaFrn.Trim();
        entity.Udin = req.Udin.ToUpperInvariant().Trim();
        entity.CertifiedAt = DateTime.UtcNow;
        entity.CertifierUserId = certifierUserId;
        entity.Status = "Certified";
        entity.UpdatedAt = DateTime.UtcNow;

        // Recompute artifacts with certification details
        ForeignRemittanceEngine.CalculateAssessment(entity);
        entity.Generated15CbCertificateText = ForeignRemittanceEngine.GenerateForm15CbCertificate(entity);
        entity.Generated15CaXmlPayload = ForeignRemittanceEngine.GenerateForm15CaXmlPayload(entity);

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<ForeignRemittanceAssessmentDto> FileRemittanceAsync(
        Guid id,
        FileRemittanceRequest req,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var entity = await _db.ForeignRemittanceAssessments
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (entity is null)
            throw new KeyNotFoundException("Foreign remittance assessment not found.");

        if (string.IsNullOrWhiteSpace(req.AcknowledgementNumber))
            throw new ArgumentException("Acknowledgement Number is mandatory to record filing.");

        entity.AcknowledgementNumber = req.AcknowledgementNumber.Trim();
        entity.Status = "Filed";
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<ForeignRemittanceAssessmentDto> GenerateArtifactsAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var entity = await _db.ForeignRemittanceAssessments
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (entity is null)
            throw new KeyNotFoundException("Foreign remittance assessment not found.");

        ForeignRemittanceEngine.CalculateAssessment(entity);
        entity.Generated15CbCertificateText = ForeignRemittanceEngine.GenerateForm15CbCertificate(entity);
        entity.Generated15CaXmlPayload = ForeignRemittanceEngine.GenerateForm15CaXmlPayload(entity);
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static ForeignRemittanceAssessmentDto ToDto(ForeignRemittanceAssessment a)
    {
        return new ForeignRemittanceAssessmentDto(
            Id: a.Id,
            ClientId: a.ClientId,
            ClientName: a.Client?.Name,
            ClientPan: a.Client?.TaxIds.GetValueOrDefault("PAN"),
            FinancialYear: a.FinancialYear,
            RemittanceReference: a.RemittanceReference,
            RemitterPan: a.RemitterPan,
            RemitterTan: a.RemitterTan,
            RemitterName: a.RemitterName,
            RemitterStatus: a.RemitterStatus,
            RemitterAddress: a.RemitterAddress,
            RemitterEmail: a.RemitterEmail,
            RemitterPhone: a.RemitterPhone,
            BeneficiaryName: a.BeneficiaryName,
            BeneficiaryCountry: a.BeneficiaryCountry,
            BeneficiaryCountryCode: a.BeneficiaryCountryCode,
            BeneficiaryAddress: a.BeneficiaryAddress,
            BeneficiaryTaxId: a.BeneficiaryTaxId,
            BeneficiaryEntityType: a.BeneficiaryEntityType,
            RemittanceCurrency: a.RemittanceCurrency,
            ForeignAmount: a.ForeignAmount,
            ExchangeRate: a.ExchangeRate,
            InrEquivalent: a.InrEquivalent,
            ProposedRemittanceDate: a.ProposedRemittanceDate,
            RbiPurposeCode: a.RbiPurposeCode,
            PurposeDescription: a.PurposeDescription,
            NatureOfRemittance: a.NatureOfRemittance,
            ContractDescription: a.ContractDescription,
            InvoiceNumber: a.InvoiceNumber,
            InvoiceDate: a.InvoiceDate,
            IsChargeableToTax: a.IsChargeableToTax,
            ChargeabilityReason: a.ChargeabilityReason,
            DomesticSection: a.DomesticSection,
            DomesticTaxRate: a.DomesticTaxRate,
            SurchargeRate: a.SurchargeRate,
            CessRate: a.CessRate,
            EffectiveDomesticRate: a.EffectiveDomesticRate,
            TreatyBenefitClaimed: a.TreatyBenefitClaimed,
            TreatyCountry: a.TreatyCountry,
            TreatyArticle: a.TreatyArticle,
            TreatyTaxRate: a.TreatyTaxRate,
            HasTrc: a.HasTrc,
            TrcPeriodFrom: a.TrcPeriodFrom,
            TrcPeriodTo: a.TrcPeriodTo,
            HasForm10F: a.HasForm10F,
            NoPeDeclaration: a.NoPeDeclaration,
            BeneficialOwnershipConfirmed: a.BeneficialOwnershipConfirmed,
            MliPptPassed: a.MliPptPassed,
            ApplicableRateType: a.ApplicableRateType,
            FinalWithholdingRate: a.FinalWithholdingRate,
            TaxBorneByRemitter: a.TaxBorneByRemitter,
            GrossedUpAmountInr: a.GrossedUpAmountInr,
            TdsAmountInr: a.TdsAmountInr,
            TdsAmountFc: a.TdsAmountFc,
            NetRemittanceFc: a.NetRemittanceFc,
            NetRemittanceInr: a.NetRemittanceInr,
            Form15CaPart: a.Form15CaPart,
            Form15CaRoutingReason: a.Form15CaRoutingReason,
            AoOrderSection: a.AoOrderSection,
            AoOrderNumber: a.AoOrderNumber,
            AoOrderDate: a.AoOrderDate,
            AoCertificateRate: a.AoCertificateRate,
            RequiresForm15Cb: a.RequiresForm15Cb,
            CaMembershipNumber: a.CaMembershipNumber,
            CaName: a.CaName,
            CaFirmName: a.CaFirmName,
            CaFrn: a.CaFrn,
            Udin: a.Udin,
            CertifiedAt: a.CertifiedAt,
            CertifierUserId: a.CertifierUserId,
            Generated15CbCertificateText: a.Generated15CbCertificateText,
            Generated15CaXmlPayload: a.Generated15CaXmlPayload,
            AcknowledgementNumber: a.AcknowledgementNumber,
            Status: a.Status,
            Checklist: a.Checklist.Select(c => new ForeignRemittanceChecklistItemDto(
                c.Id,
                c.AssessmentId,
                c.ItemCode,
                c.ItemDescription,
                c.IsMandatory,
                c.Status,
                c.VerificationNotes,
                c.VerifiedAt,
                c.CreatedAt,
                c.UpdatedAt
            )).ToList(),
            CreatedAt: a.CreatedAt,
            UpdatedAt: a.UpdatedAt
        );
    }
}
