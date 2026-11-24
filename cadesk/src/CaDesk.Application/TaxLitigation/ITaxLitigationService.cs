namespace CaDesk.Application.TaxLitigation;

public interface ITaxLitigationService
{
    // Appeals Master
    Task<List<TaxAppealDto>> GetAppealsAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<TaxAppealDetailDto?> GetAppealByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaxAppealDetailDto> SaveAppealAsync(CreateOrUpdateTaxAppealRequest req, CancellationToken ct = default);
    Task<bool> DeleteAppealAsync(Guid id, CancellationToken ct = default);

    // Grounds of Appeal
    Task<AppealGroundItemDto> SaveGroundAsync(SaveAppealGroundRequest req, CancellationToken ct = default);
    Task<bool> DeleteGroundAsync(Guid groundId, CancellationToken ct = default);

    // ITAT Rule 18 Paper Book Documents
    Task<List<PaperBookDocumentDto>> SavePaperBookDocumentAsync(SavePaperBookDocRequest req, CancellationToken ct = default);
    Task<List<PaperBookDocumentDto>> DeletePaperBookDocumentAsync(Guid docId, CancellationToken ct = default);

    // Judicial Authorities & Citations
    Task<AppealJudicialCitationDto> AddCitationAsync(AddJudicialCitationRequest req, CancellationToken ct = default);
    Task<bool> DeleteCitationAsync(Guid citationId, CancellationToken ct = default);

    // Certification & Dossier Compilation
    Task<TaxAppealDetailDto> CertifyAppealAsync(Guid appealId, CertifyAppealRequest req, CancellationToken ct = default);
    Task<string> GenerateAppealDossierHtmlAsync(Guid appealId, CancellationToken ct = default);

    // Fee Calculator
    FilingFeeResultDto CalculateFilingFee(CalculateFilingFeeRequest req);
}
