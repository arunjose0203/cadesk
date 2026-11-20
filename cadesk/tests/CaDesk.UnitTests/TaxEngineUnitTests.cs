using CaDesk.Application.Tax;
using Xunit;

namespace CaDesk.UnitTests;

public class TaxEngineUnitTests
{
    [Fact]
    public void NewRegime_IncomeSevenLakhs_Rebate87AResultsInZeroTax()
    {
        // Gross Salary 7,75,000 - 75,000 Std Deduction = 7,00,000 Taxable Income
        var req = new CalculateTaxRequest(
            Guid.NewGuid(), "2025-26", "2024-25", "individual", "new",
            GrossSalary: 775000m, HraReceived: 0, RentPaid: 0, IsMetro: false, ProfessionalTax: 0,
            AnnualRentReceived: 0, MunicipalTaxesPaid: 0, HousingLoanInterest: 0, IsSelfOccupied: false,
            BusinessGrossTurnover: 0, NetProfit: 0, PresumptiveSection: null,
            Stcg111A: 0, StcgNormal: 0, Ltcg112A: 0, Ltcg112: 0,
            SavingsInterest: 0, FdInterest: 0, DividendIncome: 0, OtherIncome: 0,
            Section80C: 0, Section80Ccd1B: 0, Section80D: 0, Section80E: 0, Section80G: 0, Section80Tta: 0,
            TdsPaid: 0, TcsPaid: 0, AdvanceTaxPaid: 0, SelfAssessmentTaxPaid: 0,
            Notes: null
        );

        var result = TaxEngine.ComputeRegime(req, isNewRegime: true);

        Assert.Equal(75000m, result.StandardDeduction);
        Assert.Equal(700000m, result.TaxableIncome);
        // Slab tax on 7L under New Regime: 0-3L (0) + 3-7L @ 5% (20,000) = 20,000
        Assert.Equal(20000m, result.BaseTax);
        // 87A rebate covers full 20,000 for income <= 7L
        Assert.Equal(20000m, result.Rebate87A);
        Assert.Equal(0m, result.TotalTaxLiability);
    }

    [Fact]
    public void NewRegime_TenLakhsIncome_ComputesCorrectSlabsAndCess()
    {
        // 10,75,000 - 75,000 = 10,00,000 Taxable Income
        // Slabs:
        // 0-3L: 0
        // 3L-7L (4L @ 5%): 20,000
        // 7L-10L (3L @ 10%): 30,000
        // Total Base Tax: 50,000
        // Cess @ 4%: 2,000
        // Total Tax: 52,000
        var req = new CalculateTaxRequest(
            Guid.NewGuid(), "2025-26", "2024-25", "individual", "new",
            GrossSalary: 1075000m, HraReceived: 0, RentPaid: 0, IsMetro: false, ProfessionalTax: 0,
            AnnualRentReceived: 0, MunicipalTaxesPaid: 0, HousingLoanInterest: 0, IsSelfOccupied: false,
            BusinessGrossTurnover: 0, NetProfit: 0, PresumptiveSection: null,
            Stcg111A: 0, StcgNormal: 0, Ltcg112A: 0, Ltcg112: 0,
            SavingsInterest: 0, FdInterest: 0, DividendIncome: 0, OtherIncome: 0,
            Section80C: 0, Section80Ccd1B: 0, Section80D: 0, Section80E: 0, Section80G: 0, Section80Tta: 0,
            TdsPaid: 50000m, TcsPaid: 0, AdvanceTaxPaid: 0, SelfAssessmentTaxPaid: 0,
            Notes: null
        );

        var result = TaxEngine.ComputeRegime(req, isNewRegime: true);

        Assert.Equal(1000000m, result.TaxableIncome);
        Assert.Equal(50000m, result.BaseTax);
        Assert.Equal(0m, result.Rebate87A); // Income > 7L
        Assert.Equal(2000m, result.Cess);
        Assert.Equal(52000m, result.TotalTaxLiability);
        Assert.Equal(2000m, result.NetPayableOrRefund); // 52000 - 50000 TDS = 2000 payable
    }

    [Fact]
    public void Comparison_HighDeductions_RecommendsOldRegime()
    {
        // Gross 15,00,000. Under Old Regime with ₹1.5L 80C, ₹50K NPS, ₹2L home loan interest
        var req = new CalculateTaxRequest(
            Guid.NewGuid(), "2025-26", "2024-25", "individual", null,
            GrossSalary: 1500000m, HraReceived: 0, RentPaid: 0, IsMetro: false, ProfessionalTax: 2500m,
            AnnualRentReceived: 0, MunicipalTaxesPaid: 0, HousingLoanInterest: 200000m, IsSelfOccupied: true,
            BusinessGrossTurnover: 0, NetProfit: 0, PresumptiveSection: null,
            Stcg111A: 0, StcgNormal: 0, Ltcg112A: 0, Ltcg112: 0,
            SavingsInterest: 0, FdInterest: 0, DividendIncome: 0, OtherIncome: 0,
            Section80C: 150000m, Section80Ccd1B: 50000m, Section80D: 25000m, Section80E: 0, Section80G: 0, Section80Tta: 0,
            TdsPaid: 0, TcsPaid: 0, AdvanceTaxPaid: 0, SelfAssessmentTaxPaid: 0,
            Notes: null
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Old Regime: 15L - 50K (std) - 2.5K (PT) - 2L (HP loss) = 12.475L Gross Income
        // Less 80C (1.5L) + 80CCD (50K) + 80D (25K) = 2.25L deductions
        // Taxable income = 10,22,500
        Assert.Equal("old", comparison.RecommendedRegime);
        Assert.True(comparison.TaxSavings > 0);
        Assert.True(comparison.OldRegime.TotalTaxLiability < comparison.NewRegime.TotalTaxLiability);
    }

    [Fact]
    public void Comparison_StandardDeductionsOnly_RecommendsNewRegime()
    {
        // Gross 12,00,000 with zero Chapter VI-A investments
        var req = new CalculateTaxRequest(
            Guid.NewGuid(), "2025-26", "2024-25", "individual", null,
            GrossSalary: 1200000m, HraReceived: 0, RentPaid: 0, IsMetro: false, ProfessionalTax: 0,
            AnnualRentReceived: 0, MunicipalTaxesPaid: 0, HousingLoanInterest: 0, IsSelfOccupied: false,
            BusinessGrossTurnover: 0, NetProfit: 0, PresumptiveSection: null,
            Stcg111A: 0, StcgNormal: 0, Ltcg112A: 0, Ltcg112: 0,
            SavingsInterest: 0, FdInterest: 0, DividendIncome: 0, OtherIncome: 0,
            Section80C: 0, Section80Ccd1B: 0, Section80D: 0, Section80E: 0, Section80G: 0, Section80Tta: 0,
            TdsPaid: 0, TcsPaid: 0, AdvanceTaxPaid: 0, SelfAssessmentTaxPaid: 0,
            Notes: null
        );

        var comparison = TaxEngine.ComputeComparison(req);

        Assert.Equal("new", comparison.RecommendedRegime);
        Assert.True(comparison.TaxSavings > 20000m);
    }
}
