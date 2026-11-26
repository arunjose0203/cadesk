using System.Text.Json.Serialization;

namespace CaDesk.Application.Tax;

public record PartnerShareInput(
    string Name,
    string Pan,
    string Status = "IND_WORKING", // IND_WORKING | INDIVIDUAL | IND_RETIRED | LLP | FIRM | DOMESTIC_COMPANY
    decimal SharePercent = 50m,
    decimal CapitalBalance = 0m,
    decimal InterestRatePercent = 12m,
    decimal InterestPaid = 0m,
    decimal RemunerationPaid = 0m,
    string? Address = null,
    string? City = null,
    string? StateCode = null,
    string? PinCode = null
);

public record KeyPersonInput(
    string Name,
    string Designation = "DIR", // MD | DIR | CEO | CFO | SEC | MGR | OPO
    string? Pan = null,
    string? Din = null,
    string? Address = null,
    string? City = null,
    string? StateCode = null,
    string? PinCode = null
);

public record ShareholderInput(
    string Name,
    string Pan,
    string ShareType = "Equity", // Equity | Preference
    long NumberOfShares = 1000,
    decimal FaceValuePerShare = 10m,
    decimal IssuePricePerShare = 10m,
    string ResidentialStatus = "RES",
    string? AllotmentDate = null
);

public record DepreciationBlockInput(
    string BlockKey, // PM15 | PM30 | PM40 | PM45 | Bldg5 | Bldg10 | Bldg40 | Furn10 | Intang25 | Land0
    string Label,
    decimal RatePercent,
    decimal OpeningWdv,
    decimal AdditionsMoreThan180Days = 0m,
    decimal AdditionsLessThan180Days = 0m,
    decimal Deletions = 0m,
    decimal AdditionalDepreciation = 0m,
    decimal? ExplicitTaxDepreciation = null
);

public record ChallanTaxPaymentInput(
    string BsrCode,
    string DepositDate, // YYYY-MM-DD
    string ChallanSerialNo,
    decimal Amount
);

public record TdsCreditLineInput(
    string TanOfDeductor,
    string DeductorName,
    string TdsSection = "94J-B", // CBDT ScheduleTDS2 enum code
    decimal GrossAmount = 0m,
    decimal TdsClaimed = 0m
);

public record CalculateBusinessTaxRequest(
    Guid ClientId,
    string AssessmentYear = "2026-27",
    string FinancialYear = "2025-26",
    string EntityType = "company", // "firm" (ITR-5) | "company" (ITR-6)
    string SubType = "Private", // Firm: Partnership | LLP | AOP ; Company: Private | Public
    string CorporateRegime = "auto", // auto | normal | 115BAA | 115BAB
    bool IsSmallTurnover25Percent = true, // Domestic company turnover <= 400 Cr in FY 2023-24 -> 25%
    string? IncorporationDate = null,
    bool IsTaxAuditApplicable = false,
    bool IsIndAsApplicable = false,
    string NatureOfBusinessCode = "16013",
    string? Notes = null,

    // Balance Sheet (Books)
    decimal PartnerOrShareCapital = 0m,
    decimal AuthorisedShareCapital = 100_000m,
    decimal ReservesAndSurplus = 0m,
    decimal LongTermBorrowings = 0m,
    decimal ShortTermBorrowings = 0m,
    decimal DeferredTaxLiability = 0m,
    decimal TradePayables = 0m,
    decimal OtherCurrentLiabilities = 0m,
    decimal FixedAssetsGrossBlock = 0m,
    decimal FixedAssetsAccumulatedDep = 0m,
    decimal FixedAssetsNetBlock = 0m,
    decimal CapitalWip = 0m,
    decimal NonCurrentInvestments = 0m,
    decimal CurrentInvestments = 0m,
    decimal Inventories = 0m,
    decimal TradeReceivables = 0m,
    decimal CashAndBankBalances = 0m,
    decimal LoansAndAdvances = 0m,
    decimal OtherCurrentAssets = 0m,

    // Profit & Loss (Books)
    decimal RevenueFromOperations = 0m,
    decimal OtherIncome = 0m,
    decimal InterestIncome = 0m,
    decimal DividendIncome = 0m,
    decimal RentIncome = 0m,
    decimal PurchasesAndDirectCost = 0m,
    decimal ChangesInInventories = 0m,
    decimal EmployeeBenefitExpense = 0m,
    decimal PartnerRemunerationDebited = 0m,
    decimal FinanceCost = 0m,
    decimal PartnerInterestDebited = 0m,
    decimal DepreciationPerBooks = 0m,
    decimal AuditFee = 0m,
    decimal OtherExpenses = 0m,
    decimal TaxProvisionCurrent = 0m,
    decimal TaxProvisionDeferred = 0m,
    decimal BalanceBroughtForwardPl = 0m,

    // Schedule BP Adjustments (Tax computation)
    decimal DisallowSec36 = 0m,
    decimal DisallowSec37 = 0m,
    decimal DisallowSec40a = 0m,
    decimal DisallowSec40A3 = 0m,
    decimal DisallowSec43B = 0m,
    decimal DisallowSec43BhMsme = 0m,
    decimal OtherDisallowances = 0m,
    decimal AllowableDeductionsUs32Etc = 0m,
    decimal IncomeTaxDepreciationSec32 = 0m,
    decimal DeductionSec10AA = 0m,
    decimal DeductionSec35AD = 0m,

    // Other Heads & Chapter VI-A & Losses
    decimal HousePropertyIncome = 0m,
    decimal Stcg111A = 0m,
    decimal StcgNormal = 0m,
    decimal Ltcg112A = 0m,
    decimal Ltcg112 = 0m,
    decimal OtherSourcesIncome = 0m,
    decimal Deduction80G = 0m,
    decimal Deduction80Jjaa = 0m,
    decimal Deduction80M = 0m,
    decimal OtherChapViaDeductions = 0m,
    decimal BroughtForwardBusinessLoss = 0m,
    decimal UnabsorbedDepreciation = 0m,

    // MAT / AMT
    decimal MatCreditBroughtForward = 0m,
    decimal AmtCreditBroughtForward = 0m,
    decimal MatAdditionsOther = 0m,
    decimal MatDeductionsOther = 0m,

    // Taxes Paid & Interest
    decimal AdvanceTaxPaid = 0m,
    decimal TdsCredit = 0m,
    decimal TcsCredit = 0m,
    decimal SelfAssessmentTaxPaid = 0m,
    decimal Interest234A = 0m,
    decimal Interest234B = 0m,
    decimal Interest234C = 0m,
    decimal LateFee234F = 0m,

    // Detailed schedules
    List<PartnerShareInput>? Partners = null,
    List<KeyPersonInput>? KeyPersons = null,
    List<ShareholderInput>? Shareholders = null,
    List<DepreciationBlockInput>? DepreciationBlocks = null,
    List<ChallanTaxPaymentInput>? AdvanceTaxChallans = null,
    List<TdsCreditLineInput>? TdsCreditEntries = null
);

