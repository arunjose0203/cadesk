using System.Net;
using System.Text.RegularExpressions;
using CaDesk.Application.Documents;

namespace CaDesk.Infrastructure.Documents;

/// <summary>Where an S3-compatible bucket is. Path-style URLs: {Endpoint}/{Bucket}/{key}.</summary>
public sealed class S3StorageOptions
{
    /// <summary>e.g. https://s3.ap-south-1.amazonaws.com, https://&lt;account&gt;.r2.cloudflarestorage.com, http://minio:9000</summary>
    public string Endpoint { get; set; } = "";
    /// <summary>AWS region (ap-south-1 = Mumbai). Cloudflare R2 uses "auto"; MinIO usually "us-east-1".</summary>
    public string Region { get; set; } = "ap-south-1";
    public string Bucket { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    /// <summary>Optional folder inside the bucket, e.g. "documents".</summary>
    public string Prefix { get; set; } = "";

    public void Validate()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http"))
            throw new InvalidOperationException("Documents:S3:Endpoint must be an http(s) URL.");
        if (string.IsNullOrWhiteSpace(Bucket) || string.IsNullOrWhiteSpace(AccessKey) || string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException("Documents:S3 needs Bucket, AccessKey and SecretKey.");
    }
}

/// <summary>
/// Document bytes in an S3-compatible bucket (Sprint 70). Same key rules as <see cref="LocalFileStore"/>:
/// keys are made by the server ("firm/yyyy/MM/id"), anything else is refused. Turn on default encryption
/// and versioning on the bucket; keep it private.
/// </summary>
public sealed partial class S3FileStore(HttpClient http, S3StorageOptions options, TimeProvider? clock = null) : IFileStore
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    [GeneratedRegex("^[0-9a-f]{32}/[0-9]{4}/[0-9]{2}/[0-9a-f]{32}$")]
    private static partial Regex KeyPattern();

    private (Uri Url, string CanonicalPath) Locate(string key)
    {
        if (!KeyPattern().IsMatch(key)) throw new ArgumentException("Not a valid storage key.", nameof(key));
        var prefix = options.Prefix.Trim('/');
        var objectKey = prefix.Length == 0 ? key : $"{prefix}/{key}";
        var basePath = new Uri(options.Endpoint).AbsolutePath.TrimEnd('/');
        var path = $"{basePath}/{S3Signer.UriEncode(options.Bucket, true)}/{S3Signer.UriEncode(objectKey, false)}";
        var root = new Uri(options.Endpoint).GetLeftPart(UriPartial.Authority);
        return (new Uri(root + path), path);
    }

    private HttpRequestMessage Signed(HttpMethod method, string key, byte[]? body)
    {
        var (url, canonicalPath) = Locate(key);
        var now = _clock.GetUtcNow();
        var payloadHash = body is null ? S3Signer.EmptyPayloadHash : S3Signer.Sha256Hex(body);
        var host = url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";
        var headers = new Dictionary<string, string>
        {
            ["host"] = host,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = S3Signer.AmzDate(now),
        };
        var msg = new HttpRequestMessage(method, url);
        msg.Headers.Host = host;
        msg.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        msg.Headers.TryAddWithoutValidation("x-amz-date", headers["x-amz-date"]);
        msg.Headers.TryAddWithoutValidation("Authorization",
            S3Signer.Authorization(method.Method, canonicalPath, "", headers, payloadHash, options.AccessKey, options.SecretKey, options.Region, now));
        if (body is not null)
        {
            msg.Content = new ByteArrayContent(body);
            msg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        }
        return msg;
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        byte[] body;
        using (var ms = new MemoryStream())
        {
            await content.CopyToAsync(ms, ct);
            body = ms.ToArray();
        }
        using var res = await http.SendAsync(Signed(HttpMethod.Put, key, body), ct);
        if (!res.IsSuccessStatusCode)
            throw new IOException($"Storage refused the file ({(int)res.StatusCode}): {Trim(await res.Content.ReadAsStringAsync(ct))}");
    }

    public async Task<Stream?> OpenAsync(string key, CancellationToken ct)
    {
        using var res = await http.SendAsync(Signed(HttpMethod.Get, key, null), ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode)
            throw new IOException($"Storage could not return the file ({(int)res.StatusCode}): {Trim(await res.Content.ReadAsStringAsync(ct))}");
        return new MemoryStream(await res.Content.ReadAsByteArrayAsync(ct), writable: false);
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        using var res = await http.SendAsync(Signed(HttpMethod.Delete, key, null), ct);
        if (!res.IsSuccessStatusCode && res.StatusCode != HttpStatusCode.NotFound)
            throw new IOException($"Storage could not delete the file ({(int)res.StatusCode}).");
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] : s;
}
