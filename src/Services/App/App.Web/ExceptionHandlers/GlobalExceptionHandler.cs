using LayeredTemplate.App.Application.Common.Exceptions;
using LayeredTemplate.App.Application.Common.Models;
using LayeredTemplate.App.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace LayeredTemplate.App.Web.ExceptionHandlers;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> logger;
    private readonly IProblemDetailsService problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        this.logger = logger;
        this.problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = new AppProblemDetails()
        {
            Instance = httpContext.Request.Path,
        };

        this.HandleException(exception, problemDetails);

        await this.problemDetailsService.TryWriteAsync(new ProblemDetailsContext()
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });

        return true;
    }

    private void HandleException(Exception exception, AppProblemDetails problemDetails)
    {
        switch (exception)
        {
            case AppMessageException messageException:
            {
                this.logger.LogInformation("Message exception stack trace: {StackTrace}", messageException.StackTrace);

                problemDetails.Status = 400;
                problemDetails.Title = messageException.Message;
                problemDetails.ErrorType = messageException.ErrorType;
                problemDetails.Detail = messageException.Details;
                break;
            }

            case AppValidationException validationException:
            {
                problemDetails.Status = 400;
                problemDetails.Errors = validationException.Errors;
                problemDetails.Title = "One or more validation errors occurred.";
                problemDetails.ErrorType = AppErrorType.ValidationError;
                break;
            }

            case DomainException domainException:
            {
                this.logger.LogError(exception, "Unhandled exception occurred.");

                problemDetails.Status = 400;
                problemDetails.Title = domainException.Message;
                problemDetails.ErrorType = AppErrorType.DomainError;
                break;
            }

            case NotSupportedException e:
            {
                this.logger.LogError(exception, "Unhandled exception occurred.");

                problemDetails.Status = 400;
                problemDetails.Title = "Not supported.";
                problemDetails.ErrorType = AppErrorType.NotSupported;
                break;
            }

            case NotImplementedException e:
            {
                this.logger.LogError(exception, "Unhandled exception occurred.");

                problemDetails.Status = 501;
                problemDetails.Title = "Not implemented.";
                problemDetails.Detail = "This feature is not yet available.";
                problemDetails.ErrorType = AppErrorType.NotImplemented;
                break;
            }

            default:
            {
                this.logger.LogError(exception, "Unhandled exception occurred.");

                problemDetails.Status = 500;
                problemDetails.Title = "Internal Server Error.";
                problemDetails.ErrorType = AppErrorType.InternalServerError;
                break;
            }
        }
    }
}