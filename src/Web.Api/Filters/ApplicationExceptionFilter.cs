using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Web.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Web.Api.Filters;

/// <inheritdoc />
public class ApplicationExceptionFilter : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ApplicationExceptionFilter>>();
        switch (context.Exception)
        {
            case HttpStatusException httpStatusException:
            {
                context.ExceptionHandled = true;

                var applicationError = new ErrorResult()
                {
                    Message = httpStatusException.Message,
                    TraceId = context.HttpContext.TraceIdentifier,
                };

                context.Result = new ObjectResult(applicationError)
                {
                    StatusCode = (int)httpStatusException.StatusCode,
                };
                break;
            }

            case NotImplementedException e:
            {
                logger.LogError(e, "An unhandled exception has occurred while executing the request.");

                context.ExceptionHandled = true;

                var applicationError = new ErrorResult()
                {
                    Message = "Not implemented.",
                    TraceId = context.HttpContext.TraceIdentifier,
                };

                context.Result = new BadRequestObjectResult(applicationError);
                break;
            }

            // ReSharper disable once ConvertTypeCheckPatternToNullCheck
            case Exception e:
            {
                var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                if (env.IsDevelopment())
                {
                    break;
                }

                logger.LogError(e, "An unhandled exception has occurred while executing the request.");

                context.ExceptionHandled = true;

                var applicationError = new ErrorResult()
                {
                    Message = "Something went wrong.",
                    TraceId = context.HttpContext.TraceIdentifier,
                };

                context.Result = new ObjectResult(applicationError)
                {
                    StatusCode = 500,
                };
                break;
            }
        }
    }
}