using CaDesk.Domain.TaxNotices;

namespace CaDesk.Application.TaxNotices;

public interface ITaxCopilotService
{
    Task<ParsedNoticeResultDto> ParseNoticeAsync(ParseNoticeRequest req, CancellationToken ct = default);
    Task<List<StatutoryPrecedentDto>> GetPrecedentsAsync(TaxAuthority? authority, PrecedentCategory? category, string? search, CancellationToken ct = default);
    Task<StatutoryPrecedentDto> CreateCustomPrecedentAsync(CreatePrecedentRequest req, CancellationToken ct = default);
    Task<TaxDefenseDraftDto> CreateDefenseDraftAsync(CreateDefenseDraftRequest req, CancellationToken ct = default);
    Task<TaxDefenseDraftDto?> GetDefenseDraftByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TaxDefenseDraftDto>> GetDefenseDraftsForNoticeAsync(Guid noticeId, CancellationToken ct = default);
    Task<TaxDefenseDraftDto> UpdateDefenseDraftAsync(Guid id, UpdateDefenseDraftRequest req, CancellationToken ct = default);
    Task<TaxDefenseDraftDto> ApproveDefenseDraftAsync(Guid id, ApproveDefenseDraftRequest req, CancellationToken ct = default);
    Task<string> ExportDefenseDraftHtmlAsync(Guid id, CancellationToken ct = default);
}
