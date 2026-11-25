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

        if (!req.IsNil)
        {
            if (string.IsNullOrWhiteSpace(req.BsrCode) || req.BsrCode.Trim().Length != 7)
                throw new RequestValidationException(nameof(req.BsrCode), "BSR code must be exactly 7 digits.");
            if (string.IsNullOrWhiteSpace(req.ChallanSerialNumber) || req.ChallanSerialNumber.Trim().Length != 5)
                throw new RequestValidationException(nameof(req.ChallanSerialNumber), "Challan serial number must be exactly 5 digits.");
        }

        var totalAmount = req.TdsAmount + req.Surcharge + req.Cess + req.Interest + req.LateFee234E;

        var challan = new TdsChallan
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            Quarter = req.Quarter.Trim().ToUpperInvariant(),
            FinancialYear = req.FinancialYear.Trim(),
            BsrCode = req.IsNil ? "0000000" : req.BsrCode.Trim(),
            ChallanDate = req.ChallanDate,
            ChallanSerialNumber = req.IsNil ? "00000" : req.ChallanSerialNumber.Trim(),
            SectionCode = req.SectionCode.Trim(),
            MinorHead = req.MinorHead ?? TdsMinorHead.Regular200,
            TdsAmount = req.TdsAmount,
            Surcharge = req.Surcharge,
            Cess = req.Cess,
            Interest = req.Interest,
            LateFee234E = req.LateFee234E,
            TotalChallanAmount = totalAmount,
            AllocatedAmount = 0m,
            IsBookEntry = req.IsBookEntry,
            IsNil = req.IsNil,
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

        decimal rate;
        string reason;
        if (req.CustomRate is { } cr && cr >= 0m)
        {
            var panEmpty = string.IsNullOrWhiteSpace(req.DeducteePan) || req.DeducteePan.Trim().Length != 10;
            rate = panEmpty && cr < 20m ? 20m : cr;
            reason = panEmpty ? HigherRateReason.NoPan206Aa : HigherRateReason.None;
        }
        else
        {
            (rate, reason) = TdsTaxCalculator.DetermineRate(
                req.Section,
                req.DeducteePan,
                ldcRate,
                req.ForceHigherRate206Ab == true,
                deductionDate
            );
        }

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
            DeducteeCode = req.DeducteeCode?.Trim(),
            RemarkCode = req.RemarkCode?.Trim().ToUpperInvariant(),
            RemittanceCode = req.RemittanceCode?.Trim(),
            CountryCode = req.CountryCode?.Trim(),
            TaxRateSource = req.TaxRateSource?.Trim().ToUpperInvariant(),
            AckNumber = req.AckNumber?.Trim(),
            OptedOutNewRegime = req.OptedOutNewRegime,
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
    // Sprint 68: Deductor Profiles, NSDL RPU .txt Export & Form 16/16A Import
    // =========================================================================

    public async Task<TdsDeductorProfileDto> GetDeductorProfileAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new BusinessRuleException("Client not found.", 404);

        var p = await db.TdsDeductorProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId, ct);
        if (p is not null) return ToProfileDto(p);

        client.TaxIds.TryGetValue("TAN", out var tan);
        client.TaxIds.TryGetValue("PAN", out var pan);
        client.TaxIds.TryGetValue("GSTIN", out var gstin);
        var deductorType = client.EntityType.ToLowerInvariant() switch
        {
            "company" or "llp" or "private limited" => "K",
            "partnership" or "firm" => "F",
            "trust" => "T",
            "aop" => "P",
            "boi" => "B",
            "individual" or "huf" or "proprietorship" => "Q",
            _ => "K"
        };
        var digits = new string((client.Phone ?? "").Where(char.IsAsciiDigit).ToArray());
        var mob10 = digits.Length >= 10 ? digits[^10..] : digits;

        return new TdsDeductorProfileDto(
            ClientId: clientId,
            Tan: (tan ?? "").Trim().ToUpperInvariant(),
            Pan: (pan ?? "").Trim().ToUpperInvariant(),
            DeductorType: deductorType,
            DeductorName: client.LegalName ?? client.Name,
            BranchDivision: "NA",
            Gstin: string.IsNullOrWhiteSpace(gstin) ? null : gstin.Trim().ToUpperInvariant(),
            FlatBlock: "",
            PremisesName: null,
            StreetRoad: null,
            AreaLocality: null,
            CityDistrict: null,
            StateCode: "16",
            Pincode: "",
            CountryName: "INDIA",
            Email: client.Email ?? "",
            StdCode: null,
            Phone: mob10,
            Mobile: mob10,
            CountryCallingCode: "91",
            RespName: "",
            RespDesignation: "Partner",
            RespPan: "",
            RespFlatBlock: "",
            RespPremisesName: null,
            RespStreetRoad: null,
            RespAreaLocality: null,
            RespCityDistrict: null,
            RespStateCode: "16",
            RespPincode: "",
            RespCountryName: "INDIA",
            RespEmail: client.Email ?? "",
            RespMobile: mob10,
            RespCountryCallingCode: "91",
            FiledPreviousRegular: false,
            PreviousTokenNo: null
        );
    }

    public async Task<TdsDeductorProfileDto> SaveDeductorProfileAsync(
        Guid clientId,
        SaveTdsDeductorProfileRequest req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            throw new BusinessRuleException("Client not found.", 404);

        var p = await db.TdsDeductorProfiles.FirstOrDefaultAsync(x => x.ClientId == clientId, ct);
        if (p is null)
        {
            p = new TdsDeductorProfile { FirmId = tenant.FirmId, ClientId = clientId, CreatedBy = currentUserId };
            db.TdsDeductorProfiles.Add(p);
        }

        p.Tan = (req.Tan ?? "").Trim().ToUpperInvariant();
        p.Pan = (req.Pan ?? "").Trim().ToUpperInvariant();
        p.DeductorType = string.IsNullOrWhiteSpace(req.DeductorType) ? "K" : req.DeductorType.Trim().ToUpperInvariant();
        p.DeductorName = (req.DeductorName ?? "").Trim();
        p.BranchDivision = string.IsNullOrWhiteSpace(req.BranchDivision) ? "NA" : req.BranchDivision.Trim();
        p.Gstin = string.IsNullOrWhiteSpace(req.Gstin) ? null : req.Gstin.Trim().ToUpperInvariant();
        p.FlatBlock = (req.FlatBlock ?? "").Trim();
        p.PremisesName = TrimNull(req.PremisesName);
        p.StreetRoad = TrimNull(req.StreetRoad);
        p.AreaLocality = TrimNull(req.AreaLocality);
        p.CityDistrict = TrimNull(req.CityDistrict);
        p.StateCode = (req.StateCode ?? "16").Trim();
        p.Pincode = (req.Pincode ?? "").Trim();
        p.CountryName = string.IsNullOrWhiteSpace(req.CountryName) ? "INDIA" : req.CountryName.Trim().ToUpperInvariant();
        p.Email = (req.Email ?? "").Trim();
        p.StdCode = TrimNull(req.StdCode);
        p.Phone = (req.Phone ?? "").Trim();
        p.Mobile = (req.Mobile ?? "").Trim();
        p.CountryCallingCode = string.IsNullOrWhiteSpace(req.CountryCallingCode) ? "91" : req.CountryCallingCode.Trim();
        p.RespName = (req.RespName ?? "").Trim();
        p.RespDesignation = (req.RespDesignation ?? "").Trim();
        p.RespPan = (req.RespPan ?? "").Trim().ToUpperInvariant();
        p.RespFlatBlock = (req.RespFlatBlock ?? "").Trim();
        p.RespPremisesName = TrimNull(req.RespPremisesName);
        p.RespStreetRoad = TrimNull(req.RespStreetRoad);
        p.RespAreaLocality = TrimNull(req.RespAreaLocality);
        p.RespCityDistrict = TrimNull(req.RespCityDistrict);
        p.RespStateCode = (req.RespStateCode ?? "16").Trim();
        p.RespPincode = (req.RespPincode ?? "").Trim();
        p.RespCountryName = string.IsNullOrWhiteSpace(req.RespCountryName) ? "INDIA" : req.RespCountryName.Trim().ToUpperInvariant();
        p.RespEmail = (req.RespEmail ?? "").Trim();
        p.RespMobile = (req.RespMobile ?? "").Trim();
        p.RespCountryCallingCode = string.IsNullOrWhiteSpace(req.RespCountryCallingCode) ? "91" : req.RespCountryCallingCode.Trim();
        p.FiledPreviousRegular = req.FiledPreviousRegular;
        p.PreviousTokenNo = TrimNull(req.PreviousTokenNo);
        p.UpdatedBy = currentUserId;
        p.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToProfileDto(p);
    }

    public async Task<TdsReturnExportResult> ExportReturnAsync(ExportTdsReturnRequest req, CancellationToken ct = default)
    {
        var profile = await GetDeductorProfileAsync(req.ClientId, ct);
        var challans = await db.TdsChallans
            .Where(c => c.ClientId == req.ClientId && c.FinancialYear == req.FinancialYear && c.Quarter == req.Quarter.Trim().ToUpperInvariant())
            .AsNoTracking()
            .ToListAsync(ct);
        var deductions = await db.TdsDeductions
            .Include(d => d.Certificate)
            .Where(d => d.ClientId == req.ClientId && d.FinancialYear == req.FinancialYear && d.Quarter == req.Quarter.Trim().ToUpperInvariant())
            .AsNoTracking()
            .ToListAsync(ct);

        var createdOn = req.FileCreationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return NsdlRpuBuilder.Build(profile, req.FormType, req.FinancialYear, req.Quarter, challans, deductions, createdOn, req.FileSequenceNo);
    }

    public async Task<ImportForm16Result> ImportForm16Async(
        ImportForm16Request req,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == req.ClientId, ct))
            throw new BusinessRuleException("Client not found.", 404);

        var parsed = Form16Importer.Parse(req.Content, req.FormType, req.FinancialYear, req.Quarter);
        var existingChallans = await db.TdsChallans
            .Where(c => c.ClientId == req.ClientId)
            .ToListAsync(ct);

        var mappedChallans = parsed.Challans.Select(ch =>
        {
            var exists = existingChallans.Any(ec =>
                ec.BsrCode == ch.BsrCode &&
                ec.ChallanSerialNumber == ch.ChallanSerialNumber &&
                ec.ChallanDate == ch.ChallanDate);
            return ch with { AlreadyExists = exists };
        }).ToList();

        if (!req.Commit || parsed.Errors.Count > 0)
        {
            return parsed with { Challans = mappedChallans, Committed = false };
        }

        var challanLookup = existingChallans.ToDictionary(
            c => (c.BsrCode, c.ChallanSerialNumber, c.ChallanDate), c => c);

        var addedChallans = 0;
        foreach (var ch in mappedChallans)
        {
            var key = (ch.BsrCode, ch.ChallanSerialNumber, ch.ChallanDate);
            if (challanLookup.ContainsKey(key)) continue;

            var firstDed = parsed.Deductions.FirstOrDefault(d =>
                d.ChallanBsr == ch.BsrCode && d.ChallanSerial == ch.ChallanSerialNumber);
            var entity = new TdsChallan
            {
                FirmId = tenant.FirmId,
                ClientId = req.ClientId,
                FinancialYear = firstDed?.FinancialYear ?? parsed.FinancialYear ?? req.FinancialYear ?? "2026-27",
                Quarter = firstDed?.Quarter ?? parsed.Quarter ?? req.Quarter ?? "Q1",
                BsrCode = ch.BsrCode,
                ChallanSerialNumber = ch.ChallanSerialNumber,
                ChallanDate = ch.ChallanDate,
                SectionCode = firstDed?.Section ?? "194J",
                MinorHead = ch.MinorHead,
                TdsAmount = ch.TaxDeposited,
                Surcharge = 0m,
                Cess = 0m,
                Interest = ch.Interest,
                LateFee234E = ch.LateFee,
                TotalChallanAmount = ch.TotalAmount,
                AllocatedAmount = 0m,
                IsBookEntry = ch.IsBookEntry,
                IsNil = false,
                Notes = $"Imported from {parsed.DetectedFormat}",
                CreatedBy = currentUserId
            };
            db.TdsChallans.Add(entity);
            challanLookup[key] = entity;
            addedChallans++;
        }

        var addedDeds = 0;
        foreach (var row in parsed.Deductions)
        {
            TdsChallan? linked = null;
            if (row.ChallanBsr is not null && row.ChallanSerial is not null && row.ChallanDate is { } cd)
                challanLookup.TryGetValue((row.ChallanBsr, row.ChallanSerial, cd), out linked);

            var hasValidPan = row.DeducteePan.Length == 10 && row.DeducteePan is not ("PANNOTAVBL" or "PANAPPLIED" or "PANINVALID");
            var ded = new TdsDeduction
            {
                FirmId = tenant.FirmId,
                ClientId = req.ClientId,
                ChallanId = linked?.Id,
                FormType = row.FormType,
                Quarter = row.Quarter,
                FinancialYear = row.FinancialYear,
                DeducteePan = row.DeducteePan,
                DeducteeName = row.DeducteeName,
                Section = row.Section,
                PaymentDate = row.PaymentDate,
                AmountPaid = row.AmountPaid,
                TdsRate = row.Rate,
                TdsAmount = row.TdsAmount,
                Surcharge = 0m,
                Cess = 0m,
                TotalTaxDeducted = row.TdsAmount,
                DeductionDate = row.DeductionDate,
                HigherRateReason = hasValidPan ? HigherRateReason.None : HigherRateReason.NoPan206Aa,
                BookingStatus = linked is null ? TdsBookingStatus.PendingDeposit : TdsBookingStatus.MappedToChallan,
                RemarkCode = hasValidPan ? null : "C",
                CreatedBy = currentUserId
            };
            if (linked is not null)
                linked.AllocatedAmount += row.TdsAmount;

            db.TdsDeductions.Add(ded);
            addedDeds++;
        }

        await db.SaveChangesAsync(ct);

        return parsed with
        {
            Challans = mappedChallans,
            ImportedChallansCount = addedChallans,
            ImportedDeductionsCount = addedDeds,
            Committed = true
        };
    }

    private static string? TrimNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static TdsDeductorProfileDto ToProfileDto(TdsDeductorProfile p) =>
        new(
            p.ClientId, p.Tan, p.Pan, p.DeductorType, p.DeductorName, p.BranchDivision, p.Gstin,
            p.FlatBlock, p.PremisesName, p.StreetRoad, p.AreaLocality, p.CityDistrict, p.StateCode, p.Pincode, p.CountryName,
            p.Email, p.StdCode, p.Phone, p.Mobile, p.CountryCallingCode,
            p.RespName, p.RespDesignation, p.RespPan,
            p.RespFlatBlock, p.RespPremisesName, p.RespStreetRoad, p.RespAreaLocality, p.RespCityDistrict,
            p.RespStateCode, p.RespPincode, p.RespCountryName, p.RespEmail, p.RespMobile, p.RespCountryCallingCode,
            p.FiledPreviousRegular, p.PreviousTokenNo
        );

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
            x.CreatedAt,
            x.IsBookEntry,
            x.IsNil
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
            x.CreatedAt,
            x.DeducteeCode,
            x.RemarkCode,
            x.RemittanceCode,
            x.CountryCode,
            x.TaxRateSource,
            x.AckNumber,
            x.OptedOutNewRegime
        );
}
