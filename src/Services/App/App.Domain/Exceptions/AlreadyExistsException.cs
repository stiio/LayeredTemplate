namespace LayeredTemplate.App.Domain.Exceptions;

public class AlreadyExistsException : DomainException
{
    public AlreadyExistsException(string message = "Already exists.", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}