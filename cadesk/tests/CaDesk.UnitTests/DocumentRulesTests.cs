using System.IO.Compression;
using System.Text;
using CaDesk.Api.Documents;
using CaDesk.Application.Documents;

namespace CaDesk.UnitTests;

public class DocumentRulesTests
{
    private static MemoryStream S(string s) => new(Encoding.ASCII.GetBytes(s));
    private static MemoryStream S(params byte[] b) => new(b);

    [Theory]
    [InlineData("Scan 01.PDF", "pdf")]
    [InlineData("photo.jpeg", "jpeg")]
    [InlineData(@"C:\docs\ledger.v2.xlsx", "xlsx")]
    [InlineData("README", "")]
    [InlineData(".hidden", "")]
    [InlineData("trailing.", "")]
    public void Extension_is_the_last_part_in_lower_case(string name, string ext) => Assert.Equal(ext, FileTypes.ExtensionOf(name));

    [Fact]
    public void Jpeg_counts_as_jpg_and_unknown_types_have_no_code()
    {
        Assert.Equal("jpg", FileTypes.CodeOf("jpeg"));
        Assert.Null(FileTypes.CodeOf("exe"));
        Assert.Null(FileTypes.CodeOf("html"));
        Assert.Equal("application/octet-stream", FileTypes.ContentTypeOf("exe"));
    }

    [Fact]
    public void Content_must_match_the_extension()
    {
        Assert.Null(FileTypes.CheckContent("pdf", S("%PDF-1.7\n...")));
        Assert.NotNull(FileTypes.CheckContent("pdf", S("<html>")));
        Assert.Null(FileTypes.CheckContent("jpg", S(0xFF, 0xD8, 0xFF, 0xE0, 0, 0)));
        Assert.NotNull(FileTypes.CheckContent("jpg", S(0x89, 0x50, 0x4E, 0x47)));
        Assert.Null(FileTypes.CheckContent("png", S(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1)));
        Assert.Null(FileTypes.CheckContent("xls", S(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0)));
        Assert.Null(FileTypes.CheckContent("csv", S("\uFEFFdate,amount\n1,2")));
        Assert.NotNull(FileTypes.CheckContent("csv", S("  <script>alert(1)</script>")));
        Assert.NotNull(FileTypes.CheckContent("csv", S(0x4D, 0x5A, 0x00, 0x01)));     // binary
        Assert.NotNull(FileTypes.CheckContent("exe", S("MZ")));
    }

    [Fact]
    public void Generated_json_must_look_like_json()
    {
        Assert.Equal("json", FileTypes.CodeOf("json"));
        Assert.Equal("application/json", FileTypes.ContentTypeOf("json"));
        Assert.Null(FileTypes.CheckContent("json", S("{\"gstin\":\"32AAACK1234M1ZU\",\"fp\":\"102026\"}")));
        Assert.Null(FileTypes.CheckContent("json", S("\n  [1,2]")));
        Assert.NotNull(FileTypes.CheckContent("json", S("<html>")));
        Assert.NotNull(FileTypes.CheckContent("json", S(0x7B, 0x00, 0x7D)));            // binary
    }

    [Fact]
    public void Office_files_must_be_real_packages_without_macros()
    {
        Assert.Null(FileTypes.CheckContent("xlsx", Zip("[Content_Types].xml", "xl/workbook.xml")));
        Assert.Null(FileTypes.CheckContent("docx", Zip("[Content_Types].xml", "word/document.xml")));
        Assert.NotNull(FileTypes.CheckContent("xlsx", Zip("[Content_Types].xml", "word/document.xml")));
        Assert.Contains("macros", FileTypes.CheckContent("xlsx", Zip("[Content_Types].xml", "xl/workbook.xml", "xl/vbaProject.bin")));
        Assert.NotNull(FileTypes.CheckContent("docx", S("PK\u0003\u0004 not really a zip")));
    }

