using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.ForeignRemittance;

public class ForeignRemittanceAssessment : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = string.Empty; // e.g. "2024-25"
    public string RemittanceReference { get; set; } = string.Empty; // e.g. "REM-2024-001"

    // Remitter (Payer) Details
    public string RemitterPan { get; set; } = string.Empty;
    public string RemitterTan { get; set; } = string.Empty;
    public string RemitterName { get; set; } = string.Empty;
    public string RemitterStatus { get; set; } = "Company"; // Company, Individual, Firm, LLP
    public string RemitterAddress { get; set; } = string.Empty;
    public string RemitterEmail { get; set; } = string.Empty;
    public string RemitterPhone { get; set; } = string.Empty;

    // Beneficiary (Remittee / Payee) Details
    public string BeneficiaryName { get; set; } = string.Empty;
    public string BeneficiaryCountry { get; set; } = string.Empty;
    public string BeneficiaryCountryCode { get; set; } = string.Empty; // e.g. "US", "SG", "GB"
    public string BeneficiaryAddress { get; set; } = string.Empty;
    public string BeneficiaryTaxId { get; set; } = string.Empty; // TIN / SSN / EIN
    public string BeneficiaryEntityType { get; set; } = "Company"; // Company, Individual, Other

    // Remittance Particulars
    public string RemittanceCurrency { get; set; } = "USD"; // USD, EUR, GBP, SGD, etc.
    public decimal ForeignAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal InrEquivalent { get; set; }
    public DateTime? ProposedRemittanceDate { get; set; }
    public string RbiPurposeCode { get; set; } = "P0802"; // e.g. P0802, S1007, S1006
    public string PurposeDescription { get; set; } = "Software consultancy / implementation";
    public string NatureOfRemittance { get; set; } = "FeesForTechnicalServices"; // Royalty, FeesForTechnicalServices, ProfessionalServices, Interest, Dividend, CapitalGains, PurchaseOfGoods, Commission, Reimbursement, Other
    public string ContractDescription { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }

    // Chargeability & Domestic Taxability (Section 195 / 9(1) / 115A)
    public bool IsChargeableToTax { get; set; } = true;
    public string ChargeabilityReason { get; set; } = string.Empty;
    public string DomesticSection { get; set; } = "Section 195 read with Section 115A";
    public decimal DomesticTaxRate { get; set; } = 20.00m; // Default statutory rate (e.g. 20% for Royalty/FTS)
    public decimal SurchargeRate { get; set; } = 0.00m;
    public decimal CessRate { get; set; } = 4.00m; // 4% Health & Education Cess
    public decimal EffectiveDomesticRate { get; set; } = 20.80m;

    // DTAA / Treaty Relief (Section 90 / 90A)
    public bool TreatyBenefitClaimed { get; set; } = true;
    public string TreatyCountry { get; set; } = string.Empty;
    public string TreatyArticle { get; set; } = "Article 12 - Royalties and Fees for Technical Services";
    public decimal TreatyTaxRate { get; set; } = 10.00m;
    public bool HasTrc { get; set; } = true; // Tax Residency Certificate
    public DateTime? TrcPeriodFrom { get; set; }
    public DateTime? TrcPeriodTo { get; set; }
    public bool HasForm10F { get; set; } = true; // Form 10F verified
    public bool NoPeDeclaration { get; set; } = true; // Permanent Establishment non-existence
    public bool BeneficialOwnershipConfirmed { get; set; } = true;
    public bool MliPptPassed { get; set; } = true; // Multilateral Instrument Principal Purpose Test
    public string ApplicableRateType { get; set; } = "TreatyRate"; // TreatyRate, DomesticRate, NilRateExempt, AOCertificateRate
    public decimal FinalWithholdingRate { get; set; } = 10.00m;

    // Grossing up (Section 195A) & Calculations
    public bool TaxBorneByRemitter { get; set; } = false; // "Net of tax" agreement
    public decimal GrossedUpAmountInr { get; set; }
    public decimal TdsAmountInr { get; set; }
    public decimal TdsAmountFc { get; set; }
    public decimal NetRemittanceFc { get; set; }
    public decimal NetRemittanceInr { get; set; }

    // Form 15CA Routing (Rule 37BB)
    public string Form15CaPart { get; set; } = "PartC"; // PartA, PartB, PartC, PartD, ExemptRule37BB
    public string Form15CaRoutingReason { get; set; } = string.Empty;
    public string? AoOrderSection { get; set; } // "195(2)", "195(3)", "197"
    public string? AoOrderNumber { get; set; }
    public DateTime? AoOrderDate { get; set; }
    public decimal? AoCertificateRate { get; set; }

    // Form 15CB CA Certification Particulars
    public bool RequiresForm15Cb { get; set; } = true;
    public string CaMembershipNumber { get; set; } = string.Empty;
    public string CaName { get; set; } = string.Empty;
    public string CaFirmName { get; set; } = string.Empty;
    public string CaFrn { get; set; } = string.Empty;
    public string Udin { get; set; } = string.Empty;
    public DateTime? CertifiedAt { get; set; }
    public Guid? CertifierUserId { get; set; }

    // Generated Statutory Artifacts
    public string? Generated15CbCertificateText { get; set; }
    public string? Generated15CaXmlPayload { get; set; }
    public string? AcknowledgementNumber { get; set; }

    public string Status { get; set; } = "Draft"; // Draft, Assessed, Certified, Filed

    // Checklist of supporting documentation
    public List<ForeignRemittanceChecklistItem> Checklist { get; set; } = [];
}

