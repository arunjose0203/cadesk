using CaDesk.Domain.Gstr9;

namespace CaDesk.Application.Gstr9;

public record Gstr9ReturnDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string FinancialYear,
    string Gstin,
    string LegalName,
    string? TradeName,
    decimal AggregateTurnover,
    bool IsGstr9Mandatory,
    bool IsGstr9cMandatory,
    string ExemptionStatus,
    Gstr9FilingStatus Status,
    decimal OutwardTaxableSupplies,
    decimal OutwardExemptedSupplies,
    decimal TotalTurnover,
    decimal ItcAvailed3b,
    decimal ItcCategorizedB2b,
    decimal ItcReversed,
    decimal NetItcAvailed,
    decimal ItcAsPer2a,
    decimal ItcReconciledVariance,
    decimal TotalTaxPayable,
    decimal TaxPaidCash,
    decimal TaxPaidItc,
    decimal ShortfallTax,
    decimal PreviousFyAmendmentsTax,
    string? Arn,
    DateTime? FiledAt,
    string? Notes,
    int TableEntriesCount,
    bool HasGstr9c,
    Gstr9cCertificationStatus? Gstr9cStatus,
    bool Drc03Pending,
    DateTime CreatedAt
);

public record Gstr9TableEntryDto(
    Guid Id,
    Guid Gstr9ReturnId,
    Gstr9TableSection TableSection,
    string RowCode,
    string Description,
    decimal TaxableValue,
    decimal IgstAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal CessAmount,
    decimal TotalTax
);

public record Gstr9cReconciliationDto(
    Guid Id,
    Guid Gstr9ReturnId,
    Guid ClientId,
    string FinancialYear,
    decimal AuditedGrossTurnover,
    decimal UnbilledRevenueStart,
    decimal UnadjustedAdvancesEnd,
    decimal DeemedSupplies,
    decimal CreditNotesAfterFy,
    decimal TradeDiscountsDisallowed,
    decimal UnbilledRevenueEnd,
    decimal UnadjustedAdvancesStart,
    decimal OtherTurnoverAdjustments,
    decimal ReconciledTurnover,
    decimal TurnoverAsPerGstr9,
    decimal UnreconciledTurnoverVariance,
    decimal ExemptedNilTurnover,
    decimal ZeroRatedWopTurnover,
    decimal RcmOutwardTurnover,
    decimal ReconciledTaxableTurnover,
    decimal TaxableTurnoverGstr9,
    decimal UnreconciledTaxableVariance,
    decimal AdditionalTaxPayable,
    decimal AdditionalInterestPayable,
    decimal AdditionalPenaltyPayable,
    decimal TotalAdditionalPayable,
    decimal BookItcTotal,
    decimal Gstr9ItcTotal,
    decimal UnreconciledItcVariance,
    Gstr9cCertificationStatus CertificationStatus,
    string? AuditorName,
    string? AuditorMembershipNo,
    string? AuditorFirmRegNo,
    string? Udin,
    DateTime? CertifiedAt,
    string? AuditorRemarks,
    string? Drc03Arn,
    DateOnly? Drc03Date,
    decimal Drc03PaidAmount,
    string? Drc03PaymentMode,
    bool IsDrc03Completed,
    List<Gstr9cDiscrepancyDto> Discrepancies
);

public record Gstr9cDiscrepancyDto(
    Guid Id,
    Guid Gstr9cId,
    DiscrepancyCategory Category,
    string TableRef,
    string Description,
    decimal DiscrepancyAmount,
    decimal TaxImpact,
    string? AuditorRecommendation,
    bool IsResolved,
    string? ResolutionNotes,
    DateTime CreatedAt
);

public record CompileGstr9ReturnRequest(
    Guid ClientId,
    string FinancialYear,
    decimal AggregateTurnover,
    decimal OutwardTaxableSupplies,
    decimal OutwardExemptedSupplies,
    decimal RcmInwardSupplies,
    decimal ItcAvailed3b,
    decimal ItcCategorizedB2b,
    decimal ItcReversed,
    decimal ItcAsPer2a,
    decimal ItcSubsequentFy,
    decimal TotalTaxPayable,
    decimal TaxPaidCash,
    decimal TaxPaidItc,
    decimal PreviousFyAmendmentsTax,
    string? Notes
);

public record UpdateGstr9ReturnRequest(
    decimal AggregateTurnover,
    decimal OutwardTaxableSupplies,
    decimal OutwardExemptedSupplies,
    decimal RcmInwardSupplies,
    decimal ItcAvailed3b,
    decimal ItcCategorizedB2b,
    decimal ItcReversed,
    decimal ItcAsPer2a,
    decimal ItcSubsequentFy,
    decimal TotalTaxPayable,
    decimal TaxPaidCash,
    decimal TaxPaidItc,
    decimal PreviousFyAmendmentsTax,
    string? Notes
);

public record ReconcileGstr9cRequest(
    decimal AuditedGrossTurnover,
    decimal UnbilledRevenueStart,
    decimal UnadjustedAdvancesEnd,
    decimal DeemedSupplies,
    decimal CreditNotesAfterFy,
    decimal TradeDiscountsDisallowed,
    decimal UnbilledRevenueEnd,
    decimal UnadjustedAdvancesStart,
    decimal OtherTurnoverAdjustments,
    decimal ExemptedNilTurnover,
    decimal ZeroRatedWopTurnover,
    decimal RcmOutwardTurnover,
    decimal AdditionalTaxPayable,
    decimal AdditionalInterestPayable,
    decimal AdditionalPenaltyPayable,
    decimal BookItcTotal,
    string? AuditorRemarks
);

public record AddDiscrepancyRequest(
    DiscrepancyCategory Category,
    string TableRef,
    string Description,
    decimal DiscrepancyAmount,
    decimal TaxImpact,
    string? AuditorRecommendation
);

public record ResolveDiscrepancyRequest(
    string ResolutionNotes
);

public record CertifyGstr9cRequest(
    string AuditorName,
    string AuditorMembershipNo,
    string AuditorFirmRegNo,
    string? Udin,
    string? AuditorRemarks
);

public record RecordDrc03PaymentRequest(
    string Drc03Arn,
    DateOnly Drc03Date,
    decimal Drc03PaidAmount,
    string? Drc03PaymentMode
);

public record FileGstr9ReturnRequest(
    string Arn,
    string? Notes
);

public record Gstr9SummaryDto(
    int TotalReturns,
    int MandatoryGstr9Count,
    int MandatoryGstr9cCount,
    int CertifiedGstr9cCount,
    int FiledReturnsCount,
    decimal TotalTurnoverReconciled,
    decimal TotalTaxPayable,
    decimal TotalTaxShortfall,
    decimal TotalDrc03Paid
);
