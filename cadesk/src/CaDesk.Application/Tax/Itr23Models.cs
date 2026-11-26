namespace CaDesk.Application.Tax;

public record SalaryEmployerInput(
    string EmployerName,
    string? TanOfEmployer = null,
    string NatureOfEmployment = "OTH", // CGOV | SGOV | PSU | PE | OTH
    decimal GrossSalary = 0m,
    decimal TdsDeducted = 0m,
    string? Address = null,
    string? City = null,
    string? StateCode = null,
    string? PinCode = null
);

public record HousePropertyInput(
    string Address = "Residential Property",
    string? City = null,
    string? StateCode = null,
    string? PinCode = null,
    string UsageType = "S", // S (Self-Occupied) | L (Let-Out) | D (Deemed Let-Out)
    decimal AnnualRent = 0m,
    decimal MunicipalTaxes = 0m,
    decimal LoanInterest = 0m,
    bool IsCoOwned = false,
    decimal AssesseeSharePercent = 100m
);

public record ForeignAssetInput(
    string CountryName = "United States",
    string CountryCode = "2", // CBDT country code e.g. 2=USA, 44=UK, 65=SG, 971=AE
    string InstitutionName = "Foreign Bank / Brokerage",
    string Address = "Overseas",
    string ZipCode = "10001",
    string AccountNumber = "ACCT001",
    string OpenDate = "2023-01-15",
    decimal PeakBalanceInr = 0m,
    decimal ClosingBalanceInr = 0m,
    decimal IncomeAccruedInr = 0m
);

public record ScheduleAlInput(
    decimal ImmovablePropertyValue = 0m,
    string ImmovableDescription = "Residential Property",
    decimal BankDeposits = 0m,
    decimal SharesAndSecurities = 0m,
    decimal InsurancePolicies = 0m,
    decimal LoansAndAdvancesGiven = 0m,
    decimal CashInHand = 0m,
    decimal JewelleryAndBullion = 0m,
    decimal VehiclesAndOther = 0m,
    decimal LiabilitiesIncurred = 0m
);

public record CalculatePersonalExtendedTaxRequest(
    Guid ClientId,
    string AssessmentYear = "2026-27",
    string FinancialYear = "2025-26",
    string ReturnFormType = "ITR-2", // ITR-2 | ITR-3
    string TaxpayerCategory = "individual", // individual | senior_citizen | super_senior | huf
    string StatusType = "I", // I (Individual) | H (HUF)
    string OptedRegime = "auto", // auto | new | old
    string? Notes = null,

    // Salary & Multiple Employers
    decimal GrossSalary = 0m,
    decimal HraReceived = 0m,
    decimal RentPaid = 0m,
    bool IsMetro = false,
    decimal ProfessionalTax = 0m,
    List<SalaryEmployerInput>? Employers = null,

    // Multiple House Properties
    decimal AnnualRentReceived = 0m,
    decimal MunicipalTaxesPaid = 0m,
    decimal HousingLoanInterest = 0m,
    bool IsSelfOccupied = true,
    List<HousePropertyInput>? HouseProperties = null,

    // Capital Gains & VDA (Crypto 115BBH)
    decimal Stcg111ASaleValue = 0m,
    decimal Stcg111ACost = 0m,
    decimal Stcg111A = 0m,
    decimal StcgNormal = 0m,
    decimal Ltcg112ASaleValue = 0m,
    decimal Ltcg112ACost = 0m,
    decimal Ltcg112A = 0m,
    decimal Ltcg112SaleValue = 0m,
    decimal Ltcg112Cost = 0m,
    decimal Ltcg112 = 0m,
    decimal VdaConsideration = 0m,
    decimal VdaCostOfAcquisition = 0m,
    decimal VdaIncome115BBH = 0m,

    // Other Sources & Chapter VI-A
    decimal SavingsInterest = 0m,
    decimal FdInterest = 0m,
    decimal DividendIncome = 0m,
    decimal OtherSourcesIncome = 0m,
    decimal Section80C = 0m,
    decimal Section80Ccd1B = 0m,
    decimal Section80D = 0m,
    decimal Section80E = 0m,
    decimal Section80G = 0m,
    decimal Section80Tta = 0m,
    decimal Deduction10AA = 0m,
    decimal Deduction35AD = 0m,

    // ITR-3 Proprietor / Professional Business Books
    string BusinessName = "Proprietary Concern",
    string NatureOfBusinessCode = "16013",
    bool IsTaxAuditApplicable = false,
    decimal PropCapital = 0m,
    decimal ReservesAndSurplus = 0m,
    decimal SecuredLoans = 0m,
    decimal UnsecuredLoans = 0m,
    decimal TradePayables = 0m,
    decimal OtherCurrentLiabilities = 0m,
    decimal FixedAssetsGrossBlock = 0m,
    decimal FixedAssetsAccumulatedDep = 0m,
    decimal FixedAssetsNetBlock = 0m,
    decimal Inventories = 0m,
    decimal TradeReceivables = 0m,
    decimal CashAndBankBalances = 0m,
    decimal LoansAndAdvancesAsset = 0m,
    decimal RevenueFromOperations = 0m,
    decimal OtherBusinessIncome = 0m,
    decimal PurchasesAndDirectCost = 0m,
    decimal ChangesInInventories = 0m,
    decimal EmployeeBenefitExpense = 0m,
    decimal FinanceCost = 0m,
    decimal DepreciationPerBooks = 0m,
    decimal AuditFee = 0m,
    decimal OtherBusinessExpenses = 0m,
    decimal DisallowSec36 = 0m,
    decimal DisallowSec37 = 0m,
    decimal DisallowSec40a = 0m,
    decimal DisallowSec40A3 = 0m,
    decimal DisallowSec43B = 0m,
    decimal DisallowSec43BhMsme = 0m,
    decimal IncomeTaxDepreciationSec32 = 0m,
    decimal BroughtForwardBusinessLoss = 0m,
    List<DepreciationBlockInput>? DepreciationBlocks = null,

    // Disclosures & Prepaid Taxes
    decimal TdsSalary = 0m,
    decimal TdsOtherThanSalary = 0m,
    decimal TcsPaid = 0m,
    decimal AdvanceTaxPaid = 0m,
    decimal SelfAssessmentTaxPaid = 0m,
    decimal Interest234A = 0m,
    decimal Interest234B = 0m,
    decimal Interest234C = 0m,
    decimal LateFee234F = 0m,
    List<ChallanTaxPaymentInput>? AdvanceTaxChallans = null,
    List<TdsCreditLineInput>? TdsCreditEntries = null,
    List<ForeignAssetInput>? ForeignAssets = null,
    ScheduleAlInput? ScheduleAl = null
);

