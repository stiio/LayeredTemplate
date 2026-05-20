using LayeredTemplate.App.Shared.Errors.Models;

namespace LayeredTemplate.App.Shared.Errors.Exceptions;

/// <summary>
/// Base class for exceptions surfaced to the API caller as 4xx ProblemDetails. Anything else
/// becomes a generic 500 with no detail leaked. Feature code throws these from handlers.
/// </summary>
public class AppMessageException : Exception
{
    public AppMessageException(string message, string? details = null)
        : base(message)
    {
        this.Details = details;
    }

    public AppMessageException(Exception innerException, string message, string? details = null)
        : base(message, innerException)
    {
        this.Details = details;
    }

    public AppErrorType ErrorType { get; protected set; } = AppErrorType.Message;

    public string? Details { get; protected set; }
}