namespace CaDesk.Application.Documents;

/// <summary>Where document bytes live. Keys are made by the server only (never from user input).</summary>
public interface IFileStore
{
    /// <summary>Writes the whole stream under the key; the file appears only when complete.</summary>
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    /// <summary>Null when the file is not there.</summary>
    Task<Stream?> OpenAsync(string key, CancellationToken ct);
    /// <summary>No error if it's already gone.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed record ScanResult(string Outcome, string? Detail)
{
    public const string Clean = "clean";
    public const string Infected = "infected";
    /// <summary>The scanner couldn't answer (down, timed out): try again later.</summary>
    public const string Error = "error";

    public static ScanResult Ok() => new(Clean, null);
    public static ScanResult Found(string name) => new(Infected, name);
    public static ScanResult Failed(string why) => new(Error, why);
}

/// <summary>Virus scanner (ClamAV on the server). When <see cref="Enabled"/> is false, files are stored as "not scanned".</summary>
public interface IVirusScanner
{
    bool Enabled { get; }
    Task<ScanResult> ScanAsync(Stream content, CancellationToken ct);
}

/// <summary>Records sensitive actions that aren't data changes (downloads), in the audit log.</summary>
public interface IAuditRecorder
{
    Task RecordAsync(string entity, string? entityId, string action, object? oldValues, object? newValues, CancellationToken ct);
}

/// <summary>An uploaded file as the use case sees it: a name, a length and a way to read it (more than once).</summary>
public sealed record UploadFile(string FileName, long Length, Func<Stream> Open);
