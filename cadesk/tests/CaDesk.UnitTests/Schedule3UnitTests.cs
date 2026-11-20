using System;
using System.Collections.Generic;
using System.Linq;
using CaDesk.Domain.Schedule3;
using Xunit;

namespace CaDesk.UnitTests;

public class Schedule3UnitTests
{
    [Fact]
    public void Compute11Ratios_CalculatesCorrectValues_AndVarianceFlags()
    {
        var assessment = new Schedule3Assessment
        {
            FinancialYear = "2024-25",
            PrecedingFinancialYear = "2023-24",

            // Current Ratio
            CurrentAssetsCurrent = 20_000_000m,       // ₹2 Cr
            CurrentAssetsPreceding = 15_000_000m,     // ₹1.5 Cr
            CurrentLiabilitiesCurrent = 10_000_000m,  // ₹1 Cr -> CR CY = 2.00
            CurrentLiabilitiesPreceding = 10_000_000m,// ₹1 Cr -> CR PY = 1.50 (Variance +33.33% >= 25%)

            // Debt-Equity Ratio
            TotalDebtCurrent = 15_000_000m,           // ₹1.5 Cr
            TotalDebtPreceding = 20_000_000m,         // ₹2 Cr
            ShareholdersEquityCurrent = 30_000_000m,  // ₹3 Cr -> DER CY = 0.50
            ShareholdersEquityPreceding = 25_000_000m,// ₹2.5 Cr -> DER PY = 0.80 (Variance -37.50% >= 25%)

            // Net Profit Ratio
            RevenueFromOperationsCurrent = 50_000_000m, // ₹5 Cr
            RevenueFromOperationsPreceding = 40_000_000m,// ₹4 Cr
            NetProfitAfterTaxesCurrent = 5_000_000m,    // ₹50L -> NPR CY = 10.00%
            NetProfitAfterTaxesPreceding = 4_000_000m,  // ₹40L -> NPR PY = 10.00% (Variance 0% < 25%)

            // Inventory Turnover Ratio
            CostOfGoodsSoldCurrent = 24_000_000m,
            CostOfGoodsSoldPreceding = 20_000_000m,
            AverageInventoryCurrent = 6_000_000m,     // ITR CY = 4.00
            AverageInventoryPreceding = 5_000_000m,   // ITR PY = 4.00 (Variance 0%)

            // Trade Receivables Turnover
            AverageTradeReceivablesCurrent = 10_000_000m,  // TRTR CY = 5.00
            AverageTradeReceivablesPreceding = 10_000_000m // TRTR PY = 4.00 (Variance +25.00% >= 25%)
        };

        var ratios = Schedule3Engine.Compute11Ratios(assessment);

        Assert.Equal(11, ratios.Count);

        // Check Current Ratio
        var cr = ratios.First(r => r.RatioCode == "R01");
        Assert.Equal(2.00m, cr.CurrentYearValue);
        Assert.Equal(1.50m, cr.PrecedingYearValue);
        Assert.Equal(33.33m, cr.VariancePercent);
        Assert.True(cr.IsVarianceSignificant);

        // Check Debt-Equity Ratio
        var der = ratios.First(r => r.RatioCode == "R02");
        Assert.Equal(0.50m, der.CurrentYearValue);
        Assert.Equal(0.80m, der.PrecedingYearValue);
        Assert.Equal(-37.50m, der.VariancePercent);
        Assert.True(der.IsVarianceSignificant);

        // Check Net Profit Ratio
        var npr = ratios.First(r => r.RatioCode == "R09");
        Assert.Equal(10.00m, npr.CurrentYearValue);
        Assert.Equal(10.00m, npr.PrecedingYearValue);
        Assert.Equal(0.00m, npr.VariancePercent);
        Assert.False(npr.IsVarianceSignificant);

        // Check Inventory Turnover Ratio
        var itr = ratios.First(r => r.RatioCode == "R05");
        Assert.Equal(4.00m, itr.CurrentYearValue);
        Assert.Equal(4.00m, itr.PrecedingYearValue);
        Assert.False(itr.IsVarianceSignificant);
    }

