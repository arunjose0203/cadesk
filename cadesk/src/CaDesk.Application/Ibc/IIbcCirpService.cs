namespace CaDesk.Application.Ibc;

public interface IIbcCirpService
{
    // CIRP Case Master Registry
    Task<List<CirpCaseDto>> GetCasesAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<CirpCaseDetailDto?> GetCaseByIdAsync(Guid id, CancellationToken ct = default);
    Task<CirpCaseDetailDto> SaveCaseAsync(SaveCirpCaseRequest req, CancellationToken ct = default);
    Task<bool> DeleteCaseAsync(Guid id, CancellationToken ct = default);

    // Committee of Creditors (CoC)
    Task<CocMemberDto> SaveCocMemberAsync(SaveCocMemberRequest req, CancellationToken ct = default);
    Task<bool> DeleteCocMemberAsync(Guid memberId, CancellationToken ct = default);

    // CoC Voting Resolutions (51%, 66%, 90%)
    Task<CocResolutionItemDto> SaveResolutionAsync(SaveCocResolutionRequest req, CancellationToken ct = default);
    Task<bool> DeleteResolutionAsync(Guid resId, CancellationToken ct = default);

    // PUFE Forensic Avoidance Transactions (Sec 43, 45, 50, 66)
    Task<PufeTransactionItemDto> SavePufeTransactionAsync(SavePufeTransactionRequest req, CancellationToken ct = default);
    Task<bool> DeletePufeTransactionAsync(Guid pufeId, CancellationToken ct = default);

    // Asset Valuations (Regulation 35 Fair & Liquidation Value)
    Task<AssetValuationItemDto> SaveValuationAsync(SaveAssetValuationRequest req, CancellationToken ct = default);
    Task<bool> DeleteValuationAsync(Guid valId, CancellationToken ct = default);

    // Regulation 36 Information Memorandum (IM) HTML Generator
    Task<string> GenerateInformationMemorandumHtmlAsync(Guid caseId, CancellationToken ct = default);

    // Statutory CIRP Limitation Calculator
    CirpTimelineCalculationDto CalculateTimelines(CalculateCirpTimelineRequest req);
}
