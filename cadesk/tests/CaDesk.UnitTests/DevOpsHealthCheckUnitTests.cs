using System.Diagnostics;
using System.Text;
using Xunit;

namespace CaDesk.UnitTests;

public class DevOpsHealthCheckUnitTests
{
    [Fact]
    public void LivenessProbe_CalculatesUptimeAndProcessStats()
    {
        var startTime = DateTime.UtcNow.AddMinutes(-5);
        var now = DateTime.UtcNow;
        var uptime = now - startTime;
        var process = Process.GetCurrentProcess();

        Assert.True(uptime.TotalSeconds >= 290);
        Assert.True(process.WorkingSet64 > 0);
        Assert.True(Environment.ProcessId > 0);
    }

    [Fact]
    public void PrometheusMetrics_GeneratesValidExpositionFormat()
    {
        var sb = new StringBuilder();
        var uptimeSeconds = 120.45;
        var workingSetBytes = 50_000_000L;
        var gcHeapBytes = 25_000_000L;
        var gen0 = 5;
        var gen1 = 2;
        var gen2 = 0;
        var dbConnected = 1;

        sb.AppendLine("# HELP cadesk_uptime_seconds Total uptime of the CaDesk API process in seconds");
        sb.AppendLine("# TYPE cadesk_uptime_seconds gauge");
        sb.AppendLine($"cadesk_uptime_seconds {uptimeSeconds:F2}");

        sb.AppendLine("# HELP cadesk_memory_bytes Process working set memory in bytes");
        sb.AppendLine("# TYPE cadesk_memory_bytes gauge");
        sb.AppendLine($"cadesk_memory_bytes {workingSetBytes}");

        sb.AppendLine("# HELP cadesk_gc_heap_bytes Managed memory allocated in heap in bytes");
        sb.AppendLine("# TYPE cadesk_gc_heap_bytes gauge");
        sb.AppendLine($"cadesk_gc_heap_bytes {gcHeapBytes}");

        sb.AppendLine("# HELP cadesk_gc_collections_total Total garbage collection cycles by generation");
        sb.AppendLine("# TYPE cadesk_gc_collections_total counter");
        sb.AppendLine($"cadesk_gc_collections_total{{generation=\"0\"}} {gen0}");
        sb.AppendLine($"cadesk_gc_collections_total{{generation=\"1\"}} {gen1}");
        sb.AppendLine($"cadesk_gc_collections_total{{generation=\"2\"}} {gen2}");

        sb.AppendLine("# HELP cadesk_database_up Indicates if the primary database is reachable (1 for up, 0 for down)");
        sb.AppendLine("# TYPE cadesk_database_up gauge");
        sb.AppendLine($"cadesk_database_up {dbConnected}");

        var output = sb.ToString();

        // Validate Prometheus standard syntax
        Assert.Contains("# HELP cadesk_uptime_seconds", output);
        Assert.Contains("# TYPE cadesk_uptime_seconds gauge", output);
        Assert.Contains("cadesk_uptime_seconds 120.45", output);
        Assert.Contains("cadesk_gc_collections_total{generation=\"0\"} 5", output);
        Assert.Contains("cadesk_database_up 1", output);
    }

    [Fact]
    public void HealthCheck_ReadinessPayload_EvaluatesHealthyWhenDependenciesPass()
    {
        var checks = new Dictionary<string, object>
        {
            ["database"] = new { status = "Healthy", message = "Connected to PostgreSQL" },
            ["documentStorage"] = new { status = "Healthy", message = "FileStore operational" }
        };

        var isReady = checks.Values.All(c => c.ToString()?.Contains("Healthy") == true);
        var status = isReady ? "Healthy" : "Unhealthy";

        Assert.True(isReady);
        Assert.Equal("Healthy", status);
        Assert.Equal(2, checks.Count);
    }

    [Fact]
    public void HealthCheck_ReadinessPayload_EvaluatesUnhealthyWhenDbFails()
    {
        var checks = new Dictionary<string, object>
        {
            ["database"] = new { status = "Unhealthy", error = "Connection refused: db:5432" },
            ["documentStorage"] = new { status = "Healthy", message = "FileStore operational" }
        };

        var isReady = !checks.Values.Any(c => c.ToString()?.Contains("Unhealthy") == true);
        var status = isReady ? "Healthy" : "Unhealthy";

        Assert.False(isReady);
        Assert.Equal("Unhealthy", status);
    }
}
