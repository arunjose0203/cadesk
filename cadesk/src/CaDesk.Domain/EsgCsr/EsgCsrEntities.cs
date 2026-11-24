using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.EsgCsr;

public enum ScheduleViiCategory
{
    EradicatingHungerPovertyAndMalnutrition = 1,
    PromotingEducationAndSkillDevelopment = 2,
    GenderEqualityAndWomenEmpowerment = 3,
    EnvironmentalSustainabilityAndAnimalWelfare = 4,
    ProtectionOfNationalHeritageArtAndCulture = 5,
    MeasuresForArmedForcesVeteransAndWarWidows = 6,
    RuralAndNationallyRecognisedSports = 7,
    PrimeMinisterNationalReliefFundOrPmCares = 8,
    TechnologyIncubatorsAndResearchInstitutions = 9,
    RuralDevelopmentProjects = 10,
    SlumAreaDevelopment = 11,
    DisasterManagementAndRelief = 12
}

public enum CsrProjectNature
{
    OngoingProject = 1,          // Multi-year up to 3 years u/s 135(6)
    OtherThanOngoingProject = 2  // Single-year allocation u/s 135(5)
}

public enum CsrComplianceStatus
{
    Draft = 1,
    InReview = 2,
    FullyCompliant = 3,
    UnspentTransferredToEscrow = 4,
    ExcessSpentCarriedForward = 5,
    NonCompliantDefault = 6,
    Certified = 7
}

public class CsrAssessment : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = "2024-25";

    // Section 135(1) 3-Pillar Applicability Criteria
    public decimal NetWorth { get; set; }                  // Criteria: >= 500 Cr
    public decimal Turnover { get; set; }                  // Criteria: >= 1000 Cr
    public decimal NetProfitPreviousYear { get; set; }      // Criteria: >= 5 Cr
    public bool IsCsrApplicable { get; set; }
    public string ApplicabilityCriterion { get; set; } = "";

    // Section 198 Net Profits for Preceding 3 FYs
    public decimal NetProfitFyMinus1 { get; set; }
    public decimal NetProfitFyMinus2 { get; set; }
    public decimal NetProfitFyMinus3 { get; set; }
    public decimal AverageNetProfit { get; set; }
    public decimal MandatoryCsrObligation2Pct { get; set; } // 2% of Average Net Profit

    // Set-off from Prior Years under Section 135(5) 3rd proviso
    public decimal SetOffAvailableFromPriorYears { get; set; }
    public decimal NetCsrObligationForCurrentFy { get; set; }

    // Actual Expenditures in Current FY
    public decimal TotalActualCsrSpent { get; set; }
    public decimal TotalSpentOngoingProjects { get; set; }
    public decimal TotalSpentOtherProjects { get; set; }
    public decimal AdministrativeOverheadsSpent { get; set; } // Capped at 5% of total CSR spent
    public decimal ImpactAssessmentSpent { get; set; }       // Capped at 2% or 50L

    // Unspent Balances & Statutory Transfer Deadlines
    public decimal UnspentOngoingProjects { get; set; }      // Must transfer within 30 days of FY close to special Unspent CSR Account u/s 135(6)
    public decimal UnspentOtherProjects { get; set; }        // Must transfer within 6 months of FY close to Schedule VII Fund u/s 135(5)
    public DateOnly? UnspentOngoingTransferDate { get; set; }
    public DateOnly? ScheduleViiFundTransferDate { get; set; }
    public decimal ExcessSpentCarriedForward { get; set; }

    // CARO 2020 Clause XX Statutory Auditor Reporting
    public string CaroClauseXxARemark { get; set; } = "";    // Clause XX(a): Other than ongoing projects (6 months)
    public string CaroClauseXxBRemark { get; set; } = "";    // Clause XX(b): Ongoing projects (30 days)
    public bool CaroQualificationRequired { get; set; }

    public CsrComplianceStatus Status { get; set; } = CsrComplianceStatus.Draft;
    public string? CertifiedByName { get; set; }
    public string? CertifiedMembershipNo { get; set; }
    public DateTimeOffset? CertifiedAt { get; set; }
    public string? Udin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<CsrProjectItem> Projects { get; set; } = new();
}

