using CaDesk.Domain.Ibc;
using Xunit;

namespace CaDesk.UnitTests;

public class IbcCirpUnitTests
{
    [Fact]
    public void IbcCirpEngine_CalculateCirpTimelines_Initial180DaysActive()
    {
        var admission = new DateOnly(2026, 1, 1);
        var asOf = new DateOnly(2026, 3, 1); // 59 days elapsed

        var result = IbcCirpEngine.CalculateCirpTimelines(admission, asOf);

        Assert.Equal(new DateOnly(2026, 1, 15), result.ClaimsLastDate); // 14 days
        Assert.Equal(new DateOnly(2026, 6, 30), result.Initial180Days); // 180 days
        Assert.Equal(new DateOnly(2026, 9, 28), result.Extended270Days); // 270 days
        Assert.Equal(new DateOnly(2026, 11, 27), result.Final330Days); // 330 days
        Assert.Equal(59, result.DaysElapsed);
        Assert.Equal(121, result.DaysRemainingTo180);
        Assert.False(result.Is180DaysExpired);
        Assert.False(result.Is330DaysCapBreached);
        Assert.Contains("CIRP in progress", result.TimelineStatusAlert);
    }

    [Fact]
    public void IbcCirpEngine_CalculateCirpTimelines_180DaysExpired_RequiresExtension()
    {
        var admission = new DateOnly(2026, 1, 1);
        var asOf = new DateOnly(2026, 7, 15); // Past 180 days (June 30)

        var result = IbcCirpEngine.CalculateCirpTimelines(admission, asOf);

        Assert.True(result.Is180DaysExpired);
        Assert.False(result.Is330DaysCapBreached);
        Assert.Contains("Initial 180-day CIRP period expired", result.TimelineStatusAlert);
        Assert.Contains("CoC 66% vote u/s 12(2)", result.TimelineStatusAlert);
    }

    [Fact]
    public void IbcCirpEngine_CalculateCirpTimelines_330DaysCapBreached()
    {
        var admission = new DateOnly(2026, 1, 1);
        var asOf = new DateOnly(2026, 12, 10); // Past 330 days (Nov 27)

        var result = IbcCirpEngine.CalculateCirpTimelines(admission, asOf);

        Assert.True(result.Is180DaysExpired);
        Assert.True(result.Is330DaysCapBreached);
        Assert.Contains("CRITICAL: 330-day absolute statutory cap", result.TimelineStatusAlert);
        Assert.Contains("Mandatory liquidation u/s 33", result.TimelineStatusAlert);
    }

    [Fact]
    public void IbcCirpEngine_CalculateCocVotingShares_ProportionalAllocation()
    {
        var members = new List<CocMember>
        {
            new() { CreditorName = "State Bank of India", Category = CreditorCategory.SecuredFinancialCreditor, AdmittedClaimAmount = 60_00_00_000m },
            new() { CreditorName = "Punjab National Bank", Category = CreditorCategory.SecuredFinancialCreditor, AdmittedClaimAmount = 40_00_00_000m }
        };

        IbcCirpEngine.CalculateCocVotingShares(members);

        Assert.Equal(60.00m, members[0].VotingSharePct);
        Assert.Equal(40.00m, members[1].VotingSharePct);
        Assert.Equal(100.00m, members[0].VotingSharePct + members[1].VotingSharePct);
    }

    [Fact]
    public void IbcCirpEngine_CalculateCocVotingShares_DisqualifiesRelatedPartyUnderSection21_2()
    {
        var members = new List<CocMember>
        {
            new() { CreditorName = "Bank of Baroda", Category = CreditorCategory.SecuredFinancialCreditor, AdmittedClaimAmount = 70_00_00_000m, IsRelatedParty = false },
            new() { CreditorName = "Promoter Holding Co Ltd", Category = CreditorCategory.UnsecuredFinancialCreditor, AdmittedClaimAmount = 30_00_00_000m, IsRelatedParty = true }
        };

        IbcCirpEngine.CalculateCocVotingShares(members);

        // Related party must receive 0% voting share pursuant to Section 21(2) first proviso
        Assert.Equal(0m, members[1].VotingSharePct);
        // Bank of Baroda receives 100% of the voting share
        Assert.Equal(100.00m, members[0].VotingSharePct);
    }

