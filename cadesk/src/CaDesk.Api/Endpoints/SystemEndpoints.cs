using CaDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

        app.MapGet("/api/health/db", async (AppDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(new { database = "ok" })
                : Results.Problem(statusCode: 503, title: "Database unavailable")).AllowAnonymous();

        return app;
    }
}
