using LayeredTemplate.App.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Extensions;

public static class CustomValidationErrorExtension
{
    public static IMvcBuilder UseCustomValidationErrorResponses(this IMvcBuilder mvcBuilder)
    {
        return mvcBuilder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = string.Join('\n', context.ModelState.Values.Where(v => v.Errors.Count > 0)
                    .SelectMany(v => v.Errors)
                    .Select(v => v.ErrorMessage));

                return new BadRequestObjectResult(new ErrorResult()
                {
                    Message = errors,
                    TraceId = context.HttpContext.TraceIdentifier,
                });
            };
        });
    }
}