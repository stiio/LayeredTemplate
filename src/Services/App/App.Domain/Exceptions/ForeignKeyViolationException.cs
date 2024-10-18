namespace LayeredTemplate.App.Domain.Exceptions;

public class ForeignKeyViolationException : DomainException
{
    public ForeignKeyViolationException(string message = "Foreign key violation.", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}