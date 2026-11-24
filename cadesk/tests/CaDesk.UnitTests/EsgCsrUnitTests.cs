using CaDesk.Domain.EsgCsr;
using Xunit;

namespace CaDesk.UnitTests;

public class EsgCsrUnitTests
{
    [Fact]
    public void CsrSection135Engine_Applicability_NetWorthThreshold()
    {
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 550m,
            turnover: 300m,
            netProfitPreviousYear: 2m,
            netProfitFyMinus1: 3m,
            netProfitFyMinus2: 2m,
            netProfitFyMinus3: 1m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0,
            totalSpentOther: 0
        );

        Assert.True(res.IsApplicable);
        Assert.Contains("Net Worth >= ₹500 Cr", res.ApplicabilityReason);
        Assert.Equal(2.0m, res.AverageNetProfit);
        Assert.Equal(0.04m, res.MandatoryCsrObligation2Pct); // 2% of 2 Cr = 0.04 Cr (4 Lakhs)
    }

    [Fact]
    public void CsrSection135Engine_Applicability_TurnoverThreshold()
    {
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 100m,
            turnover: 1200m,
            netProfitPreviousYear: 3m,
            netProfitFyMinus1: 15m,
            netProfitFyMinus2: 12m,
            netProfitFyMinus3: 9m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0,
            totalSpentOther: 0
        );

        Assert.True(res.IsApplicable);
        Assert.Contains("Turnover >= ₹1,000 Cr", res.ApplicabilityReason);
        Assert.Equal(12.0m, res.AverageNetProfit);
        Assert.Equal(0.24m, res.MandatoryCsrObligation2Pct); // 2% of 12 Cr = 0.24 Cr
    }

    [Fact]
    public void CsrSection135Engine_Applicability_NetProfitThreshold()
    {
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 100m,
            turnover: 200m,
            netProfitPreviousYear: 7.5m,
            netProfitFyMinus1: 8m,
            netProfitFyMinus2: 7m,
            netProfitFyMinus3: 6m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0,
            totalSpentOther: 0
        );

        Assert.True(res.IsApplicable);
        Assert.Contains("Net Profit >= ₹5 Cr", res.ApplicabilityReason);
        Assert.Equal(7.0m, res.AverageNetProfit);
        Assert.Equal(0.14m, res.MandatoryCsrObligation2Pct); // 2% of 7 Cr = 0.14 Cr
    }

    [Fact]
    public void CsrSection135Engine_NotApplicable_WhenBelowAllThresholds()
    {
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 200m,
            turnover: 400m,
            netProfitPreviousYear: 3m,
            netProfitFyMinus1: 3m,
            netProfitFyMinus2: 2m,
            netProfitFyMinus3: 1m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0,
            totalSpentOther: 0
        );

        Assert.False(res.IsApplicable);
        Assert.Equal(0m, res.MandatoryCsrObligation2Pct);
        Assert.Equal(0m, res.NetCsrObligationCurrentFy);
        Assert.False(res.CaroQualificationRequired);
        Assert.Contains("not applicable", res.CaroClauseXxARemark, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CsrSection135Engine_PriorYearSetOff_ReducesNetObligation()
    {
        // 3-yr profits: 30, 40, 50 -> Avg = 40 Cr. 2% = 0.80 Cr.
        // Set-off from prior years = 0.30 Cr -> Net obligation = 0.50 Cr.
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 600m,
            turnover: 1500m,
            netProfitPreviousYear: 35m,
            netProfitFyMinus1: 50m,
            netProfitFyMinus2: 40m,
            netProfitFyMinus3: 30m,
            setOffAvailableFromPriorYears: 0.30m,
            totalSpentOngoing: 0.30m,
            totalSpentOther: 0.20m
        );

        Assert.True(res.IsApplicable);
        Assert.Equal(40.0m, res.AverageNetProfit);
        Assert.Equal(0.80m, res.MandatoryCsrObligation2Pct);
        Assert.Equal(0.50m, res.NetCsrObligationCurrentFy);
        Assert.Equal(0.50m, res.TotalSpent); // 0.30 + 0.20
        Assert.Equal(0m, res.UnspentOngoing);
        Assert.Equal(0m, res.UnspentOther);
        Assert.False(res.CaroQualificationRequired);
    }

    [Fact]
    public void CsrSection135Engine_CaroClauseXxA_TimelyTransferToScheduleViiFund_Compliant()
    {
        // FY 2024-25 ends on 31-03-2025. 6-month deadline is 30-09-2025.
        // Actual transfer date 15-08-2025 -> compliant!
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 800m,
            turnover: 2000m,
            netProfitPreviousYear: 20m,
            netProfitFyMinus1: 20m,
            netProfitFyMinus2: 20m,
            netProfitFyMinus3: 20m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0,
            totalSpentOther: 0.10m, // 0.10 spent vs 0.40 obligation -> 0.30 unspent
            financialYear: "2024-25",
            scheduleViiTransferDate: new DateOnly(2025, 8, 15)
        );

        Assert.Equal(0.40m, res.MandatoryCsrObligation2Pct);
        Assert.Equal(0.30m, res.UnspentOther);
        Assert.False(res.CaroQualificationRequired);
        Assert.Contains("was duly transferred to a Fund specified in Schedule VII", res.CaroClauseXxARemark);
    }

    [Fact]
    public void CsrSection135Engine_CaroClauseXxA_DelayedTransfer_RequiresQualification()
    {
        // FY 2024-25 ends 31-03-2025. 6-month deadline is 30-09-2025.
        // Actual transfer date 15-11-2025 (delayed by 46 days) -> qualification required!
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 800m,
            turnover: 2000m,
            netProfitPreviousYear: 20m,
            netProfitFyMinus1: 20m,
            netProfitFyMinus2: 20m,
            netProfitFyMinus3: 20m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0,
            totalSpentOther: 0.10m,
            financialYear: "2024-25",
            scheduleViiTransferDate: new DateOnly(2025, 11, 15)
        );

        Assert.Equal(0.30m, res.UnspentOther);
        Assert.True(res.CaroQualificationRequired);
        Assert.Contains("The Company has not transferred the unspent CSR amount", res.CaroClauseXxARemark);
    }

    [Fact]
    public void CsrSection135Engine_CaroClauseXxB_TimelyTransferToSpecialEscrowAccount_Compliant()
    {
        // FY 2024-25 ends 31-03-2025. 30-day deadline is 30-04-2025.
        // Transfer date 20-04-2025 -> compliant!
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 600m,
            turnover: 1200m,
            netProfitPreviousYear: 25m,
            netProfitFyMinus1: 25m,
            netProfitFyMinus2: 25m,
            netProfitFyMinus3: 25m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0.20m, // 0.20 spent vs 0.50 obligation -> 0.30 unspent
            totalSpentOther: 0,
            financialYear: "2024-25",
            ongoingTransferDate: new DateOnly(2025, 4, 20)
        );

        Assert.Equal(0.50m, res.MandatoryCsrObligation2Pct);
        Assert.Equal(0.30m, res.UnspentOngoing);
        Assert.False(res.CaroQualificationRequired);
        Assert.Contains("was duly transferred to a special Unspent CSR Account", res.CaroClauseXxBRemark);
        Assert.Contains("Unspent CSR Account", res.CaroClauseXxBRemark);
    }

    [Fact]
    public void CsrSection135Engine_CaroClauseXxB_DefaultInTransfer_RequiresQualification()
    {
        // Ongoing project unspent but not transferred (transfer date is null)
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 600m,
            turnover: 1200m,
            netProfitPreviousYear: 25m,
            netProfitFyMinus1: 25m,
            netProfitFyMinus2: 25m,
            netProfitFyMinus3: 25m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0.10m,
            totalSpentOther: 0,
            financialYear: "2024-25",
            ongoingTransferDate: null
        );

        Assert.Equal(0.40m, res.UnspentOngoing);
        Assert.True(res.CaroQualificationRequired);
        Assert.Contains("The Company has failed to transfer the unspent CSR amount", res.CaroClauseXxBRemark);
    }

    [Fact]
    public void CsrSection135Engine_ExcessSpent_CarriedForwardUnderSection135_5()
    {
        // Obligation is 0.50 Cr, actual spent is 0.85 Cr -> 0.35 Cr carried forward
        var res = CsrSection135Engine.CalculateCsrObligation(
            netWorth: 600m,
            turnover: 1200m,
            netProfitPreviousYear: 25m,
            netProfitFyMinus1: 25m,
            netProfitFyMinus2: 25m,
            netProfitFyMinus3: 25m,
            setOffAvailableFromPriorYears: 0,
            totalSpentOngoing: 0.45m,
            totalSpentOther: 0.40m
        );

        Assert.Equal(0.50m, res.MandatoryCsrObligation2Pct);
        Assert.Equal(0.85m, res.TotalSpent);
        Assert.Equal(0.35m, res.ExcessCarriedForward);
        Assert.Equal(0m, res.UnspentOngoing);
        Assert.Equal(0m, res.UnspentOther);
        Assert.False(res.CaroQualificationRequired);
    }

    [Fact]
    public void BrsrCoreCalculationEngine_ComputesScope1And2EmissionsAndIntensities()
    {
        var r = new BrsrCoreReport
        {
            TurnoverInCrores = 200m,
            DieselConsumedLitres = 50_000m,        // 50,000 * 2.68 / 1000 = 134 MT CO2e
            NaturalGasConsumedSm3 = 20_000m,      // 20,000 * 1.90 / 1000 = 38 MT CO2e -> Scope 1 = 172 MT CO2e
            GridElectricityPurchasedMwh = 1000m,  // 1000 * 0.82 = 820 MT CO2e -> Scope 2 = 820 MT CO2e
            TotalWaterWithdrawalKl = 25_000m,
            TotalWaterConsumptionKl = 20_000m,
            OperatesZeroLiquidDischarge = true,
            TotalEnergyConsumptionGj = 15_000m,
            RenewableEnergySharePct = 40.0m,
            LostTimeInjuryFrequencyRateLtifr = 0.12m,
            FatalitiesCount = 0,
            TotalPermanentEmployees = 500,
            FemaleEmployeesCount = 160,
            TotalBoardDirectors = 10,
            FemaleBoardDirectors = 3,
            TotalKmpCount = 5,
            FemaleKmpCount = 2
        };

        BrsrCoreCalculationEngine.CalculateBrsrMetrics(r);

        Assert.Equal(172.0m, r.Scope1DirectEmissionsMtCo2e);
        Assert.Equal(820.0m, r.Scope2IndirectEmissionsMtCo2e);
        Assert.Equal(992.0m, r.TotalScope1And2MtCo2e);

        // GHG Intensity = 992 MT / 200 Cr = 4.96 MT / Cr
        Assert.Equal(4.96m, r.GhgIntensityPerCroreTurnover);

        // Water Intensity = 20,000 KL / 200 Cr = 100 KL / Cr
        Assert.Equal(100.0m, r.WaterIntensityPerCroreTurnover);

        // Energy Intensity = 15,000 GJ / 200 Cr = 75 GJ / Cr
        Assert.Equal(75.0m, r.EnergyIntensityPerCroreTurnover);

        // Gender diversity
        Assert.Equal(32.0m, r.FemaleEmployeesPct); // 160/500 = 32%
        Assert.Equal(30.0m, r.FemaleBoardPct);     // 3/10 = 30%
        Assert.Equal(40.0m, r.FemaleKmpPct);       // 2/5 = 40%

        // Score should be high (above 80%) with 0 fatalities, ZLD, and strong gender diversity
        Assert.True(r.OverallBrsrCoreScore >= 80m);
    }

    [Fact]
    public void BrsrCoreCalculationEngine_GeneratesPrintReadyAssuranceDossierHtml()
    {
        var r = new BrsrCoreReport
        {
            FinancialYear = "2024-25",
            TurnoverInCrores = 500m,
            DieselConsumedLitres = 80_000m,
            NaturalGasConsumedSm3 = 40_000m,
            GridElectricityPurchasedMwh = 2_500m,
            OperatesZeroLiquidDischarge = true,
            RenewableEnergySharePct = 35m,
            TotalPermanentEmployees = 1200,
            FemaleEmployeesCount = 380,
            TotalBoardDirectors = 12,
            FemaleBoardDirectors = 4,
            AssuranceAuditorName = "CA Ramesh Gupta",
            AssuranceAuditorMembershipNo = "094321",
            AssuranceUdin = "24094321AAAAAA9999"
        };
        BrsrCoreCalculationEngine.CalculateBrsrMetrics(r);

        string html = BrsrCoreCalculationEngine.GenerateAssuranceDossierHtml(r, "Tata Steel Processing Ltd", "L27100MH1990PLC056789");

        Assert.Contains("Tata Steel Processing Ltd", html);
        Assert.Contains("L27100MH1990PLC056789", html);
        Assert.Contains("INDEPENDENT STATUTORY ASSURANCE DOSSIER", html);
        Assert.Contains("SEBI BRSR CORE REASONABLE ASSURANCE", html);
        Assert.Contains("CEA CO2 Baseline Database", html);
        Assert.Contains("CA Ramesh Gupta", html);
        Assert.Contains("24094321AAAAAA9999", html);
    }
}
