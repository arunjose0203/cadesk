using CaDesk.Infrastructure.Documents;

namespace CaDesk.UnitTests;

/// <summary>
/// Sprint 70: AWS Signature V4 for S3-compatible document storage, against the worked examples in the
/// Amazon S3 API reference (example credentials, bucket "examplebucket", object "test.txt", 24 May 2013).
/// </summary>
public class S3SignerTests
{
    private const string AccessKey = "AKIAIOSFODNN7EXAMPLE";
    private const string SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
    private static readonly DateTimeOffset When = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Presigned_url_example_matches_the_documented_string_to_sign_and_signature()
    {
        const string query = "X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request"
                             + "&X-Amz-Date=20130524T000000Z&X-Amz-Expires=86400&X-Amz-SignedHeaders=host";
        var canonical = S3Signer.CanonicalRequest("GET", "/test.txt", query,
            new Dictionary<string, string> { ["host"] = "examplebucket.s3.amazonaws.com" }, "UNSIGNED-PAYLOAD");
        Assert.Equal("3bfa292879f6447bbcda7001decf97f4a54dc650c8942174ae0a9121cf58ad04", S3Signer.Sha256Hex(canonical));
        var sts = S3Signer.StringToSign(When, "us-east-1", "s3", canonical);
        Assert.Equal("aeeed9bbccd4d02ee5c0109b86d86835f995330da4c265957d157751f604d404", S3Signer.Signature(SecretKey, When, "us-east-1", "s3", sts));
    }

    [Fact]
    public void Header_signed_get_example()
    {
        var headers = new Dictionary<string, string>
        {
            ["host"] = "examplebucket.s3.amazonaws.com",
            ["range"] = "bytes=0-9",
            ["x-amz-content-sha256"] = S3Signer.EmptyPayloadHash,
            ["x-amz-date"] = "20130524T000000Z",
        };
        var auth = S3Signer.Authorization("GET", "/test.txt", "", headers, S3Signer.EmptyPayloadHash, AccessKey, SecretKey, "us-east-1", When);
        Assert.Equal("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
                     + "SignedHeaders=host;range;x-amz-content-sha256;x-amz-date, "
                     + "Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41", auth);
    }

    [Fact]
    public void Signing_key_derivation_example()
    {
        // AWS general reference: secret wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY, 20120215, us-east-1, iam
        var key = S3Signer.SigningKey("wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY", new DateTimeOffset(2012, 2, 15, 0, 0, 0, TimeSpan.Zero), "us-east-1", "iam");
        Assert.Equal("f4780e2d9f65fa895f9c67b32ce1baf0b0d8a43505a000a1a9e090d414db404d", S3Signer.Hex(key));
    }

    [Theory]
    [InlineData("photos/Jan/sample.jpg", false, "photos/Jan/sample.jpg")]
    [InlineData("a b+c", true, "a%20b%2Bc")]
    [InlineData("a/b", true, "a%2Fb")]
    [InlineData("₹", true, "%E2%82%B9")]
    public void Uri_encoding_follows_s3_rules(string input, bool encodeSlash, string expected) =>
        Assert.Equal(expected, S3Signer.UriEncode(input, encodeSlash));

    [Fact]
    public async Task Store_refuses_keys_the_server_did_not_make()
    {
        var store = new S3FileStore(new HttpClient(), new S3StorageOptions
        {
            Endpoint = "https://s3.ap-south-1.amazonaws.com", Bucket = "b", AccessKey = "a", SecretKey = "s",
        });
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenAsync("../../etc/passwd", CancellationToken.None));
    }
}
