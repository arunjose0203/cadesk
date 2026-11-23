using System.Diagnostics;
using System.Threading.RateLimiting;
using CaDesk.Api;
using CaDesk.Api.Auth;
using CaDesk.Api.Configuration;
using CaDesk.Application.Configuration;
using CaDesk.Api.Endpoints;
using CaDesk.Api.Tenancy;
using CaDesk.Api.Users;
using CaDesk.Application.Clients;
using CaDesk.Infrastructure;
using CaDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- structured logging: JSON formatting in Production / Container environments
if (builder.Environment.IsProduction() || config.GetValue<string>("Logging:Console:FormatterName") == "json")
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
    });
}

// ---- database + tenancy
builder.Services.AddInfrastructure(new DatabaseOptions
{
    AppConnectionString = config.GetConnectionString("App")
        ?? throw new InvalidOperationException("ConnectionStrings:App is missing."),
    OwnerConnectionString = config.GetConnectionString("Owner")
        ?? throw new InvalidOperationException("ConnectionStrings:Owner is missing."),
    MigrateOnStartup = config.GetValue("Database:MigrateOnStartup", true),
});

// ---- use cases
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<ClientPartsService>();
builder.Services.AddScoped<ClientImportService>();
builder.Services.AddScoped<CaDesk.Application.Compliance.DeadlineService>();
builder.Services.AddScoped<CaDesk.Application.Tasks.TaskService>();

