using CaDesk.Domain.EsgCsr;

namespace CaDesk.Application.EsgCsr;

public record CsrAssessmentDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientPan,
    string FinancialYear,
    decimal NetWorth,
    decimal Turnover,
    decimal NetProfitPreviousYear,
    bool IsCsrApplicable,
    string ApplicabilityCriterion,
    decimal AverageNetProfit,
    decimal MandatoryCsrObligation2Pct,
    decimal NetCsrObligationForCurrentFy,
    decimal TotalActualCsrSpent,
    decimal UnspentOngoingProjects,
    decimal UnspentOtherProjects,
    decimal ExcessSpentCarriedForward,
    bool CaroQualificationRequired,
    string CaroClauseXxARemark,
    string CaroClauseXxBRemark,
    CsrComplianceStatus Status,
    string? CertifiedByName,
    string? CertifiedMembershipNo,
    DateTimeOffset? CertifiedAt,
    string? Udin,
    int ProjectsCount,
    DateTime CreatedAt
);

public record CsrAssessmentDetailDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientPan,
    string FinancialYear,
    decimal NetWorth,
    decimal Turnover,
    decimal NetProfitPreviousYear,
    bool IsCsrApplicable,
    string ApplicabilityCriterion,
    decimal NetProfitFyMinus1,
    decimal NetProfitFyMinus2,
    decimal NetProfitFyMinus3,
    decimal AverageNetProfit,
    decimal MandatoryCsrObligation2Pct,
    decimal SetOffAvailableFromPriorYears,
    decimal NetCsrObligationForCurrentFy,
    decimal TotalActualCsrSpent,
    decimal TotalSpentOngoingProjects,
    decimal TotalSpentOtherProjects,
    decimal AdministrativeOverheadsSpent,
    decimal ImpactAssessmentSpent,
    decimal UnspentOngoingProjects,
    decimal UnspentOtherProjects,
    DateOnly? UnspentOngoingTransferDate,
    DateOnly? ScheduleViiFundTransferDate,
    decimal ExcessSpentCarriedForward,
    string CaroClauseXxARemark,
    string CaroClauseXxBRemark,
    bool CaroQualificationRequired,
    CsrComplianceStatus Status,
    string? CertifiedByName,
    string? CertifiedMembershipNo,
    DateTimeOffset? CertifiedAt,
    string? Udin,
    List<CsrProjectItemDto> Projects,
    DateTime CreatedAt
);

public record CsrProjectItemDto(
    Guid Id,
    Guid CsrAssessmentId,
    string ProjectTitle,
    ScheduleViiCategory ScheduleViiCategory,
    CsrProjectNature Nature,
    string StateOrDistrict,
    bool IsAspirationalDistrict,
    decimal TotalProjectOutlay,
    decimal AllocatedBudget,
    decimal ActualSpent,
    decimal UnspentAmount,
    string ImplementationMode,
    string? ImplementingAgencyName,
    string? ImplementingAgencyCsr1Number,
    string ProjectStatus,
    DateTime CreatedAt
);

public record BrsrCoreReportDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string FinancialYear,
    decimal TurnoverInCrores,
    decimal DieselConsumedLitres,
    decimal NaturalGasConsumedSm3,
    decimal GridElectricityPurchasedMwh,
    decimal Scope1DirectEmissionsMtCo2e,
    decimal Scope2IndirectEmissionsMtCo2e,
    decimal TotalScope1And2MtCo2e,
    decimal GhgIntensityPerCroreTurnover,
    decimal TotalWaterWithdrawalKl,
    decimal TotalWaterConsumptionKl,
    decimal WaterIntensityPerCroreTurnover,
    bool OperatesZeroLiquidDischarge,
    decimal TotalEnergyConsumptionGj,
    decimal EnergyIntensityPerCroreTurnover,
    decimal RenewableEnergySharePct,
    decimal LostTimeInjuryFrequencyRateLtifr,
    int FatalitiesCount,
    int PoshComplaintsReceived,
    int PoshComplaintsResolved,
    decimal MedianRemunerationLakhs,
    int TotalPermanentEmployees,
    int FemaleEmployeesCount,
    decimal FemaleEmployeesPct,
    int TotalBoardDirectors,
    int FemaleBoardDirectors,
    decimal FemaleBoardPct,
    int TotalKmpCount,
    int FemaleKmpCount,
    decimal FemaleKmpPct,
    decimal OverallBrsrCoreScore,
    string AssuranceType,
    string? AssuranceAuditorName,
    string? AssuranceAuditorMembershipNo,
    string? AssuranceUdin,
    DateTimeOffset? AssuranceCertifiedAt,
    DateTime CreatedAt
);

