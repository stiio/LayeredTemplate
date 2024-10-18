using System.Net;

namespace LayeredTemplate.App.Application.Common.Exceptions;

public class HttpStatusException : Exception
{
    public HttpStatusException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        this.StatusCode = statusCode;
    }

    public HttpStatusException(
        Exception innerException,
        string message,
        HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message, innerException)
    {
        this.StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}