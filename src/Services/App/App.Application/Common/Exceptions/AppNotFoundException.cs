using LayeredTemplate.App.Application.Common.Models;

namespace LayeredTemplate.App.Application.Common.Exceptions;

public class AppNotFoundException : AppMessageException
{
    public AppNotFoundException(string name, object key)
        : base($"Not found.")
    {
        this.ErrorType = AppErrorType.NotFound;
        this.Details = $"Entity {name} with key {key} was not found.";
    }
}