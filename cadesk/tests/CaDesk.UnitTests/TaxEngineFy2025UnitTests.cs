using CaDesk.Application.Tax;
using Xunit;

namespace CaDesk.UnitTests;

/// <summary>Finance Act 2025 rules (FY 2025-26 / AY 2026-27) and the year switch in the tax engine.</summary>
public class TaxEngineFy2025UnitTests
{
    private static CalculateTaxRequest Req(string fy, decimal otherIncome = 0, decimal stcg111A = 0, decimal grossSalary = 0) =>
        new(Guid.NewGuid(), AyFor(fy), fy, "individual", null,
            GrossSalary: grossSalary, HraReceived: 0, RentPaid: 0, IsMetro: false, ProfessionalTax: 0,
            AnnualRentReceived: 0, MunicipalTaxesPaid: 0, HousingLoanInterest: 0, IsSelfOccupied: false,
            BusinessGrossTurnover: 0, NetProfit: 0, PresumptiveSection: null,
            Stcg111A: stcg111A, StcgNormal: 0, Ltcg112A: 0, Ltcg112: 0,
            SavingsInterest: 0, FdInterest: 0, DividendIncome: 0, OtherIncome: otherIncome,
            Section80C: 0, Section80Ccd1B: 0, Section80D: 0, Section80E: 0, Section80G: 0, Section80Tta: 0,
            TdsPaid: 0, TcsPaid: 0, AdvanceTaxPaid: 0, SelfAssessmentTaxPaid: 0, Notes: null);

    private static string AyFor(string fy) => $"{int.Parse(fy[..4]) + 1}-{(int.Parse(fy[..4]) + 2) % 100:D2}";

    [Fact]
    public void Salary_of_12_75_lakh_pays_no_tax_in_FY_2025_26()
    {
        // 12,75,000 - 75,000 standard deduction = 12,00,000; slab tax 20,000 + 40,000 = 60,000, fully rebated
        var r = TaxEngine.ComputeRegime(Req("2025-26", grossSalary: 1275000m), isNewRegime: true);

        Assert.Equal(1200000m, r.TaxableIncome);
        Assert.Equal(60000m, r.BaseTax);
        Assert.Equal(60000m, r.Rebate87A);
        Assert.Equal(0m, r.TotalTaxLiability);
    }

    [Fact]
    public void Marginal_relief_caps_tax_at_the_income_above_12_lakh()
    {
        // 12,10,000: slab tax 61,500; without relief that is 61,500 of tax on 10,000 above the limit.
        // Marginal relief: tax = 10,000 (+4% cess = 10,400)
        var r = TaxEngine.ComputeRegime(Req("2025-26", otherIncome: 1210000m), isNewRegime: true);

        Assert.Equal(61500m, r.BaseTax);
        Assert.Equal(51500m, r.Rebate87A);
        Assert.Equal(10400m, r.TotalTaxLiability);
    }

    [Fact]
    public void No_rebate_or_relief_well_above_the_limit()
    {
        // 16,00,000: 20,000 + 40,000 + 60,000 = 1,20,000 + 4% cess
        var r = TaxEngine.ComputeRegime(Req("2025-26", otherIncome: 1600000m), isNewRegime: true);

        Assert.Equal(120000m, r.BaseTax);
        Assert.Equal(0m, r.Rebate87A);
        Assert.Equal(124800m, r.TotalTaxLiability);
        Assert.Equal(["₹0 - ₹4,00,000", "₹4,00,001 - ₹8,00,000", "₹8,00,001 - ₹12,00,000", "₹12,00,001 - ₹16,00,000"],
            r.SlabBreakdown.Select(s => s.SlabRange));
    }

    [Fact]
    public void Rebate_does_not_cover_tax_on_111A_gains_from_FY_2025_26()
    {
        // normal income 10,00,000 (slab tax 40,000, rebated) + STCG 1,00,000 @ 20% = 20,000 still payable
        var r = TaxEngine.ComputeRegime(Req("2025-26", otherIncome: 1000000m, stcg111A: 100000m), isNewRegime: true);

        Assert.Equal(40000m, r.BaseTax);
        Assert.Equal(20000m, r.SpecialRateTax);
        Assert.Equal(40000m, r.Rebate87A);
        Assert.Equal(20800m, r.TotalTaxLiability);
    }

    [Fact]
    public void FY_2024_25_keeps_its_own_slabs_and_7_lakh_limit()
    {
        // same 12,00,000 income in FY 2024-25: 20,000 + 30,000 + 30,000 = 80,000, no rebate above 7 lakh
        var r = TaxEngine.ComputeRegime(Req("2024-25", otherIncome: 1200000m), isNewRegime: true);

        Assert.Equal(80000m, r.BaseTax);
        Assert.Equal(0m, r.Rebate87A);
    }

    [Fact]
    public void Old_regime_surcharge_above_5_crore_is_37_percent_new_regime_25()
    {
        var oldR = TaxEngine.ComputeRegime(Req("2025-26", otherIncome: 60000000m), isNewRegime: false);
        var newR = TaxEngine.ComputeRegime(Req("2025-26", otherIncome: 60000000m), isNewRegime: true);

        Assert.Equal(Math.Round((oldR.BaseTax - oldR.Rebate87A) * 0.37m, 2), oldR.Surcharge);
        Assert.Equal(Math.Round((newR.BaseTax - newR.Rebate87A) * 0.25m, 2), newR.Surcharge);
    }
}