    [Fact]
    public void IbcCirpEngine_CalculateCocVotingShares_OperationalCreditorsExcludedFromVoting()
    {
        var members = new List<CocMember>
        {
            new() { CreditorName = "HDFC Bank Ltd", Category = CreditorCategory.SecuredFinancialCreditor, AdmittedClaimAmount = 50_00_00_000m },
            new() { CreditorName = "Raw Material Suppliers Ltd", Category = CreditorCategory.OperationalCreditorVendors, AdmittedClaimAmount = 15_00_00_000m }
        };

        IbcCirpEngine.CalculateCocVotingShares(members);

        Assert.Equal(100.00m, members[0].VotingSharePct);
        Assert.Equal(0m, members[1].VotingSharePct);
    }

    [Fact]
    public void IbcCirpEngine_EvaluateResolution_66PctResolutionPlanApproval()
    {
        // Section 30(4) requires 66% CoC approval for Resolution Plan
        var approved = IbcCirpEngine.EvaluateResolution(66m, 66.50m);
        var rejected = IbcCirpEngine.EvaluateResolution(66m, 65.90m);

        Assert.Equal(VotingResolutionStatus.Approved, approved);
        Assert.Equal(VotingResolutionStatus.Rejected, rejected);
    }

    [Fact]
    public void IbcCirpEngine_EvaluateResolution_90PctSection12AWithdrawal()
    {
        // Section 12A requires 90% CoC voting share for withdrawal of CIRP
        var approved = IbcCirpEngine.EvaluateResolution(90m, 90.00m);
        var rejected = IbcCirpEngine.EvaluateResolution(90m, 89.95m);

        Assert.Equal(VotingResolutionStatus.Approved, approved);
        Assert.Equal(VotingResolutionStatus.Rejected, rejected);
    }

    [Fact]
    public void IbcCirpEngine_EvaluateResolution_51PctRoutineAdministrative()
    {
        // Section 21(8) simple majority of 51%
        var approved = IbcCirpEngine.EvaluateResolution(51m, 51.00m);
        var rejected = IbcCirpEngine.EvaluateResolution(51m, 50.50m);

        Assert.Equal(VotingResolutionStatus.Approved, approved);
        Assert.Equal(VotingResolutionStatus.Rejected, rejected);
    }

    [Fact]
    public void IbcCirpEngine_EvaluatePufeLookback_Section43Preferential_UnrelatedVsRelated()
    {
        var admission = new DateOnly(2026, 6, 1);

        // 8 months prior (within 1 year) -> Unrelated party is within lookback
        var tx8Mo = new DateOnly(2025, 10, 1);
        Assert.True(IbcCirpEngine.EvaluatePufeLookback(admission, tx8Mo, isRelatedParty: false, PufeType.PreferentialSec43));

        // 18 months prior (beyond 1 year, within 2 years)
        var tx18Mo = new DateOnly(2024, 12, 1);
        // For unrelated party: false (1-year lookback)
        Assert.False(IbcCirpEngine.EvaluatePufeLookback(admission, tx18Mo, isRelatedParty: false, PufeType.PreferentialSec43));
        // For related party: true (2-year lookback u/s 43(4)(a))
        Assert.True(IbcCirpEngine.EvaluatePufeLookback(admission, tx18Mo, isRelatedParty: true, PufeType.PreferentialSec43));
    }

    [Fact]
    public void IbcCirpEngine_EvaluatePufeLookback_Section50ExtortionateCredit_2Years()
    {
        var admission = new DateOnly(2026, 6, 1);

        // 1.5 years prior (within 2 years) -> true
        var tx18Mo = new DateOnly(2024, 12, 1);
        Assert.True(IbcCirpEngine.EvaluatePufeLookback(admission, tx18Mo, isRelatedParty: false, PufeType.ExtortionateCreditSec50));

        // 2.5 years prior (beyond 2 years) -> false
        var tx30Mo = new DateOnly(2023, 12, 1);
        Assert.False(IbcCirpEngine.EvaluatePufeLookback(admission, tx30Mo, isRelatedParty: false, PufeType.ExtortionateCreditSec50));
    }

