using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CaDesk.Api;

/// <summary>Turns known exceptions into clean ProblemDetails responses.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        IResult? result = ex switch
        {
            RequestValidationException v => Results.ValidationProblem(
                v.Errors.ToDictionary(e => e.Key, e => e.Value)),
            AuthFailedException a => Results.Problem(statusCode: a.StatusCode, title: a.Message),
            BusinessRuleException b => Results.Problem(statusCode: b.StatusCode, title: b.Message),
            ArgumentException arg => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: arg.Message),
            DbUpdateConcurrencyException => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Changed by someone else", detail: "Reload and try again."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Already exists", detail: "A record with the same unique value already exists."),
            TenantNotSetException => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "No firm", detail: ex.Message),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge, title: "The file is too large."),
            BadHttpRequestException b => Results.Problem(statusCode: b.StatusCode, title: "The request could not be read."),
            _ => null,
        };
        if (result is null) return false;

        log.LogInformation("Handled {Exception}: {Message}", ex.GetType().Name, ex.Message);
        await result.ExecuteAsync(ctx);
        return true;
    }
}
