namespace CaDesk.Application.Mca;

public interface IMcaComplianceService
{
    Task<McaComplianceSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CompanyMasterDto>> ListCompaniesAsync(
        Guid? clientId = null,
        string? status = null,
        CancellationToken ct = default);

    Task<CompanyMasterDto?> GetCompanyByIdAsync(Guid id, CancellationToken ct = default);

    Task<CompanyMasterDto> CreateCompanyAsync(CreateCompanyRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<DirectorDinDto>> ListDirectorsAsync(
        Guid? companyId = null,
        string? kycStatus = null,
        CancellationToken ct = default);

    Task<DirectorDinDto> CreateDirectorAsync(CreateDirectorRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<McaFilingDto>> ListFilingsAsync(
        Guid? companyId = null,
        string? fy = null,
        string? formType = null,
        string? status = null,
        CancellationToken ct = default);

    Task<McaFilingDto> CreateFilingAsync(CreateFilingRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<BoardResolutionDto>> ListResolutionsAsync(
        Guid? companyId = null,
        string? meetingType = null,
        CancellationToken ct = default);

    Task<BoardResolutionDto> CreateResolutionAsync(CreateResolutionRequest req, CancellationToken ct = default);

    McaLateFeeCalculationResult CalculateLateFee(CalculateMcaLateFeeRequest req);
}
