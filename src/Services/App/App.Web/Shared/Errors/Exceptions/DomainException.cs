namespace LayeredTemplate.App.Shared.Errors.Exceptions;

/// <summary>Base class for domain-invariant violations bubbled up from the DB layer.</summary>
public class DomainException : Exception
{
    public DomainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}