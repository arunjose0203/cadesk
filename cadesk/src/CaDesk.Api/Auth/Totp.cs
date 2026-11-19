using System.Security.Cryptography;
using System.Text;

namespace CaDesk.Api.Auth;

/// <summary>
/// Time-based one-time passwords (RFC 6238: HMAC-SHA1, 30-second steps, 6 digits),
/// compatible with Google Authenticator, Microsoft Authenticator, Authy etc.
/// </summary>
public static class Totp
{
    public const int StepSeconds = 30;
    public const int Digits = 6;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20);

    public static long CurrentStep(DateTimeOffset now) => now.ToUnixTimeSeconds() / StepSeconds;

    public static string Code(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { counter[i] = (byte)(step & 0xff); step >>= 8; }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, hash);

        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// Accepts the current step and one step either side (clock drift).
    /// Refuses any step at or before <paramref name="lastUsedStep"/> so a code can't be replayed.
    /// </summary>
    public static bool Verify(byte[] secret, string? code, long lastUsedStep, DateTimeOffset now, out long matchedStep)
    {
        matchedStep = 0;
        code = code?.Replace(" ", "").Trim();
        if (code is not { Length: Digits } || !code.All(char.IsAsciiDigit)) return false;

        var current = CurrentStep(now);
        for (var s = current - 1; s <= current + 1; s++)
        {
            if (s <= lastUsedStep) continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(secret, s)), Encoding.ASCII.GetBytes(code)))
            {
                matchedStep = s;
                return true;
            }
        }
        return false;
    }

    public static string OtpAuthUri(byte[] secret, string issuer, string account) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";
}

/// <summary>RFC 4648 base32 without padding, as authenticator apps expect.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Decode(string text)
    {
        text = text.Trim().TrimEnd('=').Replace(" ", "").ToUpperInvariant();
        var output = new List<byte>(text.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var ch in text)
        {
            var v = Alphabet.IndexOf(ch);
            if (v < 0) throw new FormatException($"'{ch}' is not a base32 character.");
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}

/// <summary>Random tokens for refresh tokens, invitations and recovery codes; only hashes are stored.</summary>
public static class SecureTokens
{
    public static string New(int bytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Eight easy-to-type codes like "k7m2-9qxp".</summary>
    public static List<string> NewRecoveryCodes(int count = 8)
    {
        const string chars = "abcdefghjkmnpqrstuvwxyz23456789"; // no 0/o, 1/l/i
        var codes = new List<string>(count);
        for (var n = 0; n < count; n++)
        {
            var c = new char[9];
            for (var i = 0; i < 9; i++) c[i] = i == 4 ? '-' : chars[RandomNumberGenerator.GetInt32(chars.Length)];
            codes.Add(new string(c));
        }
        return codes;
    }

    /// <summary>Case, spaces and the hyphen don't matter when typing a recovery code.</summary>
    public static string NormaliseRecoveryCode(string code) =>
        code.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
}
