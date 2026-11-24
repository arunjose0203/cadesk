namespace CaDesk.Application.EsgCsr;

public interface IEsgCsrService
{
    // CSR Assessment Master
    Task<List<CsrAssessmentDto>> GetCsrAssessmentsAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<CsrAssessmentDetailDto?> GetCsrAssessmentByIdAsync(Guid id, CancellationToken ct = default);
    Task<CsrAssessmentDetailDto> SaveCsrAssessmentAsync(SaveCsrAssessmentRequest req, CancellationToken ct = default);
    Task<bool> DeleteCsrAssessmentAsync(Guid id, CancellationToken ct = default);

    // CSR Projects
    Task<CsrProjectItemDto> SaveCsrProjectAsync(SaveCsrProjectRequest req, CancellationToken ct = default);
    Task<bool> DeleteCsrProjectAsync(Guid projectId, CancellationToken ct = default);

    // SEBI BRSR Core Reports
    Task<List<BrsrCoreReportDto>> GetBrsrReportsAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<BrsrCoreReportDto?> GetBrsrReportByIdAsync(Guid id, CancellationToken ct = default);
    Task<BrsrCoreReportDto?> GetBrsrReportByClientAndYearAsync(Guid clientId, string financialYear, CancellationToken ct = default);
    Task<BrsrCoreReportDto> SaveBrsrReportAsync(SaveBrsrReportRequest req, CancellationToken ct = default);
    Task<bool> DeleteBrsrReportAsync(Guid id, CancellationToken ct = default);

    // Certification & Dossier
    Task<CsrAssessmentDetailDto> CertifyCsrAsync(Guid id, CertifyCsrRequest req, CancellationToken ct = default);
    Task<BrsrCoreReportDto> CertifyBrsrAsync(Guid id, CertifyBrsrRequest req, CancellationToken ct = default);
    Task<string> GenerateAssuranceDossierHtmlAsync(Guid brsrReportId, CancellationToken ct = default);

    // Obligation Calculation Helper
    CsrObligationCalculationDto CalculateCsrObligation(CalculateCsrObligationRequest req);
}