public class ForeignRemittanceChecklistItem : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public ForeignRemittanceAssessment? Assessment { get; set; }

    public string ItemCode { get; set; } = string.Empty; // e.g. TRC, FORM_10F, NO_PE, INVOICE, CONTRACT, SEC_197_LDC
    public string ItemDescription { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public string Status { get; set; } = "Pending"; // Pending, Verified, Rejected, Waived
    public string VerificationNotes { get; set; } = string.Empty;
    public DateTime? VerifiedAt { get; set; }
}

public static class ForeignRemittanceEngine
{
    // 33 Specified List RBI Purpose Codes exempt from 15CA/15CB under Rule 37BB(3)
    private static readonly HashSet<string> Rule37BbExemptCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "P0101", "P0102", "P0103", "P0104", "P0105", "P0106", "P0107", "P0108", "P0109",
        "S0001", "S0003", "S0005", "S0011", "S0014", "S0020", "S0021", "S0301", "S0302",
        "S0303", "S0304", "S0305", "S1101", "S1102", "S1103", "S1104", "S1105", "S1106",
        "S1108", "S1109", "S1301", "S1302", "S1303", "S1501"
    };

    public static bool IsRule37BbExempt(string? purposeCode)
    {
        if (string.IsNullOrWhiteSpace(purposeCode)) return false;
        return Rule37BbExemptCodes.Contains(purposeCode.Trim());
    }

    public static void CalculateAssessment(ForeignRemittanceAssessment assessment)
    {
        // 1. Calculate INR Equivalent
        assessment.InrEquivalent = Math.Round(assessment.ForeignAmount * assessment.ExchangeRate, 2);

        // 2. Check Rule 37BB(3) Specified Exempt List
        bool isExemptCode = IsRule37BbExempt(assessment.RbiPurposeCode);

        // 3. Calculate Effective Domestic Rate with surcharge and 4% cess
        // Surcharge applies to tax: Tax * (1 + Surcharge) * (1 + Cess)
        decimal taxWithSurcharge = assessment.DomesticTaxRate * (1m + (assessment.SurchargeRate / 100m));
        assessment.EffectiveDomesticRate = Math.Round(taxWithSurcharge * (1m + (assessment.CessRate / 100m)), 4);

        // 4. Rate Determination & DTAA Relief (Sec 90(2))
        if (!assessment.IsChargeableToTax)
        {
            assessment.ApplicableRateType = "NilRateExempt";
            assessment.FinalWithholdingRate = 0m;
        }
        else if (!string.IsNullOrWhiteSpace(assessment.AoOrderSection) && assessment.AoCertificateRate.HasValue)
        {
            // Lower deduction / nil order under Section 195(2), 195(3), or 197
            assessment.ApplicableRateType = "AOCertificateRate";
            assessment.FinalWithholdingRate = assessment.AoCertificateRate.Value;
        }
        else if (assessment.TreatyBenefitClaimed)
        {
            // Verify mandatory DTAA prerequisites
            bool eligibleForTreaty = assessment.HasTrc && assessment.HasForm10F && assessment.NoPeDeclaration && assessment.BeneficialOwnershipConfirmed;

            if (eligibleForTreaty && assessment.TreatyTaxRate < assessment.EffectiveDomesticRate)
            {
                assessment.ApplicableRateType = "TreatyRate";
                assessment.FinalWithholdingRate = assessment.TreatyTaxRate;
            }
            else
            {
                assessment.ApplicableRateType = "DomesticRate";
                assessment.FinalWithholdingRate = assessment.EffectiveDomesticRate;
            }
        }
        else
        {
            assessment.ApplicableRateType = "DomesticRate";
            assessment.FinalWithholdingRate = assessment.EffectiveDomesticRate;
        }

        // 5. Section 195A Grossing up & TDS calculation
        if (assessment.TaxBorneByRemitter && assessment.FinalWithholdingRate > 0 && assessment.FinalWithholdingRate < 100m)
        {
            decimal netFactor = 1m - (assessment.FinalWithholdingRate / 100m);
            assessment.GrossedUpAmountInr = Math.Round(assessment.InrEquivalent / netFactor, 2);
            assessment.TdsAmountInr = Math.Round(assessment.GrossedUpAmountInr - assessment.InrEquivalent, 2);
            assessment.TdsAmountFc = assessment.ExchangeRate > 0 ? Math.Round(assessment.TdsAmountInr / assessment.ExchangeRate, 2) : 0m;
            assessment.NetRemittanceFc = assessment.ForeignAmount;
            assessment.NetRemittanceInr = assessment.InrEquivalent;
        }
        else
        {
            assessment.GrossedUpAmountInr = assessment.InrEquivalent;
            assessment.TdsAmountInr = Math.Round(assessment.InrEquivalent * (assessment.FinalWithholdingRate / 100m), 2);
            assessment.TdsAmountFc = Math.Round(assessment.ForeignAmount * (assessment.FinalWithholdingRate / 100m), 2);
            assessment.NetRemittanceFc = Math.Round(assessment.ForeignAmount - assessment.TdsAmountFc, 2);
            assessment.NetRemittanceInr = Math.Round(assessment.InrEquivalent - assessment.TdsAmountInr, 2);
        }

        // 6. Form 15CA Part Auto-Routing (Forms 145 / 146 under the Income-tax Rules, 2026 from tax year 2026-27)
        var fy = assessment.FinancialYear;
        bool act2025 = IncomeTaxLaw.IsAct2025(fy);
        string form15Ca = IncomeTaxLaw.Form("15CA", fy), form15Cb = IncomeTaxLaw.Form("15CB", fy);
        string yearWord = act2025 ? "tax year" : "financial year";
        if (isExemptCode)
        {
            assessment.Form15CaPart = "ExemptRule37BB";
            assessment.Form15CaRoutingReason = $"Remittance is exempt as specified purpose code {assessment.RbiPurposeCode}{(act2025 ? " (exempt list carried over from rule 37BB(3) of the Income-tax Rules, 1962)" : " under Rule 37BB(3)")}. Neither {form15Ca} nor {form15Cb} is required.";
            assessment.RequiresForm15Cb = false;
        }
        else if (!assessment.IsChargeableToTax)
        {
            assessment.Form15CaPart = "PartD";
            assessment.Form15CaRoutingReason = $"Remittance is not chargeable to tax under the provisions of the {IncomeTaxLaw.ActName(fy)} (Part D). No {form15Cb} required.";
            assessment.RequiresForm15Cb = false;
        }
        else if (!string.IsNullOrWhiteSpace(assessment.AoOrderSection))
        {
            assessment.Form15CaPart = "PartB";
            assessment.Form15CaRoutingReason = $"Remittance is chargeable to tax and an order/certificate under {IncomeTaxLaw.Sec(assessment.AoOrderSection!, fy)} has been obtained from the Assessing Officer (Part B). No {form15Cb} required.";
            assessment.RequiresForm15Cb = false;
        }
        else if (assessment.InrEquivalent <= 500000m)
        {
            assessment.Form15CaPart = "PartA";
            assessment.Form15CaRoutingReason = $"Remittance is chargeable to tax and aggregate remittances do not exceed ₹5,00,000 in the {yearWord} (Part A). No {form15Cb} required.";
            assessment.RequiresForm15Cb = false;
        }
        else
        {
            assessment.Form15CaPart = "PartC";
            assessment.Form15CaRoutingReason = $"Remittance is chargeable to tax and exceeds ₹5,00,000 in the {yearWord}. {form15Cb} CA Certificate is MANDATORY (Part C).";
            assessment.RequiresForm15Cb = true;
        }
    }

    public static string GenerateForm15CbCertificate(ForeignRemittanceAssessment assessment)
    {
        var sb = new StringBuilder();
        var fy = assessment.FinancialYear;
        bool act2025 = IncomeTaxLaw.IsAct2025(fy); // Form No. 146 under the Income-tax Rules, 2026 from tax year 2026-27
        string S(string ref1961) => IncomeTaxLaw.SecShort(ref1961, fy);
        sb.AppendLine("==========================================================================================");
        sb.AppendLine($"                                      {IncomeTaxLaw.Form("15CB", fy).ToUpperInvariant()}");
        sb.AppendLine(act2025
            ? "                              [See the Income-tax Rules, 2026]                            "
            : "                                [See rule 37BB, sub-rule (2)]                             ");
        sb.AppendLine("                   Certificate of an accountant on payments to non-resident               ");
        sb.AppendLine("==========================================================================================");
        sb.AppendLine();
        sb.AppendLine($"1. Name of the Remitter                        : {assessment.RemitterName}");
        sb.AppendLine($"2. PAN of the Remitter                         : {assessment.RemitterPan}");
        sb.AppendLine($"3. TAN of the Remitter                         : {assessment.RemitterTan}");
        sb.AppendLine($"4. Status of the Remitter                      : {assessment.RemitterStatus}");
        sb.AppendLine($"5. Address of the Remitter                     : {assessment.RemitterAddress}");
        sb.AppendLine($"6. Principal Place of Business                 : {assessment.RemitterAddress}");
        sb.AppendLine($"7. Email ID of the Remitter                    : {assessment.RemitterEmail}");
        sb.AppendLine($"8. Phone Number of the Remitter                : {assessment.RemitterPhone}");
        sb.AppendLine();
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine("DETAILS OF BENEFICIARY / REMITTEE (NON-RESIDENT)");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"9.  Name of the Beneficiary                    : {assessment.BeneficiaryName}");
        sb.AppendLine($"10. Country to which remittance is made        : {assessment.BeneficiaryCountry} ({assessment.BeneficiaryCountryCode})");
        sb.AppendLine($"11. Tax Identification Number in Country       : {assessment.BeneficiaryTaxId}");
        sb.AppendLine($"12. Entity Type of Beneficiary                 : {assessment.BeneficiaryEntityType}");
        sb.AppendLine($"13. Address of the Beneficiary                 : {assessment.BeneficiaryAddress}");
        sb.AppendLine();
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine("DETAILS OF REMITTANCE & TRANSACTION");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"14. Currency of Remittance                     : {assessment.RemittanceCurrency}");
        sb.AppendLine($"15. Amount in Foreign Currency                 : {assessment.ForeignAmount:N2} {assessment.RemittanceCurrency}");
        sb.AppendLine($"16. Applicable Exchange Rate (TT Selling)      : ₹{assessment.ExchangeRate:F4} per 1 {assessment.RemittanceCurrency}");
        sb.AppendLine($"17. Amount in Indian Currency (INR)            : ₹{assessment.InrEquivalent:N2}");
        sb.AppendLine($"18. Proposed Date of Remittance                : {assessment.ProposedRemittanceDate?.ToString("dd-MMM-yyyy") ?? "Pending Execution"}");
        sb.AppendLine($"19. RBI Purpose Code                           : {assessment.RbiPurposeCode} - {assessment.PurposeDescription}");
        sb.AppendLine($"20. Nature of Remittance                       : {assessment.NatureOfRemittance}");
        sb.AppendLine($"21. Contract / Invoice Details                 : Inv #{assessment.InvoiceNumber} dated {assessment.InvoiceDate?.ToString("dd-MMM-yyyy") ?? "N/A"} ({assessment.ContractDescription})");
        sb.AppendLine();
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"TAXABILITY IN INDIA UNDER THE {IncomeTaxLaw.ActName(fy).ToUpperInvariant()}");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"22. Is the remittance chargeable to tax?       : {(assessment.IsChargeableToTax ? "YES" : "NO")}");
        sb.AppendLine($"23. Relevant Section of the Act                : {assessment.DomesticSection}");
        sb.AppendLine($"24. Chargeability Reason / Analysis            : {assessment.ChargeabilityReason}");
        sb.AppendLine($"25. Domestic Base Tax Rate                     : {assessment.DomesticTaxRate:F2}%");
        sb.AppendLine($"26. Surcharge & Cess Rates                     : Surcharge: {assessment.SurchargeRate:F2}%, Cess: {assessment.CessRate:F2}%");
        sb.AppendLine($"27. Effective Domestic Tax Rate                : {assessment.EffectiveDomesticRate:F4}%");
        sb.AppendLine();
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine("TAXABILITY UNDER DOUBLE TAXATION AVOIDANCE AGREEMENT (DTAA)");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"28. Treaty Benefit Claimed                     : {(assessment.TreatyBenefitClaimed ? "YES" : "NO")}");
        if (assessment.TreatyBenefitClaimed)
        {
            sb.AppendLine($"29. Country of Tax Treaty                      : {assessment.TreatyCountry}");
            sb.AppendLine($"30. Relevant Treaty Article                    : {assessment.TreatyArticle}");
            sb.AppendLine($"31. Tax Rate under DTAA                        : {assessment.TreatyTaxRate:F2}%");
            sb.AppendLine($"32. Tax Residency Certificate (TRC) verified   : {(assessment.HasTrc ? "YES" : "NO")} (Period: {assessment.TrcPeriodFrom?.ToString("dd-MMM-yyyy") ?? "N/A"} to {assessment.TrcPeriodTo?.ToString("dd-MMM-yyyy") ?? "N/A"})");
            sb.AppendLine($"33. {IncomeTaxLaw.Form("10F", fy).Replace("Form No. ", "Form "),-14} Electronically Verified  : {(assessment.HasForm10F ? "YES" : "NO")}");
            sb.AppendLine($"34. Non-PE / No Business Connection Confirmed  : {(assessment.NoPeDeclaration ? "YES" : "NO")}");
            sb.AppendLine($"35. Beneficial Ownership of Income Confirmed   : {(assessment.BeneficialOwnershipConfirmed ? "YES" : "NO")}");
            sb.AppendLine($"36. MLI Principal Purpose Test (PPT) Passed    : {(assessment.MliPptPassed ? "YES" : "NO")}");
        }
        sb.AppendLine($"37. Governing Tax Rate Applied ({S("90")})      : {assessment.FinalWithholdingRate:F2}% ({assessment.ApplicableRateType})");
        sb.AppendLine();
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"TAX DEDUCTION AT SOURCE (TDS) COMPUTATION ({(act2025 ? IncomeTaxLaw.Sec("195", fy) : "SECTION 195 / 195A").ToUpperInvariant()})");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"38. Tax Borne by Remitter (Grossing-up)        : {(assessment.TaxBorneByRemitter ? "YES (Net-of-tax contract)" : "NO")}");
        if (assessment.TaxBorneByRemitter)
        {
            sb.AppendLine($"39. Grossed-up Amount in INR ({S("195A")})       : ₹{assessment.GrossedUpAmountInr:N2}");
        }
        sb.AppendLine($"40. Total TDS Deducted in INR                  : ₹{assessment.TdsAmountInr:N2}");
        sb.AppendLine($"41. Equivalent TDS in Foreign Currency         : {assessment.TdsAmountFc:N2} {assessment.RemittanceCurrency}");
        sb.AppendLine($"42. Net Remittance Amount Payable to Remittee  : {assessment.NetRemittanceFc:N2} {assessment.RemittanceCurrency} (₹{assessment.NetRemittanceInr:N2})");
        sb.AppendLine();
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine("CERTIFICATE OF ACCOUNTANT (CHARTERED ACCOUNTANT IN PRACTICE)");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine("I/We have examined the agreement, invoices, books of account and other relevant documents");
        sb.AppendLine($"of {assessment.RemitterName} relating to the proposed remittance of {assessment.ForeignAmount:N2} {assessment.RemittanceCurrency} to");
        sb.AppendLine($"{assessment.BeneficiaryName}, a non-resident entity located in {assessment.BeneficiaryCountry}.");
        sb.AppendLine();
        sb.AppendLine("I/We hereby certify that:");
        sb.AppendLine($"  1. The remittance has been evaluated in accordance with {IncomeTaxLaw.Sec("195", fy)} of the {IncomeTaxLaw.ActName(fy)},");
        sb.AppendLine($"     and the Double Taxation Avoidance Agreement between India and {assessment.BeneficiaryCountry}.");
        sb.AppendLine("  2. The tax deduction at source (TDS) has been properly computed at the rate of " + $"{assessment.FinalWithholdingRate:F2}%,");
        sb.AppendLine($"     amounting to ₹{assessment.TdsAmountInr:N2} ({assessment.TdsAmountFc:N2} {assessment.RemittanceCurrency}).");
        sb.AppendLine($"  3. The documents verifying residential status (TRC, {IncomeTaxLaw.Form("10F", fy).Replace("Form No. ", "Form ")}, and No-PE declaration) have been verified.");
        sb.AppendLine();
        sb.AppendLine($"Chartered Accountant Name      : {assessment.CaName}");
        sb.AppendLine($"ICAI Membership Number         : {assessment.CaMembershipNumber}");
        sb.AppendLine($"Firm Name                      : {assessment.CaFirmName}");
        sb.AppendLine($"Firm Registration Number (FRN) : {assessment.CaFrn}");
        sb.AppendLine($"Unique Document ID (UDIN)      : {assessment.Udin}");
        sb.AppendLine($"Date of Certification          : {assessment.CertifiedAt?.ToString("dd-MM-yyyy") ?? DateTime.UtcNow.ToString("dd-MM-yyyy")}");
        sb.AppendLine($"Place of Certification         : India");
        sb.AppendLine("==========================================================================================");

        return sb.ToString();
    }

    public static string GenerateForm15CaXmlPayload(ForeignRemittanceAssessment assessment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<Form15CA xmlns=\"http://incometaxindiaefiling.gov.in/Form15CA\" version=\"1.0\">");
        sb.AppendLine("  <Header>");
        sb.AppendLine($"    <FinancialYear>{assessment.FinancialYear}</FinancialYear>");
        sb.AppendLine($"    <AssessmentYear>{GetAssessmentYear(assessment.FinancialYear)}</AssessmentYear>");
        sb.AppendLine($"    <FormPart>{assessment.Form15CaPart}</FormPart>");
        sb.AppendLine($"    <FilingDate>{DateTime.UtcNow:yyyy-MM-dd}</FilingDate>");
        sb.AppendLine("  </Header>");
        sb.AppendLine("  <RemitterDetails>");
        sb.AppendLine($"    <RemitterPAN>{assessment.RemitterPan}</RemitterPAN>");
        sb.AppendLine($"    <RemitterTAN>{assessment.RemitterTan}</RemitterTAN>");
        sb.AppendLine($"    <RemitterName><![CDATA[{assessment.RemitterName}]]></RemitterName>");
        sb.AppendLine($"    <Status>{assessment.RemitterStatus}</Status>");
        sb.AppendLine($"    <Address><![CDATA[{assessment.RemitterAddress}]]></Address>");
        sb.AppendLine($"    <Email>{assessment.RemitterEmail}</Email>");
        sb.AppendLine($"    <Phone>{assessment.RemitterPhone}</Phone>");
        sb.AppendLine("  </RemitterDetails>");
        sb.AppendLine("  <RemitteeDetails>");
        sb.AppendLine($"    <BeneficiaryName><![CDATA[{assessment.BeneficiaryName}]]></BeneficiaryName>");
        sb.AppendLine($"    <CountryCode>{assessment.BeneficiaryCountryCode}</CountryCode>");
        sb.AppendLine($"    <CountryName><![CDATA[{assessment.BeneficiaryCountry}]]></CountryName>");
        sb.AppendLine($"    <TaxIdentificationNumber>{assessment.BeneficiaryTaxId}</TaxIdentificationNumber>");
        sb.AppendLine($"    <EntityType>{assessment.BeneficiaryEntityType}</EntityType>");
        sb.AppendLine($"    <Address><![CDATA[{assessment.BeneficiaryAddress}]]></Address>");
        sb.AppendLine("  </RemitteeDetails>");
        sb.AppendLine("  <RemittanceParticulars>");
        sb.AppendLine($"    <Currency>{assessment.RemittanceCurrency}</Currency>");
        sb.AppendLine($"    <ForeignCurrencyAmount>{assessment.ForeignAmount:F2}</ForeignCurrencyAmount>");
        sb.AppendLine($"    <RateOfExchange>{assessment.ExchangeRate:F4}</RateOfExchange>");
        sb.AppendLine($"    <InrEquivalent>{assessment.InrEquivalent:F2}</InrEquivalent>");
        sb.AppendLine($"    <ProposedDateOfRemittance>{assessment.ProposedRemittanceDate?.ToString("yyyy-MM-dd") ?? ""}</ProposedDateOfRemittance>");
        sb.AppendLine($"    <RbiPurposeCode>{assessment.RbiPurposeCode}</RbiPurposeCode>");
        sb.AppendLine($"    <NatureOfRemittance>{assessment.NatureOfRemittance}</NatureOfRemittance>");
        sb.AppendLine($"    <InvoiceNumber><![CDATA[{assessment.InvoiceNumber}]]></InvoiceNumber>");
        sb.AppendLine($"    <InvoiceDate>{assessment.InvoiceDate?.ToString("yyyy-MM-dd") ?? ""}</InvoiceDate>");
        sb.AppendLine("  </RemittanceParticulars>");
        sb.AppendLine("  <TaxComputation>");
        sb.AppendLine($"    <IsChargeableToTax>{(assessment.IsChargeableToTax ? "Y" : "N")}</IsChargeableToTax>");
        sb.AppendLine($"    <DomesticSection>{assessment.DomesticSection}</DomesticSection>");
        sb.AppendLine($"    <ApplicableRateType>{assessment.ApplicableRateType}</ApplicableRateType>");
        sb.AppendLine($"    <FinalWithholdingRate>{assessment.FinalWithholdingRate:F2}</FinalWithholdingRate>");
        sb.AppendLine($"    <TaxBorneByRemitter>{(assessment.TaxBorneByRemitter ? "Y" : "N")}</TaxBorneByRemitter>");
        sb.AppendLine($"    <TdsAmountInr>{assessment.TdsAmountInr:F2}</TdsAmountInr>");
        sb.AppendLine($"    <TdsAmountFc>{assessment.TdsAmountFc:F2}</TdsAmountFc>");
        sb.AppendLine($"    <NetRemittanceFc>{assessment.NetRemittanceFc:F2}</NetRemittanceFc>");
        sb.AppendLine($"    <NetRemittanceInr>{assessment.NetRemittanceInr:F2}</NetRemittanceInr>");
        sb.AppendLine("  </TaxComputation>");

        if (assessment.Form15CaPart == "PartC")
        {
            sb.AppendLine("  <Form15CbDetails>");
            sb.AppendLine($"    <AccountantName><![CDATA[{assessment.CaName}]]></AccountantName>");
            sb.AppendLine($"    <MembershipNumber>{assessment.CaMembershipNumber}</MembershipNumber>");
            sb.AppendLine($"    <FirmName><![CDATA[{assessment.CaFirmName}]]></FirmName>");
            sb.AppendLine($"    <FirmRegistrationNumber>{assessment.CaFrn}</FirmRegistrationNumber>");
            sb.AppendLine($"    <UDIN>{assessment.Udin}</UDIN>");
            sb.AppendLine($"    <CertificateDate>{assessment.CertifiedAt?.ToString("yyyy-MM-dd") ?? ""}</CertificateDate>");
            sb.AppendLine("  </Form15CbDetails>");
        }
        else if (assessment.Form15CaPart == "PartB")
        {
            sb.AppendLine("  <AoOrderDetails>");
            sb.AppendLine($"    <AoSection>{assessment.AoOrderSection}</AoSection>");
            sb.AppendLine($"    <AoOrderNumber><![CDATA[{assessment.AoOrderNumber}]]></AoOrderNumber>");
            sb.AppendLine($"    <AoOrderDate>{assessment.AoOrderDate?.ToString("yyyy-MM-dd") ?? ""}</AoOrderDate>");
            sb.AppendLine($"    <AoCertificateRate>{assessment.AoCertificateRate:F2}</AoCertificateRate>");
            sb.AppendLine("  </AoOrderDetails>");
        }

        sb.AppendLine("</Form15CA>");
        return sb.ToString();
    }

    private static string GetAssessmentYear(string financialYear)
    {
        if (string.IsNullOrWhiteSpace(financialYear) || !financialYear.Contains('-'))
            return "2025-26";

        var parts = financialYear.Split('-');
        if (int.TryParse(parts[0], out int startYear))
        {
            int ayStart = startYear + 1;
            int ayEnd = (ayStart + 1) % 100;
            return $"{ayStart}-{ayEnd:D2}";
        }

        return "2025-26";
    }
}
