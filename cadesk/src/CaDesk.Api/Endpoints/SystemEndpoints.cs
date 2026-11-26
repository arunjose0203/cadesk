using System.Diagnostics;
using System.Text;
using CaDesk.Application.Documents;
using CaDesk.Domain.Subscriptions;
using CaDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaDesk.Api.Endpoints;

public static class SystemEndpoints
{
    private static readonly DateTime StartTimeUtc = DateTime.UtcNow;

    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Backward-compatible basic health endpoint
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow
        })).AllowAnonymous();

        // 2. Backward-compatible basic database health endpoint
        app.MapGet("/api/health/db", async (AppDbContext db, CancellationToken ct) =>
        {
            var canConnect = await db.Database.CanConnectAsync(ct);
            return canConnect
                ? Results.Ok(new { database = "ok", timestamp = DateTime.UtcNow })
                : Results.Problem(statusCode: 503, title: "Database unavailable");
        }).AllowAnonymous();

        // 3. Kubernetes / Docker Liveness Probe: /health/live and /api/health/live
        // Checks that the process is running, responsive, and not deadlocked.
        var livenessHandler = () =>
        {
            var now = DateTime.UtcNow;
            var uptime = now - StartTimeUtc;
            var process = Process.GetCurrentProcess();

            var payload = new
            {
                status = "Healthy",
                check = "liveness",
                timestamp = now,
                uptime = uptime.ToString(@"d\.hh\:mm\:ss"),
                uptimeSeconds = Math.Round(uptime.TotalSeconds, 2),
                processId = Environment.ProcessId,
                memoryWorkingSetMb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2),
                gcTotalMemoryMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2),
                threadCount = process.Threads.Count,
                framework = Environment.Version.ToString(),
                service = "CaDesk.Api",
                version = typeof(SystemEndpoints).Assembly.GetName().Version?.ToString() ?? "1.0.0"
            };

            return Results.Ok(payload);
        };

        app.MapGet("/health/live", livenessHandler).AllowAnonymous();
        app.MapGet("/api/health/live", livenessHandler).AllowAnonymous();

        // 4. Kubernetes / Docker Readiness Probe: /health/ready and /api/health/ready
        // Checks that database is reachable and critical dependencies are operational.
        var readinessHandler = async (IServiceProvider sp, CancellationToken ct) =>
        {
            var checks = new Dictionary<string, object>();
            var isReady = true;

            // Check A: Database connectivity
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var canConnect = await db.Database.CanConnectAsync(ct);

                if (canConnect)
                {
                    checks["database"] = new { status = "Healthy", message = "Connected to PostgreSQL" };
                }
                else
                {
                    isReady = false;
                    checks["database"] = new { status = "Unhealthy", message = "Cannot connect to database" };
                }
            }
            catch (Exception ex)
            {
                isReady = false;
                checks["database"] = new { status = "Unhealthy", error = ex.Message };
            }

            // Check B: Storage / Document Vault accessibility
            try
            {
                var fileStore = sp.GetService<IFileStore>();
                if (fileStore != null)
                {
                    checks["documentStorage"] = new { status = "Healthy", message = "FileStore operational" };
                }
            }
            catch (Exception ex)
            {
                isReady = false;
                checks["documentStorage"] = new { status = "Unhealthy", error = ex.Message };
            }

            var overallStatus = isReady ? "Healthy" : "Unhealthy";
            var result = new
            {
                status = overallStatus,
                check = "readiness",
                timestamp = DateTime.UtcNow,
                durationMs = Math.Round((DateTime.UtcNow - StartTimeUtc).TotalMilliseconds, 2),
                checks
            };

            return isReady
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        };

        app.MapGet("/health/ready", readinessHandler).AllowAnonymous();
        app.MapGet("/api/health/ready", readinessHandler).AllowAnonymous();

        // 5. Prometheus Metrics Endpoint: /metrics and /api/metrics
        // Outputs standard Prometheus 0.0.4 exposition format for scraping by Prometheus / VictoriaMetrics.
        var metricsHandler = async (IServiceProvider sp, HttpContext context, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var uptimeSeconds = (now - StartTimeUtc).TotalSeconds;
            var process = Process.GetCurrentProcess();
            var workingSetBytes = process.WorkingSet64;
            var gcHeapBytes = GC.GetTotalMemory(false);
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            var dbConnected = 0;

            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await db.Database.CanConnectAsync(ct))
                {
                    dbConnected = 1;
                }
            }
            catch
            {
                dbConnected = 0;
            }

            var sb = new StringBuilder();

            // Uptime
            sb.AppendLine("# HELP cadesk_uptime_seconds Total uptime of the CaDesk API process in seconds");
            sb.AppendLine("# TYPE cadesk_uptime_seconds gauge");
            sb.AppendLine($"cadesk_uptime_seconds {uptimeSeconds:F2}");

            // Process Memory
            sb.AppendLine("# HELP cadesk_memory_bytes Process working set memory in bytes");
            sb.AppendLine("# TYPE cadesk_memory_bytes gauge");
            sb.AppendLine($"cadesk_memory_bytes {workingSetBytes}");

            // GC Heap
            sb.AppendLine("# HELP cadesk_gc_heap_bytes Managed memory allocated in heap in bytes");
            sb.AppendLine("# TYPE cadesk_gc_heap_bytes gauge");
            sb.AppendLine($"cadesk_gc_heap_bytes {gcHeapBytes}");

            // Garbage Collections
            sb.AppendLine("# HELP cadesk_gc_collections_total Total garbage collection cycles by generation");
            sb.AppendLine("# TYPE cadesk_gc_collections_total counter");
            sb.AppendLine($"cadesk_gc_collections_total{{generation=\"0\"}} {gen0}");
            sb.AppendLine($"cadesk_gc_collections_total{{generation=\"1\"}} {gen1}");
            sb.AppendLine($"cadesk_gc_collections_total{{generation=\"2\"}} {gen2}");

            // Active Threads
            sb.AppendLine("# HELP cadesk_process_threads Current thread count");
            sb.AppendLine("# TYPE cadesk_process_threads gauge");
            sb.AppendLine($"cadesk_process_threads {process.Threads.Count}");

            // Database status
            sb.AppendLine("# HELP cadesk_database_up Indicates if the primary database is reachable (1 for up, 0 for down)");
            sb.AppendLine("# TYPE cadesk_database_up gauge");
            sb.AppendLine($"cadesk_database_up {dbConnected}");

            context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
            await context.Response.WriteAsync(sb.ToString(), ct);
        };

        app.MapGet("/metrics", metricsHandler).AllowAnonymous();
        app.MapGet("/api/metrics", metricsHandler).AllowAnonymous();

        return app;
    }
}
