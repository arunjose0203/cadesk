namespace CaDesk.Application.Engagements;

public interface IEngagementService
{
    Task<EngagementKycSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<IReadOnlyList<EngagementLetterDto>> ListLettersAsync(
        Guid? clientId = null,
        string? type = null,
        string? status = null,
        CancellationToken ct = default);

    Task<EngagementLetterDto?> GetLetterByIdAsync(Guid id, CancellationToken ct = default);

    Task<EngagementLetterDto> CreateLetterAsync(CreateEngagementLetterRequest req, CancellationToken ct = default);

    Task<EngagementLetterDto> SendLetterAsync(Guid id, CancellationToken ct = default);

    Task<EngagementLetterDto> AcceptLetterAsync(Guid id, AcceptEngagementLetterRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<ClientKycRecordDto>> ListKycRecordsAsync(
        Guid? clientId = null,
        string? riskRating = null,
        CancellationToken ct = default);

    Task<ClientKycRecordDto?> GetKycRecordByClientIdAsync(Guid clientId, CancellationToken ct = default);

    Task<ClientKycRecordDto> SaveKycRecordAsync(SaveClientKycRequest req, CancellationToken ct = default);

    GeneratedEngagementTemplate GenerateTemplate(GenerateTemplateRequest req);
}
