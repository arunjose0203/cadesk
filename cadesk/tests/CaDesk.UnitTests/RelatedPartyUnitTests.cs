using System;
using System.Collections.Generic;
using CaDesk.Domain.RelatedParty;
using Xunit;

namespace CaDesk.UnitTests;

public class RelatedPartyUnitTests
{
    [Fact]
    public void Transaction_OrdinaryCourseAndArmLength_BelowThreshold_RequiresNoShareholderApproval()
    {
        var tx = new RelatedPartyTransaction
        {
            TransactionType = "SaleOfGoods",
            TransactionAmount = 5000000m, // 50 Lakhs
            BenchmarkArmLengthPrice = 4900000m,
            IsOrdinaryCourseOfBusiness = true
        };

        decimal turnover = 100000000m; // 10 Cr (10% threshold = 1 Cr)
        decimal netWorth = 50000000m;

        RelatedPartyEngine.EvaluateTransaction(tx, turnover, netWorth);

        Assert.Equal(10000000m, tx.ThresholdLimitAmount);
        Assert.False(tx.ExceedsStatutoryThreshold);
        Assert.True(tx.IsArmLength);
        Assert.False(tx.RequiresShareholderApproval);
        Assert.False(tx.Section40A2bRisk);
    }

    [Fact]
    public void Transaction_ExceedingThreshold_OrdinaryCourseAndArmLength_ExemptUnder4thProviso()
    {
        var tx = new RelatedPartyTransaction
        {
            TransactionType = "SaleOfGoods",
            TransactionAmount = 15000000m, // 1.5 Cr (> 1 Cr threshold)
            BenchmarkArmLengthPrice = 15000000m,
            IsOrdinaryCourseOfBusiness = true
        };

        decimal turnover = 100000000m; // 10 Cr (10% threshold = 1 Cr)
        decimal netWorth = 50000000m;

        RelatedPartyEngine.EvaluateTransaction(tx, turnover, netWorth);

        Assert.True(tx.ExceedsStatutoryThreshold);
        Assert.True(tx.IsArmLength);
        // Under 4th proviso to Section 188(1), ordinary course of business + arm's length does NOT require shareholder approval
        Assert.False(tx.RequiresShareholderApproval);
    }

    [Fact]
    public void Transaction_ExceedingThreshold_NotAtArmLength_RequiresShareholderApproval()
    {
        var tx = new RelatedPartyTransaction
        {
            TransactionType = "PurchaseOfGoods",
            TransactionAmount = 18000000m, // 1.8 Cr (Overpaying related party)
            BenchmarkArmLengthPrice = 12000000m, // Market price 1.2 Cr (50% markup)
            IsOrdinaryCourseOfBusiness = true
        };

        decimal turnover = 100000000m; // 10 Cr (10% threshold = 1 Cr)
        decimal netWorth = 50000000m;

        RelatedPartyEngine.EvaluateTransaction(tx, turnover, netWorth);

        Assert.True(tx.ExceedsStatutoryThreshold);
        Assert.False(tx.IsArmLength);
        Assert.Equal(50.0m, tx.PriceVariancePercentage);
        Assert.True(tx.RequiresShareholderApproval); // Fails 4th proviso exemption
        Assert.True(tx.Section40A2bRisk); // Excessive payment under Section 40A(2)(b)
    }

    [Fact]
    public void PropertyTransaction_EvaluatedAgainstNetWorthThreshold()
    {
        var tx = new RelatedPartyTransaction
        {
            TransactionType = "SellingBuyingProperty",
            TransactionAmount = 8000000m, // 80 Lakhs
            BenchmarkArmLengthPrice = 8000000m,
            IsOrdinaryCourseOfBusiness = false // Property transactions are typically non-ordinary
        };

        decimal turnover = 200000000m; // 20 Cr
        decimal netWorth = 50000000m;   // 5 Cr (10% net worth = 50 Lakhs)

        RelatedPartyEngine.EvaluateTransaction(tx, turnover, netWorth);

        Assert.Equal(5000000m, tx.ThresholdLimitAmount); // 10% of 5 Cr
        Assert.True(tx.ExceedsStatutoryThreshold);
        Assert.True(tx.RequiresShareholderApproval); // Exceeds limit & not ordinary course
    }

