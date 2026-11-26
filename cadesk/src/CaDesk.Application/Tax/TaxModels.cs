namespace CaDesk.Application.Tax;

public record TaxSlabBreakdown(
    string SlabRange,
    decimal RatePercent,
    decimal TaxableInSlab,
    decimal TaxAmount
);

public record RegimeTaxResult(
    string Regime,
    decimal GrossSalary,
    decimal StandardDeduction,
    decimal NetSalary,
    decimal HousePropertyIncome,
    decimal BusinessIncome,
    decimal CapitalGainsStcg,
    decimal CapitalGainsLtcg,
    decimal OtherSourcesIncome,
    decimal GrossTotalIncome,
    decimal ChapterViaDeductions,
    decimal TaxableIncome,
    decimal BaseTax,
    decimal SpecialRateTax,
    decimal Rebate87A,
    decimal Surcharge,
    decimal Cess,
    decimal TotalTaxLiability,
    decimal PrepaidTaxes,
    decimal NetPayableOrRefund,
    List<TaxSlabBreakdown> SlabBreakdown
)
{
    public decimal SpecialTax => SpecialRateTax;
}

public record RegimeComparisonDto(
    RegimeTaxResult OldRegime,
    RegimeTaxResult NewRegime,
    string RecommendedRegime,
    decimal TaxSavings,
    string RecommendationReason
)
{
    public decimal Savings => TaxSavings;
}

public record CalculateTaxRequest(
    Guid ClientId,
    string AssessmentYear,
    string FinancialYear,
    string? TaxpayerCategory = "individual",
    string? OptedRegime = null,
    // Salary
    decimal GrossSalary = 0,
    decimal HraReceived = 0,
    decimal RentPaid = 0,
    bool IsMetro = false,
    decimal ProfessionalTax = 0,
    // House property
    decimal AnnualRentReceived = 0,
    decimal MunicipalTaxesPaid = 0,
    decimal HousingLoanInterest = 0,
    bool IsSelfOccupied = false,
    // Business
    decimal BusinessGrossTurnover = 0,
    decimal NetProfit = 0,
    string? PresumptiveSection = null,
    // Capital gains
    decimal Stcg111A = 0,
    decimal StcgNormal = 0,
    decimal Ltcg112A = 0,
    decimal Ltcg112 = 0,
    // Other sources
    decimal SavingsInterest = 0,
    decimal FdInterest = 0,
    decimal DividendIncome = 0,
    decimal OtherIncome = 0,
    // Chapter VI-A Deductions
    decimal Section80C = 0,
    decimal Section80Ccd1B = 0,
    decimal Section80D = 0,
    decimal Section80E = 0,
    decimal Section80G = 0,
    decimal Section80Tta = 0,
    // Taxes paid
    decimal TdsPaid = 0,
    decimal TcsPaid = 0,
    decimal AdvanceTaxPaid = 0,
    decimal SelfAssessmentTaxPaid = 0,
    string? Notes = null
);

public record TaxComputationDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string AssessmentYear,
    string FinancialYear,
    string TaxpayerCategory,
    string OptedRegime,
    string Status,
    decimal GrossSalary,
    decimal SalaryDeductions,
    decimal NetSalary,
    decimal HousePropertyIncome,
    decimal BusinessIncome,
    decimal CapitalGainsStcg,
    decimal CapitalGainsLtcg,
    decimal OtherSourcesIncome,
    decimal GrossTotalIncome,
    decimal ChapterViaDeductions,
    decimal TotalTaxableIncome,
    decimal TaxOnIncome,
    decimal Rebate87A,
    decimal Surcharge,
    decimal Cess,
    decimal TotalTaxLiability,
    decimal TdsTcsCredit,
    decimal AdvanceTaxPaid,
    decimal SelfAssessmentTax,
    decimal NetTaxPayableOrRefund,
    RegimeComparisonDto Comparison,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    BusinessTaxComputationResultDto? BusinessComputation = null,
    PersonalExtendedTaxResultDto? ExtendedPersonalComputation = null
);

public record ImportAisToComputationRequest(Guid ClientId, string AisJson);
