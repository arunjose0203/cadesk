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
);

public record RegimeComparisonDto(
    RegimeTaxResult OldRegime,
    RegimeTaxResult NewRegime,
    string RecommendedRegime,
    decimal TaxSavings,
    string RecommendationReason
);

public record CalculateTaxRequest(
    Guid ClientId,
    string AssessmentYear,
    string FinancialYear,
    string? TaxpayerCategory,
    string? OptedRegime,
    // Salary
    decimal GrossSalary,
    decimal HraReceived,
    decimal RentPaid,
    bool IsMetro,
    decimal ProfessionalTax,
    // House property
    decimal AnnualRentReceived,
    decimal MunicipalTaxesPaid,
    decimal HousingLoanInterest,
    bool IsSelfOccupied,
    // Business
    decimal BusinessGrossTurnover,
    decimal NetProfit,
    string? PresumptiveSection,
    // Capital gains
    decimal Stcg111A,
    decimal StcgNormal,
    decimal Ltcg112A,
    decimal Ltcg112,
    // Other sources
    decimal SavingsInterest,
    decimal FdInterest,
    decimal DividendIncome,
    decimal OtherIncome,
    // Chapter VI-A Deductions
    decimal Section80C,
    decimal Section80Ccd1B,
    decimal Section80D,
    decimal Section80E,
    decimal Section80G,
    decimal Section80Tta,
    // Taxes paid
    decimal TdsPaid,
    decimal TcsPaid,
    decimal AdvanceTaxPaid,
    decimal SelfAssessmentTaxPaid,
    string? Notes
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
    DateTime? UpdatedAt
);

public record ImportAisToComputationRequest(Guid ClientId, string AisJson);
