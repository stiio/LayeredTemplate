﻿using LayeredTemplate.App.Application.Common.Exceptions;
using LayeredTemplate.App.Domain.Exceptions;
using LayeredTemplate.App.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LayeredTemplate.App.Web.Filters;

internal class ApplicationExceptionFilter : IExceptionFilter
{
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

            case DomainException domainException:
            {
                context.ExceptionHandled = true;

                var applicationError = new ErrorResult()
                {
                    Message = domainException.Message,
                    TraceId = context.HttpContext.TraceIdentifier,
                };

                context.Result = new BadRequestObjectResult(applicationError);
                break;
            }

            case NotSupportedException e:
            {
                logger.LogError(e, "An unhandled exception has occurred while executing the request.");

                context.ExceptionHandled = true;

                var applicationError = new ErrorResult()
                {
                    Message = "Not supported.",
                    TraceId = context.HttpContext.TraceIdentifier,
                };

                context.Result = new BadRequestObjectResult(applicationError);
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