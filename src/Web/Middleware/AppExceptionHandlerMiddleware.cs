using LayeredTemplate.Application.Common.Exceptions;
using LayeredTemplate.Web.Api.Models;

namespace LayeredTemplate.Web.Middleware;

public class AppExceptionHandlerMiddleware : IMiddleware
{
    private readonly ILogger<AppExceptionHandlerMiddleware> logger;
    private readonly IWebHostEnvironment webHostEnvironment;

    public AppExceptionHandlerMiddleware(
        ILogger<AppExceptionHandlerMiddleware> logger,
        IWebHostEnvironment webHostEnvironment)
    {
        this.logger = logger;
        this.webHostEnvironment = webHostEnvironment;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            await this.HandleExceptionMessageAsync(context, e);
        }
    }

    private async Task HandleExceptionMessageAsync(HttpContext context, Exception exception)
    {
        switch (exception)
        {
            case HttpStatusException httpStatusException:
            {
                var applicationError = new ErrorResult()
                {
                    Message = httpStatusException.Message,
                    TraceId = context.TraceIdentifier,
                };

                context.Response.StatusCode = (int)httpStatusException.StatusCode;
                await context.Response.WriteAsJsonAsync(applicationError).ConfigureAwait(false);
                break;
            }

            case NotImplementedException notImplementedException:
            {
                this.logger.LogError(notImplementedException, "An unhandled exception has occurred while executing the request.");

                var applicationError = new ErrorResult()
                {
                    Message = "Not implemented.",
                    TraceId = context.TraceIdentifier,
                };

                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(applicationError).ConfigureAwait(false);
                break;
            }

            default:
            {
                if (this.webHostEnvironment.IsDevelopment())
                {
                    throw new Exception("An unhandled exception has occurred while executing the request.", exception);
                }

                this.logger.LogError(exception, "An unhandled exception has occurred while executing the request.");

                var applicationError = new ErrorResult()
                {
                    Message = "Something went wrong.",
                    TraceId = context.TraceIdentifier,
                };

                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(applicationError).ConfigureAwait(false);
                break;
            }
        }
    }
}