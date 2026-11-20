using System;
using CaDesk.Domain.ForeignRemittance;
using Xunit;

namespace CaDesk.UnitTests;

public class ForeignRemittanceUnitTests
{
    [Fact]
    public void Rule37Bb_ExemptPurposeCodes_IdentifiedCorrectly()
    {
        Assert.True(ForeignRemittanceEngine.IsRule37BbExempt("P0103")); // Import of goods
        Assert.True(ForeignRemittanceEngine.IsRule37BbExempt("S0001")); // Travel
        Assert.True(ForeignRemittanceEngine.IsRule37BbExempt("S1101")); // Maintenance
        Assert.False(ForeignRemittanceEngine.IsRule37BbExempt("P0802")); // Software consultancy
        Assert.False(ForeignRemittanceEngine.IsRule37BbExempt("S1007")); // Royalties
    }

    [Fact]
    public void NonChargeableRemittance_RoutesToPartD_AndZeroWithholding()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            ForeignAmount = 10000m,
            ExchangeRate = 85.00m,
            RbiPurposeCode = "P0802",
            IsChargeableToTax = false,
            ChargeabilityReason = "Offshore services with no Indian nexus or business connection"
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);

        Assert.Equal(850000m, assessment.InrEquivalent);
        Assert.Equal("PartD", assessment.Form15CaPart);
        Assert.False(assessment.RequiresForm15Cb);
        Assert.Equal(0m, assessment.FinalWithholdingRate);
        Assert.Equal(0m, assessment.TdsAmountInr);
        Assert.Equal(10000m, assessment.NetRemittanceFc);
    }

    [Fact]
    public void Rule37BbExemptRemittance_RoutesToExemptCategory()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            ForeignAmount = 25000m,
            ExchangeRate = 85.00m,
            RbiPurposeCode = "P0103", // Advance payment for import of goods
            IsChargeableToTax = true
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);

        Assert.Equal("ExemptRule37BB", assessment.Form15CaPart);
        Assert.False(assessment.RequiresForm15Cb);
    }

    [Fact]
    public void AssessingOfficerOrder_RoutesToPartB()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            ForeignAmount = 50000m,
            ExchangeRate = 85.00m,
            RbiPurposeCode = "S1007",
            IsChargeableToTax = true,
            AoOrderSection = "197",
            AoOrderNumber = "AO/197/2024/0042",
            AoCertificateRate = 3.50m
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);

        Assert.Equal("PartB", assessment.Form15CaPart);
        Assert.False(assessment.RequiresForm15Cb);
        Assert.Equal("AOCertificateRate", assessment.ApplicableRateType);
        Assert.Equal(3.50m, assessment.FinalWithholdingRate);
        Assert.Equal(148750m, assessment.TdsAmountInr); // 4,250,000 * 3.5%
    }

    [Fact]
    public void ChargeableBelow5Lakhs_RoutesToPartA()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            ForeignAmount = 4000m,
            ExchangeRate = 85.00m, // 340,000 INR <= 500,000
            RbiPurposeCode = "P0802",
            IsChargeableToTax = true,
            DomesticTaxRate = 20.00m,
            SurchargeRate = 0m,
            CessRate = 4.00m,
            TreatyBenefitClaimed = false
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);

        Assert.Equal(340000m, assessment.InrEquivalent);
        Assert.Equal("PartA", assessment.Form15CaPart);
        Assert.False(assessment.RequiresForm15Cb);
        Assert.Equal(20.80m, assessment.FinalWithholdingRate);
        Assert.Equal(70720m, assessment.TdsAmountInr); // 340,000 * 20.8%
    }

    [Fact]
    public void ChargeableAbove5Lakhs_WithValidDTAA_RoutesToPartC_AndAppliesTreatyRate()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            ForeignAmount = 20000m,
            ExchangeRate = 85.00m, // 1,700,000 INR > 500,000
            RbiPurposeCode = "S1006",
            IsChargeableToTax = true,
            DomesticTaxRate = 20.00m,
            SurchargeRate = 0m,
            CessRate = 4.00m,
            TreatyBenefitClaimed = true,
            TreatyCountry = "Singapore",
            TreatyTaxRate = 10.00m,
            HasTrc = true,
            HasForm10F = true,
            NoPeDeclaration = true,
            BeneficialOwnershipConfirmed = true
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);

        Assert.Equal(1700000m, assessment.InrEquivalent);
        Assert.Equal("PartC", assessment.Form15CaPart);
        Assert.True(assessment.RequiresForm15Cb);
        Assert.Equal("TreatyRate", assessment.ApplicableRateType);
        Assert.Equal(10.00m, assessment.FinalWithholdingRate);
        Assert.Equal(170000m, assessment.TdsAmountInr); // 1,700,000 * 10%
        Assert.Equal(2000m, assessment.TdsAmountFc);
        Assert.Equal(18000m, assessment.NetRemittanceFc);
        Assert.Equal(1530000m, assessment.NetRemittanceInr);
    }

    [Fact]
    public void DtaaPrerequisitesMissing_FallsBackToDomesticRate()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            ForeignAmount = 20000m,
            ExchangeRate = 85.00m,
            RbiPurposeCode = "S1006",
            IsChargeableToTax = true,
            DomesticTaxRate = 20.00m,
            SurchargeRate = 0m,
            CessRate = 4.00m,
            TreatyBenefitClaimed = true,
            TreatyCountry = "United States",
            TreatyTaxRate = 15.00m,
            HasTrc = false, // Missing TRC
            HasForm10F = true,
            NoPeDeclaration = true,
            BeneficialOwnershipConfirmed = true
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);

        Assert.Equal("DomesticRate", assessment.ApplicableRateType);
        Assert.Equal(20.80m, assessment.FinalWithholdingRate);
        Assert.Equal(353600m, assessment.TdsAmountInr); // 1,700,000 * 20.8%
    }

    [Fact]
    public void TaxBorneByRemitter_ComputesSection195AGrossingUp()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            ForeignAmount = 10000m,
            ExchangeRate = 85.00m, // 850,000 INR
            IsChargeableToTax = true,
            TreatyBenefitClaimed = true,
            TreatyTaxRate = 10.00m,
            HasTrc = true,
            HasForm10F = true,
            NoPeDeclaration = true,
            BeneficialOwnershipConfirmed = true,
            TaxBorneByRemitter = true // Net of tax contract
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);

        // Grossed up = 850,000 / (1 - 0.10) = 944,444.44
        Assert.Equal(944444.44m, assessment.GrossedUpAmountInr);
        Assert.Equal(94444.44m, assessment.TdsAmountInr);
        Assert.Equal(10000m, assessment.NetRemittanceFc);
        Assert.Equal(850000m, assessment.NetRemittanceInr);
    }

    [Fact]
    public void Form15CbCertificate_GeneratesValidStatutoryText()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            RemitterName = "TechNova Solutions Pvt Ltd",
            RemitterPan = "AAACT1234F",
            RemitterTan = "BLRT12345A",
            RemitterStatus = "Company",
            RemitterAddress = "MG Road, Bengaluru, Karnataka",
            BeneficiaryName = "CloudHost Global Inc",
            BeneficiaryCountry = "United States",
            BeneficiaryCountryCode = "US",
            BeneficiaryTaxId = "EIN-987654321",
            RemittanceCurrency = "USD",
            ForeignAmount = 50000m,
            ExchangeRate = 85.00m,
            RbiPurposeCode = "P0802",
            PurposeDescription = "Software hosting and cloud infrastructure",
            NatureOfRemittance = "FeesForTechnicalServices",
            InvoiceNumber = "INV-2024-998",
            InvoiceDate = new DateTime(2024, 10, 15),
            IsChargeableToTax = true,
            DomesticSection = "Section 195 read with Section 115A",
            TreatyBenefitClaimed = true,
            TreatyCountry = "United States",
            TreatyArticle = "Article 12 - Royalties and Fees for Included Services",
            TreatyTaxRate = 15.00m,
            HasTrc = true,
            HasForm10F = true,
            NoPeDeclaration = true,
            BeneficialOwnershipConfirmed = true,
            CaName = "Rajesh Sharma",
            CaMembershipNumber = "098765",
            CaFirmName = "Sharma & Associates LLP",
            CaFrn = "109876W",
            Udin = "24098765BK12345678",
            CertifiedAt = new DateTime(2024, 11, 20)
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);
        var certificate = ForeignRemittanceEngine.GenerateForm15CbCertificate(assessment);

        Assert.Contains("FORM NO. 15CB", certificate);
        Assert.Contains("TechNova Solutions Pvt Ltd", certificate);
        Assert.Contains("AAACT1234F", certificate);
        Assert.Contains("CloudHost Global Inc", certificate);
        Assert.Contains("United States (US)", certificate);
        Assert.Contains("15.00%", certificate);
        Assert.Contains("24098765BK12345678", certificate);
        Assert.Contains("Rajesh Sharma", certificate);
    }

    [Fact]
    public void Form15CaXmlPayload_GeneratesValidXmlStructure()
    {
        var assessment = new ForeignRemittanceAssessment
        {
            FinancialYear = "2024-25",
            RemitterPan = "AAACT1234F",
            RemitterTan = "BLRT12345A",
            RemitterName = "TechNova Solutions Pvt Ltd",
            BeneficiaryName = "CloudHost Global Inc",
            BeneficiaryCountry = "United States",
            BeneficiaryCountryCode = "US",
            BeneficiaryTaxId = "EIN-987654321",
            RemittanceCurrency = "USD",
            ForeignAmount = 10000m,
            ExchangeRate = 85.00m,
            RbiPurposeCode = "P0802",
            Form15CaPart = "PartC"
        };

        ForeignRemittanceEngine.CalculateAssessment(assessment);
        var xml = ForeignRemittanceEngine.GenerateForm15CaXmlPayload(assessment);

        Assert.Contains("<Form15CA", xml);
        Assert.Contains("<FinancialYear>2024-25</FinancialYear>", xml);
        Assert.Contains("<FormPart>PartC</FormPart>", xml);
        Assert.Contains("<RemitterPAN>AAACT1234F</RemitterPAN>", xml);
        Assert.Contains("CloudHost Global Inc", xml);
        Assert.Contains("</Form15CA>", xml);
    }
}
