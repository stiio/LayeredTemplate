namespace LayeredTemplate.Domain.Exceptions;

public class ForeignKeyViolationException : DomainException
{
    public ForeignKeyViolationException(string? details = null)
        : base(string.IsNullOrEmpty(details) ? "Foreign key violation." : $"Foreign key violation. ({details})")
    {
    }
}