public record BusinessRegimeSummaryDto(
    string RegimeCode, // FIRM_NORMAL | CORP_NORMAL | CORP_115BAA | CORP_115BAB
    string RegimeLabel,
    decimal TaxableIncome,
    decimal TaxAtNormalRates,
    decimal TaxAtSpecialRates,
    decimal BaseTax,
    decimal SurchargeBeforeMarginalRelief,
    decimal MarginalRelief,
    decimal Surcharge,
    decimal HealthAndEducationCess,
    decimal GrossTaxBeforeMatAmt,
    decimal MatOrAmtBaseTax,
    decimal MatOrAmtSurcharge,
    decimal MatOrAmtCess,
    decimal MatOrAmtTotalTax,
    bool IsMatOrAmtApplied,
    decimal MatOrAmtCreditUtilized,
    decimal MatOrAmtCreditGenerated,
    decimal InterestAndFee,
    decimal TotalTaxLiability,
    decimal PrepaidTaxes,
    decimal NetTaxPayableOrRefund,
    bool IsEligible,
    string IneligibilityOrNotes
);

public record BusinessTaxComputationResultDto(
    string EntityType, // firm | company
    string SubType,
    string AssessmentYear,
    string FinancialYear,
    decimal TotalRevenue,
    decimal TotalExpensesBeforeTax,
    decimal BookProfitBeforeTax,
    decimal BookProfitAfterTax,
    decimal TotalDisallowancesAddedBack,
    decimal FirmBookProfitUs40b,
    decimal MaxAllowableRemuneration40bv,
    decimal AllowableRemuneration40b,
    decimal DisallowedRemuneration40b,
    decimal DisallowedPartnerInterest40b,
    decimal DepreciationPerBooks,
    decimal IncomeTaxDepreciationSec32,
    decimal OtherHeadIncomesExcludedFromPl,
    decimal ComputedBusinessIncomeBeforeLoss,
    decimal BroughtForwardLossSetOff,
    decimal NetPgbpIncome,
    decimal HousePropertyIncome,
    decimal CapitalGainsTotal,
    decimal OtherSourcesIncome,
    decimal GrossTotalIncome,
    decimal ChapterViaAllowed,
    decimal TotalTaxableIncome,
    decimal MatBookProfit115JB,
    decimal AmtAdjustedTotalIncome115JC,
    string SelectedRegimeCode,
    string RecommendedRegimeCode,
    decimal SavingsVsAlternative,
    BusinessRegimeSummaryDto ActiveSummary,
    List<BusinessRegimeSummaryDto> Alternatives,
    CalculateBusinessTaxRequest NormalizedInput
);

public record BusinessPrefillRequest(
    Guid ClientId,
    string? FinancialYear = null,
    string? AssessmentYear = null,
    string? EntityType = null
);

public record BusinessPrefillSourceNote(
    string Module,
    bool Found,
    string Summary
);

public record BusinessPrefillResponseDto(
    CalculateBusinessTaxRequest PrefilledRequest,
    List<BusinessPrefillSourceNote> Sources
);
