using System;
using CaDesk.Domain.ForeignRemittance;
using Xunit;

namespace CaDesk.UnitTests;

public class FemaFirmsFlaUnitTests
{
    [Fact]
    public void FemaFirmsEngine_FcGpr_30DayStatutoryDeadline_CalculatesAccurately()
    {
        var allotmentDate = new DateTime(2025, 4, 1);
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcGpr,
            DateOfAllotmentOrTransfer = allotmentDate,
            NumberOfInstruments = 10000,
            IssueOrTransferPricePerInstrument = 150m,
            FairValuePerInstrument = 120m
        };

        var asOf = new DateTime(2025, 4, 15);
        FemaFirmsEngine.CalculateFirmsCompliance(filing, asOf);

        Assert.Equal(new DateTime(2025, 5, 1), filing.StatutoryFilingDeadline);
        Assert.Equal(14, filing.DaysElapsed);
        Assert.Equal(16, filing.DaysRemaining);
        Assert.False(filing.IsLateSubmission);
        Assert.Equal(0m, filing.LateSubmissionFeeEstimated);
        Assert.True(filing.PricingGuidelinesMet);
    }

    [Fact]
    public void FemaFirmsEngine_FcTrs_60DayStatutoryDeadline_CalculatesAccurately()
    {
        var transferDate = new DateTime(2025, 3, 1);
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcTrs,
            DateOfAllotmentOrTransfer = transferDate,
            NumberOfInstruments = 5000,
            IssueOrTransferPricePerInstrument = 200m,
            FairValuePerInstrument = 180m,
            TransferDirection = TransferDirectionType.ResidentToNonResident
        };

        var asOf = new DateTime(2025, 3, 21);
        FemaFirmsEngine.CalculateFirmsCompliance(filing, asOf);

        Assert.Equal(transferDate.AddDays(60), filing.StatutoryFilingDeadline);
        Assert.Equal(20, filing.DaysElapsed);
        Assert.Equal(40, filing.DaysRemaining);
        Assert.False(filing.IsLateSubmission);
        Assert.True(filing.PricingGuidelinesMet);
    }

    [Fact]
    public void FemaFirmsEngine_FcGpr_PricingGuidelines_IssuePriceAboveFairValue_Passes()
    {
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcGpr,
            DateOfAllotmentOrTransfer = DateTime.UtcNow,
            IssueOrTransferPricePerInstrument = 250m,
            FairValuePerInstrument = 200m
        };

        FemaFirmsEngine.CalculateFirmsCompliance(filing, DateTime.UtcNow);

        Assert.True(filing.PricingGuidelinesMet);
        Assert.Contains("meets or exceeds", filing.PricingComplianceRemarks);
    }

    [Fact]
    public void FemaFirmsEngine_FcGpr_PricingGuidelines_IssuePriceBelowFairValue_FlagsViolation()
    {
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcGpr,
            DateOfAllotmentOrTransfer = DateTime.UtcNow,
            IssueOrTransferPricePerInstrument = 180m,
            FairValuePerInstrument = 220m // Violation: issue price < fair value floor
        };

        FemaFirmsEngine.CalculateFirmsCompliance(filing, DateTime.UtcNow);

        Assert.False(filing.PricingGuidelinesMet);
        Assert.Contains("VIOLATION", filing.PricingComplianceRemarks);
    }

    [Fact]
    public void FemaFirmsEngine_FcTrs_ResidentToNonResident_TransferPriceBelowFairValue_FlagsViolation()
    {
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcTrs,
            TransferDirection = TransferDirectionType.ResidentToNonResident,
            DateOfAllotmentOrTransfer = DateTime.UtcNow,
            IssueOrTransferPricePerInstrument = 90m,
            FairValuePerInstrument = 100m // Resident cannot sell to non-resident at less than fair value
        };

        FemaFirmsEngine.CalculateFirmsCompliance(filing, DateTime.UtcNow);

        Assert.False(filing.PricingGuidelinesMet);
        Assert.Contains("VIOLATION", filing.PricingComplianceRemarks);
    }

    [Fact]
    public void FemaFirmsEngine_FcTrs_NonResidentToResident_TransferPriceAboveFairValue_FlagsViolation()
    {
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcTrs,
            TransferDirection = TransferDirectionType.NonResidentToResident,
            DateOfAllotmentOrTransfer = DateTime.UtcNow,
            IssueOrTransferPricePerInstrument = 150m,
            FairValuePerInstrument = 120m // Non-resident cannot sell to resident at more than fair value
        };

        FemaFirmsEngine.CalculateFirmsCompliance(filing, DateTime.UtcNow);

        Assert.False(filing.PricingGuidelinesMet);
        Assert.Contains("VIOLATION", filing.PricingComplianceRemarks);
    }

    [Fact]
    public void FemaFirmsEngine_DelayedFiling_CalculatesLateSubmissionFee()
    {
        var allotmentDate = new DateTime(2024, 1, 1);
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcGpr,
            DateOfAllotmentOrTransfer = allotmentDate,
            NumberOfInstruments = 100000,
            IssueOrTransferPricePerInstrument = 100m, // 1 Crore consideration
            FairValuePerInstrument = 100m
        };

        var asOf = new DateTime(2024, 3, 1); // 60 days after allotment -> 30 days delayed
        FemaFirmsEngine.CalculateFirmsCompliance(filing, asOf);

        Assert.True(filing.IsLateSubmission);
        Assert.True(filing.LateSubmissionFeeEstimated > 7500m);
    }

    [Fact]
    public void FemaFirmsEngine_GenerateComplianceCertificateHtml_ContainsStatutoryCertifications()
    {
        var filing = new FemaFirmsFiling
        {
            FilingType = FirmsFilingType.FcGpr,
            ReferenceNumber = "FIRMS/FC-GPR/2025/001",
            IndianEntityName = "Zenith Robotics India Pvt Ltd",
            IndianEntityCin = "U72900MH2021PTC123456",
            Pan = "AABCZ1234F",
            NonResidentPartyName = "Global Ventures LLC",
            NonResidentCountry = "United States",
            InstrumentType = CapitalInstrumentType.EquityShares,
            NumberOfInstruments = 50000,
            FaceValuePerInstrument = 10m,
            IssueOrTransferPricePerInstrument = 200m,
            FairValuePerInstrument = 180m,
            TotalConsiderationInr = 10000000m,
            DateOfAllotmentOrTransfer = new DateTime(2025, 4, 10),
            ValuationMethod = "DiscountedCashFlow"
        };

        FemaFirmsEngine.CalculateFirmsCompliance(filing, new DateTime(2025, 4, 20));
        var html = FemaFirmsEngine.GenerateComplianceCertificateHtml(filing);

        Assert.Contains("FORM FC-GPR", html);
        Assert.Contains("Zenith Robotics India Pvt Ltd", html);
        Assert.Contains("U72900MH2021PTC123456", html);
        Assert.Contains("Pricing Guidelines Satisfied", html);
        Assert.Contains("CERTIFICATE BY CHARTERED ACCOUNTANT", html);
    }

    [Fact]
    public void RbiFlaEngine_CalculatesNetWorth_FdiAtNetAssetValue_Correctly()
    {
        var fla = new RbiAnnualFlaReturn
        {
            PaidUpCapitalInr = 20000000m,     // 2 Cr
            ReservesAndSurplusInr = 80000000m, // 8 Cr -> Net Worth = 10 Cr
            ForeignEquityHoldingPct = 40.00m,  // 40% FDI
            StatutoryFilingDeadline = new DateTime(2025, 7, 15)
        };

        RbiFlaEngine.CalculateFlaReturn(fla, new DateTime(2025, 6, 1));

        Assert.Equal(100000000m, fla.NetWorthInr);
        Assert.Equal(40000000m, fla.FdiAtNetAssetValueInr); // 40% of 10 Cr = 4 Cr
        Assert.True(fla.IsMandatoryFiling);
        Assert.False(fla.IsDeadlineBreached);
        Assert.Equal(44, fla.DaysRemainingToDeadline);
    }

    [Fact]
    public void RbiFlaEngine_TotalOdiFinancialCommitment_SumsEquityLoansGuarantees()
    {
        var fla = new RbiAnnualFlaReturn
        {
            HasOdi = true,
            OverseasEntityName = "Zenith Global Inc",
            OverseasCountry = "Singapore",
            OdiEquityInvestmentInr = 15000000m,
            OdiLoanOutstandingInr = 5000000m,
            OdiGuaranteeIssuedInr = 10000000m
        };

        RbiFlaEngine.CalculateFlaReturn(fla, DateTime.UtcNow);

        Assert.Equal(30000000m, fla.TotalOdiFinancialCommitmentInr);
        Assert.True(fla.IsMandatoryFiling);
    }

    [Fact]
    public void RbiFlaEngine_NetForeignLiabilitiesPosition_CalculatesCorrectly()
    {
        var fla = new RbiAnnualFlaReturn
        {
            PaidUpCapitalInr = 10000000m,
            ReservesAndSurplusInr = 40000000m, // Net Worth = 5 Cr
            ForeignEquityHoldingPct = 50.00m,  // FDI at NAV = 2.5 Cr
            ExternalCommercialBorrowingsEcbInr = 10000000m, // 1 Cr
            ForeignTradeCreditSuppliersInr = 5000000m,      // 50 Lakhs
            // Liabilities = 2.5 Cr + 1 Cr + 50L = 4.0 Cr

            HasOdi = true,
            OdiEquityInvestmentInr = 10000000m, // 1 Cr
            ForeignBankBalancesInr = 2000000m   // 20 Lakhs
            // Assets = 1.2 Cr
        };

        RbiFlaEngine.CalculateFlaReturn(fla, DateTime.UtcNow);

        Assert.Equal(40000000m - 12000000m, fla.NetForeignLiabilitiesPositionInr); // 2.8 Cr
    }

    [Fact]
    public void RbiFlaEngine_July15DeadlineCountdown_FlagsBreachWhenOverdue()
    {
        var fla = new RbiAnnualFlaReturn
        {
            FinancialYear = "2024-25",
            StatutoryFilingDeadline = new DateTime(2025, 7, 15)
        };

        // As of July 20, 2025 (5 days late)
        RbiFlaEngine.CalculateFlaReturn(fla, new DateTime(2025, 7, 20));

        Assert.True(fla.IsDeadlineBreached);
        Assert.True(fla.DaysRemainingToDeadline < 0);
        Assert.Contains("OVERDUE", fla.FilingStatusAlert);
    }

    [Fact]
    public void RbiFlaEngine_GenerateFlaReturnHtml_RendersAllFourSections()
    {
        var fla = new RbiAnnualFlaReturn
        {
            FinancialYear = "2024-25",
            CompanyName = "Aura Tech Solutions Ltd",
            CompanyCin = "L72200DL2018PLC334455",
            Pan = "AABCA9988K",
            PaidUpCapitalInr = 50000000m,
            ReservesAndSurplusInr = 150000000m,
            ForeignEquityHoldingPct = 30m,
            HasOdi = true,
            OverseasEntityName = "Aura EMEA BV",
            OverseasCountry = "Netherlands",
            OdiEquityInvestmentInr = 25000000m
        };

        RbiFlaEngine.CalculateFlaReturn(fla, new DateTime(2025, 6, 15));
        var html = RbiFlaEngine.GenerateFlaReturnHtml(fla);

        Assert.Contains("SECTION I: FINANCIAL PARTICULARS", html);
        Assert.Contains("SECTION II: FOREIGN DIRECT INVESTMENT (FDI)", html);
        Assert.Contains("SECTION III: OVERSEAS DIRECT INVESTMENT (ODI)", html);
        Assert.Contains("SECTION IV: OTHER FOREIGN LIABILITIES", html);
        Assert.Contains("Aura Tech Solutions Ltd", html);
        Assert.Contains("L72200DL2018PLC334455", html);
    }

    [Fact]
    public void DtaaMfnPeEngine_EvaluateMfnClause_TreatyCountryWithMfn_RequiresCbdtNotificationUnderNestleRuling()
    {
        var (hasMfn, mfnRate, isMandatoryNotification, guidance) = DtaaMfnPeEngine.EvaluateMfnClause("Netherlands", 10.0m);

        Assert.True(hasMfn);
        Assert.Equal(10.0m, mfnRate);
        Assert.True(isMandatoryNotification);
        Assert.Contains("Nestle SA", guidance);
    }

    [Fact]
    public void DtaaMfnPeEngine_EvaluatePeRisk_PhysicalOffice_FlagsFixedPlacePe()
    {
        var (isPe, peType, summary) = DtaaMfnPeEngine.EvaluatePeRisk(
            hasPhysicalOffice: true,
            personnelDaysInIndia: 10,
            hasAuthorityToConcludeContracts: false,
            maintainsInventoryStock: false,
            treatyCountry: "United States"
        );

        Assert.True(isPe);
        Assert.Equal("FixedPlacePE", peType);
        Assert.Contains("Fixed Place PE under Article 5(1)", summary);
    }

    [Fact]
    public void DtaaMfnPeEngine_EvaluatePeRisk_PersonnelExceedingDays_FlagsServicePe()
    {
        var (isPe, peType, summary) = DtaaMfnPeEngine.EvaluatePeRisk(
            hasPhysicalOffice: false,
            personnelDaysInIndia: 195, // Exceeds US 182-day threshold
            hasAuthorityToConcludeContracts: false,
            maintainsInventoryStock: false,
            treatyCountry: "United States"
        );

        Assert.True(isPe);
        Assert.Equal("ServicePE", peType);
        Assert.Contains("Service PE", summary);
    }

    [Fact]
    public void DtaaMfnPeEngine_EvaluatePeRisk_NoOfficeOrAuthority_PassesClean()
    {
        var (isPe, peType, summary) = DtaaMfnPeEngine.EvaluatePeRisk(
            hasPhysicalOffice: false,
            personnelDaysInIndia: 45, // Within threshold
            hasAuthorityToConcludeContracts: false,
            maintainsInventoryStock: false,
            treatyCountry: "Singapore"
        );

        Assert.False(isPe);
        Assert.Equal("NoPE", peType);
        Assert.Contains("No Permanent Establishment exposure", summary);
    }
}
