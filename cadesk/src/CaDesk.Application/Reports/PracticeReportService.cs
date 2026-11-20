using CaDesk.Application.Abstractions;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Reports;

public class PracticeReportService : IPracticeReportService
{
    private readonly IAppDbContext _db;

    public PracticeReportService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PracticeExecutiveSummaryDto> GetExecutiveSummaryAsync(string? fy, CancellationToken ct = default)
    {
        var targetFy = string.IsNullOrWhiteSpace(fy) ? CurrentFinancialYear() : fy.Trim();
        var (startDate, endDate) = ParseFinancialYear(targetFy);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Invoices in FY
        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.IssueDate >= startDate && i.IssueDate <= endDate && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync(ct);

        var totalBilled = invoices.Sum(i => i.TotalAmount);
        var totalCollected = invoices.Sum(i => i.PaidAmount);
        var totalOutstanding = invoices.Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));
        var realizationPct = totalBilled > 0 ? Math.Round((totalCollected / totalBilled) * 100, 2) : 0m;

        // Timesheets in FY
        var timeEntries = await _db.TimeEntries
            .AsNoTracking()
            .Where(t => t.Date >= startDate && t.Date <= endDate)
            .ToListAsync(ct);

        var billableHours = timeEntries.Where(t => t.Billable).Sum(t => t.Hours);
        var nonBillableHours = timeEntries.Where(t => !t.Billable).Sum(t => t.Hours);
        var totalHours = billableHours + nonBillableHours;
        var utilizationPct = totalHours > 0 ? Math.Round((billableHours / totalHours) * 100, 2) : 0m;

        // Compliance deadlines in FY
        var deadlines = await _db.Deadlines
            .AsNoTracking()
            .Where(d => d.DueDate >= startDate && d.DueDate <= endDate)
            .ToListAsync(ct);

        var totalDeadlines = deadlines.Count;
        var filedOnTime = deadlines.Count(d => d.Status == DeadlineStatus.Filed && d.FiledOn.HasValue && d.FiledOn.Value <= d.DueDate);
        var filedLate = deadlines.Count(d => d.Status == DeadlineStatus.Filed && d.FiledOn.HasValue && d.FiledOn.Value > d.DueDate);
        var overdue = deadlines.Count(d => d.Status != DeadlineStatus.Filed && d.Status != DeadlineStatus.NotApplicable && d.DueDate < today);
        var slaPct = totalDeadlines > 0 ? Math.Round(((decimal)filedOnTime / totalDeadlines) * 100, 2) : 100m;

        // Clients
        var activeClients = await _db.Clients.AsNoTracking().CountAsync(c => c.IsActive, ct);
        var avgRevPerClient = activeClients > 0 ? Math.Round(totalBilled / activeClients, 2) : 0m;

        // Targets
        var targetEntity = await _db.PracticeTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FinancialYear == targetFy, ct);

        var targetDto = targetEntity is null ? null : ToDto(targetEntity);

        return new PracticeExecutiveSummaryDto(
            FinancialYear: targetFy,
            TotalBilledYtd: totalBilled,
            TotalCollectedYtd: totalCollected,
            TotalOutstandingReceivables: totalOutstanding,
            OverallRealizationRatePct: realizationPct,
            TotalBillableHoursLogged: billableHours,
            TotalNonBillableHoursLogged: nonBillableHours,
            StaffUtilizationPct: utilizationPct,
            TotalDeadlinesCount: totalDeadlines,
            FiledOnTimeCount: filedOnTime,
            FiledLateCount: filedLate,
            OverdueCount: overdue,
            SlaCompliancePct: slaPct,
            ActiveClientsCount: activeClients,
            AverageRevenuePerClient: avgRevPerClient,
            Targets: targetDto);
    }

    public async Task<IReadOnlyList<ClientProfitabilityDto>> GetClientProfitabilityAsync(string? fy, CancellationToken ct = default)
    {
        var targetFy = string.IsNullOrWhiteSpace(fy) ? CurrentFinancialYear() : fy.Trim();
        var (startDate, endDate) = ParseFinancialYear(targetFy);

        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.IssueDate >= startDate && i.IssueDate <= endDate && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync(ct);

        var timeEntries = await _db.TimeEntries
            .AsNoTracking()
            .Where(t => t.Date >= startDate && t.Date <= endDate)
            .ToListAsync(ct);

        var result = new List<ClientProfitabilityDto>();

        foreach (var c in clients)
        {
            var clientInvoices = invoices.Where(i => i.ClientId == c.Id).ToList();
            var clientTimes = timeEntries.Where(t => t.ClientId == c.Id).ToList();

            var invoiced = clientInvoices.Sum(i => i.TotalAmount);
            var paid = clientInvoices.Sum(i => i.PaidAmount);
            var balanceDue = clientInvoices.Sum(i => Math.Max(0m, i.TotalAmount - i.PaidAmount));
            var hours = clientTimes.Sum(t => t.Hours);

            var effectiveRate = hours > 0 ? Math.Round(invoiced / hours, 2) : invoiced;
            var rating = effectiveRate >= 2500m ? "HighMargin" : (effectiveRate >= 1200m ? "MediumMargin" : "AtRisk");

            result.Add(new ClientProfitabilityDto(
                ClientId: c.Id,
                ClientName: c.Name,
                EntityType: c.EntityType,
                TotalInvoiced: invoiced,
                TotalPaid: paid,
                BalanceDue: balanceDue,
                TotalHoursLogged: hours,
                EffectiveHourlyRate: effectiveRate,
                ProfitabilityRating: rating));
        }

        return result.OrderByDescending(r => r.TotalInvoiced).ThenByDescending(r => r.EffectiveHourlyRate).ToList();
    }

    public async Task<IReadOnlyList<StaffUtilizationDto>> GetStaffUtilizationAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = from ?? new DateOnly(today.Year, today.Month, 1);
        var endDate = to ?? today;

        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive)
            .ToListAsync(ct);

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(t => t.Date >= startDate && t.Date <= endDate)
            .ToListAsync(ct);

        var list = new List<StaffUtilizationDto>();

        foreach (var u in users)
        {
            var userEntries = entries.Where(e => e.UserId == u.Id).ToList();
            var billable = userEntries.Where(e => e.Billable).Sum(e => e.Hours);
            var nonBillable = userEntries.Where(e => !e.Billable).Sum(e => e.Hours);
            var total = billable + nonBillable;
            var utilPct = total > 0 ? Math.Round((billable / total) * 100, 2) : 0m;
            var billedValue = userEntries.Where(e => e.Billable).Sum(e => e.Hours * e.HourlyRate);

            var role = u.UserRoles.FirstOrDefault()?.Role.Name ?? "Staff";

            list.Add(new StaffUtilizationDto(
                UserId: u.Id,
                StaffName: u.Name,
                RoleName: role,
                BillableHours: billable,
                NonBillableHours: nonBillable,
                TotalHours: total,
                UtilizationPct: utilPct,
                BilledValueGenerated: billedValue));
        }

        return list.OrderByDescending(u => u.UtilizationPct).ThenByDescending(u => u.BillableHours).ToList();
    }

    public async Task<ReceivablesAgingDto> GetReceivablesAgingAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var openInvoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .Where(i => (i.TotalAmount - i.PaidAmount) > 0 && i.Status != InvoiceStatus.Cancelled)
            .OrderBy(i => i.DueDate)
            .ToListAsync(ct);

        decimal current0To30 = 0m;
        decimal bucket31To60 = 0m;
        decimal bucket61To90 = 0m;
        decimal bucketAbove90 = 0m;

        var agedList = new List<AgedInvoiceItemDto>();

        foreach (var inv in openInvoices)
        {
            var balanceDue = inv.TotalAmount - inv.PaidAmount;
            var ageDays = Math.Max(0, today.DayNumber - inv.DueDate.DayNumber);
            string bucket;

            if (ageDays <= 30)
            {
                bucket = "0-30 Days";
                current0To30 += balanceDue;
            }
            else if (ageDays <= 60)
            {
                bucket = "31-60 Days";
                bucket31To60 += balanceDue;
            }
            else if (ageDays <= 90)
            {
                bucket = "61-90 Days";
                bucket61To90 += balanceDue;
            }
            else
            {
                bucket = "> 90 Days (Overdue Alert)";
                bucketAbove90 += balanceDue;
            }

            agedList.Add(new AgedInvoiceItemDto(
                InvoiceId: inv.Id,
                InvoiceNumber: inv.InvoiceNumber,
                ClientId: inv.ClientId,
                ClientName: inv.Client?.Name ?? "Unknown Client",
                IssueDate: inv.IssueDate,
                DueDate: inv.DueDate,
                AgeDays: ageDays,
                TotalAmount: inv.TotalAmount,
                BalanceDue: balanceDue,
                AgingBucket: bucket));
        }

        var total = current0To30 + bucket31To60 + bucket61To90 + bucketAbove90;

        return new ReceivablesAgingDto(
            TotalReceivables: total,
            Current0To30Days: current0To30,
            Bucket31To60Days: bucket31To60,
            Bucket61To90Days: bucket61To90,
            BucketAbove90Days: bucketAbove90,
            AgedInvoices: agedList);
    }

    public async Task<ComplianceSlaReportDto> GetComplianceSlaReportAsync(string? fy, CancellationToken ct = default)
    {
        var targetFy = string.IsNullOrWhiteSpace(fy) ? CurrentFinancialYear() : fy.Trim();
        var (startDate, endDate) = ParseFinancialYear(targetFy);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var deadlines = await _db.Deadlines
            .AsNoTracking()
            .Where(d => d.DueDate >= startDate && d.DueDate <= endDate)
            .ToListAsync(ct);

        var categories = new Dictionary<string, (int Total, int OnTime, int Late, int Overdue)>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in deadlines)
        {
            var cat = ClassifyComplianceCategory(d.TypeCode);
            if (!categories.TryGetValue(cat, out var counts))
                counts = (0, 0, 0, 0);

            counts.Total++;

            if (d.Status == DeadlineStatus.Filed)
            {
                if (d.FiledOn.HasValue && d.FiledOn.Value <= d.DueDate)
                    counts.OnTime++;
                else
                    counts.Late++;
            }
            else if (d.Status != DeadlineStatus.NotApplicable && d.DueDate < today)
            {
                counts.Overdue++;
            }

            categories[cat] = counts;
        }

        var breakdown = new List<ComplianceTypeSlaDto>();
        int totalFilings = 0;
        int totalOnTime = 0;
        int totalLate = 0;
        int totalOverdue = 0;

        foreach (var (cat, counts) in categories.OrderBy(c => c.Key))
        {
            var sla = counts.Total > 0 ? Math.Round(((decimal)counts.OnTime / counts.Total) * 100, 2) : 100m;
            breakdown.Add(new ComplianceTypeSlaDto(
                Category: cat,
                TotalCount: counts.Total,
                FiledOnTime: counts.OnTime,
                FiledLate: counts.Late,
                Overdue: counts.Overdue,
                SlaPercentage: sla));

            totalFilings += counts.Total;
            totalOnTime += counts.OnTime;
            totalLate += counts.Late;
            totalOverdue += counts.Overdue;
        }

        var overallSla = totalFilings > 0 ? Math.Round(((decimal)totalOnTime / totalFilings) * 100, 2) : 100m;

        return new ComplianceSlaReportDto(
            FinancialYear: targetFy,
            TotalStatutoryFilings: totalFilings,
            FiledOnTime: totalOnTime,
            FiledLate: totalLate,
            CurrentlyOverdue: totalOverdue,
            OverallSlaPercentage: overallSla,
            CategoryBreakdown: breakdown);
    }

    public async Task<IReadOnlyList<RevenueBreakdownDto>> GetRevenueBreakdownAsync(string? fy, CancellationToken ct = default)
    {
        var targetFy = string.IsNullOrWhiteSpace(fy) ? CurrentFinancialYear() : fy.Trim();
        var (startDate, endDate) = ParseFinancialYear(targetFy);

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Where(i => i.IssueDate >= startDate && i.IssueDate <= endDate && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync(ct);

        var items = invoices.SelectMany(i => i.Items).ToList();
        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var cat = ClassifyServiceCategory(item.SacCode, item.Description);
            totals[cat] = totals.GetValueOrDefault(cat) + item.TotalAmount;
        }

        var totalRev = totals.Values.Sum();
        var list = new List<RevenueBreakdownDto>();

        foreach (var (cat, amt) in totals.OrderByDescending(t => t.Value))
        {
            var pct = totalRev > 0 ? Math.Round((amt / totalRev) * 100, 2) : 0m;
            list.Add(new RevenueBreakdownDto(cat, amt, pct));
        }

        return list;
    }

    public async Task<PracticeTargetDto?> GetPracticeTargetAsync(string fy, CancellationToken ct = default)
    {
        var entity = await _db.PracticeTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FinancialYear == fy.Trim(), ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<PracticeTargetDto> SetPracticeTargetAsync(SetPracticeTargetRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.FinancialYear))
            throw new ArgumentException("Financial year is required (e.g. 2026-27).");

        var existing = await _db.PracticeTargets
            .FirstOrDefaultAsync(t => t.FinancialYear == req.FinancialYear.Trim(), ct);

        if (existing is null)
        {
            existing = new PracticeTarget
            {
                Id = Guid.NewGuid(),
                FinancialYear = req.FinancialYear.Trim(),
                TargetAnnualRevenue = req.TargetAnnualRevenue,
                TargetBillableHours = req.TargetBillableHours,
                TargetRecoveryPercentage = req.TargetRecoveryPercentage,
                TargetComplianceSlaPct = req.TargetComplianceSlaPct
            };
            _db.PracticeTargets.Add(existing);
        }
        else
        {
            existing.TargetAnnualRevenue = req.TargetAnnualRevenue;
            existing.TargetBillableHours = req.TargetBillableHours;
            existing.TargetRecoveryPercentage = req.TargetRecoveryPercentage;
            existing.TargetComplianceSlaPct = req.TargetComplianceSlaPct;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    private static string ClassifyComplianceCategory(string typeCode)
    {
        var upper = typeCode.ToUpperInvariant();
        if (upper.StartsWith("GST") || upper.Contains("GSTR")) return "GST Returns";
        if (upper.StartsWith("TDS") || upper.Contains("26Q") || upper.Contains("27Q")) return "TDS & Withholding";
        if (upper.StartsWith("ITR") || upper.Contains("ADV_TAX") || upper.Contains("TAX_AUDIT")) return "Income Tax";
        if (upper.StartsWith("MCA") || upper.Contains("ROC") || upper.Contains("AOC") || upper.Contains("MGT")) return "MCA / ROC Governance";
        if (upper.Contains("AUDIT")) return "Statutory Audit";
        return "Other Statutory Filings";
    }

    private static string ClassifyServiceCategory(string? sacCode, string description)
    {
        var desc = description.ToUpperInvariant();
        if (sacCode == "998221" || desc.Contains("AUDIT")) return "Statutory & Tax Audit";
        if (sacCode == "998222" || desc.Contains("GST") || desc.Contains("GSTR")) return "GST Advisory & Filings";
        if (sacCode == "998223" || desc.Contains("INCOME TAX") || desc.Contains("ITR") || desc.Contains("TDS")) return "Direct Taxation & Advisory";
        if (sacCode == "998224" || desc.Contains("MCA") || desc.Contains("ROC") || desc.Contains("SECRETARIAL")) return "Corporate Law & Secretarial";
        if (desc.Contains("BOOKKEEPING") || desc.Contains("ACCOUNTING")) return "Accounting & Bookkeeping";
        return "Management & Other Advisory";
    }

    private static (DateOnly Start, DateOnly End) ParseFinancialYear(string fy)
    {
        var parts = fy.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out var startYear))
        {
            return (new DateOnly(startYear, 4, 1), new DateOnly(startYear + 1, 3, 31));
        }

        var today = DateTime.UtcNow;
        return (new DateOnly(today.Year, 4, 1), new DateOnly(today.Year + 1, 3, 31));
    }

    private static string CurrentFinancialYear()
    {
        var now = DateTime.UtcNow;
        int startYear = now.Month >= 4 ? now.Year : now.Year - 1;
        int endYear = (startYear + 1) % 100;
        return $"{startYear}-{endYear:D2}";
    }

    private static PracticeTargetDto ToDto(PracticeTarget t) => new(
        t.Id,
        t.FinancialYear,
        t.TargetAnnualRevenue,
        t.TargetBillableHours,
        t.TargetRecoveryPercentage,
        t.TargetComplianceSlaPct,
        t.CreatedAt,
        t.UpdatedAt);
}
