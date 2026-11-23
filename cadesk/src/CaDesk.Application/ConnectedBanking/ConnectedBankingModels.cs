namespace CaDesk.Application.ConnectedBanking;

public sealed record ConnectedBankSummaryDto(
    Guid Id,
    Guid BankAccountId,
    Guid ClientId,
    string ClientName,
    string BankName,
    string AccountNumber,
    string IfscCode,
    string AccountType,
    string Provider,
    string CorporateId,
    string Status,
    DateTime? LastSyncAt,
    decimal? LastSyncBalance,
    decimal CurrentBalance,
    int SyncFrequencyMinutes
);

public sealed record ConnectBankRequest(
    Guid BankAccountId,
    string Provider,
    string CorporateId,
    string UserId,
    string ApiKey,
    string ApiSecret,
    string? WebhookSecret = null
);

public sealed record LiveSyncResultDto(
    Guid BankAccountId,
    string Provider,
    decimal SyncedBalance,
    int SyncedTransactionsCount,
    DateTime SyncTimestamp,
    Guid? StatementId,
    string Message
);

public sealed record PayoutItemDto(
    Guid Id,
    Guid BatchId,
    string PayoutType,
    string BeneficiaryName,
    string BeneficiaryAccountNumber,
    string BeneficiaryIfsc,
    string? BeneficiaryPan,
    decimal Amount,
    string PaymentMode,
    string Narration,
    string? InvoiceOrChallanRef,
    string Status,
    string? UtrNumber,
    string? FailureReason
);

public sealed record PayoutBatchDto(
    Guid Id,
    string BatchNumber,
    Guid BankAccountId,
    string BankName,
    string AccountNumber,
    Guid ClientId,
    string ClientName,
    string Title,
    decimal TotalAmount,
    int TotalCount,
    string Status,
    Guid CreatedByUserId,
    string MakerName,
    string? MakerRemarks,
    Guid? AuthorizedByUserId,
    string? CheckerName,
    DateTime? AuthorizedAt,
    string? AuthMethod,
    string? AuthReference,
    string? RejectionReason,
    DateTime CreatedAt,
    IReadOnlyList<PayoutItemDto> Items
);

public sealed record CreatePayoutItemInput(
    string PayoutType,
    string BeneficiaryName,
    string BeneficiaryAccountNumber,
    string BeneficiaryIfsc,
    string? BeneficiaryPan,
    decimal Amount,
    string PaymentMode,
    string Narration,
    string? InvoiceOrChallanRef
);

public sealed record CreatePayoutBatchRequest(
    Guid BankAccountId,
    string Title,
    string? MakerRemarks,
    DateOnly? ScheduledExecutionDate,
    List<CreatePayoutItemInput> Items
);

public sealed record AuthorizePayoutBatchRequest(
    string AuthMethod, // OTP, DSC_SHA256, Biometric
    string? OtpOrToken,
    string? PartnerRemarks
);

public sealed record RejectPayoutBatchRequest(
    string Reason
);

public sealed record GenerateTaxChallanPayoutRequest(
    Guid BankAccountId,
    string ChallanType, // AdvanceTax_ITNS280, TDS_ITNS281, GST_PMT06
    string FinancialYear,
    string AssessmentYear,
    string MajorHeadCode, // 0020 (Company), 0021 (Non-Company), 0005 (GST)
    string MinorHeadCode, // 100 (Advance Tax), 200 (TDS/TCS), 300 (Self-Assessment)
    decimal TaxAmount,
    decimal SurchargeAmount,
    decimal CessAmount,
    decimal InterestAmount,
    decimal FeePenaltyAmount,
    string PanOrTanOrGstin,
    DateOnly? ScheduleDate = null
);