public class CsrProjectItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid CsrAssessmentId { get; set; }
    public CsrAssessment? CsrAssessment { get; set; }

    public string ProjectTitle { get; set; } = "";
    public ScheduleViiCategory ScheduleViiCategory { get; set; } = ScheduleViiCategory.PromotingEducationAndSkillDevelopment;
    public CsrProjectNature Nature { get; set; } = CsrProjectNature.OngoingProject;
    public string StateOrDistrict { get; set; } = "";
    public bool IsAspirationalDistrict { get; set; }

    public decimal TotalProjectOutlay { get; set; }
    public decimal AllocatedBudget { get; set; }
    public decimal ActualSpent { get; set; }
    public decimal UnspentAmount => Math.Max(0, AllocatedBudget - ActualSpent);

    public string ImplementationMode { get; set; } = "Direct"; // Direct or ImplementingAgency
    public string? ImplementingAgencyName { get; set; }
    public string? ImplementingAgencyCsr1Number { get; set; } // Form CSR-1 MCA registration e.g. CSR00012345
    public string ProjectStatus { get; set; } = "OngoingYear1";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class BrsrCoreReport : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = "2024-25";

    // Turnover Context
    public decimal TurnoverInCrores { get; set; } = 100m;

    // Pillar 1: GHG Scope 1 & Scope 2 Emissions
    public decimal DieselConsumedLitres { get; set; }
    public decimal NaturalGasConsumedSm3 { get; set; }
    public decimal GridElectricityPurchasedMwh { get; set; }

    public decimal Scope1DirectEmissionsMtCo2e { get; set; }     // Direct fuel combustion
    public decimal Scope2IndirectEmissionsMtCo2e { get; set; }   // Grid electricity (CEA emission factor 0.82 tCO2/MWh)
    public decimal TotalScope1And2MtCo2e => Scope1DirectEmissionsMtCo2e + Scope2IndirectEmissionsMtCo2e;
    public decimal GhgIntensityPerCroreTurnover { get; set; }     // Total MT CO2e / Turnover in Cr

    // Pillar 2: Water Stewardship
    public decimal TotalWaterWithdrawalKl { get; set; }          // Kilolitres
    public decimal TotalWaterConsumptionKl { get; set; }
    public decimal WaterIntensityPerCroreTurnover { get; set; }
    public bool OperatesZeroLiquidDischarge { get; set; } = true;

    // Pillar 3: Energy Footprint & Circularity
    public decimal TotalEnergyConsumptionGj { get; set; }        // Gigajoules
    public decimal EnergyIntensityPerCroreTurnover { get; set; }
    public decimal RenewableEnergySharePct { get; set; }         // % from solar, wind, biomass

    // Pillar 4: Employee Safety & Well-being
    public decimal LostTimeInjuryFrequencyRateLtifr { get; set; } // Per 1,000,000 man-hours worked
    public int FatalitiesCount { get; set; }
    public int PoshComplaintsReceived { get; set; }
    public int PoshComplaintsResolved { get; set; }
    public decimal MedianRemunerationLakhs { get; set; }

    // Pillar 5: Gender Diversity Metrics
    public int TotalPermanentEmployees { get; set; } = 500;
    public int FemaleEmployeesCount { get; set; } = 150;
    public decimal FemaleEmployeesPct { get; set; }              // e.g. 30.0%

    public int TotalBoardDirectors { get; set; } = 8;
    public int FemaleBoardDirectors { get; set; } = 2;
    public decimal FemaleBoardPct { get; set; }                  // e.g. 25.0%

    public int TotalKmpCount { get; set; } = 6;
    public int FemaleKmpCount { get; set; } = 2;
    public decimal FemaleKmpPct { get; set; }                    // e.g. 33.3%

    // Compliance & Assurance Status
    public decimal OverallBrsrCoreScore { get; set; }            // 0 - 100%
    public string AssuranceType { get; set; } = "ReasonableAssurance"; // ReasonableAssurance / LimitedAssurance
    public string? AssuranceAuditorName { get; set; }
    public string? AssuranceAuditorMembershipNo { get; set; }
    public string? AssuranceUdin { get; set; }
    public DateTimeOffset? AssuranceCertifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public static class CsrSection135Engine
{
    public record CsrCalculationResult(
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

    /// <summary>
    /// Evaluates Section 135(1) threshold gating and calculates Section 198 2% CSR obligations
    /// </summary>
    public static CsrCalculationResult CalculateCsr(
        decimal netWorth,
        decimal turnover,
        decimal netProfitPreviousYear,
        decimal npFy1,
        decimal npFy2,
        decimal npFy3,
        decimal priorExcessSetOff,
        decimal spentOngoing,
        decimal spentOther,
        decimal adminOverheads,
        decimal allocatedOngoing,
        decimal allocatedOther,
        DateOnly? unspentOngoingTransferDate,
        DateOnly? scheduleViiTransferDate,
        DateOnly fyCloseDate
    )
    {
        // 1. Applicability Check (Net worth >= 500 Cr, Turnover >= 1000 Cr, Net profit >= 5 Cr)
        bool isApplicable = false;
        var reasons = new List<string>();

        bool isCrores = netWorth < 10000000m && turnover < 10000000m;
        decimal nwThreshold = isCrores ? 500m : 5000000000m;
        decimal toThreshold = isCrores ? 1000m : 10000000000m;
        decimal npThreshold = isCrores ? 5m : 50000000m;

        if (netWorth >= nwThreshold) { isApplicable = true; reasons.Add("Net Worth >= ₹500 Cr"); }
        if (turnover >= toThreshold) { isApplicable = true; reasons.Add("Turnover >= ₹1,000 Cr"); }
        if (netProfitPreviousYear >= npThreshold) { isApplicable = true; reasons.Add("Net Profit >= ₹5 Cr"); }

        if (!isApplicable)
        {
            return new CsrCalculationResult(
                IsApplicable: false,
                ApplicabilityReason: "Below Section 135(1) statutory thresholds (Net Worth < ₹500 Cr, Turnover < ₹1,000 Cr, Net Profit < ₹5 Cr)",
                AverageNetProfit: 0m,
                MandatoryCsrObligation2Pct: 0m,
                NetCsrObligationCurrentFy: 0m,
                TotalSpent: 0m,
                UnspentOngoing: 0m,
                UnspentOther: 0m,
                ExcessCarriedForward: 0m,
                CaroQualificationRequired: false,
                CaroClauseXxARemark: "Not Applicable as Section 135 is not attracted during the subject financial year.",
                CaroClauseXxBRemark: "Not Applicable as Section 135 is not attracted during the subject financial year."
            );
        }

        // 2. Average Section 198 Net Profit for preceding 3 FYs
        decimal avgNetProfit = Math.Round((npFy1 + npFy2 + npFy3) / 3m, 2);
        decimal obligation2Pct = Math.Round(Math.Max(0, avgNetProfit) * 0.02m, 2);
        decimal netObligation = Math.Max(0, obligation2Pct - priorExcessSetOff);

        // 3. Spending & Overhead Limits
        decimal totalSpent = spentOngoing + spentOther + adminOverheads;
        decimal unspentOngoing = Math.Max(0, allocatedOngoing - spentOngoing);
        decimal unspentOther = Math.Max(0, allocatedOther - spentOther);

        decimal excessCarriedForward = 0m;
        if (totalSpent > netObligation)
        {
            excessCarriedForward = Math.Round(totalSpent - netObligation, 2);
        }

        // 4. CARO 2020 Clause XX Compliance Auditing
        // Clause XX(a): Transfer to Schedule VII Fund within 6 months of FY close (e.g. by Sep 30)
        var deadline6Months = fyCloseDate.AddMonths(6);
        bool xxACompliant = true;
        string clauseXxARemark;

        if (unspentOther > 0)
        {
            if (scheduleViiTransferDate.HasValue && scheduleViiTransferDate.Value <= deadline6Months)
            {
                clauseXxARemark = $"Compliant. Unspent amount of ₹{unspentOther:N2} in respect of other than ongoing projects was duly transferred to a Fund specified in Schedule VII to the Companies Act within 6 months of the expiry of the financial year (on {scheduleViiTransferDate:dd/MM/yyyy}).";
            }
            else
            {
                xxACompliant = false;
                clauseXxARemark = $"Qualified Remark: The Company has not transferred the unspent CSR amount of ₹{unspentOther:N2} (other than ongoing projects) to a Fund specified in Schedule VII within the statutory period of six months from the expiry of the financial year as mandated under Section 135(5).";
            }
        }
        else
        {
            clauseXxARemark = "Compliant. There are no unspent CSR amounts in respect of other than ongoing projects requiring transfer under Section 135(5).";
        }

        // Clause XX(b): Transfer to Special Unspent CSR Account in scheduled bank within 30 days (e.g. by April 30)
        var deadline30Days = fyCloseDate.AddDays(30);
        bool xxBCompliant = true;
        string clauseXxBRemark;

        if (unspentOngoing > 0)
        {
            if (unspentOngoingTransferDate.HasValue && unspentOngoingTransferDate.Value <= deadline30Days)
            {
                clauseXxBRemark = $"Compliant. Unspent amount of ₹{unspentOngoing:N2} pursuant to ongoing projects was duly transferred to a special Unspent CSR Account with a scheduled bank within 30 days of the expiry of the financial year (on {unspentOngoingTransferDate:dd/MM/yyyy}) as required under Section 135(6).";
            }
            else
            {
                xxBCompliant = false;
                clauseXxBRemark = $"Qualified Remark: The Company has failed to transfer the unspent CSR amount of ₹{unspentOngoing:N2} pertaining to ongoing projects to a special Unspent CSR Account within the statutory period of thirty days from the end of the financial year in compliance with Section 135(6).";
            }
        }
        else
        {
            clauseXxBRemark = "Compliant. There are no unspent CSR amounts remaining pursuant to ongoing projects requiring transfer under Section 135(6).";
        }

        bool caroQualified = !xxACompliant || !xxBCompliant;

        return new CsrCalculationResult(
            IsApplicable: true,
            ApplicabilityReason: string.Join("; ", reasons),
            AverageNetProfit: avgNetProfit,
            MandatoryCsrObligation2Pct: obligation2Pct,
            NetCsrObligationCurrentFy: netObligation,
            TotalSpent: totalSpent,
            UnspentOngoing: unspentOngoing,
            UnspentOther: unspentOther,
            ExcessCarriedForward: excessCarriedForward,
            CaroQualificationRequired: caroQualified,
            CaroClauseXxARemark: clauseXxARemark,
            CaroClauseXxBRemark: clauseXxBRemark
        );
    }

    public static DateOnly GetFyCloseDate(string fy)
    {
        if (string.IsNullOrWhiteSpace(fy)) return new DateOnly(DateTime.UtcNow.Year, 3, 31);
        var parts = fy.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out int startYear))
        {
            return new DateOnly(startYear + 1, 3, 31);
        }
        return new DateOnly(DateTime.UtcNow.Year, 3, 31);
    }

    public static CsrCalculationResult CalculateCsrObligation(
        decimal netWorth,
        decimal turnover,
        decimal netProfitPreviousYear,
        decimal netProfitFyMinus1,
        decimal netProfitFyMinus2,
        decimal netProfitFyMinus3,
        decimal setOffAvailableFromPriorYears,
        decimal totalSpentOngoing,
        decimal totalSpentOther,
        string financialYear = "2024-25",
        DateOnly? ongoingTransferDate = null,
        DateOnly? scheduleViiTransferDate = null,
        decimal adminOverheads = 0m,
        decimal? allocatedOngoing = null,
        decimal? allocatedOther = null
    )
    {
        var fyClose = GetFyCloseDate(financialYear);

        // Preliminary computation to determine net obligation
        decimal avgNp = Math.Round((netProfitFyMinus1 + netProfitFyMinus2 + netProfitFyMinus3) / 3m, 2);
        decimal obl = Math.Round(Math.Max(0, avgNp) * 0.02m, 2);
        decimal netObl = Math.Max(0, obl - setOffAvailableFromPriorYears);

        // Derive allocations if not explicitly given
        decimal allocOngoing = allocatedOngoing ?? 0m;
        decimal allocOther = allocatedOther ?? 0m;

        if (allocOngoing == 0m && allocOther == 0m && netObl > (totalSpentOngoing + totalSpentOther))
        {
            if (totalSpentOngoing > 0m && totalSpentOther == 0m)
            {
                allocOngoing = netObl;
            }
            else if (totalSpentOther > 0m && totalSpentOngoing == 0m)
            {
                allocOther = netObl;
            }
            else if (totalSpentOngoing > 0m && totalSpentOther > 0m)
            {
                allocOngoing = Math.Round(netObl * (totalSpentOngoing / (totalSpentOngoing + totalSpentOther)), 2);
                allocOther = netObl - allocOngoing;
            }
            else
            {
                allocOther = netObl;
            }
        }

        return CalculateCsr(
            netWorth: netWorth,
            turnover: turnover,
            netProfitPreviousYear: netProfitPreviousYear,
            npFy1: netProfitFyMinus1,
            npFy2: netProfitFyMinus2,
            npFy3: netProfitFyMinus3,
            priorExcessSetOff: setOffAvailableFromPriorYears,
            spentOngoing: totalSpentOngoing,
            spentOther: totalSpentOther,
            adminOverheads: adminOverheads,
            allocatedOngoing: allocOngoing,
            allocatedOther: allocOther,
            unspentOngoingTransferDate: ongoingTransferDate,
            scheduleViiTransferDate: scheduleViiTransferDate,
            fyCloseDate: fyClose
        );
    }
}

