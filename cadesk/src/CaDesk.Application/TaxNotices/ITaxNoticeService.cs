using CaDesk.Domain.TaxNotices;

namespace CaDesk.Application.TaxNotices;

public interface ITaxNoticeService
{
    Task<List<TaxNoticeDto>> GetNoticesAsync(TaxAuthority? authority, NoticeStatus? status, Guid? clientId, CancellationToken ct = default);
    Task<TaxNoticeDto?> GetNoticeByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaxNoticeDto> CreateNoticeAsync(CreateTaxNoticeRequest req, CancellationToken ct = default);
    Task<TaxNoticeDto> UpdateNoticeAsync(Guid id, UpdateTaxNoticeRequest req, CancellationToken ct = default);
    Task<TaxNoticeDto> VerifyDinAsync(Guid noticeId, CancellationToken ct = default);

    // Hearings
    Task<List<NoticeHearingDto>> GetHearingsAsync(Guid? noticeId, HearingStatus? status, CancellationToken ct = default);
    Task<NoticeHearingDto> AddHearingAsync(Guid noticeId, CreateHearingRequest req, CancellationToken ct = default);
    Task<NoticeHearingDto> UpdateHearingStatusAsync(Guid hearingId, UpdateHearingStatusRequest req, CancellationToken ct = default);

    // Submissions & Responses
    Task<List<NoticeResponseDto>> GetResponsesAsync(Guid noticeId, CancellationToken ct = default);
    Task<NoticeResponseDto> DraftResponseAsync(Guid noticeId, DraftResponseRequest req, CancellationToken ct = default);
    Task<NoticeResponseDto> ApproveResponseAsync(Guid responseId, CancellationToken ct = default);
    Task<NoticeResponseDto> RecordSubmissionAsync(Guid responseId, RecordSubmissionRequest req, CancellationToken ct = default);

    // Demands & Appeals
    Task<List<TaxDemandDto>> GetDemandsAsync(Guid? clientId, bool? overdueOnly, CancellationToken ct = default);
    Task<TaxDemandDto> CreateDemandAsync(CreateTaxDemandRequest req, CancellationToken ct = default);
    Task<TaxDemandDto> RecordDemandPaymentAsync(Guid demandId, RecordDemandPaymentRequest req, CancellationToken ct = default);
    Task<TaxDemandDto> UpdateAppealStatusAsync(Guid demandId, UpdateAppealRequest req, CancellationToken ct = default);

    // Dashboard Summary
    Task<NoticeDashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);
}
