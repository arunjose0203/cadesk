using CaDesk.Domain.Audit;
using CaDesk.Domain.Gst;
using CaDesk.Domain.Tds;
using CaDesk.Domain.Mca;
using CaDesk.Domain.Engagements;
using CaDesk.Domain.BankFeeds;
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
    DbSet<AuditEqrReview> AuditEqrReviews { get; }
    DbSet<AuditReviewNote> AuditReviewNotes { get; }
    DbSet<LeadSchedule> LeadSchedules { get; }
    DbSet<AuditDossier> AuditDossiers { get; }
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
    DbSet<ForensicAnomalyItem> ForensicAnomalies { get; }
    DbSet<PracticeTarget> PracticeTargets { get; }
    DbSet<TaxNotice> TaxNotices { get; }
    DbSet<NoticeHearing> NoticeHearings { get; }
    DbSet<NoticeResponse> NoticeResponses { get; }
    DbSet<TaxDemand> TaxDemands { get; }
    DbSet<PayrollEmployee> PayrollEmployees { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollPayslip> PayrollPayslips { get; }
    DbSet<EInvoiceRecord> EInvoices { get; }
    DbSet<EWayBillRecord> EWayBills { get; }
    DbSet<FixedAsset> FixedAssets { get; }
    DbSet<AssetDepreciationEntry> AssetDepreciationEntries { get; }
    DbSet<AssetPhysicalVerification> AssetPhysicalVerifications { get; }
    DbSet<AdvanceTaxPlan> AdvanceTaxPlans { get; }
    DbSet<AdvanceTaxInstallment> AdvanceTaxInstallments { get; }
    DbSet<TaxInterestComputation> TaxInterestComputations { get; }
    DbSet<Gstr9Return> Gstr9Returns { get; }
    DbSet<Gstr9TableEntry> Gstr9TableEntries { get; }
    DbSet<Gstr9cReconciliation> Gstr9cReconciliations { get; }
    DbSet<Gstr9cDiscrepancy> Gstr9cDiscrepancies { get; }
    DbSet<PresumptiveTaxComputation> PresumptiveTaxComputations { get; }
    DbSet<Presumptive44AeVehicle> Presumptive44AeVehicles { get; }
    DbSet<CaroAssessment> CaroAssessments { get; }
    DbSet<CaroClauseResponse> CaroClauseResponses { get; }
    DbSet<Form3CdAssessment> Form3CdAssessments { get; }
    DbSet<Form3CdClauseResponse> Form3CdClauseResponses { get; }
    DbSet<Schedule3Assessment> Schedule3Assessments { get; }
    DbSet<Schedule3RatioItem> Schedule3RatioItems { get; }
    DbSet<Schedule3AgeingItem> Schedule3AgeingItems { get; }
    DbSet<BranchAuditEngagement> BranchAuditEngagements { get; }
    DbSet<BranchAuditOffice> BranchAuditOffices { get; }
    DbSet<CaDesk.Domain.AuditCommittee.AuditCommitteeEngagement> AuditCommitteeEngagements { get; }
    DbSet<CaDesk.Domain.AuditCommittee.InternalControlDeficiency> InternalControlDeficiencies { get; }
    DbSet<CaDesk.Domain.AuditCommittee.SignificantAuditMatter> SignificantAuditMatters { get; }
    DbSet<CaDesk.Domain.AuditCommittee.UncorrectedMisstatement> UncorrectedMisstatements { get; }
    DbSet<CaDesk.Domain.ForeignRemittance.ForeignRemittanceAssessment> ForeignRemittanceAssessments { get; }
    DbSet<CaDesk.Domain.ForeignRemittance.ForeignRemittanceChecklistItem> ForeignRemittanceChecklists { get; }
    DbSet<CaDesk.Domain.RelatedParty.RelatedPartyEntity> RelatedPartyEntities { get; }
    DbSet<CaDesk.Domain.RelatedParty.RelatedPartyTransaction> RelatedPartyTransactions { get; }
    DbSet<CaDesk.Domain.RelatedParty.RelatedPartyOmnibusApproval> RelatedPartyOmnibusApprovals { get; }
    DbSet<CaDesk.Domain.InternalAudit.InternalAuditEngagement> InternalAuditEngagements { get; }
    DbSet<CaDesk.Domain.InternalAudit.RcmControlItem> RcmControlItems { get; }
    DbSet<CaDesk.Domain.InternalAudit.InternalAuditFinding> InternalAuditFindings { get; }
    DbSet<CaDesk.Domain.AntiProfiteering.GstRateNotification> GstRateNotifications { get; }
    DbSet<CaDesk.Domain.AntiProfiteering.AntiProfiteeringAssessment> AntiProfiteeringAssessments { get; }
    DbSet<CaDesk.Domain.AntiProfiteering.SkuProfiteeringItem> SkuProfiteeringItems { get; }
    DbSet<CaDesk.Domain.AntiProfiteering.ProfiteeringRefundRecord> ProfiteeringRefundRecords { get; }
    DbSet<CaDesk.Domain.SecretarialAudit.SecretarialAuditEngagement> SecretarialAuditEngagements { get; }
    DbSet<CaDesk.Domain.SecretarialAudit.SecretarialChecklistItem> SecretarialChecklistItems { get; }
    DbSet<CaDesk.Domain.SecretarialAudit.McaFormFilingAudit> McaFormFilingAudits { get; }
    DbSet<CaDesk.Domain.SecretarialAudit.BoardMeetingAuditRecord> BoardMeetingAuditRecords { get; }
    DbSet<CaDesk.Domain.TransferPricing.TransferPricingStudy> TransferPricingStudies { get; }
    DbSet<CaDesk.Domain.TransferPricing.AssociatedEnterprise> AssociatedEnterprises { get; }
    DbSet<CaDesk.Domain.TransferPricing.TransferPricingTransaction> TransferPricingTransactions { get; }
    DbSet<CaDesk.Domain.TransferPricing.ComparableBenchmark> ComparableBenchmarks { get; }
    DbSet<CaDesk.Domain.CostAudit.CostAuditEngagement> CostAuditEngagements { get; }
    DbSet<CaDesk.Domain.CostAudit.CostRecordHead> CostRecordHeads { get; }
    DbSet<CaDesk.Domain.CostAudit.ProductCostStatement> ProductCostStatements { get; }
    DbSet<CaDesk.Domain.CostAudit.CostAuditReconciliationItem> CostAuditReconciliationItems { get; }
    DbSet<CaDesk.Domain.TrialBalance.TrialBalanceImport> TrialBalanceImports { get; }
    DbSet<CaDesk.Domain.TrialBalance.TrialBalanceLedger> TrialBalanceLedgers { get; }
    DbSet<CaDesk.Domain.TrialBalance.ClientLedgerMapping> ClientLedgerMappings { get; }
    /// <summary>For transactions and the few raw SQL calls (numbering, jsonb lookups). RLS still applies.</summary>
    DatabaseFacade Database { get; }
    /// <summary>To discard a failed unit of work's unsaved changes (e.g. one desk of a trial balance sync).</summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
