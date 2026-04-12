namespace LayeredTemplate.App.Domain.Exceptions;

public class ForeignKeyViolationException : DomainException
{
    public ForeignKeyViolationException(string message = "Cannot delete this item because it has dependent records. Remove all related items first.", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}