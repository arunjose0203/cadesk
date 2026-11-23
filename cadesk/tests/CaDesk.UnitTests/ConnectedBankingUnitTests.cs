using CaDesk.Domain.ConnectedBanking;
using CaDesk.Domain.Users;
using Xunit;

namespace CaDesk.UnitTests;

public class ConnectedBankingUnitTests
{
    [Theory]
    [InlineData("HDFC0000123", true)]
    [InlineData("ICIC0001045", true)]
    [InlineData("UTIB0000582", true)]
    [InlineData("SBIN0001542", true)]
    [InlineData("RBIS0CBDT01", true)]
    [InlineData("RBIS0GST001", true)]
    [InlineData("HDFC123", false)]
    [InlineData("INVALID", false)]
    [InlineData("HDFC0000123456", false)]
    [InlineData("", false)]
    public void BankingValidation_ValidatesIndianIfscFormats(string ifsc, bool expectedValid)
    {
        bool result = BankingValidation.IsValidIfsc(ifsc);
        Assert.Equal(expectedValid, result);
    }

    [Theory]
    [InlineData("10452391001", true)]
    [InlineData("50200012345678", true)]
    [InlineData("123456789", true)]
    [InlineData("12345678", false)] // < 9 digits
    [InlineData("12345678901234567890", false)] // > 18 digits
    [InlineData("1045239A001", false)] // Contains alpha
    [InlineData("", false)]
    public void BankingValidation_ValidatesIndianAccountNumbers(string accountNo, bool expectedValid)
    {
        bool result = BankingValidation.IsValidAccountNumber(accountNo);
        Assert.Equal(expectedValid, result);
    }

    [Fact]
    public void PayoutPaymentMode_RtgsEnforcesMinimumTwoLakhs()
    {
        bool validUnder = PayoutPaymentMode.ValidatePaymentMode(PayoutPaymentMode.Rtgs, 150000m, out var errUnder);
        Assert.False(validUnder);
        Assert.Contains("2,00,000", errUnder);

        bool validExact = PayoutPaymentMode.ValidatePaymentMode(PayoutPaymentMode.Rtgs, 200000m, out var errExact);
        Assert.True(validExact);
        Assert.Null(errExact);

        bool validOver = PayoutPaymentMode.ValidatePaymentMode(PayoutPaymentMode.Rtgs, 5000000m, out var errOver);
        Assert.True(validOver);
        Assert.Null(errOver);
    }

    [Fact]
    public void PayoutPaymentMode_UpiEnforcesMaximumOneLakh()
    {
        bool validUnder = PayoutPaymentMode.ValidatePaymentMode(PayoutPaymentMode.Upi, 50000m, out var errUnder);
        Assert.True(validUnder);
        Assert.Null(errUnder);

        bool validOver = PayoutPaymentMode.ValidatePaymentMode(PayoutPaymentMode.Upi, 150000m, out var errOver);
        Assert.False(validOver);
        Assert.Contains("1,00,000", errOver);
    }

    [Fact]
    public void PayoutPaymentMode_ImpsEnforcesMaximumFiveLakhs()
    {
        bool validUnder = PayoutPaymentMode.ValidatePaymentMode(PayoutPaymentMode.Imps, 300000m, out var errUnder);
        Assert.True(validUnder);
        Assert.Null(errUnder);

        bool validOver = PayoutPaymentMode.ValidatePaymentMode(PayoutPaymentMode.Imps, 600000m, out var errOver);
        Assert.False(validOver);
        Assert.Contains("5,00,000", errOver);
    }

    [Fact]
    public void BankIntegrationProviders_MatchSupportedGateways()
    {
        Assert.Contains(BankIntegrationProvider.IciciCorporate, BankIntegrationProvider.All);
        Assert.Contains(BankIntegrationProvider.HdfcSmartHub, BankIntegrationProvider.All);
        Assert.Contains(BankIntegrationProvider.AxisOpen, BankIntegrationProvider.All);
        Assert.Contains(BankIntegrationProvider.OpenMoney, BankIntegrationProvider.All);
    }

    [Fact]
    public void ConnectedBanking_PermissionsConstants_AreRegistered()
    {
        Assert.Equal("banking.connected.manage", Perms.BankingConnectedManage);
        Assert.Equal("banking.payout.maker", Perms.BankingPayoutMaker);
        Assert.Equal("banking.payout.checker", Perms.BankingPayoutChecker);
    }

    [Fact]
    public void PayoutBatchStatus_CoversFullMakerCheckerLifecycle()
    {
        Assert.Contains(PayoutBatchStatus.Draft, PayoutBatchStatus.All);
        Assert.Contains(PayoutBatchStatus.PendingApproval, PayoutBatchStatus.All);
        Assert.Contains(PayoutBatchStatus.Authorized, PayoutBatchStatus.All);
        Assert.Contains(PayoutBatchStatus.Completed, PayoutBatchStatus.All);
        Assert.Contains(PayoutBatchStatus.Rejected, PayoutBatchStatus.All);
    }
}