    [Fact]
    public void ValidateRoundingRule_Under100Cr_AllowsLakhsAndThousands()
    {
        decimal turnover = 45_00_00_000m; // ₹45 Crores (< 100 Cr)

        var evalLakhs = Schedule3Engine.ValidateRoundingRule(turnover, Schedule3RoundingUnit.Lakhs);
        Assert.True(evalLakhs.IsCompliant);

        var evalThousands = Schedule3Engine.ValidateRoundingRule(turnover, Schedule3RoundingUnit.Thousands);
        Assert.True(evalThousands.IsCompliant);
    }

    [Fact]
    public void ValidateRoundingRule_Above100Cr_DisallowsHundredsOrThousands()
    {
        decimal turnover = 150_00_00_000m; // ₹150 Crores (>= 100 Cr)

        // Thousands is disallowed for >= 100 Cr
        var evalThousands = Schedule3Engine.ValidateRoundingRule(turnover, Schedule3RoundingUnit.Thousands);
        Assert.False(evalThousands.IsCompliant);
        Assert.Contains("Non-compliant", evalThousands.Rationale);
        Assert.Contains("mandates rounding off in Lakhs, Millions, or Crores", evalThousands.Rationale);

        // Crores is permitted
        var evalCrores = Schedule3Engine.ValidateRoundingRule(turnover, Schedule3RoundingUnit.Crores);
        Assert.True(evalCrores.IsCompliant);

        // Lakhs is permitted
        var evalLakhs = Schedule3Engine.ValidateRoundingRule(turnover, Schedule3RoundingUnit.Lakhs);
        Assert.True(evalLakhs.IsCompliant);
    }

    [Fact]
    public void GenerateDisclosureNoteText_ContainsTableAndSignificantExplanations()
    {
        var assessment = new Schedule3Assessment
        {
            FinancialYear = "2024-25",
            PrecedingFinancialYear = "2023-24",
            AccountingStandard = Schedule3Division.DivisionI_AS,
            RoundingUnit = Schedule3RoundingUnit.Lakhs,
            TotalIncomeCurrent = 65_00_00_000m,
            IsRoundingRuleCompliant = true
        };

        var ratios = new List<Schedule3RatioItem>
        {
            new()
            {
                RatioCode = "R01",
                RatioName = Schedule3RatioNames.CurrentRatio,
                NumeratorDescription = "Current Assets",
                DenominatorDescription = "Current Liabilities",
                CurrentYearValue = 2.10m,
                PrecedingYearValue = 1.50m,
                VariancePercent = 40.00m,
                IsVarianceSignificant = true,
                VarianceExplanation = "Increase in current investments and inventory held for export orders."
            },
            new()
            {
                RatioCode = "R09",
                RatioName = Schedule3RatioNames.NetProfitRatio,
                NumeratorDescription = "Net Profit after Taxes",
                DenominatorDescription = "Revenue from Operations",
                CurrentYearValue = 12.50m,
                PrecedingYearValue = 11.80m,
                VariancePercent = 5.93m,
                IsVarianceSignificant = false
            }
        };

        var ageing = Schedule3Engine.GetDefaultAgeingTemplates();

        var noteText = Schedule3Engine.GenerateDisclosureNoteText(assessment, ratios, ageing);

        Assert.Contains("NOTE: ADDITIONAL REGULATORY INFORMATION & FINANCIAL RATIOS", noteText);
        Assert.Contains("Current Ratio", noteText);
        Assert.Contains("40.00%", noteText);
        Assert.Contains(">= 25%", noteText);
        Assert.Contains("Increase in current investments", noteText);
        Assert.Contains("TRADE RECEIVABLES AGEING SCHEDULE", noteText);
        Assert.Contains("GENERAL INSTRUCTIONS ON ROUNDING OFF COMPLIANCE", noteText);
    }
}
