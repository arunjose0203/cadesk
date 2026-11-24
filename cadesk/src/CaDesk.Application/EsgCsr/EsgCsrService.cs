using CaDesk.Application.Abstractions;
using CaDesk.Domain.EsgCsr;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.EsgCsr;

public class EsgCsrService : IEsgCsrService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public EsgCsrService(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<CsrAssessmentDto>> GetCsrAssessmentsAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var query = _db.CsrAssessments
            .Include(x => x.Client)
            .Include(x => x.Projects)
            .AsQueryable();

        if (clientId.HasValue && clientId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

        return list.Select(x =>
        {
            string pan = (x.Client?.TaxIds != null && x.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : (x.Client?.Code ?? "");
            return new CsrAssessmentDto(
                Id: x.Id,
                ClientId: x.ClientId,
                ClientName: x.Client?.Name ?? "Client",
                ClientPan: pan,
                FinancialYear: x.FinancialYear,
                NetWorth: x.NetWorth,
                Turnover: x.Turnover,
                NetProfitPreviousYear: x.NetProfitPreviousYear,
                IsCsrApplicable: x.IsCsrApplicable,
                ApplicabilityCriterion: x.ApplicabilityCriterion,
                AverageNetProfit: x.AverageNetProfit,
                MandatoryCsrObligation2Pct: x.MandatoryCsrObligation2Pct,
                NetCsrObligationForCurrentFy: x.NetCsrObligationForCurrentFy,
                TotalActualCsrSpent: x.TotalActualCsrSpent,
                UnspentOngoingProjects: x.UnspentOngoingProjects,
                UnspentOtherProjects: x.UnspentOtherProjects,
                ExcessSpentCarriedForward: x.ExcessSpentCarriedForward,
                CaroQualificationRequired: x.CaroQualificationRequired,
                CaroClauseXxARemark: x.CaroClauseXxARemark,
                CaroClauseXxBRemark: x.CaroClauseXxBRemark,
                Status: x.Status,
                CertifiedByName: x.CertifiedByName,
                CertifiedMembershipNo: x.CertifiedMembershipNo,
                CertifiedAt: x.CertifiedAt,
                Udin: x.Udin,
                ProjectsCount: x.Projects.Count,
                CreatedAt: x.CreatedAt
            );
        }).ToList();
    }

    public async Task<CsrAssessmentDetailDto?> GetCsrAssessmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await _db.CsrAssessments
            .Include(x => x.Client)
            .Include(x => x.Projects)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (x == null) return null;

        string pan = (x.Client?.TaxIds != null && x.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : (x.Client?.Code ?? "");

        var projects = x.Projects.OrderBy(p => p.CreatedAt).Select(p => new CsrProjectItemDto(
            Id: p.Id,
            CsrAssessmentId: p.CsrAssessmentId,
            ProjectTitle: p.ProjectTitle,
            ScheduleViiCategory: p.ScheduleViiCategory,
            Nature: p.Nature,
            StateOrDistrict: p.StateOrDistrict,
            IsAspirationalDistrict: p.IsAspirationalDistrict,
            TotalProjectOutlay: p.TotalProjectOutlay,
            AllocatedBudget: p.AllocatedBudget,
            ActualSpent: p.ActualSpent,
            UnspentAmount: p.UnspentAmount,
            ImplementationMode: p.ImplementationMode,
            ImplementingAgencyName: p.ImplementingAgencyName,
            ImplementingAgencyCsr1Number: p.ImplementingAgencyCsr1Number,
            ProjectStatus: p.ProjectStatus,
            CreatedAt: p.CreatedAt
        )).ToList();

        return new CsrAssessmentDetailDto(
            Id: x.Id,
            ClientId: x.ClientId,
            ClientName: x.Client?.Name ?? "Client",
            ClientPan: pan,
            FinancialYear: x.FinancialYear,
            NetWorth: x.NetWorth,
            Turnover: x.Turnover,
            NetProfitPreviousYear: x.NetProfitPreviousYear,
            IsCsrApplicable: x.IsCsrApplicable,
            ApplicabilityCriterion: x.ApplicabilityCriterion,
            NetProfitFyMinus1: x.NetProfitFyMinus1,
            NetProfitFyMinus2: x.NetProfitFyMinus2,
            NetProfitFyMinus3: x.NetProfitFyMinus3,
            AverageNetProfit: x.AverageNetProfit,
            MandatoryCsrObligation2Pct: x.MandatoryCsrObligation2Pct,
            SetOffAvailableFromPriorYears: x.SetOffAvailableFromPriorYears,
            NetCsrObligationForCurrentFy: x.NetCsrObligationForCurrentFy,
            TotalActualCsrSpent: x.TotalActualCsrSpent,
            TotalSpentOngoingProjects: x.TotalSpentOngoingProjects,
            TotalSpentOtherProjects: x.TotalSpentOtherProjects,
            AdministrativeOverheadsSpent: x.AdministrativeOverheadsSpent,
            ImpactAssessmentSpent: x.ImpactAssessmentSpent,
            UnspentOngoingProjects: x.UnspentOngoingProjects,
            UnspentOtherProjects: x.UnspentOtherProjects,
            UnspentOngoingTransferDate: x.UnspentOngoingTransferDate,
            ScheduleViiFundTransferDate: x.ScheduleViiFundTransferDate,
            ExcessSpentCarriedForward: x.ExcessSpentCarriedForward,
            CaroClauseXxARemark: x.CaroClauseXxARemark,
            CaroClauseXxBRemark: x.CaroClauseXxBRemark,
            CaroQualificationRequired: x.CaroQualificationRequired,
            Status: x.Status,
            CertifiedByName: x.CertifiedByName,
            CertifiedMembershipNo: x.CertifiedMembershipNo,
            CertifiedAt: x.CertifiedAt,
            Udin: x.Udin,
            Projects: projects,
            CreatedAt: x.CreatedAt
        );
    }

    public async Task<CsrAssessmentDetailDto> SaveCsrAssessmentAsync(SaveCsrAssessmentRequest req, CancellationToken ct = default)
    {
        CsrAssessment? entity = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.CsrAssessments
                .Include(x => x.Projects)
                .Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct);
        }

        if (entity == null)
        {
            entity = new CsrAssessment
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                FinancialYear = req.FinancialYear,
                CreatedAt = DateTime.UtcNow,
                Status = CsrComplianceStatus.Draft
            };
            _db.CsrAssessments.Add(entity);
        }

        entity.FinancialYear = req.FinancialYear;
        entity.NetWorth = req.NetWorth;
        entity.Turnover = req.Turnover;
        entity.NetProfitPreviousYear = req.NetProfitPreviousYear;
        entity.NetProfitFyMinus1 = req.NetProfitFyMinus1;
        entity.NetProfitFyMinus2 = req.NetProfitFyMinus2;
        entity.NetProfitFyMinus3 = req.NetProfitFyMinus3;
        entity.SetOffAvailableFromPriorYears = req.SetOffAvailableFromPriorYears;
        entity.AdministrativeOverheadsSpent = req.AdministrativeOverheadsSpent;
        entity.ImpactAssessmentSpent = req.ImpactAssessmentSpent;
        entity.UnspentOngoingTransferDate = req.UnspentOngoingTransferDate;
        entity.ScheduleViiFundTransferDate = req.ScheduleViiFundTransferDate;
        entity.UpdatedAt = DateTime.UtcNow;

        // Sum up spent from existing projects
        decimal spentOngoing = entity.Projects.Where(p => p.Nature == CsrProjectNature.OngoingProject).Sum(p => p.ActualSpent);
        decimal spentOther = entity.Projects.Where(p => p.Nature == CsrProjectNature.OtherThanOngoingProject).Sum(p => p.ActualSpent);

        // Compute obligations and CARO 2020 remarks using CsrSection135Engine
        var calc = CsrSection135Engine.CalculateCsrObligation(
            netWorth: entity.NetWorth,
            turnover: entity.Turnover,
            netProfitPreviousYear: entity.NetProfitPreviousYear,
            netProfitFyMinus1: entity.NetProfitFyMinus1,
            netProfitFyMinus2: entity.NetProfitFyMinus2,
            netProfitFyMinus3: entity.NetProfitFyMinus3,
            setOffAvailableFromPriorYears: entity.SetOffAvailableFromPriorYears,
            totalSpentOngoing: spentOngoing,
            totalSpentOther: spentOther,
            financialYear: entity.FinancialYear,
            ongoingTransferDate: entity.UnspentOngoingTransferDate,
            scheduleViiTransferDate: entity.ScheduleViiFundTransferDate
        );

        entity.IsCsrApplicable = calc.IsApplicable;
        entity.ApplicabilityCriterion = calc.ApplicabilityReason;
        entity.AverageNetProfit = calc.AverageNetProfit;
        entity.MandatoryCsrObligation2Pct = calc.MandatoryCsrObligation2Pct;
        entity.NetCsrObligationForCurrentFy = calc.NetCsrObligationCurrentFy;
        entity.TotalActualCsrSpent = calc.TotalSpent;
        entity.TotalSpentOngoingProjects = spentOngoing;
        entity.TotalSpentOtherProjects = spentOther;
        entity.UnspentOngoingProjects = calc.UnspentOngoing;
        entity.UnspentOtherProjects = calc.UnspentOther;
        entity.ExcessSpentCarriedForward = calc.ExcessCarriedForward;
        entity.CaroQualificationRequired = calc.CaroQualificationRequired;
        entity.CaroClauseXxARemark = calc.CaroClauseXxARemark;
        entity.CaroClauseXxBRemark = calc.CaroClauseXxBRemark;

        if (entity.Status != CsrComplianceStatus.Certified)
        {
            if (entity.CaroQualificationRequired)
            {
                entity.Status = CsrComplianceStatus.NonCompliantDefault;
            }
            else if (entity.ExcessSpentCarriedForward > 0)
            {
                entity.Status = CsrComplianceStatus.ExcessSpentCarriedForward;
            }
            else if (entity.UnspentOngoingProjects > 0 || entity.UnspentOtherProjects > 0)
            {
                entity.Status = CsrComplianceStatus.UnspentTransferredToEscrow;
            }
            else
            {
                entity.Status = CsrComplianceStatus.FullyCompliant;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (await GetCsrAssessmentByIdAsync(entity.Id, ct))!;
    }

    public async Task<bool> DeleteCsrAssessmentAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.CsrAssessments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return false;

        _db.CsrAssessments.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CsrProjectItemDto> SaveCsrProjectAsync(SaveCsrProjectRequest req, CancellationToken ct = default)
    {
        var assessment = await _db.CsrAssessments
            .Include(x => x.Projects)
            .FirstOrDefaultAsync(x => x.Id == req.CsrAssessmentId, ct);

        if (assessment == null)
            throw new InvalidOperationException($"CSR Assessment {req.CsrAssessmentId} not found");

        CsrProjectItem? item = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            item = assessment.Projects.FirstOrDefault(p => p.Id == req.Id.Value);
        }

        if (item == null)
        {
            item = new CsrProjectItem
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                CsrAssessmentId = req.CsrAssessmentId,
                CreatedAt = DateTime.UtcNow
            };
            assessment.Projects.Add(item);
            _db.CsrProjectItems.Add(item);
        }

        item.ProjectTitle = req.ProjectTitle;
        item.ScheduleViiCategory = req.ScheduleViiCategory;
        item.Nature = req.Nature;
        item.StateOrDistrict = req.StateOrDistrict;
        item.IsAspirationalDistrict = req.IsAspirationalDistrict;
        item.TotalProjectOutlay = req.TotalProjectOutlay;
        item.AllocatedBudget = req.AllocatedBudget;
        item.ActualSpent = req.ActualSpent;
        item.ImplementationMode = req.ImplementationMode;
        item.ImplementingAgencyName = req.ImplementingAgencyName;
        item.ImplementingAgencyCsr1Number = req.ImplementingAgencyCsr1Number;
        item.ProjectStatus = req.ProjectStatus;
        item.UpdatedAt = DateTime.UtcNow;

        // Recalculate parent assessment spent & CARO remarks
        decimal spentOngoing = assessment.Projects.Where(p => p.Nature == CsrProjectNature.OngoingProject).Sum(p => p.ActualSpent);
        decimal spentOther = assessment.Projects.Where(p => p.Nature == CsrProjectNature.OtherThanOngoingProject).Sum(p => p.ActualSpent);

        var calc = CsrSection135Engine.CalculateCsrObligation(
            netWorth: assessment.NetWorth,
            turnover: assessment.Turnover,
            netProfitPreviousYear: assessment.NetProfitPreviousYear,
            netProfitFyMinus1: assessment.NetProfitFyMinus1,
            netProfitFyMinus2: assessment.NetProfitFyMinus2,
            netProfitFyMinus3: assessment.NetProfitFyMinus3,
            setOffAvailableFromPriorYears: assessment.SetOffAvailableFromPriorYears,
            totalSpentOngoing: spentOngoing,
            totalSpentOther: spentOther,
            financialYear: assessment.FinancialYear,
            ongoingTransferDate: assessment.UnspentOngoingTransferDate,
            scheduleViiTransferDate: assessment.ScheduleViiFundTransferDate
        );

        assessment.TotalActualCsrSpent = calc.TotalSpent;
        assessment.TotalSpentOngoingProjects = spentOngoing;
        assessment.TotalSpentOtherProjects = spentOther;
        assessment.UnspentOngoingProjects = calc.UnspentOngoing;
        assessment.UnspentOtherProjects = calc.UnspentOther;
        assessment.ExcessSpentCarriedForward = calc.ExcessCarriedForward;
        assessment.CaroQualificationRequired = calc.CaroQualificationRequired;
        assessment.CaroClauseXxARemark = calc.CaroClauseXxARemark;
        assessment.CaroClauseXxBRemark = calc.CaroClauseXxBRemark;

        await _db.SaveChangesAsync(ct);

        return new CsrProjectItemDto(
            Id: item.Id,
            CsrAssessmentId: item.CsrAssessmentId,
            ProjectTitle: item.ProjectTitle,
            ScheduleViiCategory: item.ScheduleViiCategory,
            Nature: item.Nature,
            StateOrDistrict: item.StateOrDistrict,
            IsAspirationalDistrict: item.IsAspirationalDistrict,
            TotalProjectOutlay: item.TotalProjectOutlay,
            AllocatedBudget: item.AllocatedBudget,
            ActualSpent: item.ActualSpent,
            UnspentAmount: item.UnspentAmount,
            ImplementationMode: item.ImplementationMode,
            ImplementingAgencyName: item.ImplementingAgencyName,
            ImplementingAgencyCsr1Number: item.ImplementingAgencyCsr1Number,
            ProjectStatus: item.ProjectStatus,
            CreatedAt: item.CreatedAt
        );
    }

    public async Task<bool> DeleteCsrProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var item = await _db.CsrProjectItems
            .Include(x => x.CsrAssessment)
            .ThenInclude(a => a!.Projects)
            .FirstOrDefaultAsync(x => x.Id == projectId, ct);

        if (item == null) return false;

        var assessment = item.CsrAssessment;
        _db.CsrProjectItems.Remove(item);

        if (assessment != null)
        {
            assessment.Projects.Remove(item);
            decimal spentOngoing = assessment.Projects.Where(p => p.Nature == CsrProjectNature.OngoingProject).Sum(p => p.ActualSpent);
            decimal spentOther = assessment.Projects.Where(p => p.Nature == CsrProjectNature.OtherThanOngoingProject).Sum(p => p.ActualSpent);

            var calc = CsrSection135Engine.CalculateCsrObligation(
                netWorth: assessment.NetWorth,
                turnover: assessment.Turnover,
                netProfitPreviousYear: assessment.NetProfitPreviousYear,
                netProfitFyMinus1: assessment.NetProfitFyMinus1,
                netProfitFyMinus2: assessment.NetProfitFyMinus2,
                netProfitFyMinus3: assessment.NetProfitFyMinus3,
                setOffAvailableFromPriorYears: assessment.SetOffAvailableFromPriorYears,
                totalSpentOngoing: spentOngoing,
                totalSpentOther: spentOther,
                financialYear: assessment.FinancialYear,
                ongoingTransferDate: assessment.UnspentOngoingTransferDate,
                scheduleViiTransferDate: assessment.ScheduleViiFundTransferDate
            );

            assessment.TotalActualCsrSpent = calc.TotalSpent;
            assessment.TotalSpentOngoingProjects = spentOngoing;
            assessment.TotalSpentOtherProjects = spentOther;
            assessment.UnspentOngoingProjects = calc.UnspentOngoing;
            assessment.UnspentOtherProjects = calc.UnspentOther;
            assessment.ExcessSpentCarriedForward = calc.ExcessCarriedForward;
            assessment.CaroQualificationRequired = calc.CaroQualificationRequired;
            assessment.CaroClauseXxARemark = calc.CaroClauseXxARemark;
            assessment.CaroClauseXxBRemark = calc.CaroClauseXxBRemark;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<BrsrCoreReportDto>> GetBrsrReportsAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var query = _db.BrsrCoreReports
            .Include(x => x.Client)
            .AsQueryable();

        if (clientId.HasValue && clientId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(MapToBrsrDto).ToList();
    }

    public async Task<BrsrCoreReportDto?> GetBrsrReportByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.BrsrCoreReports
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return item == null ? null : MapToBrsrDto(item);
    }

    public async Task<BrsrCoreReportDto?> GetBrsrReportByClientAndYearAsync(Guid clientId, string financialYear, CancellationToken ct = default)
    {
        var item = await _db.BrsrCoreReports
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.ClientId == clientId && x.FinancialYear == financialYear, ct);

        return item == null ? null : MapToBrsrDto(item);
    }

    public async Task<BrsrCoreReportDto> SaveBrsrReportAsync(SaveBrsrReportRequest req, CancellationToken ct = default)
    {
        BrsrCoreReport? entity = null;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.BrsrCoreReports.Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct);
        }

        if (entity == null)
        {
            entity = new BrsrCoreReport
            {
                Id = Guid.NewGuid(),
                FirmId = _tenant.FirmId,
                ClientId = req.ClientId,
                FinancialYear = req.FinancialYear,
                CreatedAt = DateTime.UtcNow
            };
            _db.BrsrCoreReports.Add(entity);
        }

        entity.FinancialYear = req.FinancialYear;
        entity.TurnoverInCrores = req.TurnoverInCrores;
        entity.DieselConsumedLitres = req.DieselConsumedLitres;
        entity.NaturalGasConsumedSm3 = req.NaturalGasConsumedSm3;
        entity.GridElectricityPurchasedMwh = req.GridElectricityPurchasedMwh;
        entity.TotalWaterWithdrawalKl = req.TotalWaterWithdrawalKl;
        entity.TotalWaterConsumptionKl = req.TotalWaterConsumptionKl;
        entity.OperatesZeroLiquidDischarge = req.OperatesZeroLiquidDischarge;
        entity.TotalEnergyConsumptionGj = req.TotalEnergyConsumptionGj;
        entity.RenewableEnergySharePct = req.RenewableEnergySharePct;
        entity.LostTimeInjuryFrequencyRateLtifr = req.LostTimeInjuryFrequencyRateLtifr;
        entity.FatalitiesCount = req.FatalitiesCount;
        entity.PoshComplaintsReceived = req.PoshComplaintsReceived;
        entity.PoshComplaintsResolved = req.PoshComplaintsResolved;
        entity.MedianRemunerationLakhs = req.MedianRemunerationLakhs;
        entity.TotalPermanentEmployees = req.TotalPermanentEmployees;
        entity.FemaleEmployeesCount = req.FemaleEmployeesCount;
        entity.TotalBoardDirectors = req.TotalBoardDirectors;
        entity.FemaleBoardDirectors = req.FemaleBoardDirectors;
        entity.TotalKmpCount = req.TotalKmpCount;
        entity.FemaleKmpCount = req.FemaleKmpCount;
        entity.AssuranceType = req.AssuranceType;
        entity.UpdatedAt = DateTime.UtcNow;

        // Run calculation engine
        BrsrCoreCalculationEngine.CalculateBrsrMetrics(entity);

        await _db.SaveChangesAsync(ct);
        return (await GetBrsrReportByIdAsync(entity.Id, ct))!;
    }

    public async Task<bool> DeleteBrsrReportAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.BrsrCoreReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item == null) return false;

        _db.BrsrCoreReports.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CsrAssessmentDetailDto> CertifyCsrAsync(Guid id, CertifyCsrRequest req, CancellationToken ct = default)
    {
        var entity = await _db.CsrAssessments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            throw new InvalidOperationException($"CSR Assessment {id} not found");

        entity.CertifiedByName = req.AuditorName;
        entity.CertifiedMembershipNo = req.MembershipNo;
        entity.Udin = req.Udin;
        entity.CertifiedAt = DateTimeOffset.UtcNow;
        entity.Status = CsrComplianceStatus.Certified;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (await GetCsrAssessmentByIdAsync(id, ct))!;
    }

    public async Task<BrsrCoreReportDto> CertifyBrsrAsync(Guid id, CertifyBrsrRequest req, CancellationToken ct = default)
    {
        var entity = await _db.BrsrCoreReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            throw new InvalidOperationException($"BRSR Core Report {id} not found");

        entity.AssuranceAuditorName = req.AuditorName;
        entity.AssuranceAuditorMembershipNo = req.MembershipNo;
        entity.AssuranceUdin = req.Udin;
        entity.AssuranceType = req.AssuranceType;
        entity.AssuranceCertifiedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (await GetBrsrReportByIdAsync(id, ct))!;
    }

    public async Task<string> GenerateAssuranceDossierHtmlAsync(Guid brsrReportId, CancellationToken ct = default)
    {
        var report = await _db.BrsrCoreReports
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == brsrReportId, ct);

        if (report == null)
            throw new InvalidOperationException($"BRSR Core Report {brsrReportId} not found");

        string clientName = report.Client?.Name ?? "Corporate Client";
        string clientCin = (report.Client?.TaxIds != null && report.Client.TaxIds.TryGetValue("CIN", out var c))
            ? c
            : ((report.Client?.TaxIds != null && report.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : (report.Client?.Code ?? "L00000MH2020PLC123456"));

        return BrsrCoreCalculationEngine.GenerateAssuranceDossierHtml(report, clientName, clientCin);
    }

    public CsrObligationCalculationDto CalculateCsrObligation(CalculateCsrObligationRequest req)
    {
        var result = CsrSection135Engine.CalculateCsrObligation(
            netWorth: req.NetWorth,
            turnover: req.Turnover,
            netProfitPreviousYear: req.NetProfitPreviousYear,
            netProfitFyMinus1: req.NetProfitFyMinus1,
            netProfitFyMinus2: req.NetProfitFyMinus2,
            netProfitFyMinus3: req.NetProfitFyMinus3,
            setOffAvailableFromPriorYears: req.SetOffAvailableFromPriorYears,
            totalSpentOngoing: req.TotalSpentOngoing,
            totalSpentOther: req.TotalSpentOther,
            ongoingTransferDate: req.OngoingTransferDate,
            scheduleViiTransferDate: req.ScheduleViiTransferDate
        );

        return new CsrObligationCalculationDto(
            IsApplicable: result.IsApplicable,
            ApplicabilityReason: result.ApplicabilityReason,
            AverageNetProfit: result.AverageNetProfit,
            MandatoryCsrObligation2Pct: result.MandatoryCsrObligation2Pct,
            NetCsrObligationCurrentFy: result.NetCsrObligationCurrentFy,
            TotalSpent: result.TotalSpent,
            UnspentOngoing: result.UnspentOngoing,
            UnspentOther: result.UnspentOther,
            ExcessCarriedForward: result.ExcessCarriedForward,
            CaroQualificationRequired: result.CaroQualificationRequired,
            CaroClauseXxARemark: result.CaroClauseXxARemark,
            CaroClauseXxBRemark: result.CaroClauseXxBRemark
        );
    }

    private static BrsrCoreReportDto MapToBrsrDto(BrsrCoreReport x)
    {
        return new BrsrCoreReportDto(
            Id: x.Id,
            ClientId: x.ClientId,
            ClientName: x.Client?.Name ?? "Client",
            FinancialYear: x.FinancialYear,
            TurnoverInCrores: x.TurnoverInCrores,
            DieselConsumedLitres: x.DieselConsumedLitres,
            NaturalGasConsumedSm3: x.NaturalGasConsumedSm3,
            GridElectricityPurchasedMwh: x.GridElectricityPurchasedMwh,
            Scope1DirectEmissionsMtCo2e: x.Scope1DirectEmissionsMtCo2e,
            Scope2IndirectEmissionsMtCo2e: x.Scope2IndirectEmissionsMtCo2e,
            TotalScope1And2MtCo2e: x.TotalScope1And2MtCo2e,
            GhgIntensityPerCroreTurnover: x.GhgIntensityPerCroreTurnover,
            TotalWaterWithdrawalKl: x.TotalWaterWithdrawalKl,
            TotalWaterConsumptionKl: x.TotalWaterConsumptionKl,
            WaterIntensityPerCroreTurnover: x.WaterIntensityPerCroreTurnover,
            OperatesZeroLiquidDischarge: x.OperatesZeroLiquidDischarge,
            TotalEnergyConsumptionGj: x.TotalEnergyConsumptionGj,
            EnergyIntensityPerCroreTurnover: x.EnergyIntensityPerCroreTurnover,
            RenewableEnergySharePct: x.RenewableEnergySharePct,
            LostTimeInjuryFrequencyRateLtifr: x.LostTimeInjuryFrequencyRateLtifr,
            FatalitiesCount: x.FatalitiesCount,
            PoshComplaintsReceived: x.PoshComplaintsReceived,
            PoshComplaintsResolved: x.PoshComplaintsResolved,
            MedianRemunerationLakhs: x.MedianRemunerationLakhs,
            TotalPermanentEmployees: x.TotalPermanentEmployees,
            FemaleEmployeesCount: x.FemaleEmployeesCount,
            FemaleEmployeesPct: x.FemaleEmployeesPct,
            TotalBoardDirectors: x.TotalBoardDirectors,
            FemaleBoardDirectors: x.FemaleBoardDirectors,
            FemaleBoardPct: x.FemaleBoardPct,
            TotalKmpCount: x.TotalKmpCount,
            FemaleKmpCount: x.FemaleKmpCount,
            FemaleKmpPct: x.FemaleKmpPct,
            OverallBrsrCoreScore: x.OverallBrsrCoreScore,
            AssuranceType: x.AssuranceType,
            AssuranceAuditorName: x.AssuranceAuditorName,
            AssuranceAuditorMembershipNo: x.AssuranceAuditorMembershipNo,
            AssuranceUdin: x.AssuranceUdin,
            AssuranceCertifiedAt: x.AssuranceCertifiedAt,
            CreatedAt: x.CreatedAt
        );
    }
}
