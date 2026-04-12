using LayeredTemplate.App.Application.Common.Models;

namespace LayeredTemplate.App.Application.Common.Exceptions;

public class AppMessageException : Exception
{
    public AppMessageException(
        string message,
        string? details = null)
        : base(message)
    {
        this.Details = details;
    }

    public AppMessageException(
        Exception innerException,
        string message,
        string? details = null)
        : base(message, innerException)
    {
        this.Details = details;
    }

    public AppErrorType ErrorType { get; protected set; } = AppErrorType.Message;

    public string? Details { get; protected set; }
}