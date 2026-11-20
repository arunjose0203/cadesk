using System.Text.RegularExpressions;
using CaDesk.Domain.EInvoicing;

namespace CaDesk.UnitTests;

public class EInvoicingUnitTests
{
    [Fact]
    public void IrnGenerator_ComputesDeterministic64CharHexSha256()
    {
        var supplierGstin = "27AAACB1234F1Z5";
        var fy = "2024-25";
        var docType = "INV";
        var docNumber = "INV/2024/001";

        var irn = IrnGenerator.GenerateIrn(supplierGstin, fy, docType, docNumber);

        // Must be exactly 64 characters long hexadecimal string
        Assert.Equal(64, irn.Length);
        Assert.Matches("^[0-9a-f]{64}$", irn);

        // Identical inputs must yield identical hash
        var irn2 = IrnGenerator.GenerateIrn("27aaacb1234f1z5", "2024-25", "inv", "INV/2024/001");
        Assert.Equal(irn, irn2);

        // Different doc number must produce different hash
        var irnDiff = IrnGenerator.GenerateIrn(supplierGstin, fy, docType, "INV/2024/002");
        Assert.NotEqual(irn, irnDiff);
    }

    [Fact]
    public void IrnGenerator_GeneratesSignedQrPayload_ContainsMandatoryAttributes()
    {
        var supplierGstin = "27AAACB1234F1Z5";
        var recipientGstin = "29BBBCB5678G1Z2";
        var docNo = "INV-2024-100";
        var docDate = new DateOnly(2024, 10, 15);
        var totalVal = 118000m;
        var irn = IrnGenerator.GenerateIrn(supplierGstin, "2024-25", "INV", docNo);

        var qr = IrnGenerator.GenerateSignedQrPayload(
            supplierGstin,
            recipientGstin,
            docNo,
            docDate,
            totalVal,
            itemCount: 3,
            mainHsn: "998311",
            irn: irn
        );

        Assert.Contains(supplierGstin, qr);
        Assert.Contains(recipientGstin, qr);
        Assert.Contains(docNo, qr);
        Assert.Contains("118000", qr);
        Assert.Contains(irn, qr);
        Assert.Contains("NIC-IRP-SIG-", qr);
    }

    [Theory]
    [InlineData(50, 1)]    // Under 200 km -> 1 day
    [InlineData(200, 1)]   // Exactly 200 km -> 1 day
    [InlineData(201, 2)]   // Just over 200 km -> 2 days
    [InlineData(400, 2)]   // Exactly 400 km -> 2 days
    [InlineData(450, 3)]   // 450 km -> 3 days
    [InlineData(1000, 5)]  // 1000 km -> 5 days
    public void EwbValidityCalculator_RegularCargo_Calculates1DayPer200Km(int distanceKm, int expectedDays)
    {
        var days = EwbValidityCalculator.CalculateValidityDays(distanceKm, EwbVehicleType.Regular);
        Assert.Equal(expectedDays, days);
    }

    [Theory]
    [InlineData(15, 1)]   // Under 20 km -> 1 day
    [InlineData(20, 1)]   // Exactly 20 km -> 1 day
    [InlineData(25, 2)]   // 25 km -> 2 days
    [InlineData(60, 3)]   // 60 km -> 3 days
    public void EwbValidityCalculator_OverDimensionalCargo_Calculates1DayPer20Km(int distanceKm, int expectedDays)
    {
        var days = EwbValidityCalculator.CalculateValidityDays(distanceKm, EwbVehicleType.OverDimensionalCargo);
        Assert.Equal(expectedDays, days);
    }

    [Fact]
    public void EwbValidityCalculator_CalculateExpiry_SetsCorrectTimestamp()
    {
        var start = new DateTimeOffset(2024, 10, 10, 12, 0, 0, TimeSpan.Zero);
        int distance = 350; // 2 days

        var expiry = EwbValidityCalculator.CalculateExpiry(start, distance, EwbVehicleType.Regular);

        var expected = new DateTimeOffset(2024, 10, 12, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, expiry);
    }

    [Fact]
    public void EWayBillRecord_RemainingHours_CalculatesCountdownCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var ewb = new EWayBillRecord
        {
            ValidUntil = now.AddHours(36)
        };

        var remaining = ewb.RemainingHours(now);
        Assert.True(remaining >= 35 && remaining <= 36);
    }
}
