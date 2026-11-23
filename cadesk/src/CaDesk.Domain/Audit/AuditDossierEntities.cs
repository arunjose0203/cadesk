using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Audit;

public static class LeadScheduleRef
{
    public const string A_CashAndBank = "A";
    public const string B_TradeReceivables = "B";
    public const string C_Inventories = "C";
    public const string D_LoansAndAdvances = "D";
    public const string E_PropertyPlantEquipment = "E";
    public const string F_NonCurrentInvestments = "F";
    public const string G_TradePayables = "G";
    public const string H_Borrowings = "H";
    public const string I_OtherLiabilitiesProvisions = "I";
    public const string J_ShareCapitalReserves = "J";
    public const string K_RevenueOperations = "K";
    public const string L_OtherIncome = "L";
    public const string M_CostMaterialsPurchases = "M";
    public const string N_EmployeeBenefits = "N";
    public const string O_FinanceCosts = "O";
    public const string P_DepreciationAmortization = "P";
    public const string Q_OtherOperatingExpenses = "Q";

    public static readonly (string Code, string Title, string Category)[] All =
    [
        (A_CashAndBank, "Cash & Cash Equivalents / Bank Balances (SA 505 / BRS)", "Asset"),
        (B_TradeReceivables, "Trade Receivables / Sundry Debtors (SA 505)", "Asset"),
        (C_Inventories, "Inventories & Work-in-Progress (SA 501 NRV)", "Asset"),
        (D_LoansAndAdvances, "Loans, Advances & Other Current Assets", "Asset"),
        (E_PropertyPlantEquipment, "Property, Plant & Equipment (Fixed Assets Block)", "Asset"),
        (F_NonCurrentInvestments, "Non-Current Investments & Financial Assets", "Asset"),
        (G_TradePayables, "Trade Payables & MSME Dues (Sec 43B(h))", "Liability"),
        (H_Borrowings, "Borrowings (Working Capital CC / Term Loans)", "Liability"),
        (I_OtherLiabilitiesProvisions, "Other Liabilities, Provisions & Statutory Dues", "Liability"),
        (J_ShareCapitalReserves, "Share Capital & Reserves and Surplus", "Equity"),
        (K_RevenueOperations, "Revenue from Operations (Turnover Cut-off)", "Income"),
        (L_OtherIncome, "Other Non-Operating Income", "Income"),
        (M_CostMaterialsPurchases, "Cost of Materials Consumed & Direct Purchases", "Expense"),
        (N_EmployeeBenefits, "Employee Benefit Expenses (PF, ESIC, Gratuity)", "Expense"),
        (O_FinanceCosts, "Finance Costs & Bank Borrowing Interest", "Expense"),
        (P_DepreciationAmortization, "Depreciation & Amortization (Companies Act Sch II)", "Expense"),
        (Q_OtherOperatingExpenses, "Other Operating, Administrative & Selling Expenses", "Expense")
    ];
}

public static class DossierType
{
    public const string CurrentAuditFile = "CAF";
    public const string PermanentAuditFile = "PAF";
    public const string FullPeerReviewDossier = "COMBINED_PEER_REVIEW";

    public static readonly string[] All = [CurrentAuditFile, PermanentAuditFile, FullPeerReviewDossier];
}

public static class DossierStatus
{
    public const string Draft = "draft";
    public const string Compiled = "compiled";
    public const string SealedSqc1 = "sealed_sqc1";

    public static readonly string[] All = [Draft, Compiled, SealedSqc1];
}

public class LeadSchedule : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public string LeadCode { get; set; } = LeadScheduleRef.A_CashAndBank;
    public string LeadTitle { get; set; } = "";
    public string ScheduleCategory { get; set; } = "Asset"; // Asset, Liability, Equity, Income, Expense
    public decimal UnadjustedBookBalance { get; set; }
    public decimal AuditAdjustmentsDebit { get; set; }
    public decimal AuditAdjustmentsCredit { get; set; }
    public decimal AuditedBalance { get; set; }
    public decimal PriorYearBalance { get; set; }
    public decimal VarianceAmount { get; set; }
    public decimal VariancePercentage { get; set; }
    public bool IsMaterialVariance { get; set; }
    public string? AuditProceduresSummary { get; set; }
    public string? ExceptionsNoted { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, InReview, Approved
    public Guid? SignedOffByUserId { get; set; }
    public DateTime? SignedOffAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public AuditEngagement? Engagement { get; set; }
    public User? SignedOffByUser { get; set; }
}

