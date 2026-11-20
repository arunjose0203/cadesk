using CaDesk.Domain.Audit;
using CaDesk.Domain.Gst;
using CaDesk.Domain.Tds;
using CaDesk.Domain.Mca;
using CaDesk.Domain.Engagements;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.Reports;
using CaDesk.Domain.TaxNotices;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Dsc;
using CaDesk.Domain.Portal;
using CaDesk.Domain.Tax;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Documents;
using CaDesk.Domain.Tasks;
using CaDesk.Domain.Notifications;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CaDesk.Application.Abstractions;

/// <summary>What use cases may touch. Every firm-owned set is already filtered to the current firm.</summary>
public interface IAppDbContext
{
    DbSet<Firm> Firms { get; }
    DbSet<Client> Clients { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserMfa> UserMfa { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<ClientGroup> ClientGroups { get; }
    DbSet<ClientContact> ClientContacts { get; }
    DbSet<ClientNote> ClientNotes { get; }
    DbSet<CustomFieldDefinition> CustomFieldDefinitions { get; }
    /// <summary>Platform data (not firm-owned): tax ID formats per country.</summary>
    DbSet<TaxIdType> TaxIdTypes { get; }
    DbSet<ComplianceType> ComplianceTypes { get; }
    DbSet<ComplianceExtension> ComplianceExtensions { get; }
    /// <summary>Public holidays (no firm) plus this firm's own closed days.</summary>
    DbSet<Holiday> Holidays { get; }
    DbSet<ClientCompliance> ClientCompliances { get; }
    DbSet<Deadline> Deadlines { get; }
    DbSet<WorkTask> Tasks { get; }
    DbSet<TaskChecklistItem> TaskChecklistItems { get; }
    DbSet<TaskComment> TaskComments { get; }
    /// <summary>Platform default checklists (no firm) plus this firm's own.</summary>
    DbSet<TaskTemplate> TaskTemplates { get; }
    DbSet<Notification> Notifications { get; }
    /// <summary>Platform default messages (no firm) plus this firm's own.</summary>
    DbSet<NotificationTemplate> NotificationTemplates { get; }
    DbSet<Document> Documents { get; }
    /// <summary>Platform data: the general folders (bank statements, notices ...).</summary>
    DbSet<DocumentFolder> DocumentFolders { get; }
    DbSet<DocumentUploadLink> DocumentUploadLinks { get; }
    DbSet<TaskDocument> TaskDocuments { get; }
    DbSet<TimeEntry> TimeEntries { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<DscRegister> DscRegisters { get; }
    DbSet<DscMovementLog> DscMovementLogs { get; }
    DbSet<ClientPortalUser> ClientPortalUsers { get; }
    DbSet<ServiceRequest> ServiceRequests { get; }
    DbSet<ServiceRequestMessage> ServiceRequestMessages { get; }
    DbSet<TaxComputation> TaxComputations { get; }
    DbSet<AuditEngagement> AuditEngagements { get; }
    DbSet<AuditWorkingPaper> AuditWorkingPapers { get; }
    DbSet<GstReconciliationBatch> GstReconciliationBatches { get; }
    DbSet<GstReconciliationItem> GstReconciliationItems { get; }
    DbSet<TdsLowerDeductionCertificate> TdsLowerDeductionCertificates { get; }
    DbSet<TdsChallan> TdsChallans { get; }
    DbSet<TdsDeduction> TdsDeductions { get; }
    DbSet<CompanyMaster> Companies { get; }
    DbSet<DirectorDinRecord> DirectorDins { get; }
    DbSet<McaAnnualFiling> McaFilings { get; }
    DbSet<BoardResolution> BoardResolutions { get; }
    DbSet<EngagementLetter> EngagementLetters { get; }
    DbSet<ClientKycRecord> ClientKycRecords { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<BankStatement> BankStatements { get; }
    DbSet<BankTransaction> BankTransactions { get; }
    DbSet<BankReconciliationRule> BankReconciliationRules { get; }
    DbSet<PracticeTarget> PracticeTargets { get; }
    DbSet<TaxNotice> TaxNotices { get; }
    DbSet<NoticeHearing> NoticeHearings { get; }
    DbSet<NoticeResponse> NoticeResponses { get; }
    DbSet<TaxDemand> TaxDemands { get; }
    /// <summary>For transactions and the few raw SQL calls (numbering, jsonb lookups). RLS still applies.</summary>
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
