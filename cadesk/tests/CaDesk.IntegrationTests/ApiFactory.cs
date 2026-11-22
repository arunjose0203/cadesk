using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaDesk.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Starts the real API against a real PostgreSQL database (cadesk_test).
/// Override the connection strings with CADESK_TEST_APP_CS / CADESK_TEST_OWNER_CS.
/// Each fixture signs up two fresh firms, so tests can run repeatedly on the same database.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Correct-Horse-42";

    public static readonly string AppCs = Env("CADESK_TEST_APP_CS",
        "Host=localhost;Port=5432;Database=cadesk_test;Username=cadesk_app;Password=app_dev_pw");
    public static readonly string OwnerCs = Env("CADESK_TEST_OWNER_CS",
        "Host=localhost;Port=5432;Database=cadesk_test;Username=cadesk_owner;Password=owner_dev_pw");

    /// <summary>Real time plus an adjustable offset, for lockout / MFA / token-reuse tests.</summary>
    public AdjustableClock Clock { get; } = new();

    public SignedUp FirmA { get; private set; } = null!;
    public SignedUp FirmB { get; private set; } = null!;

    public ApiFactory()
    {
        // Environment variables are read by WebApplication.CreateBuilder, before Program reads config.
        Environment.SetEnvironmentVariable("ConnectionStrings__App", AppCs);
        Environment.SetEnvironmentVariable("ConnectionStrings__Owner", OwnerCs);
        Environment.SetEnvironmentVariable("RateLimits__AuthPerMinute", "100000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:App", AppCs);
        builder.UseSetting("ConnectionStrings:Owner", OwnerCs);
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<TimeProvider>(Clock);
            s.AddSingleton<CaDesk.Application.Documents.IVirusScanner, TestVirusScanner>();
        });
    }

    public async Task InitializeAsync()
    {
        _ = Server; // starts the app, which applies migrations
        FirmA = await SignUpAsync("Firm A");
        FirmB = await SignUpAsync("Firm B");
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>HTTPS client that does NOT store cookies, so tests control the refresh cookie exactly.</summary>
    public HttpClient Raw() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
    });

    public HttpClient WithToken(string accessToken)
    {
        var c = Raw();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return c;
    }

    /// <summary>A client with a token holding every permission, for data-isolation tests.</summary>
    public HttpClient ClientFor(Guid? firmId)
    {
        var token = Services.GetRequiredService<TokenIssuer>()
            .Issue(Guid.NewGuid(), firmId, ["Partner"], TestData.AllPermissions);
        return WithToken(token);
    }

    public async Task<SignedUp> SignUpAsync(string firmName, string? email = null)
    {
        email ??= $"{Guid.NewGuid():N}@test.example";
        var res = await Raw().PostAsJsonAsync("/api/auth/signup",
            new SignUpRequest($"{firmName} {Guid.NewGuid():N}"[..20], "IN", "Test Partner", email, Password));
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<AuthResult>())!;
        return new SignedUp(body.User!.FirmId, body.User.Id, email, body.AccessToken!, TestData.RefreshCookie(res)!);
    }

    private static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;
}

/// <summary>
/// Stands in for ClamAV: a file containing the EICAR test string is "infected"; while <see cref="Down"/> is set,
/// the scanner doesn't answer (only DocumentTests sets it).
/// </summary>
public sealed class TestVirusScanner : CaDesk.Application.Documents.IVirusScanner
{
    /// <summary>The standard harmless test "virus", put together at run time so this source file itself isn't flagged.</summary>
    public static readonly string Eicar = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$" + "EICAR-STANDARD-" + "ANTIVIRUS-TEST-FILE!$H+H*";
    public static volatile bool Down;

    public bool Enabled => true;

    public async Task<CaDesk.Application.Documents.ScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        if (Down) return CaDesk.Application.Documents.ScanResult.Failed("Test scanner is down.");
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var text = System.Text.Encoding.ASCII.GetString(ms.ToArray());
        return text.Contains(Eicar, StringComparison.Ordinal)
            ? CaDesk.Application.Documents.ScanResult.Found("Eicar-Test-Signature")
            : CaDesk.Application.Documents.ScanResult.Ok();
    }
}

public sealed record SignedUp(Guid FirmId, Guid UserId, string Email, string AccessToken, string RefreshToken);

public sealed class AdjustableClock : TimeProvider
{
    public TimeSpan Offset { get; set; }
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
}

public static class TestData
{
    public static readonly string[] AllPermissions =
    [
        "advancetax.calculate", "advancetax.manage", "advancetax.read",
        "assets.depreciate", "assets.manage", "assets.read",
        "audit.archive", "audit.manage", "audit.read", "audit.review",
        "auditcommittee.manage", "auditcommittee.read", "auditcommittee.signoff",
        "bank.manage", "bank.read", "bank.reconcile",
        "billing.manage", "billing.read",
        "branchaudit.consolidate", "branchaudit.manage", "branchaudit.read",
        "caro.finalize", "caro.manage", "caro.read",
        "clients.delete", "clients.read", "clients.write",
        "compliance.manage", "compliance.read",
        "computations.approve", "computations.run",
        "documents.delete", "documents.read", "documents.upload",
        "dsc.manage", "dsc.read",
        "einvoice.cancel", "einvoice.generate", "einvoice.read",
        "engagement.manage", "engagement.read",
        "eqr.review", "extractions.approve",
        "gst.read", "gst.reconcile",
        "kyc.manage", "kyc.read",
        "mca.manage", "mca.read",
        "notices.approve", "notices.manage", "notices.read",
        "payroll.manage", "payroll.process", "payroll.read",
        "portal.manage", "portal.read",
        "presumptive.approve", "presumptive.manage", "presumptive.read",
        "reports.manage", "reports.read",
        "roles.manage",
        "schedule3.finalize", "schedule3.manage", "schedule3.read",
        "service_requests.manage", "settings.manage",
        "tasks.manage", "tasks.read", "tasks.review",
        "taxaudit.finalize", "taxaudit.manage", "taxaudit.read",
        "tds.manage", "tds.read",
        "timesheets.log", "timesheets.manage", "timesheets.read",
        "users.manage", "users.read",
        "workpapers.manage", "workpapers.read",
    ];

    /// <summary>Value of the cadesk_rt cookie from a response, or null.</summary>
    public static string? RefreshCookie(HttpResponseMessage res)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
        foreach (var c in cookies)
        {
            if (!c.StartsWith("cadesk_rt=", StringComparison.Ordinal)) continue;
            var value = c["cadesk_rt=".Length..].Split(';')[0];
            return value.Length == 0 ? null : value;
        }
        return null;
    }

    public static HttpRequestMessage WithRefreshCookie(string path, string refreshToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Add("Cookie", $"cadesk_rt={refreshToken}");
        return req;
    }
}
