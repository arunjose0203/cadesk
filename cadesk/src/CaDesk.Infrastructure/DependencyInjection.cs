using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Infrastructure.Audit;
using CaDesk.Infrastructure.Configuration;
using CaDesk.Infrastructure.Persistence;
using CaDesk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CaDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, DatabaseOptions db)
    {
        services.AddSingleton(db);
        services.AddSingleton<DbMigrator>();
        services.AddSingleton<PlatformDb>();

        // One TenantContext per request, exposed read-only and write-only.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<TenantContext>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<RequestContext>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<AuditWriter>();
        services.AddScoped<CaDesk.Application.Documents.IAuditRecorder>(sp => sp.GetRequiredService<AuditWriter>());
        services.AddDbContext<AppDbContext>((sp, o) => o
            .UseNpgsql(db.AppConnectionString)
            .AddInterceptors(
                sp.GetRequiredService<TenantConnectionInterceptor>(),
                sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // settings: per-firm snapshot cached in memory, invalidated by config_version
        services.AddMemoryCache();
        services.AddScoped<SettingsService>();
        services.AddScoped<ISettings>(sp => sp.GetRequiredService<SettingsService>());

        return services;
    }
}