    [Fact]
    public void IbcCirpEngine_EvaluatePufeLookback_Section66FraudulentTrading_NoLimitation()
    {
        var admission = new DateOnly(2026, 6, 1);
        // Even 4 years prior is actionable under Section 66 (no time bar)
        var tx4Years = new DateOnly(2022, 1, 1);

        Assert.True(IbcCirpEngine.EvaluatePufeLookback(admission, tx4Years, isRelatedParty: false, PufeType.FraudulentTradingSec66));
    }

    [Fact]
    public void IbcCirpEngine_ComputeValuation_AveragesAndThirdValuerThreshold()
    {
        // Case 1: Close estimates (less than 25% difference)
        var (avgFair1, avgLiq1, thirdValuer1) = IbcCirpEngine.ComputeValuation(
            v1Fair: 100m, v1Liq: 70m,
            v2Fair: 110m, v2Liq: 75m);

        Assert.Equal(105m, avgFair1);
        Assert.Equal(72.5m, avgLiq1);
        Assert.False(thirdValuer1);

        // Case 2: Divergent estimates (difference > 25% of higher value)
        // Fair values: 100 vs 60 -> (100 - 60)/100 = 40% > 25%
        var (avgFair2, avgLiq2, thirdValuer2) = IbcCirpEngine.ComputeValuation(
            v1Fair: 100m, v1Liq: 70m,
            v2Fair: 60m, v2Liq: 40m);

        Assert.Equal(80m, avgFair2);
        Assert.Equal(55m, avgLiq2);
        Assert.True(thirdValuer2);
    }

    [Fact]
    public void IbcCirpEngine_GenerateInformationMemorandumHtml()
    {
        var cirp = new CirpCase
        {
            CorporateDebtorName = "Apex Infrastructure Ltd",
            Cin = "L45200MH2008PLC183920",
            NcltBench = "NCLT Mumbai Bench-I",
            CourtOrderNumber = "CP(IB) No. 912/MB/2025",
            AdmissionDate = new DateOnly(2026, 1, 10),
            RpName = "CA Vikram Singhal, IP",
            RpIpRegNumber = "IBBI/IPA-001/IP-P00892/2018-2019/11492",
            TotalAdmittedFinancialDebt = 120m,
            TotalAdmittedOperationalDebt = 15m
        };

        var coc = new List<CocMember>
        {
            new() { CreditorName = "Union Bank of India", Category = CreditorCategory.SecuredFinancialCreditor, AdmittedClaimAmount = 72m, VotingSharePct = 60m, SecurityInterestDescription = "First charge on factory land" },
            new() { CreditorName = "Bank of India", Category = CreditorCategory.SecuredFinancialCreditor, AdmittedClaimAmount = 48m, VotingSharePct = 40m, SecurityInterestDescription = "First charge on plant and machinery" }
        };

        var valuations = new List<AssetValuationItem>
        {
            new() { AssetClass = AssetValuationClass.LandAndBuilding, AssetDescription = "50-acre industrial freehold land", Valuer1FairValue = 60m, Valuer1LiquidationValue = 45m, Valuer2FairValue = 64m, Valuer2LiquidationValue = 47m, AverageFairValue = 62m, AverageLiquidationValue = 46m }
        };

        var pufe = new List<PufeTransactionItem>
        {
            new() { PufeType = PufeType.PreferentialSec43, TransactionDate = new DateOnly(2025, 9, 15), CounterpartyName = "Related Vendor Pvt Ltd", IsRelatedParty = true, TransactionAmount = 4.5m, EstimatedAvoidanceRecoverableAmount = 4.5m, NcltApplicationFiled = true }
        };

        string html = IbcCirpEngine.GenerateInformationMemorandumHtml(cirp, coc, valuations, pufe);

        Assert.Contains("INFORMATION MEMORANDUM", html);
        Assert.Contains("Regulation 36 of the Insolvency and Bankruptcy Board of India", html);
        Assert.Contains("Apex Infrastructure Ltd", html);
        Assert.Contains("L45200MH2008PLC183920", html);
        Assert.Contains("NCLT Mumbai Bench-I", html);
        Assert.Contains("Union Bank of India", html);
        Assert.Contains("CA Vikram Singhal, IP", html);
        Assert.Contains("PreferentialSec43", html);
    }
}
