namespace LayeredTemplate.App.Application.Common.Exceptions;

public class AppNotFoundException : HttpStatusException
{
    public AppNotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}