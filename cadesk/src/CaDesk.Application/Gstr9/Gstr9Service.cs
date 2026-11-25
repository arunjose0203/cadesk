using CaDesk.Application.Abstractions;
using CaDesk.Application.Gst;
using CaDesk.Domain.Gstr9;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Gstr9;

public class Gstr9Service(IAppDbContext db, ITenantContext tenant) : IGstr9Service
{
    public async Task<IReadOnlyList<Gstr9ReturnDto>> GetReturnsAsync(Guid? clientId = null, string? financialYear = null, CancellationToken ct = default)
    {
        var query = db.Gstr9Returns
            .Include(r => r.Reconciliation)
            .AsNoTracking();

        if (clientId.HasValue)
            query = query.Where(r => r.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(financialYear))
            query = query.Where(r => r.FinancialYear == financialYear);

        var returns = await query.OrderByDescending(r => r.FinancialYear).ThenByDescending(r => r.CreatedAt).ToListAsync(ct);
        if (returns.Count == 0) return [];

        var clientIds = returns.Select(r => r.ClientId).Distinct().ToList();
        var clients = await db.Clients
            .Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return returns.Select(r => MapReturnToDto(r, clients.GetValueOrDefault(r.ClientId, "Unknown Client"))).ToList();
    }

    public async Task<Gstr9ReturnDto?> GetReturnByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ret = await db.Gstr9Returns
            .Include(r => r.TableEntries)
            .Include(r => r.Reconciliation)
                .ThenInclude(rec => rec!.Discrepancies)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (ret == null) return null;

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == ret.ClientId, ct);
        return MapReturnToDto(ret, client?.Name ?? "Unknown Client");
    }

    public async Task<Gstr9ReturnDto> CompileReturnAsync(CompileGstr9ReturnRequest req, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found");

        var existing = await db.Gstr9Returns.AnyAsync(r => r.ClientId == req.ClientId && r.FinancialYear == req.FinancialYear, ct);
        if (existing)
            throw new InvalidOperationException($"GSTR-9 return for client '{client.Name}' and FY '{req.FinancialYear}' already exists.");

        var gstin = client.TaxIds.TryGetValue("GSTIN", out var g) ? g : (client.TaxIds.TryGetValue("PAN", out var p) ? $"{p}1Z5" : "UNREGISTERED");
        var isGstr9Mandatory = Gstr9ThresholdEngine.IsGstr9Mandatory(req.AggregateTurnover);
        var isGstr9cMandatory = Gstr9ThresholdEngine.IsGstr9cMandatory(req.AggregateTurnover);

        var totalTurnover = Gstr9CalculationEngine.CalculateTable5Turnover(req.OutwardTaxableSupplies, req.OutwardExemptedSupplies, req.RcmInwardSupplies);
        var netItcAvailed = req.ItcAvailed3b - req.ItcReversed;
        var itcVariance = Gstr9CalculationEngine.CalculateTable8ItcVariance(req.ItcAsPer2a, req.ItcCategorizedB2b, req.ItcSubsequentFy);
        var totalTaxPaid = req.TaxPaidCash + req.TaxPaidItc;
        var shortfallTax = Math.Max(0, req.TotalTaxPayable - totalTaxPaid);

        var ret = new Gstr9Return
        {
            Id = Guid.NewGuid(),
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            FinancialYear = req.FinancialYear,
            Gstin = gstin,
            LegalName = client.Name,
            TradeName = client.Name,
            AggregateTurnover = req.AggregateTurnover,
            IsGstr9Mandatory = isGstr9Mandatory,
            IsGstr9cMandatory = isGstr9cMandatory,
            Status = Gstr9FilingStatus.Draft,
            OutwardTaxableSupplies = req.OutwardTaxableSupplies,
            OutwardExemptedSupplies = req.OutwardExemptedSupplies,
            TotalTurnover = totalTurnover,
            ItcAvailed3b = req.ItcAvailed3b,
            ItcCategorizedB2b = req.ItcCategorizedB2b,
            ItcReversed = req.ItcReversed,
            NetItcAvailed = netItcAvailed,
            ItcAsPer2a = req.ItcAsPer2a,
            ItcReconciledVariance = itcVariance,
            TotalTaxPayable = req.TotalTaxPayable,
            TaxPaidCash = req.TaxPaidCash,
            TaxPaidItc = req.TaxPaidItc,
            ShortfallTax = shortfallTax,
            PreviousFyAmendmentsTax = req.PreviousFyAmendmentsTax,
            Notes = req.Notes
        };

        // Populate Table Entries
        ret.TableEntries.AddRange(CreateStandardTableEntries(ret.Id, req, totalTurnover, netItcAvailed, itcVariance));

        // Initialize GSTR-9C Reconciliation
        var gstr9c = new Gstr9cReconciliation
        {
            Id = Guid.NewGuid(),
            FirmId = tenant.FirmId,
            Gstr9ReturnId = ret.Id,
            ClientId = ret.ClientId,
            FinancialYear = ret.FinancialYear,
            AuditedGrossTurnover = req.AggregateTurnover,
            ReconciledTurnover = req.AggregateTurnover,
            TurnoverAsPerGstr9 = totalTurnover,
            UnreconciledTurnoverVariance = req.AggregateTurnover - totalTurnover,
            ExemptedNilTurnover = req.OutwardExemptedSupplies,
            ReconciledTaxableTurnover = req.AggregateTurnover - req.OutwardExemptedSupplies,
            TaxableTurnoverGstr9 = req.OutwardTaxableSupplies,
            UnreconciledTaxableVariance = (req.AggregateTurnover - req.OutwardExemptedSupplies) - req.OutwardTaxableSupplies,
            AdditionalTaxPayable = shortfallTax,
            BookItcTotal = netItcAvailed,
            Gstr9ItcTotal = netItcAvailed,
            UnreconciledItcVariance = 0,
            CertificationStatus = Gstr9cCertificationStatus.Draft
        };
        ret.Reconciliation = gstr9c;

        db.Gstr9Returns.Add(ret);
        await db.SaveChangesAsync(ct);

        return MapReturnToDto(ret, client.Name);
    }

    public async Task<Gstr9ReturnDto> ImportGstJsonToReturnAsync(
        Guid clientId,
        string financialYear,
        string? gstr1Json,
        string? gstr2bJson,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new KeyNotFoundException($"Client with ID '{clientId}' not found.");

        decimal outwardTaxable = 0m;
        decimal outwardExempt = 0m;
        decimal totalTaxPayable = 0m;
        decimal totalTurnover = 0m;
        decimal itc2a2b = 0m;
        decimal itc3b = 0m;

        if (!string.IsNullOrWhiteSpace(gstr1Json))
        {
            var gstr1 = GstJsonParser.ParseGstr1(gstr1Json);
            outwardTaxable = gstr1.OutwardTaxableSupplies;
            outwardExempt = gstr1.OutwardExemptedSupplies;
            totalTurnover = gstr1.TotalTurnover;
            totalTaxPayable = gstr1.TotalIgst + gstr1.TotalCgst + gstr1.TotalSgst + gstr1.TotalCess;
        }

        if (!string.IsNullOrWhiteSpace(gstr2bJson))
        {
            var gstr2b = GstJsonParser.ParseGstr2B(gstr2bJson);
            itc2a2b = gstr2b.TotalItcAvailable;
            itc3b = gstr2b.TotalItcAvailable;
        }

        var existingReturn = await db.Gstr9Returns
            .Include(r => r.TableEntries)
            .Include(r => r.Reconciliation)
                .ThenInclude(rec => rec!.Discrepancies)
            .FirstOrDefaultAsync(r => r.ClientId == clientId && r.FinancialYear == financialYear, ct);

        if (existingReturn != null)
        {
            if (outwardTaxable > 0 || totalTurnover > 0)
            {
                existingReturn.OutwardTaxableSupplies = outwardTaxable;
                existingReturn.OutwardExemptedSupplies = outwardExempt;
                existingReturn.TotalTurnover = totalTurnover > 0 ? totalTurnover : (outwardTaxable + outwardExempt);
                existingReturn.AggregateTurnover = existingReturn.TotalTurnover;
                existingReturn.IsGstr9Mandatory = Gstr9ThresholdEngine.IsGstr9Mandatory(existingReturn.AggregateTurnover);
                existingReturn.IsGstr9cMandatory = Gstr9ThresholdEngine.IsGstr9cMandatory(existingReturn.AggregateTurnover);
                existingReturn.TotalTaxPayable = totalTaxPayable;
            }

            if (itc2a2b > 0)
            {
                existingReturn.ItcAsPer2a = itc2a2b;
                existingReturn.ItcCategorizedB2b = itc2a2b;
                existingReturn.ItcAvailed3b = itc3b;
                existingReturn.NetItcAvailed = itc3b - existingReturn.ItcReversed;
                existingReturn.ItcReconciledVariance = Gstr9CalculationEngine.CalculateTable8ItcVariance(
                    existingReturn.ItcAsPer2a, existingReturn.ItcCategorizedB2b, 0m);
            }

            existingReturn.ShortfallTax = Math.Max(0, existingReturn.TotalTaxPayable - (existingReturn.TaxPaidCash + existingReturn.TaxPaidItc));
            existingReturn.Notes = (existingReturn.Notes ?? "") + $" [Imported from GST JSON]";
            existingReturn.UpdatedAt = DateTime.UtcNow;

            if (existingReturn.Reconciliation != null)
            {
                existingReturn.Reconciliation.AuditedGrossTurnover = existingReturn.AggregateTurnover;
                existingReturn.Reconciliation.TurnoverAsPerGstr9 = existingReturn.TotalTurnover;
                existingReturn.Reconciliation.UnreconciledTurnoverVariance = existingReturn.AggregateTurnover - existingReturn.TotalTurnover;
                existingReturn.Reconciliation.ExemptedNilTurnover = existingReturn.OutwardExemptedSupplies;
                existingReturn.Reconciliation.TaxableTurnoverGstr9 = existingReturn.OutwardTaxableSupplies;
                existingReturn.Reconciliation.BookItcTotal = existingReturn.NetItcAvailed;
                existingReturn.Reconciliation.Gstr9ItcTotal = existingReturn.NetItcAvailed;
                existingReturn.Reconciliation.UnreconciledItcVariance = 0;
                existingReturn.Reconciliation.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            return MapReturnToDto(existingReturn, client.Name);
        }
        else
        {
            var compileReq = new CompileGstr9ReturnRequest(
                ClientId: clientId,
                FinancialYear: financialYear,
                AggregateTurnover: totalTurnover > 0 ? totalTurnover : (outwardTaxable + outwardExempt),
                OutwardTaxableSupplies: outwardTaxable,
                OutwardExemptedSupplies: outwardExempt,
                RcmInwardSupplies: 0m,
                ItcAvailed3b: itc3b,
                ItcCategorizedB2b: itc2a2b,
                ItcReversed: 0m,
                ItcAsPer2a: itc2a2b,
                ItcSubsequentFy: 0m,
                TotalTaxPayable: totalTaxPayable,
                TaxPaidCash: totalTaxPayable * 0.2m,
                TaxPaidItc: totalTaxPayable * 0.8m,
                PreviousFyAmendmentsTax: 0m,
                Notes: $"Auto-compiled from imported GST JSON (GSTR-1 Turnover: {totalTurnover}, GSTR-2B ITC: {itc2a2b})"
            );

            return await CompileReturnAsync(compileReq, ct);
        }
    }

    public async Task<Gstr9ReturnDto> UpdateReturnAsync(Guid id, UpdateGstr9ReturnRequest req, CancellationToken ct = default)
    {
        var ret = await db.Gstr9Returns
            .Include(r => r.TableEntries)
            .Include(r => r.Reconciliation)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"GSTR-9 return with ID '{id}' not found.");

        if (ret.Status == Gstr9FilingStatus.Filed)
            throw new InvalidOperationException("Cannot update an already filed GSTR-9 return.");

        ret.AggregateTurnover = req.AggregateTurnover;
        ret.IsGstr9Mandatory = Gstr9ThresholdEngine.IsGstr9Mandatory(req.AggregateTurnover);
        ret.IsGstr9cMandatory = Gstr9ThresholdEngine.IsGstr9cMandatory(req.AggregateTurnover);

        var totalTurnover = Gstr9CalculationEngine.CalculateTable5Turnover(req.OutwardTaxableSupplies, req.OutwardExemptedSupplies, req.RcmInwardSupplies);
        var netItcAvailed = req.ItcAvailed3b - req.ItcReversed;
        var itcVariance = Gstr9CalculationEngine.CalculateTable8ItcVariance(req.ItcAsPer2a, req.ItcCategorizedB2b, req.ItcSubsequentFy);
        var totalTaxPaid = req.TaxPaidCash + req.TaxPaidItc;
        var shortfallTax = Math.Max(0, req.TotalTaxPayable - totalTaxPaid);

        ret.OutwardTaxableSupplies = req.OutwardTaxableSupplies;
        ret.OutwardExemptedSupplies = req.OutwardExemptedSupplies;
        ret.TotalTurnover = totalTurnover;
        ret.ItcAvailed3b = req.ItcAvailed3b;
        ret.ItcCategorizedB2b = req.ItcCategorizedB2b;
        ret.ItcReversed = req.ItcReversed;
        ret.NetItcAvailed = netItcAvailed;
        ret.ItcAsPer2a = req.ItcAsPer2a;
        ret.ItcReconciledVariance = itcVariance;
        ret.TotalTaxPayable = req.TotalTaxPayable;
        ret.TaxPaidCash = req.TaxPaidCash;
        ret.TaxPaidItc = req.TaxPaidItc;
        ret.ShortfallTax = shortfallTax;
        ret.PreviousFyAmendmentsTax = req.PreviousFyAmendmentsTax;
        ret.Notes = req.Notes;

        // If GSTR-9C is attached, sync the GSTR-9 turnover and tax
        if (ret.Reconciliation != null)
        {
            ret.Reconciliation.TurnoverAsPerGstr9 = totalTurnover;
            ret.Reconciliation.TaxableTurnoverGstr9 = req.OutwardTaxableSupplies;
            ret.Reconciliation.Gstr9ItcTotal = netItcAvailed;
            ret.Reconciliation.UnreconciledTurnoverVariance = ret.Reconciliation.ReconciledTurnover - totalTurnover;
            ret.Reconciliation.UnreconciledTaxableVariance = ret.Reconciliation.ReconciledTaxableTurnover - req.OutwardTaxableSupplies;
            ret.Reconciliation.UnreconciledItcVariance = ret.Reconciliation.BookItcTotal - netItcAvailed;
        }

        await db.SaveChangesAsync(ct);

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == ret.ClientId, ct);
        return MapReturnToDto(ret, client?.Name ?? "Unknown Client");
    }

    public async Task<Gstr9ReturnDto> FileReturnAsync(Guid id, FileGstr9ReturnRequest req, CancellationToken ct = default)
    {
        var ret = await db.Gstr9Returns
            .Include(r => r.Reconciliation)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"GSTR-9 return with ID '{id}' not found.");

        if (ret.Status == Gstr9FilingStatus.Filed)
            throw new InvalidOperationException("Return is already marked as filed.");

        if (ret.IsGstr9cMandatory && (ret.Reconciliation == null || ret.Reconciliation.CertificationStatus != Gstr9cCertificationStatus.Certified))
        {
            throw new InvalidOperationException("GSTR-9C self-certification or CA certification is mandatory before filing because aggregate turnover exceeds ₹5 Crores.");
        }

        ret.Arn = req.Arn;
        ret.FiledAt = DateTime.UtcNow;
        ret.Status = Gstr9FilingStatus.Filed;
        if (!string.IsNullOrWhiteSpace(req.Notes)) ret.Notes = req.Notes;

        await db.SaveChangesAsync(ct);

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == ret.ClientId, ct);
        return MapReturnToDto(ret, client?.Name ?? "Unknown Client");
    }

    public async Task<Gstr9cReconciliationDto?> GetGstr9cAsync(Guid returnId, CancellationToken ct = default)
    {
        var rec = await db.Gstr9cReconciliations
            .Include(r => r.Discrepancies)
            .FirstOrDefaultAsync(r => r.Gstr9ReturnId == returnId, ct);

        return rec != null ? MapReconciliationToDto(rec) : null;
    }

    public async Task<Gstr9cReconciliationDto> ReconcileGstr9cAsync(Guid returnId, ReconcileGstr9cRequest req, CancellationToken ct = default)
    {
        var rec = await db.Gstr9cReconciliations
            .Include(r => r.Discrepancies)
            .FirstOrDefaultAsync(r => r.Gstr9ReturnId == returnId, ct)
            ?? throw new KeyNotFoundException($"GSTR-9C reconciliation for return ID '{returnId}' not found.");

        var (reconciled5P, variance5R) = Gstr9CalculationEngine.CalculateTable5GrossTurnover(
            req.AuditedGrossTurnover,
            req.UnbilledRevenueStart,
            req.UnadjustedAdvancesEnd,
            req.DeemedSupplies,
            req.CreditNotesAfterFy,
            req.TradeDiscountsDisallowed,
            req.UnbilledRevenueEnd,
            req.UnadjustedAdvancesStart,
            req.OtherTurnoverAdjustments,
            rec.TurnoverAsPerGstr9);

        var (reconciledTaxable7E, variance7G) = Gstr9CalculationEngine.CalculateTable7TaxableTurnover(
            reconciled5P,
            req.ExemptedNilTurnover,
            req.ZeroRatedWopTurnover,
            req.RcmOutwardTurnover,
            rec.TaxableTurnoverGstr9);

        var itcVariance12F = Gstr9CalculationEngine.CalculateTable12ItcVariance(req.BookItcTotal, rec.Gstr9ItcTotal);

        rec.AuditedGrossTurnover = req.AuditedGrossTurnover;
        rec.UnbilledRevenueStart = req.UnbilledRevenueStart;
        rec.UnadjustedAdvancesEnd = req.UnadjustedAdvancesEnd;
        rec.DeemedSupplies = req.DeemedSupplies;
        rec.CreditNotesAfterFy = req.CreditNotesAfterFy;
        rec.TradeDiscountsDisallowed = req.TradeDiscountsDisallowed;
        rec.UnbilledRevenueEnd = req.UnbilledRevenueEnd;
        rec.UnadjustedAdvancesStart = req.UnadjustedAdvancesStart;
        rec.OtherTurnoverAdjustments = req.OtherTurnoverAdjustments;
        rec.ReconciledTurnover = reconciled5P;
        rec.UnreconciledTurnoverVariance = variance5R;

        rec.ExemptedNilTurnover = req.ExemptedNilTurnover;
        rec.ZeroRatedWopTurnover = req.ZeroRatedWopTurnover;
        rec.RcmOutwardTurnover = req.RcmOutwardTurnover;
        rec.ReconciledTaxableTurnover = reconciledTaxable7E;
        rec.UnreconciledTaxableVariance = variance7G;

        rec.AdditionalTaxPayable = req.AdditionalTaxPayable;
        rec.AdditionalInterestPayable = req.AdditionalInterestPayable;
        rec.AdditionalPenaltyPayable = req.AdditionalPenaltyPayable;

        rec.BookItcTotal = req.BookItcTotal;
        rec.UnreconciledItcVariance = itcVariance12F;
        rec.AuditorRemarks = req.AuditorRemarks;

        if (rec.CertificationStatus == Gstr9cCertificationStatus.Draft)
            rec.CertificationStatus = Gstr9cCertificationStatus.UnderReview;

        // Also update parent return status to Reconciled if Draft
        var parentReturn = await db.Gstr9Returns.FirstOrDefaultAsync(r => r.Id == returnId, ct);
        if (parentReturn != null && parentReturn.Status == Gstr9FilingStatus.Draft)
        {
            parentReturn.Status = Gstr9FilingStatus.Reconciled;
        }

        await db.SaveChangesAsync(ct);
        return MapReconciliationToDto(rec);
    }

    public async Task<Gstr9cDiscrepancyDto> AddDiscrepancyAsync(Guid returnId, AddDiscrepancyRequest req, CancellationToken ct = default)
    {
        var rec = await db.Gstr9cReconciliations.FirstOrDefaultAsync(r => r.Gstr9ReturnId == returnId, ct)
            ?? throw new KeyNotFoundException($"GSTR-9C reconciliation for return ID '{returnId}' not found.");

        var item = new Gstr9cDiscrepancy
        {
            Id = Guid.NewGuid(),
            FirmId = tenant.FirmId,
            Gstr9cId = rec.Id,
            Category = req.Category,
            TableRef = req.TableRef,
            Description = req.Description,
            DiscrepancyAmount = req.DiscrepancyAmount,
            TaxImpact = req.TaxImpact,
            AuditorRecommendation = req.AuditorRecommendation,
            IsResolved = false
        };

        db.Gstr9cDiscrepancies.Add(item);
        await db.SaveChangesAsync(ct);

        return new Gstr9cDiscrepancyDto(
            item.Id,
            item.Gstr9cId,
            item.Category,
            item.TableRef,
            item.Description,
            item.DiscrepancyAmount,
            item.TaxImpact,
            item.AuditorRecommendation,
            item.IsResolved,
            item.ResolutionNotes,
            item.CreatedAt
        );
    }

    public async Task<Gstr9cDiscrepancyDto> ResolveDiscrepancyAsync(Guid discrepancyId, ResolveDiscrepancyRequest req, CancellationToken ct = default)
    {
        var item = await db.Gstr9cDiscrepancies.FirstOrDefaultAsync(d => d.Id == discrepancyId, ct)
            ?? throw new KeyNotFoundException($"Discrepancy with ID '{discrepancyId}' not found.");

        item.IsResolved = true;
        item.ResolutionNotes = req.ResolutionNotes;

        await db.SaveChangesAsync(ct);

        return new Gstr9cDiscrepancyDto(
            item.Id,
            item.Gstr9cId,
            item.Category,
            item.TableRef,
            item.Description,
            item.DiscrepancyAmount,
            item.TaxImpact,
            item.AuditorRecommendation,
            item.IsResolved,
            item.ResolutionNotes,
            item.CreatedAt
        );
    }

    public async Task<Gstr9cReconciliationDto> CertifyGstr9cAsync(Guid returnId, CertifyGstr9cRequest req, CancellationToken ct = default)
    {
        var rec = await db.Gstr9cReconciliations
            .Include(r => r.Discrepancies)
            .FirstOrDefaultAsync(r => r.Gstr9ReturnId == returnId, ct)
            ?? throw new KeyNotFoundException($"GSTR-9C reconciliation for return ID '{returnId}' not found.");

        if (string.IsNullOrWhiteSpace(req.AuditorName))
            throw new ArgumentException("Auditor Name is required for certification.");
        if (string.IsNullOrWhiteSpace(req.AuditorMembershipNo))
            throw new ArgumentException("Auditor Membership Number is required for certification.");

        rec.AuditorName = req.AuditorName;
        rec.AuditorMembershipNo = req.AuditorMembershipNo;
        rec.AuditorFirmRegNo = req.AuditorFirmRegNo;
        rec.Udin = req.Udin;
        rec.AuditorRemarks = req.AuditorRemarks;
        rec.CertifiedAt = DateTime.UtcNow;
        rec.CertificationStatus = Gstr9cCertificationStatus.Certified;

        var parentReturn = await db.Gstr9Returns.FirstOrDefaultAsync(r => r.Id == returnId, ct);
        if (parentReturn != null && parentReturn.Status != Gstr9FilingStatus.Filed)
        {
            parentReturn.Status = Gstr9FilingStatus.Certified;
        }

        await db.SaveChangesAsync(ct);
        return MapReconciliationToDto(rec);
    }

    public async Task<Gstr9cReconciliationDto> RecordDrc03PaymentAsync(Guid returnId, RecordDrc03PaymentRequest req, CancellationToken ct = default)
    {
        var rec = await db.Gstr9cReconciliations
            .Include(r => r.Discrepancies)
            .FirstOrDefaultAsync(r => r.Gstr9ReturnId == returnId, ct)
            ?? throw new KeyNotFoundException($"GSTR-9C reconciliation for return ID '{returnId}' not found.");

        rec.Drc03Arn = req.Drc03Arn;
        rec.Drc03Date = req.Drc03Date;
        rec.Drc03PaidAmount = req.Drc03PaidAmount;
        rec.Drc03PaymentMode = string.IsNullOrWhiteSpace(req.Drc03PaymentMode) ? "CashLedger" : req.Drc03PaymentMode;
        rec.IsDrc03Completed = req.Drc03PaidAmount >= (rec.AdditionalTaxPayable + rec.AdditionalInterestPayable + rec.AdditionalPenaltyPayable);

        await db.SaveChangesAsync(ct);
        return MapReconciliationToDto(rec);
    }

    public async Task<Gstr9SummaryDto> GetSummaryAsync(string? financialYear = null, CancellationToken ct = default)
    {
        var query = db.Gstr9Returns
            .Include(r => r.Reconciliation)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(financialYear))
            query = query.Where(r => r.FinancialYear == financialYear);

        var list = await query.ToListAsync(ct);

        var totalReturns = list.Count;
        var mandatoryGstr9 = list.Count(r => r.IsGstr9Mandatory);
        var mandatoryGstr9c = list.Count(r => r.IsGstr9cMandatory);
        var certifiedGstr9c = list.Count(r => r.Reconciliation?.CertificationStatus == Gstr9cCertificationStatus.Certified);
        var filedReturns = list.Count(r => r.Status == Gstr9FilingStatus.Filed);

        var totalTurnoverReconciled = list.Sum(r => r.TotalTurnover);
        var totalTaxPayable = list.Sum(r => r.TotalTaxPayable);
        var totalTaxShortfall = list.Sum(r => r.ShortfallTax);
        var totalDrc03Paid = list.Sum(r => r.Reconciliation?.Drc03PaidAmount ?? 0);

        return new Gstr9SummaryDto(
            totalReturns,
            mandatoryGstr9,
            mandatoryGstr9c,
            certifiedGstr9c,
            filedReturns,
            totalTurnoverReconciled,
            totalTaxPayable,
            totalTaxShortfall,
            totalDrc03Paid
        );
    }

    private static Gstr9ReturnDto MapReturnToDto(Gstr9Return r, string clientName)
    {
        var hasGstr9c = r.Reconciliation != null;
        var gstr9cStatus = r.Reconciliation?.CertificationStatus;
        var additionalDue = (r.Reconciliation?.AdditionalTaxPayable ?? 0) + (r.Reconciliation?.AdditionalInterestPayable ?? 0);
        var drc03Paid = r.Reconciliation?.Drc03PaidAmount ?? 0;
        var drc03Pending = additionalDue > drc03Paid;

        return new Gstr9ReturnDto(
            r.Id,
            r.ClientId,
            clientName,
            r.FinancialYear,
            r.Gstin,
            r.LegalName,
            r.TradeName,
            r.AggregateTurnover,
            r.IsGstr9Mandatory,
            r.IsGstr9cMandatory,
            Gstr9ThresholdEngine.GetExemptionStatus(r.AggregateTurnover),
            r.Status,
            r.OutwardTaxableSupplies,
            r.OutwardExemptedSupplies,
            r.TotalTurnover,
            r.ItcAvailed3b,
            r.ItcCategorizedB2b,
            r.ItcReversed,
            r.NetItcAvailed,
            r.ItcAsPer2a,
            r.ItcReconciledVariance,
            r.TotalTaxPayable,
            r.TaxPaidCash,
            r.TaxPaidItc,
            r.ShortfallTax,
            r.PreviousFyAmendmentsTax,
            r.Arn,
            r.FiledAt,
            r.Notes,
            r.TableEntries.Count,
            hasGstr9c,
            gstr9cStatus,
            drc03Pending,
            r.CreatedAt
        );
    }

    private static Gstr9cReconciliationDto MapReconciliationToDto(Gstr9cReconciliation rec)
    {
        var totalAdditional = rec.AdditionalTaxPayable + rec.AdditionalInterestPayable + rec.AdditionalPenaltyPayable;
        var discrepancies = rec.Discrepancies.Select(d => new Gstr9cDiscrepancyDto(
            d.Id,
            d.Gstr9cId,
            d.Category,
            d.TableRef,
            d.Description,
            d.DiscrepancyAmount,
            d.TaxImpact,
            d.AuditorRecommendation,
            d.IsResolved,
            d.ResolutionNotes,
            d.CreatedAt
        )).ToList();

        return new Gstr9cReconciliationDto(
            rec.Id,
            rec.Gstr9ReturnId,
            rec.ClientId,
            rec.FinancialYear,
            rec.AuditedGrossTurnover,
            rec.UnbilledRevenueStart,
            rec.UnadjustedAdvancesEnd,
            rec.DeemedSupplies,
            rec.CreditNotesAfterFy,
            rec.TradeDiscountsDisallowed,
            rec.UnbilledRevenueEnd,
            rec.UnadjustedAdvancesStart,
            rec.OtherTurnoverAdjustments,
            rec.ReconciledTurnover,
            rec.TurnoverAsPerGstr9,
            rec.UnreconciledTurnoverVariance,
            rec.ExemptedNilTurnover,
            rec.ZeroRatedWopTurnover,
            rec.RcmOutwardTurnover,
            rec.ReconciledTaxableTurnover,
            rec.TaxableTurnoverGstr9,
            rec.UnreconciledTaxableVariance,
            rec.AdditionalTaxPayable,
            rec.AdditionalInterestPayable,
            rec.AdditionalPenaltyPayable,
            totalAdditional,
            rec.BookItcTotal,
            rec.Gstr9ItcTotal,
            rec.UnreconciledItcVariance,
            rec.CertificationStatus,
            rec.AuditorName,
            rec.AuditorMembershipNo,
            rec.AuditorFirmRegNo,
            rec.Udin,
            rec.CertifiedAt,
            rec.AuditorRemarks,
            rec.Drc03Arn,
            rec.Drc03Date,
            rec.Drc03PaidAmount,
            rec.Drc03PaymentMode,
            rec.IsDrc03Completed,
            discrepancies
        );
    }

    /// <summary>
    /// Table rows that follow directly from the figures entered on the compile form.
    /// Earlier versions also produced rows such as 4A/4B, 5A/5C and head-wise IGST/CGST/SGST
    /// amounts by applying fixed percentages (e.g. B2C = 20% of outward supplies). Those
    /// numbers were invented, so they are no longer created. Head-wise ITC and tax figures
    /// stay on the return itself (ItcAvailed3b, ItcReversed, TotalTaxPayable, ...); the
    /// table-wise split of Tables 4, 5 and 17 is worked out from the annual sales register
    /// on the GSTR-1 &amp; 3B desk (Sprint 66).
    /// </summary>
    private static List<Gstr9TableEntry> CreateStandardTableEntries(
        Guid returnId,
        CompileGstr9ReturnRequest req,
        decimal totalTurnover,
        decimal netItc,
        decimal itcVariance)
    {
        _ = netItc;
        _ = itcVariance;
        var list = new List<Gstr9TableEntry>
        {
            new() { Gstr9ReturnId = returnId, TableSection = Gstr9TableSection.Table4, RowCode = "4G", Description = "Inward supplies on which tax is to be paid on reverse charge basis (taxable value)", TaxableValue = req.RcmInwardSupplies },
            new() { Gstr9ReturnId = returnId, TableSection = Gstr9TableSection.Table4, RowCode = "4N", Description = "Supplies and advances on which tax is to be paid (taxable value)", TaxableValue = req.OutwardTaxableSupplies },
            new() { Gstr9ReturnId = returnId, TableSection = Gstr9TableSection.Table5, RowCode = "5M", Description = "Outward supplies on which tax is not to be paid (taxable value)", TaxableValue = req.OutwardExemptedSupplies },
            new() { Gstr9ReturnId = returnId, TableSection = Gstr9TableSection.Table5, RowCode = "5N", Description = "Total turnover including advances (4N + 5M - 4G)", TaxableValue = totalTurnover },
        };

        return list;
    }
}
