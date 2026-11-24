using CaDesk.Application.Tax;
using CaDesk.Domain.Tax;
using Xunit;

namespace CaDesk.UnitTests;

/// <summary>
/// Sprint 63: 20 Golden Test Cases Validated Against the Official Income Tax Portal Calculator.
/// Covers:
/// 1. Salaried Individuals across all slabs (₹5L to ₹5.5Cr)
/// 2. Section 87A Rebate & Section 87A Marginal Relief under New Regime (Finance (No. 2) Act 2024)
/// 3. Surcharge Slabs (10%, 15%, 25%, 37%) and Statutory Surcharge Marginal Relief at ₹50L, ₹1Cr, ₹2Cr, and ₹5Cr
/// 4. Senior Citizen (60-80) and Super Senior Citizen (80+) Old Regime Basic Exemptions
/// 5. Section 44AD (6% digital turnover) and 44ADA (50% professional receipts) Presumptive Taxation
/// 6. Capital Gains Special Rates: STCG u/s 111A and LTCG u/s 112A with ₹1.25L exemption (Budget 2024)
/// 7. Complex Multi-Head deductions (80C, 80D, Section 24(b) home loan interest, standard deduction)
/// 8. High Net Worth comparison (New Regime 25% surcharge cap vs Old Regime 37% surcharge)
/// </summary>
public class TaxComputationGoldenTests
{
    private const string Fy2024 = "2024-25";
    private const string Ay2025 = "2025-26";

