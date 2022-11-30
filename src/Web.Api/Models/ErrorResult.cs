namespace LayeredTemplate.Web.Api.Models;

/// <summary>
/// Error Result
/// </summary>
public class ErrorResult
{
    /// <summary>
    /// Message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Trace identifier
    /// </summary>
    public string? TraceId { get; set; }
}