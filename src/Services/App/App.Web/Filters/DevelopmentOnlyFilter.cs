using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LayeredTemplate.App.Web.Filters;

public class DevelopmentOnlyFilter : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var apiVersionStatus = context.HttpContext.GetRequestedApiVersion()?.Status;

        if (apiVersionStatus != "dev")
        {
            return;
        }

        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}