    [Fact]
    public void OfficeOfProfit_RemunerationEvaluatedAgainst30LakhsLimit()
    {
        var tx = new RelatedPartyTransaction
        {
            TransactionType = "RemunerationOfficeOfProfit",
            TransactionAmount = 4200000m, // 42 Lakhs/year (> 30 Lakhs / 2.5L per month)
            BenchmarkArmLengthPrice = 3000000m,
            IsOrdinaryCourseOfBusiness = true
        };

        RelatedPartyEngine.EvaluateTransaction(tx, 500000000m, 100000000m);

        Assert.Equal(3000000m, tx.ThresholdLimitAmount);
        Assert.True(tx.ExceedsStatutoryThreshold);
        Assert.False(tx.IsArmLength);
        Assert.True(tx.RequiresShareholderApproval);
        Assert.True(tx.Section40A2bRisk);
    }

    [Fact]
    public void PriceVariance_Within3PercentTolerance_ConsideredArmLength()
    {
        var tx = new RelatedPartyTransaction
        {
            TransactionType = "AvailingOfServices",
            TransactionAmount = 1025000m, // 2.5% variance over benchmark
            BenchmarkArmLengthPrice = 1000000m,
            IsOrdinaryCourseOfBusiness = true
        };

        RelatedPartyEngine.EvaluateTransaction(tx, 100000000m, 50000000m);

        Assert.Equal(2.5m, tx.PriceVariancePercentage);
        Assert.True(tx.IsArmLength); // Within +/- 3% tolerance
        Assert.False(tx.Section40A2bRisk);
    }

    [Fact]
    public void Mbp4Register_GeneratesCompliantSection189Text()
    {
        var party = new RelatedPartyEntity
        {
            Name = "Apex Logistics Solutions LLP",
            RelationshipType = "EnterpriseControlledByKMP"
        };

        var txList = new List<RelatedPartyTransaction>
        {
            new()
            {
                RelatedParty = party,
                TransactionDate = new DateTime(2024, 7, 15),
                TransactionType = "AvailingOfServices",
                TransactionAmount = 3500000m,
                IsArmLength = true,
                AuditCommitteeApprovalStatus = "Approved"
            }
        };

        var mbp4 = RelatedPartyEngine.GenerateMbp4Register(txList, "Zenith Global Industries Ltd", "2024-25");

        Assert.Contains("FORM MBP - 4", mbp4);
        Assert.Contains("Zenith Global Industries Ltd", mbp4);
        Assert.Contains("Apex Logistics Solutions LLP", mbp4);
        Assert.Contains("3,500,000.00", mbp4);
        Assert.Contains("Section 189 of Companies Act, 2013", mbp4);
    }

    [Fact]
    public void As18DisclosureNote_GeneratesGroupedDisclosures()
    {
        var holding = new RelatedPartyEntity
        {
            Id = Guid.NewGuid(),
            Name = "Zenith Holding Corp Ltd",
            RelationshipType = "HoldingCompany",
            Pan = "AAACZ1111H",
            NatureOfInterest = "Ultimate Parent Company"
        };

        var subsidiary = new RelatedPartyEntity
        {
            Id = Guid.NewGuid(),
            Name = "Zenith Technologies Pvt Ltd",
            RelationshipType = "Subsidiary",
            Pan = "AAACZ2222S",
            NatureOfInterest = "100% Wholly Owned Subsidiary"
        };

        var txList = new List<RelatedPartyTransaction>
        {
            new()
            {
                RelatedParty = holding,
                TransactionType = "AvailingOfServices",
                TransactionAmount = 12000000m,
                OutstandingBalanceAtYearEnd = 1500000m,
                BalanceType = "Credit"
            },
            new()
            {
                RelatedParty = subsidiary,
                TransactionType = "SaleOfGoods",
                TransactionAmount = 45000000m,
                OutstandingBalanceAtYearEnd = 8000000m,
                BalanceType = "Debit"
            }
        };

        var note = RelatedPartyEngine.GenerateAs18IndAs24DisclosureNote(txList, "Zenith Global Industries Ltd", "2024-25");

        Assert.Contains("RELATED PARTY DISCLOSURES (AS 18 / Ind AS 24)", note);
        Assert.Contains("HoldingCompany", note);
        Assert.Contains("Zenith Holding Corp Ltd", note);
        Assert.Contains("Subsidiary", note);
        Assert.Contains("Zenith Technologies Pvt Ltd", note);
        Assert.Contains("45,000,000.00", note);
        Assert.Contains("12,000,000.00", note);
    }
}
