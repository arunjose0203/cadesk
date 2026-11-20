using CaDesk.Application.Abstractions;
using CaDesk.Domain.Tds;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Tds;

public class TdsService(IAppDbContext db, ITenantContext tenant)
{
    // =========================================================================
    // Lower Deduction Certificates (LDC u/s 197)
    // =========================================================================

    public async Task<TdsLowerDeductionCertificateDto> CreateLdcAsync(
        CreateLdcRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        if (string.IsNullOrWhiteSpace(req.DeducteePan) || req.DeducteePan.Length != 10)
            throw new RequestValidationException(nameof(req.DeducteePan), "Valid 10-digit PAN required.");
        if (string.IsNullOrWhiteSpace(req.CertificateNumber))
            throw new RequestValidationException(nameof(req.CertificateNumber), "Certificate number required.");
        if (req.ThresholdAmount <= 0)
            throw new RequestValidationException(nameof(req.ThresholdAmount), "Threshold amount must be greater than 0.");

        var cert = new TdsLowerDeductionCertificate
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            DeducteePan = req.DeducteePan.Trim().ToUpperInvariant(),
            DeducteeName = req.DeducteeName.Trim(),
            Section = req.Section.Trim(),
            CertificateNumber = req.CertificateNumber.Trim(),
            FinancialYear = req.FinancialYear.Trim(),
            ValidFrom = req.ValidFrom,
            ValidTo = req.ValidTo,
            SpecifiedRate = req.SpecifiedRate,
            ThresholdAmount = req.ThresholdAmount,
            UtilizedAmount = 0m,
            CreatedBy = currentUserId
        };

        db.TdsLowerDeductionCertificates.Add(cert);
        await db.SaveChangesAsync(ct);

