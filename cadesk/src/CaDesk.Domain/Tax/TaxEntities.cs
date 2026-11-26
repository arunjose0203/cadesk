using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Tax;

public static class TaxpayerCategory
{
    public const string Individual = "individual";
    public const string SeniorCitizen = "senior_citizen";
    public const string SuperSenior = "super_senior";
    public const string Huf = "huf";
    public const string Firm = "firm";
    public const string Company = "company";
    public const string Trust = "trust";

    public static readonly string[] All = [Individual, SeniorCitizen, SuperSenior, Huf, Firm, Company, Trust];
}

public static class TaxRegime
{
    public const string New = "new";
    public const string Old = "old";

    public static readonly string[] All = [New, Old];
}

public static class ComputationStatus
{
    public const string Draft = "draft";
    public const string Computed = "computed";
    public const string Approved = "approved";
    public const string Filed = "filed";

    public static readonly string[] All = [Draft, Computed, Approved, Filed];
}

public class TaxComputation : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string AssessmentYear { get; set; } = "2025-26";
    public string FinancialYear { get; set; } = "2024-25";
    public string TaxpayerCategory { get; set; } = Tax.TaxpayerCategory.Individual;
    public string OptedRegime { get; set; } = TaxRegime.New;
    public string Status { get; set; } = ComputationStatus.Draft;

    // 5 Heads of Income
    public decimal GrossSalary { get; set; }
    public decimal SalaryDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal HousePropertyIncome { get; set; }
    public decimal BusinessIncome { get; set; }
    public decimal CapitalGainsStcg { get; set; }
    public decimal CapitalGainsLtcg { get; set; }
    public decimal OtherSourcesIncome { get; set; }

    // Aggregate & Deductions
    public decimal GrossTotalIncome { get; set; }
    public decimal ChapterViaDeductions { get; set; }
    public decimal TotalTaxableIncome { get; set; }

    // Tax Liability Calculation
    public decimal TaxOnIncome { get; set; }
    public decimal Rebate87A { get; set; }
    public decimal Surcharge { get; set; }
    public decimal Cess { get; set; }
    public decimal TotalTaxLiability { get; set; }

    // Prepaid Taxes & Net Outcome
    public decimal TdsTcsCredit { get; set; }
    public decimal AdvanceTaxPaid { get; set; }
    public decimal SelfAssessmentTax { get; set; }
    public decimal NetTaxPayableOrRefund { get; set; }

    // JSON details
    public string ComputationBreakdown { get; set; } = "{}";
    public string ComparisonData { get; set; } = "{}";

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Firm? Firm { get; set; }
    public Client? Client { get; set; }
    public User? CreatedByUser { get; set; }
    public User? UpdatedByUser { get; set; }
}
