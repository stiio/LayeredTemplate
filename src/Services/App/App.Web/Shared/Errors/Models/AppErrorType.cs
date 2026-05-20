namespace LayeredTemplate.App.Shared.Errors.Models;

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