        return ToLdcDto(cert, client.Name);
    }

    public async Task<List<TdsLowerDeductionCertificateDto>> ListLdcsAsync(
        Guid? clientId = null,
        string? financialYear = null,
        CancellationToken ct = default)
    {
        var q = db.TdsLowerDeductionCertificates
            .Include(x => x.Client)
            .AsNoTracking();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear) && financialYear != "all") q = q.Where(x => x.FinancialYear == financialYear);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(x => ToLdcDto(x, x.Client?.Name)).ToList();
    }

    // =========================================================================
    // Challans (ITNS 281)
    // =========================================================================

    public async Task<TdsChallanDto> CreateChallanAsync(
        CreateTdsChallanRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        if (string.IsNullOrWhiteSpace(req.BsrCode) || req.BsrCode.Length != 7)
            throw new RequestValidationException(nameof(req.BsrCode), "BSR code must be exactly 7 digits.");
        if (string.IsNullOrWhiteSpace(req.ChallanSerialNumber) || req.ChallanSerialNumber.Length != 5)
            throw new RequestValidationException(nameof(req.ChallanSerialNumber), "Challan serial number must be exactly 5 digits.");

        var totalAmount = req.TdsAmount + req.Surcharge + req.Cess + req.Interest + req.LateFee234E;

        var challan = new TdsChallan
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            Quarter = req.Quarter.Trim().ToUpperInvariant(),
            FinancialYear = req.FinancialYear.Trim(),
            BsrCode = req.BsrCode.Trim(),
            ChallanDate = req.ChallanDate,
            ChallanSerialNumber = req.ChallanSerialNumber.Trim(),
            SectionCode = req.SectionCode.Trim(),
            MinorHead = req.MinorHead ?? TdsMinorHead.Regular200,
            TdsAmount = req.TdsAmount,
            Surcharge = req.Surcharge,
            Cess = req.Cess,
            Interest = req.Interest,
            LateFee234E = req.LateFee234E,
            TotalChallanAmount = totalAmount,
            AllocatedAmount = 0m,
            Notes = req.Notes?.Trim(),
            CreatedBy = currentUserId
        };

        db.TdsChallans.Add(challan);
        await db.SaveChangesAsync(ct);

        return ToChallanDto(challan, client.Name, 0);
    }

    public async Task<List<TdsChallanDto>> ListChallansAsync(
        Guid? clientId = null,
        string? financialYear = null,
        string? quarter = null,
        CancellationToken ct = default)
    {
        var q = db.TdsChallans
            .Include(x => x.Client)
            .Include(x => x.Deductions)
            .AsNoTracking();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear) && financialYear != "all") q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(quarter) && quarter != "all") q = q.Where(x => x.Quarter == quarter);

        var list = await q.OrderByDescending(x => x.ChallanDate).ToListAsync(ct);
        return list.Select(x => ToChallanDto(x, x.Client?.Name, x.Deductions?.Count ?? 0)).ToList();
    }

    // =========================================================================
    // Deductions Recording & Matching
    // =========================================================================

    public async Task<TdsDeductionDto> RecordDeductionAsync(
        RecordTdsDeductionRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        if (string.IsNullOrWhiteSpace(req.DeducteeName))
            throw new RequestValidationException(nameof(req.DeducteeName), "Deductee name is required.");
        if (req.AmountPaid <= 0)
            throw new RequestValidationException(nameof(req.AmountPaid), "Amount paid must be greater than 0.");

        var deductionDate = req.DeductionDate ?? req.PaymentDate;

        decimal? ldcRate = null;
        TdsLowerDeductionCertificate? ldc = null;

        if (req.CertificateId.HasValue)
        {
            ldc = await db.TdsLowerDeductionCertificates.FirstOrDefaultAsync(x => x.Id == req.CertificateId.Value, ct);
            if (ldc != null)
            {
                // Check if within validity dates
                if (deductionDate >= ldc.ValidFrom && deductionDate <= ldc.ValidTo)
                {
                    // Check threshold utilization
                    if (ldc.UtilizedAmount + req.AmountPaid <= ldc.ThresholdAmount)
                    {
                        ldcRate = ldc.SpecifiedRate;
                        ldc.UtilizedAmount += req.AmountPaid;
                        ldc.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }

        var (rate, reason) = TdsTaxCalculator.DetermineRate(
            req.Section,
            req.DeducteePan,
            ldcRate,
            req.ForceHigherRate206Ab == true
        );

        var taxAmount = TdsTaxCalculator.ComputeTaxAmount(req.AmountPaid, rate);

        var deduction = new TdsDeduction
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            FormType = req.FormType ?? TdsFormType.Form26Q,
            Quarter = req.Quarter.Trim().ToUpperInvariant(),
            FinancialYear = req.FinancialYear.Trim(),
            DeducteePan = req.DeducteePan.Trim().ToUpperInvariant(),
            DeducteeName = req.DeducteeName.Trim(),
            Section = req.Section.Trim(),
            PaymentDate = req.PaymentDate,
            AmountPaid = req.AmountPaid,
            TdsRate = rate,
            TdsAmount = taxAmount,
            Surcharge = 0m,
            Cess = 0m,
            TotalTaxDeducted = taxAmount,
            DeductionDate = deductionDate,
            CertificateId = ldcRate.HasValue ? req.CertificateId : null,
            HigherRateReason = reason,
            BookingStatus = TdsBookingStatus.PendingDeposit,
            CreatedBy = currentUserId
        };

        db.TdsDeductions.Add(deduction);
        await db.SaveChangesAsync(ct);

        return ToDeductionDto(deduction, client.Name, null, ldc?.CertificateNumber);
    }

    public async Task<List<TdsDeductionDto>> ListDeductionsAsync(
        Guid? clientId = null,
        string? financialYear = null,
        string? quarter = null,
        string? formType = null,
        string? bookingStatus = null,
        CancellationToken ct = default)
    {
        var q = db.TdsDeductions
            .Include(x => x.Client)
            .Include(x => x.Challan)
            .Include(x => x.Certificate)
            .AsNoTracking();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear) && financialYear != "all") q = q.Where(x => x.FinancialYear == financialYear);
        if (!string.IsNullOrWhiteSpace(quarter) && quarter != "all") q = q.Where(x => x.Quarter == quarter);
        if (!string.IsNullOrWhiteSpace(formType) && formType != "all") q = q.Where(x => x.FormType == formType);
        if (!string.IsNullOrWhiteSpace(bookingStatus) && bookingStatus != "all") q = q.Where(x => x.BookingStatus == bookingStatus);

        var list = await q.OrderByDescending(x => x.DeductionDate).ToListAsync(ct);
        return list.Select(x => ToDeductionDto(x, x.Client?.Name, x.Challan, x.Certificate?.CertificateNumber)).ToList();
    }

    public async Task<TdsChallanDto> MapDeductionsToChallanAsync(
        MapDeductionsToChallanRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var challan = await db.TdsChallans
            .Include(x => x.Client)
            .Include(x => x.Deductions)
            .FirstOrDefaultAsync(x => x.Id == req.ChallanId, ct);

        if (challan is null) throw new BusinessRuleException("Challan not found.", 404);

        var deductions = await db.TdsDeductions
            .Where(x => req.DeductionIds.Contains(x.Id))
            .ToListAsync(ct);

        decimal newlyAllocated = 0m;
        foreach (var d in deductions)
        {
            d.ChallanId = challan.Id;
            d.BookingStatus = TdsBookingStatus.MappedToChallan;
            d.UpdatedAt = DateTime.UtcNow;
            d.UpdatedBy = currentUserId;
            newlyAllocated += d.TotalTaxDeducted;
        }

        challan.AllocatedAmount += newlyAllocated;
        challan.UpdatedAt = DateTime.UtcNow;
        challan.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);

        return ToChallanDto(challan, challan.Client?.Name, challan.Deductions?.Count ?? deductions.Count);
    }

    public async Task<TdsSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var deductions = await db.TdsDeductions.AsNoTracking().ToListAsync(ct);
        var challans = await db.TdsChallans.AsNoTracking().ToListAsync(ct);
        var ldcs = await db.TdsLowerDeductionCertificates.AsNoTracking().ToListAsync(ct);

        var totalDeductions = deductions.Count;
        var totalTaxDeducted = deductions.Sum(x => x.TotalTaxDeducted);
        var totalDeposited = challans.Sum(x => x.TotalChallanAmount);
        var totalPendingDeposit = deductions
            .Where(x => x.BookingStatus == TdsBookingStatus.PendingDeposit)
            .Sum(x => x.TotalTaxDeducted);

        var activeLdcs = ldcs.Count(x => x.ValidTo >= DateOnly.FromDateTime(DateTime.UtcNow));

        return new TdsSummaryDto(
            totalDeductions,
            totalTaxDeducted,
            totalDeposited,
            totalPendingDeposit,
            activeLdcs,
            challans.Count
        );
    }

    // =========================================================================
    // DTO Mappings
    // =========================================================================

    private static TdsLowerDeductionCertificateDto ToLdcDto(TdsLowerDeductionCertificate x, string? clientName) =>
        new(
            x.Id,
            x.ClientId,
            clientName,
            x.DeducteePan,
            x.DeducteeName,
            x.Section,
            x.CertificateNumber,
            x.FinancialYear,
            x.ValidFrom,
            x.ValidTo,
            x.SpecifiedRate,
            x.ThresholdAmount,
            x.UtilizedAmount,
            Math.Max(0m, x.ThresholdAmount - x.UtilizedAmount),
            x.CreatedAt
        );

    private static TdsChallanDto ToChallanDto(TdsChallan x, string? clientName, int deductionsCount) =>
        new(
            x.Id,
            x.ClientId,
            clientName,
            x.Quarter,
            x.FinancialYear,
            x.BsrCode,
            x.ChallanDate,
            x.ChallanSerialNumber,
            x.SectionCode,
            x.MinorHead,
            x.TdsAmount,
            x.Surcharge,
            x.Cess,
            x.Interest,
            x.LateFee234E,
            x.TotalChallanAmount,
            x.AllocatedAmount,
            Math.Max(0m, x.TotalChallanAmount - x.AllocatedAmount),
            deductionsCount,
            x.Notes,
            x.CreatedAt
        );

    private static TdsDeductionDto ToDeductionDto(TdsDeduction x, string? clientName, TdsChallan? challan, string? certNo) =>
        new(
            x.Id,
            x.ClientId,
            clientName,
            x.ChallanId,
            challan?.BsrCode,
            challan?.ChallanDate,
            challan?.ChallanSerialNumber,
            x.FormType,
            x.Quarter,
            x.FinancialYear,
            x.DeducteePan,
            x.DeducteeName,
            x.Section,
            x.PaymentDate,
            x.AmountPaid,
            x.TdsRate,
            x.TdsAmount,
            x.Surcharge,
            x.Cess,
            x.TotalTaxDeducted,
            x.DeductionDate,
            x.CertificateId,
            certNo,
            x.HigherRateReason,
            x.BookingStatus,
            x.CreatedAt
        );
}
