using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CaDesk.Api;

/// <summary>Turns known exceptions into clean ProblemDetails responses.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> log, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        // internals (stack traces, SQL, entity names) are only shown while developing or testing
        var showDetail = env.IsDevelopment() || env.IsEnvironment("Testing");

        IResult? result = ex switch
        {
            RequestValidationException v => Results.ValidationProblem(
                v.Errors.ToDictionary(e => e.Key, e => e.Value)),
            AuthFailedException a => Results.Problem(statusCode: a.StatusCode, title: a.Message),
            BusinessRuleException b => Results.Problem(statusCode: b.StatusCode, title: b.Message),
            ArgumentException arg => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: arg.Message),
            DbUpdateConcurrencyException conc => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Changed by someone else",
                detail: showDetail
                    ? "Reload and try again. " + string.Join("; ", conc.Entries.Select(e => $"{e.Entity.GetType().Name}: {e.State}"))
                    : "Reload and try again."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Already exists", detail: "A record with the same unique value already exists."),
            TenantNotSetException => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "No firm", detail: ex.Message),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge, title: "The file is too large."),
            BadHttpRequestException b => Results.Problem(statusCode: b.StatusCode, title: "The request could not be read."),
            // services use these for "not found in this firm" and "not allowed in this state"
            KeyNotFoundException when ThrownByCaDesk(ex) =>
                Results.Problem(statusCode: StatusCodes.Status404NotFound, title: ex.Message),
            InvalidOperationException when ThrownByCaDesk(ex) =>
                Results.Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message),
            _ => null,
        };

        if (result is null)
        {
            log.LogError(ex, "Unhandled {Exception} on {Method} {Path}", ex.GetType().Name, ctx.Request.Method, ctx.Request.Path);
            result = Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Something went wrong", detail: showDetail ? ex.ToString() : null);
        }
        else log.LogInformation("Handled {Exception}: {Message}", ex.GetType().Name, ex.Message);

        await result.ExecuteAsync(ctx);
        return true;
    }

    /// <summary>True when our own code threw it, not EF Core or the framework (those are real bugs and stay 500).</summary>
    private static bool ThrownByCaDesk(Exception ex) =>
        ex.TargetSite?.DeclaringType?.FullName?.StartsWith("CaDesk.", StringComparison.Ordinal) == true;
}