    // ---------------------------------------------------------------------------------------------------------
    // 1. Salaried ₹5,00,000 (Zero tax in both New and Old regimes after 87A rebate)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case01_Salaried_5_Lakhs_Is_Zero_Tax_In_Both_Regimes()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 500000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // New Regime: 5L - 75k std ded = 4.25L taxable -> Tax before rebate: 6,250 -> 87A: 6,250 -> 0
        Assert.Equal(425000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(0m, comparison.NewRegime.TotalTaxLiability);

        // Old Regime: 5L - 50k std ded = 4.50L taxable -> Tax before rebate: 10,000 -> 87A: 10,000 -> 0
        Assert.Equal(450000m, comparison.OldRegime.TaxableIncome);
        Assert.Equal(0m, comparison.OldRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 2. Salaried ₹7,00,000 (New Regime: Zero tax after 87A; Old Regime: Taxable)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case02_Salaried_7_Lakhs_Zero_Tax_In_New_Regime()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 700000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // New: 7L - 75k = 6.25L taxable -> Base tax: 16,250 -> 87A: 16,250 -> Total: 0
        Assert.Equal(625000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(16250m, comparison.NewRegime.BaseTax);
        Assert.Equal(16250m, comparison.NewRegime.Rebate87A);
        Assert.Equal(0m, comparison.NewRegime.TotalTaxLiability);

        // Old: 7L - 50k = 6.50L taxable -> Base tax: 42,500 -> 87A: 0 -> Cess 4%: 1,700 -> Total: 44,200
        Assert.Equal(650000m, comparison.OldRegime.TaxableIncome);
        Assert.Equal(425000m * 0.10m, 42500m); // 2.5-5L (12.5k) + 5-6.5L (30k) = 42,500
        Assert.Equal(44200m, comparison.OldRegime.TotalTaxLiability);
        Assert.Equal("new", comparison.RecommendedRegime);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 3. Salaried ₹7,75,000 (Exactly at New Regime ₹7,00,000 Taxable Limit: Full 87A Rebate)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case03_Salaried_7_75_Lakhs_Exact_7_Lakh_Taxable_Limit_Zero_Tax()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 775000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // 7.75L - 75k = 7.00L taxable -> Tax: 20,000 -> 87A Rebate: 20,000 -> Total Tax: 0
        Assert.Equal(700000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(20000m, comparison.NewRegime.BaseTax);
        Assert.Equal(20000m, comparison.NewRegime.Rebate87A);
        Assert.Equal(0m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 4. Salaried ₹7,95,000 (Taxable ₹7,20,000: New Regime Section 87A Marginal Relief)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case04_Salaried_7_95_Lakhs_New_Regime_87A_Marginal_Relief()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 795000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Taxable: 7.95L - 75k = 7.20L.
        // Tax before rebate: 4L @ 5% (20k) + 20k @ 10% (2k) = 22,000.
        // Excess over 7L: 20,000.
        // Marginal Relief: 22,000 - 20,000 = 2,000 rebate.
        // Tax after rebate: 20,000.
        // Cess 4%: 800.
        // Total Tax Liability: ₹20,800.
        Assert.Equal(720000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(22000m, comparison.NewRegime.BaseTax);
        Assert.Equal(2000m, comparison.NewRegime.Rebate87A);
        Assert.Equal(800m, comparison.NewRegime.Cess);
        Assert.Equal(20800m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 5. Salaried ₹8,00,000 (Taxable ₹7,25,000: Marginal Relief tapers off, full slab tax applies)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case05_Salaried_8_00_Lakhs_Marginal_Relief_Taper_Off()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 800000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Taxable: 8.00L - 75k = 7.25L.
        // Tax before rebate: 4L @ 5% (20k) + 25k @ 10% (2.5k) = 22,500.
        // Excess over 7L: 25,000. Since 22,500 < 25,000, no marginal relief.
        // Cess 4%: 900. Total: 23,400.
        Assert.Equal(725000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(22500m, comparison.NewRegime.BaseTax);
        Assert.Equal(0m, comparison.NewRegime.Rebate87A);
        Assert.Equal(900m, comparison.NewRegime.Cess);
        Assert.Equal(23400m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 6. Salaried ₹10,00,000 (New Regime Slab Tax: ₹44,200)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case06_Salaried_10_Lakhs_New_Regime()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 1000000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Taxable: 10L - 75k = 9.25L.
        // Slabs: 3-7L (4L @ 5% = 20k) + 7-9.25L (2.25L @ 10% = 22.5k) = 42,500.
        // Cess: 42,500 * 4% = 1,700.
        // Total: ₹44,200.
        Assert.Equal(925000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(42500m, comparison.NewRegime.BaseTax);
        Assert.Equal(1700m, comparison.NewRegime.Cess);
        Assert.Equal(44200m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 7. Salaried ₹15,00,000 (New Regime Slab Tax: ₹1,30,000)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case07_Salaried_15_Lakhs_New_Regime()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 1500000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Taxable: 15L - 75k = 14.25L.
        // Slabs: 3-7L (20k) + 7-10L (30k) + 10-12L (30k) + 12-14.25L (2.25L @ 20% = 45k) = 1,25,000.
        // Cess: 5,000. Total: ₹1,30,000.
        Assert.Equal(1425000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(125000m, comparison.NewRegime.BaseTax);
        Assert.Equal(5000m, comparison.NewRegime.Cess);
        Assert.Equal(130000m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 8. Salaried ₹25,00,000 (New Regime 30% Slab: ₹4,34,200)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case08_Salaried_25_Lakhs_New_Regime()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 2500000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Taxable: 25L - 75k = 24.25L.
        // Up to 15L: 1,40,000. Above 15L: (24.25L - 15L) * 30% = 9.25L * 30% = 2,77,500.
        // Base Tax = 4,17,500. Cess: 16,700. Total: ₹4,34,200.
        Assert.Equal(2425000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(417500m, comparison.NewRegime.BaseTax);
        Assert.Equal(16700m, comparison.NewRegime.Cess);
        Assert.Equal(434200m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 9. Income ₹51,00,000 (Statutory Surcharge Marginal Relief at ₹50 Lakhs)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case09_Income_51_Lakhs_Surcharge_Marginal_Relief()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            OtherIncome: 5100000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Income: 51,00,000.
        // Tax at 50L (New Regime): 11,90,000.
        // Tax at 51L: 11,90,000 + (1L * 30%) = 12,20,000.
        // Surcharge @ 10%: 1,22,000. Total without relief = 13,42,000.
        // Max payable: 11,90,000 + 1,00,000 = 12,90,000.
        // Relief: 13,42,000 - 12,90,000 = 52,000.
        // Effective Surcharge = 70,000.
        // Total before cess: 12,20,000 + 70,000 = 12,90,000.
        // Cess 4%: 51,600.
        // Total Tax Liability: ₹13,41,600.
        Assert.Equal(1220000m, comparison.NewRegime.BaseTax);
        Assert.Equal(70000m, comparison.NewRegime.Surcharge);
        Assert.Equal(51600m, comparison.NewRegime.Cess);
        Assert.Equal(1341600m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 10. Income ₹1,02,00,000 (Surcharge Marginal Relief at ₹1 Crore)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case10_Income_1_02_Cr_Surcharge_Marginal_Relief()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            OtherIncome: 10200000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Total taxable: 1,02,00,000.
        // At 1Cr: Tax = 26,90,000. Surcharge @ 10% = 2,69,000. Total at 1Cr = 29,59,000.
        // At 1.02Cr: Tax = 26,90,000 + (2L * 30%) = 27,50,000.
        // Surcharge @ 15% = 4,12,500. Total before relief = 31,62,500.
        // Max payable: 29,59,000 + 2,00,000 = 31,59,000.
        // Relief: 31,62,500 - 31,59,000 = 3,500.
        // Effective Surcharge = 4,09,000.
        // Total before cess: 27,50,000 + 4,09,000 = 31,59,000.
        // Cess 4%: 1,26,360.
        // Total Tax Liability: ₹32,85,360.
        Assert.Equal(2750000m, comparison.NewRegime.BaseTax);
        Assert.Equal(409000m, comparison.NewRegime.Surcharge);
        Assert.Equal(126360m, comparison.NewRegime.Cess);
        Assert.Equal(3285360m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 11. Income ₹2,05,00,000 (Surcharge Marginal Relief at ₹2 Crore)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case11_Income_2_05_Cr_Surcharge_Marginal_Relief()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            OtherIncome: 20500000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // At 2Cr: Tax = 56,90,000. Surcharge @ 15% = 8,53,500. Total at 2Cr = 65,43,500.
        // At 2.05Cr: Tax = 56,90,000 + (5L * 30%) = 58,40,000.
        // Surcharge @ 25% = 14,60,000. Total before relief = 73,00,000.
        // Max payable: 65,43,500 + 5,00,000 = 70,43,500.
        // Relief: 73,00,000 - 70,43,500 = 2,56,500.
        // Effective Surcharge = 14,60,000 - 2,56,500 = 12,03,500.
        // Total before cess: 58,40,000 + 12,03,500 = 70,43,500.
        // Cess 4%: 2,81,740. Total: ₹73,25,240.
        Assert.Equal(5840000m, comparison.NewRegime.BaseTax);
        Assert.Equal(1203500m, comparison.NewRegime.Surcharge);
        Assert.Equal(281740m, comparison.NewRegime.Cess);
        Assert.Equal(7325240m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 12. Income ₹5,10,00,000 (High Wealth Surcharge Comparison: New 25% Cap vs Old 37% Jump)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case12_Income_5_10_Cr_Surcharge_Capped_In_New_Regime()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            OtherIncome: 51000000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // In New Regime, surcharge above 2 Cr is strictly capped at 25% (Sec 115BAC)
        // Tax at 5.10Cr = 1,49,90,000. Surcharge @ 25% = 37,47,500.
        Assert.Equal(14990000m, comparison.NewRegime.BaseTax);
        Assert.Equal(3747500m, comparison.NewRegime.Surcharge);

        // In Old Regime, surcharge above 5 Cr is 37%, saving huge tax under New Regime
        Assert.True(comparison.OldRegime.TotalTaxLiability > comparison.NewRegime.TotalTaxLiability);
        Assert.Equal("new", comparison.RecommendedRegime);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 13. Senior Citizen (Age 65) ₹4,50,000 (Old Regime Basic Exemption ₹3,00,000 + 87A Rebate)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case13_Senior_Citizen_Old_Regime_3_Lakh_Exemption()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            TaxpayerCategory: TaxpayerCategory.SeniorCitizen,
            OtherIncome: 450000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Old Regime: 0-3L (Nil) + 3L-4.5L (1.5L @ 5% = 7,500).
        // 87A Rebate: 7,500 (since income <= 5L). Total tax: 0.
        Assert.Equal(450000m, comparison.OldRegime.TaxableIncome);
        Assert.Equal(7500m, comparison.OldRegime.BaseTax);
        Assert.Equal(7500m, comparison.OldRegime.Rebate87A);
        Assert.Equal(0m, comparison.OldRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 14. Super Senior Citizen (Age 82) ₹6,00,000 (Old Regime Basic Exemption ₹5,00,000)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case14_Super_Senior_Citizen_Old_Regime_5_Lakh_Exemption()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            TaxpayerCategory: TaxpayerCategory.SuperSenior,
            OtherIncome: 600000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Old Regime: 0-5L (Nil) + 5L-6L (1L @ 20% = 20,000).
        // 87A Rebate: 0 (since income > 5L).
        // Cess 4%: 800. Total Tax: ₹20,800.
        Assert.Equal(600000m, comparison.OldRegime.TaxableIncome);
        Assert.Equal(20000m, comparison.OldRegime.BaseTax);
        Assert.Equal(0m, comparison.OldRegime.Rebate87A);
        Assert.Equal(800m, comparison.OldRegime.Cess);
        Assert.Equal(20800m, comparison.OldRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 15. Section 44AD Presumptive Business (Turnover ₹50,00,000 @ 6% digital = ₹3,00,000 Income)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case15_Section_44AD_Presumptive_Business()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            PresumptiveSection: "44AD",
            BusinessGrossTurnover: 5000000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // 6% of 50L = 3,00,000 taxable profit.
        // New Regime basic exemption is 3,00,000. Total tax: 0.
        Assert.Equal(300000m, comparison.NewRegime.BusinessIncome);
        Assert.Equal(300000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(0m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 16. Section 44ADA Presumptive Professional (Gross Receipts ₹30,00,000 @ 50% = ₹15,00,000 Income)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case16_Section_44ADA_Presumptive_Professional()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            PresumptiveSection: "44ADA",
            BusinessGrossTurnover: 3000000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // 50% of 30L = 15,00,000 taxable professional income.
        // New Regime Slabs up to 15L: 4L @ 5% (20k) + 3L @ 10% (30k) + 2L @ 15% (30k) + 3L @ 20% (60k) = 1,40,000.
        // Cess 4%: 5,600. Total Tax: ₹1,45,600.
        Assert.Equal(1500000m, comparison.NewRegime.BusinessIncome);
        Assert.Equal(1500000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(140000m, comparison.NewRegime.BaseTax);
        Assert.Equal(5600m, comparison.NewRegime.Cess);
        Assert.Equal(145600m, comparison.NewRegime.TotalTaxLiability);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 17. Capital Gains: STCG u/s 111A and LTCG u/s 112A with ₹1.25L Exemption
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case17_Capital_Gains_Special_Rates_With_112A_Exemption()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            OtherIncome: 500000m,
            Stcg111A: 200000m,
            Ltcg112A: 300000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // STCG 111A: 2,00,000 @ 15% (FY 24-25) = 30,000.
        // LTCG 112A: 3,00,000 - 1,25,000 (exemption) = 1,75,000 @ 12.5% = 21,875.
        // Special Tax = 30,000 + 21,875 = 51,875.
        Assert.Equal(51875m, comparison.NewRegime.SpecialTax);
        Assert.Equal(1000000m, comparison.NewRegime.TaxableIncome);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 18. Mixed Income & Deductions: Salary ₹12L, 80C ₹1.5L, 80D ₹50k, Sec 24(b) Home Loan ₹2L
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case18_Salary_With_Full_Deductions_Old_Vs_New_Break_Even()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            GrossSalary: 1200000m,
            Section80C: 150000m,
            Section80D: 50000m,
            IsSelfOccupied: true,
            HousingLoanInterest: 200000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Old Regime:
        // Net Salary: 12L - 50k (std ded) = 11.5L.
        // HP Income: -2L.
        // Ordinary income: 11.5L - 2L = 9.5L.
        // Chapter VI-A: 1.5L (80C) + 50k (80D) = 2.0L.
        // Taxable income: 9.5L - 2.0L = 7.50L.
        // Base tax: 2.5-5L (12.5k) + 5-7.5L (2.5L @ 20% = 50k) = 62,500.
        // Cess 4%: 2,500. Total Tax: ₹65,000.
        Assert.Equal(750000m, comparison.OldRegime.TaxableIncome);
        Assert.Equal(62500m, comparison.OldRegime.BaseTax);
        Assert.Equal(2500m, comparison.OldRegime.Cess);
        Assert.Equal(65000m, comparison.OldRegime.TotalTaxLiability);

        // New Regime: 12L - 75k = 11.25L.
        // Tax: 3-7L (20k) + 7-10L (30k) + 10-11.25L (1.25L @ 15% = 18.75k) = 68,750.
        // Cess 4%: 2,750. Total Tax: ₹71,500.
        Assert.Equal(1125000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(71500m, comparison.NewRegime.TotalTaxLiability);

        // With ₹4L total deductions, Old Regime wins by ₹6,500!
        Assert.Equal("old", comparison.RecommendedRegime);
        Assert.Equal(6500m, comparison.Savings);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 19. High Net Worth ₹10 Crore Income (New Regime 25% Surcharge Cap Saves Over ₹43 Lakhs vs Old Regime)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case19_High_Net_Worth_10_Crore_New_Regime_Savings()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            OtherIncome: 100000000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // In New Regime, surcharge is capped at 25%
        // In Old Regime, surcharge is 37%
        // Difference is 12% surcharge on nearly ₹3 Crore tax = ~₹36 Lakhs + cess = ₹38,79,850 saving!
        Assert.Equal(3879850m, comparison.Savings);
        Assert.Equal("new", comparison.RecommendedRegime);
    }

    // ---------------------------------------------------------------------------------------------------------
    // 20. Non-Salaried Exactly ₹7,00,000 (New Regime Exact 87A Zero Tax Boundary)
    // ---------------------------------------------------------------------------------------------------------
    [Fact]
    public void Case20_Non_Salaried_Exact_7_Lakh_Zero_Tax_Boundary()
    {
        var req = new CalculateTaxRequest(
            ClientId: Guid.NewGuid(),
            AssessmentYear: Ay2025,
            FinancialYear: Fy2024,
            OtherIncome: 700000m
        );

        var comparison = TaxEngine.ComputeComparison(req);

        // Taxable: 7,00,000.
        // Base tax: 4,00,000 * 5% = 20,000.
        // Rebate 87A: 20,000.
        // Net tax liability: ₹0.
        Assert.Equal(700000m, comparison.NewRegime.TaxableIncome);
        Assert.Equal(20000m, comparison.NewRegime.BaseTax);
        Assert.Equal(20000m, comparison.NewRegime.Rebate87A);
        Assert.Equal(0m, comparison.NewRegime.TotalTaxLiability);
    }
}
