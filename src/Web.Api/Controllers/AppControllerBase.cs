using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers;

/// <summary>
/// App Controller Base
/// </summary>
public abstract class AppControllerBase : ControllerBase
{
    /// <summary>
    /// Response 200
    /// </summary>
    /// <param name="message">Message</param>
    /// <returns>Return <see cref="SuccessfulResult"/></returns>
    [NonAction]
    public OkObjectResult Response200(string? message = "Successful operation.")
    {
        return this.Ok(new SuccessfulResult()
        {
            Message = message,
        });
    }

    /// <summary>
    /// Response 400
    /// </summary>
    /// <param name="message">Message</param>
    /// <returns>Return <see cref="ErrorResult"/></returns>
    [NonAction]
    public BadRequestObjectResult Response400(string message = "Validation Error.")
    {
        return this.BadRequest(
            new ErrorResult()
            {
                Message = message,
                TraceId = this.HttpContext.TraceIdentifier,
            });
    }

    /// <summary>
    /// NotEqualIdsResponse
    /// </summary>
    /// <returns></returns>
    /// /// <returns>Return <see cref="ErrorResult"/></returns>
    [NonAction]
    public BadRequestObjectResult NotEqualIdsResponse()
    {
        return this.BadRequest(
            new ErrorResult()
            {
                Message = $"The identifiers in the path and body do not match.",
                TraceId = this.HttpContext.TraceIdentifier,
            });
    }
}