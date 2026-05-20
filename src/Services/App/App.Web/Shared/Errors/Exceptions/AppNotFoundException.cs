using LayeredTemplate.App.Shared.Errors.Models;

namespace LayeredTemplate.App.Shared.Errors.Exceptions;

public sealed class AppNotFoundException : AppMessageException
{
    public AppNotFoundException(string name, object key)
        : base("Not found.")
    {
        this.ErrorType = AppErrorType.NotFound;
        this.Details = $"Entity {name} with key {key} was not found.";
    }
}