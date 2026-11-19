using System.Text.RegularExpressions;
using CaDesk.Application.Documents;

namespace CaDesk.Infrastructure.Documents;

/// <summary>
/// Document bytes on a local disk or mounted volume (Documents:StoragePath). Back this folder up with the database.
/// Keys are made by the server ("firm/yyyy/MM/id"); anything else is refused, so no path can leave the root.
/// Files are written to a temporary name and moved into place, so a half-written file is never served.
/// </summary>
public sealed partial class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(string root)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(_root);
    }

    [GeneratedRegex("^[0-9a-f]{32}/[0-9]{4}/[0-9]{2}/[0-9a-f]{32}$")]
    private static partial Regex KeyPattern();

    private string PathOf(string key)
    {
        if (!KeyPattern().IsMatch(key)) throw new ArgumentException("Not a valid storage key.", nameof(key));
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Not a valid storage key.", nameof(key));
        return full;
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await content.CopyToAsync(file, ct);
            File.Move(temp, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken ct)
    {
        var path = PathOf(key);
        Stream? s = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
        return Task.FromResult(s);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathOf(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
