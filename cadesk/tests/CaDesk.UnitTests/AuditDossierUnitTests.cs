using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.UnitTests;

public class AuditDossierUnitTests
{
    [Fact]
    public void LeadScheduleRef_ContainsAll17StandardIcaiCodes()
    {
        var allSchedules = LeadScheduleRef.All;

        Assert.Equal(17, allSchedules.Length);

        var codes = allSchedules.Select(s => s.Code).ToList();
        Assert.Contains(LeadScheduleRef.A_CashAndBank, codes);
        Assert.Contains(LeadScheduleRef.B_TradeReceivables, codes);
        Assert.Contains(LeadScheduleRef.C_Inventories, codes);
        Assert.Contains(LeadScheduleRef.D_LoansAndAdvances, codes);
        Assert.Contains(LeadScheduleRef.E_PropertyPlantEquipment, codes);
        Assert.Contains(LeadScheduleRef.F_NonCurrentInvestments, codes);
        Assert.Contains(LeadScheduleRef.G_TradePayables, codes);
        Assert.Contains(LeadScheduleRef.H_Borrowings, codes);
        Assert.Contains(LeadScheduleRef.I_OtherLiabilitiesProvisions, codes);
        Assert.Contains(LeadScheduleRef.J_ShareCapitalReserves, codes);
        Assert.Contains(LeadScheduleRef.K_RevenueOperations, codes);
        Assert.Contains(LeadScheduleRef.L_OtherIncome, codes);
        Assert.Contains(LeadScheduleRef.M_CostMaterialsPurchases, codes);
        Assert.Contains(LeadScheduleRef.N_EmployeeBenefits, codes);
        Assert.Contains(LeadScheduleRef.O_FinanceCosts, codes);
        Assert.Contains(LeadScheduleRef.P_DepreciationAmortization, codes);
        Assert.Contains(LeadScheduleRef.Q_OtherOperatingExpenses, codes);
    }

    [Fact]
    public void AuditDossierEngine_CalculateAuditedBalance_HandlesAssetVsLiabilityProperly()
    {
        // For Assets: 10,00,000 unadjusted + 50,000 Dr Adj - 20,000 Cr Adj = 10,30,000
        var assetBalance = AuditDossierEngine.CalculateAuditedBalance(1_000_000m, 50_000m, 20_000m, "Asset");
        Assert.Equal(1_030_000m, assetBalance);

        // For Liabilities: 25,00,000 unadjusted + 1,00,000 Cr Adj - 30,000 Dr Adj = 25,70,000
        var liabBalance = AuditDossierEngine.CalculateAuditedBalance(2_500_000m, 30_000m, 100_000m, "Liability");
        Assert.Equal(2_570_000m, liabBalance);

        // For Expenses: 5,00,000 unadjusted + 25,000 Dr Adj - 10,000 Cr Adj = 5,15,000
        var expBalance = AuditDossierEngine.CalculateAuditedBalance(500_000m, 25_000m, 10_000m, "Expense");
        Assert.Equal(515_000m, expBalance);

        // For Income: 50,00,000 unadjusted + 2,00,000 Cr Adj - 50,000 Dr Adj = 51,50,000
        var incBalance = AuditDossierEngine.CalculateAuditedBalance(5_000_000m, 50_000m, 200_000m, "Income");
        Assert.Equal(5_150_000m, incBalance);
    }

    [Fact]
    public void AuditDossierEngine_CalculateVariance_DetectsMaterialVariances()
    {
        const decimal materiality = 500_000m;

        // Current: 60,00,000, Prior: 50,00,000 -> Diff = 10,00,000 (> 5,00,000) -> Material
        var (diff1, pct1, isMat1) = AuditDossierEngine.CalculateVariance(6_000_000m, 5_000_000m, materiality);
        Assert.Equal(1_000_000m, diff1);
        Assert.Equal(20m, pct1);
        Assert.True(isMat1);

        // Current: 50,50,000, Prior: 50,00,000 -> Diff = 50,000 (< 5,00,000 & 1%) -> Non-material
        var (diff2, pct2, isMat2) = AuditDossierEngine.CalculateVariance(5_050_000m, 5_000_000m, materiality);
        Assert.Equal(50_000m, diff2);
        Assert.Equal(1m, pct2);
        Assert.False(isMat2);

        // Prior Year was 0: Current = 1,00,000 (< Materiality, but new line -> 100% variance -> Material)
        var (diff3, pct3, isMat3) = AuditDossierEngine.CalculateVariance(100_000m, 0m, materiality);
        Assert.Equal(100_000m, diff3);
        Assert.Equal(100m, pct3);
        Assert.True(isMat3);
    }

    [Fact]
    public void AuditDossierEngine_ComputeSha256Checksum_IsDeterministicAndTamperEvident()
    {
        const string payload1 = "Engagement-2025|Turnover-100000000|Materiality-1000000|WPs-12";
        const string payload2 = "Engagement-2025|Turnover-100000000|Materiality-1000000|WPs-12";
        const string modified = "Engagement-2025|Turnover-100000000|Materiality-1000000|WPs-13"; // altered 1 WP

        var hash1 = AuditDossierEngine.ComputeSha256Checksum(payload1);
        var hash2 = AuditDossierEngine.ComputeSha256Checksum(payload2);
        var hashModified = AuditDossierEngine.ComputeSha256Checksum(modified);

        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hashModified);
    }

    [Fact]
    public void AuditDossierEngine_GetStandardNfraIndex_HasMandatorySections()
    {
        var sections = AuditDossierEngine.GetStandardNfraIndex();

        Assert.Equal(9, sections.Count);

        var indexCodes = sections.Select(s => s.IndexCode).ToList();
        Assert.Contains("IDX-100", indexCodes); // Appointment & Independence
        Assert.Contains("IDX-200", indexCodes); // Planning & Materiality
        Assert.Contains("IDX-300", indexCodes); // Internal Controls & Fraud
        Assert.Contains("IDX-400", indexCodes); // Lead Schedules & Substantive WPs
        Assert.Contains("IDX-500", indexCodes); // Analytical Procedures & Ratio Variances
        Assert.Contains("IDX-600", indexCodes); // External Confirmations (SA 505)
        Assert.Contains("IDX-700", indexCodes); // CARO 2020 21 Clauses
        Assert.Contains("IDX-800", indexCodes); // Subsequent Events, Going Concern & MRL
        Assert.Contains("IDX-900", indexCodes); // EQR Review & Audit Report

        Assert.All(sections, s => Assert.True(s.Mandatory));
    }

    [Fact]
    public void AuditQualityEngine_CanSignOffAudit_EnforcesSqc1Rules()
    {
        // 1 open review note -> cannot sign off
        var (allowed1, reason1) = AuditQualityEngine.CanSignOffAudit(1, false, null);
        Assert.False(allowed1);
        Assert.Contains("review note(s) remain open", reason1);

        // 0 open notes, but EQR required and still in progress -> cannot sign off
        var (allowed2, reason2) = AuditQualityEngine.CanSignOffAudit(0, true, EqrReviewStatus.InProgress);
        Assert.False(allowed2);
        Assert.Contains("Engagement Quality Review (EQR) hot review clearance is mandatory", reason2);

        // 0 open notes, EQR clearance granted -> allowed
        var (allowed3, reason3) = AuditQualityEngine.CanSignOffAudit(0, true, EqrReviewStatus.ClearanceGranted);
        Assert.True(allowed3);
        Assert.Contains("Eligible for Partner Sign-off", reason3);
    }
}
