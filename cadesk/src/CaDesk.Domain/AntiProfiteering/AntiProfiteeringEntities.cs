using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.AntiProfiteering;

public class GstRateNotification : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string NotificationNumber { get; set; } = "";
    public DateTime NotificationDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string HsnOrSacCode { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal PreviousRatePercentage { get; set; }
    public decimal RevisedRatePercentage { get; set; }
    public decimal RateReductionPercentage { get; set; }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AntiProfiteeringAssessment : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string AssessmentReference { get; set; } = "";
    public string Title { get; set; } = "";
    public string FinancialYear { get; set; } = "";
    public DateTime AuditPeriodFrom { get; set; }
    public DateTime AuditPeriodTo { get; set; }

    public Guid? GstNotificationId { get; set; }
    public GstRateNotification? GstNotification { get; set; }
    public string NotificationReference { get; set; } = "";
    public DateTime? RateCutEffectiveDate { get; set; }

    public string AuditStatus { get; set; } = "Draft"; // Draft, UnderReview, Certified, FindingsLogged
    public string LeadAuditorName { get; set; } = "";
    public string? Udin { get; set; } // 18-digit ICAI UDIN
    public DateTime? CertificationDate { get; set; }

    public int TotalSkusAudited { get; set; }
    public int ProfiteeredSkusCount { get; set; }
    public decimal TotalTurnoverPostCut { get; set; }
    public decimal TotalUndueProfiteeredAmount { get; set; }
    public decimal TotalCustomerRefundedAmount { get; set; }
    public decimal TotalCwfDepositedAmount { get; set; }
    public decimal CwfInterestPayableAmount { get; set; }

    public string? StatutoryReportText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<SkuProfiteeringItem> SkuItems { get; set; } = [];
    public List<ProfiteeringRefundRecord> RefundRecords { get; set; } = [];
}

