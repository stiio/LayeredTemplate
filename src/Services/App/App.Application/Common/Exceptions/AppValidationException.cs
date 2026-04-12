namespace LayeredTemplate.App.Application.Common.Exceptions;

public class AppValidationException : Exception
{
    public AppValidationException(Dictionary<string, string[]> errors)
        : base("Validation failure.")
    {
        this.Errors = errors;
    }

    public Dictionary<string, string[]> Errors { get; private set; }
}