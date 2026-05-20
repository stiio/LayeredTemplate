using LayeredTemplate.App.Shared.Errors.Models;

namespace LayeredTemplate.App.Shared.Errors.Exceptions;

public sealed class AppAccessDeniedException : AppMessageException
{
    public AppAccessDeniedException(string? details = null)
        : base("Access denied.")
    {
        this.ErrorType = AppErrorType.AccessDenied;
        this.Details = details;
    }
}