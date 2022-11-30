using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Web.Api.Extensions;

/// <summary>
/// CustomValidationErrorExtension
/// </summary>
public static class CustomValidationErrorExtension
{
    /// <summary>
    /// Use Custom Validation Error Responses
    /// </summary>
    /// <param name="mvcBuilder"></param>
    /// <returns></returns>
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