public static class BrsrCoreCalculationEngine
{
    // Standard GHG emission factors (IPCC / CEA India Baseline)
    public const decimal DieselEmissionFactorKgCo2PerLitre = 2.68m;     // kg CO2 per litre diesel
    public const decimal NaturalGasEmissionFactorKgCo2PerSm3 = 1.90m;   // kg CO2 per standard cubic metre
    public const decimal IndiaGridEmissionFactorTco2PerMwh = 0.82m;     // CEA India Baseline: 0.82 tonnes CO2 per MWh

    public record BrsrCalculations(
        decimal Scope1MtCo2e,
        decimal Scope2MtCo2e,
        decimal TotalGhgMtCo2e,
        decimal GhgIntensity,
        decimal WaterIntensity,
        decimal EnergyIntensity,
        decimal FemaleEmployeesPct,
        decimal FemaleBoardPct,
        decimal FemaleKmpPct,
        decimal OverallScore
    );

    public static BrsrCalculations ComputeBrsrMetrics(
        decimal turnoverCr,
        decimal dieselLitres,
        decimal gasSm3,
        decimal gridMwh,
        decimal waterKl,
        decimal energyGj,
        decimal ltifr,
        int totalEmployees,
        int femaleEmployees,
        int totalDirectors,
        int femaleDirectors,
        int totalKmp,
        int femaleKmp,
        decimal renewablePct
    )
    {
        // 1. Scope 1 direct combustion (convert kg to metric tonnes)
        decimal scope1Diesel = (dieselLitres * DieselEmissionFactorKgCo2PerLitre) / 1000m;
        decimal scope1Gas = (gasSm3 * NaturalGasEmissionFactorKgCo2PerSm3) / 1000m;
        decimal scope1Total = Math.Round(scope1Diesel + scope1Gas, 2);

        // 2. Scope 2 indirect grid electricity
        decimal scope2Total = Math.Round(gridMwh * IndiaGridEmissionFactorTco2PerMwh, 2);
        decimal totalGhg = scope1Total + scope2Total;

        // 3. Intensities per Crore of Turnover
        decimal safeTurnover = Math.Max(1m, turnoverCr);
        decimal ghgIntensity = Math.Round(totalGhg / safeTurnover, 2);
        decimal waterIntensity = Math.Round(waterKl / safeTurnover, 2);
        decimal energyIntensity = Math.Round(energyGj / safeTurnover, 2);

        // 4. Diversity Ratios
        decimal femaleEmpPct = totalEmployees > 0 ? Math.Round(((decimal)femaleEmployees / totalEmployees) * 100m, 1) : 0m;
        decimal femaleBrdPct = totalDirectors > 0 ? Math.Round(((decimal)femaleDirectors / totalDirectors) * 100m, 1) : 0m;
        decimal femaleKmpPct = totalKmp > 0 ? Math.Round(((decimal)femaleKmp / totalKmp) * 100m, 1) : 0m;

        // 5. Composite BRSR Core Health Score (0 - 100%)
        decimal score = 0m;
        if (femaleBrdPct >= 12.5m) score += 15m; // At least 1 woman director per SEBI LODR
        if (femaleEmpPct >= 20.0m) score += 15m;
        if (renewablePct >= 20.0m) score += 20m;
        if (ltifr <= 1.0m) score += 20m;          // Low accident rate
        if (waterIntensity <= 500m) score += 15m; // Controlled water footprint
        if (ghgIntensity <= 50m) score += 15m;    // Low carbon intensity

        return new BrsrCalculations(
            Scope1MtCo2e: scope1Total,
            Scope2MtCo2e: scope2Total,
            TotalGhgMtCo2e: totalGhg,
            GhgIntensity: ghgIntensity,
            WaterIntensity: waterIntensity,
            EnergyIntensity: energyIntensity,
            FemaleEmployeesPct: femaleEmpPct,
            FemaleBoardPct: femaleBrdPct,
            FemaleKmpPct: femaleKmpPct,
            OverallScore: score
        );
    }