public record SaveCsrAssessmentRequest(
    Guid? Id,
    Guid ClientId,
    string FinancialYear,
    decimal NetWorth,
    decimal Turnover,
    decimal NetProfitPreviousYear,
    decimal NetProfitFyMinus1,
    decimal NetProfitFyMinus2,
    decimal NetProfitFyMinus3,
    decimal SetOffAvailableFromPriorYears,
    decimal AdministrativeOverheadsSpent,
    decimal ImpactAssessmentSpent,
    DateOnly? UnspentOngoingTransferDate,
    DateOnly? ScheduleViiFundTransferDate
);

public record SaveCsrProjectRequest(
    Guid? Id,
    Guid CsrAssessmentId,
    string ProjectTitle,
    ScheduleViiCategory ScheduleViiCategory,
    CsrProjectNature Nature,
    string StateOrDistrict,
    bool IsAspirationalDistrict,
    decimal TotalProjectOutlay,
    decimal AllocatedBudget,
    decimal ActualSpent,
    string ImplementationMode,
    string? ImplementingAgencyName,
    string? ImplementingAgencyCsr1Number,
    string ProjectStatus
);

public record SaveBrsrReportRequest(
    Guid? Id,
    Guid ClientId,
    string FinancialYear,
    decimal TurnoverInCrores,
    decimal DieselConsumedLitres,
    decimal NaturalGasConsumedSm3,
    decimal GridElectricityPurchasedMwh,
    decimal TotalWaterWithdrawalKl,
    decimal TotalWaterConsumptionKl,
    bool OperatesZeroLiquidDischarge,
    decimal TotalEnergyConsumptionGj,
    decimal RenewableEnergySharePct,
    decimal LostTimeInjuryFrequencyRateLtifr,
    int FatalitiesCount,
    int PoshComplaintsReceived,
    int PoshComplaintsResolved,
    decimal MedianRemunerationLakhs,
    int TotalPermanentEmployees,
    int FemaleEmployeesCount,
    int TotalBoardDirectors,
    int FemaleBoardDirectors,
    int TotalKmpCount,
    int FemaleKmpCount,
    string AssuranceType
);

public record CertifyCsrRequest(
    string AuditorName,
    string MembershipNo,
    string Udin
);

public record CertifyBrsrRequest(
    string AuditorName,
    string MembershipNo,
    string Udin,
    string AssuranceType
);

public record CalculateCsrObligationRequest(
    decimal NetWorth,
    decimal Turnover,
    decimal NetProfitPreviousYear,
    decimal NetProfitFyMinus1,
    decimal NetProfitFyMinus2,
    decimal NetProfitFyMinus3,
    decimal SetOffAvailableFromPriorYears,
    decimal TotalSpentOngoing,
    decimal TotalSpentOther,
    DateOnly? OngoingTransferDate,
    DateOnly? ScheduleViiTransferDate
);

public record CsrObligationCalculationDto(
    bool IsApplicable,
    string ApplicabilityReason,
    decimal AverageNetProfit,
    decimal MandatoryCsrObligation2Pct,
    decimal NetCsrObligationCurrentFy,
    decimal TotalSpent,
    decimal UnspentOngoing,
    decimal UnspentOther,
    decimal ExcessCarriedForward,
    bool CaroQualificationRequired,
    string CaroClauseXxARemark,
    string CaroClauseXxBRemark
);
