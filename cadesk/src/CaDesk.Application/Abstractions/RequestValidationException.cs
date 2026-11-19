namespace CaDesk.Application.Abstractions;

/// <summary>Input failed validation; becomes a 400 ValidationProblem with field errors.</summary>
public class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The request is not valid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}

/// <summary>A rule was broken (e.g. "can't deactivate yourself"); becomes 409 or the given status.</summary>
public class BusinessRuleException(string message, int statusCode = 409) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
