namespace LayeredTemplate.Application.Common.Exceptions;

public class NotFoundException : HttpStatusException
{
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}