using CaDesk.Domain.Payroll;

namespace CaDesk.UnitTests;

public class PayrollUnitTests
{
    [Fact]
    public void StatutoryDeductionCalculator_Pf_CeilingApplied_At15000()
    {
        // Basic ₹25,000 exceeds ₹15,000 statutory PF ceiling
        decimal basicSalary = 25000m;

        var (eePf, erEps, erEpf) = StatutoryDeductionCalculator.CalculatePf(basicSalary, isPfEligible: true);

        // 12% of 15,000 = 1,800
        Assert.Equal(1800m, eePf);
        // 8.33% of 15,000 capped at 1,250
        Assert.Equal(1250m, erEps);
        // Remainder 1,800 - 1,250 = 550
        Assert.Equal(550m, erEpf);
    }

    [Fact]
    public void StatutoryDeductionCalculator_Pf_UnderCeiling_CalculatesAccurateSplit()
    {
        decimal basicSalary = 10000m;

        var (eePf, erEps, erEpf) = StatutoryDeductionCalculator.CalculatePf(basicSalary, isPfEligible: true);

        // 12% of 10,000 = 1,200
        Assert.Equal(1200m, eePf);
        // 8.33% of 10,000 = 833
        Assert.Equal(833m, erEps);
        // 1,200 - 833 = 367
        Assert.Equal(367m, erEpf);
    }

    [Fact]
    public void StatutoryDeductionCalculator_Pf_NotEligible_ReturnsZero()
    {
        var (eePf, erEps, erEpf) = StatutoryDeductionCalculator.CalculatePf(20000m, isPfEligible: false);

        Assert.Equal(0m, eePf);
        Assert.Equal(0m, erEps);
        Assert.Equal(0m, erEpf);
    }

    [Fact]
    public void StatutoryDeductionCalculator_Esi_Eligible_GrossUnder21000()
    {
        decimal grossSalary = 18000m;

        var (eeEsi, erEsi) = StatutoryDeductionCalculator.CalculateEsi(grossSalary, isEsiEligible: true);

        // 0.75% of 18,000 = 135
        Assert.Equal(135m, eeEsi);
        // 3.25% of 18,000 = 585
        Assert.Equal(585m, erEsi);
    }

    [Fact]
    public void StatutoryDeductionCalculator_Esi_Exceeds21000Ceiling_ReturnsZero()
    {
        decimal grossSalary = 25000m;

        var (eeEsi, erEsi) = StatutoryDeductionCalculator.CalculateEsi(grossSalary, isEsiEligible: true);

        Assert.Equal(0m, eeEsi);
        Assert.Equal(0m, erEsi);
    }

    [Theory]
    [InlineData(6000, 5, 0)]      // <= 7500
    [InlineData(8500, 5, 175)]    // 7501..10000
    [InlineData(35000, 5, 200)]   // > 10000 in May
    [InlineData(35000, 2, 300)]   // > 10000 in February (₹300 annual balance)
    public void StatutoryDeductionCalculator_Pt_Maharashtra_CalculatesCorrectSlabs(decimal gross, int month, decimal expectedPt)
    {
        var pt = StatutoryDeductionCalculator.CalculatePt(gross, "Maharashtra", month, isPtEligible: true);
        Assert.Equal(expectedPt, pt);
    }

    [Theory]
    [InlineData(12000, 0)]   // <= 15000
    [InlineData(25000, 200)] // > 15000
    public void StatutoryDeductionCalculator_Pt_Karnataka_CalculatesCorrectSlabs(decimal gross, decimal expectedPt)
    {
        var pt = StatutoryDeductionCalculator.CalculatePt(gross, "Karnataka", month: 6, isPtEligible: true);
        Assert.Equal(expectedPt, pt);
    }

    [Fact]
    public void StatutoryDeductionCalculator_SalaryTds_NewRegime_Under87ARebate_ReturnsZero()
    {
        // Monthly gross 50,000 * 12 = 600,000. Less 75,000 std ded = 525,000 taxable.
        // Under Sec 87A new regime (up to 7,00,000 taxable), tax is zero.
        decimal monthlyGross = 50000m;

        var tds = StatutoryDeductionCalculator.CalculateMonthlySalaryTds(monthlyGross, PayrollTaxRegime.NewRegime);

        Assert.Equal(0m, tds);
    }

    [Fact]
    public void StatutoryDeductionCalculator_SalaryTds_NewRegime_HighIncome_CalculatesTax()
    {
        // Monthly gross 1,50,000 * 12 = 18,00,000.
        // Taxable = 17,25,000.
        decimal monthlyGross = 150000m;

        var tds = StatutoryDeductionCalculator.CalculateMonthlySalaryTds(monthlyGross, PayrollTaxRegime.NewRegime);

        Assert.True(tds > 10000m);
    }

    [Fact]
    public void EpfoEcrGenerator_GeneratesStandardDelimitedString()
    {
        var payslips = new List<PayrollPayslip>
        {
            new()
            {
                Uan = "100987654321",
                EmployeeName = "Vikram Aditya",
                GrossSalary = 30000m,
                BasicSalary = 15000m,
                EmployeePf = 1800m,
                EmployerEps = 1250m,
                EmployerEpf = 550m,
                DaysWorked = 30,
                DaysInMonth = 30
            }
        };

        var ecr = EpfoEcrGenerator.GenerateEcrText(payslips);

        Assert.Contains("#~#100987654321#~#Vikram Aditya#~#30000#~#15000#~#15000#~#15000#~#1800#~#1250#~#550#~#0#~#0#~#", ecr);
    }
}
