using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers;

/// <summary>
/// App Controller Base
/// </summary>
public abstract class AppControllerBase : ControllerBase
{
    /// <summary>
    /// Successful Result
    /// </summary>
    /// <param name="message">Message</param>
    /// <returns>Return <see cref="Models.SuccessfulResult"/></returns>
    [NonAction]
    public OkObjectResult SuccessfulResult(string? message = "Successful operation.")
    {
        return this.Ok(new SuccessfulResult()
        {
            Message = message,
        });
    }

    /// <summary>
    /// Error Result
    /// </summary>
    /// <param name="message">Message</param>
    /// <returns>Return <see cref="Models.ErrorResult"/></returns>
    [NonAction]
    public BadRequestObjectResult ErrorResult(string message = "Validation Error.")
    {
        return this.BadRequest(
            new ErrorResult()
            {
                Message = message,
                TraceId = this.HttpContext.TraceIdentifier,
            });
    }
}