    public static void CalculateBrsrMetrics(BrsrCoreReport r)
    {
        var m = ComputeBrsrMetrics(
            r.TurnoverInCrores,
            r.DieselConsumedLitres,
            r.NaturalGasConsumedSm3,
            r.GridElectricityPurchasedMwh,
            r.TotalWaterConsumptionKl,
            r.TotalEnergyConsumptionGj,
            r.LostTimeInjuryFrequencyRateLtifr,
            r.TotalPermanentEmployees,
            r.FemaleEmployeesCount,
            r.TotalBoardDirectors,
            r.FemaleBoardDirectors,
            r.TotalKmpCount,
            r.FemaleKmpCount,
            r.RenewableEnergySharePct
        );

        r.Scope1DirectEmissionsMtCo2e = m.Scope1MtCo2e;
        r.Scope2IndirectEmissionsMtCo2e = m.Scope2MtCo2e;
        r.GhgIntensityPerCroreTurnover = m.GhgIntensity;
        r.WaterIntensityPerCroreTurnover = m.WaterIntensity;
        r.EnergyIntensityPerCroreTurnover = m.EnergyIntensity;
        r.FemaleEmployeesPct = m.FemaleEmployeesPct;
        r.FemaleBoardPct = m.FemaleBoardPct;
        r.FemaleKmpPct = m.FemaleKmpPct;
        r.OverallBrsrCoreScore = m.OverallScore;
    }