    private static MemoryStream Zip(params string[] entries)
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var e in entries) using (var w = new StreamWriter(z.CreateEntry(e).Open())) w.Write("<x/>");
        ms.Position = 0;
        return ms;
    }

    [Theory]
    [InlineData(@"C:\Users\me\Desktop\Form 16.pdf", "Form 16.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("in<v>oi|ce?.pdf", "in_v_oi_ce_.pdf")]
    [InlineData("  ..  ", "file")]
    [InlineData("tab\tand\nnew line.pdf", "tab_and_new line.pdf")]
    public void File_names_lose_folders_and_unsafe_characters(string given, string safe) => Assert.Equal(safe, DocumentRules.SafeFileName(given));

    [Fact]
    public void Long_names_keep_their_extension()
    {
        var safe = DocumentRules.SafeFileName(new string('a', 400) + ".pdf");
        Assert.Equal(255, safe.Length);
        Assert.EndsWith(".pdf", safe);
        Assert.Equal("Bank April", DocumentRules.DisplayName(null, "Bank April.pdf"));
        Assert.Equal("Given name", DocumentRules.DisplayName("  Given   name ", "x.pdf"));
    }

    [Fact]
    public void Years_follow_the_firms_financial_year()
    {
        Assert.Equal("2026-27", DocumentRules.FyOf(new DateOnly(2026, 4, 1), 4));
        Assert.Equal("2025-26", DocumentRules.FyOf(new DateOnly(2026, 3, 31), 4));
        Assert.Equal("2026", DocumentRules.FyOf(new DateOnly(2026, 3, 31), 1));

        Assert.True(DocumentRules.IsValidFy("2026-27", 4));
        Assert.True(DocumentRules.IsValidFy("1999-00", 4));
        Assert.False(DocumentRules.IsValidFy("2026-28", 4));
        Assert.False(DocumentRules.IsValidFy("2026", 4));
        Assert.True(DocumentRules.IsValidFy("2026", 1));
        Assert.False(DocumentRules.IsValidFy("2026-27", 1));
        Assert.False(DocumentRules.IsValidFy("26-27", 4));

        Assert.Equal(new DateOnly(2027, 3, 31), DocumentRules.FyEnd("2026-27", 4));
        Assert.Equal(new DateOnly(2026, 12, 31), DocumentRules.FyEnd("2026", 1));

        var years = DocumentRules.RecentYears(new DateOnly(2026, 11, 19), 4);
        Assert.Equal("2027-28", years[0]);
        Assert.Contains("2026-27", years);
        Assert.Equal(years.Count, years.Distinct().Count());
    }

    [Fact]
    public void Storage_keys_are_made_only_of_ids_and_dates()
    {
        var firm = Guid.Parse("0193b2c4-0000-7000-8000-000000000001");
        var doc = Guid.Parse("0193b2c4-0000-7000-8000-0000000000ff");
        Assert.Equal("0193b2c4000070008000000000000001/2026/11/0193b2c40000700080000000000000ff",
            DocumentRules.StorageKey(firm, doc, new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("stream: OK", "clean", null)]
    [InlineData("stream: Win.Test.EICAR_HDB-1 FOUND\0", "infected", "Win.Test.EICAR_HDB-1")]
    [InlineData("INSTREAM size limit exceeded. ERROR", "error", null)]
    [InlineData("", "error", null)]
    public void ClamAv_replies_are_understood(string reply, string outcome, string? virus)
    {
        var r = ClamAvScanner.Parse(reply);
        Assert.Equal(outcome, r.Outcome);
        if (virus is not null) Assert.Equal(virus, r.Detail);
    }
}

public class LocalFileStoreTests
{
    [Fact]
    public async Task Saves_opens_and_deletes_and_refuses_keys_it_did_not_make()
    {
        var root = Path.Combine(Path.GetTempPath(), "cadesk-store-" + Guid.NewGuid().ToString("N"));
        var store = new CaDesk.Infrastructure.Documents.LocalFileStore(root);
        var key = DocumentRules.StorageKey(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        try
        {
            await store.SaveAsync(key, new MemoryStream([1, 2, 3]), CancellationToken.None);
            await using (var s = await store.OpenAsync(key, CancellationToken.None))
            {
                Assert.NotNull(s);
                var ms = new MemoryStream();
                await s!.CopyToAsync(ms);
                Assert.Equal(new byte[] { 1, 2, 3 }, ms.ToArray());
            }
            await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(key, new MemoryStream([9]), CancellationToken.None));   // never overwritten
            Assert.Empty(Directory.GetFiles(root, "*.part", SearchOption.AllDirectories));

            await store.DeleteAsync(key, CancellationToken.None);
            Assert.Null(await store.OpenAsync(key, CancellationToken.None));
            await store.DeleteAsync(key, CancellationToken.None);                                                             // already gone: fine

            foreach (var bad in new[] { "../../etc/passwd", "a/b/c", key + "/../x", key.ToUpperInvariant() })
                await Assert.ThrowsAsync<ArgumentException>(() => store.OpenAsync(bad, CancellationToken.None));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
