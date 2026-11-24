using System.Text;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Billing;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.PartnerCompanion;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.PartnerCompanion;

public sealed class PartnerCompanionService(
    IAppDbContext db,
    ITenantContext tenant,
    TimeProvider clock
)
{
    private DateTime NowUtc => clock.GetUtcNow().UtcDateTime;

    public async Task<PartnerActionCenterResponse> GetActionCenterPulseAsync(Guid partnerUserId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(NowUtc);
        int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var startOfWeek = today.AddDays(-daysSinceMonday);
        var startOfMonth = new DateOnly(today.Year, today.Month, 1);

        // 1. Practice Pulse Metrics
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.FirmId == tenant.FirmId)
            .ToListAsync(ct);

        decimal todayCollections = payments.Where(p => p.PaymentDate == today).Sum(p => p.Amount);
        decimal monthCollections = payments.Where(p => p.PaymentDate >= startOfMonth).Sum(p => p.Amount);

        var timeEntries = await db.TimeEntries.AsNoTracking()
            .Where(t => t.FirmId == tenant.FirmId)
            .ToListAsync(ct);

        decimal unbilledWip = timeEntries.Where(t => t.Billable && !t.IsBilled).Sum(t => t.Hours * t.HourlyRate);
        decimal weekHours = timeEntries.Where(t => t.Date >= startOfWeek).Sum(t => t.Hours);

        int activeStaff = await db.Users.AsNoTracking()
            .Where(u => u.FirmId == tenant.FirmId && u.IsActive && u.DeletedAt == null)
            .CountAsync(ct);

        var openDeadlines = await db.Deadlines.AsNoTracking()
            .Where(d => d.FirmId == tenant.FirmId && (d.Status == DeadlineStatus.Pending || d.Status == DeadlineStatus.InProgress))
            .ToListAsync(ct);

        int criticalDeadlinesCount = openDeadlines.Count(d => d.DueDate <= today.AddDays(7));

        // 2. Pending Audits awaiting Partner Sign-off
        var clients = await db.Clients.AsNoTracking()
            .Where(c => c.FirmId == tenant.FirmId)
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var audits = await db.AuditEngagements.AsNoTracking()
            .Where(a => a.FirmId == tenant.FirmId && a.Status != EngagementStatus.Archived)
            .OrderByDescending(a => a.Status == EngagementStatus.PartnerSignoff ? 2 : a.Status == EngagementStatus.Review ? 1 : 0)
            .ThenBy(a => a.PlannedEndDate)
            .Take(15)
            .ToListAsync(ct);

        var leadSchedules = await db.LeadSchedules.AsNoTracking()
            .Where(ls => ls.FirmId == tenant.FirmId)
            .ToListAsync(ct);

        var pendingAudits = audits.Select(a =>
        {
            var schedules = leadSchedules.Where(ls => ls.EngagementId == a.Id).ToList();
            int totalSchedules = schedules.Count;
            int completedSchedules = schedules.Count(s => s.Status == "Approved" || s.SignedOffAt != null);
            string clientName = clients.GetValueOrDefault(a.ClientId, "Unknown Client");

            return new PendingAuditItemDto(
                a.Id,
                a.ClientId,
                clientName,
                a.FinancialYear,
                a.AuditType,
                a.OverallMateriality,
                a.AuditOpinion ?? "Unmodified",
                completedSchedules,
                totalSchedules,
                a.Status,
                a.PlannedEndDate
            );
        }).ToList();

        // 3. Pending Timesheets
        var users = await db.Users.AsNoTracking()
            .Where(u => u.FirmId == tenant.FirmId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var pendingTimesheets = timeEntries
            .Where(t => t.Billable && !t.IsBilled)
            .OrderByDescending(t => t.Date)
            .Take(20)
            .Select(t => new PendingTimesheetItemDto(
                t.Id,
                users.GetValueOrDefault(t.UserId, "Staff Member"),
                clients.GetValueOrDefault(t.ClientId, "Client Work"),
                t.Date,
                t.Hours,
                t.HourlyRate,
                t.Hours * t.HourlyRate,
                t.Description
            )).ToList();

        // 4. Pending Invoices (Draft status)
        var pendingInvoicesList = await db.Invoices.AsNoTracking()
            .Where(i => i.FirmId == tenant.FirmId && i.Status == InvoiceStatus.Draft)
            .OrderByDescending(i => i.IssueDate)
            .Take(15)
            .ToListAsync(ct);

        var pendingInvoices = pendingInvoicesList.Select(i => new PendingInvoiceItemDto(
            i.Id,
            i.InvoiceNumber,
            clients.GetValueOrDefault(i.ClientId, "Client"),
            i.IssueDate,
            i.DueDate,
            i.TotalAmount,
            i.Status
        )).ToList();

        // 5. Bank Forensic Anomalies
        var forensicAlertsList = await db.ForensicAnomalies.AsNoTracking()
            .Where(f => f.FirmId == tenant.FirmId && f.Status == ForensicAnomalyStatus.Flagged)
            .OrderByDescending(f => f.ExposureAmount)
            .Take(15)
            .ToListAsync(ct);

        var bankAccounts = await db.BankAccounts.AsNoTracking()
            .Where(b => b.FirmId == tenant.FirmId)
            .ToDictionaryAsync(b => b.Id, b => b.ClientId, ct);

        var forensicAlerts = forensicAlertsList.Select(f =>
        {
            string cName = "Corporate Client";
            if (bankAccounts.TryGetValue(f.BankAccountId, out var cId) && clients.TryGetValue(cId, out var name))
            {
                cName = name;
            }

            return new ForensicAlertItemDto(
                f.Id,
                cName,
                f.AnomalyType,
                f.RiskLevel,
                f.Amount,
                f.ExposureAmount,
                f.Counterparty,
                f.Narration,
                f.StatutorySection,
                f.Status,
                f.TransactionDate
            );
        }).ToList();

        // 6. Deadlines Countdown
        var deadlineItems = openDeadlines
            .Where(d => d.DueDate <= today.AddDays(14))
            .OrderBy(d => d.DueDate)
            .Take(15)
            .Select(d =>
            {
                int remaining = d.DueDate.DayNumber - today.DayNumber;
                string cName = clients.GetValueOrDefault(d.ClientId, "Client");
                string title = $"{d.TypeCode.ToUpperInvariant()} — {cName}";

                return new DeadlineCountdownItemDto(
                    d.Id,
                    title,
                    d.DueDate,
                    remaining,
                    d.TypeCode,
                    d.Status
                );
            }).ToList();

        // 7. Security Preferences
        var pref = await db.PartnerMobilePreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.FirmId == tenant.FirmId && p.UserId == partnerUserId, ct);

        var security = new PartnerMobileSecurityDto(
            HasQuickPin: !string.IsNullOrEmpty(pref?.QuickPinHash),
            BiometricEnabled: pref?.BiometricEnabled ?? false,
            AutoLockMinutes: pref?.AutoLockMinutes ?? 5
        );

        var pulse = new PartnerPulseSummaryDto(
            TodayCollections: todayCollections,
            MonthCollections: monthCollections,
            UnbilledWipAmount: unbilledWip,
            BillableHoursThisWeek: weekHours,
            ActiveStaffCount: activeStaff,
            CriticalDeadlinesCount: criticalDeadlinesCount,
            PendingAuditSignoffsCount: pendingAudits.Count(a => a.Status == EngagementStatus.PartnerSignoff || a.Status == EngagementStatus.Review),
            PendingTimesheetApprovalsCount: pendingTimesheets.Count,
            PendingInvoiceApprovalsCount: pendingInvoices.Count,
            UnresolvedForensicAlertsCount: forensicAlerts.Count
        );

        return new PartnerActionCenterResponse(
            Pulse: pulse,
            PendingAudits: pendingAudits,
            PendingTimesheets: pendingTimesheets,
            PendingInvoices: pendingInvoices,
            ForensicAlerts: forensicAlerts,
            Deadlines: deadlineItems,
            Security: security
        );
    }

    public async Task<bool> QuickSignAuditAsync(Guid auditId, Guid partnerUserId, QuickSignAuditRequest req, CancellationToken ct = default)
    {
        var audit = await db.AuditEngagements
            .FirstOrDefaultAsync(a => a.FirmId == tenant.FirmId && a.Id == auditId, ct);

        if (audit is null) return false;

        // 1. Stage Gate: allow only when in PartnerSignoff stage
        if (audit.Status != EngagementStatus.PartnerSignoff)
        {
            throw new InvalidOperationException($"Audit engagement is currently in '{audit.Status}' stage. Quick sign-off is only permitted when the audit has reached the PartnerSignoff stage.");
        }

        // 2. Quality review clearance check: open review notes & EQR clearance
        var openNotesCount = await db.AuditReviewNotes
            .CountAsync(n => n.FirmId == tenant.FirmId && n.EngagementId == auditId && (n.Status == ReviewNoteStatus.Open || n.Status == ReviewNoteStatus.Reopened), ct);

        var (allowed, reason) = AuditQualityEngine.CanSignOffAudit(openNotesCount, audit.EqrRequired, audit.EqrStatus);
        if (!allowed)
        {
            throw new InvalidOperationException(reason);
        }

        // 3. Validate UDIN format (ICAI 18-character alphanumeric identifier)
        if (!UdinValidator.IsValid(req.Udin))
        {
            throw new ArgumentException("Invalid ICAI UDIN format. Must be an 18-character alphanumeric identifier starting with a 2-digit fiscal issuance year.");
        }

        // 4. Save to dedicated fields, not free-text notes
        audit.Udin = req.Udin!.Trim().ToUpperInvariant();
        audit.PartnerMembershipNo = req.PartnerMembershipNo?.Trim();
        audit.PartnerSignoffRemarks = req.Remarks?.Trim();
        audit.LeadPartnerId = partnerUserId;
        audit.AuditReportDate = DateOnly.FromDateTime(NowUtc);
        audit.ArchivalDeadline = AuditQualityEngine.CalculateArchivalDeadline(audit.AuditReportDate.Value);
        audit.UpdatedAt = DateTime.UtcNow;
        audit.UpdatedBy = partnerUserId;

        // 5. Explicit audit-log entry
        db.AuditLogs.Add(new AuditLog
        {
            FirmId = tenant.FirmId,
            ActorId = partnerUserId,
            ActorType = AuditActor.User,
            Entity = "AuditEngagement",
            EntityId = audit.Id.ToString(),
            Action = "signoff",
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                udin = audit.Udin,
                membershipNo = audit.PartnerMembershipNo,
                remarks = audit.PartnerSignoffRemarks,
                status = audit.Status,
                reportDate = audit.AuditReportDate,
                archivalDeadline = audit.ArchivalDeadline
            }),
            At = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> QuickApproveTimesheetsAsync(List<Guid> timeEntryIds, Guid partnerUserId, CancellationToken ct = default)
    {
        if (timeEntryIds == null || timeEntryIds.Count == 0) return 0;

        var entries = await db.TimeEntries
            .Where(t => t.FirmId == tenant.FirmId && timeEntryIds.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var entry in entries)
        {
            entry.Billable = true;
        }

        await db.SaveChangesAsync(ct);
        return entries.Count;
    }

    public async Task<bool> QuickApproveInvoiceAsync(Guid invoiceId, Guid partnerUserId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.FirmId == tenant.FirmId && i.Id == invoiceId, ct);

        if (invoice is null) return false;

        invoice.Status = InvoiceStatus.Sent;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TriageForensicAlertAsync(Guid anomalyId, TriageForensicAlertRequest req, Guid partnerUserId, CancellationToken ct = default)
    {
        var anomaly = await db.ForensicAnomalies
            .FirstOrDefaultAsync(f => f.FirmId == tenant.FirmId && f.Id == anomalyId, ct);

        if (anomaly is null) return false;

        anomaly.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.AuditorNotes))
        {
            anomaly.AuditorNotes = req.AuditorNotes.Trim();
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetupQuickPinAsync(Guid userId, SetupQuickPinRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Pin) || req.Pin.Length < 4 || req.Pin.Length > 6 || !req.Pin.All(char.IsDigit))
        {
            throw new ArgumentException("Quick PIN must be 4 to 6 numeric digits.");
        }

        var (hash, salt) = PartnerMobilePreference.HashPin(req.Pin);

        var pref = await db.PartnerMobilePreferences
            .FirstOrDefaultAsync(p => p.FirmId == tenant.FirmId && p.UserId == userId, ct);

        if (pref is null)
        {
            pref = new PartnerMobilePreference
            {
                FirmId = tenant.FirmId,
                UserId = userId,
                QuickPinHash = hash,
                QuickPinSalt = salt,
                BiometricEnabled = req.BiometricEnabled,
                LastActiveAt = NowUtc
            };
            db.PartnerMobilePreferences.Add(pref);
        }
        else
        {
            pref.QuickPinHash = hash;
            pref.QuickPinSalt = salt;
            pref.BiometricEnabled = req.BiometricEnabled;
            pref.LastActiveAt = NowUtc;
            pref.UpdatedAt = NowUtc;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<QuickPinVerificationResult> VerifyQuickPinAsync(Guid userId, VerifyQuickPinRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Pin))
        {
            return new QuickPinVerificationResult(false, "PIN is required.");
        }

        var pref = await db.PartnerMobilePreferences
            .FirstOrDefaultAsync(p => p.FirmId == tenant.FirmId && p.UserId == userId, ct);

        if (pref is null || string.IsNullOrEmpty(pref.QuickPinHash) || string.IsNullOrEmpty(pref.QuickPinSalt))
        {
            return new QuickPinVerificationResult(false, "No Quick PIN configured for this partner.");
        }

        bool match = PartnerMobilePreference.VerifyPin(req.Pin, pref.QuickPinHash, pref.QuickPinSalt);
        if (match)
        {
            pref.LastActiveAt = NowUtc;
            await db.SaveChangesAsync(ct);
            return new QuickPinVerificationResult(true, "Authentication successful.");
        }

        return new QuickPinVerificationResult(false, "Invalid PIN entered.");
    }
}
