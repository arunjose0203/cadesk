using CaDesk.Domain.BankFeeds;
using Xunit;

namespace CaDesk.UnitTests;

public class ForensicAnalysisUnitTests
{
    private readonly Guid _firmId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();

    [Fact]
    public void ScanSec40A3_FlagsGeneralCashPaymentAbove10k()
    {
        var txs = new List<BankTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 5, 10),
                TransactionType = BankTransactionType.Debit,
                Amount = 15_000m,
                Narration = "Self Cash Withdrawal for office expenses"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 5, 11),
                TransactionType = BankTransactionType.Debit,
                Amount = 8_500m,
                Narration = "Petty cash bearer payment"
            }
        };

        var anomalies = ForensicAuditEngine.ScanSec40A3(txs, _firmId, _bankAccountId);

        Assert.Single(anomalies);
        var a = anomalies[0];
        Assert.Equal(ForensicAnomalyType.Sec40A3CashPayment, a.AnomalyType);
        Assert.Equal(15_000m, a.Amount);
        Assert.Equal(15_000m, a.ExposureAmount);
        Assert.Equal(ForensicRiskLevel.High, a.RiskLevel);
        Assert.Contains("General Limit", a.StatutorySection);
    }

    [Fact]
    public void ScanSec40A3_AllowsTransportPaymentUpTo35kAndFlagsAbove()
    {
        var txs = new List<BankTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 6, 1),
                TransactionType = BankTransactionType.Debit,
                Amount = 32_000m,
                Narration = "Cash payment to Sri Balaji Transport lorry freight"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 6, 2),
                TransactionType = BankTransactionType.Debit,
                Amount = 45_000m,
                Narration = "Cash withdrawal for carriage fleet logistics transporter"
            }
        };

        var anomalies = ForensicAuditEngine.ScanSec40A3(txs, _firmId, _bankAccountId);

        Assert.Single(anomalies);
        var a = anomalies[0];
        Assert.Equal(45_000m, a.Amount);
        Assert.Contains("Goods Carriage Limit", a.StatutorySection);
    }

    [Fact]
    public void ScanSec269St_FlagsCashReceiptAtOrAbove2LakhsAsCritical()
    {
        var txs = new List<BankTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 7, 1),
                TransactionType = BankTransactionType.Credit,
                Amount = 250_000m,
                Narration = "Cash deposit CDM branch 401"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 7, 2),
                TransactionType = BankTransactionType.Credit,
                Amount = 180_000m,
                Narration = "Cash deposit self"
            }
        };

        var anomalies = ForensicAuditEngine.ScanSec269St(txs, _firmId, _bankAccountId);

        Assert.Single(anomalies);
        var a = anomalies[0];
        Assert.Equal(ForensicAnomalyType.Sec269StCashReceipt, a.AnomalyType);
        Assert.Equal(250_000m, a.Amount);
        Assert.Equal(250_000m, a.ExposureAmount); // 100% penalty under 271DA
        Assert.Equal(ForensicRiskLevel.Critical, a.RiskLevel);
        Assert.Contains("269ST", a.StatutorySection);
    }

    [Fact]
    public void ScanSec269SsT_FlagsCashLoansAcceptedOrRepaidAbove20k()
    {
        var txs = new List<BankTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 8, 15),
                TransactionType = BankTransactionType.Credit,
                Amount = 50_000m,
                Narration = "Cash deposit hand loan from director"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 8, 20),
                TransactionType = BankTransactionType.Debit,
                Amount = 40_000m,
                Narration = "Cash withdrawal unsecured loan repayment"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 8, 22),
                TransactionType = BankTransactionType.Credit,
                Amount = 15_000m,
                Narration = "Cash deposit advance borrowing"
            }
        };

        var anomalies = ForensicAuditEngine.ScanSec269SsT(txs, _firmId, _bankAccountId);

        Assert.Equal(2, anomalies.Count);
        Assert.Contains(anomalies, a => a.AnomalyType == ForensicAnomalyType.Sec269SsLoanAccepted && a.Amount == 50_000m);
        Assert.Contains(anomalies, a => a.AnomalyType == ForensicAnomalyType.Sec269TLoanRepaid && a.Amount == 40_000m);
    }

    [Fact]
    public void ScanRoundTripping_DetectsCircularDebitAndCreditWithin5Days()
    {
        var txs = new List<BankTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 9, 1),
                TransactionType = BankTransactionType.Debit,
                Amount = 500_000m,
                Narration = "RTGS Outflow to Vendor X"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 9, 3),
                TransactionType = BankTransactionType.Credit,
                Amount = 498_000m, // within 1% tolerance
                Narration = "NEFT Inflow from Associate Y"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 9, 20),
                TransactionType = BankTransactionType.Debit,
                Amount = 100_000m,
                Narration = "Normal salary run"
            }
        };

        var anomalies = ForensicAuditEngine.ScanRoundTripping(txs, _firmId, _bankAccountId, dayWindow: 5, tolerancePct: 1.0m);

        Assert.Single(anomalies);
        var a = anomalies[0];
        Assert.Equal(ForensicAnomalyType.RoundTripping, a.AnomalyType);
        Assert.Equal(500_000m, a.Amount);
        Assert.Equal(ForensicRiskLevel.Critical, a.RiskLevel);
    }

    [Fact]
    public void ScanSmurfingStructuring_DetectsSplitCashWithdrawalsWithin48h()
    {
        var txs = new List<BankTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 10, 5),
                TransactionType = BankTransactionType.Debit,
                Amount = 9_500m,
                Narration = "Cash withdrawal ATM 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 10, 6),
                TransactionType = BankTransactionType.Debit,
                Amount = 9_800m,
                Narration = "Cash withdrawal ATM 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 10, 6),
                TransactionType = BankTransactionType.Debit,
                Amount = 9_000m,
                Narration = "Self bearer cash withdrawal"
            }
        };

        var anomalies = ForensicAuditEngine.ScanSmurfing(txs, _firmId, _bankAccountId);

        Assert.NotEmpty(anomalies);
        Assert.All(anomalies, a => Assert.Equal(ForensicAnomalyType.SmurfingStructuring, a.AnomalyType));
        Assert.Contains("Section 40A(3)", anomalies[0].StatutorySection);
    }

    [Fact]
    public void ScanHighValueRoundSum_DetectsExactMultiplesOfOneLakhAbove500k()
    {
        var txs = new List<BankTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 11, 1),
                TransactionType = BankTransactionType.Debit,
                Amount = 1_000_000m, // 10 Lakhs round
                Narration = "Transfer to Consultant"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TransactionDate = new DateOnly(2025, 11, 2),
                TransactionType = BankTransactionType.Debit,
                Amount = 654_321m, // Odd amount
                Narration = "Vendor payment invoice 402"
            }
        };

        var anomalies = ForensicAuditEngine.ScanHighValueRoundSum(txs, _firmId, _bankAccountId);

        Assert.Single(anomalies);
        Assert.Equal(ForensicAnomalyType.HighValueRoundSum, anomalies[0].AnomalyType);
        Assert.Equal(1_000_000m, anomalies[0].Amount);
    }

    [Fact]
    public void CalculateOverallRiskScore_AccuratelyWeightsRiskLevelsAndCapsAt100()
    {
        var anomalies = new List<ForensicAnomalyItem>
        {
            new() { RiskLevel = ForensicRiskLevel.Critical }, // 25
            new() { RiskLevel = ForensicRiskLevel.Critical }, // 25
            new() { RiskLevel = ForensicRiskLevel.High },     // 15
            new() { RiskLevel = ForensicRiskLevel.High },     // 15
            new() { RiskLevel = ForensicRiskLevel.Medium },   // 5
            new() { RiskLevel = ForensicRiskLevel.Low }       // 2
        }; // total = 87

        var score = ForensicAuditEngine.CalculateOverallRiskScore(anomalies);
        Assert.Equal(87, score);

        // Add 2 more criticals: 87 + 50 = 137 -> capped at 100
        anomalies.Add(new ForensicAnomalyItem { RiskLevel = ForensicRiskLevel.Critical });
        anomalies.Add(new ForensicAnomalyItem { RiskLevel = ForensicRiskLevel.Critical });
        var cappedScore = ForensicAuditEngine.CalculateOverallRiskScore(anomalies);
        Assert.Equal(100, cappedScore);
    }
}
