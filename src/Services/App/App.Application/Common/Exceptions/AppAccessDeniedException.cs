using LayeredTemplate.App.Application.Common.Models;

namespace LayeredTemplate.App.Application.Common.Exceptions;

public class AppAccessDeniedException : AppMessageException
{
    public AppAccessDeniedException(string? details)
        : base("Access denied.")
    {
        this.ErrorType = AppErrorType.AccessDenied;
        this.Details = details;
    }
}