// ---- reminders: outbox + channels (Notifications:Email / Notifications:WhatsApp; "Log" mode sends nothing)
builder.Services.AddScoped<CaDesk.Application.Notifications.NotificationService>();
builder.Services.AddScoped<CaDesk.Application.Notifications.NotificationDispatcher>();
var emailOptions = config.GetSection("Notifications:Email").Get<CaDesk.Api.Notifications.EmailOptions>() ?? new();
var waOptions = config.GetSection("Notifications:WhatsApp").Get<CaDesk.Api.Notifications.WhatsAppOptions>() ?? new();
if (emailOptions.Mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
{
    if (emailOptions.Host.Length == 0 || emailOptions.FromAddress.Length == 0)
        throw new InvalidOperationException("Notifications:Email:Host and FromAddress are required when Mode is Smtp.");
    builder.Services.AddSingleton(emailOptions);
    builder.Services.AddSingleton<CaDesk.Application.Notifications.INotificationChannel, CaDesk.Api.Notifications.SmtpEmailChannel>();
}
else if (emailOptions.Mode.Equals("SendGrid", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(emailOptions);
    builder.Services.AddHttpClient<CaDesk.Api.Notifications.SendGridEmailChannel>();
    builder.Services.AddTransient<CaDesk.Application.Notifications.INotificationChannel>(sp =>
        sp.GetRequiredService<CaDesk.Api.Notifications.SendGridEmailChannel>());
}
else if (emailOptions.Mode.Equals("Ses", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(emailOptions);
    builder.Services.AddHttpClient<CaDesk.Api.Notifications.AwsSesEmailChannel>();
    builder.Services.AddTransient<CaDesk.Application.Notifications.INotificationChannel>(sp =>
        sp.GetRequiredService<CaDesk.Api.Notifications.AwsSesEmailChannel>());
}
else
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        Console.WriteLine("WARNING: Notifications:Email:Mode is not Smtp - reminder emails are only logged, not sent.");
    builder.Services.AddSingleton<CaDesk.Application.Notifications.INotificationChannel>(sp =>
        new CaDesk.Api.Notifications.LogChannel("email", sp.GetRequiredService<ILogger<CaDesk.Api.Notifications.LogChannel>>()));
}
if (waOptions.Mode.Equals("Cloud", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<CaDesk.Api.Notifications.WhatsAppCloudChannel>(c =>
    {
        c.BaseAddress = new Uri(waOptions.BaseUrl);
        c.Timeout = TimeSpan.FromSeconds(20);
    });
    builder.Services.AddTransient<CaDesk.Application.Notifications.INotificationChannel>(sp =>
        sp.GetRequiredService<CaDesk.Api.Notifications.WhatsAppCloudChannel>());
}
else
    builder.Services.AddSingleton<CaDesk.Application.Notifications.INotificationChannel>(sp =>
        new CaDesk.Api.Notifications.LogChannel("whatsapp", sp.GetRequiredService<ILogger<CaDesk.Api.Notifications.LogChannel>>()));
// ---- document vault: files on disk (Documents:StoragePath), virus scan with ClamAV (Documents:Scan:Mode = ClamAv | None)
builder.Services.AddScoped<CaDesk.Application.Documents.DocumentService>();
builder.Services.AddScoped<CaDesk.Application.Documents.PortalService>();
builder.Services.AddScoped<CaDesk.Application.Billing.BillingService>();
builder.Services.AddScoped<CaDesk.Application.Billing.TimesheetService>();
builder.Services.AddScoped<CaDesk.Application.Dsc.DscService>();
builder.Services.AddScoped<CaDesk.Application.Portal.ClientPortalService>();
builder.Services.AddScoped<CaDesk.Application.Tax.TaxComputationService>();
builder.Services.AddScoped<CaDesk.Application.Audit.AuditService>();
builder.Services.AddScoped<CaDesk.Application.Gst.GstReconciliationService>();
builder.Services.AddScoped<CaDesk.Application.Tds.TdsService>();
builder.Services.AddScoped<CaDesk.Application.Mca.IMcaComplianceService, CaDesk.Application.Mca.McaComplianceService>();
builder.Services.AddScoped<CaDesk.Application.Engagements.IEngagementService, CaDesk.Application.Engagements.EngagementService>();
builder.Services.AddScoped<CaDesk.Application.BankFeeds.IBankFeedService, CaDesk.Application.BankFeeds.BankFeedService>();
builder.Services.AddScoped<CaDesk.Application.Reports.IPracticeReportService, CaDesk.Application.Reports.PracticeReportService>();
builder.Services.AddScoped<CaDesk.Application.TaxNotices.ITaxNoticeService, CaDesk.Application.TaxNotices.TaxNoticeService>();
builder.Services.AddScoped<CaDesk.Application.Payroll.IPayrollService, CaDesk.Application.Payroll.PayrollService>();
builder.Services.AddScoped<CaDesk.Application.EInvoicing.IEInvoicingService, CaDesk.Application.EInvoicing.EInvoicingService>();
builder.Services.AddScoped<CaDesk.Application.FixedAssets.IFixedAssetService, CaDesk.Application.FixedAssets.FixedAssetService>();
builder.Services.AddScoped<CaDesk.Application.AdvanceTax.IAdvanceTaxService, CaDesk.Application.AdvanceTax.AdvanceTaxService>();
builder.Services.AddScoped<CaDesk.Application.Gstr9.IGstr9Service, CaDesk.Application.Gstr9.Gstr9Service>();
builder.Services.AddScoped<CaDesk.Application.Presumptive.PresumptiveTaxService>();
builder.Services.AddScoped<CaDesk.Application.Caro.CaroService>();
builder.Services.AddScoped<CaDesk.Application.TaxAudit.Form3CdService>();
builder.Services.AddScoped<CaDesk.Application.Schedule3.Schedule3Service>();
builder.Services.AddScoped<CaDesk.Application.BranchAudit.BranchAuditService>();
builder.Services.AddScoped<CaDesk.Application.AuditCommittee.AuditCommitteeService>();
builder.Services.AddScoped<CaDesk.Application.ForeignRemittance.ForeignRemittanceService>();
builder.Services.AddScoped<CaDesk.Application.RelatedParty.RelatedPartyService>();
builder.Services.AddScoped<CaDesk.Application.InternalAudit.InternalAuditService>();
builder.Services.AddScoped<CaDesk.Application.AntiProfiteering.AntiProfiteeringService>();
builder.Services.AddScoped<CaDesk.Application.SecretarialAudit.SecretarialAuditService>();
builder.Services.AddScoped<CaDesk.Application.TransferPricing.TransferPricingService>();
builder.Services.AddScoped<CaDesk.Application.CostAudit.CostAuditService>();
builder.Services.AddScoped<CaDesk.Application.TrialBalance.TrialBalanceSyncEngine>();
builder.Services.AddScoped<CaDesk.Application.TrialBalance.TrialBalanceService>();
builder.Services.AddScoped<CaDesk.Application.Subscriptions.SubscriptionService>();
builder.Services.AddScoped<CaDesk.Application.Aqmm.AqmmService>();
builder.Services.AddScoped<CaDesk.Application.Notifications.CommunicationGatewayService>();
builder.Services.AddScoped<CaDesk.Application.SecurityCompliance.SecurityComplianceService>();
builder.Services.AddScoped<CaDesk.Application.PartnerCompanion.PartnerCompanionService>();
builder.Services.AddScoped<CaDesk.Application.ConnectedBanking.ConnectedBankingService>();
builder.Services.AddScoped<CaDesk.Application.TaxNotices.ITaxCopilotService, CaDesk.Application.TaxNotices.TaxCopilotService>();
var docOptions = config.GetSection("Documents").Get<CaDesk.Api.Documents.DocumentOptions>() ?? new();
var storagePath = docOptions.StoragePath;
if (storagePath.Length == 0)
{
    if (builder.Environment.IsEnvironment("Testing"))
        storagePath = Path.Combine(Path.GetTempPath(), "cadesk-test-documents");
    else if (builder.Environment.IsDevelopment())
        storagePath = Path.Combine(builder.Environment.ContentRootPath, "data", "documents");
    else
        throw new InvalidOperationException("Documents:StoragePath must be set outside Development (a folder that is backed up).");
}
builder.Services.AddSingleton<CaDesk.Application.Documents.IFileStore>(new CaDesk.Infrastructure.Documents.LocalFileStore(storagePath));
if (docOptions.Scan.Mode.Equals("ClamAv", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(docOptions.Scan);
    builder.Services.AddSingleton<CaDesk.Application.Documents.IVirusScanner, CaDesk.Api.Documents.ClamAvScanner>();
}
else
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        Console.WriteLine("WARNING: Documents:Scan:Mode is not ClamAv - uploaded files are not scanned for viruses.");
    builder.Services.AddSingleton<CaDesk.Application.Documents.IVirusScanner, CaDesk.Api.Documents.NoVirusScanner>();
}

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SecretStore>();
builder.Services.AddScoped<ISecretStore>(sp => sp.GetRequiredService<SecretStore>());
builder.Services.AddScoped<CaDesk.Api.Audit.AuditQueryService>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<CaDesk.Api.Compliance.DeadlineJob>();   // daily: next deadlines, extensions
    builder.Services.AddHostedService<CaDesk.Api.Notifications.NotificationJob>(); // every 5 min: reminders
    builder.Services.AddHostedService<CaDesk.Api.Documents.DocumentJob>();         // every 10 min: waiting scans, trash
}
builder.Services.AddHostedService<CaDesk.Api.Audit.AuditPartitionJob>(); // keeps next months' audit partitions ready

// ---- keys that protect MFA secrets: must survive restarts (mount this folder as a volume)
var dp = builder.Services.AddDataProtection().SetApplicationName("CaDesk");
if (config["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    dp.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
else if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException(
        "DataProtection:KeysPath must be set outside Development, or MFA secrets become unreadable after a restart.");

// ---- authentication (JWT) + authorization
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<TokenIssuer>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep "sub", "firm_id", "role", "perm" as they are
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.Key(),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
// check token lifetimes against the app's TimeProvider (same clock that issues them)
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((o, clock) =>
        o.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var skew = TimeSpan.FromSeconds(30);
            return (notBefore is null || notBefore.Value <= now + skew) && expires is { } exp && exp > now - skew;
        });
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.FirmMember, p => p.RequireAuthenticatedUser().RequireClaim(AppClaims.FirmId));

// ---- brute-force protection on sign-in endpoints (per client IP)
var authPerMinute = config.GetValue("RateLimits:AuthPerMinute", 20);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // client upload links (no sign-in): enough for a client sending a batch of files, not for guessing links
    var portalPerMinute = config.GetValue("RateLimits:PortalPerMinute", 60);
    o.AddPolicy(DocumentEndpoints.PortalRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = portalPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// ---- behind Caddy: trust X-Forwarded-For / -Proto ONLY from the proxy networks listed in config
// (e.g. the Docker network 172.16.0.0/12). Anyone else's X-Forwarded-For is ignored.
var proxyNetworks = config.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var cidr in proxyNetworks)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !System.Net.IPAddress.TryParse(parts[0], out _) || !int.TryParse(parts[1], out _))
            throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks entry '{cidr}' must look like 172.16.0.0/12.");
#pragma warning disable ASPDEPR005 // KnownNetworks: newer KnownIPNetworks alternative exists in .NET 10
        o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse(parts[0]), int.Parse(parts[1])));
