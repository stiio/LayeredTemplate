using LayeredTemplate.App.Web.Models;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Controllers;

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
}