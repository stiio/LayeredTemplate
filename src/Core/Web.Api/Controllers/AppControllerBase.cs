using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Api.Controllers;

public abstract class AppControllerBase : ControllerBase
{
    [NonAction]
    public OkObjectResult SuccessfulResult(string? message = "Successful operation.")
    {
        return this.Ok(new SuccessfulResult()
        {
            Message = message,
        });
    }

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