namespace LayeredTemplate.App.Domain.Exceptions;

public class AlreadyExistsException : DomainException
{
    public AlreadyExistsException(string message = "A record with the same identifier already exists.", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}