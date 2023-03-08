namespace LayeredTemplate.Domain.Exceptions;

public class DuplicateUniqueColumnException : DomainException
{
    public DuplicateUniqueColumnException(string? tableName, string? columnName)
        : base($"Duplicate column {columnName} for {tableName}.")
    {
    }
}