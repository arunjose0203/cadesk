using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using CaDesk.Application.Documents;

namespace CaDesk.Api.Documents;

/// <summary>Documents:* configuration.</summary>
public sealed class DocumentOptions
{
    /// <summary>Folder for document files (a mounted volume in Docker). Back it up with the database.</summary>
    public string StoragePath { get; set; } = "";
    /// <summary>Local (files under StoragePath) or S3 (an S3-compatible bucket: AWS S3, Cloudflare R2, MinIO).</summary>
    public string Storage { get; set; } = "Local";
    public CaDesk.Infrastructure.Documents.S3StorageOptions S3 { get; set; } = new();
    public ScanOptions Scan { get; set; } = new();
}

public sealed class ScanOptions
{
    /// <summary>ClamAv = scan every file with clamd; None = store files as "not scanned".</summary>
    public string Mode { get; set; } = "None";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Streams the file to clamd (ClamAV daemon) with the INSTREAM command. clamd's StreamMaxLength must be at least the
/// largest file the firm allows (doc.max_size_mb), or large files stay "waiting for scan" with the reason shown.
/// </summary>
public sealed class ClamAvScanner(ScanOptions options, ILogger<ClamAvScanner> log) : IVirusScanner
{
    public bool Enabled => true;

    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(options.Host, options.Port, timeout.Token);
            await using var net = tcp.GetStream();

            await net.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            var buffer = new byte[64 * 1024];
            var size = new byte[4];
            int read;
            while ((read = await content.ReadAsync(buffer, timeout.Token)) > 0)
            {
                BinaryPrimitives.WriteUInt32BigEndian(size, (uint)read);
                await net.WriteAsync(size, timeout.Token);
                await net.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            BinaryPrimitives.WriteUInt32BigEndian(size, 0);
            await net.WriteAsync(size, timeout.Token);
            await net.FlushAsync(timeout.Token);

            var reply = await ReadReplyAsync(net, timeout.Token);
            return Parse(reply);
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning("Virus scanner at {Host}:{Port} did not answer: {Message}", options.Host, options.Port, ex.Message);
            return ScanResult.Failed("The virus scanner did not answer; the file will be scanned again shortly.");
        }
    }

    /// <summary>"stream: OK", "stream: Win.Test.EICAR_HDB-1 FOUND", "INSTREAM size limit exceeded. ERROR".</summary>
    internal static ScanResult Parse(string reply)
    {
        reply = reply.Trim('\0', ' ', '\n', '\r');
        if (reply.EndsWith("OK", StringComparison.Ordinal)) return ScanResult.Ok();
        if (reply.EndsWith("FOUND", StringComparison.Ordinal))
        {
            var name = reply.StartsWith("stream:", StringComparison.Ordinal) ? reply["stream:".Length..] : reply;
            return ScanResult.Found(name[..^"FOUND".Length].Trim());
        }
        return ScanResult.Failed(reply.Length == 0 ? "The virus scanner gave no answer." : $"Virus scanner: {reply}");
    }

    private static async Task<string> ReadReplyAsync(NetworkStream net, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new byte[256];
        while (sb.Length < 4096)
        {
            var n = await net.ReadAsync(buffer, ct);
            if (n == 0) break;
            var text = Encoding.ASCII.GetString(buffer, 0, n);
            var end = text.IndexOf('\0');
            if (end >= 0) { sb.Append(text[..end]); break; }
            sb.Append(text);
        }
        return sb.ToString();
    }
}

/// <summary>Scanning switched off (Documents:Scan:Mode = None): files are stored and marked "not scanned".</summary>
public sealed class NoVirusScanner : IVirusScanner
{
    public bool Enabled => false;
    public Task<ScanResult> ScanAsync(Stream content, CancellationToken ct) => Task.FromResult(ScanResult.Failed("Scanning is off."));
}
