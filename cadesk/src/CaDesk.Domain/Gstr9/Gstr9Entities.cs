using CaDesk.Domain.Common;

namespace CaDesk.Domain.Gstr9;

public enum Gstr9FilingStatus
{
    Draft,
    Reconciled,
    Certified,
    Filed
}

public enum Gstr9TableSection
{
    Table4,
    Table5,
    Table6,
    Table7,
    Table8,
    Table9,
    Table10_14
}

public enum Gstr9cCertificationStatus
{
    Draft,
    UnderReview,
    Certified
}

public enum DiscrepancyCategory
{
    TurnoverTimingDifference,
    UnbilledRevenue,
    AdvanceAdjustment,
    CreditNoteAdjustment,
    RateMismatch,
    ItcBookVsReturnMismatch,
    ExpenseItcUnclaimed,
    NonGstSupplyDifference,
    Other
}

public class Gstr9Return : TenantEntity
{
    public Guid ClientId { get; set; }
    public string FinancialYear { get; set; } = string.Empty; // e.g. "2023-24"
    public string Gstin { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public decimal AggregateTurnover { get; set; }
    public bool IsGstr9Mandatory { get; set; }
    public bool IsGstr9cMandatory { get; set; }
    public Gstr9FilingStatus Status { get; set; } = Gstr9FilingStatus.Draft;

    // Part II: Outward & Inward Supplies
    public decimal OutwardTaxableSupplies { get; set; } // Table 4N
    public decimal OutwardExemptedSupplies { get; set; } // Table 5M
    public decimal TotalTurnover { get; set; } // Table 5N = 4N + 5M - 4G

    // Part III: Input Tax Credit (ITC)
    public decimal ItcAvailed3b { get; set; } // Table 6A
    public decimal ItcCategorizedB2b { get; set; } // Table 6B
    public decimal ItcReversed { get; set; } // Table 7I
    public decimal NetItcAvailed { get; set; } // Table 7J = 6A - 7I
    public decimal ItcAsPer2a { get; set; } // Table 8A
    public decimal ItcReconciledVariance { get; set; } // Table 8D = 8A - (8B + 8C)

    // Part IV: Tax Details
    public decimal TotalTaxPayable { get; set; } // Table 9
    public decimal TaxPaidCash { get; set; }
    public decimal TaxPaidItc { get; set; }
    public decimal ShortfallTax { get; set; }

    // Part V: Subsequent FY Declarations
    public decimal PreviousFyAmendmentsTax { get; set; } // Table 14

    // Filing metadata
    public string? Arn { get; set; }
    public DateTime? FiledAt { get; set; }
    public string? Notes { get; set; }

    public List<Gstr9TableEntry> TableEntries { get; set; } = new();
    public Gstr9cReconciliation? Reconciliation { get; set; }
}

public class Gstr9TableEntry : TenantEntity
{
    public Guid Gstr9ReturnId { get; set; }
    public Gstr9TableSection TableSection { get; set; }
    public string RowCode { get; set; } = string.Empty; // e.g. 4A, 4B, 5A, 6B, 7A, 8A
    public string Description { get; set; } = string.Empty;
    public decimal TaxableValue { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal CessAmount { get; set; }

    public decimal TotalTax => IgstAmount + CgstAmount + SgstAmount + CessAmount;
}

public class Gstr9cReconciliation : TenantEntity
{
    public Guid Gstr9ReturnId { get; set; }
    public Guid ClientId { get; set; }
    public string FinancialYear { get; set; } = string.Empty;

    // Table 5: Gross Turnover Reconciliation
    public decimal AuditedGrossTurnover { get; set; } // 5A
    public decimal UnbilledRevenueStart { get; set; } // 5B (+)
    public decimal UnadjustedAdvancesEnd { get; set; } // 5C (+)
    public decimal DeemedSupplies { get; set; } // 5D (+)
    public decimal CreditNotesAfterFy { get; set; } // 5E (+)
    public decimal TradeDiscountsDisallowed { get; set; } // 5F (+)
    public decimal UnbilledRevenueEnd { get; set; } // 5H (-)
    public decimal UnadjustedAdvancesStart { get; set; } // 5I (-)
    public decimal OtherTurnoverAdjustments { get; set; } // 5O (+/-)
    public decimal ReconciledTurnover { get; set; } // 5P
    public decimal TurnoverAsPerGstr9 { get; set; } // 5Q
    public decimal UnreconciledTurnoverVariance { get; set; } // 5R = 5P - 5Q

    // Table 7: Taxable Turnover Reconciliation
    public decimal ExemptedNilTurnover { get; set; } // 7B (-)
    public decimal ZeroRatedWopTurnover { get; set; } // 7C (-)
    public decimal RcmOutwardTurnover { get; set; } // 7D (-)
    public decimal ReconciledTaxableTurnover { get; set; } // 7E = 5P - 7B - 7C - 7D
    public decimal TaxableTurnoverGstr9 { get; set; } // 7F
    public decimal UnreconciledTaxableVariance { get; set; } // 7G = 7E - 7F