public class SkuProfiteeringItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid AssessmentId { get; set; }
    public AntiProfiteeringAssessment? Assessment { get; set; }

    public string ProductCode { get; set; } = "";
    public string HsnOrSacCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string UnitOfMeasurement { get; set; } = "PCS";

    public decimal PreCutBasePrice { get; set; }
    public decimal OldGstRate { get; set; }
    public decimal PreCutSellingPrice { get; set; }

    public decimal NewGstRate { get; set; }
    public decimal CommensuratePriceRequired { get; set; }

    public decimal PostCutActualBasePrice { get; set; }
    public decimal PostCutActualSellingPrice { get; set; }
    public decimal BasePriceIncreasePercentage { get; set; }

    public decimal QuantitySoldPostCut { get; set; }
    public decimal ProfiteeringPerUnit { get; set; }
    public decimal TotalProfiteeredAmount { get; set; }

    public string ComplianceStatus { get; set; } = "Compliant"; // Compliant, Deficient, Profiteered
    public bool HasRawMaterialCostJustification { get; set; }
    public string JustificationRemarks { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class ProfiteeringRefundRecord : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid AssessmentId { get; set; }
    public AntiProfiteeringAssessment? Assessment { get; set; }

    public string RecipientType { get; set; } = "IdentifiedCustomer"; // IdentifiedCustomer, ConsumerWelfareFundCentral, ConsumerWelfareFundState
    public string CustomerNameOrEntity { get; set; } = "";
    public string CustomerGstin { get; set; } = "";
    public string InvoiceReference { get; set; } = "";
    public decimal RefundAmount { get; set; }
    public string RefundMode { get; set; } = "CreditNote"; // CreditNote, BankTransfer, ChallanGST_PMT_06
    public DateTime RefundOrDepositDate { get; set; }
    public string ChallanOrCinNumber { get; set; } = "";
    public decimal InterestAmountPaid { get; set; } // Sec 171(3A) 18% p.a.
    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AntiProfiteeringEngine
{
    public static void EvaluateSku(SkuProfiteeringItem sku)
    {
        // 1. Pre-cut selling price = Base * (1 + OldRate)
        sku.PreCutSellingPrice = Math.Round(sku.PreCutBasePrice * (1m + (sku.OldGstRate / 100m)), 2);

        // 2. Commensurate selling price required by law = PreCutBase * (1 + NewRate)
        sku.CommensuratePriceRequired = Math.Round(sku.PreCutBasePrice * (1m + (sku.NewGstRate / 100m)), 2);

        // 3. Post-cut actual selling price charged = PostCutActualBase * (1 + NewRate)
        sku.PostCutActualSellingPrice = Math.Round(sku.PostCutActualBasePrice * (1m + (sku.NewGstRate / 100m)), 2);

        // 4. Base price increase %
        sku.BasePriceIncreasePercentage = sku.PreCutBasePrice > 0m
            ? Math.Round(((sku.PostCutActualBasePrice - sku.PreCutBasePrice) / sku.PreCutBasePrice) * 100m, 2)
            : 0m;

        // 5. Profiteering per unit = Max(0, ActualSelling - CommensurateRequired)
        sku.ProfiteeringPerUnit = Math.Max(0m, Math.Round(sku.PostCutActualSellingPrice - sku.CommensuratePriceRequired, 2));

        // 6. Total profiteered amount = ProfiteeringPerUnit * Quantity
        sku.TotalProfiteeredAmount = Math.Round(sku.ProfiteeringPerUnit * sku.QuantitySoldPostCut, 2);

        // 7. Compliance status classification
        if (sku.ProfiteeringPerUnit <= 0.01m)
        {
            sku.ComplianceStatus = "Compliant";
        }
        else if (sku.PostCutActualSellingPrice >= (sku.PreCutSellingPrice - 0.05m))
        {
            // Zero or negligible benefit passed (base price hiked to pocket tax cut)
            sku.ComplianceStatus = "Profiteered";
        }
        else
        {
            // Partial reduction passed, but base price was increased so reduction is not commensurate
            sku.ComplianceStatus = "Deficient";
        }
    }

    public static (decimal CentralCwf, decimal StateCwf, decimal InterestAmount) CalculateCwfSplit(decimal unrefundedAmount, int delayDays)
    {
        if (unrefundedAmount <= 0m) return (0m, 0m, 0m);

        decimal central = Math.Round(unrefundedAmount * 0.50m, 2);
        decimal state = Math.Round(unrefundedAmount * 0.50m, 2);

        // Section 171(3A) 18% p.a. interest calculation if delayed
        decimal interest = 0m;
        if (delayDays > 0)
        {
            interest = Math.Round(unrefundedAmount * 0.18m * ((decimal)delayDays / 365m), 2);
        }

        return (central, state, interest);
    }

    public static List<GstRateNotification> GenerateBaselineRateNotifications(Guid firmId)
    {
        return
        [
            new()
            {
                FirmId = firmId,
                NotificationNumber = "41/2017-Central Tax (Rate)",
                NotificationDate = new DateTime(2017, 11, 14),
                EffectiveDate = new DateTime(2017, 11, 15),
                HsnOrSacCode = "9963",
                Description = "Restaurant and Hospitality Catering Services (excluding 5-star hotel premises)",
                PreviousRatePercentage = 18.00m,
                RevisedRatePercentage = 5.00m,
                RateReductionPercentage = 13.00m,
                Notes = "Mandated non-ITC rate cut from 18% to 5% with commensurate price reduction."
            },
            new()
            {
                FirmId = firmId,
                NotificationNumber = "18/2018-Central Tax (Rate)",
                NotificationDate = new DateTime(2018, 7, 26),
                EffectiveDate = new DateTime(2018, 7, 27),
                HsnOrSacCode = "8418",
                Description = "Refrigerators, Freezers and Electric White Goods Appliances",
                PreviousRatePercentage = 28.00m,
                RevisedRatePercentage = 18.00m,
                RateReductionPercentage = 10.00m,
                Notes = "GST Council 28th meeting rate rationalization on consumer durables."
            },
            new()
            {
                FirmId = firmId,
                NotificationNumber = "24/2018-Central Tax (Rate)",
                NotificationDate = new DateTime(2018, 12, 31),
                EffectiveDate = new DateTime(2019, 1, 1),
                HsnOrSacCode = "6403",
                Description = "Footwear with sale price exceeding ₹1,000 per pair and leather accessories",
                PreviousRatePercentage = 18.00m,
                RevisedRatePercentage = 12.00m,
                RateReductionPercentage = 6.00m,
                Notes = "Rate reduction to boost local manufacturing and consumption."
            },
            new()
            {
                FirmId = firmId,
                NotificationNumber = "14/2019-Central Tax (Rate)",
                NotificationDate = new DateTime(2019, 7, 30),
                EffectiveDate = new DateTime(2019, 8, 1),
                HsnOrSacCode = "8703",
                Description = "Electric Vehicles (EVs) and EV onboard charging equipment",
                PreviousRatePercentage = 12.00m,
                RevisedRatePercentage = 5.00m,
                RateReductionPercentage = 7.00m,
                Notes = "Green mobility incentive slashing EV GST from 12% to 5%."
            },
            new()
            {
                FirmId = firmId,
                NotificationNumber = "06/2022-Central Tax (Rate)",
                NotificationDate = new DateTime(2022, 7, 13),
                EffectiveDate = new DateTime(2022, 7, 18),
                HsnOrSacCode = "2106",
                Description = "Pre-packaged and labelled food products, curd, lassi and agricultural staples",
                PreviousRatePercentage = 0.00m,
                RevisedRatePercentage = 5.00m,
                RateReductionPercentage = -5.00m,
                Notes = "Tariff rationalization on pre-packaged commodity retail trade."
            }
        ];
    }

    public static string GenerateStatutoryAuditReport(AntiProfiteeringAssessment assessment, string clientName, string clientGstin)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================================================================");
        sb.AppendLine("              INDEPENDENT CHARTERED ACCOUNTANT'S ANTI-PROFITEERING AUDIT CERTIFICATE               ");
        sb.AppendLine("      [Pursuant to Section 171 of the Central Goods and Services Tax Act, 2017 & Rule 122-137]       ");
        sb.AppendLine("====================================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Entity Name        : {clientName}");
        sb.AppendLine($"Entity GSTIN       : {clientGstin}");
        sb.AppendLine($"Assessment Ref     : {assessment.AssessmentReference}");
        sb.AppendLine($"Financial Year     : {assessment.FinancialYear}");
        sb.AppendLine($"Audit Period       : {assessment.AuditPeriodFrom:dd-MMM-yyyy} to {assessment.AuditPeriodTo:dd-MMM-yyyy}");
        sb.AppendLine($"Rate Notification  : {assessment.NotificationReference}");
        sb.AppendLine($"Rate Cut Effective : {assessment.RateCutEffectiveDate?.ToString("dd-MMM-yyyy") ?? "N/A"}");
        sb.AppendLine($"Lead Auditor / CA  : {assessment.LeadAuditorName}");
        sb.AppendLine($"UDIN               : {assessment.Udin ?? "DRAFT / UNSEALED"}");
        sb.AppendLine($"Date of Report     : {assessment.CertificationDate?.ToString("dd-MMM-yyyy") ?? DateTime.UtcNow.ToString("dd-MMM-yyyy")}");
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("1. EXECUTIVE AUDIT SUMMARY & STATUTORY FINDINGS (SECTION 171)");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("We have examined the books of account, sales invoices, master price lists, and ERP transaction");
        sb.AppendLine($"data of {clientName} to verify compliance with Section 171 of the CGST Act, 2017 regarding");
        sb.AppendLine("passing on the benefit of reduction in rate of tax to recipients by way of commensurate price cuts.");
        sb.AppendLine();
        sb.AppendLine($"Total SKUs / Lines Audited     : {assessment.TotalSkusAudited}");
        sb.AppendLine($"Compliant SKUs (Passed On)     : {assessment.TotalSkusAudited - assessment.ProfiteeredSkusCount}");
        sb.AppendLine($"Non-Compliant / Profiteered    : {assessment.ProfiteeredSkusCount}");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Total Turnover Audited Post-Cut: Rs. {0:N2}", assessment.TotalTurnoverPostCut));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Total Undue Profiteered Amount : Rs. {0:N2}", assessment.TotalUndueProfiteeredAmount));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Refunded to Identified Buyers  : Rs. {0:N2}", assessment.TotalCustomerRefundedAmount));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Deposited to Consumer Welfare  : Rs. {0:N2}", assessment.TotalCwfDepositedAmount));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Section 171(3A) Interest (18%) : Rs. {0:N2}", assessment.CwfInterestPayableAmount));
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("2. SKU-LEVEL COMMENSURATE PRICE REDUCTION SCHEDULE");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-14} | {1,-20} | {2,8} | {3,8} | {4,10} | {5,10} | {6,12} | {7,-11}",
            "SKU Code", "Product Name", "Pre-Cut", "Old Rate", "Req. Price", "Act. Price", "Profiteered", "Status"));
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        foreach (var s in assessment.SkuItems.Take(30))
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-14} | {1,-20} | {2,8:F2} | {3,7:F1}% | {4,10:F2} | {5,10:F2} | {6,12:N2} | {7,-11}",
                s.ProductCode.Length > 14 ? s.ProductCode[..14] : s.ProductCode,
                s.ProductName.Length > 20 ? s.ProductName[..20] : s.ProductName,
                s.PreCutBasePrice,
                s.OldGstRate,
                s.CommensuratePriceRequired,
                s.PostCutActualSellingPrice,
                s.TotalProfiteeredAmount,
                s.ComplianceStatus));
        }

        if (assessment.SkuItems.Count > 30)
        {
            sb.AppendLine($"... and {assessment.SkuItems.Count - 30} additional SKUs audited.");
        }

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine();

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("3. STATUTORY REMEDIATION & CONSUMER WELFARE FUND (CWF) ALLOCATION");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        decimal unrefunded = Math.Max(0m, assessment.TotalUndueProfiteeredAmount - assessment.TotalCustomerRefundedAmount - assessment.TotalCwfDepositedAmount);
        var (central, state, intAmount) = CalculateCwfSplit(unrefunded, 30);

        sb.AppendLine($"Unrefunded Profiteering Balance: Rs. {unrefunded:N2}");
        sb.AppendLine($"Central Consumer Welfare Fund  : Rs. {central:N2} (50%)");
        sb.AppendLine($"State Consumer Welfare Fund    : Rs. {state:N2} (50%)");
        sb.AppendLine($"Mandatory Statutory Interest   : Rs. {intAmount:N2} (18% p.a. under Sec 171(3A))");
        sb.AppendLine();
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine("4. AUDITOR'S STATUTORY CONCLUSION & OPINION");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        if (assessment.TotalUndueProfiteeredAmount <= 0m || unrefunded <= 0m)
        {
            sb.AppendLine("In our opinion and to the best of our information, the entity has passed on the full benefit");
            sb.AppendLine("of the GST rate reduction to the recipients by way of commensurate reduction in prices in accordance");
            sb.AppendLine("with Section 171 of the CGST Act, 2017. No undue profiteering remains unaddressed.");
        }
        else
        {
            sb.AppendLine("ADVERSE FINDING / QUALIFICATION: The entity has failed to pass on the full benefit of rate reduction");
            sb.AppendLine($"amounting to Rs. {unrefunded:N2} to recipients. The entity must immediately credit this balance");
            sb.AppendLine("along with 18% statutory interest into the Central and State Consumer Welfare Funds pursuant to");
            sb.AppendLine("Rule 133(3)(c) of the CGST Rules, 2017 to avoid penalty under Section 171(3A).");
        }
        sb.AppendLine();
        sb.AppendLine($"Signed for and on behalf of the Audit Team:");
        sb.AppendLine($"{assessment.LeadAuditorName}");
        sb.AppendLine($"Chartered Accountants / Statutory Audit Firm");
        sb.AppendLine($"ICAI UDIN: {assessment.Udin ?? "DRAFT"}");

        return sb.ToString();
    }
}
