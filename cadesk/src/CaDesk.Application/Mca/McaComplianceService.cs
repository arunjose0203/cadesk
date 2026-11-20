using CaDesk.Application.Abstractions;
using CaDesk.Domain.Mca;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Mca;

public sealed class McaComplianceService(IAppDbContext db, ITenantContext tenant) : IMcaComplianceService
{
    public async Task<McaComplianceSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalCompanies = await db.Companies.CountAsync(ct);
        var activeCompanies = await db.Companies.CountAsync(c => c.CompanyStatus == CompanyStatus.Active, ct);
        var totalDirectors = await db.DirectorDins.CountAsync(ct);
        var pendingKyc = await db.DirectorDins.CountAsync(d => d.Dir3KycStatus != Dir3KycStatus.Complied, ct);
        var pendingFilings = await db.McaFilings.CountAsync(f => f.Status == McaFilingStatus.Pending, ct);
        var approvedFilings = await db.McaFilings.CountAsync(f => f.Status == McaFilingStatus.Approved, ct);
        var totalLateFees = await db.McaFilings.SumAsync(f => (decimal?)f.LateFee, ct) ?? 0m;

        return new McaComplianceSummaryDto(
            TotalCompaniesCount: totalCompanies,
            ActiveCompaniesCount: activeCompanies,
            TotalDirectorsCount: totalDirectors,
            PendingKycCount: pendingKyc,
            PendingFilingsCount: pendingFilings,
            ApprovedFilingsCount: approvedFilings,
            TotalLateFeesAccrued: totalLateFees
        );
    }

    public async Task<IReadOnlyList<CompanyMasterDto>> ListCompaniesAsync(
        Guid? clientId = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = db.Companies.Include(c => c.Client).Include(c => c.Directors).Include(c => c.Filings).AsQueryable();

        if (clientId.HasValue)
            q = q.Where(c => c.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.CompanyStatus == status.Trim());

        var items = await q.OrderBy(c => c.CompanyName).ToListAsync(ct);

        return items.Select(c => new CompanyMasterDto(
            Id: c.Id,
            ClientId: c.ClientId,
            ClientName: c.Client?.Name,
            Cin: c.Cin,
            CompanyName: c.CompanyName,
            CompanyClass: c.CompanyClass,
            AuthorizedCapital: c.AuthorizedCapital,
            PaidUpCapital: c.PaidUpCapital,
            RegisteredOfficeAddress: c.RegisteredOfficeAddress,
            RocJurisdiction: c.RocJurisdiction,
            IncorporationDate: c.IncorporationDate,
            CompanyStatus: c.CompanyStatus,
            LastAgmDate: c.LastAgmDate,
            FinancialYearEndMonth: c.FinancialYearEndMonth,
            DirectorsCount: c.Directors.Count,
            PendingFilingsCount: c.Filings.Count(f => f.Status == McaFilingStatus.Pending),
            CreatedAt: c.CreatedAt
        )).ToList();
    }

    public async Task<CompanyMasterDto?> GetCompanyByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Companies
            .Include(x => x.Client)
            .Include(x => x.Directors)
            .Include(x => x.Filings)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (c is null) return null;

        return new CompanyMasterDto(
            Id: c.Id,
            ClientId: c.ClientId,
            ClientName: c.Client?.Name,
            Cin: c.Cin,
            CompanyName: c.CompanyName,
            CompanyClass: c.CompanyClass,
            AuthorizedCapital: c.AuthorizedCapital,
            PaidUpCapital: c.PaidUpCapital,
            RegisteredOfficeAddress: c.RegisteredOfficeAddress,
            RocJurisdiction: c.RocJurisdiction,
            IncorporationDate: c.IncorporationDate,
            CompanyStatus: c.CompanyStatus,
            LastAgmDate: c.LastAgmDate,
            FinancialYearEndMonth: c.FinancialYearEndMonth,
            DirectorsCount: c.Directors.Count,
            PendingFilingsCount: c.Filings.Count(f => f.Status == McaFilingStatus.Pending),
            CreatedAt: c.CreatedAt
        );
    }

    public async Task<CompanyMasterDto> CreateCompanyAsync(CreateCompanyRequest req, CancellationToken ct = default)
    {
        var cin = (req.Cin ?? "").Trim().ToUpperInvariant();
        if (!McaFeeCalculator.IsValidCin(cin))
            throw new ArgumentException("Invalid CIN. CIN must be a valid 21-character alphanumeric code (e.g. U72200MH2020PTC123456).", nameof(req.Cin));

        var name = (req.CompanyName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(req.CompanyName));

        var entity = new CompanyMaster
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            Cin = cin,
            CompanyName = name,
            CompanyClass = req.CompanyClass ?? CompanyClass.Private,
            AuthorizedCapital = req.AuthorizedCapital,
            PaidUpCapital = req.PaidUpCapital,
            RegisteredOfficeAddress = (req.RegisteredOfficeAddress ?? "Registered Office").Trim(),
            RocJurisdiction = (req.RocJurisdiction ?? "RoC-Mumbai").Trim(),
            IncorporationDate = req.IncorporationDate,
            CompanyStatus = req.CompanyStatus ?? CompanyStatus.Active,
            LastAgmDate = req.LastAgmDate,
            FinancialYearEndMonth = req.FinancialYearEndMonth ?? 3
        };

        db.Companies.Add(entity);
        await db.SaveChangesAsync(ct);

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == req.ClientId, ct);

        return new CompanyMasterDto(
            Id: entity.Id,
            ClientId: entity.ClientId,
            ClientName: client?.Name,
            Cin: entity.Cin,
            CompanyName: entity.CompanyName,
            CompanyClass: entity.CompanyClass,
            AuthorizedCapital: entity.AuthorizedCapital,
            PaidUpCapital: entity.PaidUpCapital,
            RegisteredOfficeAddress: entity.RegisteredOfficeAddress,
            RocJurisdiction: entity.RocJurisdiction,
            IncorporationDate: entity.IncorporationDate,
            CompanyStatus: entity.CompanyStatus,
            LastAgmDate: entity.LastAgmDate,
            FinancialYearEndMonth: entity.FinancialYearEndMonth,
            DirectorsCount: 0,
            PendingFilingsCount: 0,
            CreatedAt: entity.CreatedAt
        );
    }

    public async Task<IReadOnlyList<DirectorDinDto>> ListDirectorsAsync(
        Guid? companyId = null,
        string? kycStatus = null,
        CancellationToken ct = default)
    {
        var q = db.DirectorDins.Include(d => d.Company).AsQueryable();

        if (companyId.HasValue)
            q = q.Where(d => d.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(kycStatus))
            q = q.Where(d => d.Dir3KycStatus == kycStatus.Trim());

        var items = await q.OrderBy(d => d.DirectorName).ToListAsync(ct);

        return items.Select(d => new DirectorDinDto(
            Id: d.Id,
            CompanyId: d.CompanyId,
            CompanyName: d.Company?.CompanyName,
            Din: d.Din,
            DirectorName: d.DirectorName,
            Pan: d.Pan,
            Designation: d.Designation,
            AppointmentDate: d.AppointmentDate,
            CessationDate: d.CessationDate,
            DscExpiryDate: d.DscExpiryDate,
            Dir3KycStatus: d.Dir3KycStatus,
            Dir3KycLastFiledDate: d.Dir3KycLastFiledDate,
            IsSignatory: d.IsSignatory,
            CreatedAt: d.CreatedAt
        )).ToList();
    }

    public async Task<DirectorDinDto> CreateDirectorAsync(CreateDirectorRequest req, CancellationToken ct = default)
    {
        var din = (req.Din ?? "").Trim();
        if (!McaFeeCalculator.IsValidDin(din))
            throw new ArgumentException("Invalid DIN. DIN must be exactly 8 digits (e.g. 01234567).", nameof(req.Din));

        var name = (req.DirectorName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Director name is required.", nameof(req.DirectorName));

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == req.CompanyId, ct)
            ?? throw new InvalidOperationException($"Company {req.CompanyId} not found.");

        var entity = new DirectorDinRecord
        {
            FirmId = tenant.FirmId,
            CompanyId = req.CompanyId,
            Din = din,
            DirectorName = name,
            Pan = string.IsNullOrWhiteSpace(req.Pan) ? null : req.Pan.Trim().ToUpperInvariant(),
            Designation = req.Designation ?? DirectorDesignation.Director,
            AppointmentDate = req.AppointmentDate,
            CessationDate = req.CessationDate,
            DscExpiryDate = req.DscExpiryDate,
            Dir3KycStatus = req.Dir3KycStatus ?? Dir3KycStatus.Complied,
            Dir3KycLastFiledDate = req.Dir3KycLastFiledDate,
            IsSignatory = req.IsSignatory ?? true
        };

        db.DirectorDins.Add(entity);
        await db.SaveChangesAsync(ct);

        return new DirectorDinDto(
            Id: entity.Id,
            CompanyId: entity.CompanyId,
            CompanyName: company.CompanyName,
            Din: entity.Din,
            DirectorName: entity.DirectorName,
            Pan: entity.Pan,
            Designation: entity.Designation,
            AppointmentDate: entity.AppointmentDate,
            CessationDate: entity.CessationDate,
            DscExpiryDate: entity.DscExpiryDate,
            Dir3KycStatus: entity.Dir3KycStatus,
            Dir3KycLastFiledDate: entity.Dir3KycLastFiledDate,
            IsSignatory: entity.IsSignatory,
            CreatedAt: entity.CreatedAt
        );
    }

    public async Task<IReadOnlyList<McaFilingDto>> ListFilingsAsync(
        Guid? companyId = null,
        string? fy = null,
        string? formType = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var q = db.McaFilings.Include(f => f.Company).AsQueryable();

        if (companyId.HasValue)
            q = q.Where(f => f.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(fy))
            q = q.Where(f => f.FinancialYear == fy.Trim());

        if (!string.IsNullOrWhiteSpace(formType))
            q = q.Where(f => f.FormType == formType.Trim());

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(f => f.Status == status.Trim());

        var items = await q.OrderByDescending(f => f.DueDate).ToListAsync(ct);

        return items.Select(f =>
        {
            var daysDelayed = 0;
            if (f.FilingDate.HasValue && f.FilingDate.Value > f.DueDate)
                daysDelayed = f.FilingDate.Value.DayNumber - f.DueDate.DayNumber;

            return new McaFilingDto(
                Id: f.Id,
                CompanyId: f.CompanyId,
                CompanyName: f.Company?.CompanyName,
                FinancialYear: f.FinancialYear,
                FormType: f.FormType,
                DueDate: f.DueDate,
                FilingDate: f.FilingDate,
                Srn: f.Srn,
                Status: f.Status,
                NormalFee: f.NormalFee,
                LateFee: f.LateFee,
                TotalFee: f.TotalFee,
                Remarks: f.Remarks,
                DaysDelayed: daysDelayed,
                CreatedAt: f.CreatedAt
            );
        }).ToList();
    }

    public async Task<McaFilingDto> CreateFilingAsync(CreateFilingRequest req, CancellationToken ct = default)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == req.CompanyId, ct)
            ?? throw new InvalidOperationException($"Company {req.CompanyId} not found.");

        var normalFee = req.NormalFee ?? McaFeeCalculator.DetermineStandardNormalFee(req.FormType, company.PaidUpCapital);
        var lateFee = 0m;
        var daysDelayed = 0;

        if (req.FilingDate.HasValue)
        {
            var calc = McaFeeCalculator.CalculateLateFee(req.FormType, req.DueDate, req.FilingDate.Value, company.PaidUpCapital);
            normalFee = calc.NormalFee;
            lateFee = calc.LateFee;
            daysDelayed = calc.DaysDelayed;
        }

        var entity = new McaAnnualFiling
        {
            FirmId = tenant.FirmId,
            CompanyId = req.CompanyId,
            FinancialYear = req.FinancialYear.Trim(),
            FormType = req.FormType.Trim(),
            DueDate = req.DueDate,
            FilingDate = req.FilingDate,
            Srn = string.IsNullOrWhiteSpace(req.Srn) ? null : req.Srn.Trim().ToUpperInvariant(),
            Status = req.Status ?? (req.FilingDate.HasValue ? McaFilingStatus.Approved : McaFilingStatus.Pending),
            NormalFee = normalFee,
            LateFee = lateFee,
            TotalFee = normalFee + lateFee,
            Remarks = req.Remarks?.Trim()
        };

        db.McaFilings.Add(entity);
        await db.SaveChangesAsync(ct);

        return new McaFilingDto(
            Id: entity.Id,
            CompanyId: entity.CompanyId,
            CompanyName: company.CompanyName,
            FinancialYear: entity.FinancialYear,
            FormType: entity.FormType,
            DueDate: entity.DueDate,
            FilingDate: entity.FilingDate,
            Srn: entity.Srn,
            Status: entity.Status,
            NormalFee: entity.NormalFee,
            LateFee: entity.LateFee,
            TotalFee: entity.TotalFee,
            Remarks: entity.Remarks,
            DaysDelayed: daysDelayed,
            CreatedAt: entity.CreatedAt
        );
    }

    public async Task<IReadOnlyList<BoardResolutionDto>> ListResolutionsAsync(
        Guid? companyId = null,
        string? meetingType = null,
        CancellationToken ct = default)
    {
        var q = db.BoardResolutions.Include(r => r.Company).AsQueryable();

        if (companyId.HasValue)
            q = q.Where(r => r.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(meetingType))
            q = q.Where(r => r.MeetingType == meetingType.Trim());

        var items = await q.OrderByDescending(r => r.MeetingDate).ToListAsync(ct);

        return items.Select(r => new BoardResolutionDto(
            Id: r.Id,
            CompanyId: r.CompanyId,
            CompanyName: r.Company?.CompanyName,
            ResolutionNumber: r.ResolutionNumber,
            MeetingType: r.MeetingType,
            MeetingDate: r.MeetingDate,
            Category: r.Category,
            Subject: r.Subject,
            Verbiage: r.Verbiage,
            PassedUnanimously: r.PassedUnanimously,
            SignatoryDin: r.SignatoryDin,
            SignatoryName: r.SignatoryName,
            CreatedAt: r.CreatedAt
        )).ToList();
    }

    public async Task<BoardResolutionDto> CreateResolutionAsync(CreateResolutionRequest req, CancellationToken ct = default)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == req.CompanyId, ct)
            ?? throw new InvalidOperationException($"Company {req.CompanyId} not found.");

        var resNumber = (req.ResolutionNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(resNumber))
            throw new ArgumentException("Resolution number is required.", nameof(req.ResolutionNumber));

        var subject = (req.Subject ?? "").Trim();
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(req.Subject));

        var verbiage = (req.Verbiage ?? "").Trim();
        if (string.IsNullOrWhiteSpace(verbiage))
            throw new ArgumentException("Resolution text/verbiage is required.", nameof(req.Verbiage));

        var entity = new BoardResolution
        {
            FirmId = tenant.FirmId,
            CompanyId = req.CompanyId,
            ResolutionNumber = resNumber,
            MeetingType = req.MeetingType ?? MeetingType.BoardMeeting,
            MeetingDate = req.MeetingDate,
            Category = req.Category ?? ResolutionCategory.Ordinary,
            Subject = subject,
            Verbiage = verbiage,
            PassedUnanimously = req.PassedUnanimously ?? true,
            SignatoryDin = req.SignatoryDin?.Trim(),
            SignatoryName = req.SignatoryName?.Trim()
        };

        db.BoardResolutions.Add(entity);
        await db.SaveChangesAsync(ct);

        return new BoardResolutionDto(
            Id: entity.Id,
            CompanyId: entity.CompanyId,
            CompanyName: company.CompanyName,
            ResolutionNumber: entity.ResolutionNumber,
            MeetingType: entity.MeetingType,
            MeetingDate: entity.MeetingDate,
            Category: entity.Category,
            Subject: entity.Subject,
            Verbiage: entity.Verbiage,
            PassedUnanimously: entity.PassedUnanimously,
            SignatoryDin: entity.SignatoryDin,
            SignatoryName: entity.SignatoryName,
            CreatedAt: entity.CreatedAt
        );
    }

    public McaLateFeeCalculationResult CalculateLateFee(CalculateMcaLateFeeRequest req)
    {
        return McaFeeCalculator.CalculateLateFee(
            req.FormType,
            req.DueDate,
            req.FilingDate,
            req.PaidUpCapital
        );
    }
}
