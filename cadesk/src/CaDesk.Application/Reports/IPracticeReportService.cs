namespace CaDesk.Application.Reports;

public interface IPracticeReportService
{
    Task<PracticeExecutiveSummaryDto> GetExecutiveSummaryAsync(string? fy, CancellationToken ct = default);
    Task<IReadOnlyList<ClientProfitabilityDto>> GetClientProfitabilityAsync(string? fy, CancellationToken ct = default);
    Task<IReadOnlyList<StaffUtilizationDto>> GetStaffUtilizationAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<ReceivablesAgingDto> GetReceivablesAgingAsync(CancellationToken ct = default);
    Task<ComplianceSlaReportDto> GetComplianceSlaReportAsync(string? fy, CancellationToken ct = default);
    Task<IReadOnlyList<RevenueBreakdownDto>> GetRevenueBreakdownAsync(string? fy, CancellationToken ct = default);
    Task<PracticeTargetDto?> GetPracticeTargetAsync(string fy, CancellationToken ct = default);
    Task<PracticeTargetDto> SetPracticeTargetAsync(SetPracticeTargetRequest req, CancellationToken ct = default);
}
