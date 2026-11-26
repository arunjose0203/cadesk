namespace CaDesk.Application.Tax;

public record CalculateTrustTaxRequest(
    Guid ClientId,
    string AssessmentYear = "2026-27",
    string FinancialYear = "2025-26",
    string ReturnFurnishedSection = "139-4A", // 139-4A | 139-4B | 139-4C | 139-4D
    string SectionExemptionClaimed = "11", // 11 | 23CIV | 23CV | 23CVI | 23CIIIAD | 13A
    string StatusOrCompanyType = "5", // 5=Trust, 6=Sec 8 Company, 7=Institution, 4=Political
    string? ProjectOrInstitutionName = null,
    string? Registration12AbNo = "AAABT1234AE20214",
    string? Registration12AbDate = "2021-06-15",
    string? IncorporationDate = "2018-04-01",
    string? Notes = null,

    // Voluntary Contributions & Receipts
    decimal CorpusDonations80G = 0m,
    decimal CorpusDonationsOther = 0m,
    decimal GrantsReceivedGovt = 0m,
    decimal GrantsReceivedCorporate = 0m,
    decimal OtherSpecificAndGeneralDonations = 0m,
    decimal ForeignContributions = 0m,
    decimal AnonymousDonationsReceived = 0m,

    // Other Income derived from Trust Property / Activities (Schedule AI)
    decimal ReceiptsFromMainObjects = 0m,
    decimal ReceiptsIncidental = 0m,
    decimal RentIncome = 0m,
    decimal DividendIncome = 0m,
    decimal InterestIncome = 0m,

    // Charitable Application & Accumulation (Schedule A + Form 10)
    decimal RevenueApplicationCharitable = 0m,
    decimal CapitalApplicationCharitable = 0m,
    string ActivityCategory = "Educational", // Educational | MedicalRelief | ReliefOfPoor | Religious | GeneralPublicUtility
    decimal DisallowedApplication40aOr40A3 = 0m,
    decimal Form10AccumulationUs11_2 = 0m,
    bool Form10Furnished = false,
    string? Form10FilingDate = null,
    decimal SpecifiedViolationIncome115BBI = 0m,

    // Balance Sheet (PARTA_BS)
    decimal CorpusFundClosing = 0m,
    decimal AccumulatedFundClosing = 0m,
    decimal SecuredLoans = 0m,
    decimal UnsecuredLoans = 0m,
    decimal FixedAssetsGrossBlock = 0m,
    decimal FixedAssetsDepreciation = 0m,
    decimal FixedAssetsNetBlock = 0m,
    decimal InvestmentsSec11_5 = 0m,
    decimal SundryDebtors = 0m,
    decimal CashAndBankBalances = 0m,
    decimal LoansAndAdvances = 0m,
    decimal SundryCreditors = 0m,
    decimal Provisions = 0m,

    // Prepaid Taxes & Interest
    decimal TdsCredit = 0m,
    decimal TcsCredit = 0m,
    decimal AdvanceTaxPaid = 0m,
    decimal SelfAssessmentTaxPaid = 0m,
    decimal Interest234A = 0m,
    decimal Interest234B = 0m,
    decimal Interest234C = 0m,
    decimal LateFee234F = 0m,
    List<ChallanTaxPaymentInput>? AdvanceTaxChallans = null,
    List<TdsCreditLineInput>? TdsCreditEntries = null
);

public record TrustTaxResultDto(
    string AssessmentYear,
    string FinancialYear,
    string ReturnFurnishedSection,
    string SectionExemptionClaimed,
    decimal GrossVoluntaryNonCorpus,
    decimal CorpusExempt11_1d,
    decimal OtherObjectAndInvestmentReceipts,
    decimal TotalEligibleIncome,
    decimal StatutoryAccumulation15Pct,
    decimal Required85PctApplication,
    decimal TotalApplicationClaimed,
    decimal DisallowedApplication,
    decimal NetAllowedApplication,
    decimal Form10Accumulation,
    decimal AnonymousDonationsTotal,
    decimal AnonymousDonationsTaxable115BBC,
    decimal ShortfallTaxableNormalIncome,
    decimal SpecifiedIncome115BBI,
    decimal TotalTaxableIncome,
    decimal TaxOnNormalShortfall,
    decimal TaxOnAnonymous115BBC,
    decimal TaxOn115BBI,
    decimal Surcharge,
    decimal Cess,
    decimal InterestAndFee,
    decimal TotalTaxLiability,
    decimal PrepaidTaxes,
    decimal NetPayableOrRefund,
    string SummaryNote,
    CalculateTrustTaxRequest NormalizedInput
);
