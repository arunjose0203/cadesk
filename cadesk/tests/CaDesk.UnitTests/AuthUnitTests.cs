using System.Text;
using CaDesk.Api.Auth;

namespace CaDesk.UnitTests;

public class TotpTests
{
    // RFC 6238 appendix B, SHA-1, secret "12345678901234567890"; last 6 digits of the 8-digit values.
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1234567890L, "005924")]
    [InlineData(20000000000L, "353130")]
    public void Matches_rfc_6238_vectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, Totp.Code(RfcSecret, unixSeconds / Totp.StepSeconds));

    [Fact]
    public void Accepts_current_and_neighbour_steps_but_not_replays()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var step = Totp.CurrentStep(now);

        Assert.True(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step), 0, now, out var matched));
        Assert.Equal(step, matched);
        Assert.True(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step - 1), 0, now, out _));
        Assert.False(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step - 2), 0, now, out _));
        Assert.False(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step), lastUsedStep: step, now, out _));
        Assert.False(Totp.Verify(RfcSecret, "12a456", 0, now, out _));
        Assert.False(Totp.Verify(RfcSecret, null, 0, now, out _));
    }

    [Fact]
    public void Accepts_codes_typed_with_a_space()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var code = Totp.Code(RfcSecret, Totp.CurrentStep(now));
        Assert.True(Totp.Verify(RfcSecret, $"{code[..3]} {code[3..]}", 0, now, out _));
    }
}

public class Base32Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Encodes_rfc_4648_vectors_without_padding(string input, string expected) =>
        Assert.Equal(expected, Base32.Encode(Encoding.ASCII.GetBytes(input)));

    [Fact]
    public void Round_trips_random_secrets()
    {
        for (var i = 0; i < 50; i++)
        {
            var secret = Totp.NewSecret();
            Assert.Equal(secret, Base32.Decode(Base32.Encode(secret)));
            Assert.Equal(secret, Base32.Decode(Base32.Encode(secret).ToLowerInvariant()));
        }
    }

    [Fact]
    public void Rejects_invalid_characters() => Assert.Throws<FormatException>(() => Base32.Decode("ABC1"));
}

public class SecureTokenTests
{
    [Fact]
    public void Tokens_are_url_safe_unique_and_hashed()
    {
        var a = SecureTokens.New();
        var b = SecureTokens.New();
        Assert.NotEqual(a, b);
        Assert.DoesNotContain('+', a);
        Assert.DoesNotContain('/', a);
        Assert.DoesNotContain('=', a);
        Assert.Equal(64, SecureTokens.Hash(a).Length);
        Assert.Equal(SecureTokens.Hash(a), SecureTokens.Hash(a));
    }

    [Fact]
    public void Recovery_codes_are_readable_and_normalise()
    {
        var codes = SecureTokens.NewRecoveryCodes();
        Assert.Equal(8, codes.Count);
        Assert.All(codes, c => Assert.Matches("^[a-z2-9]{4}-[a-z2-9]{4}$", c));
        var expected = codes[0].Replace("-", "");
        Assert.Equal(expected, SecureTokens.NormaliseRecoveryCode(codes[0]));
        Assert.Equal(expected, SecureTokens.NormaliseRecoveryCode($" {codes[0].ToUpperInvariant()} "));
        Assert.Equal(expected, SecureTokens.NormaliseRecoveryCode(expected.ToUpperInvariant()));
    }
}

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("short", false)]
    [InlineData("aaaaaaaaaaaa", false)]          // too repetitive
    [InlineData("ravi.kumar-2026!", false)]      // contains email name
    [InlineData("Correct-Horse-42", true)]
    [InlineData("monsoon tea in munnar", true)]  // long passphrases are fine
    public void Applies_simple_rules(string password, bool ok) =>
        Assert.Equal(ok, AuthService.PasswordProblem(password, "ravi.kumar@firm.example") is null);

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("name@firm.example", true)]
    [InlineData("no-at-sign.com", false)]
    [InlineData("@firm.com", false)]
    [InlineData("a b@firm.com", false)]
    [InlineData("a@firm", false)]
    public void Recognises_email_shape(string email, bool ok) =>
        Assert.Equal(ok, AuthService.LooksLikeEmail(email));
}
