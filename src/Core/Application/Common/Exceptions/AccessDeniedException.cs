using System.Net;

namespace LayeredTemplate.Application.Common.Exceptions;

public class AccessDeniedException : HttpStatusException
{
    public AccessDeniedException(HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base("Access denied.", statusCode)
    {
    }
}