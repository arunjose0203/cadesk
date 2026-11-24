using CaDesk.Domain.Ibc;

namespace CaDesk.Application.Ibc;

public record CirpCaseDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientPan,
    string CorporateDebtorName,
    string Cin,
    string NcltBench,
    string CourtOrderNumber,
    DateOnly AdmissionDate,
    string IrpName,
    string IrpIpRegNumber,
    string RpName,
    string RpIpRegNumber,
    bool MoratoriumSection14Declared,
    DateOnly ClaimsLastDate,
    DateOnly Initial180DaysDeadline,
    DateOnly Extended270DaysDeadline,
    DateOnly Final330DaysLimit,
    int DaysElapsed,
    int DaysRemainingTo180,
    int DaysRemainingTo330,
    bool Is180DaysExpired,
    bool Is330DaysCapBreached,
    string TimelineStatusAlert,
    CirpStage Stage,
    decimal TotalAdmittedFinancialDebt,
    decimal TotalAdmittedOperationalDebt,
    string StatusNote,
    int MembersCount,
    int ResolutionsCount,
    int PufeCount,
    int ValuationsCount,
    DateTime CreatedAt
);

public record CirpCaseDetailDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string ClientPan,
    string CorporateDebtorName,
    string Cin,
    string NcltBench,
    string CourtOrderNumber,
    DateOnly AdmissionDate,
    string IrpName,
    string IrpIpRegNumber,
    string RpName,
    string RpIpRegNumber,
    bool MoratoriumSection14Declared,
    DateOnly ClaimsLastDate,
    DateOnly Initial180DaysDeadline,
    DateOnly Extended270DaysDeadline,
    DateOnly Final330DaysLimit,
    int DaysElapsed,
    int DaysRemainingTo180,
    int DaysRemainingTo330,
    bool Is180DaysExpired,
    bool Is330DaysCapBreached,
    string TimelineStatusAlert,
    CirpStage Stage,
    decimal TotalAdmittedFinancialDebt,
    decimal TotalAdmittedOperationalDebt,
    string StatusNote,
    List<CocMemberDto> CocMembers,
    List<CocResolutionItemDto> Resolutions,
    List<PufeTransactionItemDto> PufeTransactions,
    List<AssetValuationItemDto> Valuations,
    DateTime CreatedAt
);

public record CocMemberDto(
    Guid Id,
    Guid CirpCaseId,
    string CreditorName,
    CreditorCategory Category,
    decimal ClaimSubmittedAmount,
    decimal AdmittedClaimAmount,
    decimal VotingSharePct,
    string SecurityInterestDescription,
    bool IsRelatedParty,
    DateTime CreatedAt
);

public record CocResolutionItemDto(
    Guid Id,
    Guid CirpCaseId,
    int MeetingNumber,
    string ResolutionNumber,
    string ResolutionTitle,
    string StatutorySection,
    decimal RequiredThresholdPct,
    decimal VotingShareInFavourPct,
    decimal VotingShareAgainstPct,
    decimal VotingShareAbstainedPct,
    VotingResolutionStatus Status,
    DateTime CreatedAt
);

public record PufeTransactionItemDto(
    Guid Id,
    Guid CirpCaseId,
    PufeType PufeType,
    DateOnly TransactionDate,
    string CounterpartyName,
    bool IsRelatedParty,
    decimal TransactionAmount,
    decimal EstimatedAvoidanceRecoverableAmount,
    int LookbackPeriodYears,
    bool IsWithinLookbackPeriod,
    string ForensicAuditorObservations,
    bool NcltApplicationFiled,
    DateTime CreatedAt
);

public record AssetValuationItemDto(
    Guid Id,
    Guid CirpCaseId,
    AssetValuationClass AssetClass,
    string AssetDescription,
    string Valuer1Name,
    decimal Valuer1FairValue,
    decimal Valuer1LiquidationValue,
    string Valuer2Name,
    decimal Valuer2FairValue,
    decimal Valuer2LiquidationValue,
    decimal AverageFairValue,
    decimal AverageLiquidationValue,
    bool ThirdValuerRequired,
    DateTime CreatedAt
);

public record SaveCirpCaseRequest(
    Guid? Id,
    Guid ClientId,
    string CorporateDebtorName,
    string Cin,
    string NcltBench,
    string CourtOrderNumber,
    DateOnly AdmissionDate,
    string IrpName,
    string IrpIpRegNumber,
    string RpName,
    string RpIpRegNumber,
    bool MoratoriumSection14Declared,
    CirpStage Stage,
    string StatusNote
);

public record SaveCocMemberRequest(
    Guid? Id,
    Guid CirpCaseId,
    string CreditorName,
    CreditorCategory Category,
    decimal ClaimSubmittedAmount,
    decimal AdmittedClaimAmount,
    string SecurityInterestDescription,
    bool IsRelatedParty
);

public record SaveCocResolutionRequest(
    Guid? Id,
    Guid CirpCaseId,
    int MeetingNumber,
    string ResolutionNumber,
    string ResolutionTitle,
    string StatutorySection,
    decimal RequiredThresholdPct,
    decimal VotingShareInFavourPct,
    decimal VotingShareAgainstPct,
    decimal VotingShareAbstainedPct
);

public record SavePufeTransactionRequest(
    Guid? Id,
    Guid CirpCaseId,
    PufeType PufeType,
    DateOnly TransactionDate,
    string CounterpartyName,
    bool IsRelatedParty,
    decimal TransactionAmount,
    decimal EstimatedAvoidanceRecoverableAmount,
    string ForensicAuditorObservations,
    bool NcltApplicationFiled
);

public record SaveAssetValuationRequest(
    Guid? Id,
    Guid CirpCaseId,
    AssetValuationClass AssetClass,
    string AssetDescription,
    string Valuer1Name,
    decimal Valuer1FairValue,
    decimal Valuer1LiquidationValue,
    string Valuer2Name,
    decimal Valuer2FairValue,
    decimal Valuer2LiquidationValue
);

public record CalculateCirpTimelineRequest(
    DateOnly AdmissionDate,
    DateOnly AsOfDate
);

public record CirpTimelineCalculationDto(
    DateOnly AdmissionDate,
    DateOnly ClaimsLastDate,
    DateOnly Initial180Days,
    DateOnly Extended270Days,
    DateOnly Final330Days,
    int DaysElapsed,
    int DaysRemainingTo180,
    int DaysRemainingTo330,
    bool Is180DaysExpired,
    bool Is330DaysCapBreached,
    string TimelineStatusAlert
);
