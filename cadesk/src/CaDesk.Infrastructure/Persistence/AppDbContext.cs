using System.Linq.Expressions;
using System.Reflection;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Gst;
using CaDesk.Domain.Tds;
using CaDesk.Domain.Mca;
using CaDesk.Domain.Engagements;
using CaDesk.Domain.BankFeeds;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Dsc;
using CaDesk.Domain.Portal;
using CaDesk.Domain.Tax;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Documents;
using CaDesk.Domain.Tasks;
using CaDesk.Domain.Notifications;
using CaDesk.Domain.Reports;
using CaDesk.Domain.TaxNotices;
using CaDesk.Domain.Payroll;
using CaDesk.Domain.EInvoicing;
using CaDesk.Domain.FixedAssets;
using CaDesk.Domain.AdvanceTax;
using CaDesk.Domain.Gstr9;
using CaDesk.Domain.Presumptive;
using CaDesk.Domain.Caro;
using CaDesk.Domain.TaxAudit;
using CaDesk.Domain.Schedule3;
using CaDesk.Domain.BranchAudit;
using CaDesk.Domain.AuditCommittee;
using CaDesk.Domain.ForeignRemittance;
using CaDesk.Domain.RelatedParty;
using CaDesk.Domain.InternalAudit;
using CaDesk.Domain.AntiProfiteering;
using CaDesk.Domain.SecretarialAudit;
using CaDesk.Domain.TransferPricing;
using CaDesk.Domain.CostAudit;
using CaDesk.Domain.TrialBalance;
using CaDesk.Domain.Common;
using CaDesk.Domain.Configuration;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CaDesk.Infrastructure.Persistence;

