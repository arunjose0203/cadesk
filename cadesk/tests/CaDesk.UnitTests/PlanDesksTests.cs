using CaDesk.Domain.Subscriptions;

namespace CaDesk.UnitTests;

public class PlanDesksTests
{
    [Theory]
    [InlineData("STARTER", "clients", true)]
    [InlineData("STARTER", "gstr1-export", true)]
    [InlineData("STARTER", "taxaudit", true)]
    [InlineData("STARTER", "caro", false)]
    [InlineData("starter", "audit-workspace", false)]
    [InlineData("GROWTH", "caro", true)]
    [InlineData("GROWTH", "transferpricing", false)]
    [InlineData("ENTERPRISE", "transferpricing", true)]
    [InlineData("INTERNAL", "uae-tax", true)]
    [InlineData("DEMO", "ibc-cirp", true)]
    [InlineData(null, "caro", false)]
    [InlineData("PRO", "caro", false)]   // an unknown code is treated as Starter
    public void Plans_open_their_desks(string? tier, string desk, bool allowed) =>
        Assert.Equal(allowed, PlanDesks.Allows(tier, desk));

    [Fact]
    public void Unlisted_desks_are_in_every_plan() =>
        Assert.Equal(SubscriptionPlanTier.Starter, PlanDesks.RequiredTier("some-new-desk"));

    [Fact]
    public void Minus_one_means_unlimited()
    {
        Assert.Null(PlanDesks.Limit(-1));
        Assert.Equal(100, PlanDesks.Limit(SubscriptionEngine.StarterPlan.ClientLimit));
    }
}
