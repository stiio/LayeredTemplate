namespace LayeredTemplate.App.Shared.Errors;

/// <summary>
/// User-facing error code carried by <see cref="AppProblemDetails"/>. Used by client SDKs to
/// branch on the kind of failure (e.g. show a toast for <c>Message</c>, redirect on <c>NotFound</c>,
/// re-validate inputs on <c>ValidationError</c>).
/// </summary>
public enum AppErrorType
{
    InternalServerError,
    ValidationError,
    Message,
    NotFound,
    AccessDenied,
    DomainError,
    NotSupported,
    NotImplemented,
}

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

public sealed class AppNotFoundException : AppMessageException
{
    public AppNotFoundException(string name, object key)
        : base("Not found.")
    {
        this.ErrorType = AppErrorType.NotFound;
        this.Details = $"Entity {name} with key {key} was not found.";
    }
}

public sealed class AppAccessDeniedException : AppMessageException
{
    public AppAccessDeniedException(string? details = null)
        : base("Access denied.")
    {
        this.ErrorType = AppErrorType.AccessDenied;
        this.Details = details;
    }
}

public sealed class AppValidationException : Exception
{
    public AppValidationException(Dictionary<string, string[]> errors)
        : base("Validation failure.")
    {
        this.Errors = errors;
    }

    public Dictionary<string, string[]> Errors { get; }
}

/// <summary>Base class for domain-invariant violations bubbled up from the DB layer.</summary>
public class DomainException : Exception
{
    public DomainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class AlreadyExistsException : DomainException
{
    public AlreadyExistsException(string message = "A record with the same identifier already exists.", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class ForeignKeyViolationException : DomainException
{
    public ForeignKeyViolationException(string message = "Cannot delete this item because it has dependent records. Remove all related items first.", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