    // Table 9: Additional Tax Liability
    public decimal AdditionalTaxPayable { get; set; }
    public decimal AdditionalInterestPayable { get; set; }
    public decimal AdditionalPenaltyPayable { get; set; }

    // Table 12 & 14: ITC Reconciliation
    public decimal BookItcTotal { get; set; } // 12A
    public decimal Gstr9ItcTotal { get; set; } // 12E
    public decimal UnreconciledItcVariance { get; set; } // 12F = 12A - 12E

    // Certification & Verification
    public Gstr9cCertificationStatus CertificationStatus { get; set; } = Gstr9cCertificationStatus.Draft;
    public string? AuditorName { get; set; }
    public string? AuditorMembershipNo { get; set; }
    public string? AuditorFirmRegNo { get; set; }
    public string? Udin { get; set; }
    public DateTime? CertifiedAt { get; set; }
    public string? AuditorRemarks { get; set; }

    // DRC-03 Payment Tracking
    public string? Drc03Arn { get; set; }
    public DateOnly? Drc03Date { get; set; }
    public decimal Drc03PaidAmount { get; set; }
    public string? Drc03PaymentMode { get; set; } = "CashLedger";
    public bool IsDrc03Completed { get; set; }

    public List<Gstr9cDiscrepancy> Discrepancies { get; set; } = new();
}

public class Gstr9cDiscrepancy : TenantEntity
{
    public Guid Gstr9cId { get; set; }
    public DiscrepancyCategory Category { get; set; }
    public string TableRef { get; set; } = "Table5"; // Table5, Table7, Table9, Table12, Table14
    public string Description { get; set; } = string.Empty;
    public decimal DiscrepancyAmount { get; set; }
    public decimal TaxImpact { get; set; }
    public string? AuditorRecommendation { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolutionNotes { get; set; }
}

public static class Gstr9ThresholdEngine
{
    public const decimal Gstr9Threshold = 20_000_000m;  // ₹2 Crores
    public const decimal Gstr9cThreshold = 50_000_000m; // ₹5 Crores

    public static bool IsGstr9Mandatory(decimal aggregateTurnover) => aggregateTurnover > Gstr9Threshold;
    public static bool IsGstr9cMandatory(decimal aggregateTurnover) => aggregateTurnover > Gstr9cThreshold;

    public static string GetExemptionStatus(decimal aggregateTurnover)
    {
        if (aggregateTurnover <= Gstr9Threshold)
            return "Exempt (Optional filing under CBIC Notification)";
        if (aggregateTurnover <= Gstr9cThreshold)
            return "Mandatory GSTR-9 (GSTR-9C Self-Certification Exempt)";
        return "Mandatory GSTR-9 and GSTR-9C Reconciliation";
    }
}

public static class Gstr9CalculationEngine
{
    public static decimal CalculateTable5Turnover(decimal outwardTaxable4N, decimal outwardExempt5M, decimal rcmInward4G)
    {
        // Table 5N = Total turnover (including advances etc.) on which tax is payable (4N) + not payable (5M) - inward RCM (4G)
        return (outwardTaxable4N + outwardExempt5M) - rcmInward4G;
    }

    public static decimal CalculateTable8ItcVariance(decimal itc2a8A, decimal itcClaimed8B, decimal itcNextFy8C)
    {
        // Table 8D = 8A - (8B + 8C)
        return itc2a8A - (itcClaimed8B + itcNextFy8C);
    }

    public static (decimal reconciledTurnover, decimal variance) CalculateTable5GrossTurnover(
        decimal auditedGross5A,
        decimal unbilledStart5B,
        decimal unadjustedAdvEnd5C,
        decimal deemed5D,
        decimal creditNotesAfterFy5E,
        decimal tradeDiscountsDisallowed5F,
        decimal unbilledEnd5H,
        decimal unadjustedAdvStart5I,
        decimal otherAdjustments5O,
        decimal gstr9Turnover5Q)
    {
        // 5P = 5A + 5B + 5C + 5D + 5E + 5F - 5H - 5I + 5O
        var additions = unbilledStart5B + unadjustedAdvEnd5C + deemed5D + creditNotesAfterFy5E + tradeDiscountsDisallowed5F + otherAdjustments5O;
        var subtractions = unbilledEnd5H + unadjustedAdvStart5I;
        var reconciled5P = auditedGross5A + additions - subtractions;
        var variance5R = reconciled5P - gstr9Turnover5Q;
        return (reconciled5P, variance5R);
    }

    public static (decimal reconciledTaxableTurnover, decimal variance) CalculateTable7TaxableTurnover(
        decimal reconciledGross5P,
        decimal exempt7B,
        decimal zeroRated7C,
        decimal rcm7D,
        decimal gstr9Taxable7F)
    {
        // 7E = 5P - (7B + 7C + 7D)
        var reconciledTaxable7E = reconciledGross5P - (exempt7B + zeroRated7C + rcm7D);
        var variance7G = reconciledTaxable7E - gstr9Taxable7F;
        return (reconciledTaxable7E, variance7G);
    }

    public static decimal CalculateTable12ItcVariance(decimal bookItc12A, decimal gstr9Itc12E)
    {
        // 12F = 12A - 12E
        return bookItc12A - gstr9Itc12E;
    }
}