public record PersonalRegimeBreakdownDto(
    string Regime, // new | old
    decimal GrossSalary,
    decimal SalaryDeductions,
    decimal NetSalary,
    decimal HousePropertyIncome,
    decimal BusinessBookProfit,
    decimal BusinessDisallowances,
    decimal DepreciationBooks,
    decimal DepreciationIncomeTax32,
    decimal BusinessIncomeNetPgbp,
    decimal Stcg111A,
    decimal StcgNormal,
    decimal Ltcg112A,
    decimal Ltcg112,
    decimal VdaIncome115BBH,
    decimal TotalCapitalGainsAndVda,
    decimal OtherSourcesIncome,
    decimal GrossTotalIncome,
    decimal ChapterViaDeductions,
    decimal TaxableIncome,
    decimal SlabTaxOnNormalIncome,
    decimal TaxOnStcg111A,
    decimal TaxOnLtcg112A,
    decimal TaxOnLtcg112,
    decimal TaxOnVda115BBH,
    decimal SpecialRateTax,
    decimal Rebate87A,
    decimal Surcharge,
    decimal Cess,
    decimal AmtTotalTax115JC,
    bool IsAmtApplied,
    decimal InterestAndFee,
    decimal TotalTaxLiability,
    decimal PrepaidTaxes,
    decimal NetPayableOrRefund,
    List<TaxSlabBreakdown> SlabBreakdown
);

public record PersonalExtendedTaxResultDto(
    string ReturnFormType, // ITR-2 | ITR-3
    string StatusType, // I | H
    string AssessmentYear,
    string FinancialYear,
    PersonalRegimeBreakdownDto OldRegime,
    PersonalRegimeBreakdownDto NewRegime,
    string SelectedRegime, // new | old
    string RecommendedRegime,
    decimal TaxSavings,
    string RecommendationReason,
    PersonalRegimeBreakdownDto ActiveSummary,
    CalculatePersonalExtendedTaxRequest NormalizedInput
);

public record PersonalPrefillResponseDto(
    CalculatePersonalExtendedTaxRequest PrefilledRequest,
    List<BusinessPrefillSourceNote> Sources
);
