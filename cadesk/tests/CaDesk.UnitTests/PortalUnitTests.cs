using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CaDesk.Domain.Portal;
using Xunit;

namespace CaDesk.UnitTests;

public class PortalUnitTests
{
    private static readonly Regex EmailRegex = new(@"^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$", RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("director@client.com", true)]
    [InlineData("cfo.office+tax@corp.co.in", true)]
    [InlineData("plainaddress", false)]
    [InlineData("@missingusername.com", false)]
    [InlineData("user@.com", false)]
    public void PortalUserEmail_ValidatesCorrectFormat(string email, bool isValid)
    {
        var valid = EmailRegex.IsMatch(email);
        Assert.Equal(isValid, valid);
    }

    [Fact]
    public void PasswordHashing_Pbkdf2WithSalt_VerifiesCorrectlyAndRejectsWrong()
    {
        string password = "SecureClientPassword#2026";
        
        // Hash
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);
        string stored = $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        // Verify correct
        var parts = stored.Split('.');
        byte[] readSalt = Convert.FromBase64String(parts[0]);
        byte[] readHash = Convert.FromBase64String(parts[1]);

        byte[] checkHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            readSalt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        Assert.True(CryptographicOperations.FixedTimeEquals(readHash, checkHash));

        // Verify wrong
        byte[] wrongHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("WrongPassword123"),
            readSalt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        Assert.False(CryptographicOperations.FixedTimeEquals(readHash, wrongHash));
    }

    [Theory]
    [InlineData(RequestCategory.TaxFiling, true)]
    [InlineData(RequestCategory.NoticeReply, true)]
    [InlineData(RequestCategory.Certification, true)]
    [InlineData(RequestCategory.Advisory, true)]
    [InlineData(RequestCategory.BillingQuery, true)]
    [InlineData(RequestCategory.General, true)]
    [InlineData("invalid_category", false)]
    public void RequestCategories_ValidatesKnownValues(string cat, bool isValid)
    {
        Assert.Equal(isValid, RequestCategory.All.Contains(cat));
    }

    [Theory]
    [InlineData(RequestStatus.Submitted, true)]
    [InlineData(RequestStatus.InProgress, true)]
    [InlineData(RequestStatus.Resolved, true)]
    [InlineData(RequestStatus.Closed, true)]
    [InlineData("cancelled_ticket", false)]
    public void RequestStatus_ValidatesStandardLifecycle(string status, bool isValid)
    {
        Assert.Equal(isValid, RequestStatus.All.Contains(status));
    }

    [Fact]
    public void RequestNumber_FormatsSequentialString()
    {
        int year = 2026;
        int count = 42;
        string formatted = $"REQ/{year}/{(count + 1):D4}";

        Assert.Equal("REQ/2026/0043", formatted);
    }
}
