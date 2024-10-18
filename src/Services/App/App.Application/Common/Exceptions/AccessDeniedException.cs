using System.Net;

namespace LayeredTemplate.App.Application.Common.Exceptions;

public class AccessDeniedException : HttpStatusException
{
    public AccessDeniedException(HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base("Access denied.", statusCode)
    {
    }
}