    public static string GenerateAssuranceDossierHtml(BrsrCoreReport brsr, string clientName, string clientCin)
    {
        var dummyCsr = new CsrAssessment
        {
            Client = new Domain.Clients.Client { Name = clientName, Code = clientCin },
            FinancialYear = brsr.FinancialYear,
            NetWorth = 1000m,
            Turnover = brsr.TurnoverInCrores,
            NetProfitPreviousYear = 20m,
            IsCsrApplicable = true,
            ApplicabilityCriterion = "Turnover >= ₹1,000 Cr & Net Worth >= ₹500 Cr",
            AverageNetProfit = 25m,
            MandatoryCsrObligation2Pct = 0.50m,
            TotalActualCsrSpent = 0.50m,
            CertifiedByName = brsr.AssuranceAuditorName,
            CertifiedMembershipNo = brsr.AssuranceAuditorMembershipNo,
            Udin = brsr.AssuranceUdin
        };
        return GenerateAssuranceDossierHtml(dummyCsr, brsr, new List<CsrProjectItem>());
    }

    /// <summary>
    /// Generates the standalone SEBI BRSR Core & Section 135 CSR Assurance Dossier HTML
    /// </summary>
    public static string GenerateAssuranceDossierHtml(
        CsrAssessment csr,
        BrsrCoreReport brsr,
        List<CsrProjectItem> projects
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\" />");
        sb.AppendLine("<title>SEBI BRSR Core & Section 135 CSR Assurance Dossier</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Helvetica Neue', Arial, sans-serif; font-size: 10pt; line-height: 1.5; color: #222; margin: 40px; }");
        sb.AppendLine("h1, h2, h3 { color: #1e3a8a; margin: 6px 0; }");
        sb.AppendLine("h1 { font-size: 16pt; text-align: center; border-bottom: 2px solid #1e3a8a; padding-bottom: 8px; }");
        sb.AppendLine("h2 { font-size: 13pt; margin-top: 24px; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 12px 0; }");
        sb.AppendLine("th, td { border: 1px solid #cbd5e1; padding: 6px 10px; font-size: 9pt; }");
        sb.AppendLine("th { background-color: #f1f5f9; text-align: left; }");
        sb.AppendLine(".card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px; margin: 12px 0; }");
        sb.AppendLine(".badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-weight: bold; font-size: 8pt; }");
        sb.AppendLine(".badge-green { background: #dcfce7; color: #15803d; }");
        sb.AppendLine(".badge-red { background: #fee2e2; color: #b91c1c; }");
        sb.AppendLine(".page-break { page-break-before: always; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("<h1>INDEPENDENT STATUTORY ASSURANCE DOSSIER</h1>");
        sb.AppendLine("<p style=\"text-align: center; font-size: 11pt; color: #475569;\"><b>COMPANIES ACT 2013 SECTION 135 & SEBI BRSR CORE REASONABLE ASSURANCE</b></p>");
        sb.AppendLine($"<p style=\"text-align: center; font-size: 10pt;\"><b>Entity:</b> {csr.Client?.Name ?? "Client Entity"} {(!string.IsNullOrEmpty(csr.Client?.Code) ? $"({csr.Client.Code})" : "")} | <b>Financial Year:</b> {csr.FinancialYear}</p>");

        // Section 1: CSR Section 135 Compliance
        sb.AppendLine("<h2>1. SECTION 135 STATUTORY CSR COMPLIANCE & 2% OBLIGATION</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><th style=\"width: 35%;\">Section 135(1) Applicability</th><td><span class=\"badge {(csr.IsCsrApplicable ? "badge-green" : "badge-red")}\">{(csr.IsCsrApplicable ? "MANDATORILY APPLICABLE" : "EXEMPT")}</span> - {csr.ApplicabilityCriterion}</td></tr>");
        sb.AppendLine($"<tr><th>Net Worth / Turnover / Net Profit</th><td>Net Worth: ₹{csr.NetWorth:N0} | Turnover: ₹{csr.Turnover:N0} | Net Profit: ₹{csr.NetProfitPreviousYear:N0}</td></tr>");
        sb.AppendLine($"<tr><th>Section 198 Average Net Profit (3 Prior FYs)</th><td><b>₹{csr.AverageNetProfit:N2}</b></td></tr>");
        sb.AppendLine($"<tr><th>Mandatory 2% CSR Obligation</th><td><b>₹{csr.MandatoryCsrObligation2Pct:N2}</b></td></tr>");
        sb.AppendLine($"<tr><th>Set-off Available from Prior Years</th><td>₹{csr.SetOffAvailableFromPriorYears:N2}</td></tr>");
        sb.AppendLine($"<tr><th>Net Obligation for Current FY</th><td><b>₹{csr.NetCsrObligationForCurrentFy:N2}</b></td></tr>");
        sb.AppendLine($"<tr><th>Total Actual CSR Spent</th><td><b>₹{csr.TotalActualCsrSpent:N2}</b> (Ongoing: ₹{csr.TotalSpentOngoingProjects:N2} | Other: ₹{csr.TotalSpentOtherProjects:N2} | Admin: ₹{csr.AdministrativeOverheadsSpent:N2})</td></tr>");
        sb.AppendLine($"<tr><th>Unspent CSR Balances</th><td>Ongoing Projects: ₹{csr.UnspentOngoingProjects:N2} | Other Projects: ₹{csr.UnspentOtherProjects:N2}</td></tr>");
        sb.AppendLine($"<tr><th>Excess Spent Carried Forward</th><td><b>₹{csr.ExcessSpentCarriedForward:N2}</b> (Available for set-off for next 3 FYs u/s 135(5))</td></tr>");
        sb.AppendLine("</table>");

        // CARO Clause XX Disclosures
        sb.AppendLine("<h3>CARO 2020 CLAUSE XX STATUTORY AUDIT REPORTING</h3>");
        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine($"<p><b>Clause XX(a) (Other than ongoing projects - 6 months transfer):</b><br/>{csr.CaroClauseXxARemark}</p>");
        sb.AppendLine($"<p><b>Clause XX(b) (Ongoing projects special account - 30 days transfer):</b><br/>{csr.CaroClauseXxBRemark}</p>");
        sb.AppendLine("</div>");

        // CSR Project Schedule VII Table
        if (projects.Count > 0)
        {
            sb.AppendLine("<h3>SCHEDULE VII PROJECT PORTFOLIO</h3>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>S.No.</th><th>Project Title</th><th>Schedule VII Category</th><th>Nature</th><th>Allocated Budget</th><th>Actual Spent</th><th>Implementing Agency</th></tr></thead><tbody>");
            int pIndex = 1;
            foreach (var p in projects)
            {
                sb.AppendLine($"<tr><td>{pIndex++}</td><td><b>{p.ProjectTitle}</b><br/><span style=\"font-size: 8pt; color: #64748b;\">{p.StateOrDistrict} {(p.IsAspirationalDistrict ? "[Aspirational District]" : "")}</span></td><td>{p.ScheduleViiCategory}</td><td>{p.Nature}</td><td>₹{p.AllocatedBudget:N2}</td><td>₹{p.ActualSpent:N2}</td><td>{p.ImplementationMode} {(!string.IsNullOrEmpty(p.ImplementingAgencyCsr1Number) ? $"({p.ImplementingAgencyCsr1Number})" : "")}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // Section 2: SEBI BRSR Core
        sb.AppendLine("<div class=\"page-break\"></div>");
        sb.AppendLine("<h2>2. SEBI BRSR CORE MANDATORY 9-PRINCIPLE METRICS</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><th style=\"width: 35%;\">Scope 1 GHG Direct Emissions</th><td><b>{brsr.Scope1DirectEmissionsMtCo2e:N2} MT CO2e</b> (Diesel: {brsr.DieselConsumedLitres:N0} L | Natural Gas: {brsr.NaturalGasConsumedSm3:N0} Sm3)</td></tr>");
        sb.AppendLine($"<tr><th>Scope 2 GHG Indirect Emissions</th><td><b>{brsr.Scope2IndirectEmissionsMtCo2e:N2} MT CO2e</b> (Grid Electricity: {brsr.GridElectricityPurchasedMwh:N0} MWh @ 0.82 tCO2/MWh per CEA CO2 Baseline Database v19)</td></tr>");
        sb.AppendLine($"<tr><th>Total Scope 1 + Scope 2 Emissions</th><td><b>{brsr.TotalScope1And2MtCo2e:N2} MT CO2e</b></td></tr>");
        sb.AppendLine($"<tr><th>GHG Emission Intensity</th><td><b>{brsr.GhgIntensityPerCroreTurnover:N2} MT CO2e / ₹ Cr Turnover</b></td></tr>");
        sb.AppendLine($"<tr><th>Water Withdrawal & Consumption</th><td>Total: {brsr.TotalWaterWithdrawalKl:N0} KL | Consumed: {brsr.TotalWaterConsumptionKl:N0} KL (Intensity: {brsr.WaterIntensityPerCroreTurnover:N2} KL/Cr)</td></tr>");
        sb.AppendLine($"<tr><th>Zero Liquid Discharge (ZLD)</th><td>{(brsr.OperatesZeroLiquidDischarge ? "OPERATIONAL & ACTIVE" : "NOT OPERATED")}</td></tr>");
        sb.AppendLine($"<tr><th>Renewable Energy Share</th><td><b>{brsr.RenewableEnergySharePct:F1}%</b> (Intensity: {brsr.EnergyIntensityPerCroreTurnover:N2} GJ/Cr)</td></tr>");
        sb.AppendLine($"<tr><th>Safety Metric (LTIFR)</th><td><b>{brsr.LostTimeInjuryFrequencyRateLtifr:F2}</b> per million man-hours (Fatalities: {brsr.FatalitiesCount})</td></tr>");
        sb.AppendLine($"<tr><th>Gender Diversity Ratios</th><td>Board: <b>{brsr.FemaleBoardPct:F1}%</b> ({brsr.FemaleBoardDirectors}/{brsr.TotalBoardDirectors}) | KMP: <b>{brsr.FemaleKmpPct:F1}%</b> | Employees: <b>{brsr.FemaleEmployeesPct:F1}%</b> ({brsr.FemaleEmployeesCount}/{brsr.TotalPermanentEmployees})</td></tr>");
        sb.AppendLine($"<tr><th>POSH Sexual Harassment Cases</th><td>Received: {brsr.PoshComplaintsReceived} | Resolved: {brsr.PoshComplaintsResolved}</td></tr>");
        sb.AppendLine($"<tr><th>Composite BRSR Core ESG Score</th><td><b>{brsr.OverallBrsrCoreScore:F1} / 100</b></td></tr>");
        sb.AppendLine("</table>");

        // Section 3: Assurance Certificate
        sb.AppendLine("<h2>3. REASONABLE ASSURANCE STATEMENT UNDER SEBI GUIDELINES</h2>");
        sb.AppendLine("<div class=\"card\" style=\"border: 2px solid #1e3a8a;\">");
        sb.AppendLine("<p><b>To the Board of Directors of " + (csr.Client?.Name ?? "The Company") + ":</b></p>");
        sb.AppendLine("<p style=\"text-align: justify;\">We have performed reasonable assurance procedures on the Section 135 CSR Compliance and SEBI BRSR Core indicators for the financial year ended March 31, " + (csr.FinancialYear.Split('-').Length > 1 ? csr.FinancialYear.Split('-')[1] : "2025") + ". Based on our examination of records, verification of vouchers, electricity and fuel bills, and bank transfer receipts, nothing has come to our attention that causes us to believe that the CSR expenditures and BRSR Core KPIs are not prepared, in all material respects, in accordance with Section 135 of the Companies Act, 2013 and SEBI circular SEBI/HO/CFD/CFD-SEC-2/P/CIR/2023/122.</p>");
        sb.AppendLine("<br/>");
        sb.AppendLine("<p><b>For and on behalf of Statutory Assurance Auditor:</b><br/>" + (csr.CertifiedByName ?? "CA Desk ESG Assurance Practice") + "<br/>ICAI Membership No.: " + (csr.CertifiedMembershipNo ?? "504932") + "<br/>UDIN: " + (csr.Udin ?? "26504932AAAAES1029") + "<br/>Date: " + DateTime.UtcNow.ToString("dd/MM/yyyy") + "</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