public class AuditDossier : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid EngagementId { get; set; }
    public string DossierType { get; set; } = Domain.Audit.DossierType.FullPeerReviewDossier;
    public int Version { get; set; } = 1;
    public string Status { get; set; } = DossierStatus.Draft;
    public string IntegritySha256 { get; set; } = "";
    public bool NfraInspectionReady { get; set; }
    public int TotalWorkingPapersIncluded { get; set; }
    public int LeadSchedulesCount { get; set; }
    public int TotalExceptionsNoted { get; set; }
    public int TotalOpenNotes { get; set; }
    public string? PartnerSignoffRemarks { get; set; }
    public string? DossierManifestJson { get; set; }

    public DateTime CompiledAt { get; set; } = DateTime.UtcNow;
    public Guid? CompiledBy { get; set; }
    public DateTime? SealedAt { get; set; }
    public Guid? SealedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Firm? Firm { get; set; }
    public AuditEngagement? Engagement { get; set; }
    public User? CompiledByUser { get; set; }
    public User? SealedByUser { get; set; }
}

public static class AuditDossierEngine
{
    public static List<(string IndexCode, string SectionTitle, string SaStandard, bool Mandatory)> GetStandardNfraIndex()
    {
        return
        [
            ("IDX-100", "Appointment, Engagement Terms & Independence Evaluation", "SA 210 / SA 220 / SQC 1", true),
            ("IDX-200", "Audit Planning, Risk Assessment & Materiality Thresholds", "SA 300 / SA 315 / SA 320", true),
            ("IDX-300", "Internal Financial Controls (IFC) & Fraud Risk Assessment", "SA 240 / Sec 143(3)(i)", true),
            ("IDX-400", "Lead Schedules (A-Q) & Substantive Testing Workpapers", "SA 500 / SA 530 / SA 501", true),
            ("IDX-500", "Substantive Analytical Procedures & Ratio Variances", "SA 520 / Schedule III", true),
            ("IDX-600", "External Confirmations (Banks, Debtors & Creditors)", "SA 505", true),
            ("IDX-700", "Companies (Auditor's Report) Order (CARO 2020) 21 Clauses", "Order under Sec 143(11)", true),
            ("IDX-800", "Subsequent Events, Going Concern & Management Representation Letter", "SA 560 / SA 570 / SA 580", true),
            ("IDX-900", "Engagement Quality Review (EQR) Clearance & Independent Auditor's Report", "SA 220 / SA 700 / SA 705", true)
        ];
    }

    public static string ComputeSha256Checksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public static decimal CalculateAuditedBalance(decimal unadjusted, decimal adjustmentsDebit, decimal adjustmentsCredit, string category)
    {
        // For Assets and Expenses, Debit increases balance, Credit decreases
        // For Liabilities, Equity, and Income, Credit increases balance, Debit decreases
        if (category is "Asset" or "Expense")
        {
            return unadjusted + adjustmentsDebit - adjustmentsCredit;
        }
        else
        {
            return unadjusted + adjustmentsCredit - adjustmentsDebit;
        }
    }

    public static (decimal Amount, decimal Percentage, bool IsMaterial) CalculateVariance(decimal current, decimal prior, decimal materialityThreshold)
    {
        var diff = current - prior;
        var pct = prior != 0 ? Math.Round((diff / Math.Abs(prior)) * 100m, 2) : 100m;
        var isMaterial = Math.Abs(diff) >= materialityThreshold || Math.Abs(pct) >= 20m;
        return (diff, pct, isMaterial);
    }
}
