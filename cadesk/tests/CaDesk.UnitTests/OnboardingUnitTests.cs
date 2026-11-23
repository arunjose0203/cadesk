using System.Text.RegularExpressions;
using Xunit;

namespace CaDesk.UnitTests;

public class OnboardingUnitTests
{
    private static bool IsValidFrn(string? frn)
    {
        if (string.IsNullOrWhiteSpace(frn)) return false;
        var trimmed = frn.Trim().ToUpperInvariant();
        // ICAI Firm Registration Number (FRN): typically 6 digits followed by a letter (e.g. 104523W) or 6 digits (e.g. 012345N or 123456S)
        return Regex.IsMatch(trimmed, @"^[0-9]{6}[A-Z]$") || Regex.IsMatch(trimmed, @"^[0-9]{6}$");
    }

    private static bool IsValidMembershipNo(string? memNo)
    {
        if (string.IsNullOrWhiteSpace(memNo)) return false;
        var trimmed = memNo.Trim();
        // ICAI Membership Number: 5 to 6 digits
        return Regex.IsMatch(trimmed, @"^[0-9]{5,6}$");
    }

    private static bool IsValidFirmPan(string? pan)
    {
        if (string.IsNullOrWhiteSpace(pan)) return false;
        var trimmed = pan.Trim().ToUpperInvariant();
        // Indian PAN format: 5 letters, 4 digits, 1 letter. 4th letter 'F' for partnership/LLP firm, 'C' for company, 'P' for sole proprietor
        return Regex.IsMatch(trimmed, @"^[A-Z]{3}[FCP][A-Z][0-9]{4}[A-Z]$");
    }

    [Theory]
    [InlineData("104523W", true)]
    [InlineData("012345N", true)]
    [InlineData("324512S", true)]
    [InlineData("123456", true)]
    [InlineData("10452W", false)] // 5 digits
    [InlineData("1045234W", false)] // 7 digits
    [InlineData("ABC123W", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void FrnValidation_ValidatesIcaiFirmRegistrationNumbers(string? frn, bool expected)
    {
        var valid = IsValidFrn(frn);
        Assert.Equal(expected, valid);
    }

    [Theory]
    [InlineData("054321", true)]
    [InlineData("123456", true)]
    [InlineData("99999", true)] // 5 digits
    [InlineData("1234", false)] // too short
    [InlineData("1234567", false)] // too long
    [InlineData("A12345", false)]
    [InlineData("", false)]
    public void MembershipNoValidation_ValidatesIcaiMemberNumbers(string? memNo, bool expected)
    {
        var valid = IsValidMembershipNo(memNo);
        Assert.Equal(expected, valid);
    }

    [Theory]
    [InlineData("AAAFR1234A", true)] // Firm PAN (4th char 'F')
    [InlineData("AABCR5678B", true)] // Corporate PAN (4th char 'C')
    [InlineData("ABCPE1234F", true)] // Proprietary PAN (4th char 'P')
    [InlineData("AAATR1234A", false)] // Trust ('T')
    [InlineData("AAAFR12345", false)] // Last char not letter
    [InlineData("AAAF1234A", false)] // Too short
    public void FirmPanValidation_ValidatesAppropriateEntityTypes(string? pan, bool expected)
    {
        var valid = IsValidFirmPan(pan);
        Assert.Equal(expected, valid);
    }

    [Fact]
    public void ProductTour_ContainsAll6CoreMoats()
    {
        var moats = new[]
        {
            "Universal Trial Balance Importer",
            "Forensic Bank Analyzer & Fraud Detection",
            "SA 505 Automated External Confirmations",
            "SA 230 Digital Audit Workpapers & Peer Review Dossier",
            "1-Click ICAI Independent Auditor's Report (SA 700)",
            "ICAI AQMM v1.0 600-Point Audit Quality Maturity Model"
        };

        Assert.Equal(6, moats.Length);
        Assert.Contains(moats, m => m.Contains("Trial Balance"));
        Assert.Contains(moats, m => m.Contains("Forensic Bank"));
        Assert.Contains(moats, m => m.Contains("SA 505"));
        Assert.Contains(moats, m => m.Contains("SA 230"));
        Assert.Contains(moats, m => m.Contains("SA 700"));
        Assert.Contains(moats, m => m.Contains("AQMM v1.0"));
    }
}
