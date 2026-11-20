namespace CaDesk.Application.Dsc;

public record CreateDscRequest(
    Guid? ClientId,
    string HolderName,
    string Pan,
    string? DscType,
    string? IssuingAuthority,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string? SerialNumber,
    string? UsbTokenId,
    string? Location,
    Guid? CustodianUserId,
    string? Notes
);

public record UpdateDscRequest(
    Guid? ClientId,
    string HolderName,
    string Pan,
    string? DscType,
    string? IssuingAuthority,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string? SerialNumber,
    string? UsbTokenId,
    string? Notes
);

public record TransferCustodyRequest(
    string ToLocation,
    Guid? CustodianUserId,
    string? Remarks
);

public record DscMovementLogDto(
    Guid Id,
    DateTime MovedAt,
    string FromLocation,
    string ToLocation,
    Guid? HandlerUserId,
    string? HandlerUserName,
    string? Remarks
);

public record DscDto(
    Guid Id,
    Guid? ClientId,
    string? ClientName,
    string HolderName,
    string Pan,
    string DscType,
    string IssuingAuthority,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    int DaysRemaining,
    string? SerialNumber,
    string? UsbTokenId,
    string Location,
    Guid? CustodianUserId,
    string? CustodianUserName,
    string Status,
    string? Notes,
    List<DscMovementLogDto> MovementLogs,
    DateTime CreatedAt
);

public record DscSummaryDto(
    int TotalActive,
    int ExpiringSoon,
    int Expired,
    int InVault,
    int CheckedOut
);
