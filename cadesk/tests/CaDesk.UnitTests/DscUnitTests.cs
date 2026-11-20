using System.Text.RegularExpressions;
using CaDesk.Domain.Dsc;
using Xunit;

namespace CaDesk.UnitTests;

public class DscUnitTests
{
    private static readonly Regex PanRegex = new("^[A-Z]{5}[0-9]{4}[A-Z]$");

    [Theory]
    [InlineData("ABCDE1234F", true)]
    [InlineData("AAACB1234F", true)]
    [InlineData("ZZZZZ9999Z", true)]
    [InlineData("abcde1234f", false)] // lower-case without normalization
    [InlineData("ABCD1234F", false)]  // 4 letters
    [InlineData("ABCDE12345", false)] // digit at end
    [InlineData("12345ABCDE", false)] // reversed
    public void PanValidation_MatchesIncomeTaxStandard(string pan, bool isValid)
    {
        var match = PanRegex.IsMatch(pan);
        Assert.Equal(isValid, match);
    }

    [Theory]
    [InlineData(60, DscStatus.Active)]
    [InlineData(31, DscStatus.Active)]
    [InlineData(30, DscStatus.ExpiringSoon)]
    [InlineData(5, DscStatus.ExpiringSoon)]
    [InlineData(0, DscStatus.ExpiringSoon)]
    [InlineData(-1, DscStatus.Expired)]
    [InlineData(-100, DscStatus.Expired)]
    public void ExpiryStatusComputation_ClassifiesCorrectly(int dayOffset, string expectedStatus)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var validTo = today.AddDays(dayOffset);
        int warningDays = 30;

        string status;
        if (validTo < today) status = DscStatus.Expired;
        else if (validTo <= today.AddDays(warningDays)) status = DscStatus.ExpiringSoon;
        else status = DscStatus.Active;

        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void DaysRemaining_ComputesExactDayDifference()
    {
        var today = new DateOnly(2026, 11, 19);
        var expiry = new DateOnly(2026, 12, 19);

        var days = expiry.DayNumber - today.DayNumber;

        Assert.Equal(30, days);
    }

    [Theory]
    [InlineData(DscLocation.Vault, true)]
    [InlineData(DscLocation.WithStaff, true)]
    [InlineData(DscLocation.WithClient, true)]
    [InlineData(DscLocation.InTransit, true)]
    [InlineData("lost_somewhere", false)]
    public void LocationWhitelist_ValidatesKnownCustodyLocations(string location, bool isValid)
    {
        Assert.Equal(isValid, DscLocation.All.Contains(location));
    }
}
