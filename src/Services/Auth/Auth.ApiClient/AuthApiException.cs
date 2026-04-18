using System.Net;

namespace LayeredTemplate.Auth.ApiClient;

/// <summary>
/// Thrown by <c>IAuthUsersClient</c> for non-success responses (other than 404 on single-item gets,
/// which return <c>null</c>).
/// </summary>
/// <remarks>
/// The Auth API uses RFC 7807 Problem Details. For 400 validation failures the server returns
/// <c>ValidationProblemDetails</c> with a per-field <c>errors</c> dictionary; this is exposed as
/// <see cref="Errors"/>. For other failures (401, 403, 409, 5xx) only <see cref="Title"/>/<see cref="Detail"/>
/// are populated and <see cref="Errors"/> is <c>null</c>.
/// </remarks>
public class AuthApiException : Exception
{
    public AuthApiException(
        HttpStatusCode statusCode,
        string message,
        string? title = null,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        this.StatusCode = statusCode;
        this.Title = title;
        this.Detail = detail;
        this.Errors = errors;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>Short human-readable summary of the problem type (ProblemDetails <c>title</c>).</summary>
    public string? Title { get; }

    /// <summary>Human-readable explanation specific to this occurrence (ProblemDetails <c>detail</c>).</summary>
    public string? Detail { get; }

    /// <summary>
    /// Field → messages map from <c>ValidationProblemDetails.errors</c>. <c>null</c> when the response
    /// was not a validation problem. The empty-string key holds top-level errors not tied to a specific field.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}
