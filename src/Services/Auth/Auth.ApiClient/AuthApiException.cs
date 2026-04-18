using System.Net;

namespace LayeredTemplate.Auth.ApiClient;

/// <summary>
/// Thrown by <c>IAuthUsersClient</c> for non-success responses (other than 404 on single-item gets,
/// which return <c>null</c>).
/// </summary>
public class AuthApiException : Exception
{
    public AuthApiException(HttpStatusCode statusCode, string message, IReadOnlyList<string>? details = null)
        : base(message)
    {
        this.StatusCode = statusCode;
        this.Details = details;
    }

    public HttpStatusCode StatusCode { get; }

    public IReadOnlyList<string>? Details { get; }
}
