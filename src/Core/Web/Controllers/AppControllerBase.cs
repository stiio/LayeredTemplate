using LayeredTemplate.Web.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Web.Controllers;

public abstract class AppControllerBase : ControllerBase
{
    protected ISender Sender => this.HttpContext.RequestServices.GetRequiredService<ISender>();

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