using CaDesk.Domain.Subscriptions;
using Xunit;

namespace CaDesk.UnitTests;

public class SubscriptionUnitTests
{
    [Fact]
    public void GetAllPlans_ReturnsStarterGrowthEnterpriseTiers()
    {
        var plans = SubscriptionEngine.GetAllPlans();

        Assert.Equal(3, plans.Count);
        Assert.Contains(plans, p => p.TierCode == SubscriptionPlanTier.Starter);
        Assert.Contains(plans, p => p.TierCode == SubscriptionPlanTier.Growth);
        Assert.Contains(plans, p => p.TierCode == SubscriptionPlanTier.Enterprise);
    }

    [Theory]
    [InlineData(SubscriptionPlanTier.Starter, 1499, 14999, 3, 100, 2)]
    [InlineData(SubscriptionPlanTier.Growth, 4999, 49999, 15, 500, -1)]
    [InlineData(SubscriptionPlanTier.Enterprise, 14999, 149999, -1, -1, -1)]
    public void GetPlan_ReturnsExpectedPricingAndLimits(
        string tier,
        decimal expectedMonthly,
        decimal expectedAnnual,
        int expectedUsers,
        int expectedClients,
        int expectedAudits)
    {
        var plan = SubscriptionEngine.GetPlan(tier);

        Assert.Equal(expectedMonthly, plan.MonthlyPrice);
        Assert.Equal(expectedAnnual, plan.AnnualPrice);
        Assert.Equal(expectedUsers, plan.UserLimit);
        Assert.Equal(expectedClients, plan.ClientLimit);
        Assert.Equal(expectedAudits, plan.ActiveAuditsLimit);
    }

    [Fact]
    public void GetPlan_FallbackToStarter_WhenUnknownTier()
    {
        var plan = SubscriptionEngine.GetPlan("NON_EXISTENT_TIER");
        Assert.Equal(SubscriptionPlanTier.Starter, plan.TierCode);
    }

    [Fact]
    public void CalculateGstBreakdown_IntraState_SplitsInto9PercentCgstAndSgst()
    {
        var (basePrice, cgst, sgst, igst, total) = SubscriptionEngine.CalculateGstBreakdown(4999m, isInterState: false);

        Assert.Equal(4999m, basePrice);
        Assert.Equal(449.91m, cgst);
        Assert.Equal(449.91m, sgst);
        Assert.Equal(0m, igst);
        Assert.Equal(5898.82m, total);
        Assert.Equal(total, basePrice + cgst + sgst);
    }

    [Fact]
    public void CalculateGstBreakdown_InterState_AppliesFull18PercentIgst()
    {
        var (basePrice, cgst, sgst, igst, total) = SubscriptionEngine.CalculateGstBreakdown(14999m, isInterState: true);

        Assert.Equal(14999m, basePrice);
        Assert.Equal(0m, cgst);
        Assert.Equal(0m, sgst);
        Assert.Equal(2699.82m, igst);
        Assert.Equal(17698.82m, total);
        Assert.Equal(total, basePrice + igst);
    }

    [Fact]
    public void CalculateGstBreakdown_AnnualGrowthPlan_CalculatesAccurateTaxes()
    {
        var (basePrice, cgst, sgst, igst, total) = SubscriptionEngine.CalculateGstBreakdown(49999m, isInterState: false);

        Assert.Equal(49999m, basePrice);
        Assert.Equal(4499.91m, cgst);
        Assert.Equal(4499.91m, sgst);
        Assert.Equal(58998.82m, total);
    }

    [Fact]
    public void GetTrialDaysRemaining_FutureDate_ReturnsCeiledDays()
    {
        var trialEnd = DateTime.UtcNow.AddDays(13.4);
        var remaining = SubscriptionEngine.GetTrialDaysRemaining(trialEnd);

        Assert.Equal(14, remaining);
    }

    [Fact]
    public void GetTrialDaysRemaining_PastDate_ReturnsZero()
    {
        var trialEnd = DateTime.UtcNow.AddDays(-2);
        var remaining = SubscriptionEngine.GetTrialDaysRemaining(trialEnd);

        Assert.Equal(0, remaining);
    }

    [Fact]
    public void PlanDefinition_GrowthIsPopular_HasKeyAuditMoatFeatures()
    {
        var plan = SubscriptionEngine.GrowthPlan;

        Assert.True(plan.IsPopular);
        Assert.Contains(plan.IncludedFeatures, f => f.Contains("CARO 2020"));
        Assert.Contains(plan.IncludedFeatures, f => f.Contains("Bank Statement Analyzer"));
        Assert.Contains(plan.IncludedFeatures, f => f.Contains("SA 505"));
        Assert.Contains(plan.IncludedFeatures, f => f.Contains("ICAI Independent Auditor's Report"));
    }

    [Fact]
    public void PlanDefinition_EnterpriseHasDossierAndTransferPricing()
    {
        var plan = SubscriptionEngine.EnterprisePlan;

        Assert.Contains(plan.IncludedFeatures, f => f.Contains("Form 3CEB"));
        Assert.Contains(plan.IncludedFeatures, f => f.Contains("Cost Audit & Form CRA-1"));
        Assert.Contains(plan.IncludedFeatures, f => f.Contains("SA 230 Digital Audit Dossier"));
        Assert.Contains(plan.IncludedFeatures, f => f.Contains("Custom Domain"));
    }
}
