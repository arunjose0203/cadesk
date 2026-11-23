using CaDesk.Domain.Audit;
using Xunit;

namespace CaDesk.UnitTests;

public class ExternalConfirmationUnitTests
{
    [Fact]
    public void ConfirmationDeskEngine_CalculateVariance_ReturnsZeroWhenBalancesMatch()
    {
        var variance = ConfirmationDeskEngine.CalculateVariance(100_000m, 100_000m);
        Assert.Equal(0m, variance);
    }

    [Fact]
    public void ConfirmationDeskEngine_CalculateVariance_ComputesAccurateDifference()
    {
        // Client books show 5,00,000, confirming party confirms 4,50,000 -> variance 50,000
        var variance = ConfirmationDeskEngine.CalculateVariance(500_000m, 450_000m);
        Assert.Equal(50_000m, variance);
    }

    [Fact]
    public void ConfirmationDeskEngine_EvaluateConfirmationResponse_TreatsSmallRoundingWithin1RupeeAsAgreed()
    {
        var status = ConfirmationDeskEngine.EvaluateConfirmationResponse(125_450.50m, 125_450.00m, null);
        Assert.Equal(ConfirmationStatus.ConfirmedAgreed, status);
    }

    [Fact]
    public void ConfirmationDeskEngine_EvaluateConfirmationResponse_FlagsDisputeWhenDifferencesExceedThreshold()
    {
        var status = ConfirmationDeskEngine.EvaluateConfirmationResponse(100_000m, 85_000m, ConfirmationDisputeReason.GoodsInTransit);
        Assert.Equal(ConfirmationStatus.Disputed, status);
    }

    [Fact]
    public void ConfirmationDeskEngine_GenerateAccessToken_ProducesValid64CharHexToken()
    {
        var token1 = ConfirmationDeskEngine.GenerateAccessToken();
        var token2 = ConfirmationDeskEngine.GenerateAccessToken();

        Assert.NotNull(token1);
        Assert.Equal(64, token1.Length);
        Assert.NotEqual(token1, token2);
        Assert.Matches("^[a-f0-9]{64}$", token1);
    }

    [Fact]
    public void ConfirmationDeskEngine_GenerateConfirmationLetterHtml_IncludesClientAndAuditorInformation()
    {
        var req = new ExternalConfirmationRequest
        {
            PartyName = "Bharat Steel Works Ltd",
            PartyContactEmail = "finance@bharatsteel.com",
            PartyPanGstin = "27AAACB1234F1Z5",
            PartyType = ConfirmationPartyType.TradeDebtor,
            ConfirmationType = ConfirmationType.PositiveStatedBalance,
            BookBalanceAmount = 750_000m,
            AsOfDate = new DateOnly(2025, 3, 31)
        };

        var html = ConfirmationDeskEngine.GenerateConfirmationLetterHtml(req, "Titan Infra Solutions Ltd", "K.S. Aiyar & Co, Chartered Accountants");

        Assert.Contains("Bharat Steel Works Ltd", html);
        Assert.Contains("Titan Infra Solutions Ltd", html);
        Assert.Contains("K.S. Aiyar & Co", html);
        Assert.Contains("Balance as per Client's Records", html);
        Assert.Contains(req.SecureAccessToken, html);
    }

    [Fact]
    public void ConfirmationDeskEngine_GenerateSa505SummaryHtml_RendersAllConfirmingPartiesAndKpis()
    {
        var campaign = new ConfirmationCampaign
        {
            Title = "FY 2024-25 Trade Debtors Circularization",
            AsOfDate = new DateOnly(2025, 3, 31),
            TotalRequestsCount = 2,
            ConfirmedAgreedCount = 1,
            DisputedCount = 1,
            PendingCount = 0,
            TotalBookBalanceAmount = 1_500_000m
        };

        var requests = new List<ExternalConfirmationRequest>
        {
            new()
            {
                PartyName = "Tata Consultancy Services",
                PartyType = ConfirmationPartyType.TradeDebtor,
                LeadScheduleRef = "B",
                BookBalanceAmount = 1_000_000m,
                ConfirmedBalanceAmount = 1_000_000m,
                VarianceAmount = 0m,
                Status = ConfirmationStatus.ConfirmedAgreed
            },
            new()
            {
                PartyName = "Infosys Ltd",
                PartyType = ConfirmationPartyType.TradeDebtor,
                LeadScheduleRef = "B",
                BookBalanceAmount = 500_000m,
                ConfirmedBalanceAmount = 450_000m,
                VarianceAmount = 50_000m,
                DisputeReason = ConfirmationDisputeReason.GoodsInTransit,
                AlternativeProceduresPerformed = "Subsequent payment verified in April 2025 bank statement",
                AuditorConclusion = ConfirmationAuditorConclusion.TimingDifferenceAcceptable,
                Status = ConfirmationStatus.Disputed
            }
        };

        var html = ConfirmationDeskEngine.GenerateSa505SummaryHtml(campaign, requests, "Client ABC Ltd", "Auditor XYZ & Co");

        Assert.Contains("Tata Consultancy Services", html);
        Assert.Contains("Infosys Ltd", html);
        Assert.Contains("GoodsInTransit", html);
        Assert.Contains("TimingDifferenceAcceptable", html);
        Assert.Contains("Subsequent payment verified", html);
        Assert.Contains("Total Book Balance", html);
    }
}
