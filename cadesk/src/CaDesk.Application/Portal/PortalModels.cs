namespace CaDesk.Application.Portal;

public record CreatePortalUserRequest(
    Guid ClientId,
    string Email,
    string Name,
    string? Phone,
    string Password,
    string? Role
);

public record PortalUserDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    string Email,
    string Name,
    string? Phone,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt
);

public record PortalLoginRequest(
    string Email,
    string Password
);

public record ClientSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string EntityType,
    Dictionary<string, string> TaxIds
);

public record PortalAuthResult(
    string AccessToken,
    int ExpiresIn,
    PortalUserDto User,
    ClientSummaryDto Client
);

public record CreateServiceRequestInput(
    string Category,
    string Subject,
    string Description,
    string? Priority
);

public record ServiceRequestMessageDto(
    Guid Id,
    string SenderType,
    string SenderName,
    Guid? SenderUserId,
    string Message,
    Guid? AttachmentDocId,
    DateTime CreatedAt
);

public record ServiceRequestDto(
    Guid Id,
    Guid ClientId,
    string? ClientName,
    Guid? PortalUserId,
    string? PortalUserName,
    string RequestNumber,
    string Category,
    string Subject,
    string Description,
    string Priority,
    string Status,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? ResolutionNotes,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<ServiceRequestMessageDto> Messages
);

public record AddRequestMessageInput(
    string Message,
    Guid? AttachmentDocId
);

public record UpdateServiceRequestStatusInput(
    string Status,
    string? ResolutionNotes,
    Guid? AssignedUserId
);

public record ClientFilingDto(
    Guid Id,
    string TypeCode,
    string PeriodKey,
    DateOnly DueDate,
    string Status,
    DateOnly? FiledOn,
    string? AckNo
);

public record ClientPortalDashboardDto(
    ClientSummaryDto Client,
    int OpenRequestsCount,
    int PendingFilingsCount,
    decimal OutstandingFees,
    int ActiveDscCount
);