/// <summary>
/// Lock 1 of 2 on tenant data: every ITenantOwned set is filtered to the current firm,
/// and new rows are stamped with it. Lock 2 is PostgreSQL row-level security
/// (see TenantConnectionInterceptor and 0001_initial.sql).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : DbContext(options), IAppDbContext
{
    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserMfa> UserMfa => Set<UserMfa>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<ClientGroup> ClientGroups => Set<ClientGroup>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<ClientNote> ClientNotes => Set<ClientNote>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<TaxIdType> TaxIdTypes => Set<TaxIdType>();
    public DbSet<ComplianceType> ComplianceTypes => Set<ComplianceType>();
    public DbSet<ComplianceExtension> ComplianceExtensions => Set<ComplianceExtension>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<ClientCompliance> ClientCompliances => Set<ClientCompliance>();
    public DbSet<Deadline> Deadlines => Set<Deadline>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentFolder> DocumentFolders => Set<DocumentFolder>();
    public DbSet<DocumentUploadLink> DocumentUploadLinks => Set<DocumentUploadLink>();
    public DbSet<TaskDocument> TaskDocuments => Set<TaskDocument>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DscRegister> DscRegisters => Set<DscRegister>();
    public DbSet<DscMovementLog> DscMovementLogs => Set<DscMovementLog>();
    public DbSet<ClientPortalUser> ClientPortalUsers => Set<ClientPortalUser>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ServiceRequestMessage> ServiceRequestMessages => Set<ServiceRequestMessage>();
    public DbSet<TaxComputation> TaxComputations => Set<TaxComputation>();
    public DbSet<AuditEngagement> AuditEngagements => Set<AuditEngagement>();
    public DbSet<AuditWorkingPaper> AuditWorkingPapers => Set<AuditWorkingPaper>();
    public DbSet<AuditEqrReview> AuditEqrReviews => Set<AuditEqrReview>();
    public DbSet<AuditReviewNote> AuditReviewNotes => Set<AuditReviewNote>();
    public DbSet<GstReconciliationBatch> GstReconciliationBatches => Set<GstReconciliationBatch>();
    public DbSet<GstReconciliationItem> GstReconciliationItems => Set<GstReconciliationItem>();
    public DbSet<TdsLowerDeductionCertificate> TdsLowerDeductionCertificates => Set<TdsLowerDeductionCertificate>();
    public DbSet<TdsChallan> TdsChallans => Set<TdsChallan>();
    public DbSet<TdsDeduction> TdsDeductions => Set<TdsDeduction>();
    public DbSet<CompanyMaster> Companies => Set<CompanyMaster>();
    public DbSet<DirectorDinRecord> DirectorDins => Set<DirectorDinRecord>();
    public DbSet<McaAnnualFiling> McaFilings => Set<McaAnnualFiling>();
    public DbSet<BoardResolution> BoardResolutions => Set<BoardResolution>();
    public DbSet<EngagementLetter> EngagementLetters => Set<EngagementLetter>();
    public DbSet<ClientKycRecord> ClientKycRecords => Set<ClientKycRecord>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankStatement> BankStatements => Set<BankStatement>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<BankReconciliationRule> BankReconciliationRules => Set<BankReconciliationRule>();
    public DbSet<PracticeTarget> PracticeTargets => Set<PracticeTarget>();
    public DbSet<TaxNotice> TaxNotices => Set<TaxNotice>();
    public DbSet<NoticeHearing> NoticeHearings => Set<NoticeHearing>();
    public DbSet<NoticeResponse> NoticeResponses => Set<NoticeResponse>();
    public DbSet<TaxDemand> TaxDemands => Set<TaxDemand>();
    public DbSet<PayrollEmployee> PayrollEmployees => Set<PayrollEmployee>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollPayslip> PayrollPayslips => Set<PayrollPayslip>();
    public DbSet<EInvoiceRecord> EInvoices => Set<EInvoiceRecord>();
    public DbSet<EWayBillRecord> EWayBills => Set<EWayBillRecord>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<AssetDepreciationEntry> AssetDepreciationEntries => Set<AssetDepreciationEntry>();
    public DbSet<AssetPhysicalVerification> AssetPhysicalVerifications => Set<AssetPhysicalVerification>();
    public DbSet<AdvanceTaxPlan> AdvanceTaxPlans => Set<AdvanceTaxPlan>();
    public DbSet<AdvanceTaxInstallment> AdvanceTaxInstallments => Set<AdvanceTaxInstallment>();
    public DbSet<TaxInterestComputation> TaxInterestComputations => Set<TaxInterestComputation>();
    public DbSet<Gstr9Return> Gstr9Returns => Set<Gstr9Return>();
    public DbSet<Gstr9TableEntry> Gstr9TableEntries => Set<Gstr9TableEntry>();
    public DbSet<Gstr9cReconciliation> Gstr9cReconciliations => Set<Gstr9cReconciliation>();
    public DbSet<Gstr9cDiscrepancy> Gstr9cDiscrepancies => Set<Gstr9cDiscrepancy>();
    public DbSet<PresumptiveTaxComputation> PresumptiveTaxComputations => Set<PresumptiveTaxComputation>();
    public DbSet<Presumptive44AeVehicle> Presumptive44AeVehicles => Set<Presumptive44AeVehicle>();
    public DbSet<CaroAssessment> CaroAssessments => Set<CaroAssessment>();
    public DbSet<CaroClauseResponse> CaroClauseResponses => Set<CaroClauseResponse>();
    public DbSet<Form3CdAssessment> Form3CdAssessments => Set<Form3CdAssessment>();
    public DbSet<Form3CdClauseResponse> Form3CdClauseResponses => Set<Form3CdClauseResponse>();
    public DbSet<Schedule3Assessment> Schedule3Assessments => Set<Schedule3Assessment>();
    public DbSet<Schedule3RatioItem> Schedule3RatioItems => Set<Schedule3RatioItem>();
    public DbSet<Schedule3AgeingItem> Schedule3AgeingItems => Set<Schedule3AgeingItem>();
    public DbSet<BranchAuditEngagement> BranchAuditEngagements => Set<BranchAuditEngagement>();
    public DbSet<BranchAuditOffice> BranchAuditOffices => Set<BranchAuditOffice>();
    public DbSet<AuditCommitteeEngagement> AuditCommitteeEngagements => Set<AuditCommitteeEngagement>();
    public DbSet<InternalControlDeficiency> InternalControlDeficiencies => Set<InternalControlDeficiency>();
    public DbSet<SignificantAuditMatter> SignificantAuditMatters => Set<SignificantAuditMatter>();
    public DbSet<UncorrectedMisstatement> UncorrectedMisstatements => Set<UncorrectedMisstatement>();
    public DbSet<ForeignRemittanceAssessment> ForeignRemittanceAssessments => Set<ForeignRemittanceAssessment>();
    public DbSet<ForeignRemittanceChecklistItem> ForeignRemittanceChecklists => Set<ForeignRemittanceChecklistItem>();
    public DbSet<RelatedPartyEntity> RelatedPartyEntities => Set<RelatedPartyEntity>();
    public DbSet<RelatedPartyTransaction> RelatedPartyTransactions => Set<RelatedPartyTransaction>();
    public DbSet<RelatedPartyOmnibusApproval> RelatedPartyOmnibusApprovals => Set<RelatedPartyOmnibusApproval>();
    public DbSet<InternalAuditEngagement> InternalAuditEngagements => Set<InternalAuditEngagement>();
    public DbSet<RcmControlItem> RcmControlItems => Set<RcmControlItem>();
    public DbSet<InternalAuditFinding> InternalAuditFindings => Set<InternalAuditFinding>();
    public DbSet<GstRateNotification> GstRateNotifications => Set<GstRateNotification>();
    public DbSet<AntiProfiteeringAssessment> AntiProfiteeringAssessments => Set<AntiProfiteeringAssessment>();
    public DbSet<SkuProfiteeringItem> SkuProfiteeringItems => Set<SkuProfiteeringItem>();
    public DbSet<ProfiteeringRefundRecord> ProfiteeringRefundRecords => Set<ProfiteeringRefundRecord>();
    public DbSet<SecretarialAuditEngagement> SecretarialAuditEngagements => Set<SecretarialAuditEngagement>();
    public DbSet<SecretarialChecklistItem> SecretarialChecklistItems => Set<SecretarialChecklistItem>();
    public DbSet<McaFormFilingAudit> McaFormFilingAudits => Set<McaFormFilingAudit>();
    public DbSet<BoardMeetingAuditRecord> BoardMeetingAuditRecords => Set<BoardMeetingAuditRecord>();
    public DbSet<TransferPricingStudy> TransferPricingStudies => Set<TransferPricingStudy>();
    public DbSet<AssociatedEnterprise> AssociatedEnterprises => Set<AssociatedEnterprise>();
    public DbSet<TransferPricingTransaction> TransferPricingTransactions => Set<TransferPricingTransaction>();
    public DbSet<ComparableBenchmark> ComparableBenchmarks => Set<ComparableBenchmark>();
    public DbSet<CostAuditEngagement> CostAuditEngagements => Set<CostAuditEngagement>();
    public DbSet<CostRecordHead> CostRecordHeads => Set<CostRecordHead>();
    public DbSet<ProductCostStatement> ProductCostStatements => Set<ProductCostStatement>();
    public DbSet<CostAuditReconciliationItem> CostAuditReconciliationItems => Set<CostAuditReconciliationItem>();
    public DbSet<TrialBalanceImport> TrialBalanceImports => Set<TrialBalanceImport>();
    public DbSet<TrialBalanceLedger> TrialBalanceLedgers => Set<TrialBalanceLedger>();
    public DbSet<ClientLedgerMapping> ClientLedgerMappings => Set<ClientLedgerMapping>();
    public DbSet<ConfigDefinition> ConfigDefinitions => Set<ConfigDefinition>();
    public DbSet<ConfigValue> ConfigValues => Set<ConfigValue>();
    public DbSet<ConfigChangeLog> ConfigChangeLog => Set<ConfigChangeLog>();
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Read by the query filters at query time. Guid.Empty = no firm = no rows.</summary>
    public Guid CurrentFirmId => tenant.HasTenant ? tenant.FirmId : Guid.Empty;

    /// <summary>
    /// timestamptz only accepts UTC. Dates posted as "2026-04-01" or built with new DateTime(...) arrive with
    /// Kind=Unspecified and would fail on save, so every DateTime is treated as UTC on the way in and out.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        c.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
        v => ToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? ToUtc(v.Value) : v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    private static DateTime ToUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
    };

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Firm>(e =>
        {
            e.ToTable("firms");
            e.Property(x => x.TaxIds).HasJsonbDictionary();
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.BaseCurrency).HasMaxLength(3).IsFixedLength();
            // A firm sees only its own row.
            e.HasQueryFilter(f => f.Id == CurrentFirmId);
        });

        b.Entity<Client>(e =>
        {
            e.ToTable("clients");
            e.Property(x => x.TaxIds).HasJsonbDictionary();
            e.Property(x => x.Custom).HasJsonbDictionary();
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.HasIndex(x => new { x.FirmId, x.Code }).IsUnique().HasDatabaseName("ux_clients_firm_code");
            e.HasIndex(x => new { x.FirmId, x.Name }).HasDatabaseName("ix_clients_firm_name");
        });

        b.Entity<ClientGroup>(e => e.ToTable("client_groups"));

        b.Entity<ClientContact>(e =>
        {
            e.ToTable("client_contacts");
            e.Property(x => x.Email).HasColumnType("citext");
        });

        b.Entity<ClientNote>(e => e.ToTable("client_notes"));

        b.Entity<CustomFieldDefinition>(e =>
        {
            e.ToTable("custom_field_definitions");
            e.Property(x => x.Options).HasJsonbList();
        });

        b.Entity<TaxIdType>(e =>
        {
            e.ToTable("tax_id_types");                // platform data: same rows for every firm
            e.HasKey(x => new { x.CountryCode, x.Code });
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
        });

        b.Entity<ComplianceType>(e =>
        {
            e.ToTable("compliance_types");             // platform data: same rows for every firm
            e.HasKey(x => x.Code);
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.PeriodOverrides).HasColumnType("jsonb");
            e.Property(x => x.AppliesWhen).HasColumnType("jsonb");
        });

        b.Entity<ComplianceExtension>(e =>
        {
            e.ToTable("compliance_extensions");
            e.HasKey(x => new { x.TypeCode, x.PeriodKey });
        });

        b.Entity<Holiday>(e =>
        {
            e.ToTable("holidays");
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            // public holidays are visible to every firm; a firm's own closed days only to that firm (RLS agrees)
            e.HasQueryFilter(h => h.FirmId == null || h.FirmId == CurrentFirmId);
        });

        b.Entity<ClientCompliance>(e => e.ToTable("client_compliances"));
        b.Entity<Deadline>(e => e.ToTable("deadlines"));

        // relationships declared so EF inserts a task before its steps and history in one save
        b.Entity<WorkTask>(e =>
        {
            e.ToTable("tasks");
            e.HasOne<Deadline>().WithMany().HasForeignKey(x => x.DeadlineId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TaskChecklistItem>(e =>
        {
            e.ToTable("task_checklist_items");
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TaskComment>(e =>
        {
            e.ToTable("task_comments");
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(x => x.Params).HasColumnType("jsonb");
        });
        b.Entity<NotificationTemplate>(e =>
        {
            e.ToTable("notification_templates");
            e.Property(x => x.WaParams).HasJsonbList();
            e.HasQueryFilter(t => t.FirmId == null || t.FirmId == CurrentFirmId);
        });

        b.Entity<Document>(e => e.ToTable("documents"));
        b.Entity<DocumentFolder>(e =>
        {
            e.ToTable("document_folders");             // platform data: same rows for every firm
            e.HasKey(x => x.Code);
        });
        b.Entity<DocumentUploadLink>(e => e.ToTable("document_upload_links"));
        b.Entity<TaskDocument>(e =>
        {
            e.ToTable("task_documents");
            e.HasKey(x => new { x.TaskId, x.DocumentId });
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TimeEntry>(e =>
        {
            e.ToTable("time_entries");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        b.Entity<Invoice>(e =>
        {
            e.ToTable("invoices");
            e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<InvoiceItem>(e =>
        {
            e.ToTable("invoice_items");
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
        });

        b.Entity<DscRegister>(e =>
        {
            e.ToTable("dsc_registers");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CustodianUser).WithMany().HasForeignKey(x => x.CustodianUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.MovementLogs).WithOne().HasForeignKey(x => x.DscId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DscMovementLog>(e =>
        {
            e.ToTable("dsc_movement_logs");
            e.HasOne(x => x.HandlerUser).WithMany().HasForeignKey(x => x.HandlerUserId);
        });

        b.Entity<ClientPortalUser>(e =>
        {
            e.ToTable("client_portal_users");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy);
        });

        b.Entity<ServiceRequest>(e =>
        {
            e.ToTable("service_requests");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PortalUser).WithMany().HasForeignKey(x => x.PortalUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Messages).WithOne(x => x.Request).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ServiceRequestMessage>(e =>
        {
            e.ToTable("service_request_messages");
        });

        b.Entity<TaxComputation>(e =>
        {
            e.ToTable("tax_computations");
            e.Property(x => x.ComputationBreakdown).HasColumnType("jsonb");
            e.Property(x => x.ComparisonData).HasColumnType("jsonb");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy);
            e.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedBy);
        });

        b.Entity<AuditEngagement>(e =>
        {
            e.ToTable("audit_engagements");
            e.HasOne(x => x.ArchivedByUser).WithMany().HasForeignKey(x => x.ArchivedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LeadPartner).WithMany().HasForeignKey(x => x.LeadPartnerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.WorkingPapers).WithOne(x => x.Engagement).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditWorkingPaper>(e =>
        {
            e.ToTable("audit_working_papers");
            e.Property(x => x.Attachments).HasColumnType("jsonb");
            e.HasOne(x => x.Preparer).WithMany().HasForeignKey(x => x.PreparerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AuditEqrReview>(e =>
        {
            e.ToTable("audit_eqr_reviews");
            e.Property(x => x.PreIssuanceChecklist).HasColumnType("jsonb");
            e.HasOne(x => x.Engagement).WithMany(x => x.EqrReviews).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EqrPartner).WithMany().HasForeignKey(x => x.EqrPartnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditReviewNote>(e =>
        {
            e.ToTable("audit_review_notes");
            e.HasOne(x => x.Engagement).WithMany(x => x.ReviewNotes).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WorkingPaper).WithMany(x => x.ReviewNotesList).HasForeignKey(x => x.WorkingPaperId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.RaisedByUser).WithMany().HasForeignKey(x => x.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ResponseByUser).WithMany().HasForeignKey(x => x.ResponseByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ClearedByUser).WithMany().HasForeignKey(x => x.ClearedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<GstReconciliationBatch>(e =>
        {
            e.ToTable("gst_reconciliation_batches");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Items).WithOne(x => x.Batch).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GstReconciliationItem>(e =>
        {
            e.ToTable("gst_reconciliation_items");
        });

        b.Entity<TdsLowerDeductionCertificate>(e =>
        {
            e.ToTable("tds_lower_deduction_certificates");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TdsChallan>(e =>
        {
            e.ToTable("tds_challans");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Deductions).WithOne(x => x.Challan).HasForeignKey(x => x.ChallanId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<TdsDeduction>(e =>
        {
            e.ToTable("tds_deductions");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Certificate).WithMany().HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<CompanyMaster>(e =>
        {
            e.ToTable("company_master");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Directors).WithOne(x => x.Company).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Filings).WithOne(x => x.Company).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Resolutions).WithOne(x => x.Company).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DirectorDinRecord>(e =>
        {
            e.ToTable("director_din_records");
            e.HasOne(x => x.Company).WithMany(x => x.Directors).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<McaAnnualFiling>(e =>
        {
            e.ToTable("mca_annual_filings");
            e.HasOne(x => x.Company).WithMany(x => x.Filings).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BoardResolution>(e =>
        {
            e.ToTable("board_resolutions");
            e.HasOne(x => x.Company).WithMany(x => x.Resolutions).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EngagementLetter>(e =>
        {
            e.ToTable("engagement_letters");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClientKycRecord>(e =>
        {
            e.ToTable("client_kyc_records");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BankAccount>(e =>
        {
            e.ToTable("bank_accounts");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Statements).WithOne(x => x.BankAccount).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Transactions).WithOne(x => x.BankAccount).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BankStatement>(e =>
        {
            e.ToTable("bank_statements");
            e.HasOne(x => x.BankAccount).WithMany(x => x.Statements).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Transactions).WithOne(x => x.Statement).HasForeignKey(x => x.StatementId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BankTransaction>(e =>
        {
            e.ToTable("bank_transactions");
            e.HasOne(x => x.Statement).WithMany(x => x.Transactions).HasForeignKey(x => x.StatementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.BankAccount).WithMany(x => x.Transactions).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BankReconciliationRule>(e =>
        {
            e.ToTable("bank_reconciliation_rules");
        });

        b.Entity<PracticeTarget>(e =>
        {
            e.ToTable("practice_targets");
            e.HasIndex(x => new { x.FirmId, x.FinancialYear }).IsUnique().HasDatabaseName("ux_practice_targets_firm_fy");
        });

        b.Entity<TaxNotice>(e =>
        {
            e.ToTable("tax_notices");
            e.Property(x => x.Authority).HasConversion<string>();
            e.Property(x => x.Category).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.DinStatus).HasConversion<string>();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Hearings).WithOne(x => x.Notice).HasForeignKey(x => x.NoticeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Responses).WithOne(x => x.Notice).HasForeignKey(x => x.NoticeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Demands).WithOne(x => x.Notice).HasForeignKey(x => x.NoticeId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<NoticeHearing>(e =>
        {
            e.ToTable("notice_hearings");
            e.Property(x => x.HearingType).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
        });

        b.Entity<NoticeResponse>(e =>
        {
            e.ToTable("notice_responses");
            e.Property(x => x.Status).HasConversion<string>();
        });

        b.Entity<TaxDemand>(e =>
        {
            e.ToTable("tax_demands");
            e.Property(x => x.AppealStatus).HasConversion<string>();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PayrollEmployee>(e =>
        {
            e.ToTable("payroll_employees");
            e.Property(x => x.TaxRegime).HasConversion<string>();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.EmployeeCode }).IsUnique().HasDatabaseName("ux_payroll_employees_client_code");
        });

        b.Entity<PayrollRun>(e =>
        {
            e.ToTable("payroll_runs");
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payslips).WithOne(x => x.PayrollRun).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.PayrollPeriod }).IsUnique().HasDatabaseName("ux_payroll_runs_client_period");
        });

        b.Entity<PayrollPayslip>(e =>
        {
            e.ToTable("payroll_payslips");
            e.HasOne(x => x.PayrollRun).WithMany(x => x.Payslips).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EInvoiceRecord>(e =>
        {
            e.ToTable("einvoice_records");
            e.Property(x => x.DocType).HasConversion<string>();
            e.Property(x => x.SupplyType).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.SupplierGstin, x.FinancialYear, x.DocType, x.DocNumber }).IsUnique().HasDatabaseName("ux_einvoice_records_supplier_doc");
        });

        b.Entity<EWayBillRecord>(e =>
        {
            e.ToTable("eway_bill_records");
            e.Property(x => x.SupplyType).HasConversion<string>();
            e.Property(x => x.SubSupplyType).HasConversion<string>();
            e.Property(x => x.TransportMode).HasConversion<string>();
            e.Property(x => x.VehicleType).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EInvoice).WithMany().HasForeignKey(x => x.EInvoiceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.EwbNumber }).IsUnique().HasDatabaseName("ux_eway_bill_records_ewb_num");
        });

        b.Entity<FixedAsset>(e =>
        {
            e.ToTable("fixed_assets");
            e.Property(x => x.AssetClass).HasConversion<string>();
            e.Property(x => x.CompanyDepreciationMethod).HasConversion<string>();
            e.Property(x => x.ShiftType).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.AssetTag }).IsUnique().HasDatabaseName("ux_fixed_assets_tag");
            e.HasMany(x => x.DepreciationEntries).WithOne(x => x.FixedAsset).HasForeignKey(x => x.FixedAssetId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.PhysicalVerifications).WithOne(x => x.FixedAsset).HasForeignKey(x => x.FixedAssetId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AssetDepreciationEntry>(e =>
        {
            e.ToTable("asset_depreciation_entries");
            e.HasIndex(x => new { x.FirmId, x.FixedAssetId, x.FinancialYear }).IsUnique().HasDatabaseName("ux_asset_depr_entries_asset_fy");
        });

        b.Entity<AssetPhysicalVerification>(e =>
        {
            e.ToTable("asset_physical_verifications");
            e.Property(x => x.Status).HasConversion<string>();
        });

        b.Entity<AdvanceTaxPlan>(e =>
        {
            e.ToTable("advance_tax_plans");
            e.Property(x => x.Regime).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).IsUnique().HasDatabaseName("ux_advance_tax_plans_client_fy");
            e.HasMany(x => x.Installments).WithOne(x => x.Plan).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.InterestComputations).WithOne(x => x.Plan).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AdvanceTaxInstallment>(e =>
        {
            e.ToTable("advance_tax_installments");
            e.Property(x => x.Interest234c).HasColumnName("interest_234c");
            e.HasIndex(x => new { x.FirmId, x.PlanId, x.InstallmentNumber }).IsUnique().HasDatabaseName("ux_advance_tax_installments");
        });

        b.Entity<TaxInterestComputation>(e =>
        {
            e.ToTable("tax_interest_computations");
            e.Property(x => x.Interest234a).HasColumnName("interest_234a");
            e.Property(x => x.Interest234b).HasColumnName("interest_234b");
            e.Property(x => x.Interest234cTotal).HasColumnName("interest_234c_total");
            e.Property(x => x.Is234bApplicable).HasColumnName("is_234b_applicable");
            e.Property(x => x.MonthsDelayed234a).HasColumnName("months_delayed_234a");
            e.Property(x => x.MonthsDelayed234b).HasColumnName("months_delayed_234b");
        });

        b.Entity<Gstr9Return>(e =>
        {
            e.ToTable("gstr9_returns");
            e.Property(x => x.ItcAsPer2a).HasColumnName("itc_as_per_2a");
            e.Property(x => x.ItcAvailed3b).HasColumnName("itc_availed_3b");
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).IsUnique().HasDatabaseName("ux_gstr9_returns_client_fy");
            e.HasMany(x => x.TableEntries).WithOne().HasForeignKey(x => x.Gstr9ReturnId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Reconciliation).WithOne().HasForeignKey<Gstr9cReconciliation>(x => x.Gstr9ReturnId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Gstr9TableEntry>(e =>
        {
            e.ToTable("gstr9_table_entries");
            e.Property(x => x.TableSection).HasConversion<string>();
            e.HasIndex(x => new { x.FirmId, x.Gstr9ReturnId }).HasDatabaseName("ix_gstr9_table_entries_return");
        });

        b.Entity<Gstr9cReconciliation>(e =>
        {
            e.ToTable("gstr9c_reconciliations");
            e.Property(x => x.CertificationStatus).HasConversion<string>();
            e.HasIndex(x => new { x.FirmId, x.Gstr9ReturnId }).IsUnique().HasDatabaseName("ux_gstr9c_returns");
            e.HasMany(x => x.Discrepancies).WithOne().HasForeignKey(x => x.Gstr9cId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Gstr9cDiscrepancy>(e =>
        {
            e.ToTable("gstr9c_discrepancies");
            e.Property(x => x.Category).HasConversion<string>();
            e.HasIndex(x => new { x.FirmId, x.Gstr9cId }).HasDatabaseName("ix_gstr9c_discrepancies_rec");
        });

        b.Entity<PresumptiveTaxComputation>(e =>
        {
            e.ToTable("presumptive_tax_computations");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Vehicles).WithOne(x => x.Computation).HasForeignKey(x => x.ComputationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_presumptive_computations_client_fy");
        });

        b.Entity<Presumptive44AeVehicle>(e =>
        {
            e.ToTable("presumptive_44ae_vehicles");
            e.HasOne(x => x.Computation).WithMany(x => x.Vehicles).HasForeignKey(x => x.ComputationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ComputationId }).HasDatabaseName("ix_presumptive_44ae_vehicles_computation");
        });

        b.Entity<CaroAssessment>(e =>
        {
            e.ToTable("caro_assessments");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Engagement).WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FinalizedByUser).WithMany().HasForeignKey(x => x.FinalizedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.ClauseResponses).WithOne(x => x.Assessment).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_caro_assessments_client_fy");
        });

        b.Entity<CaroClauseResponse>(e =>
        {
            e.ToTable("caro_clause_responses");
            e.HasOne(x => x.Assessment).WithMany(x => x.ClauseResponses).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VerifiedByUser).WithMany().HasForeignKey(x => x.VerifiedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.AssessmentId }).HasDatabaseName("ix_caro_clause_responses_assessment");
        });

        b.Entity<Form3CdAssessment>(e =>
        {
            e.ToTable("form3cd_assessments");
            e.Property(x => x.Section44AbClause).HasColumnName("section44ab_clause");
            e.Property(x => x.TotalDisallowanceSec40A3).HasColumnName("total_disallowance_sec40a3");
            e.Property(x => x.TotalDisallowanceSec43B).HasColumnName("total_disallowance_sec43b");
            e.Property(x => x.TotalDisallowanceSec43Bh).HasColumnName("total_disallowance_sec43bh");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuditEngagement).WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FinalizedByUser).WithMany().HasForeignKey(x => x.FinalizedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.ClauseResponses).WithOne(x => x.Assessment).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_form3cd_assessments_client_fy");
        });

        b.Entity<Form3CdClauseResponse>(e =>
        {
            e.ToTable("form3cd_clause_responses");
            e.HasOne(x => x.Assessment).WithMany(x => x.ClauseResponses).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VerifiedByUser).WithMany().HasForeignKey(x => x.VerifiedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.AssessmentId }).HasDatabaseName("ix_form3cd_clause_responses_assessment");
        });

        b.Entity<Schedule3Assessment>(e =>
        {
            e.ToTable("schedule3_assessments");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuditEngagement).WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FinalizedByUser).WithMany().HasForeignKey(x => x.FinalizedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Ratios).WithOne(x => x.Assessment).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.AgeingItems).WithOne(x => x.Assessment).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_schedule3_assessments_client_fy");
        });

        b.Entity<Schedule3RatioItem>(e =>
        {
            e.ToTable("schedule3_ratio_items");
            e.HasOne(x => x.Assessment).WithMany(x => x.Ratios).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.AssessmentId }).HasDatabaseName("ix_schedule3_ratio_items_assessment");
        });

        b.Entity<Schedule3AgeingItem>(e =>
        {
            e.ToTable("schedule3_ageing_items");
            e.Property(x => x.Less6MonthsOr1Year).HasColumnName("less_6_months_or_1_year");
            e.HasOne(x => x.Assessment).WithMany(x => x.AgeingItems).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.AssessmentId }).HasDatabaseName("ix_schedule3_ageing_items_assessment");
        });

        b.Entity<BranchAuditEngagement>(e =>
        {
            e.ToTable("branch_audit_engagements");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuditEngagement).WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FinalizedByUser).WithMany().HasForeignKey(x => x.FinalizedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Branches).WithOne(x => x.Engagement).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_branch_audit_engagements_client_fy");
        });

        b.Entity<BranchAuditOffice>(e =>
        {
            e.ToTable("branch_audit_offices");
            e.HasOne(x => x.Engagement).WithMany(x => x.Branches).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_branch_audit_offices_engagement");
            e.HasIndex(x => x.AccessTokenHash).HasDatabaseName("ix_branch_audit_offices_token");
        });

        b.Entity<AuditCommitteeEngagement>(e =>
        {
            e.ToTable("audit_committee_engagements");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AuditEngagement).WithMany().HasForeignKey(x => x.AuditEngagementId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.SignedOffByUser).WithMany().HasForeignKey(x => x.SignedOffByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Deficiencies).WithOne(x => x.Engagement).HasForeignKey(x => x.CommitteeEngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.SignificantMatters).WithOne(x => x.Engagement).HasForeignKey(x => x.CommitteeEngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.UncorrectedMisstatements).WithOne(x => x.Engagement).HasForeignKey(x => x.CommitteeEngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_audit_committee_engagements_client_fy");
        });

        b.Entity<InternalControlDeficiency>(e =>
        {
            e.ToTable("internal_control_deficiencies");
            e.HasOne(x => x.Engagement).WithMany(x => x.Deficiencies).HasForeignKey(x => x.CommitteeEngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.CommitteeEngagementId }).HasDatabaseName("ix_internal_control_deficiencies_engagement");
        });

        b.Entity<SignificantAuditMatter>(e =>
        {
            e.ToTable("significant_audit_matters");
            e.HasOne(x => x.Engagement).WithMany(x => x.SignificantMatters).HasForeignKey(x => x.CommitteeEngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.CommitteeEngagementId }).HasDatabaseName("ix_significant_audit_matters_engagement");
        });

        b.Entity<UncorrectedMisstatement>(e =>
        {
            e.ToTable("uncorrected_misstatements");
            e.HasOne(x => x.Engagement).WithMany(x => x.UncorrectedMisstatements).HasForeignKey(x => x.CommitteeEngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.CommitteeEngagementId }).HasDatabaseName("ix_uncorrected_misstatements_engagement");
        });

        b.Entity<ForeignRemittanceAssessment>(e =>
        {
            e.ToTable("foreign_remittance_assessments");
            e.Property(x => x.Form15CaPart).HasColumnName("form15ca_part");
            e.Property(x => x.Form15CaRoutingReason).HasColumnName("form15ca_routing_reason");
            e.Property(x => x.Generated15CaXmlPayload).HasColumnName("generated_15ca_xml_payload");
            e.Property(x => x.Generated15CbCertificateText).HasColumnName("generated_15cb_certificate_text");
            e.Property(x => x.HasForm10F).HasColumnName("has_form10f");
            e.Property(x => x.RequiresForm15Cb).HasColumnName("requires_form15cb");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_foreign_remittance_assessments_client_fy");
        });

        b.Entity<ForeignRemittanceChecklistItem>(e =>
        {
            e.ToTable("foreign_remittance_checklists");
            e.HasOne(x => x.Assessment).WithMany(x => x.Checklist).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.AssessmentId }).HasDatabaseName("ix_foreign_remittance_checklists_assessment");
        });

        b.Entity<RelatedPartyEntity>(e =>
        {
            e.ToTable("related_party_entities");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId }).HasDatabaseName("ix_related_party_entities_client");
        });

        b.Entity<RelatedPartyTransaction>(e =>
        {
            e.ToTable("related_party_transactions");
            e.Property(x => x.Section40A2bRisk).HasColumnName("section_40a2b_risk");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RelatedParty).WithMany(x => x.Transactions).HasForeignKey(x => x.RelatedPartyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_related_party_transactions_client_fy");
        });

        b.Entity<RelatedPartyOmnibusApproval>(e =>
        {
            e.ToTable("related_party_omnibus_approvals");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_related_party_omnibus_client_fy");
        });

        b.Entity<InternalAuditEngagement>(e =>
        {
            e.ToTable("internal_audit_engagements");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_internal_audit_engagements_client_fy");
        });

        b.Entity<RcmControlItem>(e =>
        {
            e.ToTable("rcm_control_items");
            e.HasOne(x => x.Engagement).WithMany(x => x.Controls).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_rcm_control_items_engagement");
        });

        b.Entity<InternalAuditFinding>(e =>
        {
            e.ToTable("internal_audit_findings");
            e.HasOne(x => x.Engagement).WithMany(x => x.Findings).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RcmControlItem>().WithMany().HasForeignKey(x => x.ControlItemId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_internal_audit_findings_engagement");
        });

        b.Entity<GstRateNotification>(e =>
        {
            e.ToTable("gst_rate_notifications");
            e.HasIndex(x => new { x.FirmId, x.HsnOrSacCode }).HasDatabaseName("ix_gst_rate_notifications_firm_hsn");
            e.HasIndex(x => new { x.FirmId, x.EffectiveDate }).HasDatabaseName("ix_gst_rate_notifications_firm_date");
        });

        b.Entity<AntiProfiteeringAssessment>(e =>
        {
            e.ToTable("anti_profiteering_assessments");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GstNotification).WithMany().HasForeignKey(x => x.GstNotificationId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_apa_firm_client_fy");
        });

        b.Entity<SkuProfiteeringItem>(e =>
        {
            e.ToTable("sku_profiteering_items");
            e.HasOne(x => x.Assessment).WithMany(x => x.SkuItems).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.AssessmentId }).HasDatabaseName("ix_sku_items_firm_assessment");
        });

        b.Entity<ProfiteeringRefundRecord>(e =>
        {
            e.ToTable("profiteering_refund_records");
            e.HasOne(x => x.Assessment).WithMany(x => x.RefundRecords).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.AssessmentId }).HasDatabaseName("ix_refund_records_firm_assessment");
        });

        b.Entity<SecretarialAuditEngagement>(e =>
        {
            e.ToTable("secretarial_audit_engagements");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_sa_engagements_firm_client_fy");
        });

        b.Entity<SecretarialChecklistItem>(e =>
        {
            e.ToTable("secretarial_checklist_items");
            e.HasOne(x => x.Engagement).WithMany(x => x.ChecklistItems).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_sa_checklist_firm_engagement");
        });

        b.Entity<McaFormFilingAudit>(e =>
        {
            e.ToTable("mca_form_filing_audits");
            e.HasOne(x => x.Engagement).WithMany(x => x.McaFilings).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_mca_filings_firm_engagement");
        });

        b.Entity<BoardMeetingAuditRecord>(e =>
        {
            e.ToTable("board_meeting_audit_records");
            e.HasOne(x => x.Engagement).WithMany(x => x.MeetingAudits).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_meeting_audits_firm_engagement");
        });

        b.Entity<TransferPricingStudy>(e =>
        {
            e.ToTable("transfer_pricing_studies");
            e.Property(x => x.Form3CebReportText).HasColumnName("form_3ceb_report_text");
            e.Property(x => x.Rule10DDocumentationMaintained).HasColumnName("rule_10d_documentation_maintained");
            e.Property(x => x.Rule10DDocumentationPath).HasColumnName("rule_10d_documentation_path");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_tp_studies_firm_client_fy");
        });

        b.Entity<AssociatedEnterprise>(e =>
        {
            e.ToTable("associated_enterprises");
            e.HasOne(x => x.Study).WithMany(x => x.AssociatedEnterprises).HasForeignKey(x => x.StudyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.StudyId }).HasDatabaseName("ix_ae_firm_study");
        });

        b.Entity<TransferPricingTransaction>(e =>
        {
            e.ToTable("transfer_pricing_transactions");
            e.HasOne(x => x.Study).WithMany(x => x.Transactions).HasForeignKey(x => x.StudyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AssociatedEnterprise).WithMany().HasForeignKey(x => x.AssociatedEnterpriseId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.StudyId }).HasDatabaseName("ix_tp_txs_firm_study");
        });

        b.Entity<ComparableBenchmark>(e =>
        {
            e.ToTable("comparable_benchmarks");
            e.HasOne(x => x.Study).WithMany(x => x.Comparables).HasForeignKey(x => x.StudyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.FirmId, x.StudyId }).HasDatabaseName("ix_comparables_firm_study");
        });

        b.Entity<CostAuditEngagement>(e =>
        {
            e.ToTable("cost_audit_engagements");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_cost_audit_engagements_firm_client_fy");
        });

        b.Entity<CostRecordHead>(e =>
        {
            e.ToTable("cost_record_heads");
            e.HasOne(x => x.Engagement).WithMany(x => x.RecordHeads).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_cost_record_heads_firm_engagement");
        });

        b.Entity<ProductCostStatement>(e =>
        {
            e.ToTable("product_cost_statements");
            e.HasOne(x => x.Engagement).WithMany(x => x.ProductStatements).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_product_cost_statements_firm_engagement");
        });

        b.Entity<CostAuditReconciliationItem>(e =>
        {
            e.ToTable("cost_audit_reconciliation_items");
            e.HasOne(x => x.Engagement).WithMany(x => x.ReconciliationItems).HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.EngagementId }).HasDatabaseName("ix_cost_audit_rec_items_firm_engagement");
        });

        b.Entity<TrialBalanceImport>(e =>
        {
            e.ToTable("trial_balance_imports");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Ledgers).WithOne(x => x.Import).HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.FinancialYear }).HasDatabaseName("ix_tb_imports_firm_client_fy");
        });

        b.Entity<TrialBalanceLedger>(e =>
        {
            e.ToTable("trial_balance_ledgers");
            e.HasOne(x => x.Import).WithMany(x => x.Ledgers).HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ImportId }).HasDatabaseName("ix_tb_ledgers_firm_import");
            e.HasIndex(x => new { x.FirmId, x.LedgerName }).HasDatabaseName("ix_tb_ledgers_name");
        });

        b.Entity<ClientLedgerMapping>(e =>
        {
            e.ToTable("client_ledger_mappings");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FirmId, x.ClientId, x.LedgerPattern }).IsUnique().HasDatabaseName("ux_client_ledger_mapping");
            e.HasIndex(x => new { x.FirmId, x.ClientId }).HasDatabaseName("ix_client_ledger_mappings_firm_client");
        });

        b.Entity<TaskTemplate>(e =>
        {
            e.ToTable("task_templates");
            e.Property(x => x.Items).HasJsonbStringList();
            e.HasQueryFilter(t => t.FirmId == null || t.FirmId == CurrentFirmId);
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Email).HasColumnType("citext");
            e.HasIndex(x => new { x.FirmId, x.Email }).IsUnique().HasDatabaseName("ux_users_firm_email");
            e.HasMany(x => x.UserRoles).WithOne().HasForeignKey(x => x.UserId);
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId);
        });

        b.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(x => new { x.RoleId, x.PermissionCode });
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });

        b.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasKey(x => x.Code);
        });

        b.Entity<RefreshToken>(e => e.ToTable("refresh_tokens"));

        b.Entity<UserMfa>(e =>
        {
            e.ToTable("user_mfa");
            e.HasKey(x => x.UserId);
        });

        b.Entity<Invitation>(e =>
        {
            e.ToTable("invitations");
            e.Property(x => x.Email).HasColumnType("citext");
        });

        b.Entity<ConfigDefinition>(e =>
        {
            e.ToTable("config_definitions");
            e.HasKey(x => x.Key);
            e.Property(x => x.JsonSchema).HasColumnType("jsonb");
            e.Property(x => x.DefaultValue).HasColumnType("jsonb");
            e.Property(x => x.Labels).HasColumnType("jsonb");
            e.Property(x => x.HelpText).HasColumnType("jsonb");
        });

        b.Entity<ConfigValue>(e =>
        {
            e.ToTable("config_values");
            e.Property(x => x.Value).HasColumnType("jsonb");
            // platform / country / plan rows (no firm) are visible to every firm; firm rows only to their firm
            e.HasQueryFilter(v => v.FirmId == null || v.FirmId == CurrentFirmId);
        });

        b.Entity<ConfigChangeLog>(e =>
        {
            e.ToTable("config_change_log");
            e.Property(x => x.OldValue).HasColumnType("jsonb");
            e.Property(x => x.NewValue).HasColumnType("jsonb");
            e.HasQueryFilter(c => c.FirmId == CurrentFirmId);
        });

        b.Entity<Secret>(e => e.ToTable("secrets"));

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");                  // partitioned by month in PostgreSQL; PK is (id, at)
            e.Property(x => x.OldValues).HasColumnType("jsonb");
            e.Property(x => x.NewValues).HasColumnType("jsonb");
            e.HasQueryFilter(a => a.FirmId == CurrentFirmId);
        });

        ApplyTenantFilters(b);
        ApplySnakeCaseNames(b);
        ApplyRowVersion(b);
    }

    // ---- tenant + soft-delete filters for every ITenantOwned entity ----

    private void ApplyTenantFilters(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            var method = typeof(ISoftDelete).IsAssignableFrom(et.ClrType)
                ? nameof(SetTenantSoftDeleteFilter)
                : nameof(SetTenantFilter);
            GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(et.ClrType)
                .Invoke(this, [b]);
        }
    }

    private void SetTenantFilter<T>(ModelBuilder b) where T : class, ITenantOwned =>
        b.Entity<T>().HasQueryFilter(e => e.FirmId == CurrentFirmId);

    private void SetTenantSoftDeleteFilter<T>(ModelBuilder b) where T : class, ITenantOwned, ISoftDelete =>
        b.Entity<T>().HasQueryFilter(e => e.FirmId == CurrentFirmId && e.DeletedAt == null);

    // ---- naming: snake_case columns to match the SQL migrations ----

    private static void ApplySnakeCaseNames(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes())
        {
            // Ids are made in C# (Guid.CreateVersion7), never by the database. Telling EF so means a new child
            // added only through a loaded parent's collection is INSERTed; otherwise EF sees a key value, assumes
            // an existing row, UPDATEs nothing and reports "changed by someone else".
            if (et.FindPrimaryKey()?.Properties is [{ Name: "Id" } idKey] && idKey.ClrType == typeof(Guid))
                idKey.ValueGenerated = ValueGenerated.Never;

            foreach (var p in et.GetProperties())
            {
                if (p.ClrType == typeof(uint) && p.Name == nameof(TenantEntity.Version)) continue; // xmin
                // an explicit HasColumnName wins: some SQL migrations split digits differently (interest_234c, form15ca_part)
                if (p.FindAnnotation(RelationalAnnotationNames.ColumnName) is not null) continue;
                p.SetColumnName(p.Name.ToSnakeCase());
            }
            foreach (var k in et.GetKeys()) k.SetName(k.GetName()?.ToSnakeCase());
            foreach (var i in et.GetIndexes()) i.SetDatabaseName(i.GetDatabaseName()?.ToSnakeCase());
        }
    }

    // ---- optimistic concurrency on PostgreSQL xmin ----

    private static void ApplyRowVersion(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes().Where(t => typeof(TenantEntity).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(et.ClrType).Property(nameof(TenantEntity.Version))
                .HasColumnName("xmin").HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }
    }

    // ---- stamping on save ----

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Stamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void Stamp()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Firm firm)
            {
                if (entry.State == EntityState.Added) firm.CreatedAt = now;
                else if (entry.State == EntityState.Modified) firm.UpdatedAt = now;
            }

            if (entry.Entity is ITenantOwned owned)
            {
                if (entry.State == EntityState.Added)
                    owned.FirmId = tenant.FirmId; // throws TenantNotSetException if no firm
                else if (entry.State is EntityState.Modified or EntityState.Deleted && owned.FirmId != CurrentFirmId)
                    throw new InvalidOperationException("Attempt to change another firm's data was blocked.");
            }

            if (entry.Entity is IAuditable a)
            {
                if (entry.State == EntityState.Added)
                {
                    a.CreatedAt = now;
                    a.CreatedBy = tenant.UserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    a.UpdatedAt = now;
                    a.UpdatedBy = tenant.UserId;
                }
            }
        }
    }
}