#pragma warning restore ASPDEPR005
    }
});

// ---- errors
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (app.Services.GetRequiredService<DatabaseOptions>().MigrateOnStartup)
    await app.Services.GetRequiredService<DbMigrator>().MigrateAsync();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>(); // after authentication, before authorization
app.UseMiddleware<MfaSetupGate>();
app.UseAuthorization();

// ---- structured HTTP telemetry middleware (trace latency, status, firm & user)
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();

    var path = context.Request.Path.Value ?? string.Empty;
    // Skip health/metrics polling spam from logs unless request failed
    if ((!path.StartsWith("/health") && !path.StartsWith("/metrics") && !path.StartsWith("/api/health") && !path.StartsWith("/api/metrics")) || context.Response.StatusCode >= 400)
    {
        var firmId = context.User.FindFirst(AppClaims.FirmId)?.Value ?? "anonymous";
        var userId = context.User.FindFirst(AppClaims.Subject)?.Value ?? "anonymous";
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [Firm: {FirmId}] [User: {UserId}]",
            context.Request.Method,
            path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            firmId,
            userId);
    }
});

app.MapSystemEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapClientEndpoints();
app.MapComplianceEndpoints();
app.MapTaskEndpoints();
app.MapNotificationEndpoints();
app.MapDocumentEndpoints();
app.MapBillingEndpoints();
app.MapTimesheetEndpoints();
app.MapDscEndpoints();
app.MapClientPortalEndpoints();
app.MapTaxComputationEndpoints();
app.MapStatutoryAuditEndpoints();
app.MapGstReconciliationEndpoints();
app.MapTdsEndpoints();
app.MapMcaComplianceEndpoints();
app.MapEngagementEndpoints();
app.MapBankFeedEndpoints();
app.MapPracticeReportEndpoints();
app.MapTaxNoticeEndpoints();
app.MapPayrollEndpoints();
app.MapEInvoicingEndpoints();
app.MapFixedAssetEndpoints();
app.MapAdvanceTaxEndpoints();
app.MapGstr9Endpoints();
app.MapPresumptiveTaxEndpoints();
app.MapCaroEndpoints();
app.MapForm3CdEndpoints();
app.MapSchedule3Endpoints();
app.MapBranchAuditEndpoints();
app.MapAuditCommitteeEndpoints();
app.MapForeignRemittanceEndpoints();
app.MapRelatedPartyEndpoints();
app.MapInternalAuditEndpoints();
app.MapAntiProfiteeringEndpoints();
app.MapSecretarialAuditEndpoints();
app.MapTransferPricingEndpoints();
app.MapCostAuditEndpoints();
app.MapSettingsEndpoints();
app.MapAuditEndpoints();
app.MapTrialBalanceEndpoints();
app.MapSubscriptionEndpoints();
app.MapAqmmEndpoints();
app.MapCommunicationGatewayEndpoints();
app.MapSecurityComplianceEndpoints();
app.MapPartnerCompanionEndpoints();
app.MapConnectedBankingEndpoints();
app.MapTaxCopilotEndpoints();

app.Run();

public static class Policies
{
    public const string FirmMember = "FirmMember";
}

/// <summary>Lets integration tests start the API with WebApplicationFactory.</summary>
public partial class Program { }
