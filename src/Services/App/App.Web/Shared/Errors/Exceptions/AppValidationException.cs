namespace LayeredTemplate.App.Shared.Errors.Exceptions;

public sealed class AppValidationException : Exception
{
    public AppValidationException(Dictionary<string, string[]> errors)
        : base("Validation failure.")
    {
        this.Errors = errors;
    }

    public Dictionary<string, string[]> Errors { get; }
}