using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CaDesk.Infrastructure.Documents;

/// <summary>
/// AWS Signature Version 4 for S3-compatible storage (AWS S3, Cloudflare R2, MinIO), written out so no SDK
/// is needed. Checked against the worked examples in the Amazon S3 API reference (see S3SignerTests).
/// </summary>
public static class S3Signer
{
    public const string Algorithm = "AWS4-HMAC-SHA256";
    public const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>S3's UriEncode: every byte except A-Z a-z 0-9 - . _ ~ is %XX (upper-case hex); '/' kept when encodeSlash is false.</summary>
    public static string UriEncode(string value, bool encodeSlash)
    {
        var sb = new StringBuilder(value.Length * 2);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '.' or '_' or '~' || (c == '/' && !encodeSlash))
                sb.Append(c);
            else
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
    public static string Sha256Hex(byte[] data) => Hex(SHA256.HashData(data));
    public static string Sha256Hex(string data) => Sha256Hex(Encoding.UTF8.GetBytes(data));

    public static byte[] SigningKey(string secretKey, DateTimeOffset now, string region, string service)
    {
        static byte[] H(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
        var k = H(Encoding.UTF8.GetBytes("AWS4" + secretKey), now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        k = H(k, region);
        k = H(k, service);
        return H(k, "aws4_request");
    }

    public static string AmzDate(DateTimeOffset now) => now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    public static string Scope(DateTimeOffset now, string region, string service) =>
        $"{now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}/{region}/{service}/aws4_request";

    /// <summary>
    /// The canonical request. canonicalPath is already URI-encoded; canonicalQuery is the sorted, encoded
    /// query ("" when none); headers are lower-case names to trimmed values.
    /// </summary>
    public static string CanonicalRequest(string method, string canonicalPath, string canonicalQuery,
        IReadOnlyDictionary<string, string> headers, string payloadHash)
    {
        var names = headers.Keys.Select(k => k.ToLowerInvariant()).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var canonicalHeaders = string.Concat(names.Select(n => $"{n}:{headers.First(h => h.Key.Equals(n, StringComparison.OrdinalIgnoreCase)).Value.Trim()}\n"));
        return string.Join('\n', method, canonicalPath, canonicalQuery, canonicalHeaders, string.Join(';', names), payloadHash);
    }

    public static string StringToSign(DateTimeOffset now, string region, string service, string canonicalRequest) =>
        string.Join('\n', Algorithm, AmzDate(now), Scope(now, region, service), Sha256Hex(canonicalRequest));

    public static string Signature(string secretKey, DateTimeOffset now, string region, string service, string stringToSign) =>
        Hex(HMACSHA256.HashData(SigningKey(secretKey, now, region, service), Encoding.UTF8.GetBytes(stringToSign)));

    /// <summary>The Authorization header value for a request signed with the given headers.</summary>
    public static string Authorization(string method, string canonicalPath, string canonicalQuery,
        IReadOnlyDictionary<string, string> headers, string payloadHash,
        string accessKey, string secretKey, string region, DateTimeOffset now, string service = "s3")
    {
        var canonical = CanonicalRequest(method, canonicalPath, canonicalQuery, headers, payloadHash);
        var signature = Signature(secretKey, now, region, service, StringToSign(now, region, service, canonical));
        var signed = string.Join(';', headers.Keys.Select(k => k.ToLowerInvariant()).OrderBy(k => k, StringComparer.Ordinal));
        return $"{Algorithm} Credential={accessKey}/{Scope(now, region, service)}, SignedHeaders={signed}, Signature={signature}";
    }
}
